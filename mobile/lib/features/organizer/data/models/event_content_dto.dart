import 'package:freezed_annotation/freezed_annotation.dart';

part 'event_content_dto.freezed.dart';
part 'event_content_dto.g.dart';

/// Organiser-side event content DTOs — speakers, sponsors, venues, sessions and media.
///
/// Each mirrors its endpoint's `ToJson` exactly (`SpeakerEndpoints`, `SponsorEndpoints`,
/// `VenueEndpoints`, `ScheduleEndpoints`, `MediaEndpoints`). They live in one file because they
/// are one API surface — every route is `/v1/orgs/{orgId}/…` under the same D-015 role gate — and
/// splitting them would mean six near-identical datasources instead of the one that exists.
///
/// Ticket types deliberately have **no DTO here**: `features/events/data/models/ticket_type_dto.dart`
/// already maps `TicketTypeEndpoints.ToJson`, and the public and org-scoped routes return the same
/// shape. That DTO was widened with the organiser-only fields rather than duplicated.

/// `SpeakerEndpoints.ToJson` — GET/POST `/v1/orgs/{orgId}/speakers`.
@freezed
class SpeakerDto with _$SpeakerDto {
  const factory SpeakerDto({
    required String id,
    @JsonKey(name: 'org_id') String? orgId,
    required String name,
    String? bio,
    /// A storage key, not a URL — needs presigning before it renders.
    @JsonKey(name: 'photo_key') String? photoKey,
    String? company,
    String? role,
    @JsonKey(name: 'social_links_json') String? socialLinksJson,
    // Null for the common case (curated content, no account) — a linked speaker can Connect/View
    // Profile (D-20x).
    @JsonKey(name: 'user_id') String? userId,
    String? username,
    @JsonKey(name: 'avatar_key') String? avatarKey,
  }) = _SpeakerDto;

  factory SpeakerDto.fromJson(Map<String, dynamic> json) => _$SpeakerDtoFromJson(json);
}

/// `SponsorEndpoints.ToJson` — GET/POST `/v1/orgs/{orgId}/sponsors`.
@freezed
class SponsorDto with _$SponsorDto {
  const factory SponsorDto({
    required String id,
    @JsonKey(name: 'org_id') String? orgId,
    required String name,
    @JsonKey(name: 'logo_key') String? logoKey,
    String? website,
    /// Lowercased server-side (`title`/`gold`/`silver`/`bronze`/`partner`).
    String? tier,
    int? priority,
  }) = _SponsorDto;

  factory SponsorDto.fromJson(Map<String, dynamic> json) => _$SponsorDtoFromJson(json);
}

/// `VenueEndpoints.ToJson` — GET/POST `/v1/orgs/{orgId}/venues`.
@freezed
class VenueDto with _$VenueDto {
  const factory VenueDto({
    required String id,
    @JsonKey(name: 'org_id') String? orgId,
    required String name,
    String? address,
    String? city,
    double? lat,
    double? lng,
    @JsonKey(name: 'google_maps_url') String? googleMapsUrl,
    int? capacity,
    @JsonKey(name: 'has_parking') @Default(false) bool hasParking,
    @JsonKey(name: 'is_accessible') @Default(false) bool isAccessible,
    String? notes,
    @JsonKey(name: 'image_keys') @Default(<String>[]) List<String> imageKeys,
  }) = _VenueDto;

  factory VenueDto.fromJson(Map<String, dynamic> json) => _$VenueDtoFromJson(json);
}

/// `ScheduleEndpoints.ToJson` — GET/POST `/v1/orgs/{orgId}/events/{eventId}/sessions`.
@freezed
class SessionDto with _$SessionDto {
  const factory SessionDto({
    required String id,
    @JsonKey(name: 'event_id') String? eventId,
    required String title,
    String? description,
    /// Lowercased server-side (`session`/`break`/`keynote`/`workshop`).
    @Default('session') String kind,
    @JsonKey(name: 'starts_at') required DateTime startsAt,
    @JsonKey(name: 'ends_at') required DateTime endsAt,
    @Default(0) int sort,
    @JsonKey(name: 'speaker_ids') @Default(<String>[]) List<String> speakerIds,
  }) = _SessionDto;

