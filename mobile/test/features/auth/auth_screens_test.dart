import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:kurx_mobile/core/network/api_error.dart';
import 'package:kurx_mobile/core/security/device_key_service.dart';
import 'package:kurx_mobile/core/security/passkey_service.dart';
import 'package:kurx_mobile/core/storage/token_store.dart';
import 'package:kurx_mobile/features/auth/data/datasources/trusted_device_remote_data_source.dart';
import 'package:kurx_mobile/features/auth/data/models/trusted_device_dtos.dart';
import 'package:kurx_mobile/features/auth/data/repositories/trusted_device_repository.dart';
import 'package:kurx_mobile/features/auth/presentation/pages/recovery_page.dart';
import 'package:kurx_mobile/features/auth/presentation/pages/security_page.dart';
import 'package:kurx_mobile/features/auth/presentation/pages/step_up_page.dart';

/// A repository whose auth operations are scripted, so each screen can be driven through success,
/// refusal, offline and cancelled-biometrics without a network or a device.
class _FakeRepo extends TrustedDeviceRepository {
  _FakeRepo()
      : super(
          TrustedDeviceRemoteDataSource(Dio()),
          _UnusedKeys(),
          TokenStore(const FlutterSecureStorage()),
          _NoPasskeys(),
        );

  Object? startRecoveryError;
  Object? redeemError;
  Object? stepUpError;
  Object? generateCodesError;

  final List<String> startedRecoveries = [];
  final List<String> stepUps = [];
  final List<String> revokedDevices = [];
  final List<String> revokedSessions = [];
  bool lastRevokeWasThisDevice = false;

  /// Lets a test observe the in-flight state; a fake that resolves instantly never shows one.
  Duration listDelay = Duration.zero;

  List<TrustedDeviceDto> devices = const [];
  List<AuthSessionDto> sessions = const [];
  List<String> codes = const ['aaaa1111-bbbb2222', 'cccc3333-dddd4444'];

  @override
  Future<void> startRecovery(String identifier) async {
    if (startRecoveryError != null) throw startRecoveryError!;
    startedRecoveries.add(identifier);
  }

  @override
  Future<void> redeemRecovery({
    required String identifier,
    required String otpCode,
    required String recoveryCode,
  }) async {
    if (redeemError != null) throw redeemError!;
  }

  @override
  Future<void> stepUp({required String action, required String deviceId}) async {
    if (stepUpError != null) throw stepUpError!;
    stepUps.add(action);
  }

  @override
  Future<List<TrustedDeviceDto>> listDevices() async {
    if (listDelay > Duration.zero) await Future<void>.delayed(listDelay);
    return devices;
  }

  @override
  Future<List<AuthSessionDto>> listSessions() async {
    if (listDelay > Duration.zero) await Future<void>.delayed(listDelay);
    return sessions;
  }

  @override
  Future<void> revokeDevice(String deviceId, {required bool isThisDevice}) async {
    revokedDevices.add(deviceId);
    lastRevokeWasThisDevice = isThisDevice;
  }

  @override
  Future<void> revokeSession(String sessionId) async => revokedSessions.add(sessionId);

  @override
  Future<List<String>> generateRecoveryCodes() async {
    if (generateCodesError != null) throw generateCodesError!;
    return codes;
  }

  // Security Center (Phase 2E) — kept deterministic so the screen never hits a real network.
  List<TrustedBrowserDto> browsers = const [];
  List<SecurityActivityDto> activity = const [];
  final List<String> revokedBrowsers = [];
  bool signedOutEverywhere = false;

  @override
  Future<SecurityOverviewDto> securityOverview() async => const SecurityOverviewDto(
        hasPassword: true,
        emailVerified: false,
        phoneVerified: true,
        trustedBrowsers: 0,
        trustedDevices: 0,
        passkeys: 0,
        activeSessions: 0,
        recoveryCodesRemaining: 0,
        stepUpSatisfied: false,
        canStepUp: false,
      );

  @override
  Future<List<TrustedBrowserDto>> listTrustedBrowsers() async => browsers;

  @override
  Future<List<SecurityActivityDto>> securityActivity() async => activity;

  @override
  Future<void> revokeTrustedBrowser(String id) async => revokedBrowsers.add(id);

  @override
  Future<int> signOutEverywhere() async {
    signedOutEverywhere = true;
    return 3;
  }
}


