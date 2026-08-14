import 'dart:convert';

import 'package:dio/dio.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:kurx_mobile/core/security/device_key_service.dart';
import 'package:kurx_mobile/core/security/passkey_service.dart';
import 'package:kurx_mobile/core/storage/token_store.dart';
import 'package:kurx_mobile/features/auth/data/datasources/trusted_device_remote_data_source.dart';
import 'package:kurx_mobile/features/auth/data/models/trusted_device_dtos.dart';
import 'package:kurx_mobile/features/auth/data/repositories/trusted_device_repository.dart';
import 'package:kurx_mobile/features/auth/presentation/pages/approve_login_page.dart';

/// Records what was signed and what was sent, so the ceremonies can be asserted end to end
/// without a device: the interesting behaviour is *which nonce gets signed and where the
/// signature is sent*, and that is all in Dart.
class _FakeKeys implements DeviceKeyService {
  _FakeKeys({this.signThrows});

  final DeviceKeyException? signThrows;
  final List<String> signed = [];
  bool created = false;
  bool deleted = false;
  String? spki = 'EXISTING_SPKI';

  @override
  Future<bool> hasKey() async => spki != null;

  @override
  Future<String> createKey() async {
    created = true;
    return spki = 'NEW_SPKI';
  }

  @override
  Future<String?> publicKeySpki() async => spki;

  @override
  Future<String> sign(String message) async {
    if (signThrows != null) throw signThrows!;
    signed.add(message);
    return 'SIG($message)';
  }

  @override
  Future<void> deleteKey() async {
    deleted = true;
    spki = null;
  }
}

/// A Dio whose adapter answers from a canned route table and records every request.
({Dio dio, List<RequestOptions> sent}) _fakeDio(Map<String, dynamic> routes) {
  final sent = <RequestOptions>[];
  final dio = Dio(BaseOptions(baseUrl: 'http://test'));
  dio.httpClientAdapter = _StubAdapter(routes, sent);
  return (dio: dio, sent: sent);
}

class _StubAdapter implements HttpClientAdapter {
  _StubAdapter(this.routes, this.sent);

  final Map<String, dynamic> routes;
  final List<RequestOptions> sent;

  @override
  Future<ResponseBody> fetch(RequestOptions options, Stream<Uint8List>? _, Future<void>? _) async {
    sent.add(options);
    final body = routes[options.path];
    if (body == null) return ResponseBody.fromString('{}', 404);
    return ResponseBody.fromString(
      body is String ? body : _encode(body),
      200,
      headers: {
        Headers.contentTypeHeader: [Headers.jsonContentType]
      },
    );
  }

  static String _encode(dynamic value) => jsonEncode(value);

  @override
  void close({bool force = false}) {}
}


/// Enrollment records the device id and revocation clears it (D-117), so the repository now makes
/// real secure-storage calls. The channel is mocked with an in-memory map rather than the store
/// being faked out, so what the tests exercise stays the production TokenStore.
final Map<String, String> _storage = {};

void _mockSecureStorage() {
  TestWidgetsFlutterBinding.ensureInitialized();
  TestDefaultBinaryMessengerBinding.instance.defaultBinaryMessenger.setMockMethodCallHandler(
    const MethodChannel('plugins.it_nomads.com/flutter_secure_storage'),
    (call) async {
      final key = call.arguments['key'] as String?;
      switch (call.method) {
        case 'write':
          _storage[key!] = call.arguments['value'] as String;
          return null;
        case 'read':
          return _storage[key];
        case 'delete':
          _storage.remove(key);
          return null;
        case 'readAll':
          return Map<String, String>.from(_storage);
        case 'deleteAll':
          _storage.clear();
          return null;
        default:
          return null;
      }
    },
  );
}

TrustedDeviceRepository _repo(Dio dio, DeviceKeyService keys) => TrustedDeviceRepository(
      TrustedDeviceRemoteDataSource(dio),
      keys,
      TokenStore(const FlutterSecureStorage()),
      _NoPasskeys(),
    );


