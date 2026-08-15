import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:kurx_mobile/core/theme/app_theme.dart';
import 'package:kurx_mobile/features/auth/domain/entities/current_user.dart';
import 'package:kurx_mobile/features/auth/presentation/providers/auth_providers.dart';
import 'package:kurx_mobile/features/events/domain/entities/event_category.dart';
import 'package:kurx_mobile/features/organizer/presentation/pages/create_event_gate_page.dart';

/// D-305 — the Create-Event gate. D-343 — the two verification tiers.
/// Mirrors `web/test/create-event-gate.test.tsx`.
///
/// The same properties are pinned on both clients, because the same regressions are possible on both:
///
///   1. **The form cannot be reached without passing the gate.** On Flutter this is a routing property
///      — the form lives at `/events/create/form`, behind `/events/create` — so what is asserted here
///      is that the gate does not navigate until the questions are answered.
///   2. **A free public event must not be asked for bank details.** That was the D-307 behaviour and
///      D-343 removed it: bank ownership answers "whose account receives money", which a free event
///      never asks.
///   3. **Paid stays behind the financial tier**, and a Private event can never reach it at all.
///
/// The Type-filtering rule is asserted against `EventCategory.allowsProduct` — the same predicate web
/// tests as `typesFor`. If the two ever disagree, one client offers a catalogue the other refuses.

Widget _host(Widget child, {CurrentUser? user}) => ProviderScope(
      overrides: [currentUserProvider.overrideWith((ref) => user)],
      child: MaterialApp(
        theme: AppTheme.light(),
        home: child,
      ),
    );

CurrentUser _user({
  required bool canOrganizePaid,
  bool canCreatePublicEvent = false,
  bool canCreatePrivateEvent = true,
  bool identityVerified = false,
  bool bankVerified = false,
}) =>
    CurrentUser(
      id: 'u1',
      phone: '+919876500123',
      name: 'Audit Organiser',
      needsOnboarding: false,
      trust: TrustCapabilities(
        canOrganizePaid: canOrganizePaid,
        canCreatePublicEvent: canCreatePublicEvent,
        canCreatePrivateEvent: canCreatePrivateEvent,
        identityVerified: identityVerified,
        bankVerified: bankVerified,
      ),
    );

/// Product step → pricing step.
Future<void> _chooseProduct(WidgetTester tester, String which) async {
  await tester.tap(find.text(which));
  await tester.pumpAndSettle();
  await tester.tap(find.text('Continue'));
  await tester.pumpAndSettle();
}

