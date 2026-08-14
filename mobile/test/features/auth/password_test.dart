import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:kurx_mobile/common/widgets/kurx_button.dart';
import 'package:kurx_mobile/core/network/api_error.dart';
import 'package:kurx_mobile/features/auth/data/datasources/auth_remote_data_source.dart';
import 'package:kurx_mobile/features/auth/data/models/current_user_dto.dart';
import 'package:kurx_mobile/features/auth/data/models/token_response_dto.dart';
import 'package:kurx_mobile/features/auth/presentation/pages/password_page.dart';
import 'package:kurx_mobile/features/auth/presentation/pages/password_reset_page.dart';
import 'package:kurx_mobile/features/auth/presentation/password_policy.dart';
import 'package:kurx_mobile/features/auth/presentation/providers/auth_providers.dart';

/// Scripts the password endpoints so the create/change/reset screens can be driven without a network.
class _FakeAuthRemote extends AuthRemoteDataSource {
  _FakeAuthRemote({this.hasPassword = false}) : super(Dio());

  bool hasPassword;
  ApiError? setError;
  ApiError? changeError;
  ApiError? completeResetError;

  String? setValue;
  (String, String)? changed;
  final List<String> resetStarts = [];
  ({String id, String otp, String pw, String recovery})? resetCompleted;

  @override
  Future<({bool hasPassword, int minLength, int maxLength})> passwordStatus() async =>
      (hasPassword: hasPassword, minLength: 12, maxLength: 128);

  @override
  Future<void> setPassword(String password) async {
    if (setError != null) throw setError!;
    setValue = password;
  }

  @override
  Future<void> changePassword(String currentPassword, String newPassword) async {
    if (changeError != null) throw changeError!;
    changed = (currentPassword, newPassword);
  }

  @override
  Future<void> startPasswordReset(String identifier) async => resetStarts.add(identifier);

  @override
  Future<TokenResponseDto> completePasswordReset({
    required String identifier,
    required String otpCode,
    required String newPassword,
    required String recoveryCode,
  }) async {
    if (completeResetError != null) throw completeResetError!;
    resetCompleted = (id: identifier, otp: otpCode, pw: newPassword, recovery: recoveryCode);
    return const TokenResponseDto(accessToken: 'access', refreshToken: 'refresh');
  }

  @override
  Future<CurrentUserDto> me() async =>
      const CurrentUserDto(id: 'u1', phone: '+919000000001', name: 'Asha', username: 'asha');
}

const _valid = 'a-long-enough-passphrase';
const _other = 'another-long-passphrase';

