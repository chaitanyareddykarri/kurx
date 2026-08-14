import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:kurx_mobile/core/theme/app_theme.dart';
import 'package:kurx_mobile/features/auth/domain/entities/current_user.dart';
import 'package:kurx_mobile/features/auth/presentation/providers/auth_providers.dart';
import 'package:kurx_mobile/features/events/domain/entities/event_category.dart';
import 'package:kurx_mobile/features/organizer/presentation/pages/create_event_gate_page.dart';

/// D-305 — the Create-Event gate, mirroring `web/test/create-event-gate.test.tsx`.
///
/// The same two properties are pinned on both clients, because the same two regressions are possible
/// on both:
///
///   1. **The form cannot be reached without passing the gate.** On Flutter this is a routing property
///      — the form lives at `/events/create/form`, behind `/events/create` — so what is asserted here
///      is that the gate does not navigate until a product is chosen.
///   2. **The gate never blocks a free event.** `canOrganizeFree` is true for any account; gating
///      creation on identity would lock out every unverified organiser from something the platform
///      explicitly allows.
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
      ),
    );

void main() {
  group('CreateEventGatePage', () {
    testWidgets('opens on eligibility, not on the form', (tester) async {
      await tester.pumpWidget(_host(const CreateEventGatePage(), user: _user(canOrganizePaid: false)));
      await tester.pumpAndSettle();

      expect(find.text('Before you start'), findsOneWidget);
      expect(find.text('What kind of event is this?'), findsNothing);
    });

    testWidgets('lets an unverified account through — a free event needs no verification',
        (tester) async {
      await tester.pumpWidget(_host(const CreateEventGatePage(), user: _user(canOrganizePaid: false)));
      await tester.pumpAndSettle();

      // The load-bearing assertion, identical to web's. If this fails, creation has been gated on
      // identity and every unverified organiser is locked out.
      await tester.tap(find.text('Continue'));
      await tester.pumpAndSettle();

      expect(find.text('What kind of event is this?'), findsOneWidget);
    });

    testWidgets('says paid hosting is what needs verification, not creation', (tester) async {
      await tester.pumpWidget(_host(const CreateEventGatePage(), user: _user(canOrganizePaid: false)));
      await tester.pumpAndSettle();

      expect(find.textContaining("don't need any of this to create a free event"), findsOneWidget);
    });

    testWidgets('shows paid hosting as available once the account is paid-capable', (tester) async {
      await tester.pumpWidget(_host(const CreateEventGatePage(), user: _user(canOrganizePaid: true)));
      await tester.pumpAndSettle();

      expect(find.textContaining('Identity, PAN and bank account are verified'), findsOneWidget);
      expect(find.textContaining("don't need any of this"), findsNothing);
    });

    testWidgets('does not leave the gate until a product is chosen', (tester) async {
      await tester.pumpWidget(_host(const CreateEventGatePage(), user: _user(canOrganizePaid: false)));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Continue'));
      await tester.pumpAndSettle();

      // Continue is inert with no product selected: tapping it must leave us on the product step
      // rather than pushing the form route.
      await tester.tap(find.text('Continue'));
      await tester.pumpAndSettle();

      expect(find.text('What kind of event is this?'), findsOneWidget);
    });

    // D-307 — Public requires the full set, free or paid. Mirrors web's block of the same name.

    testWidgets('blocks Public when the account is not verified, and names what is missing',
        (tester) async {
      await tester.pumpWidget(_host(const CreateEventGatePage(),
          user: _user(canOrganizePaid: false, canCreatePublicEvent: false)));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Continue'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Public'));
      await tester.pumpAndSettle();

      expect(find.textContaining('Verify your identity to host public events'), findsOneWidget);
      // Penny drop is named because it is the bank-OWNERSHIP link, not a separate predicate.
      expect(find.textContaining('penny drop'), findsOneWidget);
    });

    testWidgets('lets a completely unverified account choose PRIVATE', (tester) async {
      await tester.pumpWidget(_host(const CreateEventGatePage(),
          user: _user(canOrganizePaid: false, canCreatePublicEvent: false)));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Continue'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Private'));
      await tester.pumpAndSettle();

      expect(find.textContaining('Private events need no financial verification'), findsOneWidget);
    });

    testWidgets('never asks a Private host for penny drop', (tester) async {
      await tester.pumpWidget(_host(const CreateEventGatePage(),
          user: _user(canOrganizePaid: false, canCreatePublicEvent: false)));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Continue'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Private'));
      await tester.pumpAndSettle();

      // A private event cannot take payment, so a bank requirement would be proof of something that
      // can never happen.
      expect(find.textContaining('penny drop'), findsNothing);
    });

    testWidgets('can go back from the product step', (tester) async {
      await tester.pumpWidget(_host(const CreateEventGatePage(), user: _user(canOrganizePaid: false)));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Continue'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Back'));
      await tester.pumpAndSettle();

      expect(find.text('Before you start'), findsOneWidget);
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
