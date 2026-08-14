import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'package:kurx_mobile/common/widgets/kurx_button.dart';
import 'package:kurx_mobile/core/network/api_error.dart';
import 'package:kurx_mobile/features/auth/data/datasources/auth_remote_data_source.dart';
import 'package:kurx_mobile/features/auth/data/models/current_user_dto.dart';
import 'package:kurx_mobile/features/auth/data/models/registration_status_dto.dart';
import 'package:kurx_mobile/features/auth/presentation/pages/registration_flow_page.dart';
import 'package:kurx_mobile/features/auth/presentation/providers/auth_providers.dart';

/// A data source whose registration calls are scripted, so the wizard can be driven through the
/// profile → password → email ceremony without a network. `registrationStatus` returns [statusValue],
/// which the profile/password/email calls mutate exactly as the backend would, so the wizard advances
/// the same way it does in production.
class _FakeAuthRemote extends AuthRemoteDataSource {
  _FakeAuthRemote(this.statusValue) : super(Dio());

  RegistrationStatusDto statusValue;
  String? savedPassword;

  /// When set, `setPassword` throws it — how the server refuses a password that fails policy.
  ApiError? setPasswordError;

  @override
  Future<void> setPassword(String password) async {
    if (setPasswordError != null) throw setPasswordError!;
    savedPassword = password;
    statusValue = _status(
      email: statusValue.email,
      emailVerified: statusValue.emailVerified,
      needsOnboarding: false,
      hasPassword: true,
    );
  }
  String availability = 'available';
  ApiError? startEmailError;
  ApiError? completeEmailError;

  final List<String> sentEmails = [];
  final List<String> verifiedEmails = [];
  (String?, String?)? savedProfile;
  DateTime? savedDateOfBirth;
  String? savedBio;

  @override
  Future<RegistrationStatusDto> registrationStatus() async => statusValue;

  @override
  Future<void> startEmailVerification(String email) async {
    if (startEmailError != null) throw startEmailError!;
    sentEmails.add(email);
  }

  @override
  Future<void> completeEmailVerification(String email, String code) async {
    if (completeEmailError != null) throw completeEmailError!;
    verifiedEmails.add(email);
    // hasPassword carried through: dropping it made the fake claim the password step was outstanding
    // again, which is not something verifying an email can undo.
    statusValue = _status(
      email: email,
      emailVerified: true,
      needsOnboarding: statusValue.needsOnboarding,
      hasPassword: statusValue.hasPassword,
    );
  }

  @override
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
  }) async {
    // Registration sends name + username + date of birth, and bio when written. The remaining
    // display fields (D-219) are edited later from the profile editor.
    savedProfile = (name, username);
    savedDateOfBirth = dateOfBirth;
    savedBio = bio;
    statusValue = _status(
      email: statusValue.email,
      emailVerified: statusValue.emailVerified,
      needsOnboarding: false,
      hasPassword: statusValue.hasPassword,
    );
  }

  /// The server's password policy. Overridden so the widget under test reads a deterministic minimum
  /// instead of falling through to a real Dio call.
  @override
  Future<({bool hasPassword, int minLength, int maxLength})> passwordStatus() async =>
      (hasPassword: statusValue.hasPassword, minLength: 12, maxLength: 128);

  @override
  Future<String> usernameAvailability(String username) async => availability;

  @override
  Future<CurrentUserDto> me() async =>
      const CurrentUserDto(id: 'u1', phone: '+919000000001', name: 'Asha', username: 'asha');
}

RegistrationStatusDto _status({
  String? email,
  bool emailVerified = false,
  bool needsOnboarding = true,
  bool hasPassword = false,
  bool hasTrustedDevice = false,
}) {
  // Same order the server emits (Kurx.Domain.Onboarding.Remaining): the two blocking steps, then
  // the skippable one, then the device.
  final remaining = <String>[
    if (needsOnboarding) 'complete_profile',
    if (!hasPassword) 'create_password',
    if (email == null || !emailVerified) 'verify_email',
    if (!hasTrustedDevice) 'enroll_device',
  ];
  return RegistrationStatusDto(
    hasPassword: hasPassword,
    email: email,
    emailVerified: emailVerified,
    phone: '+919000000001',
    phoneVerified: true,
    hasTrustedDevice: hasTrustedDevice,
    needsOnboarding: needsOnboarding,
    remaining: remaining,
  );
}