/// Passkeys are not exercised by these tests; the repository just needs a collaborator.
class _NoPasskeys implements PasskeyService {
  @override
  Future<bool> isAvailable() async => false;
  @override
  Future<Map<String, dynamic>> register(Map<String, dynamic> o) => throw UnimplementedError();
  @override
  Future<Map<String, dynamic>> authenticate(Map<String, dynamic> o) => throw UnimplementedError();
}

void main() {
  setUp(() {
    _storage.clear();
    _mockSecureStorage();
  });

  group('DTO parsing', () {
    test('pending login parses the camelCase projection', () {
      final dto = PendingLoginDto.fromJson({
        'challengeId': 'c1',
        'nonce': 'NONCE',
        'matchNumber': 42,
        'contextJson': '{"ip":"1.2.3.4","ua":"Chrome"}',
        'expiresAt': '2026-07-18T12:00:00Z',
      });
      expect(dto.challengeId, 'c1');
      expect(dto.matchNumber, 42);
    });

    test('device state maps to the lifecycle FSM', () {
      TrustedDeviceDto make(String state) => TrustedDeviceDto.fromJson({
            'id': 'd1',
            'name': 'Pixel',
            'platform': 'android',
            'state': state,
            'createdAt': '2026-07-18T12:00:00Z',
          });
      expect(make('Trusted').isTrusted, isTrue);
      // A suspended device (e.g. after account recovery, D-083) must not read as trusted.
      expect(make('Suspended').isTrusted, isFalse);
      expect(make('Revoked').isTrusted, isFalse);
    });

    test('step-up status defaults to not-satisfied on a partial payload', () {
      final dto = StepUpStatusDto.fromJson({});
      expect(dto.satisfied, isFalse);
      expect(dto.canStepUp, isFalse);
    });
  });

  group('enrollment ceremony', () {
    test('signs the server nonce and posts the signature to verify', () async {
      final fake = _fakeDio({
        '/v1/auth/devices/enroll': {
          'device_id': 'dev-1',
          'credential_id': 'cred-1',
          'challenge_id': 'ch-1',
          'nonce': 'THE_NONCE',
          'match_number': 12,
          'expires_at': '2026-07-18T12:00:00Z',
        },
        '/v1/auth/devices/enroll/verify': {'ok': true, 'state': 'Trusted'},
      });
      final keys = _FakeKeys();
      final repo = _repo(fake.dio, keys);

      final deviceId = await repo.enrollThisDevice(name: 'Pixel', platform: 'android');

      expect(deviceId, 'dev-1');
      expect(keys.signed, ['THE_NONCE']); // exactly the challenge, nothing else
      final verify = fake.sent.firstWhere((r) => r.path.endsWith('/verify'));
      expect((verify.data as Map)['signature'], 'SIG(THE_NONCE)');
      expect((verify.data as Map)['deviceId'], 'dev-1');
    });

    test('reuses an existing key instead of minting a second one', () async {
      final fake = _fakeDio({
        '/v1/auth/devices/enroll': {
          'device_id': 'dev-1',
          'credential_id': 'c',
          'challenge_id': 'ch',
          'nonce': 'N',
          'match_number': 1,
          'expires_at': '2026-07-18T12:00:00Z',
        },
        '/v1/auth/devices/enroll/verify': {'ok': true},
      });
      final keys = _FakeKeys();
      await _repo(fake.dio, keys)
          .enrollThisDevice(name: 'Pixel', platform: 'android');

      expect(keys.created, isFalse);
      final begin = fake.sent.first;
      expect((begin.data as Map)['publicKeySpki'], 'EXISTING_SPKI');
    });
  });

  group('login approval', () {
    test('approve signs "{nonce}.{NN}" and sends the entered match number (D-181)', () async {
      final fake = _fakeDio({'/v1/auth/login/approve': {'ok': true}});
      final keys = _FakeKeys();
      final repo = _repo(fake.dio, keys);

      await repo.approveLogin(
        challenge: PendingLoginDto(
          challengeId: 'ch-9',
          nonce: 'LOGIN_NONCE',
          matchNumber: 77,
          contextJson: null,
          expiresAt: DateTime.utc(2026, 7, 18),
        ),
        deviceId: 'dev-9',
        matchNumber: 77,
      );

      // The signature is over the nonce joined with the zero-padded two-digit number the user entered.
      expect(keys.signed, ['LOGIN_NONCE.77']);
      final body = fake.sent.single.data as Map;
      expect(body['challengeId'], 'ch-9');
      expect(body['deviceId'], 'dev-9');
      expect(body['matchNumber'], 77);
      expect(body['signature'], 'SIG(LOGIN_NONCE.77)');
    });

    test('reject never touches the key — declining must not need biometrics', () async {
      final fake = _fakeDio({'/v1/auth/login/reject': {'ok': true}});
      final keys = _FakeKeys();

      await _repo(fake.dio, keys)
          .rejectLogin('ch-9');

      expect(keys.signed, isEmpty);
    });
  });

  group('device revocation', () {
    test('revoking this device also destroys its local key', () async {
      final fake = _fakeDio({'/v1/auth/devices/dev-1/revoke': {'ok': true}});
      final keys = _FakeKeys();

      await _repo(fake.dio, keys)
          .revokeDevice('dev-1', isThisDevice: true);

      expect(keys.deleted, isTrue);
    });

    test('revoking another device leaves this one usable', () async {
      final fake = _fakeDio({'/v1/auth/devices/dev-2/revoke': {'ok': true}});
      final keys = _FakeKeys();

      await _repo(fake.dio, keys)
          .revokeDevice('dev-2', isThisDevice: false);

      expect(keys.deleted, isFalse);
    });
  });

  group('approval screen', () {
    Widget harness(Dio dio, DeviceKeyService keys) => ProviderScope(
          overrides: [
            trustedDeviceRemoteDataSourceProvider
                .overrideWithValue(TrustedDeviceRemoteDataSource(dio)),
            deviceKeyServiceProvider.overrideWithValue(keys),
          ],
          child: const MaterialApp(home: ApproveLoginPage(deviceId: 'dev-1')),
        );

    testWidgets('asks the user to enter the number from the other screen (D-181), never showing it', (tester) async {
      final fake = _fakeDio({
        '/v1/auth/login/pending': [
          {
            'challengeId': 'ch-1',
            'nonce': 'N',
            'matchNumber': 73,
            'contextJson': '{"ip":"1.2.3.4","ua":"Chrome"}',
            'expiresAt': '2027-01-01T00:00:00Z',
          }
        ],
      });

      await tester.pumpWidget(harness(fake.dio, _FakeKeys()));
      await tester.pumpAndSettle();

      // The number is NOT shown on this device (the anti-relay defence): the user reads it from the
      // waiting browser and types it in. So there is an entry field, the request context, and reject.
      expect(find.text('73'), findsNothing);
      expect(find.byType(TextField), findsOneWidget);
      expect(find.textContaining('1.2.3.4'), findsOneWidget);
      expect(find.text("No, this wasn't me"), findsOneWidget);
    });

    testWidgets('empty state when nothing is waiting', (tester) async {
      final fake = _fakeDio({'/v1/auth/login/pending': []});

      await tester.pumpWidget(harness(fake.dio, _FakeKeys()));
      await tester.pumpAndSettle();

      expect(find.text('No sign-in requests waiting.'), findsOneWidget);
    });

    testWidgets('a cancelled biometric prompt is not surfaced as an error', (tester) async {
      final fake = _fakeDio({
        '/v1/auth/login/pending': [
          {
            'challengeId': 'ch-1',
            'nonce': 'N',
            'matchNumber': 21,
            'contextJson': null,
            'expiresAt': '2027-01-01T00:00:00Z',
          }
        ],
      });
      final keys = _FakeKeys(signThrows: const DeviceKeyException('user_canceled'));

      await tester.pumpWidget(harness(fake.dio, keys));
      await tester.pumpAndSettle();
      await tester.enterText(find.byType(TextField), '21');   // the number the user read from the browser
      await tester.pump();
      await tester.tap(find.text('Approve'));
      await tester.pumpAndSettle();

      // Cancelling the biometric prompt is a choice, not a failure — no scary red text.
      expect(find.textContaining('Could not'), findsNothing);
    });
  });
}
