import 'package:dio/dio.dart';

import '../../../../core/network/api_guard.dart';
import '../models/current_user_dto.dart';
import '../models/registration_status_dto.dart';
import '../models/token_response_dto.dart';

/// Thin HTTP layer over the verified `/v1/auth/*` + `/v1/me` endpoints.
/// Request bodies are camelCase; responses are snake_case (D-019).
class AuthRemoteDataSource {
  AuthRemoteDataSource(this._dio);

  final Dio _dio;

  Future<void> requestOtp(String phone) => guard(() async {
        await _dio.post('/v1/auth/otp/request', data: {'phone': phone});
      });

  Future<TokenResponseDto> verifyOtp(String phone, String code) => guard(() async {
        final res = await _dio.post(
          '/v1/auth/otp/verify',
          data: {'phone': phone, 'code': code},
        );
        return TokenResponseDto.fromJson((res.data as Map).cast<String, dynamic>());
      });

  Future<CurrentUserDto> me() => guard(() async {
        final res = await _dio.get('/v1/me');
        return CurrentUserDto.fromJson((res.data as Map).cast<String, dynamic>());
      });

  Future<void> logout(String refreshToken) => guard(() async {
        await _dio.post('/v1/auth/logout', data: {'refreshToken': refreshToken});
      });

  /// `PATCH /v1/me/profile` — onboarding completion, username changes, and (D-219) the rest of the
  /// self-declared display fields. Partial by design: an omitted field is left untouched server-side.
  Future<void> updateProfile({
    String? name,
    String? username,
    String? headline,
    String? bio,
    List<String>? skills,
    List<String>? languages,
    List<String>? interests,
    String? educationJson,
    String? linksJson,
    String? avatarKey,
    String? coverKey,
    DateTime? dateOfBirth,
  }) =>
      guard(() async {
        await _dio.patch('/v1/me/profile', data: {
          'name': ?name,
          'username': ?username,
          'headline': ?headline,
          'bio': ?bio,
          // Date-only on the wire: the column is a Postgres `date`, and sending a timestamp would
          // let the zone shift the calendar day.
          'dateOfBirth': ?_dateOnly(dateOfBirth),
          'skills': ?skills,
          'languages': ?languages,
          'interests': ?interests,
          'educationJson': ?educationJson,
          'linksJson': ?linksJson,
          'avatarKey': ?avatarKey,
          'coverKey': ?coverKey,
        });
      });

  static String? _dateOnly(DateTime? d) => d == null
      ? null
      : '${d.year.toString().padLeft(4, '0')}-'
          '${d.month.toString().padLeft(2, '0')}-'
          '${d.day.toString().padLeft(2, '0')}';

  /// `PATCH /v1/me/privacy` (D-219; per-section tiers D-221) — partial: omitted flags and sections
  /// are left unchanged, and a named section overrides the boolean covering it.
  Future<void> updatePrivacy({
    bool? profilePublic,
    bool? showAttended,
    bool? showCertificates,
    bool? showAllies,
    Map<String, String>? sections,
  }) =>
      guard(() async {
        await _dio.patch('/v1/me/privacy', data: {
          'profilePublic': ?profilePublic,
          'showAttended': ?showAttended,
          'showCertificates': ?showCertificates,
          'showAllies': ?showAllies,
          'sections': ?sections,
        });
      });

  /// `POST /v1/me/profile-image/presign` (D-219) — step 1 of the two-step image upload; the caller
  /// PUTs the bytes to the returned URL and then persists `key` via [updateProfile].
  Future<({String key, String url, Map<String, String> headers})> presignProfileImage({
    required String slot,
    required String contentType,
    required int maxBytes,
  }) =>
      guard(() async {
        final res = await _dio.post('/v1/me/profile-image/presign', data: {
          'slot': slot,
          'contentType': contentType,
          'maxBytes': maxBytes,
        });
        final m = (res.data as Map);
        return (
          key: m['key'] as String,
          url: m['url'] as String,
          headers: ((m['headers'] as Map?) ?? const {}).map((k, v) => MapEntry('$k', '$v')),
        );
      });

