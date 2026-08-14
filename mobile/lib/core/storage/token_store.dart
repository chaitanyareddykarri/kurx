import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';

/// Session tokens at rest.
///
/// Keychain (iOS) / EncryptedSharedPreferences-backed Keystore (Android) only — never Hive, never
/// SharedPreferences. A refresh token is a bearer credential for the whole account, so it must sit
/// behind platform-managed encryption that survives neither a backup extraction nor a rooted-device
/// file read as plaintext.
///
/// `AuthInterceptor` serialises refreshes through a single in-flight future because reusing an
/// already-rotated refresh token is treated as theft by the backend (D-009/D-014, narrowed to the
/// session family for device-bound tokens by D-081).
class TokenStore {
  TokenStore(this._storage);

  final FlutterSecureStorage _storage;

  static const _accessKey = 'kurx_access_token';
  static const _refreshKey = 'kurx_refresh_token';
  static const _deviceIdKey = 'kurx_trusted_device_id';

  Future<void> save({required String access, required String refresh}) async {
    await _storage.write(key: _accessKey, value: access);
    await _storage.write(key: _refreshKey, value: refresh);
  }

  Future<String?> accessToken() => _storage.read(key: _accessKey);

  Future<String?> refreshToken() => _storage.read(key: _refreshKey);

  /// This device's trusted-device id, recorded at enrollment.
  ///
  /// Approving a sign-in and completing a step-up both have to name *which* device is signing. The
  /// server rejects a signature from any other device, so without this the screens that need it
  /// cannot be reached at all — it is stored rather than re-fetched because those screens open from
  /// a push notification, possibly before any list request has run.
  ///
  /// Deliberately survives [clear]: the enrollment is a property of the hardware, not of the
  /// session. Signing out and back in must not orphan a keystore key that is still registered
  /// server-side. It is removed by [forgetDevice] when the device is actually revoked.
  Future<void> saveDeviceId(String deviceId) => _storage.write(key: _deviceIdKey, value: deviceId);

  Future<String?> deviceId() => _storage.read(key: _deviceIdKey);

  Future<void> forgetDevice() => _storage.delete(key: _deviceIdKey);

  /// A refresh token is what makes a cold start resumable — an expired access token alone is not
  /// "logged out", the interceptor can still rotate it.
  Future<bool> hasSession() async {
    final refresh = await _storage.read(key: _refreshKey);
    return refresh != null && refresh.isNotEmpty;
  }

  Future<void> clear() async {
    await _storage.delete(key: _accessKey);
    await _storage.delete(key: _refreshKey);
  }
}

final secureStorageProvider = Provider<FlutterSecureStorage>((ref) {
  return const FlutterSecureStorage(
    // Android: force the EncryptedSharedPreferences implementation rather than the legacy
    // plaintext-with-RSA-wrapped-key path.
    aOptions: AndroidOptions(encryptedSharedPreferences: true),
    // iOS: unlocked-this-device-only, so tokens never ride along in an iCloud/iTunes backup
    // onto a different handset.
    iOptions: IOSOptions(accessibility: KeychainAccessibility.first_unlock_this_device),
  );
});

final tokenStoreProvider = Provider<TokenStore>((ref) => TokenStore(ref.watch(secureStorageProvider)));