/// Passkeys are not exercised by these tests; the repository just needs a collaborator.
class _NoPasskeys implements PasskeyService {
  @override
  Future<bool> isAvailable() async => false;
  @override
  Future<Map<String, dynamic>> register(Map<String, dynamic> o) => throw UnimplementedError();
  @override
  Future<Map<String, dynamic>> authenticate(Map<String, dynamic> o) => throw UnimplementedError();
}

/// The screens never sign anything directly — the repository does — so this must never be called.
class _UnusedKeys implements DeviceKeyService {
  @override
  Future<String> createKey() => throw UnimplementedError();
  @override
  Future<void> deleteKey() => throw UnimplementedError();
  @override
  Future<bool> hasKey() async => true;
  @override
  Future<String?> publicKeySpki() async => 'SPKI';
  @override
  Future<String> sign(String message) => throw UnimplementedError();
}

TrustedDeviceDto _device(String id, {String name = 'Pixel 8', String state = 'Trusted'}) =>
    TrustedDeviceDto(
      id: id,
      name: name,
      platform: 'android',
      state: state,
      lastSeenAt: null,
      createdAt: DateTime.utc(2026, 7, 1),
    );

AuthSessionDto _session(String id, {String? deviceName = 'Pixel 8'}) => AuthSessionDto(
      id: id,
      deviceId: 'dev-1',
      deviceName: deviceName,
      platform: 'android',
      isCurrent: false,
      createdAt: DateTime.utc(2026, 7, 1),
      lastRotatedAt: null,
    );

