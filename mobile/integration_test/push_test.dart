import 'package:firebase_core/firebase_core.dart';
import 'package:firebase_messaging/firebase_messaging.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:integration_test/integration_test.dart';
import 'package:kurx_mobile/core/push/push_service.dart';

/// On-device verification of Firebase/FCM wiring (D-096).
///
/// Host-VM tests can only cover payload parsing — they cannot prove the app is correctly registered
/// with a Firebase project. This runs against the **real** Firebase SDK on a real Android runtime.
///
/// Run: `flutter test integration_test/push_test.dart -d <device>`
///
/// The suite **adapts to the device**: an emulator image without Google Play Services cannot obtain
/// an FCM token, and that is a legitimate environment rather than a failure. In that case it asserts
/// the *graceful degradation* path instead — which is itself a requirement, since push must never be
/// load-bearing for login approval.
void main() {
  IntegrationTestWidgetsFlutterBinding.ensureInitialized();

  testWidgets('Firebase initialises with the bundled google-services.json', (tester) async {
    await Firebase.initializeApp();

    final app = Firebase.app();
    final options = app.options;

    // Proves the Gradle plugin's generated resources were actually read at runtime — not merely
    // that the file existed at build time.
    debugPrint('FIREBASE OPTIONS: appId=${options.appId} projectId=${options.projectId} '
        'messagingSenderId=${options.messagingSenderId}');

    expect(options.projectId, isNotEmpty);
    expect(options.appId, isNotEmpty);
    expect(options.messagingSenderId, isNotEmpty);
  });

  testWidgets('FCM token is obtained and uploaded, or degrades cleanly', (tester) async {
    await Firebase.initializeApp();

    final uploaded = <String>[];
    final service = PushService(
      FirebaseMessaging.instance,
      (token, platform) async => uploaded.add('$platform:$token'),
    );

    final available = await service.start(platform: 'android');

    if (available) {
      // Real registration happened: a token was issued by FCM and handed to the uploader.
      expect(uploaded, hasLength(1), reason: 'the token must be registered exactly once');
      expect(uploaded.single, startsWith('android:'));
      final token = uploaded.single.split(':').sublist(1).join(':');
      expect(token.length, greaterThan(20), reason: 'an FCM token is a long opaque string');
      debugPrint('FCM TOKEN OBTAINED (length ${token.length})');
    } else {
      // No Play Services / permission denied / no network. The requirement here is that this is
      // survivable: nothing threw, and no partial registration was recorded.
      expect(uploaded, isEmpty, reason: 'a failed start must not report a bogus registration');
      debugPrint('FCM UNAVAILABLE on this device — degradation path exercised');
    }

    await service.dispose();
  });
}
