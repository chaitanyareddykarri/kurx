import 'package:flutter_test/flutter_test.dart';
import 'package:kurx_mobile/features/public_profile/data/models/public_profile_dto.dart';

/// Shapes mirrored directly from this pass's backend JSON projections (`AllyEndpoints.cs`,
/// `PublicProfileEndpoints.cs`) — see the `OrgDto` test's own doc comment for why this matters.
void main() {
  test('mutual detail parses shared events and orgs', () {
    final dto = MutualDetailDto.fromJson({
      'shared_events': [
        {'id': 'e1', 'title': 'Flutter Summit', 'slug': 'flutter-summit', 'starts_at': '2026-03-01T10:00:00Z'}
      ],
      'shared_orgs': [
        {'id': 'o1', 'name': 'NSRIT College', 'slug': 'nsrit-college'}
      ],
    });
    expect(dto.sharedEvents.single.title, 'Flutter Summit');
    expect(dto.sharedOrgs.single.name, 'NSRIT College');
  });

  test('mutual detail with nothing shared parses to empty lists, not null', () {
    final dto = MutualDetailDto.fromJson({'shared_events': [], 'shared_orgs': []});
    expect(dto.sharedEvents, isEmpty);
    expect(dto.sharedOrgs, isEmpty);
  });

  test('an ally suggestion parses its ranking reason', () {
    final dto = AllySuggestionDto.fromJson({
      'user_id': 'u1',
      'name': 'Asha Rao',
      'username': 'asharao',
      'avatar_key': null,
      'shared_event_count': 3,
      'shared_org_count': 1,
      'reason': '3 shared events, same organization',
    });
    expect(dto.sharedEventCount, 3);
    expect(dto.reason, contains('shared events'));
  });

  test('a public user search result parses', () {
    final dto = PublicUserSearchResultDto.fromJson({
      'id': 'u2',
      'name': 'Zzyzx Findme Person',
      'username': 'searchable123',
      'avatar_key': null,
      'headline': null,
    });
    expect(dto.username, 'searchable123');
    expect(dto.headline, isNull);
  });
}