void main() {
  group('CreateEventGatePage', () {
    testWidgets('opens on the product question, not on the form', (tester) async {
      await tester.pumpWidget(_host(const CreateEventGatePage(), user: _user(canOrganizePaid: false)));
      await tester.pumpAndSettle();

      expect(find.text('Who is this event for?'), findsOneWidget);
      expect(find.text('Are you charging for tickets?'), findsNothing);
    });

    testWidgets('does not leave the product step until a product is chosen', (tester) async {
      await tester.pumpWidget(_host(const CreateEventGatePage(), user: _user(canOrganizePaid: false)));
      await tester.pumpAndSettle();

      // Continue is inert with no product selected.
      await tester.tap(find.text('Continue'));
      await tester.pumpAndSettle();

      expect(find.text('Who is this event for?'), findsOneWidget);
    });

    // ── D-343 · the identity tier gates Public ──────────────────────────────

    testWidgets('asks a free public event for identity only, never for bank details', (tester) async {
      await tester.pumpWidget(_host(const CreateEventGatePage(),
          user: _user(canOrganizePaid: false, canCreatePublicEvent: true, identityVerified: true)));
      await tester.pumpAndSettle();
      await _chooseProduct(tester, 'Public');

      // Free is the default answer, so this is the state the person lands in.
      expect(find.textContaining('What a free public event needs'), findsOneWidget);
      expect(find.textContaining('Government ID or PAN approved'), findsOneWidget);
      expect(find.textContaining('penny drop'), findsNothing);
      expect(find.textContaining('Bank account approved'), findsNothing);
    });

    testWidgets('blocks Public when identity is not verified', (tester) async {
      await tester.pumpWidget(_host(const CreateEventGatePage(),
          user: _user(canOrganizePaid: false, canCreatePublicEvent: false)));
      await tester.pumpAndSettle();
      await _chooseProduct(tester, 'Public');

      // Still on the gate — Continue is inert, so nothing was pushed.
      await tester.tap(find.text('Continue'));
      await tester.pumpAndSettle();
      expect(find.text('Are you charging for tickets?'), findsOneWidget);
      expect(find.textContaining('Complete verification'), findsOneWidget);
    });

    // ── D-343 · the financial tier gates Paid ───────────────────────────────

    testWidgets('names the bank-ownership links only once Paid is chosen', (tester) async {
      await tester.pumpWidget(_host(const CreateEventGatePage(),
          user: _user(canOrganizePaid: true, canCreatePublicEvent: true, identityVerified: true)));
      await tester.pumpAndSettle();
      await _chooseProduct(tester, 'Public');
      await tester.tap(find.text('Paid'));
      await tester.pumpAndSettle();

      expect(find.textContaining('What selling tickets needs'), findsOneWidget);
      // Penny drop and name match are the bank-OWNERSHIP links inside bankVerified.
      expect(find.textContaining('penny drop'), findsOneWidget);
    });

    testWidgets('does not let Paid past the gate without the financial tier, even when Public is open',
        (tester) async {
      // The exact D-343 shape: identity cleared, bank not. Public is allowed; selling is not.
      await tester.pumpWidget(_host(const CreateEventGatePage(),
          user: _user(canOrganizePaid: false, canCreatePublicEvent: true, identityVerified: true)));
      await tester.pumpAndSettle();
      await _chooseProduct(tester, 'Public');
      await tester.tap(find.text('Paid'));
      await tester.pumpAndSettle();

      await tester.tap(find.text('Continue'));
      await tester.pumpAndSettle();
      expect(find.text('Are you charging for tickets?'), findsOneWidget);
    });

    // ── Private ─────────────────────────────────────────────────────────────

    testWidgets('lets a completely unverified account choose PRIVATE', (tester) async {
      await tester.pumpWidget(_host(const CreateEventGatePage(),
          user: _user(canOrganizePaid: false, canCreatePublicEvent: false)));
      await tester.pumpAndSettle();
      await _chooseProduct(tester, 'Private');

      expect(find.textContaining('Nothing to verify'), findsOneWidget);
    });

    testWidgets('never asks a Private host for identity or bank details', (tester) async {
      await tester.pumpWidget(_host(const CreateEventGatePage(),
          user: _user(canOrganizePaid: false, canCreatePublicEvent: false)));
      await tester.pumpAndSettle();
      await _chooseProduct(tester, 'Private');

      expect(find.textContaining('penny drop'), findsNothing);
      expect(find.textContaining('Government ID or PAN approved'), findsNothing);
    });

    testWidgets('cannot sell tickets on a Private event', (tester) async {
      await tester.pumpWidget(_host(const CreateEventGatePage(),
          user: _user(canOrganizePaid: true, canCreatePublicEvent: true)));
      await tester.pumpAndSettle();
      await _chooseProduct(tester, 'Private');

      // The card is present but inert — a missing card answers nothing.
      expect(find.textContaining('Not available for a private event'), findsOneWidget);
    });

    // ── D-323 · the bypass must not be restated as a verification ───────────

    testWidgets('does not claim a verification that never happened when the gate is merely bypassed',
        (tester) async {
      await tester.pumpWidget(_host(const CreateEventGatePage(),
          user: _user(canOrganizePaid: true, canCreatePublicEvent: true, identityVerified: false)));
      await tester.pumpAndSettle();
      await _chooseProduct(tester, 'Public');

      expect(find.textContaining('Enabled without verification'), findsOneWidget);
      expect(find.textContaining('Nothing about your identity has been confirmed'), findsOneWidget);
    });

    testWidgets('can go back from the pricing step', (tester) async {
      await tester.pumpWidget(_host(const CreateEventGatePage(),
          user: _user(canOrganizePaid: false, canCreatePublicEvent: true, identityVerified: true)));
      await tester.pumpAndSettle();
      await _chooseProduct(tester, 'Public');
      await tester.tap(find.text('Back'));
      await tester.pumpAndSettle();

      expect(find.text('Who is this event for?'), findsOneWidget);
    });
  });

  /// Must stay identical to web's `typesFor`. Two clients, one rule.
  group('EventCategory.allowsProduct', () {
    const wedding = EventCategory(id: 'w', name: 'Wedding', productClass: 'Private');
    const meetup = EventCategory(id: 'm', name: 'Meetup', productClass: 'Public');
    const legacy = EventCategory(id: 'l', name: 'Unclassified');

    test('a Private type is offered only for Private', () {
      expect(wedding.allowsProduct('Private'), isTrue);
      expect(wedding.allowsProduct('Public'), isFalse);
    });

    test('a Public type is offered only for Public', () {
      expect(meetup.allowsProduct('Public'), isTrue);
      expect(meetup.allowsProduct('Private'), isFalse);
    });

    test('an unclassified type counts as Public, matching the server fallback', () {
      // ResolveArchetypeAsync returns EventProduct.Public when ProductClass is null. A client that
      // disagreed would offer a Type that then produced an event of the other class.
      expect(legacy.allowsProduct('Public'), isTrue);
      expect(legacy.allowsProduct('Private'), isFalse);
    });
  });
}
