import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:kurx_mobile/core/theme/app_theme.dart';
import 'package:kurx_mobile/features/organizer/data/models/event_manage_dto.dart';
import 'package:kurx_mobile/features/organizer/presentation/pages/event_manage_detail_page.dart';
import 'package:kurx_mobile/features/organizer/presentation/providers/organizer_providers.dart';

/// D-213 — a `DefaultTabController(length: 13, ...)` shipped against a 14-`Tab` `TabBar`/
/// `TabBarView` (D-212 added 7 tabs to an existing 7, miscounted as 13). Neither `flutter analyze`
/// nor any pre-existing test could catch it: the mismatch is a runtime assertion inside
/// `TabBar`/`TabBarView`'s build, not a static-analysis-detectable error, and no widget test
/// constructed this page at all. This test exists specifically to close that gap — it pumps the
/// real page and asserts nothing throws, so a future tab-count edit that forgets to update
/// `length` fails here instead of silently shipping.
void main() {
  const orgId = 'org-1';
  const eventId = 'evt-1';
  // Keyed by event id alone (D-267) — the event resolves its own organisation.
  const param = eventId;

  final event = EventManageDto(
    id: eventId,
    title: 'Flutter Summit',
    status: 'published',
    orgId: orgId,
    createdAt: DateTime(2026, 1, 1),
  );

  Widget host() => ProviderScope(
        overrides: [
          eventManageDetailProvider(param).overrideWith((_) async => event),
        ],
        child: MaterialApp(
          theme: AppTheme.light(),
          home: const EventManageDetailPage(orgId: orgId, eventId: eventId),
        ),
      );

  testWidgets('opens without a TabController assertion and renders every tab', (tester) async {
    await tester.pumpWidget(host());
    await tester.pumpAndSettle();

    // The regression this guards against throws during build, before any of these widgets
    // would exist — if TabController.length ever drifts from the tab count again, this is
    // where it fails.
    expect(tester.takeException(), isNull);

    for (final label in const [
      'Overview', 'Tickets', 'Venue', 'Schedule', 'Speakers', 'Sponsors', 'Media',
      'Team', 'Attendees', 'Check-in', 'Invitations', 'Announcements', 'Analytics', 'Certificates',
    ]) {
      expect(find.text(label), findsOneWidget, reason: '"$label" tab should render');
    }
  });

  testWidgets('every tab is selectable without throwing', (tester) async {
    await tester.pumpWidget(host());
    await tester.pumpAndSettle();

    for (final label in const [
      'Team', 'Attendees', 'Check-in', 'Invitations', 'Announcements', 'Analytics', 'Certificates',
    ]) {
      // isScrollable: true means later tabs sit outside the default test viewport — scroll each
      // into view first so the tap is real, not a warned-and-ignored off-screen hit.
      await tester.ensureVisible(find.text(label));
      await tester.tap(find.text(label));
      await tester.pumpAndSettle();
      expect(tester.takeException(), isNull, reason: 'selecting "$label" should not throw');
    }
  });
}
