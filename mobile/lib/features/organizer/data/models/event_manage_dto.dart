import 'package:freezed_annotation/freezed_annotation.dart';

part 'event_manage_dto.freezed.dart';
part 'event_manage_dto.g.dart';

/// One of the caller's own events. Serves both `GET /v1/me/events` (the Workspace row, D-267) and
/// `GET /v1/events/{id}` (the detail read), so every field the two do not share is optional.
///
/// **This DTO previously matched neither.** It required `cover_url`, `total_capacity`, `view_count`
/// and `created_at` — names the row projection has never emitted (`ToOrgRowJson`/`ToMyEventRowJson`
/// send `capacity`, and no row carries a cover or a view count at all) — and it required `org_id`,
/// which the row also omitted. Every parse threw, which is why the organiser's event list rendered as
/// an error state rather than as events. Pinned against the real projections here.
@freezed
class EventManageDto with _$EventManageDto {
  const factory EventManageDto({
    required String id,
    required String title,
    String? slug,
    required String status,
    // The organisation this event REPRESENTS — not its owner, which is the user in `created_by`
    // (D-268). Used to build the org-scoped management sub-resource URLs; the client never chooses
    // it, the event carries it. `org_id` is the deprecated D-273a alias, still read as a fallback so
    // this parses against a server deployed before the rename.
    @JsonKey(name: 'representing_org_id') String? representingOrgId,
    @JsonKey(name: 'org_id') String? orgId,
    @JsonKey(name: 'starts_at') DateTime? startsAt,
    @JsonKey(name: 'ends_at') DateTime? endsAt,
    @JsonKey(name: 'tickets_sold') @Default(0) int ticketsSold,
    @Default(0) int capacity,
    @JsonKey(name: 'revenue_paise') @Default(0) int revenuePaise,
    @JsonKey(name: 'checked_in') @Default(0) int checkedIn,
    // D-388 — the two facts that decide whether this event's details are frozen behind admin approval.
    // Defaulted rather than required, like everything else detail-only here: `GET /v1/me/events` rows
    // do not carry them, and a required field the row projection omits is what made every parse throw
    // before (see the class remarks).
    @Default('Public') String product,
    @Default(1) int version,
    // Detail-only (`ToEventJson`).
    @JsonKey(name: 'banner_key') String? bannerKey,
    @JsonKey(name: 'view_count') @Default(0) int viewCount,
    // Row-only: who the event REPRESENTS. Metadata, never a grouping key and never its owner (D-268).
    EventRepresentationDto? representation,
    @JsonKey(name: 'created_at') DateTime? createdAt,
  }) = _EventManageDto;

  factory EventManageDto.fromJson(Map<String, dynamic> json) =>
      _$EventManageDtoFromJson(json);
}

/// Mirrors `AttendeeRow` (`Kurx.Application.Abstractions.IAttendeeService`) exactly as
/// `GET /v1/orgs/{orgId}/events/{eventId}/attendees` actually returns it. Unlike the rest of this
/// API, this endpoint serializes camelCase (default Minimal API `Results.Ok` on a raw record, no
/// manual JSON projection) — verified against a live response (D-20x). The previous shape
/// (`id`/`name`/`email`/`checked_in`) matched neither the real field names nor casing, so this
/// screen had never actually rendered a real attendee.
@freezed
class AttendeeDto with _$AttendeeDto {
  const factory AttendeeDto({
    required String ticketId,
    required String code,
    required String state,
    DateTime? checkedInAt,
    required String buyerName,
    required String buyerPhone,
    String? ticketTypeName,
    String? groupDisplayName,
    // Identity for the Professional Identity System (D-20x) — null for a guest ticket, or a
    // linked account whose profile isn't public.
    String? buyerUserId,
    String? buyerUsername,
    String? buyerAvatarKey,
  }) = _AttendeeDto;

  factory AttendeeDto.fromJson(Map<String, dynamic> json) =>
      _$AttendeeDtoFromJson(json);
}

@freezed
class InvitationDto with _$InvitationDto {
  const factory InvitationDto({
    required String id,
    String? email,
    String? phone,
    String? name,
    required String status,
    @JsonKey(name: 'sent_at') required DateTime sentAt,
  }) = _InvitationDto;

  factory InvitationDto.fromJson(Map<String, dynamic> json) =>
      _$InvitationDtoFromJson(json);
}

