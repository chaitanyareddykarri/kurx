import 'dart:async';

import 'dart:io' show Platform;

import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../features/auth/data/repositories/trusted_device_repository.dart';
import '../router/app_router.dart';
import 'push_service.dart';

/// Starts push once the user is signed in, and routes login-approval messages (AM4/D-096).
///
/// ## What a push actually does here
///
/// Receiving `type: login_approval` **only invalidates the pending-approvals provider**, causing a
/// re-fetch of `/v1/auth/login/pending`. The nonce the device signs always comes from that server
/// response, never from the push payload. So the worst a forged, replayed or tampered push can
/// achieve is making the app refresh a list — it can never cause or pre-authorise an approval.
///
/// The push is therefore a **latency optimisation over polling**, and everything keeps working when
/// it is absent: no Play Services, denied notification permission, FCM throttling, or a build with
/// no Firebase config all degrade to pull-to-refresh.
class PushBootstrap {
  PushBootstrap(this._ref);

  final Ref _ref;
  StreamSubscription<PushMessage>? _subscription;
  StreamSubscription<PushMessage>? _openedSubscription;
  bool _started = false;

  /// Idempotent: safe to call on every sign-in and on app resume.
  Future<void> start() async {
    if (_started) return;
    _started = true;

    final service = _ref.read(pushServiceProvider);
    final available = await service.start(platform: _platformName());
    if (!available) {
      // Not an error worth showing: approvals still arrive by polling.
      debugPrint('Push not available; falling back to polling for approvals.');
      return;
    }

    _subscription = service.messages.listen(_onMessage);

    // Deep linking (D-107). Two entry points, and both are needed: a tap while the app is alive
    // arrives on `opened`, whereas a tap that launched the app from terminated state is only ever
    // available from getInitialMessage() and would otherwise be lost.
    _openedSubscription = service.opened.listen(_onOpened);
    final launch = await service.initialMessage();
    if (launch != null) _onOpened(launch);
  }

  /// Navigates in response to a tapped notification, reusing the app's single GoRouter — there is
  /// deliberately no second navigation mechanism.
  void _onOpened(PushMessage message) {
    if (!message.isChat) return;

    // Room id first (D-292): the route is keyed on it, and a direct-message push carries no event
    // at all — reading only eventId meant every DM notification was dropped here before it could
    // navigate. eventId remains a fallback for pushes minted before roomId was on the payload; the
    // controller resolves it.
    final target = (message.roomId?.isNotEmpty ?? false) ? message.roomId! : message.eventId;
    if (target == null || target.isEmpty) return;

    // "-> specific message" is best-effort by contract: there is no fetch-message-by-id endpoint and
    // cursors are exclusive, so the room opens at the latest page and the message is visible only if
    // it falls inside it (documented debt, D-104).
    _ref.read(routerProvider).push('/chats/$target');
  }

  void _onMessage(PushMessage message) {
    if (!message.isLoginApproval) return;
    // Re-fetch from the server; the payload is a hint, never a source of truth.
    _ref.invalidate(pendingLoginsProvider);
  }

  Future<void> dispose() async {
    await _subscription?.cancel();
    await _openedSubscription?.cancel();
    _subscription = null;
    _openedSubscription = null;
    _started = false;
  }

  static String _platformName() {
    if (kIsWeb) return 'web';
    return Platform.isIOS ? 'ios' : 'android';
  }
}

final pushBootstrapProvider = Provider<PushBootstrap>((ref) {
  final bootstrap = PushBootstrap(ref);
  ref.onDispose(bootstrap.dispose);
  return bootstrap;
});
