import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:kurx_mobile/core/theme/app_theme.dart';
import 'package:kurx_mobile/features/organizer/data/models/event_content_dto.dart';
import 'package:kurx_mobile/features/organizer/data/models/org_dto.dart';
import 'package:kurx_mobile/features/organizer/presentation/pages/event_representation_page.dart';
import 'package:kurx_mobile/features/organizer/presentation/providers/event_content_providers.dart';
import 'package:kurx_mobile/features/organizer/presentation/providers/organizer_providers.dart';

/// D-382 — the event's Representing screen is the organiser app's only home for representation.
///
/// Two things are pinned here. First, that the screen exists at all and reports the reviewer's verdict:
/// before it, the letter could only be filed inside the create-event wizard, so "changes requested"
/// reached a Flutter organiser with nowhere to answer it. Second, that a re-file does not demand the
/// letter again — the client never holds the stored key, so requiring a re-upload to fix a phone number
/// would be asking for the same document twice.
/// Scrolls the page's own list until [target] is built. `scrollUntilVisible` without an explicit
/// scrollable throws "Too many elements" here: the role dropdown contributes Scrollables of its own.
Future<void> scrollTo(WidgetTester tester, Finder target) =>
    tester.scrollUntilVisible(target, 300, scrollable: find.byType(Scrollable).first);

void main() {
  const orgId = 'org-1';
  const eventId = 'evt-1';

  const org = OrgDto(
    id: orgId,
    name: 'NSRIT College',
    verificationStatus: 'verified',
  );

  EventAuthorizationDto filed({
    String status = 'Submitted',
    String? letterheadUrl,
    String? reasonCode,
    String? notes,
  }) =>
      EventAuthorizationDto(
        eventId: eventId,
        headName: 'A. Rao',
        headDesignation: 'Principal',
        officialEmail: 'principal@nsrit.edu.in',
        officialPhone: '+919876543210',
        representativeRole: 'Principal',
        letterheadUrl: letterheadUrl,
        status: status,
        reasonCode: reasonCode,
        notes: notes,
      );

  Widget host(EventAuthorizationDto? authorization) => ProviderScope(
        overrides: [
          eventAuthorizationProvider(eventId).overrideWith((_) async => authorization),
          representativeRolesProvider.overrideWith((_) async => const ['Principal', 'Other']),
          orgDetailProvider(orgId).overrideWith((_) async => org),
        ],
        child: MaterialApp(
          theme: AppTheme.light(),
          home: const EventRepresentationPage(orgId: orgId, eventId: eventId),
        ),
      );

  testWidgets('names the organization and reports a filing that is waiting on a reviewer',
      (tester) async {
    await tester.pumpWidget(host(filed(letterheadUrl: 'https://storage/letter.pdf')));
    await tester.pumpAndSettle();

    expect(tester.takeException(), isNull);
    expect(find.text('NSRIT College'), findsOneWidget);
    expect(find.text('Pending review'), findsOneWidget);
    // What is on file is prefilled, so correcting one field never means retyping the other six.
    expect(find.text('A. Rao'), findsOneWidget);
  });

  testWidgets('says action required, and carries the reviewer\'s reason, when changes were asked for',
      (tester) async {
    await tester.pumpWidget(host(filed(
      status: 'ChangesRequested',
      letterheadUrl: 'https://storage/letter.pdf',
      reasonCode: 'Incomplete',
      notes: 'The letter does not name the event.',
    )));
    await tester.pumpAndSettle();

    expect(find.text('Action required'), findsOneWidget);
    // A verdict with no reason is one the organiser cannot act on.
    expect(find.textContaining('does not name the event'), findsOneWidget);
  });

  testWidgets('says action required when nothing has been filed at all', (tester) async {
    await tester.pumpWidget(host(null));
    await tester.pumpAndSettle();

    expect(find.text('Action required'), findsOneWidget);
    // A `ListView` builds only what is on screen, so the submit button has to be scrolled to before
    // it exists to be found. The page's own list is the first Scrollable — the role dropdown brings
    // others, which is what makes the default lookup ambiguous.
    await scrollTo(tester, find.text('File authorization'));
    expect(find.text('File authorization'), findsOneWidget);
  });

  testWidgets('a re-file does not demand the letter again', (tester) async {
    // Everything is already on file, including the letter — so nothing is missing, and the button
    // offers an update rather than a first filing.
    await tester.pumpWidget(host(filed(letterheadUrl: 'https://storage/letter.pdf')));
    await tester.pumpAndSettle();

    await scrollTo(tester, find.text('Update authorization'));
    expect(find.text('Update authorization'), findsOneWidget);
    expect(find.text('Attach the authorization letter'), findsNothing);
    expect(find.text('A letter is already on file. Attach one only to replace it.'), findsOneWidget);
  });

  testWidgets('warns that replacing an approved authorization costs the approval', (tester) async {
    // `EventAuthorizationService.Apply` resets the row to `Submitted` and clears the verdict on every
    // re-file — correct, and expensive: fixing a typo costs the event its permission to publish.
    await tester.pumpWidget(host(filed(status: 'Approved', letterheadUrl: 'https://storage/letter.pdf')));
    await tester.pumpAndSettle();

    await scrollTo(tester, find.text('Replace and re-submit'));
    expect(find.textContaining('sends it back for review'), findsOneWidget);
    // The label says what the button does, rather than reading as "save".
    expect(find.text('Update authorization'), findsNothing);
  });

  testWidgets('a first filing with no letter says so', (tester) async {
    await tester.pumpWidget(host(null));
    await tester.pumpAndSettle();

    // `validateEventAuthorization` — the same rule the create-event wizard runs on its Representing
    // step, so the two surfaces cannot disagree about what a complete authorization is.
    await scrollTo(tester, find.text('Attach the authorization letter'));
    expect(find.text('Attach the authorization letter'), findsOneWidget);
  });
}
