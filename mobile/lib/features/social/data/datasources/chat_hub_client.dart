import 'dart:async';

import 'package:signalr_netcore/signalr_client.dart';

/// One server→client chat event, unwrapped from the versioned envelope (D-104).
///
/// The envelope is `{ v, type, roomId, id, payload }` and arrives on a single `chat` method, so a
/// new event type ships server-first. Unknown types and unknown fields are ignored by contract —
/// that forward-compatibility rule is why this class never switches exhaustively on [type].
class ChatEvent {
  const ChatEvent({required this.type, required this.roomId, this.id, this.payload});

  /// MessageReceived | MessageDeleted | MessageEdited | MessagePinned | ReactionChanged | RoomUpdated |
  /// MemberMuted | MemberBanned | MemberRemoved | PresenceChanged | TypingChanged | ReadReceiptChanged |
  /// DeliveryReceiptChanged. Not an enum by contract (D-104): an unknown type must parse and be ignored,
  /// never break an older client.
  final String type;
  final String roomId;
  /// Id of the message this event concerns. Lets a reconnecting client detect a gap against its
  /// cache and reconcile with `?after=`; null for room-level events.
  final String? id;
  final Map<String, dynamic>? payload;

  static ChatEvent? tryParse(Object? raw) {
    if (raw is! Map) return null;
    final map = Map<String, dynamic>.from(raw);
    final type = map['type'];
    final roomId = map['roomId'];
    if (type is! String || roomId == null) return null;
    return ChatEvent(
      type: type,
      roomId: '$roomId',
      id: map['id']?.toString(),
      payload: (map['payload'] as Map?)?.cast<String, dynamic>(),
    );
  }
}

/// Live chat updates over SignalR.
///
/// Shaped after [LoginStatusStream] (the app's existing hub client) with one difference that
/// matters: `/hubs/chat` requires authentication, and a WebSocket cannot carry an Authorization
/// header — so the access token rides the query string, which is the server's documented convention.
///
/// **Realtime is an optimisation, never the source of truth.** A dead connection degrades the room
/// to pull-to-refresh plus cursor sync; it is never surfaced as "chat is broken". Every reconnect
/// re-runs the `?after=` catch-up, so a message missed while disconnected is recovered from REST
/// rather than lost.
class ChatHubClient {
  ChatHubClient({required this.baseUrl, required this.accessToken});

  final String baseUrl;

  /// Read lazily on every (re)connect so a token refreshed by [AuthInterceptor] is picked up
  /// without tearing this client down.
  final Future<String?> Function() accessToken;

  HubConnection? _connection;
  final _events = StreamController<ChatEvent>.broadcast();
  final _reconnects = StreamController<void>.broadcast();
  final _disconnects = StreamController<void>.broadcast();
  String? _joinedRoomId;
  bool _disposed = false;

  Stream<ChatEvent> get events => _events.stream;

  /// Fires after the connection is re-established. The room controller listens and runs the
  /// `?after=` catch-up, which is what closes the gap opened while the socket was down.
  Stream<void> get reconnected => _reconnects.stream;

  /// Fires when the socket drops or starts reconnecting.
  ///
  /// Without this, a dropped connection left `live` true and every presence indicator frozen on
  /// screen — the client would keep showing people as online for as long as the tab stayed open,
  /// which is precisely the fabricated presence D-114 forbids. The event stream does **not** error
  /// on a normal close, so nothing else observes it.
  Stream<void> get disconnected => _disconnects.stream;

  bool get isConnected => _connection?.state == HubConnectionState.Connected;

  /// Connects and joins [roomId]. Returns false when realtime is unavailable — callers must treat
  /// that as "no live updates", not as an error to show.
  Future<bool> connect(String roomId) async {
    if (_disposed) return false;
    await disconnect();

    try {
      final token = await accessToken();
      if (token == null) return false;

      final connection = HubConnectionBuilder()
          .withUrl('$baseUrl/hubs/chat?access_token=$token')
          .withAutomaticReconnect(retryDelays: [0, 2000, 5000, 10000, 30000])
          .build();

      connection.on('chat', (args) {
        final event = ChatEvent.tryParse(args?.isNotEmpty == true ? args!.first : null);
        if (event != null && !_events.isClosed) _events.add(event);
      });

      // withAutomaticReconnect re-establishes the socket but does NOT restore group membership —
      // JoinRoom must be re-invoked, and the server re-checks membership each time (D-017).
      connection.onreconnecting(({Exception? error}) {
        if (!_disconnects.isClosed) _disconnects.add(null);
      });
      connection.onclose(({Exception? error}) {
        if (!_disconnects.isClosed) _disconnects.add(null);
      });

      connection.onreconnected(({String? connectionId}) async {
        final room = _joinedRoomId;
        if (room == null) return;
        try {
          await connection.invoke('JoinRoom', args: [room]);
          if (!_reconnects.isClosed) _reconnects.add(null);
        } catch (_) {
          // Membership may have been revoked while offline (banned, refunded). REST will report it.
        }
      });

      await connection.start();
      await connection.invoke('JoinRoom', args: [roomId]);
      _connection = connection;
      _joinedRoomId = roomId;
      return true;
    } catch (_) {
      await _connection?.stop();
      _connection = null;
      _joinedRoomId = null;
      return false;
    }
  }

  /// Tells the server this user started or stopped typing (D-114).
  ///
  /// Fire-and-forget: a dropped typing event is cosmetic, and the receiver's own TTL clears the
  /// indicator anyway. Never worth surfacing an error for.
  Future<void> sendTyping(String roomId, bool isTyping) async {
    final connection = _connection;
    if (connection?.state != HubConnectionState.Connected) return;
    try {
      await connection!.invoke('Typing', args: [roomId, isTyping]);
    } catch (_) {
      // Cosmetic only.
    }
  }

  /// Refreshes this connection's presence TTL.
  ///
  /// Not polling — presence *state* only ever arrives as a pushed event. This exists so the
  /// server's TTL can stay short enough to recover from a crashed process without expiring under a
  /// healthy idle connection.
  Future<void> heartbeat() async {
    final connection = _connection;
    if (connection?.state != HubConnectionState.Connected) return;
    try {
      await connection!.invoke('Heartbeat');
    } catch (_) {
      // The next tick tries again; a missed beat is covered by the TTL margin.
    }
  }

  Future<void> disconnect() async {
    final connection = _connection;
    final room = _joinedRoomId;
    _connection = null;
    _joinedRoomId = null;
    if (connection == null) return;
    try {
      if (room != null && connection.state == HubConnectionState.Connected) {
        await connection.invoke('LeaveRoom', args: [room]);
      }
    } catch (_) {
      // Best effort — the server drops the connection from the group on disconnect anyway.
    }
    await connection.stop();
  }

  Future<void> dispose() async {
    _disposed = true;
    await disconnect();
    if (!_events.isClosed) await _events.close();
    if (!_reconnects.isClosed) await _reconnects.close();
    if (!_disconnects.isClosed) await _disconnects.close();
  }
}
