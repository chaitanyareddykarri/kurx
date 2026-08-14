import 'package:dio/dio.dart';

import '../../../../core/network/api_guard.dart';
import '../models/token_response_dto.dart';
import '../models/trusted_device_dtos.dart';

/// HTTP layer for the trusted-device authentication platform (AM2–AM9).
///
/// Kept separate from [AuthRemoteDataSource] (the legacy OTP surface) so the two auth generations
/// stay visibly distinct while both are supported — see the staged migration in D-089.
class TrustedDeviceRemoteDataSource {
  TrustedDeviceRemoteDataSource(this._dio);

  final Dio _dio;

  // ── Enrollment (AM2/D-079) ────────────────────────────────────────────────

  /// Step 1: register this device's public key. The device is PendingVerification until it proves
  /// possession by signing the returned nonce.
  Future<DeviceEnrollmentDto> beginEnrollment({
    required String name,
    required String platform,
    required String publicKeySpki,
  }) =>
      guard(() async {
        final res = await _dio.post('/v1/auth/devices/enroll', data: {
          'name': name,
          'platform': platform,
          'publicKeySpki': publicKeySpki,
          'alg': 'ES256',
        });
        return DeviceEnrollmentDto.fromJson((res.data as Map).cast<String, dynamic>());
      });

  /// Step 2: prove possession. Only after this does the device reach Trusted.
  Future<void> completeEnrollment({
    required String challengeId,
    required String deviceId,
    required String signature,
  }) =>
      guard(() async {
        await _dio.post('/v1/auth/devices/enroll/verify', data: {
          'challengeId': challengeId,
          'deviceId': deviceId,
          'signature': signature,
        });
      });

  Future<List<TrustedDeviceDto>> listDevices() => guard(() async {
        final res = await _dio.get('/v1/auth/devices');
        return (res.data as List)
            .map((e) => TrustedDeviceDto.fromJson((e as Map).cast<String, dynamic>()))
            .toList();
      });

  /// Revoking also signs out every session the device holds (D-081) — the "lost my phone" action.
  Future<void> revokeDevice(String deviceId) => guard(() async {
        await _dio.post('/v1/auth/devices/$deviceId/revoke', data: const {});
      });

  // ── Push-approval login, device side (AM4/D-080) ──────────────────────────

  /// Challenges awaiting this user's approval. Polled on resume and refreshed by the FCM
  /// data message, so an approval still works when push is disabled or throttled.
  Future<List<PendingLoginDto>> pendingLogins() => guard(() async {
        final res = await _dio.get('/v1/auth/login/pending');
        return (res.data as List)
            .map((e) => PendingLoginDto.fromJson((e as Map).cast<String, dynamic>()))
            .toList();
      });

  Future<void> approveLogin({
    required String challengeId,
    required String deviceId,
    required int matchNumber,
    required String signature,
  }) =>
      guard(() async {
        await _dio.post('/v1/auth/login/approve', data: {
          'challengeId': challengeId,
          'deviceId': deviceId,
          'matchNumber': matchNumber,
          'signature': signature,
        });
      });

  Future<void> rejectLogin(String challengeId) => guard(() async {
        await _dio.post('/v1/auth/login/reject', data: {'challengeId': challengeId});
      });

  // ── Password-first login (Phase 2B/D-182) ─────────────────────────────────
  // This client always sends rememberBrowser:false — on mobile the trusted *device key* is factor 2,
  // not a browser cookie. The reply is either a session (a device is already trusted) or a
  // device-approval challenge to wait on with [loginStatus].

  Future<PasswordLoginDto> loginPassword({
    required String identifier,
    required String password,
  }) =>
      guard(() async {
        final res = await _dio.post('/v1/auth/login/password', data: {
          'identifier': identifier,
          'password': password,
          'rememberBrowser': false,
          // Surface only reorders the returned methods (D-283) — on a handset the "trusted device" is
          // usually this device, which is not a second factor, so codes rank first here.
          'surface': 'mobile',
        });
        return PasswordLoginDto.fromJson((res.data as Map).cast<String, dynamic>());
      });

