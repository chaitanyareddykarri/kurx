import 'package:flutter_test/flutter_test.dart';
import 'package:kurx_mobile/features/organizer/data/models/event_manage_dto.dart';

/// D-319 — the invitee's inbox reads this DTO, so the event context on it is what makes an invite
/// answerable. A row naming only a role and a uuid is not something anyone can decide on.
void main() {
  Map<String, dynamic> row({Map<String, dynamic> over = const {}}) => {
        'id': 'a1',
        'event_id': 'e1',
        'org_id': 'o1',
        'user_id': 'u1',
        'role': 'Judge',
        'custom_role': null,
        'status': 'invited',
        'show_on_profile': true,
        'notes': null,
        'created_at': '2026-08-01T10:00:00Z',
        'assignee_name': 'Asha',
        'assignee_username': null,
        'assignee_avatar_key': null,
        'event_title': 'Winter Hack',
        'event_slug': 'winter-hack',
        'event_starts_at': '2026-09-01T10:00:00Z',
        'representing_org_name': 'Acme Institute',
        ...over,
      };

  test('carries the event context the inbox renders', () {
    final a = AssignmentDto.fromJson(row());
    expect(a.eventTitle, 'Winter Hack');
    expect(a.eventSlug, 'winter-hack');
    expect(a.eventStartsAt, DateTime.parse('2026-09-01T10:00:00Z'));
    expect(a.representingOrgName, 'Acme Institute');
  });

  test('a self-represented event names no organisation', () {
    // D-268 — the representation row is named after the person. A fallback label here would
    // reintroduce the "personal organization" concept the domain model does not have.
    final a = AssignmentDto.fromJson(row(over: {'representing_org_name': null}));
    expect(a.representingOrgName, isNull);
    expect(a.eventTitle, 'Winter Hack');
  });

  test('deserialises against an API build that predates the event context', () {
    // The fields are defaulted, not required, so a client running ahead of the server degrades to a
    // thinner card instead of throwing on every row in the list.
    final older = row()
      ..remove('event_title')
      ..remove('event_slug')
      ..remove('event_starts_at')
      ..remove('representing_org_name');

    final a = AssignmentDto.fromJson(older);
    expect(a.eventTitle, '');
    expect(a.eventStartsAt, isNull);
    expect(a.representingOrgName, isNull);
    expect(a.role, 'Judge');
  });
}
