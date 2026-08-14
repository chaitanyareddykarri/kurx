import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:kurx_mobile/core/router/app_router.dart';
import 'package:kurx_mobile/core/session/session_controller.dart';
import 'package:kurx_mobile/features/auth/presentation/pages/guest_gate_page.dart';
import 'package:kurx_mobile/features/auth/presentation/providers/auth_providers.dart';
import 'package:kurx_mobile/features/events/domain/entities/event_category.dart';
import 'package:kurx_mobile/features/events/domain/entities/event_detail.dart';
import 'package:kurx_mobile/features/events/domain/entities/event_kind.dart';
import 'package:kurx_mobile/features/events/domain/entities/event_query.dart';
import 'package:kurx_mobile/features/events/domain/entities/event_summary.dart';
import 'package:kurx_mobile/features/events/domain/entities/paged_events.dart';
import 'package:kurx_mobile/features/events/domain/entities/ticket_type.dart';
import 'package:kurx_mobile/features/events/domain/repositories/events_repository.dart';
import 'package:kurx_mobile/features/events/presentation/providers/events_providers.dart';
import 'package:kurx_mobile/features/shell/presentation/app_shell.dart';

class _FakeSession extends SessionController {
  _FakeSession(this._state);
  final SessionState _state;
  @override
  SessionState build() => _state;
}

class _FakeEventsRepo implements EventsRepository {
  static const _sample = [EventSummary(id: '1', title: 'Sunburn Arena', slug: 'sunburn', status: 'published')];

  @override
  Future<List<EventSummary>> upcoming({int limit = 20}) async => _sample;
  @override
  Future<List<EventSummary>> featured({int limit = 20}) async => const [];
  @override
  Future<List<EventSummary>> trending({int limit = 20}) async => _sample;
  @override
  Future<List<EventSummary>> latest({int limit = 20}) async => _sample;
  @override
  Future<PagedEvents> search(EventQuery query) async => const PagedEvents(items: [], total: 0);
  @override
  Future<List<EventCategory>> categories() async => const [EventCategory(id: 'c1', name: 'Music')];
  @override
  Future<List<EventCategory>> eventTypes() async => const [];
  @override
  Future<List<EventKind>> kinds() async => const [];
  @override
  Future<List<EventSummary>> forYou({int limit = 10}) async => const [];
  @override
  Future<EventDetail> detail(String slug) => throw UnimplementedError();
  @override
  Future<List<EventSummary>> related(String slug, {int limit = 6}) => throw UnimplementedError();
  @override
  Future<List<TicketType>> ticketTypes(String eventId) => throw UnimplementedError();
}

List<Override> _overrides(SessionState session, {String? returnTo}) => [
      sessionControllerProvider.overrideWith(() => _FakeSession(session)),
      eventsRepositoryProvider.overrideWithValue(_FakeEventsRepo()),
      if (returnTo != null) loginReturnToProvider.overrideWith((ref) => returnTo),
    ];