@freezed
class AnnouncementDto with _$AnnouncementDto {
  const factory AnnouncementDto({
    required String id,
    required String title,
    String? body,
    required String status,
    @JsonKey(name: 'open_count') @Default(0) int openCount,
    @JsonKey(name: 'click_count') @Default(0) int clickCount,
    @JsonKey(name: 'sent_at') DateTime? sentAt,
    @JsonKey(name: 'created_at') required DateTime createdAt,
  }) = _AnnouncementDto;

  factory AnnouncementDto.fromJson(Map<String, dynamic> json) =>
      _$AnnouncementDtoFromJson(json);
}

@freezed
class AnalyticsDto with _$AnalyticsDto {
  const factory AnalyticsDto({
    @JsonKey(name: 'total_revenue_paise') @Default(0) int totalRevenuePaise,
    @JsonKey(name: 'tickets_sold') @Default(0) int ticketsSold,
    @JsonKey(name: 'attendance_rate') @Default(0.0) double attendanceRate,
    @JsonKey(name: 'total_views') @Default(0) int totalViews,
  }) = _AnalyticsDto;

  factory AnalyticsDto.fromJson(Map<String, dynamic> json) =>
      _$AnalyticsDtoFromJson(json);
}

/// Event staff assignment (D-064/D-212) — Owner/Manager assigns by phone; the invitee accepts or
/// declines their own row. Mirrors web's `EventAssignment` (`web/lib/api.ts`) one-for-one.
@freezed
class AssignmentDto with _$AssignmentDto {
  const factory AssignmentDto({
    required String id,
    @JsonKey(name: 'event_id') required String eventId,
    @JsonKey(name: 'org_id') required String orgId,
    @JsonKey(name: 'user_id') required String userId,
    required String role,
    @JsonKey(name: 'custom_role') String? customRole,
    required String status,
    @JsonKey(name: 'show_on_profile') @Default(false) bool showOnProfile,
    String? notes,
    @JsonKey(name: 'created_at') required DateTime createdAt,
    @JsonKey(name: 'assignee_name') required String assigneeName,
    @JsonKey(name: 'assignee_username') String? assigneeUsername,
    @JsonKey(name: 'assignee_avatar_key') String? assigneeAvatarKey,
    // D-319 — event context. The organiser's roster already knows which event it is looking at; the
    // invitee's own inbox is the one that needs this, and both read this DTO. Defaulted rather than
    // required so an older API build still deserialises rather than throwing on every row.
    @JsonKey(name: 'event_title') @Default('') String eventTitle,
    @JsonKey(name: 'event_slug') String? eventSlug,
    @JsonKey(name: 'event_starts_at') DateTime? eventStartsAt,
    // Null for a self-represented event, which names no organisation at all (D-268). Render nothing —
    // never a fallback label, which would reintroduce the "personal organization" concept.
    @JsonKey(name: 'representing_org_name') String? representingOrgName,
  }) = _AssignmentDto;

  factory AssignmentDto.fromJson(Map<String, dynamic> json) =>
      _$AssignmentDtoFromJson(json);
}

/// `EventRegistrationEndpoints.ToRegistrationJson` — the registration shadow of an Order/Ticket.
@freezed
class RegistrationDto with _$RegistrationDto {
  const factory RegistrationDto({
    required String id,
    @JsonKey(name: 'event_id') required String eventId,
    @JsonKey(name: 'ticket_type_id') String? ticketTypeId,
    @JsonKey(name: 'subject_type') @Default('Person') String subjectType,
    @JsonKey(name: 'subject_id') String? subjectId,
    @JsonKey(name: 'order_id') String? orderId,
    /// `pending` / `confirmed` / `waitlisted` / `cancelled`, lowercased for display.
    @Default('pending') String state,
    @JsonKey(name: 'admission_count') @Default(0) int admissionCount,
    @JsonKey(name: 'created_at') DateTime? createdAt,
  }) = _RegistrationDto;

  factory RegistrationDto.fromJson(Map<String, dynamic> json) =>
      _$RegistrationDtoFromJson(json);
}

/// Who an event represents (D-268). `kind == 'personal'` means the owner represents themselves and
/// carries no organisation at all — there is no organisation id or name in that branch, because in
/// the domain there is no organisation. Representation never denotes ownership; the owner is the user.
@freezed
class EventRepresentationDto with _$EventRepresentationDto {
  const factory EventRepresentationDto({
    required String kind,
    @JsonKey(name: 'organization_id') String? organizationId,
    @JsonKey(name: 'organization_name') String? organizationName,
    @Default(false) bool verified,
  }) = _EventRepresentationDto;

  factory EventRepresentationDto.fromJson(Map<String, dynamic> json) =>
      _$EventRepresentationDtoFromJson(json);
}
