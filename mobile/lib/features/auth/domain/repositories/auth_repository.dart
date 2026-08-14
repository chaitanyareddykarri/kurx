import '../entities/current_user.dart';

/// Auth operations against the verified `/v1/auth/*` + `/v1/me` contract.
/// Implementations throw [ApiError] on failure.
abstract interface class AuthRepository {
  /// `POST /v1/auth/otp/request` — sends the login OTP. The channel is the server's decision (D-281),
  /// not this client's; today that resolves to SMS.
  Future<void> requestOtp(String phone);

  /// `POST /v1/auth/otp/verify` — verifies the code and persists the returned tokens.
  Future<void> verifyOtp(String phone, String code);

  /// `GET /v1/me` — the current user (drives the onboarding gate).
  Future<CurrentUser> me();

  /// `POST /v1/auth/logout` for the stored refresh token, then clears local tokens.
  Future<void> logout();

  /// `PATCH /v1/me/profile` with Name + Username, then re-fetches `/v1/me` so the
  /// caller gets the canonical `needs_onboarding: false` user (D-037).
  Future<CurrentUser> completeOnboarding({required String name, required String username});

  /// `PATCH /v1/me/profile` with just Username (post-onboarding change), then
  /// re-fetches `/v1/me` for the canonical user.
  Future<CurrentUser> updateUsername(String username);

  /// `GET /v1/usernames/availability` — true only when the username is `available`.
  Future<bool> checkUsernameAvailable(String username);

  /// `PATCH /v1/me/profile` with the self-declared display fields (D-219), then re-fetches `/v1/me`.
  /// Partial: an omitted argument is left untouched server-side.
  Future<CurrentUser> updateDisplayProfile({
    String? name,
    String? headline,
    String? bio,
    List<String>? skills,
    List<String>? languages,
    List<String>? interests,
    String? educationJson,
    String? linksJson,
    String? avatarKey,
    String? coverKey,
    /// Editable after onboarding collects it (D-311) — otherwise a mis-tapped birth date would be
    /// permanent. Server re-validates the minimum age.
    DateTime? dateOfBirth,
  });

  /// `PATCH /v1/me/privacy` (D-219; per-section tiers D-221) — partial, then re-fetches.
  Future<CurrentUser> updatePrivacy({
    bool? profilePublic,
    bool? showAttended,
    bool? showCertificates,
    bool? showAllies,
    Map<String, String>? sections,
  });

  /// `POST /v1/me/profile-image/presign` then PUTs [bytes] to the returned URL (D-219).
  /// Returns the storage key to persist via [updateDisplayProfile]; never writes it itself.
  Future<String> uploadProfileImage({
    required String slot,
    required String contentType,
    required List<int> bytes,
  });
}