  /// `GET /v1/usernames/availability` — returns available/unavailable/reserved/invalid.
  Future<String> usernameAvailability(String username) => guard(() async {
        final res = await _dio.get('/v1/usernames/availability', queryParameters: {'username': username});
        return (res.data as Map)['status'] as String;
      });

  /// Step 2 of the image upload: PUT the bytes straight to storage. Deliberately a bare [Dio] with no
  /// base URL or auth interceptor — a presigned URL carries its own authorization in the signature, and
  /// attaching the session bearer to a third-party storage host would leak it (D-110).
  Future<void> putPresigned(
    String url,
    Map<String, String> headers,
    String contentType,
    List<int> bytes,
  ) =>
      guard(() async {
        await Dio().put(
          url,
          data: Stream.fromIterable([bytes]),
          options: Options(
            headers: {...headers, 'Content-Length': bytes.length},
            contentType: contentType,
          ),
        );
      });

  // ── Registration ceremony (Phase 2D) ──────────────────────────────────────
  // All authenticated: registration begins with the OTP login above, then the client drives the
  // remaining steps from this status.

  Future<RegistrationStatusDto> registrationStatus() => guard(() async {
        final res = await _dio.get('/v1/auth/registration/status');
        return RegistrationStatusDto.fromJson((res.data as Map).cast<String, dynamic>());
      });

  /// Sends a verification code to [email]. Rejects a malformed address or one owned by another account.
  Future<void> startEmailVerification(String email) => guard(() async {
        await _dio.post('/v1/auth/email/verify/start', data: {'email': email});
      });

  /// Verifies the emailed code; on success the backend sets and marks the email verified.
  Future<void> completeEmailVerification(String email, String code) => guard(() async {
        await _dio.post('/v1/auth/email/verify/complete', data: {'email': email, 'code': code});
      });

  // ── Password lifecycle (Phase 2C/2D, D-126/D-127/D-129) ────────────────────
  // Create/change are authenticated; the reset ceremony is anonymous (the user can't sign in) and
  // requires OTP + a recovery code (INV-B).

  Future<({bool hasPassword, int minLength, int maxLength})> passwordStatus() => guard(() async {
        final res = await _dio.get('/v1/auth/password/status');
        final m = (res.data as Map);
        return (
          hasPassword: m['has_password'] as bool? ?? false,
          minLength: (m['min_length'] as num?)?.toInt() ?? 12,
          maxLength: (m['max_length'] as num?)?.toInt() ?? 128,
        );
      });

  /// First-time creation. Fails with `password_already_set` if one exists — use [changePassword] then.
  Future<void> setPassword(String password) => guard(() async {
        await _dio.post('/v1/auth/password/set', data: {'password': password});
      });

  Future<void> changePassword(String currentPassword, String newPassword) => guard(() async {
        await _dio.post('/v1/auth/password/change',
            data: {'currentPassword': currentPassword, 'newPassword': newPassword});
      });

  /// Anonymous — always succeeds (anti-enumeration), sending a reset OTP to the account if it exists.
  Future<void> startPasswordReset(String identifier) => guard(() async {
        await _dio.post('/v1/auth/password/reset/start', data: {'identifier': identifier});
      });

  /// Anonymous. OTP + recovery code (INV-B). On success returns a fresh session (the reset logs you in).
  Future<TokenResponseDto> completePasswordReset({
    required String identifier,
    required String otpCode,
    required String newPassword,
    required String recoveryCode,
  }) =>
      guard(() async {
        final res = await _dio.post('/v1/auth/password/reset/complete', data: {
          'identifier': identifier,
          'otpCode': otpCode,
          'newPassword': newPassword,
          'recoveryCode': recoveryCode,
        });
        return TokenResponseDto.fromJson((res.data as Map).cast<String, dynamic>());
      });
}
