import 'package:flutter_test/flutter_test.dart';
import 'package:kurx_mobile/features/organizer/data/models/org_dto.dart';

/// Byte-for-byte responses captured from a live API (D-064). The previous [OrgDto] required a
/// `created_at` that neither payload carries, so parsing **any** non-empty org list threw —
/// and because `guard()` only caught `DioException`, that escaped as an unhandled error and
/// was repainted as "no connection". Pin the real shapes so the drift can't come back.
void main() {
  // GET /v1/orgs
  const listItem = {
    'id': '6b2c5167-9539-4c57-b37e-704f1b37d24f',
    'name': 'NSRIT College',
    'slug': 'nsrit-college',
    'logo_key': null,
    'role': 'owner',
  };

  // GET /v1/orgs/{id}  (OrgEndpoints.ToOrgJson)
  const detail = {
    'id': '6b2c5167-9539-4c57-b37e-704f1b37d24f',
    'name': 'NSRIT College',
    'slug': 'nsrit-college',
    'logo_key': null,
    'bio': null,
    'links_json': null,
    'payout_account_status': 'none',
    'bank_last4': null,
    'tier': 1,
    'role': 'owner',
    'type': 'college',
    'primary_domain': null,
    'verification_status': 'unverified',
  };

  test('parses the list shape, which carries no detail fields', () {
    final org = OrgDto.fromJson(Map<String, dynamic>.from(listItem));
    expect(org.name, 'NSRIT College');
    expect(org.role, 'owner');
    // Detail-only fields are absent here — they must be null, not defaulted to something
    // the UI would render as fact.
    expect(org.verificationStatus, isNull);
    expect(org.type, isNull);
  });

  test('parses the detail shape', () {
    final org = OrgDto.fromJson(Map<String, dynamic>.from(detail));
    expect(org.type, 'college');
    expect(org.role, 'owner');
    expect(org.payoutAccountStatus, 'none');
    expect(org.verificationStatus, 'unverified');
    // `tier` arrives as an int; typing it as String threw the same cast error.
    expect(org.tier, 1);
  });

  // This test used to be named "…is camelCase, unlike the rest of the API" and pinned that shape. It
  // was pinning a bug: the endpoint forwarded the WalletView record straight out of Results.Ok, so
  // ASP.NET's default naming policy showed through while 65 of 75 endpoint files answered snake_case.
  // Both web and admin had written snake_case schemas against it and threw on every call. The server
  // now applies the platform convention to record responses too (D-259 addendum), so this pins the
  // corrected shape — captured from a live response, like the payloads above.
  test('GET /v1/orgs/{id}/wallet is snake_case, like the rest of the API', () {
    final wallet = WalletDto.fromJson(const {
      'org_id': '6b2c5167-9539-4c57-b37e-704f1b37d24f',
      'collected_paise': 0,
      'available_paise': 150000,
      'advanced_paise': 0,
      'reserved_paise': 0,
      'settled_paise': 0,
      'lifetime_earned_paise': 150000,
      'lifetime_withdrawn_paise': 0,
      'currency': 'INR',
    });
    expect(wallet.availablePaise, 150000);
    expect(wallet.lifetimeEarnedPaise, 150000);
    expect(wallet.orgId, '6b2c5167-9539-4c57-b37e-704f1b37d24f');
  });

  test('parses the members shape, which is keyed by user_id and has no id', () {
    // GET /v1/orgs/{orgId}/members (OrgEndpoints.ToMemberJson)
    final member = OrgMemberDto.fromJson(const {
      'user_id': 'c0539299-fc88-4a1c-9181-dae0f25bdbb2',
      'phone': '919898980001',
      'name': 'Naveen',
      'username': 'naveen_org',
      'role': 'owner',
      'joined_at': '2026-07-16T17:09:31.888897Z',
    });
    expect(member.userId, 'c0539299-fc88-4a1c-9181-dae0f25bdbb2');
    expect(member.username, 'naveen_org');
    expect(member.role, 'owner');
  });
}