  /// Asks the server to deliver a code for one of the methods it offered. The poll token is what proves
  /// this caller passed the password step — a challenge id alone yields nothing (D-280).
  Future<void> sendSecondFactorCode({
    required String challengeId,
    required String pollToken,
    required String method,
  }) =>
      guard(() async {
        await _dio.post('/v1/auth/login/second-factor/send', data: {
          'challengeId': challengeId,
          'pollToken': pollToken,
          'method': method,
        });
      });

  Future<TokenResponseDto> verifySecondFactorCode({
    required String challengeId,
    required String pollToken,
    required String code,
  }) =>
      guard(() async {
        final res = await _dio.post('/v1/auth/login/second-factor/verify', data: {
          'challengeId': challengeId,
          'pollToken': pollToken,
          'code': code,
        });
        return TokenResponseDto.fromJson((res.data as Map).cast<String, dynamic>());
      });

  // ── Push-approval login status (AM4/D-080) ────────────────────────────────
  // The password-first login's device-approval branch (D-182) polls this. The passwordless
  // `/login/start` initiation was removed — no client ever called it.

  /// Polls for the outcome. The poll token is what binds this client to the login — a challenge id
  /// alone yields nothing (D-080).
  Future<LoginStatusDto> loginStatus({required String challengeId, required String pollToken}) =>
      guard(() async {
        final res = await _dio.post('/v1/auth/login/status', data: {
          'challengeId': challengeId,
          'pollToken': pollToken,
        });
        return LoginStatusDto.fromJson((res.data as Map).cast<String, dynamic>());
      });

  // ── Passkeys (AM3/D-086) ──────────────────────────────────────────────────
  // The `options` / `response` payloads are opaque W3C WebAuthn JSON, passed straight through to
  // and from Credential Manager. Nothing here parses them — the server and the platform are the
  // only two parties that need to understand their contents (D-097).

  Future<PasskeyCeremonyDto> passkeyRegisterOptions() => guard(() async {
        final res = await _dio.post('/v1/auth/passkeys/register/options', data: const {});
        return PasskeyCeremonyDto.fromJson((res.data as Map).cast<String, dynamic>());
      });

  Future<String> completePasskeyRegistration({
    required String challengeId,
    required Map<String, dynamic> response,
    required String deviceName,
  }) =>
      guard(() async {
        final res = await _dio.post('/v1/auth/passkeys/register', data: {
          'challengeId': challengeId,
          'response': response,
          'deviceName': deviceName,
        });
        return (res.data as Map)['device_id'] as String;
      });

  /// Anti-enumeration: returns well-formed options even for an unknown identifier, with an empty
  /// credential list. The caller must not treat success as proof the account exists.
  Future<PasskeyCeremonyDto> passkeyLoginOptions(String identifier) => guard(() async {
        final res = await _dio.post('/v1/auth/passkeys/login/options', data: {'identifier': identifier});
        return PasskeyCeremonyDto.fromJson((res.data as Map).cast<String, dynamic>());
      });

  Future<TokenResponseDto> completePasskeyLogin({
    required String challengeId,
    required Map<String, dynamic> response,
  }) =>
      guard(() async {
        final res = await _dio.post('/v1/auth/passkeys/login', data: {
          'challengeId': challengeId,
          'response': response,
        });
        return TokenResponseDto.fromJson((res.data as Map).cast<String, dynamic>());
      });

  Future<List<TrustedDeviceDto>> listPasskeys() => guard(() async {
        final res = await _dio.get('/v1/auth/passkeys');
        return (res.data as List)
            .map((e) => TrustedDeviceDto.fromJson((e as Map).cast<String, dynamic>()))
            .toList();
      });

  // ── Sessions / "your devices" (AM5/D-081) ─────────────────────────────────

  Future<List<AuthSessionDto>> listSessions() => guard(() async {
        final res = await _dio.get('/v1/auth/sessions');
        return (res.data as List)
            .map((e) => AuthSessionDto.fromJson((e as Map).cast<String, dynamic>()))
            .toList();
      });

