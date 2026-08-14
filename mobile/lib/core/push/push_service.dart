import 'dart:async';

import 'package:firebase_messaging/firebase_messaging.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../network/api_error.dart';
import '../network/network_providers.dart';

/// A push message the app knows how to act on.
///
/// Parsed from FCM's `data` payload rather than `notification`, because the backend sends
/// data-only messages for login approval (`OutboxDispatchJob` / `LoginApprovalService`) so the app
/// controls presentation and can act while in the foreground.
class PushMessage {
  const PushMessage({
    required this.type,
    this.challengeId,
    this.matchNumber,
    this.eventId,
    this.roomId,
    this.messageId,
    this.senderId,
  });

  /// `login_approval` | `login.approved` | `device.revoked` | `recovery.redeemed` | `chat` | unknown
  final String type;
  final String? challengeId;
  final int? matchNumber;

  // ── Chat deep-link identifiers (D-107) ──────────────────────────────────────────────────
  final String? eventId;
  final String? roomId;
  final String? messageId;
  final String? senderId;

  bool get isLoginApproval => type == 'login_approval';

  /// True for every chat notification kind (message, mention, announcement, room locked,
  /// moderation notice) — the backend distinguishes them by `Kind`, but they all deep-link the same
  /// way, so the client only needs to know it is chat.
  bool get isChat => type == 'chat';

  /// Tolerant by design: an unknown or malformed payload yields a message with an unknown type
  /// rather than throwing. A push handler that throws on an unexpected field is a crash the user
  /// cannot avoid or report.
  ///
  /// Two payload shapes are accepted. The auth rail (AM4/D-096) sends `type`; the chat pipeline
  /// (D-107) sends `notificationType`. `type` wins when both are present, so existing auth pushes
  /// behave exactly as before and the frozen chat contract needs no server change.
  factory PushMessage.fromData(Map<String, dynamic> data) => PushMessage(
        type: data['type']?.toString() ??
            data['notificationType']?.toString() ??
            'unknown',
        challengeId: data['challenge_id']?.toString(),
        matchNumber: int.tryParse(data['match_number']?.toString() ?? ''),
        eventId: data['eventId']?.toString(),
        roomId: data['roomId']?.toString(),
        messageId: data['messageId']?.toString(),
        senderId: data['senderId']?.toString(),
      );
}

/// Registers this install for push and routes incoming messages (AM4/D-096).
///
/// ## What a push is, and is not
///
/// A login-approval push is a **hint that something is waiting**, never the thing itself. The
/// payload carries no nonce, no token, and nothing signable — the app always re-fetches
/// `/v1/auth/login/pending` and signs the nonce the *server* hands it. So a forged or replayed push
/// can at worst make the app refresh a list; it can never cause an approval.
///
/// That also means push is optional: FCM being unavailable (no Play Services, permission denied,
/// throttled) degrades the UX to pull-to-refresh, never breaks approval.
class PushService {
  PushService(this._messaging, this._registerToken);

  final FirebaseMessaging _messaging;

  /// Uploads the token to `POST /v1/devices/register`. Injected so the whole flow is testable
  /// without Firebase or a network.
  final Future<void> Function(String token, String platform) _registerToken;

  final _messages = StreamController<PushMessage>.broadcast();
  final _opened = StreamController<PushMessage>.broadcast();
  StreamSubscription<String>? _tokenRefresh;

  /// Messages that arrived while the app was running.
  Stream<PushMessage> get messages => _messages.stream;

  /// Messages the user **tapped**, as opposed to ones that merely arrived (D-107).
  ///
  /// Deep linking has to distinguish the two: navigating on arrival would yank a user out of
  /// whatever they were doing every time a chat message came in. Only a tap is an instruction to go
  /// somewhere. Cold start is covered by [initialMessage] rather than this stream, because the
  /// launch message is already consumed by the time any listener attaches.
  Stream<PushMessage> get opened => _opened.stream;

  /// The notification that launched the app from terminated state, or null. Consumed once.
  Future<PushMessage?> initialMessage() async {
    try {
      final message = await _messaging.getInitialMessage();
      if (message == null) return null;
      return PushMessage.fromData(message.data);
    } catch (_) {
      return null;
    }
  }

  /// Requests permission, registers the token, and starts listening.
  ///
  /// Returns false when push is unavailable for any reason. Callers must treat that as "no
  /// acceleration", not as a failure worth showing the user — approvals still work by polling.
  Future<bool> start({required String platform}) async {
    try {
      final settings = await _messaging.requestPermission();
      if (settings.authorizationStatus == AuthorizationStatus.denied) {
        // A declined prompt is a legitimate user choice, not an error state.
        return false;
      }

      final token = await _messaging.getToken();
      if (token == null || token.isEmpty) return false;
      await _safeRegister(token, platform);

      // FCM rotates tokens (app restore, reinstall, storage clear). A stale token means silent
      // delivery failure, so re-register on every rotation.
      _tokenRefresh ??= _messaging.onTokenRefresh.listen(
        (refreshed) => _safeRegister(refreshed, platform),
        onError: (_) {},
      );

      FirebaseMessaging.onMessage.listen(_emit);
      // A tap feeds both streams: existing listeners keep seeing every message, and the
      // deep-link handler sees only the ones the user actually acted on.
      FirebaseMessaging.onMessageOpenedApp.listen((m) {
        _emit(m);
        if (!_opened.isClosed) _opened.add(PushMessage.fromData(m.data));
      });

      // A push received while the app was terminated: surfaced on next launch so tapping the
      // notification still lands on the approval screen.
      final initial = await _messaging.getInitialMessage();
      if (initial != null) _emit(initial);

      return true;
    } catch (e) {
      // Missing Play Services, no google-services.json, an emulator without FCM — all recoverable.
      debugPrint('Push unavailable: $e');
      return false;
    }
  }

  void _emit(RemoteMessage message) {
    if (_messages.isClosed) return;
    _messages.add(PushMessage.fromData(message.data));
  }

  /// Registration failure must never break app start — the user simply gets no push.
  Future<void> _safeRegister(String token, String platform) async {
    try {
      await _registerToken(token, platform);
    } on ApiError catch (e) {
      debugPrint('FCM token registration failed: ${e.code}');
    } catch (e) {
      debugPrint('FCM token registration failed: $e');
    }
  }

  Future<void> dispose() async {
    await _tokenRefresh?.cancel();
    _tokenRefresh = null;
    if (!_messages.isClosed) await _messages.close();
    if (!_opened.isClosed) await _opened.close();
  }
}

/// Uploads the FCM token to the existing device-registration endpoint. Deliberately reuses
/// `POST /v1/devices/register` (the notification device table `LoginApprovalService` already pushes
/// to) rather than introducing a second registry — the trusted-device table is a different concept
/// and must not be conflated with a push destination.
final fcmTokenRegistrarProvider = Provider<Future<void> Function(String, String)>((ref) {
  final dio = ref.watch(dioProvider);
  return (token, platform) async {
    await dio.post('/v1/devices/register', data: {
      'fcmToken': token,
      'platform': platform,
      'deviceName': null,
      'appVersion': null,
    });
  };
});

final pushServiceProvider = Provider<PushService>((ref) {
  final service = PushService(FirebaseMessaging.instance, ref.watch(fcmTokenRegistrarProvider));
  ref.onDispose(service.dispose);
  return service;
});
