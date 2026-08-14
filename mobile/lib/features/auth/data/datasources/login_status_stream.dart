import 'dart:async';

import 'package:signalr_netcore/signalr_client.dart';

/// Realtime login status over SignalR (AM9/D-087) for the mobile *waiting* client — the case where
/// a user signs in on this phone and approves from another trusted device.
///
/// Two properties carried over from the backend contract, and both matter:
///
/// 1. **The hub is anonymous, authorized by the poll token.** The waiting client has no session yet,
///    so `Watch(challengeId, pollToken)` is what proves it started this login. Knowing a challenge
///    id is not enough — the same rule that gates `/login/status`.
/// 2. **The push carries status only, never tokens.** Tokens are always collected over HTTP, so the
///    "minted exactly once" guarantee stays on one code path. This class therefore emits a signal to
///    *go and poll*, not a session.
///
/// Connection failure is not an error condition: polling is the required fallback, so callers treat
/// a dead stream as "no acceleration" rather than "login broken".
class LoginStatusStream {
  LoginStatusStream({required this.baseUrl});

  final String baseUrl;

  HubConnection? _connection;
  final _controller = StreamController<String>.broadcast();

  /// Emits the status string (`approved` / `rejected`) each time the server pushes one.
  Stream<String> get statuses => _controller.stream;

  /// Connects and subscribes. Returns false when realtime is unavailable — the caller should keep
  /// polling and must not surface this to the user.
  Future<bool> watch({required String challengeId, required String pollToken}) async {
    try {
      final connection = HubConnectionBuilder()
          .withUrl('$baseUrl/hubs/login')
          .withAutomaticReconnect()
          .build();

      connection.on('login_status', (arguments) {
        final first = arguments?.isNotEmpty == true ? arguments!.first : null;
        final status = first is Map ? first['status'] as String? : null;
        if (status != null && !_controller.isClosed) _controller.add(status);
      });

      await connection.start();
      await connection.invoke('Watch', args: [challengeId, pollToken]);
      _connection = connection;
      return true;
    } catch (_) {
      // Offline, proxy, or a hub that refused the subscription. Polling covers it.
      await _connection?.stop();
      _connection = null;
      return false;
    }
  }

  Future<void> dispose() async {
    await _connection?.stop();
    _connection = null;
    if (!_controller.isClosed) await _controller.close();
  }
}