void main() {
  late _FakeRepo repo;

  setUp(() {
    TestWidgetsFlutterBinding.ensureInitialized();
    TestDefaultBinaryMessengerBinding.instance.defaultBinaryMessenger.setMockMethodCallHandler(
      const MethodChannel('plugins.it_nomads.com/flutter_secure_storage'),
      (call) async => null,
    );
    repo = _FakeRepo();
  });

  /// The security page is now long enough that the recovery-codes button starts below the fold,
  /// and a ListView does not build off-screen children. Scroll to it exactly as a user would.
  Future<void> tapGenerateCodes(WidgetTester tester) async {
    await tester.scrollUntilVisible(find.text('Generate recovery codes'), 300);
    await tester.pumpAndSettle();
    await tester.tap(find.text('Generate recovery codes'));
    await tester.pumpAndSettle();
  }

  Widget host(Widget child) => ProviderScope(
        overrides: [trustedDeviceRepositoryProvider.overrideWithValue(repo)],
        child: MaterialApp(home: child),
      );

  // ══ Recovery ═════════════════════════════════════════════════════════════

  group('RecoveryPage', () {
    testWidgets('the send button stays disabled until an identifier is entered', (tester) async {
      await tester.pumpWidget(host(const RecoveryPage()));

      final button = tester.widget<FilledButton>(find.widgetWithText(FilledButton, 'Send code'));
      expect(button.onPressed, isNull);

      await tester.enterText(find.byType(TextField), '+919000000001');
      await tester.pump();

      expect(
        tester.widget<FilledButton>(find.widgetWithText(FilledButton, 'Send code')).onPressed,
        isNotNull,
      );
    });

    testWidgets('sending a code advances to the two-factor step', (tester) async {
      await tester.pumpWidget(host(const RecoveryPage()));
      await tester.enterText(find.byType(TextField), '+919000000001');
      await tester.pump();
      await tester.tap(find.text('Send code'));
      await tester.pumpAndSettle();

      // Both factors are demanded — a recovery code alone is never a login (D-083).
      expect(find.text('Code from your phone'), findsOneWidget);
      expect(find.text('Recovery code'), findsOneWidget);
      expect(repo.startedRecoveries.single, '+919000000001');
    });

    testWidgets('the redeem step never says which half was wrong', (tester) async {
      repo.redeemError = const ApiError(status: 401, code: 'invalid_recovery');

      await tester.pumpWidget(host(const RecoveryPage()));
      await tester.enterText(find.byType(TextField), '+919000000001');
      await tester.pump();
      await tester.tap(find.text('Send code'));
      await tester.pumpAndSettle();

      await tester.enterText(find.widgetWithText(TextField, 'Code from your phone'), '123456');
      await tester.enterText(find.widgetWithText(TextField, 'Recovery code'), 'aaaa1111-bbbb2222');
      await tester.pump();
      await tester.tap(find.text('Recover account'));
      await tester.pumpAndSettle();

      // One opaque message, mirroring the server's single `invalid_recovery` — anything more
      // specific would leak account existence the backend is careful not to reveal.
      expect(find.textContaining("didn't work"), findsOneWidget);
      expect(find.textContaining('recovery code'), findsWidgets);
      expect(find.textContaining('no account'), findsNothing);
      expect(find.textContaining('expired'), findsNothing);
    });

    testWidgets('warns that recovery signs every other session out', (tester) async {
      await tester.pumpWidget(host(const RecoveryPage()));
      await tester.enterText(find.byType(TextField), '+919000000001');
      await tester.pump();
      await tester.tap(find.text('Send code'));
      await tester.pumpAndSettle();

      expect(find.textContaining('signs out every existing session'), findsOneWidget);
    });

    testWidgets('a failure to start is reported without advancing', (tester) async {
      repo.startRecoveryError = const ApiError(status: 0, code: 'network_error');

      await tester.pumpWidget(host(const RecoveryPage()));
      await tester.enterText(find.byType(TextField), '+919000000001');
      await tester.pump();
      await tester.tap(find.text('Send code'));
      await tester.pumpAndSettle();

      expect(find.textContaining("Couldn't start recovery"), findsOneWidget);
      expect(find.text('Recovery code'), findsNothing);
    });
  });

  // ══ Step-up ══════════════════════════════════════════════════════════════

  group('StepUpPage', () {
    Widget stepUpHost({String? reason}) => host(
          StepUpPage(action: 'recovery_codes', deviceId: 'dev-1', reason: reason),
        );

    testWidgets('shows the supplied reason', (tester) async {
      await tester.pumpWidget(stepUpHost(reason: 'Generating new recovery codes.'));
      expect(find.text('Generating new recovery codes.'), findsOneWidget);
    });

    testWidgets('a successful confirmation records the step-up', (tester) async {
      await tester.pumpWidget(stepUpHost());
      await tester.tap(find.text('Confirm with biometrics'));
      await tester.pumpAndSettle();

      expect(repo.stepUps.single, 'recovery_codes');
    });

    testWidgets('a cancelled biometric prompt shows no error', (tester) async {
      repo.stepUpError = const DeviceKeyException('user_canceled');

      await tester.pumpWidget(stepUpHost());
      await tester.tap(find.text('Confirm with biometrics'));
      await tester.pumpAndSettle();

      // Cancelling is a choice, not a failure.
      expect(find.textContaining("Couldn't"), findsNothing);
      expect(find.text('Confirm with biometrics'), findsOneWidget);
    });

    testWidgets('an invalidated key tells the user to re-enrol, not just "failed"', (tester) async {
      repo.stepUpError = const DeviceKeyException('key_invalidated');

      await tester.pumpWidget(stepUpHost());
      await tester.tap(find.text('Confirm with biometrics'));
      await tester.pumpAndSettle();

      // Biometric enrollment changed — recoverable, but only by setting the device up again.
      expect(find.textContaining('Set this device up again'), findsOneWidget);
    });

    testWidgets('offline is reported as offline, not as a refusal', (tester) async {
      repo.stepUpError = const ApiError(status: 0, code: 'network_error');

      await tester.pumpWidget(stepUpHost());
      await tester.tap(find.text('Confirm with biometrics'));
      await tester.pumpAndSettle();

      expect(find.textContaining("You're offline"), findsOneWidget);
    });

    testWidgets('cancelling is a definite refusal, never an ambiguous null', (tester) async {
      // A caller must not be able to mistake "cancelled" for "authorised".
      Object? popped = 'unset';
      await tester.pumpWidget(host(
        Builder(
          builder: (context) => ElevatedButton(
            onPressed: () async {
              popped = await Navigator.of(context).push<bool>(
                MaterialPageRoute(
                  builder: (_) => const StepUpPage(action: 'x', deviceId: 'dev-1'),
                ),
              );
            },
            child: const Text('go'),
          ),
        ),
      ));

      await tester.tap(find.text('go'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Cancel'));
      await tester.pumpAndSettle();

      expect(popped, isFalse);
    });
  });

  // ══ Security ═════════════════════════════════════════════════════════════

  group('SecurityPage', () {
    testWidgets('shows a loading indicator before data arrives', (tester) async {
      repo.listDelay = const Duration(milliseconds: 200);

      await tester.pumpWidget(host(const SecurityPage()));
      await tester.pump();   // no settle: catch the in-flight state

      expect(find.byType(CircularProgressIndicator), findsWidgets);

      await tester.pump(const Duration(milliseconds: 250));
      await tester.pumpAndSettle();
      expect(find.byType(CircularProgressIndicator), findsNothing);
    });

    testWidgets('renders empty states rather than a blank screen', (tester) async {
      await tester.pumpWidget(host(const SecurityPage()));
      await tester.pumpAndSettle();

      // The Security Center is now several sections tall; a ListView doesn't build off-screen
      // children, so scroll to each empty state exactly as a user would.
      await tester.scrollUntilVisible(find.text('No trusted devices yet.'), 300);
      expect(find.text('No trusted devices yet.'), findsOneWidget);
      await tester.scrollUntilVisible(find.text('No active sessions.'), 300);
      expect(find.text('No active sessions.'), findsOneWidget);
    });

    testWidgets('lists devices and sessions, labelling this device', (tester) async {
      repo.devices = [_device('dev-1'), _device('dev-2', name: 'iPad')];
      repo.sessions = [_session('sess-1')];

      await tester.pumpWidget(host(const SecurityPage(thisDeviceId: 'dev-1')));
      await tester.pumpAndSettle();

      await tester.scrollUntilVisible(find.text('iPad'), 300);
      expect(find.text('Pixel 8'), findsWidgets);
      expect(find.text('iPad'), findsOneWidget);
      // The user must be able to tell which entry is the phone in their hand.
      expect(find.textContaining('this device'), findsOneWidget);
    });

    testWidgets('removing a device asks for confirmation first', (tester) async {
      repo.devices = [_device('dev-2', name: 'iPad')];

      await tester.pumpWidget(host(const SecurityPage(thisDeviceId: 'dev-1')));
      await tester.pumpAndSettle();
      await tester.scrollUntilVisible(find.text('Remove'), 300);
      await tester.tap(find.text('Remove'));
      await tester.pumpAndSettle();

      // Destructive and immediate — it must not happen on a single stray tap.
      expect(find.textContaining('signed out immediately'), findsOneWidget);
      expect(repo.revokedDevices, isEmpty);

      await tester.tap(find.widgetWithText(TextButton, 'Cancel'));
      await tester.pumpAndSettle();
      expect(repo.revokedDevices, isEmpty, reason: 'cancelling must not revoke');

      await tester.tap(find.text('Remove'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(FilledButton, 'Remove'));
      await tester.pumpAndSettle();

      expect(repo.revokedDevices.single, 'dev-2');
      expect(repo.lastRevokeWasThisDevice, isFalse);
    });

    testWidgets('removing THIS device is flagged so the local key is destroyed', (tester) async {
      repo.devices = [_device('dev-1')];

      await tester.pumpWidget(host(const SecurityPage(thisDeviceId: 'dev-1')));
      await tester.pumpAndSettle();
      await tester.scrollUntilVisible(find.text('Remove'), 300);
      await tester.tap(find.text('Remove'));
      await tester.pumpAndSettle();

      expect(find.text('Sign this device out?'), findsOneWidget);
      await tester.tap(find.widgetWithText(FilledButton, 'Remove'));
      await tester.pumpAndSettle();

      expect(repo.lastRevokeWasThisDevice, isTrue);
    });

    testWidgets('signing a session out calls through without a confirmation', (tester) async {
      repo.sessions = [_session('sess-1')];

      await tester.pumpWidget(host(const SecurityPage()));
      await tester.pumpAndSettle();
      await tester.scrollUntilVisible(find.text('Sign out'), 300);
      await tester.pumpAndSettle();
      await tester.tap(find.text('Sign out'));
      await tester.pumpAndSettle();

      expect(repo.revokedSessions.single, 'sess-1');
    });

    testWidgets('generated recovery codes are shown once, with a keep-them warning',
        (tester) async {
      await tester.pumpWidget(host(const SecurityPage()));
      await tester.pumpAndSettle();
      await tapGenerateCodes(tester);

      expect(find.text('aaaa1111-bbbb2222'), findsOneWidget);
      expect(find.textContaining("won't be shown again"), findsOneWidget);
    });

    testWidgets('a 403 is explained as step-up, not as a permissions error', (tester) async {
      repo.generateCodesError = const ApiError(status: 403, code: 'step_up_required');

      await tester.pumpWidget(host(const SecurityPage()));
      await tester.pumpAndSettle();
      await tapGenerateCodes(tester);

      expect(find.textContaining("Confirm it's you"), findsOneWidget);
    });

    testWidgets('being offline is NOT reported as needing a step-up', (tester) async {
      // Regression: `catch (_)` previously told an offline user to "confirm it's you", which is
      // unactionable — they cannot step up without a network either.
      repo.generateCodesError = const ApiError(status: 0, code: 'network_error');

      await tester.pumpWidget(host(const SecurityPage()));
      await tester.pumpAndSettle();
      await tapGenerateCodes(tester);

      expect(find.textContaining("Confirm it's you"), findsNothing);
      expect(find.textContaining("Can't reach Kurx"), findsOneWidget);
    });

    // ── Security Center (Phase 2E) ─────────────────────────────────────────

    testWidgets('shows the security overview and section headings', (tester) async {
      await tester.pumpWidget(host(const SecurityPage()));
      await tester.pumpAndSettle();

      expect(find.text('Overview'), findsOneWidget);
      await tester.scrollUntilVisible(find.text('Trusted browsers'), 300);
      expect(find.text('Trusted browsers'), findsOneWidget);
      await tester.scrollUntilVisible(find.text('Recent security activity'), 300);
      expect(find.text('Recent security activity'), findsOneWidget);
    });

    testWidgets('a trusted browser can be forgotten after confirming', (tester) async {
      repo.browsers = [
        TrustedBrowserDto(
          id: 'b1',
          label: null,
          browser: 'Chrome',
          operatingSystem: 'Windows',
          ip: null,
          approxLocation: null,
          isCurrent: false,
          createdAt: DateTime.utc(2026, 7, 1),
          lastUsedAt: null,
          expiresAt: DateTime.utc(2026, 8, 1),
        ),
      ];

      await tester.pumpWidget(host(const SecurityPage()));
      await tester.pumpAndSettle();
      await tester.scrollUntilVisible(find.text('Chrome · Windows'), 300);
      await tester.tap(find.widgetWithText(TextButton, 'Remove'));
      await tester.pumpAndSettle();

      // Destructive — confirmed first, and the confirm button reads "Forget", not "Remove".
      expect(find.text('Forget this browser?'), findsOneWidget);
      expect(repo.revokedBrowsers, isEmpty);
      await tester.tap(find.widgetWithText(FilledButton, 'Forget'));
      await tester.pumpAndSettle();

      expect(repo.revokedBrowsers.single, 'b1');
    });

    testWidgets('recent security activity is shown in plain language', (tester) async {
      repo.activity = [
        SecurityActivityDto(type: 'password.changed', severity: 'warning', context: null, createdAt: DateTime.now()),
        SecurityActivityDto(type: 'login.succeeded', severity: 'info', context: null, createdAt: DateTime.now()),
      ];

      await tester.pumpWidget(host(const SecurityPage()));
      await tester.pumpAndSettle();
      await tester.scrollUntilVisible(find.text('Password changed'), 300);
      expect(find.text('Password changed'), findsOneWidget);
      expect(find.text('Signed in'), findsOneWidget);
    });

    testWidgets('sign out everywhere confirms, then revokes every session', (tester) async {
      repo.sessions = [_session('sess-1')];

      await tester.pumpWidget(host(const SecurityPage()));
      await tester.pumpAndSettle();
      await tester.scrollUntilVisible(find.text('Sign out everywhere'), 300);
      await tester.tap(find.text('Sign out everywhere'));
      await tester.pumpAndSettle();

      expect(find.text('Sign out everywhere?'), findsOneWidget);
      await tester.tap(find.widgetWithText(FilledButton, 'Sign out'));
      await tester.pumpAndSettle();

      expect(repo.signedOutEverywhere, isTrue);
    });

    // ── Step-up (Phase 2F, AM6) ────────────────────────────────────────────

    testWidgets("the standalone Confirm it's you runs a D-181 step-up", (tester) async {
      // Only offered when this handset is enrolled (it is the device that signs).
      await tester.pumpWidget(host(const SecurityPage(thisDeviceId: 'dev-1')));
      await tester.pumpAndSettle();

      await tester.scrollUntilVisible(find.widgetWithText(OutlinedButton, "Confirm it's you"), 300);
      await tester.tap(find.widgetWithText(OutlinedButton, "Confirm it's you"));
      await tester.pumpAndSettle();

      // Reuses the same step-up screen as the recovery-codes gate.
      expect(find.text('Confirm with biometrics'), findsOneWidget);
      await tester.tap(find.text('Confirm with biometrics'));
      await tester.pumpAndSettle();

      expect(repo.stepUps, contains('security'));
    });
  });
}