  Future<void> revokeSession(String sessionId) => guard(() async {
        await _dio.post('/v1/auth/sessions/$sessionId/revoke', data: const {});
      });

  // ── Security Center (Phase 2E) + trusted browsers (Phase 2A) ──────────────

  Future<SecurityOverviewDto> securityOverview() => guard(() async {
        final res = await _dio.get('/v1/auth/security-center');
        return SecurityOverviewDto.fromJson((res.data as Map).cast<String, dynamic>());
      });

  Future<List<SecurityActivityDto>> securityActivity({int limit = 25}) => guard(() async {
        final res = await _dio.get('/v1/auth/security-center/activity', queryParameters: {'limit': limit});
        return (res.data as List)
            .map((e) => SecurityActivityDto.fromJson((e as Map).cast<String, dynamic>()))
            .toList();
      });

  /// The panic button: revokes every session and forgets every trusted browser. Returns the count.
  Future<int> signOutEverywhere() => guard(() async {
        final res = await _dio.post('/v1/auth/security-center/sign-out-all', data: const {});
        return ((res.data as Map)['revoked'] as num?)?.toInt() ?? 0;
      });

  Future<List<TrustedBrowserDto>> listTrustedBrowsers() => guard(() async {
        final res = await _dio.get('/v1/auth/trusted-browsers');
        return (res.data as List)
            .map((e) => TrustedBrowserDto.fromJson((e as Map).cast<String, dynamic>()))
            .toList();
      });

  Future<void> revokeTrustedBrowser(String id) => guard(() async {
        await _dio.post('/v1/auth/trusted-browsers/$id/revoke', data: const {});
      });

  // ── Step-up (AM6/D-084) ───────────────────────────────────────────────────

  Future<StepUpStatusDto> stepUpStatus() => guard(() async {
        final res = await _dio.get('/v1/auth/step-up/status');
        return StepUpStatusDto.fromJson((res.data as Map).cast<String, dynamic>());
      });

  Future<StepUpChallengeDto> startStepUp(String action) => guard(() async {
        final res = await _dio.post('/v1/auth/step-up/start', data: {'action': action});
        return StepUpChallengeDto.fromJson((res.data as Map).cast<String, dynamic>());
      });

  Future<void> verifyStepUp({
    required String challengeId,
    required String deviceId,
    required int matchNumber,
    required String signature,
  }) =>
      guard(() async {
        await _dio.post('/v1/auth/step-up/verify', data: {
          'challengeId': challengeId,
          'deviceId': deviceId,
          'matchNumber': matchNumber,
          'signature': signature,
        });
      });

  // ── Recovery codes (AM7/D-083) ────────────────────────────────────────────

  Future<int> recoveryCodesRemaining() => guard(() async {
        final res = await _dio.get('/v1/auth/recovery-codes');
        return ((res.data as Map)['remaining'] as num).toInt();
      });

  /// The plaintext codes exist outside the server exactly once — the caller must show them and
  /// must not persist them anywhere on the device.
  Future<List<String>> generateRecoveryCodes() => guard(() async {
        final res = await _dio.post('/v1/auth/recovery-codes', data: const {});
        return ((res.data as Map)['codes'] as List).cast<String>();
      });

  /// Anti-enumeration: always succeeds, even for an unknown account.
  Future<void> startRecovery(String identifier) => guard(() async {
        await _dio.post('/v1/auth/recovery/start', data: {'identifier': identifier});
      });

  Future<TokenResponseDto> redeemRecovery({
    required String identifier,
    required String otpCode,
    required String recoveryCode,
  }) =>
      guard(() async {
        final res = await _dio.post('/v1/auth/recovery/redeem', data: {
          'identifier': identifier,
          'otpCode': otpCode,
          'recoveryCode': recoveryCode,
        });
        return TokenResponseDto.fromJson((res.data as Map).cast<String, dynamic>());
      });
}
