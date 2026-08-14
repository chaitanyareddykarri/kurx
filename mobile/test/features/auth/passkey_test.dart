import 'dart:convert';

import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:kurx_mobile/core/security/passkey_service.dart';

/// Covers the Dart half of the passkey rail (D-097): that the service is a faithful JSON shuttle
/// and that native error codes survive intact.
///
/// The Credential Manager ceremonies themselves cannot run here — they need Google Play services, a
/// screen lock, and a Digital Asset Links association. Those are PENDING HARDWARE VALIDATION.
void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  const channel = MethodChannel('kurx/passkey');
  final messenger = TestDefaultBinaryMessengerBinding.instance.defaultBinaryMessenger;

  final calls = <MethodCall>[];

  void stub(Object? Function(MethodCall call) handler) {
    messenger.setMockMethodCallHandler(channel, (call) async {
      calls.add(call);
      return handler(call);
    });
  }

  setUp(calls.clear);
  tearDown(() => messenger.setMockMethodCallHandler(channel, null));

  group('JSON shuttling', () {
    test('server options are passed to the platform verbatim', () async {
      stub((_) => '{"id":"abc","response":{}}');

      const options = {
        'challenge': 'Y2hhbGxlbmdl',
        'rp': {'id': 'kurx.in', 'name': 'Kurx'},
        'user': {'id': 'dXNlcg', 'name': 'a', 'displayName': 'A'},
        'pubKeyCredParams': [
          {'type': 'public-key', 'alg': -7}
        ],
        // A field the Dart layer knows nothing about must still reach the platform untouched —
        // this is what stops the client becoming a second place the WebAuthn schema can drift.
        'someFutureExtension': {'nested': true},
      };

      await const PlatformPasskeyService().register(options);

      final sent = jsonDecode(calls.single.arguments['requestJson'] as String);
      expect(sent, equals(options), reason: 'options must not be reshaped in transit');
    });

    test('the platform response is returned as-is', () async {
      const response = {
        'id': 'cred-1',
        'rawId': 'cred-1',
        'type': 'public-key',
        'response': {'attestationObject': 'AAA', 'clientDataJSON': 'BBB'},
      };
      stub((_) => jsonEncode(response));

      final result = await const PlatformPasskeyService().register(const {'challenge': 'x'});
      expect(result, equals(response));
    });

    test('register and authenticate hit their own platform methods', () async {
      stub((_) => '{"ok":true}');
      const service = PlatformPasskeyService();

      await service.register(const {'challenge': 'x'});
      await service.authenticate(const {'challenge': 'y'});

      expect(calls.map((c) => c.method).toList(), ['register', 'authenticate']);
    });
  });

  group('error codes survive the boundary', () {
    Future<PasskeyException> capture(String code, {String? message}) async {
      stub((_) => throw PlatformException(code: code, message: message));
      try {
        await const PlatformPasskeyService().register(const {'challenge': 'x'});
        fail('expected a PasskeyException');
      } on PasskeyException catch (e) {
        return e;
      }
    }

    test('native codes are carried through unchanged', () async {
      // Callers branch on these; remapping them to a generic failure would lose the distinction
      // between "user said no" and "this device cannot do passkeys".
      for (final code in ['user_canceled', 'already_registered', 'no_provider', 'dom_error']) {
        expect((await capture(code)).code, code);
      }
    });

    test('benign outcomes are distinguished from faults', () async {
      expect((await capture('user_canceled')).isBenign, isTrue);
      expect((await capture('already_registered')).isBenign, isTrue);
      expect((await capture('no_credential')).isBenign, isTrue);
      // A Digital Asset Links mismatch is a real misconfiguration, not a benign state.
      expect((await capture('dom_error')).isBenign, isFalse);
    });

    test('cases needing a fallback to the device-key rail are flagged', () async {
      expect((await capture('no_provider')).needsFallback, isTrue);
      expect((await capture('unavailable')).needsFallback, isTrue);
      expect((await capture('user_canceled')).needsFallback, isFalse);
    });

    test('an unimplemented platform reports unavailable rather than crashing', () async {
      // iOS has no plugin yet: the app must fall back, not throw an unhandled MissingPluginException.
      messenger.setMockMethodCallHandler(channel, null);

      try {
        await const PlatformPasskeyService().register(const {'challenge': 'x'});
        fail('expected a PasskeyException');
      } on PasskeyException catch (e) {
        expect(e.code, 'unavailable');
        expect(e.needsFallback, isTrue);
      }
    });

    test('isAvailable returns false instead of throwing when unsupported', () async {
      messenger.setMockMethodCallHandler(channel, null);
      expect(await const PlatformPasskeyService().isAvailable(), isFalse);
    });

    test('a malformed platform response is reported, not silently accepted', () async {
      stub((_) => 'not json at all');
      try {
        await const PlatformPasskeyService().register(const {'challenge': 'x'});
        fail('expected a PasskeyException');
      } on PasskeyException catch (e) {
        expect(e.code, 'unknown');
      }
    });

    test('a non-object response is rejected', () async {
      stub((_) => '"a bare string"');
      try {
        await const PlatformPasskeyService().authenticate(const {'challenge': 'x'});
        fail('expected a PasskeyException');
      } on PasskeyException catch (e) {
        expect(e.code, 'unknown');
      }
    });
  });
}
