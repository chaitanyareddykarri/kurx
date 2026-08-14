import 'package:freezed_annotation/freezed_annotation.dart';

part 'attendee_dtos.freezed.dart';
part 'attendee_dtos.g.dart';

/// Attendee-side DTOs: orders, reviews, waitlist, participations, org invitations, eligibility and
/// identity. Each mirrors its endpoint's `ToJson` verbatim — field names were read off the endpoint
/// rather than inferred, because a DTO written against an imagined shape throws on the first
/// non-empty response (the D-064 trap that broke the org list).

/// `OrderEndpoints.ToTicketJson`.
@freezed
class TicketDto with _$TicketDto {
  const factory TicketDto({
    required String id,
    required String code,
    /// Lowercased server-side: `issued` / `checkedin` / `cancelled` / `transferred`.
    @Default('issued') String state,
    @JsonKey(name: 'checked_in_at') DateTime? checkedInAt,
    @JsonKey(name: 'answers_json') String? answersJson,
    @JsonKey(name: 'created_at') DateTime? createdAt,
  }) = _TicketDto;

  factory TicketDto.fromJson(Map<String, dynamic> json) => _$TicketDtoFromJson(json);
}

/// `OrderEndpoints.ToOrderJson`.
@freezed
class OrderDto with _$OrderDto {
  const OrderDto._();

  const factory OrderDto({
    required String id,
    @JsonKey(name: 'event_id') required String eventId,
    @JsonKey(name: 'ticket_type_id') String? ticketTypeId,
    /// Lowercased: `pending` / `paid` / `failed` / `refunded` / `cancelled`.
    @Default('pending') String status,
    @JsonKey(name: 'amount_paise') @Default(0) int amountPaise,
    /// Present only on the paid path — the gateway order the client would hand to Razorpay.
    @JsonKey(name: 'razorpay_order_id') String? razorpayOrderId,
    @JsonKey(name: 'group_id') String? groupId,
    @JsonKey(name: 'join_code') String? joinCode,
    @JsonKey(name: 'created_at') DateTime? createdAt,
    @Default(<TicketDto>[]) List<TicketDto> tickets,
    /// Returned for a guest (unauthenticated) order — possession of it is the credential (D-036).
    @JsonKey(name: 'guest_access_token') String? guestAccessToken,
  }) = _OrderDto;

  factory OrderDto.fromJson(Map<String, dynamic> json) => _$OrderDtoFromJson(json);

  /// A free order is settled the moment it is created — the backend issues the ticket inline and
  /// returns `Paid`. A priced order comes back `Pending` with a gateway order id attached.
  bool get isSettled => status == 'paid';
  bool get awaitsPayment => status == 'pending' && amountPaise > 0;
}

/// `EventReviewEndpoints.ToJson`.
@freezed
class ReviewDto with _$ReviewDto {
  const factory ReviewDto({
    required String id,
    @JsonKey(name: 'event_id') String? eventId,
    required int rating,
    String? title,
    String? body,
    @JsonKey(name: 'is_anonymous') @Default(false) bool isAnonymous,
    /// The author held an issued ticket — the backend's own badge, never derived client-side.
    @JsonKey(name: 'is_verified') @Default(false) bool isVerified,
    @JsonKey(name: 'author_name') String? authorName,
    @JsonKey(name: 'created_at') DateTime? createdAt,
  }) = _ReviewDto;

  factory ReviewDto.fromJson(Map<String, dynamic> json) => _$ReviewDtoFromJson(json);
}

/// The `{ items, summary { average, count }, total }` envelope the review list returns.
@freezed
class ReviewPageDto with _$ReviewPageDto {
  const factory ReviewPageDto({
    @Default(<ReviewDto>[]) List<ReviewDto> items,
    @Default(0) double average,
    @Default(0) int count,
    @Default(0) int total,
  }) = _ReviewPageDto;

