import 'package:flutter_test/flutter_test.dart';
import 'package:kurx_mobile/features/organizer/presentation/pages/checkin_scanner_page.dart';

/// D-385 — the gate scanner tells the two credentials apart.
///
/// An attendee badge carries a bare ticket code; a staff badge carries `staff:{assignmentId}:{sig}`.
/// They go to different routes, so the scanner has to classify the payload before it posts — and
/// classifying it wrong means either a staff pass hitting the ticket route (where it cannot even bind)
/// or a ticket hitting the staff route. This is the whole of that decision, so it is worth pinning
/// directly rather than through a camera.
void main() {
  group('credential routing', () {
    test('a staff pass is recognised by its scheme', () {
      expect(isStaffPass('staff:2f1c8f3a-0b21-4f2e-9c33-9a1d2e3f4a5b:deadbeef'), isTrue);
    });

    test('a bare ticket code is not a staff pass', () {
      // What TicketQrEndpoints encodes: the ticket's Guid and nothing else.
      expect(isStaffPass('2f1c8f3a-0b21-4f2e-9c33-9a1d2e3f4a5b'), isFalse);
    });

    test('a payload that merely mentions staff is not one', () {
      // The prefix is the scheme, not a substring — "staffing" or a name must not route to the gate.
      expect(isStaffPass('staffing-desk-01'), isFalse);
      expect(isStaffPass('ticket:staff:abc'), isFalse);
      expect(isStaffPass(''), isFalse);
    });
  });

  group('what the marshal reads back', () {
    test('shows the name and what the badge authorises', () {
      // The point of scanning a staff badge is to check the claim against the person holding it.
      expect(
        staffLabel({'name': 'Priya Raghunathan', 'access_level': 'All Access'}),
        'Priya Raghunathan · All Access',
      );
    });

    test('shows the name alone when there is no access level', () {
      expect(staffLabel({'name': 'Arjun Mehta', 'access_level': ''}), 'Arjun Mehta');
      expect(staffLabel({'name': 'Arjun Mehta'}), 'Arjun Mehta');
    });

    test('falls back to a plain confirmation rather than showing nothing', () {
      // A response shape this build does not expect must not blank the banner: the scan succeeded, and
      // the operator needs to know that even if the detail is missing.
      expect(staffLabel(null), 'Staff checked in');
      expect(staffLabel('unexpected'), 'Staff checked in');
      expect(staffLabel({'access_level': 'All Access'}), 'Staff checked in');
      expect(staffLabel({'name': '   '}), 'Staff checked in');
    });
  });
}