void main() {
  setUp(() {
    TestWidgetsFlutterBinding.ensureInitialized();
    TestDefaultBinaryMessengerBinding.instance.defaultBinaryMessenger.setMockMethodCallHandler(
      const MethodChannel('plugins.it_nomads.com/flutter_secure_storage'),
      (call) async => null,
    );
  });

  Widget host(_FakeAuthRemote remote, Widget child) => ProviderScope(
        overrides: [authRemoteDataSourceProvider.overrideWithValue(remote)],
        child: MaterialApp(home: child),
      );

  // ══ Policy (mirrors the backend, D-129) ══════════════════════════════════════

  group('password policy', () {
    test('length is the only hard gate — 12 chars minimum, no composition rules', () {
      expect(passwordIssue('short'), isNotNull);
      expect(passwordIssue('elevenchars'), isNotNull); // 11
      expect(passwordIssue('twelvechars1'), isNull); // 12
      expect(passwordIssue('a' * 129), isNotNull);
    });

    test('strength is a soft hint that rewards length', () {
      expect(passwordStrength('').score, 0);
      expect(passwordStrength('short').label, 'Too short');
      expect(passwordStrength('a-long-enough-passphrase-1234').score, 4);
    });

    test('backend error codes map to actionable copy', () {
      expect(passwordErrorCopy(const ApiError(status: 400, code: 'password_breached')),
          contains('too common'));
      expect(passwordErrorCopy(const ApiError(status: 401, code: 'invalid_credentials')),
          contains('current password'));
      expect(passwordErrorCopy(const ApiError(status: 403, code: 'second_factor_required')),
          contains('recovery code'));
    });
  });

  // ══ Create / change ══════════════════════════════════════════════════════════

  group('PasswordPage', () {
    testWidgets('with no password, creating one calls set and confirms', (tester) async {
      final remote = _FakeAuthRemote(hasPassword: false);
      await tester.pumpWidget(host(remote, const PasswordPage()));
      await tester.pumpAndSettle();

      expect(find.text('Create a password'), findsWidgets);
      await tester.enterText(find.byType(TextField).at(0), _valid); // password
      await tester.enterText(find.byType(TextField).at(1), _valid); // confirm
      await tester.pump();
      await tester.tap(find.text('Create password'));
      await tester.pumpAndSettle();

      expect(remote.setValue, _valid);
      expect(find.text('Password updated'), findsOneWidget);
    });

    testWidgets('with a password, changing requires the current one', (tester) async {
      final remote = _FakeAuthRemote(hasPassword: true);
      await tester.pumpWidget(host(remote, const PasswordPage()));
      await tester.pumpAndSettle();

      // Three fields: current, new, confirm.
      await tester.enterText(find.byType(TextField).at(0), 'old-passphrase-here');
      await tester.enterText(find.byType(TextField).at(1), _valid);
      await tester.enterText(find.byType(TextField).at(2), _valid);
      await tester.pump();
      await tester.tap(find.widgetWithText(KurxButton, 'Change password'));
      await tester.pumpAndSettle();

      expect(remote.changed, ('old-passphrase-here', _valid));
    });

    testWidgets('a too-short password is blocked before any call', (tester) async {
      final remote = _FakeAuthRemote(hasPassword: false);
      await tester.pumpWidget(host(remote, const PasswordPage()));
      await tester.pumpAndSettle();

      await tester.enterText(find.byType(TextField).at(0), 'short');
      await tester.enterText(find.byType(TextField).at(1), 'short');
      await tester.pump();
      await tester.tap(find.text('Create password'));
      await tester.pumpAndSettle();

      expect(find.textContaining('at least 12'), findsOneWidget);
      expect(remote.setValue, isNull);
    });

    testWidgets('mismatched confirmation is caught client-side', (tester) async {
      final remote = _FakeAuthRemote(hasPassword: false);
      await tester.pumpWidget(host(remote, const PasswordPage()));
      await tester.pumpAndSettle();

      await tester.enterText(find.byType(TextField).at(0), _valid);
      await tester.enterText(find.byType(TextField).at(1), _other);
      await tester.pump();
      await tester.tap(find.text('Create password'));
      await tester.pumpAndSettle();

      expect(find.textContaining("don't match"), findsOneWidget);
      expect(remote.setValue, isNull);
    });

    testWidgets('a server rejection (reused) is shown as actionable copy', (tester) async {
      final remote = _FakeAuthRemote(hasPassword: true)
        ..changeError = const ApiError(status: 400, code: 'password_reused');
      await tester.pumpWidget(host(remote, const PasswordPage()));
      await tester.pumpAndSettle();

      await tester.enterText(find.byType(TextField).at(0), 'old-passphrase-here');
      await tester.enterText(find.byType(TextField).at(1), _valid);
      await tester.enterText(find.byType(TextField).at(2), _valid);
      await tester.pump();
      await tester.tap(find.widgetWithText(KurxButton, 'Change password'));
      await tester.pumpAndSettle();

      expect(find.textContaining('used that password recently'), findsOneWidget);
    });
  });

  // ══ Forgot / reset (E2E through the router) ══════════════════════════════════

  testWidgets('reset ceremony: OTP + recovery code + new password → signed in', (tester) async {
    final remote = _FakeAuthRemote();
    final router = GoRouter(
      initialLocation: '/reset',
      routes: [
        GoRoute(path: '/reset', builder: (_, _) => const PasswordResetPage()),
        GoRoute(path: '/events', builder: (_, _) => const Text('EVENTS HOME')),
        GoRoute(path: '/onboarding', builder: (_, _) => const Text('ONBOARDING')),
      ],
    );
    await tester.pumpWidget(ProviderScope(
      overrides: [authRemoteDataSourceProvider.overrideWithValue(remote)],
      child: MaterialApp.router(routerConfig: router),
    ));
    await tester.pumpAndSettle();

    await tester.enterText(find.byType(TextField).first, 'asha@example.com');
    await tester.pump();
    await tester.tap(find.text('Send code'));
    await tester.pumpAndSettle();
    expect(remote.resetStarts.single, 'asha@example.com');

    // reset step: otp, recovery, password, confirm.
    await tester.enterText(find.byType(TextField).at(0), '123456');
    await tester.enterText(find.byType(TextField).at(1), 'aaaa1111-bbbb2222');
    await tester.enterText(find.byType(TextField).at(2), _valid);
    await tester.enterText(find.byType(TextField).at(3), _valid);
    await tester.pump();
    await tester.tap(find.widgetWithText(KurxButton, 'Reset password'));
    await tester.pumpAndSettle();

    expect(remote.resetCompleted?.recovery, 'aaaa1111-bbbb2222');
    expect(remote.resetCompleted?.pw, _valid);
    expect(find.text('EVENTS HOME'), findsOneWidget); // the reset logged the user in
  });

  // The recovery code is factor 2 only when one is SUPPLIED. `PasswordResetService` falls back to
  // `stepUp.StatusAsync(...).Satisfied` when the field is blank, so the client must be able to submit
  // without it and let the server rule. This screen used to require it locally, which refused every
  // account that never minted codes — i.e. almost all of them, since codes come only from an explicit
  // step-up-gated call. Without this test that local rule can quietly come back.
  testWidgets('reset submits with no recovery code — the server decides factor 2', (tester) async {
    final remote = _FakeAuthRemote();
    final router = GoRouter(
      initialLocation: '/reset',
      routes: [
        GoRoute(path: '/reset', builder: (_, _) => const PasswordResetPage()),
        GoRoute(path: '/events', builder: (_, _) => const Text('EVENTS HOME')),
        GoRoute(path: '/onboarding', builder: (_, _) => const Text('ONBOARDING')),
      ],
    );
    await tester.pumpWidget(ProviderScope(
      overrides: [authRemoteDataSourceProvider.overrideWithValue(remote)],
      child: MaterialApp.router(routerConfig: router),
    ));
    await tester.pumpAndSettle();

    await tester.enterText(find.byType(TextField).first, 'asha@example.com');
    await tester.pump();
    await tester.tap(find.text('Send code'));
    await tester.pumpAndSettle();

    // otp, password, confirm — the recovery field at index 1 is deliberately left empty.
    await tester.enterText(find.byType(TextField).at(0), '123456');
    await tester.enterText(find.byType(TextField).at(2), _valid);
    await tester.enterText(find.byType(TextField).at(3), _valid);
    await tester.pump();
    await tester.tap(find.widgetWithText(KurxButton, 'Reset password'));
    await tester.pumpAndSettle();

    // It reached the server, carrying an empty code rather than being blocked on the device.
    expect(remote.resetCompleted?.recovery, '');
    expect(remote.resetCompleted?.pw, _valid);
    expect(find.text('EVENTS HOME'), findsOneWidget);
  });
}
