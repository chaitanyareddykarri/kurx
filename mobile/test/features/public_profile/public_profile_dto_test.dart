import 'package:flutter_test/flutter_test.dart';
import 'package:kurx_mobile/features/public_profile/data/models/public_profile_dto.dart';

/// Byte-for-byte responses captured from a live backend (D-201) — see the `OrgDto` test's own
/// doc comment for why this matters: `guard()` only converts `DioException`s, so a DTO field
/// mismatch throws unhandled and gets repainted as "no connection" rather than a parse error.
void main() {
  // GET /v1/public/users/{username} — a fresh profile with zero activity.
  const freshProfile = {
    'id': '5c430cf5-c512-4668-b56c-3fa52c3f48bf',
    'name': '',
    'username': 'livecheckalice',
    'headline': null,
    'bio': null,
    'summary': ' is building their verified event identity on Kurx.',
    'avatar_key': null,
    'cover_key': null,
    'college': null,
    'links': null,
    'skills': null,
    'stats': {
      'events_conducted': 0,
      'events_attended': null,
      'certificates_count': 0,
      'participations': 0,
      'achievements': 0,
      'ally_count': 0,
    },
    'verification': {
      'identity_verified': false,
      'verified_member': false,
      'organizer': false,
      'verified_certificates': 0,
      'years_on_platform': 0,
    },
    'event_dna': [],
    'achievements': [],
    'identity_labels': [],
    'organizations': [],
  };

  // POST /v1/allies/requests/{id}/accept
  const acceptedConnection = {
    'id': '86fc3b5d-1cb7-4adc-a437-d7ef9997ffbf',
    'other_user_id': '5c430cf5-c512-4668-b56c-3fa52c3f48bf',
    'other_name': '',
    'other_username': 'livecheckalice',
    'other_avatar_key': null,
    'status': 'Accepted',
    'visibility': 'Public',
    'requested_at': '2026-07-30T12:40:04.249675+00:00',
    'responded_at': '2026-07-30T12:40:04.8132955+00:00',
    'first_shared_event_id': null,
    'first_shared_event_title': null,
    'first_shared_event_slug': null,
  };

  // GET /v1/public/users/{username}/allies
  const alliesList = [
    {
      'user_id': '4b2fda74-6548-4f7b-b9fb-502eb61cd20b',
      'name': '',
      'username': 'livecheckbob',
      'avatar_key': null,
      'mutual_event_count': 0,
    }
  ];

  test('a fresh public profile with zero activity parses cleanly', () {
    final dto = PublicProfileDto.fromJson(freshProfile);
    expect(dto.username, 'livecheckalice');
    expect(dto.stats.allyCount, 0);
    expect(dto.stats.certificatesCount, 0);
    expect(dto.achievements, isEmpty);
    expect(dto.organizations, isEmpty);
    expect(dto.identityLabels, isEmpty);
  });

  test('an accepted ally connection parses with its status and timestamps', () {
    final dto = AllyConnectionDto.fromJson(acceptedConnection);
    expect(dto.status, 'Accepted');
    expect(dto.otherUsername, 'livecheckalice');
    expect(dto.respondedAt, isNotNull);
    expect(dto.firstSharedEventId, isNull);
  });

  test('the public allies list parses each card', () {
    final dtos = alliesList.map(AllyProfileCardDto.fromJson).toList();
    expect(dtos, hasLength(1));
    expect(dtos.single.username, 'livecheckbob');
    expect(dtos.single.mutualEventCount, 0);
  });

  // ── D-225 / D-226 wire shapes ─────────────────────────────────────────────
  // Both camelCase-vs-snake_case bugs in this codebase's history (D-208, D-210) came from
  // hand-written DTOs drifting from hand-built JSON, so every new payload gets a parsing test.

  test('metrics parse, and a hidden section arrives as null rather than zero', () {
    final dto = ProfileMetricsDto.fromJson({
      'events_organized': 3,
      'events_participated': 7,
      // Hidden for this viewer — must stay null so the UI renders an em dash, not "0".
      'events_attended': null,
      'completion_rate': 0.75,
      'assignments_accepted': 2,
      'assignments_completed': 1,
      'competitions_entered': 4,
      'competitions_won': 1,
      'speaker_sessions': 2,
      'certificates': null,
      'achievement_certificates': 1,
      'organizations': 2,
      'verified_organizations': 1,
      'cities': ['Hyderabad', 'Bengaluru'],
      'event_dna': [
        {'kind': 'hackathon', 'count': 5},
        {'kind': 'conference', 'count': 2},
      ],
    });

    expect(dto.eventsOrganized, 3);
    expect(dto.eventsAttended, isNull);
    expect(dto.certificates, isNull);
    expect(dto.completionRate, 0.75);
    expect(dto.cities, ['Hyderabad', 'Bengaluru']);
    expect(dto.eventDna.first.kind, 'hackathon');
    expect(dto.eventDna.first.count, 5);
  });

  test('experience parses its band together with the counts behind it', () {
    final dto = ExperienceSummaryDto.fromJson({
      'band': 'Active',
      'distinct_events': 14,
      'events_organized': 3,
      'events_participated': 9,
      'events_attended': 2,
      'leadership_events': 5,
      'organizations': 2,
      'verified_organizations': 1,
      'assignments_completed': 4,
      'speaker_sessions': 3,
      'competitions_won': 1,
      'years_active': 2,
      'first_activity_at': '2024-03-14T00:00:00Z',
    });

    expect(dto.band, 'Active');
    expect(dto.distinctEvents, 14);
    expect(dto.leadershipEvents, 5);
    expect(dto.firstActivityAt, isNotNull);
  });

  test('mutual detail parses derived relationships alongside the shared context', () {
    final dto = MutualDetailDto.fromJson({
      'shared_events': <Map<String, dynamic>>[],
      'shared_orgs': <Map<String, dynamic>>[],
      'relationships': [
        {'type': 'judged', 'label': 'Judged their entry', 'count': 2, 'context': 'Kurx Hacks'},
        {'type': 'shared_community', 'label': 'Same verified community', 'count': 1, 'context': 'IIIT'},
      ],
    });

    expect(dto.relationships, hasLength(2));
    expect(dto.relationships.first.type, 'judged');
    expect(dto.relationships.first.label, 'Judged their entry');
    expect(dto.relationships.first.context, 'Kurx Hacks');
  });

  test('a pre-D-226 backend omitting relationships still parses', () {
    final dto = MutualDetailDto.fromJson({
      'shared_events': <Map<String, dynamic>>[],
      'shared_orgs': <Map<String, dynamic>>[],
    });

    expect(dto.relationships, isEmpty);
  });

  test('the derived headline and the new trust signals parse, defaulting safely when absent', () {
    final withHeadline = PublicProfileDto.fromJson({
      ...freshProfile,
      'derived_headline': 'Verified Student • Public Speaker',
      'verification': {
        ...(freshProfile['verification'] as Map<String, dynamic>),
        'phone_verified': true,
        'email_verified': true,
        'speaker_verified': true,
        'community_verified': false,
      },
    });
    expect(withHeadline.derivedHeadline, 'Verified Student • Public Speaker');
    expect(withHeadline.verification.speakerVerified, isTrue);
    expect(withHeadline.verification.communityVerified, isFalse);

    // An older backend sends neither — the client must not crash, and must not invent a headline.
    final legacy = PublicProfileDto.fromJson(freshProfile);
    expect(legacy.derivedHeadline, '');
    expect(legacy.verification.speakerVerified, isFalse);
  });
}