  factory SessionDto.fromJson(Map<String, dynamic> json) => _$SessionDtoFromJson(json);
}

/// `MediaEndpoints` presign reply — `{ key, url, headers }`. The client PUTs the bytes to [url]
/// with [headers] applied, then attaches [key]. With a real object store the PUT goes straight to
/// the bucket; on localdisk it hits `StorageEndpoints`, where the signature (not the session) is
/// the credential — so the PUT must **not** carry the session bearer token.
@freezed
class PresignDto with _$PresignDto {
  const factory PresignDto({
    required String key,
    required String url,
    /// Headers the storage provider requires on the PUT. Empty for localdisk, populated for S3-style
    /// providers; always applied verbatim so a provider swap needs no client change.
    @Default(<String, String>{}) Map<String, String> headers,
  }) = _PresignDto;

  factory PresignDto.fromJson(Map<String, dynamic> json) => _$PresignDtoFromJson(json);
}

/// One attached media row. There is **no `GET …/media` list route** — the media array is returned
/// by the org-scoped event detail (`GET /v1/orgs/{orgId}/events/{eventId}` → `ToEventJson.media`),
/// which is what [EventContentRemoteDataSource.media] reads. Attach/remove reply `{ok:true}`, so a
/// write is followed by a re-read of the detail rather than trusting a returned row.
@freezed
class EventMediaDto with _$EventMediaDto {
  const factory EventMediaDto({
    required String id,
    /// `gallery` or `document`.
    @Default('gallery') String kind,
    required String key,
    String? caption,
    @Default(0) int sort,
  }) = _EventMediaDto;

  factory EventMediaDto.fromJson(Map<String, dynamic> json) => _$EventMediaDtoFromJson(json);
}

/// `CertificateEndpoints` per-event roster row — GET `/v1/events/{eventId}/certificates`.
/// Field names verified against the endpoint's inline projection: it sends `holder_name`, not
/// `user_name`, and `issued_at` is mapped from `CreatedAt`.
@freezed
class CertificateRosterDto with _$CertificateRosterDto {
  const factory CertificateRosterDto({
    required String id,
    @JsonKey(name: 'event_id') String? eventId,
    @JsonKey(name: 'verify_code') String? verifyCode,
    @JsonKey(name: 'user_id') String? userId,
    @JsonKey(name: 'holder_name') String? holderName,
    /// Lowercased server-side (`participation`/`completion`/`achievement`…).
    String? kind,
    /// Lowercased server-side.
    String? status,
    @JsonKey(name: 'is_revoked') @Default(false) bool isRevoked,
    @JsonKey(name: 'revoked_reason') String? revokedReason,
    @JsonKey(name: 'issued_at') DateTime? issuedAt,
  }) = _CertificateRosterDto;

  factory CertificateRosterDto.fromJson(Map<String, dynamic> json) =>
      _$CertificateRosterDtoFromJson(json);
}

/// `MembershipClaimEndpoints.ToJson` — GET `/v1/me/membership-claims`.
@freezed
class MembershipClaimDto with _$MembershipClaimDto {
  const factory MembershipClaimDto({
    required String id,
    @JsonKey(name: 'org_id') required String orgId,
    @JsonKey(name: 'org_name') String? orgName,
    @JsonKey(name: 'org_slug') String? orgSlug,
    /// Lowercased server-side.
    @JsonKey(name: 'claimed_role') @Default('staff') String claimedRole,
    /// `submitted` / `under_review` / `official_contact_verification` / `approved` / `rejected`.
    @Default('submitted') String status,
    @JsonKey(name: 'fast_track') @Default(false) bool fastTrack,
    @JsonKey(name: 'valid_until') DateTime? validUntil,
    @JsonKey(name: 'reviewed_at') DateTime? reviewedAt,
    String? notes,
    @JsonKey(name: 'created_at') DateTime? createdAt,
  }) = _MembershipClaimDto;

  factory MembershipClaimDto.fromJson(Map<String, dynamic> json) =>
      _$MembershipClaimDtoFromJson(json);
}
