import 'dart:convert';

import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

/// Runs the two WebAuthn ceremonies through Android Credential Manager (AM3, D-097).
///
/// The platform speaks the **W3C WebAuthn JSON serialization**, which is exactly what the backend's
/// fido2-net-lib already emits and accepts. So this service is a typed shuttle: server options in,
/// platform response out, with no field of either ever inspected. Parsing here would duplicate the
/// server's WebAuthn schema and give the encoding a second place to drift — the classic way these
/// integrations fail with an opaque "invalid signature".
abstract class PasskeyService {
  /// Whether this platform can run passkey ceremonies at all.
  Future<bool> isAvailable();

  /// Registration. [optionsJson] is the server's options object; returns the platform's
  /// `registrationResponseJson` decoded to a map for posting back.
  Future<Map<String, dynamic>> register(Map<String, dynamic> optionsJson);

  /// Authentication. Returns the platform's `authenticationResponseJson` as a map.
  Future<Map<String, dynamic>> authenticate(Map<String, dynamic> optionsJson);
}

class PasskeyException implements Exception {
  const PasskeyException(this.code, [this.message]);

  /// `user_canceled` | `already_registered` | `no_credential` | `no_provider` | `dom_error`
  /// | `unavailable` | `unknown`
  final String code;
  final String? message;

  /// True when the outcome is a normal state of the world rather than a fault — the UI should
  /// explain and offer an alternative, not show an error.
  bool get isBenign =>
      code == 'user_canceled' || code == 'already_registered' || code == 'no_credential';

  /// True when this device simply cannot do passkeys, so the app should fall back to the
  /// device-key rail (trusted-device approval) rather than dead-ending the user.
  bool get needsFallback => code == 'no_provider' || code == 'unavailable';

  @override
  String toString() => 'PasskeyException($code${message == null ? '' : ': $message'})';
}

class PlatformPasskeyService implements PasskeyService {
  const PlatformPasskeyService([this._channel = const MethodChannel('kurx/passkey')]);

  final MethodChannel _channel;

  @override
  Future<bool> isAvailable() async {
    try {
      return await _channel.invokeMethod<bool>('isAvailable') ?? false;
    } on MissingPluginException {
      return false;                       // iOS not implemented yet, or plugin not registered
    } on PlatformException {
      return false;
    }
  }

  @override
  Future<Map<String, dynamic>> register(Map<String, dynamic> optionsJson) =>
      _invoke('register', optionsJson);

  @override
  Future<Map<String, dynamic>> authenticate(Map<String, dynamic> optionsJson) =>
      _invoke('authenticate', optionsJson);

  Future<Map<String, dynamic>> _invoke(String method, Map<String, dynamic> optionsJson) async {
    try {
      final responseJson = await _channel.invokeMethod<String>(
        method,
        {'requestJson': jsonEncode(optionsJson)},
      );
      if (responseJson == null || responseJson.isEmpty) {
        throw const PasskeyException('unknown', 'Platform returned no response');
      }
      final decoded = jsonDecode(responseJson);
      if (decoded is! Map) {
        throw const PasskeyException('unknown', 'Platform response was not an object');
      }
      return decoded.cast<String, dynamic>();
    } on PlatformException catch (e) {
      // Native codes are already the vocabulary above; carry them through unchanged so callers
      // branch on a stable value rather than on a localized OS message.
      throw PasskeyException(e.code, e.message);
    } on MissingPluginException {
      throw const PasskeyException('unavailable', 'Passkeys are not supported on this platform');
    } on FormatException catch (e) {
      throw PasskeyException('unknown', 'Malformed platform response: ${e.message}');
    }
  }
}

final passkeyServiceProvider = Provider<PasskeyService>((ref) => const PlatformPasskeyService());