void main() {
  testWidgets('a guest launches straight into browsing — no login wall', (tester) async {
    final container = ProviderContainer(overrides: _overrides(const SessionState(AuthStatus.unauthenticated)));
    addTearDown(container.dispose);
    await tester.pumpWidget(UncontrolledProviderScope(
      container: container,
      child: MaterialApp.router(routerConfig: container.read(routerProvider)),
    ));
    await tester.pumpAndSettle();

    expect(find.widgetWithText(AppBar, 'Home'), findsOneWidget); // discovery home, not login
    expect(find.text('Browsing as guest'), findsOneWidget); // guest mode shown
    expect(find.widgetWithText(TextButton, 'Sign in'), findsOneWidget); // login CTA
    expect(find.text('Send code'), findsNothing); // login page NOT forced
  });

  testWidgets('an authenticated user sees no guest banner, and sign-out under Profile', (tester) async {
    final container = ProviderContainer(overrides: _overrides(const SessionState(AuthStatus.authenticated)));
    addTearDown(container.dispose);
    await tester.pumpWidget(UncontrolledProviderScope(
      container: container,
      child: MaterialApp.router(routerConfig: container.read(routerProvider)),
    ));
    await tester.pumpAndSettle();

    expect(find.widgetWithText(AppBar, 'Home'), findsOneWidget);
    expect(find.text('Browsing as guest'), findsNothing);

    // Sign-out moved off the Home app bar when Profile became the top-left destination: Profile
    // already owned a confirmed sign-out, so the bar's unconfirmed duplicate was the one to drop.
    // Reached here the way a user reaches it — the profile avatar in the shared shell bar.
    //
    // Bounded pumps, not pumpAndSettle: Profile fetches over a network this test has none of, so it
    // never reaches a settled frame. The nav is what is under test, and it has already rendered.
    await tester.tap(find.byTooltip('Profile'));
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 400));

    // Sign-out sits near the bottom of Profile's ListView, which builds lazily — it has to be
    // scrolled to before it exists to find.
    await tester.scrollUntilVisible(find.text('Sign out'), 200, maxScrolls: 20);
    expect(find.text('Sign out'), findsOneWidget);
  });

  // Authenticated, where it used to be a guest: Community / Posts / Messages / Workspace all call
  // authenticated endpoints, so they are now behind the router's sign-in gate and a guest tapping
  // them lands on login (asserted in the next test). What this one covers is the shell's branch
  // switching, which is orthogonal to auth — so it runs as the user who can actually reach all five.
  testWidgets('the bottom nav shell switches branches', (tester) async {
    final container = ProviderContainer(overrides: _overrides(const SessionState(AuthStatus.authenticated)));
    addTearDown(container.dispose);
    await tester.pumpWidget(UncontrolledProviderScope(
      container: container,
      child: MaterialApp.router(routerConfig: container.read(routerProvider)),
    ));
    await tester.pumpAndSettle();

    // The shell wraps the branches in the custom floating pill nav (D-057) — icon-only, tab order
    // Home / Community / Posts / Messages / Workspace, the product flow's Main Application areas.
    // Profile and Notifications are app-bar corners, not tabs. Assert the shell is present and we
    // start on the Home branch.
    expect(find.byType(AppShell), findsOneWidget);
    expect(find.widgetWithText(AppBar, 'Home'), findsOneWidget); // Home branch active

    // Switching tabs: labels are not rendered, so tap the unselected (outline) icon; this drives the
    // real _KurxNavBar.onTap → navigationShell.goBranch.
    //
    // Bounded pumps, not pumpAndSettle: these branches fetch over a network this test has none of,
    // so they never reach a settled frame. The app bar title renders on the first frame either way,
    // and it is what identifies the active branch.
    Future<void> tapTab(IconData icon) async {
      await tester.tap(find.byIcon(icon));
      await tester.pump();
      await tester.pump(const Duration(milliseconds: 400));
    }

    await tapTab(Icons.people_outline_rounded);
    expect(find.widgetWithText(AppBar, 'Community'), findsOneWidget);

    await tapTab(Icons.dashboard_outlined);
    expect(find.widgetWithText(AppBar, 'Workspace'), findsOneWidget);

    await tapTab(Icons.article_outlined);
    expect(find.widgetWithText(AppBar, 'Posts'), findsOneWidget);
  });

  // The regression this gate exists for: a guest could open any protected screen, which then called
  // an authenticated endpoint, took a 401 and rendered its generic "something went wrong". Being
  // signed out is not a failure, and it is not a reason to drop someone into a password field
  // either — the gate names the feature and offers a way in *or* back.
  testWidgets('a guest opening a protected tab gets an explained gate', (tester) async {
    final container = ProviderContainer(overrides: _overrides(const SessionState(AuthStatus.unauthenticated)));
    addTearDown(container.dispose);
    await tester.pumpWidget(UncontrolledProviderScope(
      container: container,
      child: MaterialApp.router(routerConfig: container.read(routerProvider)),
    ));
    await tester.pumpAndSettle();

    await tester.tap(find.byIcon(Icons.forum_outlined)); // Messages
    await tester.pumpAndSettle();

    expect(find.widgetWithText(AppBar, 'Messages'), findsOneWidget); // the feature, named
    expect(find.text('Sign in to use this feature'), findsOneWidget);
    expect(find.widgetWithText(FilledButton, 'Sign in'), findsOneWidget);
    expect(find.widgetWithText(OutlinedButton, 'Create account'), findsOneWidget);
    expect(find.text('Welcome to Kurx'), findsNothing); // the login form is NOT forced

    // Arriving by redirect leaves nothing on the stack to pop, so dismissing has to return to
    // browsing under its own steam rather than closing the app.
    await tester.tap(find.text('Not now — keep browsing'));
    await tester.pumpAndSettle();
    expect(find.widgetWithText(AppBar, 'Home'), findsOneWidget);
  });

  testWidgets('the gate hands off to the existing login route', (tester) async {
    final container = ProviderContainer(overrides: _overrides(const SessionState(AuthStatus.unauthenticated)));
    addTearDown(container.dispose);
    await tester.pumpWidget(UncontrolledProviderScope(
      container: container,
      child: MaterialApp.router(routerConfig: container.read(routerProvider)),
    ));
    await tester.pumpAndSettle();

    await tester.tap(find.byIcon(Icons.dashboard_outlined)); // Workspace
    await tester.pumpAndSettle();
    expect(find.widgetWithText(AppBar, 'Workspace'), findsOneWidget);

    await tester.tap(find.widgetWithText(FilledButton, 'Sign in'));
    await tester.pumpAndSettle();

    expect(find.text('Welcome to Kurx'), findsOneWidget); // the one real login screen
    // Pushed, not replaced, so the gate is still behind it and back works.
    expect(container.read(loginReturnToProvider), Routes.workspace);

    // Sign-in is not a dead end for someone who has no account yet. The screen shipped with only
    // "Use a one-time code instead", which no new user reads as "sign up".
    expect(find.text('New to Kurx? Create an account'), findsOneWidget);
    await tester.tap(find.text('New to Kurx? Create an account'));
    await tester.pumpAndSettle();
    expect(find.widgetWithText(AppBar, 'Continue with a code'), findsOneWidget);
  });

  test('the gate names the feature behind each protected route', () {
    expect(GuestGatePage.featureFor(Routes.messages).title, 'Messages');
    expect(GuestGatePage.featureFor(Routes.workspace).title, 'Workspace');
    expect(GuestGatePage.featureFor('/events/create').title, 'Creating events');
    // Longest prefix wins, so a nested route is not claimed by a shorter sibling.
    expect(GuestGatePage.featureFor('/profile/devices').title, 'Your profile');
    expect(GuestGatePage.featureFor('/something-new').title, 'This feature');
  });

  // go_router takes the FIRST route that matches, not the most specific one, so every literal
  // `/events/<word>` has to be declared before the `/events/:slug` wildcard. `/events/create` was
  // declared 370 lines after it: Create Event opened the event *detail* page for a slug named
  // "create", which fetched `GET /v1/events/create`, took a 404 and showed "We couldn't find that."
  test('literal /events/ routes are declared before the :slug wildcard', () {
    final container = ProviderContainer(overrides: _overrides(const SessionState(AuthStatus.authenticated)));
    addTearDown(container.dispose);
    final paths = container
        .read(routerProvider)
        .configuration
        .routes
        .whereType<GoRoute>()
        .map((r) => r.path)
        .where((p) => p.startsWith('/events/'))
        .toList();

    final wildcard = paths.indexOf('/events/:slug');
    expect(wildcard, isNot(-1), reason: '/events/:slug must stay a top-level route');
    for (final literal in paths.where((p) => !p.split('/')[2].startsWith(':'))) {
      expect(paths.indexOf(literal), lessThan(wildcard),
          reason: '$literal is shadowed by /events/:slug and can never match');
    }
  });

  test('the public allowlist fails closed', () {
    for (final loc in [
      Routes.splash, Routes.events, Routes.search, Routes.categories,
      Routes.settings, Routes.profile, Routes.login, Routes.loginOtp,
      Routes.recover, Routes.resetPassword,
      '/events/sunburn-2026', '/discover/trending', '/categories/c1',
      '/u/asha', '/legal/terms', '/help',
    ]) {
      expect(isPublicRoute(loc), isTrue, reason: '$loc must stay browsable signed-out');
    }
    // `/events/create` shares the shape of an event slug but is the creation gate (D-305); the
    // settings and profile roots are guest-aware while everything nested under them is not.
    for (final loc in [
      '/events/create', '/events/create/form?product=Public',
      '/events/e1/manage', '/events/sunburn/checkout/e1/t1',
      Routes.tickets, Routes.saved, Routes.workspace, Routes.messages,
      Routes.posts, Routes.community, Routes.notifications, Routes.calendar,
      Routes.security, Routes.accountSettings, '/profile/edit', '/profile/devices',
      '/orders', '/certificates', '/groups', '/representing',
    ]) {
      expect(isPublicRoute(loc), isFalse, reason: '$loc must require a session');
    }
  });

  testWidgets('after auth, the router returns to the remembered origin', (tester) async {
    final container = ProviderContainer(
      overrides: _overrides(const SessionState(AuthStatus.authenticated), returnTo: Routes.search),
    );
    addTearDown(container.dispose);
    final router = container.read(routerProvider);
    await tester.pumpWidget(UncontrolledProviderScope(
      container: container,
      child: MaterialApp.router(routerConfig: router),
    ));
    await tester.pumpAndSettle();

    // Navigating to login while authenticated bounces to the saved return location.
    router.go(Routes.login);
    await tester.pumpAndSettle();
    expect(find.widgetWithText(AppBar, 'Search events'), findsOneWidget); // landed on search
    expect(find.widgetWithText(OutlinedButton, 'Filters'), findsOneWidget); // filter bar present
    expect(find.text('Send code'), findsNothing); // not the login page
  });
}