  factory ReviewPageDto.fromJson(Map<String, dynamic> json) {
    final summary = (json['summary'] as Map?)?.cast<String, dynamic>() ?? const {};
    return ReviewPageDto(
      items: (json['items'] as List? ?? const [])
          .cast<Map<String, dynamic>>()
          .map(ReviewDto.fromJson)
          .toList(),
      average: (summary['average'] as num?)?.toDouble() ?? 0,
      count: (summary['count'] as num?)?.toInt() ?? 0,
      total: (json['total'] as num?)?.toInt() ?? 0,
    );
  }
}

/// `WaitlistEndpoints.ToJson`.
@freezed
class WaitlistEntryDto with _$WaitlistEntryDto {
  const factory WaitlistEntryDto({
    required String id,
    @JsonKey(name: 'event_id') required String eventId,
    @JsonKey(name: 'ticket_type_id') required String ticketTypeId,
    @Default(0) int position,
    /// Lowercased: `waiting` / `notified` / `converted` / `cancelled` / `expired`.
    @Default('waiting') String status,
    @JsonKey(name: 'offer_expires_at') DateTime? offerExpiresAt,
    @JsonKey(name: 'created_at') DateTime? createdAt,
  }) = _WaitlistEntryDto;

  factory WaitlistEntryDto.fromJson(Map<String, dynamic> json) =>
      _$WaitlistEntryDtoFromJson(json);
}

/// `ParticipantEndpoints.ToJson` — a role someone holds at an event (speaker, judge, mentor…).
@freezed
class ParticipationDto with _$ParticipationDto {
  const factory ParticipationDto({
    required String id,
    @JsonKey(name: 'event_id') required String eventId,
    @JsonKey(name: 'subject_type') String? subjectType,
    @JsonKey(name: 'subject_id') String? subjectId,
    @JsonKey(name: 'role_slug') required String roleSlug,
    @JsonKey(name: 'role_class') String? roleClass,
    @JsonKey(name: 'custom_label') String? customLabel,
    /// `invited` / `accepted` / `declined` / `removed`.
    @Default('invited') String state,
    String? visibility,
    @JsonKey(name: 'counts_toward_capacity') @Default(false) bool countsTowardCapacity,
    @JsonKey(name: 'inventory_segment') String? inventorySegment,
    @JsonKey(name: 'created_at') DateTime? createdAt,
  }) = _ParticipationDto;

  factory ParticipationDto.fromJson(Map<String, dynamic> json) =>
      _$ParticipationDtoFromJson(json);
}

/// `OrgInvitationEndpoints.ToJson`.
///
/// **No `token` field, deliberately not invented.** Accept/decline are keyed by token
/// (`POST /v1/org-invitations/{token}/accept`) but `ListMineAsync` never selects `i.Token`, so an
/// invitee cannot action an invitation from this list — only from the link they were sent. The
/// inbox therefore lists and explains; the deep-link route does the accepting.
@freezed
class OrgInvitationDto with _$OrgInvitationDto {
  const factory OrgInvitationDto({
    required String id,
    @JsonKey(name: 'org_id') required String orgId,
    @JsonKey(name: 'org_name') String? orgName,
    @JsonKey(name: 'invited_phone') String? invitedPhone,
    @Default('staff') String role,
    @Default('pending') String status,
    @JsonKey(name: 'expires_at') DateTime? expiresAt,
    @JsonKey(name: 'created_at') DateTime? createdAt,
  }) = _OrgInvitationDto;

  factory OrgInvitationDto.fromJson(Map<String, dynamic> json) =>
      _$OrgInvitationDtoFromJson(json);
}

/// `AudienceEndpoints` eligibility check — `{ allowed, reason }`.
@freezed
class EligibilityDto with _$EligibilityDto {
  const factory EligibilityDto({
    @Default(true) bool allowed,
    String? reason,
  }) = _EligibilityDto;