void main() {
  // The profile step renders the chosen date with DateFormat(..., 'en_IN'), which throws unless the
  // locale data is loaded — `main()` does this at startup, so a widget test has to as well.
  setUpAll(() async => initializeDateFormatting('en_IN'));

  setUp(() {
    TestWidgetsFlutterBinding.ensureInitialized();
    TestDefaultBinaryMessengerBinding.instance.defaultBinaryMessenger.setMockMethodCallHandler(
      const MethodChannel('plugins.it_nomads.com/flutter_secure_storage'),
      (call) async => null,
    );
  });

  Widget host(_FakeAuthRemote remote) => ProviderScope(
        overrides: [authRemoteDataSourceProvider.overrideWithValue(remote)],
        child: const MaterialApp(home: RegistrationFlowPage()),
      );

  /// A status where both blocking steps are behind the user, so the flow opens on the email step.
  RegistrationStatusDto atEmailStep() => _status(needsOnboarding: false, hasPassword: true);

  testWidgets('opens on the profile step — the first thing the account actually needs',
      (tester) async {
    await tester.pumpWidget(host(_FakeAuthRemote(_status())));
    await tester.pumpAndSettle();

    // Email used to lead this flow, which put a skippable step ahead of the two the account cannot
    // be finished without.
    expect(find.text('Complete your profile'), findsOneWidget);
    expect(find.text('Verify your email'), findsNothing);
    // Phone is verified implicitly by the OTP signup — shown as a completed step.
    expect(find.text('Phone verified'), findsOneWidget);
    expect(find.text('Email verified'), findsOneWidget);
    expect(find.text('Profile complete'), findsOneWidget);
  });

  testWidgets('verifying email finishes the flow', (tester) async {
    final remote = _FakeAuthRemote(atEmailStep());
    await tester.pumpWidget(host(remote));
    await tester.pumpAndSettle();

    await tester.enterText(find.widgetWithText(TextField, 'Email'), 'asha@example.com');
    await tester.pump();
    await tester.tap(find.text('Send code'));
    await tester.pumpAndSettle();

    await tester.enterText(find.widgetWithText(TextField, '6-digit code'), '123456');
    await tester.pump();
    await tester.tap(find.text('Verify email'));
    await tester.pumpAndSettle();

    expect(remote.verifiedEmails.single, 'asha@example.com');
    expect(find.text("You're all set!"), findsOneWidget);
  });

  testWidgets('an email already owned by another account is reported, staying on the email step',
      (tester) async {
    final remote = _FakeAuthRemote(atEmailStep())
      ..startEmailError = const ApiError(status: 409, code: 'email_taken');
    await tester.pumpWidget(host(remote));
    await tester.pumpAndSettle();

    await tester.enterText(find.widgetWithText(TextField, 'Email'), 'taken@example.com');
    await tester.pump();
    await tester.tap(find.text('Send code'));
    await tester.pumpAndSettle();

    expect(find.textContaining('already linked to another account'), findsOneWidget);
    expect(find.widgetWithText(TextField, '6-digit code'), findsNothing);
  });

  testWidgets('email is the only skippable step, and skipping it finishes', (tester) async {
    await tester.pumpWidget(host(_FakeAuthRemote(atEmailStep())));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Skip for now'));
    await tester.pumpAndSettle();

    expect(find.text("You're all set!"), findsOneWidget);
  });

  /// The date the picker below lands on. The dialog opens on `today - 20 years`; the helper selects
  /// that same year (the only one guaranteed on screen without scrolling the year grid) and day 15,
  /// and selecting a year keeps the month.
  /// One year off the dialog's initial year, not the initial year itself: tapping the year that is
  /// already selected fires no change, so the picker never leaves year mode and no day cell appears.
  /// Adjacent, so it is on screen without scrolling the year grid.
  final pickedYear = DateTime.now().year - 21;
  final expectedDateOfBirth = DateTime(DateTime.now().year - 21, DateTime.now().month, 15);

  /// Drives the real date picker, because the date of birth is required and there is no other way to
  /// supply it. Opens in year mode, so: pick a year, then a day, then confirm.
  ///
  /// `ensureVisible` first — the profile step is taller than the test viewport, so the field is built
  /// but scrolled off, and tapping it without scrolling finds nothing to hit.
  Future<void> pickDateOfBirth(WidgetTester tester) async {
    final field = find.text('Select your date of birth');
    await tester.ensureVisible(field);
    await tester.pumpAndSettle();
    await tester.tap(field);
    await tester.pumpAndSettle();

    await tester.tap(find.text('$pickedYear'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('15').first);
    await tester.pumpAndSettle();
    await tester.tap(find.text('OK'));
    await tester.pumpAndSettle();
  }

  Future<void> tapContinue(WidgetTester tester) async {
    await tester.ensureVisible(find.text('Continue'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Continue'));
    await tester.pumpAndSettle();
  }

  /// Fills every required profile field and continues. Leaves the flow on whatever step comes next.
  Future<void> completeProfile(WidgetTester tester) async {
    // Profile step's text fields, in order: [Name, username, Bio]. The date of birth is a tappable
    // field rather than a text input.
    await tester.enterText(find.byType(TextField).at(0), 'Asha K');
    await tester.enterText(find.byType(TextField).at(1), 'ashak');
    await tester.pumpAndSettle(); // let the availability check settle to "available"
    await pickDateOfBirth(tester);
    await tapContinue(tester);
  }

  testWidgets('the profile cannot be submitted without a date of birth', (tester) async {
    await tester.pumpWidget(host(_FakeAuthRemote(_status())));
    await tester.pumpAndSettle();

    await tester.enterText(find.byType(TextField).at(0), 'Asha K');
    await tester.enterText(find.byType(TextField).at(1), 'ashak');
    await tester.pumpAndSettle();

    // Name and username are filled and the handle is available, but the server counts onboarding as
    // unfinished without a date of birth — so submitting here would bounce straight back.
    final button = tester.widget<KurxButton>(find.widgetWithText(KurxButton, 'Continue'));
    expect(button.onPressed, isNull);

    await pickDateOfBirth(tester);
    expect(
      tester.widget<KurxButton>(find.widgetWithText(KurxButton, 'Continue')).onPressed,
      isNotNull,
    );
  });

  testWidgets('the profile submits name, username and date of birth', (tester) async {
    final remote = _FakeAuthRemote(_status());
    await tester.pumpWidget(host(remote));
    await tester.pumpAndSettle();

    await tester.enterText(find.byType(TextField).at(2), 'Runner and organiser.');
    await completeProfile(tester);

    expect(remote.savedProfile, ('Asha K', 'ashak'));
    expect(remote.savedDateOfBirth, expectedDateOfBirth);
    expect(remote.savedBio, 'Runner and organiser.');
  });

  testWidgets('the password step is required — there is no way past it', (tester) async {
    final remote = _FakeAuthRemote(_status());
    await tester.pumpWidget(host(remote));
    await tester.pumpAndSettle();

    await completeProfile(tester);

    expect(find.text('Set a password'), findsOneWidget);
    // The skip that used to sit here let a user finish registration with no password at all, which
    // the server then reported as unfinished onboarding — so the next launch dropped them right back
    // into this flow.
    expect(find.text('Skip for now'), findsNothing);
    expect(find.text("You're all set!"), findsNothing);
  });

  testWidgets('the password minimum comes from the server, not a weaker client guess', (tester) async {
    final remote = _FakeAuthRemote(_status());
    await tester.pumpWidget(host(remote));
    await tester.pumpAndSettle();
    await completeProfile(tester);

    // Eight characters passed the old client check and were then rejected by the backend's 12.
    await tester.enterText(find.byType(TextField).at(0), 'eightch8');
    await tester.enterText(find.byType(TextField).at(1), 'eightch8');
    await tester.pumpAndSettle();

    expect(find.text('At least 12 characters'), findsOneWidget);
    expect(
      tester.widget<KurxButton>(find.widgetWithText(KurxButton, 'Set password')).onPressed,
      isNull,
    );
  });

  testWidgets('setting a password requires a match and then completes the flow', (tester) async {
    final remote = _FakeAuthRemote(_status(email: 'asha@example.com', emailVerified: true));
    await tester.pumpWidget(host(remote));
    await tester.pumpAndSettle();

    await completeProfile(tester);

    // Password step has exactly two fields: [password, confirm].
    await tester.enterText(find.byType(TextField).at(0), 'correct-horse-battery');
    await tester.enterText(find.byType(TextField).at(1), 'mismatch');
    await tester.pumpAndSettle();

    // A mismatch must not be submittable — otherwise the user sets a password they cannot repeat.
    final button = tester.widget<KurxButton>(find.widgetWithText(KurxButton, 'Set password'));
    expect(button.onPressed, isNull);
    expect(find.text('Passwords do not match'), findsOneWidget);

    await tester.enterText(find.byType(TextField).at(1), 'correct-horse-battery');
    await tester.pumpAndSettle();
    await tester.tap(find.text('Set password'));
    await tester.pumpAndSettle();

    expect(remote.savedPassword, 'correct-horse-battery');
    expect(find.text("You're all set!"), findsOneWidget);
  });

  testWidgets('the email step follows the password when the email is still unverified',
      (tester) async {
    // The regression: `_savePassword` set `_step = _Step.success` outright instead of re-reading the
    // checklist, so on mobile the email step was built, listed in the checklist, and unreachable in
    // one sitting. The test above passed the whole time because it starts already email-verified.
    final remote = _FakeAuthRemote(_status());
    await tester.pumpWidget(host(remote));
    await tester.pumpAndSettle();

    await completeProfile(tester);

    await tester.enterText(find.byType(TextField).at(0), 'correct-horse-battery');
    await tester.enterText(find.byType(TextField).at(1), 'correct-horse-battery');
    await tester.pumpAndSettle();
    await tester.tap(find.text('Set password'));
    await tester.pumpAndSettle();

    expect(remote.savedPassword, 'correct-horse-battery');
    expect(find.text('Verify your email'), findsOneWidget);
    expect(find.text("You're all set!"), findsNothing);
  });

  testWidgets('a refused password says why, instead of "could not set your password"',
      (tester) async {
    // `password_breached` is what a user hits after being told "12 characters" and picking one of
    // the 12-character strings the deny list exists to catch. The screen used to switch on
    // `weak_password` — a code the backend never emits — so every real refusal read as a shrug.
    final remote = _FakeAuthRemote(_status(email: 'asha@example.com', emailVerified: true))
      ..setPasswordError = const ApiError(status: 400, code: 'password_breached');
    await tester.pumpWidget(host(remote));
    await tester.pumpAndSettle();

    await completeProfile(tester);

    await tester.enterText(find.byType(TextField).at(0), 'password1234');
    await tester.enterText(find.byType(TextField).at(1), 'password1234');
    await tester.pumpAndSettle();
    await tester.tap(find.text('Set password'));
    await tester.pumpAndSettle();

    expect(find.text('That password is too common. Choose something less guessable.'),
        findsOneWidget);
    expect(find.text('Could not set your password. Try again.'), findsNothing);
    // Still on the password step — a refusal must not advance the ceremony.
    expect(find.text('Verify your email'), findsNothing);
  });
}
