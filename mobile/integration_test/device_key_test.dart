import 'dart:convert';

import 'package:flutter/foundation.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:integration_test/integration_test.dart';
import 'package:kurx_mobile/core/security/device_key_service.dart';

/// On-device verification of the native device-key plugin (D-092).
///
/// Host-VM widget tests cannot touch AndroidKeyStore or the Secure Enclave at all — they only ever
/// exercise a fake. Everything asserted here runs against the **real** platform keystore on a real
/// Android runtime, which is the only way claims like "the key is inside secure hardware" or
/// "signing requires user authentication" can be anything other than an assertion in a comment.
///
/// Run with:  flutter test integration_test/device_key_test.dart -d emulator-5554
///
/// The suite deliberately **adapts to the device** rather than assuming one: a device with no
/// enrolled PIN/biometric cannot create an auth-required key at all, and refusing to create one is
/// the correct behaviour, so that is asserted as a first-class outcome rather than skipped.
void main() {
  IntegrationTestWidgetsFlutterBinding.ensureInitialized();

  const channel = MethodChannel(DeviceKeyPluginChannel.name);
  const service = PlatformDeviceKeyService();

  Future<Map<String, dynamic>> securityInfo() async {
    final raw = await channel.invokeMapMethod<String, dynamic>('securityInfo');
    return raw ?? <String, dynamic>{};
  }

  /// BiometricManager.canAuthenticate() == BIOMETRIC_SUCCESS
  const biometricSuccess = 0;

  setUp(() async {
    // Each test starts from a known state; deleting a non-existent key is a no-op.
    await service.deleteKey();
  });

  tearDownAll(() async {
    await service.deleteKey();
  });

  testWidgets('the plugin is registered and reports the device security posture', (tester) async {
    final info = await securityInfo();

    // Proves the MethodChannel is wired to the native side at all — a missing registration
    // surfaces as MissingPluginException, which is the failure this guards against.
    expect(info['apiLevel'], isA<int>());
    expect(info['strongBoxSupported'], isA<bool>());
    expect(info['biometricStatus'], isA<int>());

    // Not an assertion, a record: printed into the run log so the evidence for what this
    // particular device supports is captured alongside the pass/fail.
    debugPrint('DEVICE SECURITY POSTURE: $info');
  });

  testWidgets('key lifecycle behaves correctly for this device\'s auth capability', (tester) async {
    final before = await securityInfo();
    final canAuthenticate = before['biometricStatus'] == biometricSuccess;

    expect(await service.hasKey(), isFalse, reason: 'setUp should have cleared the key');

    if (!canAuthenticate) {
      // No PIN/biometric enrolled. Creating an auth-required key would produce one that can never
      // be used, so the plugin must refuse up front rather than fail later mid-login.
      await expectLater(
        service.createKey(),
        throwsA(isA<DeviceKeyException>()
            .having((e) => e.requiresReenrollment || e.code == 'unavailable', 'is a setup error', isTrue)),
      );
      expect(await service.hasKey(), isFalse, reason: 'a refused creation must leave no key behind');
      return;
    }

    final spki = await service.createKey();

    // A P-256 SubjectPublicKeyInfo is 91 bytes -> 124 base64 chars. Checking the shape catches a
    // plugin returning a raw point or a private key by mistake.
    expect(spki, isNotEmpty);
    expect(base64Decode(spki).length, 91,
        reason: 'expected a 91-byte P-256 SubjectPublicKeyInfo');
    expect(await service.hasKey(), isTrue);
    expect(await service.publicKeySpki(), spki, reason: 'the key must be stable across reads');

    final after = await securityInfo();

    // The security claims the architecture rests on, read back from the key itself rather than
    // assumed from the generation request (D-091).
    expect(after['insideSecureHardware'], isTrue,
        reason: 'the private key must live in the TEE/StrongBox, not app memory');
    expect(after['userAuthenticationRequired'], isTrue,
        reason: 'a signature must prove a human was present, not just that the app ran');
    expect(after['invalidatedByBiometricEnrollment'], anyOf(isTrue, isNull),
        reason: 'enrolling a new biometric must invalidate the old proof-of-possession');

    debugPrint('KEY POSTURE AFTER CREATE: $after');

    // Deletion is what "revoke this device" relies on locally.
    await service.deleteKey();
    expect(await service.hasKey(), isFalse);
    expect(await service.publicKeySpki(), isNull);
  });

  testWidgets('signing without a key reports not_enrolled rather than crashing', (tester) async {
    expect(await service.hasKey(), isFalse);

    await expectLater(
      service.sign('some-nonce'),
      throwsA(isA<DeviceKeyException>().having((e) => e.code, 'code', 'not_enrolled')),
    );
  });
}

/// Channel name kept in one place so the test cannot drift from the implementation.
class DeviceKeyPluginChannel {
  static const name = 'kurx/device_key';
}
