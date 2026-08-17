import 'package:flutter_test/flutter_test.dart';
import 'package:kurx_mobile/features/organizer/presentation/pages/event_status_page.dart';
import 'package:kurx_mobile/features/workspace/presentation/pages/workspace_hub_page.dart';

/// D-362 — approval grants permission to publish; it never publishes.
///
/// The backend defect (a free event publishable straight out of the review queue) is covered by
/// `LifecycleVisibilityTests` over HTTP. This covers the half that lived in the client: both apps
/// told the organiser the *opposite* of the rule.
///
/// `_openHostStates` omitted `approved`, contradicting the flow written in the comment directly above
/// it — "Admin Review → Approved → Host Workspace Opens". So an approved event drew an hourglass
/// reading "Opens after approval", and Publish — the one action its host now had — lived inside a
/// workspace the page would not open. The status rail had no Approved step at all and drew an approved
/// event as still "Submitted for review".
void main() {
  group('the host workspace opens at approval', () {
    test('every approved-onward state opens it', () {
      for (final s in ['approved', 'published', 'scheduled', 'live', 'completed', 'closed']) {
        expect(WorkspaceHubPage.openHostStates, contains(s), reason: '$s must open the workspace');
      }
    });

    test('nothing before approval opens it', () {
      // A draft or an event under review is not a working workspace.
      for (final s in ['draft', 'pendingreview', 'underreview', 'rejected', 'changesrequested']) {
        expect(WorkspaceHubPage.openHostStates, isNot(contains(s)));
      }
    });

    /// Pinned as a whole set against web's `OPEN_HOST_STATES`, not just probed. Two clients disagreeing
    /// about when a workspace opens is the same defect twice, and neither would look broken.
    test('matches web OPEN_HOST_STATES exactly', () {
      expect(
        WorkspaceHubPage.openHostStates.toList()..sort(),
        ['approved', 'closed', 'completed', 'live', 'published', 'scheduled'],
      );
    });
  });

  group('the status rail', () {
    test('approved is its own step, past review and short of published', () {
      expect(EventStatusPage.statusIndex('underreview'), 1);
      expect(EventStatusPage.statusIndex('approved'), 2);
      expect(EventStatusPage.statusIndex('published'), 3);

      // The point of the step: approved must not be drawn as still in review…
      expect(EventStatusPage.statusIndex('approved'),
          greaterThan(EventStatusPage.statusIndex('pendingreview')));
      // …nor as published, which is the lie the rail would have told at index 2 of the old 5-step list
      // ("Published — Visible to public") on an event no member of the public can see.
      expect(EventStatusPage.statusIndex('approved'),
          lessThan(EventStatusPage.statusIndex('published')));
    });

    test('the later states keep their places after the insert', () {
      // Adding a step mid-rail shifts every index after it; getting this wrong lights the wrong row.
      expect(EventStatusPage.statusIndex('scheduled'), 3);
      expect(EventStatusPage.statusIndex('live'), 4);
      expect(EventStatusPage.statusIndex('closed'), 5);
      expect(EventStatusPage.statusIndex('completed'), 5);
    });

    test('an unknown status falls back to draft rather than guessing forward', () {
      expect(EventStatusPage.statusIndex('somethingnew'), 0);
    });
  });

  /// The fallback above is right for an UNKNOWN status and wrong for a known one. `rejected`,
  /// `cancelled` and `archived` were never given an arm, inherited it, and were drawn as **Draft** —
  /// a cancelled event shown as if it were back at the beginning.
  group('states that are off the rail are not drawn on it', () {
    test('the three terminal-or-refused states get a note instead of a step', () {
      for (final s in ['rejected', 'cancelled', 'archived']) {
        expect(EventStatusPage.offRailNote(s), isNotNull,
            reason: '$s must not borrow the vocabulary of progress');
      }
    });

    test('every state that belongs on the rail still gets it', () {
      for (final s in [
        'draft', 'pendingreview', 'underreview', 'changesrequested',
        'approved', 'scheduled', 'published', 'live', 'closed', 'completed',
      ]) {
        expect(EventStatusPage.offRailNote(s), isNull, reason: '$s is a step, not a dead end');
      }
    });

    test('an unknown status keeps the rail rather than inventing a dead end', () {
      expect(EventStatusPage.offRailNote('somethingnew'), isNull);
    });
  });
}
