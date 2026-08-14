import 'package:freezed_annotation/freezed_annotation.dart';

import '../../domain/entities/current_user.dart';

part 'current_user_dto.freezed.dart';
part 'current_user_dto.g.dart';

/// Mirrors the snake_case `GET /v1/me` body.
@freezed
class CurrentUserDto with _$CurrentUserDto {
  const CurrentUserDto._();

  const factory CurrentUserDto({
    required String id,
    required String phone,
    @Default('') String name,
    String? username,
    String? email,
    /// Proven by an emailed one-time code, not merely present (D-182).
    @JsonKey(name: 'email_verified') @Default(false) bool emailVerified,
    @JsonKey(name: 'needs_onboarding') @Default(false) bool needsOnboarding,
    /// Date-only (`YYYY-MM-DD`, D-311). Required to finish onboarding, and the only fact on the
    /// account an age-restricted event's MinAge/MaxAge can read. Nullable so an older backend, or a
    /// pre-D-311 account, still parses.
    @JsonKey(name: 'date_of_birth') DateTime? dateOfBirth,
    /// When the account was created — the "member since" fact.
    @JsonKey(name: 'created_at') DateTime? createdAt,
    // Self-declared display fields (D-219). All optional so an older backend still parses.
    String? headline,
    String? bio,
    @Default(<String>[]) List<String> skills,
    @Default(<String>[]) List<String> languages,
    @Default(<String>[]) List<String> interests,
    /// jsonb array of education entries. Self-declared and never proof (D-220): it was never
    /// migrated into evidence-backed membership claims.
    @JsonKey(name: 'education_json') String? educationJson,
    @JsonKey(name: 'links_json') String? linksJson,
    @JsonKey(name: 'avatar_key') String? avatarKey,
    @JsonKey(name: 'cover_key') String? coverKey,
    ProfilePrivacyDto? privacy,
    /// Derived user trust (M7). **Nested on the wire and previously not parsed at all**, so this
    /// client had no access to `can_organize_paid` and could not tell a verified organiser from an
    /// unverified one. Web declared the same field flat and silently defaulted it to false for every
    /// user — the same failure class as D-245 and D-292, found in both clients at once.
    TrustCapabilitiesDto? trust,
  }) = _CurrentUserDto;

  factory CurrentUserDto.fromJson(Map<String, dynamic> json) =>
      _$CurrentUserDtoFromJson(json);

  CurrentUser toEntity() => CurrentUser(
        id: id,
        phone: phone,
        name: name,
        username: username,
        email: email,
        emailVerified: emailVerified,
        needsOnboarding: needsOnboarding,
        dateOfBirth: dateOfBirth,
        createdAt: createdAt,
        headline: headline,
        bio: bio,
        skills: skills,
        languages: languages,
        interests: interests,
        educationJson: educationJson,
        linksJson: linksJson,
        avatarKey: avatarKey,
        coverKey: coverKey,
        privacy: privacy?.toEntity() ?? const ProfilePrivacy(),
        trust: trust?.toEntity() ?? const TrustCapabilities(),
      );
}

/// The `trust` block on `/v1/me` (M7, D-046) — capability flags derived LIVE from identity
/// verification and fraud signals. Defaults are the closed position: an older backend that omits the
/// block grants nothing, which fails safe rather than advertising a capability the server will refuse.
@freezed
class TrustCapabilitiesDto with _$TrustCapabilitiesDto {
  const TrustCapabilitiesDto._();

  const factory TrustCapabilitiesDto({
    @Default('L1') String level,
    @JsonKey(name: 'can_organize_free') @Default(true) bool canOrganizeFree,
    @JsonKey(name: 'can_organize_paid') @Default(false) bool canOrganizePaid,
    @JsonKey(name: 'can_receive_payout') @Default(false) bool canReceivePayout,
    @JsonKey(name: 'identity_verified') @Default(false) bool identityVerified,
    @JsonKey(name: 'bank_verified') @Default(false) bool bankVerified,
    /// D-307. Closed position on absence — a missing capability must never read as permission.
    @JsonKey(name: 'can_create_public_event') @Default(false) bool canCreatePublicEvent,
    /// Defaults true so an older backend does not accidentally block Private.
    @JsonKey(name: 'can_create_private_event') @Default(true) bool canCreatePrivateEvent,
  }) = _TrustCapabilitiesDto;

  factory TrustCapabilitiesDto.fromJson(Map<String, dynamic> json) =>
      _$TrustCapabilitiesDtoFromJson(json);

  TrustCapabilities toEntity() => TrustCapabilities(
        level: level,
        canOrganizeFree: canOrganizeFree,
        canOrganizePaid: canOrganizePaid,
        canReceivePayout: canReceivePayout,
        identityVerified: identityVerified,
        bankVerified: bankVerified,
        canCreatePublicEvent: canCreatePublicEvent,
        canCreatePrivateEvent: canCreatePrivateEvent,
      );
}

/// The `privacy` block on `/v1/me` (D-219). Defaults match the server's column defaults so a missing
/// block is indistinguishable from an untouched account.
@freezed
class ProfilePrivacyDto with _$ProfilePrivacyDto {
  const ProfilePrivacyDto._();

  const factory ProfilePrivacyDto({
    @JsonKey(name: 'profile_public') @Default(true) bool profilePublic,
    @JsonKey(name: 'show_attended') @Default(false) bool showAttended,
    @JsonKey(name: 'show_certificates') @Default(true) bool showCertificates,
    @JsonKey(name: 'show_allies') @Default(true) bool showAllies,
    /// Per-section four-tier visibility (D-221). Absent on an older backend, in which case
    /// [ProfilePrivacy.tierFor] derives the tier from the booleans above.
    @Default(<String, String>{}) Map<String, String> sections,
  }) = _ProfilePrivacyDto;

  factory ProfilePrivacyDto.fromJson(Map<String, dynamic> json) =>
      _$ProfilePrivacyDtoFromJson(json);

  ProfilePrivacy toEntity() => ProfilePrivacy(
        profilePublic: profilePublic,
        showAttended: showAttended,
        showCertificates: showCertificates,
        showAllies: showAllies,
        sections: sections,
      );
}
