import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/network/auth_retry.dart';
import '../../../../core/storage/token_store.dart';
import '../../../../core/network/network_providers.dart';
import '../../../../core/security/device_key_service.dart';
import '../../../../core/security/passkey_service.dart';
import '../datasources/trusted_device_remote_data_source.dart';
import '../models/trusted_device_dtos.dart';

/// Orchestrates the device-key ceremonies (AM2/AM4/AM6): every one of them is
/// "ask the server for a nonce → sign it in the secure element → send the signature back".
///
/// The signing step prompts for biometrics on both platforms, so each of these methods is a
/// user-present action and must only ever be called from an explicit user gesture — never on a
/// timer, never from a background isolate.
class TrustedDeviceRepository {
  TrustedDeviceRepository(this._remote, this._keys, this._tokens, this._passkeys);

  final TrustedDeviceRemoteDataSource _remote;
  final DeviceKeyService _keys;
  final TokenStore _tokens;
  final PasskeyService _passkeys;

  /// Full two-step enrollment. Returns the new device id.
  ///
  /// The key is created first and only then registered, so a failure part-way leaves an unused
  /// keystore entry rather than a server-side device with no key behind it.
  Future<String> enrollThisDevice({required String name, required String platform}) async {
    final spki = await _keys.publicKeySpki() ?? await _keys.createKey();

    final enrollment = await _remote.beginEnrollment(
      name: name,
      platform: platform,
      publicKeySpki: spki,
    );
    final signature = await _keys.sign(enrollment.nonce);
    await _remote.completeEnrollment(
      challengeId: enrollment.challengeId,
      deviceId: enrollment.deviceId,
      signature: signature,
    );
    // Recorded only after the server confirms the enrollment, so a failed verify never leaves a
    // device id behind that the approve/step-up screens would then sign with.
    await _tokens.saveDeviceId(enrollment.deviceId);
    return enrollment.deviceId;
  }

  Future<List<PendingLoginDto>> pendingLogins() => _remote.pendingLogins();

  /// Approves a waiting sign-in. [deviceId] must be *this* device's id — the server rejects a
  /// signature from any other device, but sending the wrong one wastes a biometric prompt.
  ///
  /// Retried only for faults that never reached the server (see [AuthRetry]); an approval is **not**
  /// queued for later delivery, because the challenge expires in ~120s and replaying the intent
  /// against context the user can no longer see is exactly what number-matching guards against.
  Future<void> approveLogin({
    required PendingLoginDto challenge,
    required String deviceId,
    required int matchNumber,
  }) async {
    // D-181: sign "{nonce}.{NN}" — the two-digit match number the user read from the waiting browser and
    // entered here — so the hardware signature attests to those exact digits and cannot be replayed
    // against a challenge showing a different number.
    final payload = '${challenge.nonce}.${matchNumber.toString().padLeft(2, '0')}';
    final signature = await _keys.sign(payload);
    await AuthRetry.run(
      () => _remote.approveLogin(
        challengeId: challenge.challengeId,
        deviceId: deviceId,
        matchNumber: matchNumber,
        signature: signature,
      ),
      // Not idempotent: the challenge is single-use, so an ambiguous timeout must not be repeated.
      isIdempotent: false,
    );
  }

  /// Rejecting needs no signature: declining is always safe, and demanding biometrics to say "no"
  /// would leave a suspicious request pending whenever the user can't authenticate.
  /// Safe to repeat — rejecting an already-rejected challenge changes nothing.
  Future<void> rejectLogin(String challengeId) =>
      AuthRetry.run(() => _remote.rejectLogin(challengeId), isIdempotent: true);

  Future<void> stepUp({required String action, required String deviceId}) async {
    final challenge = await _remote.startStepUp(action);
    // D-181: step-up binds the match number into the signed payload too. It happens on this same device
    // (no second screen to read from), so the challenge's own match number is what is signed and sent.
    final payload = '${challenge.nonce}.${challenge.matchNumber.toString().padLeft(2, '0')}';
    final signature = await _keys.sign(payload);
    await _remote.verifyStepUp(
      challengeId: challenge.challengeId,
      deviceId: deviceId,
      matchNumber: challenge.matchNumber,
      signature: signature,
    );
  }

  Future<List<TrustedDeviceDto>> listDevices() => _remote.listDevices();

  Future<List<AuthSessionDto>> listSessions() => _remote.listSessions();

  // ── Security Center (Phase 2E) + trusted browsers (Phase 2A) ──────────────

  Future<SecurityOverviewDto> securityOverview() => _remote.securityOverview();

  Future<List<SecurityActivityDto>> securityActivity() => _remote.securityActivity();

  Future<List<TrustedBrowserDto>> listTrustedBrowsers() => _remote.listTrustedBrowsers();

  /// Idempotent: forgetting an already-forgotten browser is a no-op server-side.
  Future<void> revokeTrustedBrowser(String id) =>
      AuthRetry.run(() => _remote.revokeTrustedBrowser(id), isIdempotent: true);

  /// The panic button. Idempotent — running it twice just revokes nothing the second time.
  Future<int> signOutEverywhere() =>
      AuthRetry.run(() => _remote.signOutEverywhere(), isIdempotent: true);

  /// Idempotent: revoking an already-revoked session is a no-op server-side, so an ambiguous
  /// timeout is safe to repeat.
  Future<void> revokeSession(String sessionId) =>
      AuthRetry.run(() => _remote.revokeSession(sessionId), isIdempotent: true);

