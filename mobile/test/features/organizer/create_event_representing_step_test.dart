import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:kurx_mobile/common/widgets/kurx_button.dart';
import 'package:kurx_mobile/core/theme/app_theme.dart';
import 'package:kurx_mobile/features/organizer/data/models/org_dto.dart';
import 'package:kurx_mobile/features/organizer/presentation/pages/create_event_page.dart';
import 'package:kurx_mobile/features/organizer/presentation/providers/event_content_providers.dart';
import 'package:kurx_mobile/features/organizer/presentation/providers/organizer_providers.dart';

/// D-382 — the create-event wizard's Representing step, on Flutter.
///
/// Two defects are pinned here, and the first one made the screen unusable rather than merely awkward:
///
///   1. **A private event could not be created at all.** `_stepErrors[_Step.representing]` has demanded
///      a valid representation for every product since D-379, but the step still rendered the retired
///      "Hosted by you … there's no organisation to name and nothing to verify" branch with NO picker.
///      Nothing to answer, and a Continue that could never enable.
///   2. **The letter was asked on step twelve**, after Legal — so an organiser learned on the last step
///      that the first one was incomplete. It is asked here now, with the organization it authorises.
void main() {
  const org = RepresentationDto(
    organizationId: 'org-1',
    name: 'NSRIT College',
    authority: 'representative',
    isVerified: true,
    canBackPaidEvent: true,
  );

  Widget host(String product, {List<RepresentationDto> reps = const [org]}) => ProviderScope(
        overrides: [
          myRepresentationsProvider.overrideWith((_) async => reps),
          representativeRolesProvider.overrideWith((_) async => const ['Principal', 'Other']),
        ],
        child: MaterialApp(
          theme: AppTheme.light(),
          home: CreateEventPage(product: product),
        ),
      );

  for (final product in const ['Public', 'Private']) {
    testWidgets('a $product event opens on Representing and can answer it', (tester) async {
      await tester.pumpWidget(host(product));
      await tester.pumpAndSettle();

      expect(tester.takeException(), isNull);
      // The picker — the thing the Private branch did not render. Without it the step had no answer.
      expect(find.text('NSRIT College'), findsOneWidget);
      expect(find.text('Who are you hosting this event on behalf of?'), findsOneWidget);
    });

    testWidgets('a $product event is asked for the letter on step one, not step twelve',
        (tester) async {
      await tester.pumpWidget(host(product));
      await tester.pumpAndSettle();

      // Nothing is chosen yet, so the authorization half is not on screen: the letter names the
      // organization, and asking for it before one is picked is asking about nothing.
      expect(find.text("Signatory's name"), findsNothing);

      await tester.tap(find.text('NSRIT College'));
      await tester.pumpAndSettle();

      // Same step. No Continue was pressed between choosing the organization and being asked to
      // evidence it.
      expect(find.text('Step 1 of 11'), findsOneWidget);
      for (final label in const [
        "Signatory's name",
        'Their designation',
        'Official email',
        'Official phone',
        'Your role in this organisation',
        'Authorization letter',
      ]) {
        await tester.scrollUntilVisible(find.text(label), 200,
            scrollable: find.byType(Scrollable).first);
        expect(find.text(label), findsOneWidget, reason: '"$label" belongs on the Representing step');
      }
    });
  }

  testWidgets('the wizard is eleven steps — Authorization is no longer one of them', (tester) async {
    await tester.pumpWidget(host('Public'));
    await tester.pumpAndSettle();

    // Was twelve, with `Authorization` appended after Legal.
    expect(find.text('Step 1 of 11'), findsOneWidget);
    expect(find.text('Authorization'), findsNothing);
  });

  testWidgets('with no representation the step opens its own registration form, not a link away',
      (tester) async {
    await tester.pumpWidget(host('Private', reps: const []));
    await tester.pumpAndSettle();

    expect(find.textContaining("don't represent an organisation yet"), findsOneWidget);

    // The registration form is RENDERED HERE, already open, because there is nothing else to answer.
    // It used to be `context.push('/representing/new')` — a navigation out of a wizard holding ten
    // steps of unsaved answers, so anyone without a representation lost the event they were creating.
    for (final label in const [
      'Organisation name',
      'Type',
      'Organisation email domain (optional)',
      'Proof of affiliation',
    ]) {
      await tester.scrollUntilVisible(find.text(label), 200,
          scrollable: find.byType(Scrollable).first);
      expect(find.text(label), findsOneWidget,
          reason: '"$label" belongs on the Representing step, not on another route');
    }
    expect(find.widgetWithText(KurxButton, 'Save organisation'), findsOneWidget);
  });

  testWidgets('the registration form is collapsed behind a control when a representation exists',
      (tester) async {
    await tester.pumpWidget(host('Public'));
    await tester.pumpAndSettle();

    // Someone who already represents an organisation is not made to scroll past a registration form
    // they do not need — but the way to add another is still on this step, never on another route.
    expect(find.text('Organisation name'), findsNothing);
    expect(find.widgetWithText(TextButton, 'Add your college or organisation'), findsOneWidget);

    await tester.tap(find.widgetWithText(TextButton, 'Add your college or organisation'));
    await tester.pumpAndSettle();
    expect(find.text('Organisation name'), findsOneWidget);
  });

  testWidgets('a pending organisation is selectable and says publishing is what waits', (tester) async {
    const pending = RepresentationDto(
      organizationId: 'org-2',
      name: 'Pending Institute',
      authority: 'representative',
      isVerified: false,
      canBackPaidEvent: false,
    );
    await tester.pumpWidget(host('Public', reps: const [pending]));
    await tester.pumpAndSettle();

    // A staged organisation CAN carry a draft — the server grants Manager off the pending
    // `Representative` seat and refuses only publication (`pending_org_verification`). Filtering it out
    // of the picker enforced nothing and left whoever had just registered it unable to continue.
    expect(find.text('Pending Institute'), findsOneWidget);
    expect(find.textContaining("can't publish until that's approved"), findsOneWidget);

    await tester.tap(find.text('Pending Institute'));
    await tester.pumpAndSettle();

    // Selecting it answers the step: the authorisation half appears, which only happens once the
    // representation is valid.
    expect(find.text("Signatory's name"), findsOneWidget);
  });
}