  factory EligibilityDto.fromJson(Map<String, dynamic> json) => _$EligibilityDtoFromJson(json);
}

/// `IdentityEndpoints.ToJson` — the caller's own verification state.
/// Only masked last-4 values ever cross the wire (M3/D-042); there is no full-number field to hold.
/// One entry in a person's verification history — an append-only record of every decision made
/// about their identity, newest first. Sourced from `verification_reviews`, which has recorded every
/// automated and human decision since M3 and simply had no read path.
@freezed
class IdentityHistoryEntryDto with _$IdentityHistoryEntryDto {
  const factory IdentityHistoryEntryDto({
    required String component,
    required String decision,
    @JsonKey(name: 'reviewer_id') String? reviewerId,
    @JsonKey(name: 'reason_code') String? reasonCode,
    String? notes,
    @JsonKey(name: 'created_at') required DateTime createdAt,
  }) = _IdentityHistoryEntryDto;

  factory IdentityHistoryEntryDto.fromJson(Map<String, dynamic> json) =>
      _$IdentityHistoryEntryDtoFromJson(json);
}

@freezed
class IdentityStatusDto with _$IdentityStatusDto {
  const factory IdentityStatusDto({
    /// L0–L5 trust label.
    String? level,
    /// `unverified` / `pending` / `verified` / `rejected`.
    @Default('unverified') String status,
    @JsonKey(name: 'govt_id_kind') String? govtIdKind,
    @JsonKey(name: 'govt_id_last4') String? govtIdLast4,
    @JsonKey(name: 'pan_last4') String? panLast4,
    @JsonKey(name: 'bank_last4') String? bankLast4,
    // Per-component state. The aggregate `status` above cannot answer "is my PAN verified" — every
    // submission wrote it, so one failed bank check reported the whole identity rejected. Defaulted
    // so a client pinned to an older backend still parses.
    @JsonKey(name: 'govt_id_status') @Default('NotStarted') String govtIdStatus,
    @JsonKey(name: 'pan_status') @Default('NotStarted') String panStatus,
    @JsonKey(name: 'bank_status') @Default('NotStarted') String bankStatus,
    /// Penny drop proves the account exists and accepts deposits; `bankLast4` alone only ever meant
    /// a number was typed.
    @JsonKey(name: 'penny_drop_status') @Default('NotStarted') String pennyDropStatus,
    /// Whether the bank's registered holder name matched — the control that catches a PAN and a bank
    /// account belonging to two different people.
    @JsonKey(name: 'bank_name_match') @Default('NotChecked') String bankNameMatch,
    @JsonKey(name: 'bank_verified_at') DateTime? bankVerifiedAt,
    @JsonKey(name: 'reviewed_at') DateTime? reviewedAt,
    @JsonKey(name: 'expires_at') DateTime? expiresAt,
    @JsonKey(name: 'updated_at') DateTime? updatedAt,
  }) = _IdentityStatusDto;

  factory IdentityStatusDto.fromJson(Map<String, dynamic> json) =>
      _$IdentityStatusDtoFromJson(json);
}

/// `RefundEndpoints.ToViewJson` (D-199).
@freezed
class RefundDto with _$RefundDto {
  const factory RefundDto({
    required String id,
    @JsonKey(name: 'order_id') required String orderId,
    @JsonKey(name: 'event_id') String? eventId,
    @JsonKey(name: 'org_id') String? orgId,
    @JsonKey(name: 'amount_paise') @Default(0) int amountPaise,
    @Default('INR') String currency,
    String? reason,
    /// Lowercased server-side: `pending` / `processing` / `succeeded` / `failed`.
    @Default('pending') String status,
    @JsonKey(name: 'razorpay_refund_id') String? razorpayRefundId,
    @JsonKey(name: 'created_at') DateTime? createdAt,
  }) = _RefundDto;

  factory RefundDto.fromJson(Map<String, dynamic> json) => _$RefundDtoFromJson(json);
}