  /// Revoking *this* device also drops its local key — leaving it behind would let a later enroll
  /// silently reuse a key the server has already marked revoked.
  Future<void> revokeDevice(String deviceId, {required bool isThisDevice}) async {
    await _remote.revokeDevice(deviceId);
    if (isThisDevice) {
      await _keys.deleteKey();
      await _tokens.forgetDevice();
    }
  }

  // ── Passkeys (AM3/D-097) ──────────────────────────────────────────────────
  // Same three-step shape as every other ceremony: ask the server for options, run them on the
  // platform, send the result back. The passkey enrols as an ordinary TrustedDevice server-side,
  // so listing/revocation/step-up need no passkey-specific code at all.

  /// Registers a passkey for the signed-in user. Returns the new trusted-device id.
  Future<String> registerPasskey({required String deviceName}) async {
    final ceremony = await _remote.passkeyRegisterOptions();
    final response = await _passkeys.register(ceremony.optionsJson);
    return _remote.completePasskeyRegistration(
      challengeId: ceremony.challengeId,
      response: response,
      deviceName: deviceName,
    );
  }

  /// Signs in with a passkey and stores the resulting session.
  ///
  /// Not retried on an ambiguous timeout: the assertion consumes a single-use challenge, so a
  /// repeat could burn a second ceremony for one sign-in.
  Future<void> loginWithPasskey(String identifier) async {
    final ceremony = await _remote.passkeyLoginOptions(identifier);
    final response = await _passkeys.authenticate(ceremony.optionsJson);
    final tokens = await _remote.completePasskeyLogin(
      challengeId: ceremony.challengeId,
      response: response,
    );
    await _tokens.save(access: tokens.accessToken, refresh: tokens.refreshToken);
  }

  Future<List<TrustedDeviceDto>> listPasskeys() => _remote.listPasskeys();

  Future<bool> passkeysAvailable() => _passkeys.isAvailable();

  Future<StepUpStatusDto> stepUpStatus() => _remote.stepUpStatus();

  Future<int> recoveryCodesRemaining() => _remote.recoveryCodesRemaining();

  Future<List<String>> generateRecoveryCodes() => _remote.generateRecoveryCodes();

  /// Anti-enumeration: succeeds even for an unknown account, so the caller must not treat success
  /// as proof the account exists. Idempotent — asking twice just re-sends.
  Future<void> startRecovery(String identifier) =>
      AuthRetry.run(() => _remote.startRecovery(identifier), isIdempotent: true);

  /// Consumes a recovery code + OTP and stores the resulting session.
  ///
  /// Not retried on an ambiguous timeout: the recovery code is single-use, so a repeat could burn a
  /// second code for one recovery.
  Future<void> redeemRecovery({
    required String identifier,
    required String otpCode,
    required String recoveryCode,
  }) async {
    final tokens = await _remote.redeemRecovery(
      identifier: identifier,
      otpCode: otpCode,
      recoveryCode: recoveryCode,
    );
    await _tokens.save(access: tokens.accessToken, refresh: tokens.refreshToken);
  }
}

final trustedDeviceRemoteDataSourceProvider = Provider<TrustedDeviceRemoteDataSource>(
  (ref) => TrustedDeviceRemoteDataSource(ref.watch(dioProvider)),
);

final trustedDeviceRepositoryProvider = Provider<TrustedDeviceRepository>(
  (ref) => TrustedDeviceRepository(
    ref.watch(trustedDeviceRemoteDataSourceProvider),
    ref.watch(deviceKeyServiceProvider),
    ref.watch(tokenStoreProvider),
    ref.watch(passkeyServiceProvider),
  ),
);

/// This device's trusted-device id, or null when it has never been enrolled.
///
/// Not autoDispose: it is read on nearly every security screen and changes only at enroll/revoke,
/// both of which invalidate it explicitly.
final thisDeviceIdProvider = FutureProvider<String?>(
  (ref) => ref.watch(tokenStoreProvider).deviceId(),
);

/// Pending approvals, refreshed on demand (app resume, push receipt, pull-to-refresh).
final pendingLoginsProvider = FutureProvider.autoDispose<List<PendingLoginDto>>(
  (ref) => ref.watch(trustedDeviceRepositoryProvider).pendingLogins(),
);

final trustedDevicesProvider = FutureProvider.autoDispose<List<TrustedDeviceDto>>(
  (ref) => ref.watch(trustedDeviceRepositoryProvider).listDevices(),
);

/// Passkeys are a subset of trusted devices server-side; listed separately only so the UI can show
/// them under their own heading.
final passkeysProvider = FutureProvider.autoDispose<List<TrustedDeviceDto>>(
  (ref) => ref.watch(trustedDeviceRepositoryProvider).listPasskeys(),
);

final authSessionsProvider = FutureProvider.autoDispose<List<AuthSessionDto>>(
  (ref) => ref.watch(trustedDeviceRepositoryProvider).listSessions(),
);

final securityOverviewProvider = FutureProvider.autoDispose<SecurityOverviewDto>(
  (ref) => ref.watch(trustedDeviceRepositoryProvider).securityOverview(),
);

final securityActivityProvider = FutureProvider.autoDispose<List<SecurityActivityDto>>(
  (ref) => ref.watch(trustedDeviceRepositoryProvider).securityActivity(),
);

final trustedBrowsersProvider = FutureProvider.autoDispose<List<TrustedBrowserDto>>(
  (ref) => ref.watch(trustedDeviceRepositoryProvider).listTrustedBrowsers(),
);
