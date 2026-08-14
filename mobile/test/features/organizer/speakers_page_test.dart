import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:kurx_mobile/core/theme/app_theme.dart';
import 'package:kurx_mobile/features/organizer/data/models/event_content_dto.dart';
import 'package:kurx_mobile/features/organizer/presentation/pages/speakers_page.dart';
import 'package:kurx_mobile/features/organizer/presentation/providers/event_content_providers.dart';

/// The second half of the fake-screen defect: the list body was a hardcoded
/// `const EmptyState(title: 'No speakers yet')`, so an event with ten speakers still read
/// "No speakers yet" forever. These tests pin the list to the provider.
void main() {
  const orgId = 'org-1';
  const eventId = 'evt-1';
  const ref = (orgId: orgId, eventId: eventId);

  Widget host({
    required List<SpeakerDto> assigned,
    required List<SpeakerDto> catalogue,
  }) =>
      ProviderScope(
        overrides: [
          eventSpeakersProvider(ref).overrideWith((_) async => assigned),
          orgSpeakersProvider(orgId).overrideWith((_) async => catalogue),
        ],
        child: MaterialApp(
          theme: AppTheme.light(),
          home: const SpeakersPage(orgId: orgId, eventId: eventId),
        ),
      );

  testWidgets('renders the speakers the server returns', (tester) async {
    await tester.pumpWidget(
      host(
        assigned: const [
          SpeakerDto(id: 's1', name: 'Ada Lovelace', role: 'Mathematician', company: 'Analytical'),
        ],
        catalogue: const [
          SpeakerDto(id: 's1', name: 'Ada Lovelace'),
          SpeakerDto(id: 's2', name: 'Grace Hopper'),
        ],
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('Ada Lovelace'), findsWidgets);
    expect(find.text('Grace Hopper'), findsOneWidget);
    // The bug this replaces: a populated event must never claim to be empty.
    expect(find.text('No speakers yet'), findsNothing);
  });

  testWidgets('an empty catalogue shows the empty state, not a blank screen', (tester) async {
    await tester.pumpWidget(host(assigned: const [], catalogue: const []));
    await tester.pumpAndSettle();

    expect(find.text('No speakers yet'), findsOneWidget);
    expect(
      find.textContaining('No speakers on this event yet'),
      findsOneWidget,
      reason: 'the two lists are distinct — an empty lineup is not an empty catalogue',
    );
  });

  testWidgets('a speaker already on the event cannot be added twice', (tester) async {
    await tester.pumpWidget(
      host(
        assigned: const [SpeakerDto(id: 's1', name: 'Ada Lovelace')],
        catalogue: const [SpeakerDto(id: 's1', name: 'Ada Lovelace')],
      ),
    );
    await tester.pumpAndSettle();

    // The catalogue row for an already-assigned speaker offers no add action.
    expect(find.byTooltip('Already on this event'), findsOneWidget);
    expect(find.byTooltip('Add to this event'), findsNothing);
  });

  testWidgets('a failed load offers retry rather than an empty lineup', (tester) async {
    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          eventSpeakersProvider(ref).overrideWith((_) async => throw Exception('offline')),
          orgSpeakersProvider(orgId).overrideWith((_) async => const <SpeakerDto>[]),
        ],
        child: MaterialApp(
          theme: AppTheme.light(),
          home: const SpeakersPage(orgId: orgId, eventId: eventId),
        ),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('Retry'), findsOneWidget);
    expect(find.text('No speakers yet'), findsNothing);
  });
}
