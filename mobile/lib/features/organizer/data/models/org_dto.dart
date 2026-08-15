import 'package:freezed_annotation/freezed_annotation.dart';

part 'org_dto.freezed.dart';
part 'org_dto.g.dart';

/// Mirrors what `OrgEndpoints` actually returns — verified against a live API, because the
/// previous shape was written against an API that does not exist (D-064): it required a
/// `created_at` the server never sends, so **any** non-empty org list threw on parse, and it
/// declared `member_count`/`event_count`/`total_revenue_paise` that no endpoint returns, whose
/// zero defaults were rendered as if they were real figures.
///
/// This is the **detail** shape only (`GET|POST /v1/orgs/{id}`, `ToOrgJson`). The list it used to
/// double as is now a different thing entirely — see [RepresentationDto].
@freezed
class OrgDto with _$OrgDto {
  const factory OrgDto({
    required String id,
    required String name,
    String? slug,
    /// A storage key (`orgs/…`), not a URL — it needs presigning before it can be rendered.
    @JsonKey(name: 'logo_key') String? logoKey,
    /// The caller's role in this org, lowercased (`owner`/`manager`/`staff`/`finance`).
    String? role,
    // ── Detail-only: absent from the list response, hence nullable. ──
    String? bio,
    @JsonKey(name: 'links_json') String? linksJson,
    @JsonKey(name: 'payout_account_status') String? payoutAccountStatus,
    @JsonKey(name: 'bank_last4') String? bankLast4,
    /// An integer over the wire (`"tier": 1`), not a string.
    int? tier,
    /// `OrganizationType`, lowercased (D-043).
    String? type,
    @JsonKey(name: 'primary_domain') String? primaryDomain,
    @JsonKey(name: 'verification_status') String? verificationStatus,
  }) = _OrgDto;

  factory OrgDto.fromJson(Map<String, dynamic> json) =>
      _$OrgDtoFromJson(json);
}

@freezed
/// `GET /v1/orgs/{orgId}/members` (`OrgEndpoints.ToMemberJson`) → `user_id, phone, name,
/// username, role, joined_at`. It previously required an `id` the endpoint never sends —
/// so the members list threw on parse — and declared an `email` no endpoint returns while
/// missing the `username` it does (D-064). The member's identity here is `user_id`.
class OrgMemberDto with _$OrgMemberDto {
  const factory OrgMemberDto({
    @JsonKey(name: 'user_id') required String userId,
    required String name,
    String? username,
    String? phone,
    required String role,
    @JsonKey(name: 'joined_at') required DateTime joinedAt,
    @JsonKey(name: 'avatar_key') String? avatarKey,
    @JsonKey(name: 'is_verified') @Default(false) bool isVerified,
  }) = _OrgMemberDto;

  factory OrgMemberDto.fromJson(Map<String, dynamic> json) =>
      _$OrgMemberDtoFromJson(json);
}

@freezed
class LedgerEntryDto with _$LedgerEntryDto {
  const factory LedgerEntryDto({
    required String id,
    required String type,
    required String description,
    @JsonKey(name: 'amount_paise') required int amountPaise,
    @JsonKey(name: 'balance_after_paise') required int balanceAfterPaise,
    @JsonKey(name: 'created_at') required DateTime createdAt,
  }) = _LedgerEntryDto;

  factory LedgerEntryDto.fromJson(Map<String, dynamic> json) =>
      _$LedgerEntryDtoFromJson(json);
}

/// `GET /v1/orgs/{orgId}/wallet`. Unlike every other endpoint in this API, `WalletEndpoints`
/// returns the `WalletView` record directly instead of a hand-built snake_case object, so
/// these keys arrive **camelCase** — verified against a live response. The names are spelled
/// out here rather than "fixed" client-side, because the wire format is the wire format.
@freezed
class WalletDto with _$WalletDto {
  const factory WalletDto({
    @JsonKey(name: 'org_id') required String orgId,
    @JsonKey(name: 'collected_paise') @Default(0) int collectedPaise,
    @JsonKey(name: 'available_paise') @Default(0) int availablePaise,
    @JsonKey(name: 'advanced_paise') @Default(0) int advancedPaise,
    @JsonKey(name: 'reserved_paise') @Default(0) int reservedPaise,
    @JsonKey(name: 'settled_paise') @Default(0) int settledPaise,
    @JsonKey(name: 'lifetime_earned_paise') @Default(0) int lifetimeEarnedPaise,
    @JsonKey(name: 'lifetime_withdrawn_paise') @Default(0) int lifetimeWithdrawnPaise,
    // One currency per wallet (V3 §9.1, D-257). The server has always sent it; nothing read it.
    @Default('INR') String currency,
  }) = _WalletDto;

  factory WalletDto.fromJson(Map<String, dynamic> json) =>
      _$WalletDtoFromJson(json);
}

/// An organization the caller may represent — `GET /v1/me/representations` (D-268).
///
/// A separate type from [OrgDto] on purpose. This list answers "who may I represent?", not "what
/// organizations exist"; its fields are named for that question (`organization_id`, `authority`), and
/// self-representation is **not in it** — representing yourself is not an organization, so there is no
/// "personal" row and no `is_personal` flag to filter on. The picker offers Personal as its own choice.
@freezed
class RepresentationDto with _$RepresentationDto {
  const factory RepresentationDto({
    @JsonKey(name: 'organization_id') required String organizationId,
    required String name,
    String? slug,
    /// A storage key (`orgs/…`), not a URL — it needs presigning before it can be rendered.
    @JsonKey(name: 'logo_key') String? logoKey,
    /// The caller's authority to act for this organization, lowercased — not a role over events.
    required String authority,

    /// The ORGANIZATION's registry status (D-350), distinct from [authority], which is the caller's
    /// standing over it. A staged representation request is a real `PendingReview` row in this list —
    /// legitimate for a free event, never for a paid one, because ticket money settles into the
    /// organization's account. Defaulted so an older server that omits the field reads as "not
    /// verified", which is the closed position.
    @JsonKey(name: 'is_verified') @Default(false) bool isVerified,

    /// D-352 — the CAPABILITY the client gates on; [isVerified] is the FACT it displays. They differ
    /// only under the dev bypass, which opens the gate without forging the status. Defaulted false so an
    /// older server that omits it lands on the closed position.
    @JsonKey(name: 'can_back_paid_event') @Default(false) bool canBackPaidEvent,
  }) = _RepresentationDto;

  factory RepresentationDto.fromJson(Map<String, dynamic> json) =>
      _$RepresentationDtoFromJson(json);
}
