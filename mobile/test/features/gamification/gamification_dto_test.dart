import 'package:flutter_test/flutter_test.dart';
import 'package:kurx_mobile/features/gamification/data/models/gamification_dto.dart';

/// Shapes mirrored directly from the real backend (`GamificationEndpoints.cs` returns
/// `Results.Ok(record)` with no snake_case mapping, so the wire format is camelCase) — see the
/// doc comment on `BadgeDto` for why this matters. These DTOs previously described a fictional
/// level/tier system and a nested `badges` list that never existed server-side (D-212).
void main() {
  test('points summary parses totalPoints and history, no level fields', () {
    final dto = PointsSummaryDto.fromJson({
      'totalPoints': 340,
      'history': [
        {'id': 'p1', 'source': 'checkin', 'points': 100, 'reason': 'Checked in to Flutter Summit', 'createdAt': '2026-03-01T10:00:00Z'},
      ],
    });
    expect(dto.totalPoints, 340);
    expect(dto.history.single.points, 100);
    expect(dto.history.single.reason, contains('Checked in'));
  });

  test('a badge parses without an "earned" flag — every returned badge is earned', () {
    final dto = BadgeDto.fromJson({
      'id': 'b1',
      'name': 'Early Adopter',
      'type': 'milestone',
      'description': 'Joined in the first month',
      'iconKey': 'badges/early-adopter.png',
      'earnedAt': '2026-01-15T00:00:00Z',
    });
    expect(dto.name, 'Early Adopter');
    expect(dto.iconKey, 'badges/early-adopter.png');
  });

  test('a leaderboard entry parses camelCase userId and optional username', () {
    final dto = LeaderboardEntryDto.fromJson({
      'rank': 1,
      'userId': 'u1',
      'name': 'Asha Rao',
      'username': 'asharao',
      'points': 500,
    });
    expect(dto.userId, 'u1');
    expect(dto.username, 'asharao');
  });
}
