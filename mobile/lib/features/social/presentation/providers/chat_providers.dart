import 'dart:async';

import 'package:flutter/foundation.dart';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/network/api_error.dart';
import '../../../../core/network/app_config.dart';
import '../../../../core/network/network_providers.dart';
import '../../../../core/storage/chat_cache.dart';
import '../../../../core/storage/token_store.dart';
import '../../data/datasources/chat_hub_client.dart';
import '../../data/datasources/chat_remote_data_source.dart';
import '../../data/repositories/chat_repository_impl.dart';
import 'package:dio/dio.dart';

import '../../domain/entities/chat_attachment.dart';
import '../../domain/entities/chat_message.dart';
import '../../domain/entities/chat_room.dart';
import '../../domain/repositories/chat_repository.dart';

final chatRepositoryProvider = Provider<ChatRepository>((ref) => ChatRepositoryImpl(
      ChatRemoteDataSource(ref.watch(dioProvider)),
      ref.watch(chatCacheProvider),
    ));

/// The signed-in user's rooms.
///
/// Sorted client-side. `lastActivity` is the last message's timestamp as of D-292 (it was the
/// event's updated-at), so the server value is now sound on its own; a room with a locally-known
/// last message still wins, because a just-sent message is known here before the next fetch.
final myChatsProvider = FutureProvider.autoDispose<List<MyChat>>((ref) async {
  final repo = ref.watch(chatRepositoryProvider);
  final rooms = await repo.myChats();

  DateTime? localActivity(MyChat c) {
    final cached = repo.cachedMessages(c.roomId);
    return cached.isEmpty ? null : cached.last.createdAt;
  }

  final sorted = [...rooms]..sort((a, b) {
      final at = localActivity(a) ?? a.lastActivity;
      final bt = localActivity(b) ?? b.lastActivity;
      if (at == null && bt == null) return a.eventTitle.compareTo(b.eventTitle);
      if (at == null) return 1;
      if (bt == null) return -1;
      return bt.compareTo(at);
    });
  return sorted;
});

/// D-306 — the event rooms this reader has filed away.
///
/// A sibling of [myChatsProvider] rather than a family over it: six call sites read the active list and
/// none of them wants a parameter, so a family would churn all of them to express a distinction only
/// this screen makes. Newest first, with no local-activity override — an archived room is not competing
/// for attention in the inbox, so the server's ordering is the whole story.
final archivedChatsProvider = FutureProvider.autoDispose<List<MyChat>>((ref) async {
  final rooms = await ref.watch(chatRepositoryProvider).myChats(archived: true);
  return [...rooms]..sort((a, b) {
      final at = a.lastActivity, bt = b.lastActivity;
      if (at == null && bt == null) return a.eventTitle.compareTo(b.eventTitle);
      if (at == null) return 1;
      if (bt == null) return -1;
      return bt.compareTo(at);
    });
});

/// Someone the server says is typing, and when we stop believing it.
///
/// The expiry comes from the server's `ttlSeconds`, so a lost "stopped" event can never strand an
/// indicator on screen — the receiver forgets on its own.
class TypingUser {
  const TypingUser({required this.userId, required this.name, required this.expiresAt});

  final String userId;
  final String name;
  final DateTime expiresAt;

  bool get isExpired => DateTime.now().isAfter(expiresAt);
}

/// Immutable view of one open room.
class ChatRoomState {
  const ChatRoomState({
    required this.messages,
    this.room,
    this.loading = true,
    this.loadingOlder = false,
    this.hasMoreOlder = true,
    this.live = false,
    this.error,
    this.uploads = const {},
    this.onlineUserIds = const {},
    this.typing = const {},
    this.readPointers = const {},
    this.deliveredPointers = const {},
    this.presenceEnabled = false,
  });

  final List<ChatMessage> messages;
  final ChatRoom? room;
  final bool loading;
  final bool loadingOlder;
  final bool hasMoreOlder;

  /// True while the SignalR connection is up. Drives a subtle banner only — the room stays fully
  /// usable without it.
  final bool live;
  final Object? error;

  /// Everyone the server says is connected to this room (D-114).
  ///
  /// Populated from the room fetch, then maintained purely by `PresenceChanged` events. Presence is
  /// **never inferred locally** — the client does not guess that a sender must be online, and does
  /// not poll.
  final Set<String> onlineUserIds;

  /// Who is typing right now, keyed by user id. Never persisted, never restored after a restart.
  final Map<String, TypingUser> typing;

  /// Each member's furthest-read message id. Monotonic: an older pointer is discarded rather than
  /// applied, so an out-of-order event cannot walk a receipt backwards.
  final Map<String, String> readPointers;

  /// D-295 — the newest message each member has RECEIVED. Deliberately separate from [readPointers]:
  /// arriving is not reading, and one map for both would mark everything read the moment it landed.
  final Map<String, String> deliveredPointers;

  /// False when the server has no shared presence store. Distinguishes "nobody is here" from "we
  /// cannot know", so the UI can hide indicators rather than show everyone as offline.
  final bool presenceEnabled;

  /// Uploads still in flight or failed, keyed by their local id. They live outside [messages]
  /// because no message exists until the upload is confirmed — which is exactly the rule that a
  /// permanent chat message must not appear before confirmation succeeds.
  final Map<String, ChatAttachment> uploads;

  ChatCapabilities get capabilities => room?.capabilities ?? const ChatCapabilities.none();

  ChatRoomState copyWith({
    List<ChatMessage>? messages,
    ChatRoom? room,
    bool? loading,
    bool? loadingOlder,
    bool? hasMoreOlder,
    bool? live,
    Object? error,
    bool clearError = false,
    Map<String, ChatAttachment>? uploads,
    Set<String>? onlineUserIds,
    Map<String, TypingUser>? typing,
    Map<String, String>? readPointers,
    Map<String, String>? deliveredPointers,
    bool? presenceEnabled,
  }) =>
      ChatRoomState(
        messages: messages ?? this.messages,
        room: room ?? this.room,
        loading: loading ?? this.loading,
        loadingOlder: loadingOlder ?? this.loadingOlder,
        hasMoreOlder: hasMoreOlder ?? this.hasMoreOlder,
        live: live ?? this.live,
        error: clearError ? null : (error ?? this.error),
        uploads: uploads ?? this.uploads,
        onlineUserIds: onlineUserIds ?? this.onlineUserIds,
        typing: typing ?? this.typing,
        readPointers: readPointers ?? this.readPointers,
        deliveredPointers: deliveredPointers ?? this.deliveredPointers,
        presenceEnabled: presenceEnabled ?? this.presenceEnabled,
      );

  /// Typists other than me, still within their TTL. Derived — widgets never keep their own map.
  List<TypingUser> typistsExcept(String? myUserId) => typing.values
      .where((t) => t.userId != myUserId && !t.isExpired)
      .toList()
    ..sort((a, b) => a.name.compareTo(b.name));   // stable order, so the banner does not jitter

  bool isOnline(String? userId) =>
      presenceEnabled && userId != null && onlineUserIds.contains(userId);

  /// The newest message any *other* member has read. One value for the whole list, so a receipt
  /// update does not have to be evaluated per bubble.
  String? highestReadByOthers(String? myUserId) => _highest(readPointers, myUserId);

  /// D-295 — the newest message any other member has RECEIVED. Drives the second tick.
  String? highestDeliveredByOthers(String? myUserId) => _highest(deliveredPointers, myUserId);

  static String? _highest(Map<String, String> pointers, String? myUserId) {
    String? best;
    for (final entry in pointers.entries) {
      if (entry.key == myUserId) continue;
      if (best == null || entry.value.compareTo(best) > 0) best = entry.value;
    }
    return best;
  }
}

/// Drives one chat room: history, pagination, optimistic send, live updates and reconnect catch-up.
class ChatRoomController extends StateNotifier<ChatRoomState> {
  ChatRoomController(this._ref, this.routeId)
      : super(const ChatRoomState(messages: [])) {
    _init();
  }

  final Ref _ref;

  /// The id the route carried (D-292). Normally a **roomId** — that is what every list, profile and
  /// notification now pushes. It may still be an **eventId** from an older deep link or the
  /// workspace tile, which is why [_fetchRoom] resolves rather than assumes. Everything downstream
  /// uses [_roomId], the real room id, so only the lookup ever has to care.
  final String routeId;

  ChatHubClient? _hub;
  Map<String, ChatAttachment> _uploads = const {};
  Timer? _typingPrune;
  Timer? _heartbeat;
  Timer? _typingStopDebounce;
  bool _typingSent = false;
  StreamSubscription<ChatEvent>? _events;
  StreamSubscription<void>? _reconnects;
  StreamSubscription<void>? _disconnects;
  String? _roomId;
  /// D-295 — stops the delivery acknowledgement re-firing for a message already acknowledged.
  String? _ackedDeliveredId;

  ChatRepository get _repo => _ref.read(chatRepositoryProvider);

  /// Loads the room for [routeId], resolving an event-addressed id when that is what arrived.
  ///
  /// Mirrors the web route's behaviour (D-292): try the room endpoint first, because a room id is
  /// what every current caller pushes, and fall back to the event endpoint only on a 404 — the one
  /// status that means "no room has this id", as opposed to 403 which means the room exists and is
  /// not the caller's. Once resolved, [_roomId] short-circuits this, so a refresh or a presence
  /// resync never pays for the fallback twice.
  Future<ChatRoom> _fetchRoom() async {
    final known = _roomId;
    if (known != null) return _repo.roomById(known);
    try {
      return await _repo.roomById(routeId);
    } on ApiError catch (e) {
      if (e.status != 404) rethrow;
      return _repo.room(routeId);
    }
  }

  Future<void> _init() async {
    // Cache first: history paints before any network call, so a cold start offline is a populated
    // room rather than a spinner or an error.
    try {
      final room = await _fetchRoom();
      // Leaving the room mid-load disposes this controller; the container goes with it, so every
      // step past an await has to re-check rather than touch a torn-down ref.
      if (!mounted) return;
      _roomId = room.roomId;
      state = state.copyWith(
        room: room,
        messages: _repo.cachedMessages(room.roomId),
        loading: false,
        clearError: true,
        // Initial synchronisation: everything after this arrives as an event.
        onlineUserIds: room.onlineUserIds.toSet(),
        presenceEnabled: room.presenceEnabled,
      );
    } catch (e) {
      if (mounted) state = state.copyWith(loading: false, error: e);
      return;
    }

    await _loadLatest();
    if (!mounted) return;
    await _connect();
    if (!mounted) return;
    unawaited(_flush());
  }

  Future<void> _loadLatest() async {
    final roomId = _roomId;
    if (roomId == null) return;
    try {
      final messages = await _repo.latest(roomId);
      // The room can be closed mid-fetch — autoDispose tears the controller down as soon as the
      // last listener goes, and touching state after that throws.
      if (!mounted) return;
      state = state.copyWith(messages: messages, clearError: true);
      _markReadToNewest();
      unawaited(_ackDelivered());   // D-295 — separate from the read pointer above, deliberately
    } catch (e) {
      if (mounted && state.messages.isEmpty) state = state.copyWith(error: e);
    }
  }

  Future<void> _connect() async {
    final roomId = _roomId;
    if (roomId == null || !mounted) return;

    final hub = ChatHubClient(
      baseUrl: AppConfig.apiBase,
      accessToken: () => _ref.read(tokenStoreProvider).accessToken(),
    );
    _hub = hub;
    _events = hub.events.listen(onEvent);
    // Every reconnect re-runs the delta fetch: that is what recovers messages sent while the
    // socket was down, and it is why a dropped connection is not a correctness problem.
    _reconnects = hub.reconnected.listen((_) => unawaited(_catchUp()));

    // Losing the socket means presence is unknowable. Clearing beats showing stale dots, and the
    // reconnect path re-reads the roster rather than restoring what we were holding.
    _disconnects = hub.disconnected.listen((_) {
      if (!mounted) return;
      state = state.copyWith(live: false);
      _clearLivePresence();
    });
    _events!.onError((_) => _clearLivePresence());

    final connected = await hub.connect(roomId);
    if (!mounted) return;
    state = state.copyWith(live: connected);
    if (!connected) _clearLivePresence();

    // Refresh the server's presence TTL well inside its 120s window.
    _heartbeat?.cancel();
    _heartbeat = Timer.periodic(const Duration(seconds: 45), (_) => unawaited(hub.heartbeat()));
  }

  /// Applies one realtime event. Public so tests can drive the event path without standing up a
  /// SignalR connection — the hub client is constructed internally by [_connect].
  @visibleForTesting
  void onEvent(ChatEvent event) {
    if (event.roomId != _roomId) return;

    switch (event.type) {
      case 'MessageReceived':
        final payload = event.payload;
        if (payload == null) {
          unawaited(_catchUp());   // envelope without a body: reconcile from REST
          return;
        }
        unawaited(_ingest([payload]));
      case 'PresenceChanged':
        _applyPresence(event.payload);
      case 'TypingChanged':
        _applyTyping(event.payload);
      case 'ReadReceiptChanged':
        _applyReadReceipt(event.payload);
        break;
      case 'DeliveryReceiptChanged':   // D-295
        _applyDeliveryReceipt(event.payload);
      case 'MessageDeleted':
      // D-293: an edit rewrites a message already on screen. Re-read like MessageDeleted always has —
      // an edit is rare enough that the extra page fetch is not worth a bespoke merge path, and this
      // keeps the Flutter and web clients handling it identically.
      case 'MessageEdited':
      case 'MessagePinned':
      case 'RoomUpdated':
      case 'MemberMuted':
      case 'MemberBanned':
      // D-299. Both were broadcast by the server and handled by nobody.
      //
      // `ReactionChanged`: reactions were not live at all — another member's emoji only showed up after
      // a manual refresh, because the summary lives on the message and nothing re-read it.
      //
      // `MemberRemoved`: the eviction event from D-294, the one a REFUND takes. The server pulls the
      // socket out of the group, and this client kept painting a live room that had merely gone quiet,
      // with sends failing and nothing explaining why. `MemberBanned` above has always been handled;
      // this is the sibling that was missed.
      case 'ReactionChanged':
      case 'MemberRemoved':
        // Room-level change: re-read authoritative state rather than mutating locally, so
        // capabilities (and therefore the whole UI) stay server-driven.
        unawaited(refresh());
      default:
        // Unknown type — ignore by contract, so new server events never break an old client.
        break;
    }
  }

  /// Applies a server presence transition. The server only emits on an actual change, so this is
  /// a straight set operation — no local inference, no guessing.
  void _applyPresence(Map<String, dynamic>? payload) {
    final userId = payload?['userId']?.toString();
    if (userId == null || !mounted) return;

    final online = payload!['online'] == true;
    final next = {...state.onlineUserIds};
    if (online) {
      next.add(userId);
    } else {
      next.remove(userId);
      // Someone who went offline cannot still be typing.
      final typing = {...state.typing}..remove(userId);
      state = state.copyWith(onlineUserIds: next, typing: typing);
      return;
    }
    state = state.copyWith(onlineUserIds: next);
  }

  void _applyTyping(Map<String, dynamic>? payload) {
    final userId = payload?['userId']?.toString();
    if (userId == null || !mounted) return;

    final typing = {...state.typing};
    if (payload!['isTyping'] == true) {
      final ttl = payload['ttlSeconds'] is int ? payload['ttlSeconds'] as int : 8;
      // A repeat simply pushes the expiry out — duplicates are absorbed, not stacked.
      typing[userId] = TypingUser(
        userId: userId,
        name: payload['userName']?.toString() ?? 'Someone',
        expiresAt: DateTime.now().add(Duration(seconds: ttl)),
      );
    } else {
      typing.remove(userId);
    }

    state = state.copyWith(typing: typing);
    _syncTypingPrune();
  }

  /// Read pointers only ever move forward. Server ids are UUIDv7, so a lexical compare is a time
  /// compare — an out-of-order or replayed event is discarded rather than walking a receipt back.
  void _applyReadReceipt(Map<String, dynamic>? payload) {
    final userId = payload?['userId']?.toString();
    final messageId = payload?['lastReadMessageId']?.toString();
    if (userId == null || messageId == null || !mounted) return;

    final current = state.readPointers[userId];
    if (current != null && messageId.compareTo(current) <= 0) return;   // duplicate or stale

    state = state.copyWith(readPointers: {...state.readPointers, userId: messageId});
  }

  /// D-295 — same forward-only rule as the read pointer, into the delivery map. The server's payload
  /// key is `messageId`, not `lastReadMessageId`: a delivery receipt is a different event, not a
  /// read receipt with another name.
  void _applyDeliveryReceipt(Map<String, dynamic>? payload) {
    final userId = payload?['userId']?.toString();
    final messageId = payload?['messageId']?.toString();
    if (userId == null || messageId == null || !mounted) return;

    final current = state.deliveredPointers[userId];
    if (current != null && messageId.compareTo(current) <= 0) return;

    state = state.copyWith(deliveredPointers: {...state.deliveredPointers, userId: messageId});
  }

  /// Runs a prune timer only while someone is actually typing, so an idle room has no timers at all.
  void _syncTypingPrune() {
    if (state.typing.isEmpty) {
      _typingPrune?.cancel();
      _typingPrune = null;
      return;
    }
    _typingPrune ??= Timer.periodic(const Duration(seconds: 1), (_) {
      if (!mounted) return;
      final live = {...state.typing}..removeWhere((_, t) => t.isExpired);
      if (live.length != state.typing.length) state = state.copyWith(typing: live);
      if (live.isEmpty) _syncTypingPrune();
    });
  }

  /// Reports composer activity. Debounced: `started` is sent once per typing burst, and a `stopped`
  /// follows a short pause — so a fast typist produces two events, not one per keystroke.
  void onComposerChanged(String text) {
    final roomId = _roomId;
    final hub = _hub;
    if (roomId == null || hub == null || !state.presenceEnabled) return;

    _typingStopDebounce?.cancel();

    if (text.trim().isEmpty) {
      // An empty composer is not typing.
      if (_typingSent) {
        _typingSent = false;
        unawaited(hub.sendTyping(roomId, false));
      }
      return;
    }

    if (!_typingSent) {
      _typingSent = true;
      unawaited(hub.sendTyping(roomId, true));
    }
    _typingStopDebounce = Timer(const Duration(seconds: 3), () {
      _typingSent = false;
      unawaited(hub.sendTyping(roomId, false));
    });
  }

  /// Called once a message is sent — the burst is over.
  void _stopTyping() {
    final roomId = _roomId;
    final hub = _hub;
    _typingStopDebounce?.cancel();
    if (roomId != null && hub != null && _typingSent) {
      _typingSent = false;
      unawaited(hub.sendTyping(roomId, false));
    }
  }

  Future<void> _ingest(List<Map<String, dynamic>> raw) async {
    final roomId = _roomId;
    if (roomId == null) return;
    await _repo.latest(roomId);   // merges + persists; cheap because the page is already warm
    if (!mounted) return;
    state = state.copyWith(messages: _repo.cachedMessages(roomId));
    _markReadToNewest();
  }

  /// Fetches everything after the newest message we hold. The gap-recovery path.
  Future<void> _catchUp() async {
    final roomId = _roomId;
    if (roomId == null) return;
    final confirmed = state.messages.where((m) => m.status == ChatSendStatus.sent).toList();
    if (confirmed.isEmpty) {
      await _loadLatest();
      return;
    }
    try {
      await _repo.since(roomId, confirmed.last.id);
      if (!mounted) return;
      state = state.copyWith(messages: _repo.cachedMessages(roomId), live: true);
      _markReadToNewest();
    } catch (_) {
      // Still unreachable; the next reconnect or manual refresh tries again.
    }

    // Reconnect re-synchronises the roster from the server rather than trusting what we held while
    // disconnected — presence during a gap is unknowable, so it is re-read, never carried over.
    await _resyncPresence();
  }

  Future<void> _resyncPresence() async {
    try {
      final room = await _fetchRoom();
      if (!mounted) return;
      state = state.copyWith(
        room: room,
        live: true,
        onlineUserIds: room.onlineUserIds.toSet(),
        presenceEnabled: room.presenceEnabled,
      );
    } catch (_) {
      // Leave the last known roster; the next reconnect tries again.
    }
  }

  /// The disconnect path, exposed for tests: the hub client is built internally by [_connect], so
  /// there is no socket to drop from the outside.
  @visibleForTesting
  void debugConnectionLost() {
    if (!mounted) return;
    state = state.copyWith(live: false);
    _clearLivePresence();
  }

  /// Drops all live presence. Called when the socket goes away: showing stale dots would be
  /// fabricating presence, which is worse than showing none.
  void _clearLivePresence() {
    if (!mounted) return;
    _typingPrune?.cancel();
    _typingPrune = null;
    state = state.copyWith(onlineUserIds: const {}, typing: const {});
  }

  Future<void> loadOlder() async {
    final roomId = _roomId;
    if (roomId == null || state.loadingOlder || !state.hasMoreOlder) return;
    final confirmed = state.messages.where((m) => m.status == ChatSendStatus.sent).toList();
    if (confirmed.isEmpty) return;

    state = state.copyWith(loadingOlder: true);
    try {
      final older = await _repo.older(roomId, confirmed.first.id);
      if (!mounted) return;
      state = state.copyWith(
        messages: _repo.cachedMessages(roomId),
        loadingOlder: false,
        hasMoreOlder: older.isNotEmpty,
      );
    } catch (_) {
      if (mounted) state = state.copyWith(loadingOlder: false);
    }
  }

  /// Uploads one file, then queues a message carrying it.
  ///
  /// The pending message is rendered immediately with the attachment inside it, so the user sees
  /// their photo with a progress ring rather than a placeholder that later swaps out. Nothing
  /// becomes a permanent message until the server has confirmed both the upload and the send.
  Future<void> sendAttachment({
    required String fileName,
    required String contentType,
    required int sizeBytes,
    required List<int> bytes,
    String body = '',
    String? localPath,
    CancelToken? cancelToken,
  }) async {
    final roomId = _roomId;
    if (roomId == null) return;

    final localId = 'local-att-${DateTime.now().microsecondsSinceEpoch}';
    var attachment = ChatAttachment(
      id: localId,
      fileName: fileName,
      contentType: contentType,
      sizeBytes: sizeBytes,
      localPath: localPath,
      status: AttachmentUploadStatus.uploading,
    );

    void publish(ChatAttachment a) {
      if (!mounted) return;
      _uploads = {..._uploads, localId: a};
      state = state.copyWith(uploads: _uploads);
    }

    publish(attachment);

    try {
      final confirmed = await _repo.uploadAttachment(
        roomId,
        attachment,
        bytes,
        onProgress: (p) => publish(attachment = attachment.copyWith(progress: p)),
        cancelToken: cancelToken,
      );

      // Upload done: drop it from the in-flight map and let the message own it.
      _uploads = {..._uploads}..remove(localId);
      if (!mounted) return;
      state = state.copyWith(uploads: _uploads);

      await _repo.queueSend(roomId, body.trim(), attachments: [confirmed]);
      state = state.copyWith(messages: _repo.cachedMessages(roomId));
      await _flush();
    } on ApiError catch (e) {
      // Server refusals are shown verbatim by code — "file too large" is actionable, "upload
      // failed" is not.
      publish(attachment.copyWith(status: AttachmentUploadStatus.failed, error: e.code));
    } catch (_) {
      publish(attachment.copyWith(status: AttachmentUploadStatus.failed, error: 'upload_failed'));
    }
  }

  /// Discards a failed or cancelled upload. Nothing was sent, so there is nothing to undo.
  void dismissUpload(String localId) {
    _uploads = {..._uploads}..remove(localId);
    if (mounted) state = state.copyWith(uploads: _uploads);
  }

  /// [replyToMessageId] quotes another message. The repository and the server have both accepted it
  /// since chat shipped; nothing ever passed it, which is why replying was unreachable from the app.
  Future<void> send(String body, {String? replyToMessageId}) async {
    final roomId = _roomId;
    final text = body.trim();
    if (roomId == null || text.isEmpty) return;

    _stopTyping();

    // Render immediately; the row carries its clientMessageId so the server echo collapses onto it.
    await _repo.queueSend(roomId, text, replyToMessageId: replyToMessageId);
    if (!mounted) return;
    state = state.copyWith(messages: _repo.cachedMessages(roomId));
    await _flush();
  }

  Future<void> _flush() async {
    final roomId = _roomId;
    if (roomId == null || !mounted) return;
    await _repo.flushOutbox(roomId);
    if (!mounted) return;
    state = state.copyWith(messages: _repo.cachedMessages(roomId));
    _markReadToNewest();
  }

  Future<void> retry(String clientMessageId) async {
    final roomId = _roomId;
    if (roomId == null) return;
    await _repo.retry(roomId, clientMessageId);
    await _flush();
  }

  Future<void> discard(String clientMessageId) async {
    final roomId = _roomId;
    if (roomId == null) return;
    await _repo.discard(roomId, clientMessageId);
    if (mounted) state = state.copyWith(messages: _repo.cachedMessages(roomId));
  }

  /// D-293 — edit your own message. Throws [ApiError] on refusal (`no_change`, `edit_window_expired`,
  /// `forbidden`) so the caller can show the actual reason rather than a generic failure.
  Future<void> editMessage(String messageId, String body) async {
    final roomId = _roomId;
    if (roomId == null) return;
    await _repo.editMessage(roomId, messageId, body);
    if (mounted) state = state.copyWith(messages: _repo.cachedMessages(roomId));
  }

  /// Delete for everyone.
  Future<void> deleteMessage(String messageId) async {
    final roomId = _roomId;
    if (roomId == null) return;
    await _repo.deleteMessage(roomId, messageId);
    if (mounted) state = state.copyWith(messages: _repo.cachedMessages(roomId));
  }

  /// D-295 — toggles one emoji. The server returns the whole summary and the repository writes it
  /// into the cache, so the rebuilt list already carries the new counts.
  Future<void> toggleReaction(String messageId, String emoji) async {
    final roomId = _roomId;
    if (roomId == null) return;
    await _repo.toggleReaction(roomId, messageId, emoji);
    if (mounted) state = state.copyWith(messages: _repo.cachedMessages(roomId));
  }

  /// D-295 — acknowledges DELIVERY of the newest confirmed message.
  ///
  /// Deliberately not the read pointer: arriving is not reading, and conflating the two would mark
  /// every message read the instant it landed whether or not anyone looked. Optimistic rows are
  /// skipped because a `local:` id is not a server message. Forward-only server-side, so a failure
  /// needs no retry path and a repeat costs nothing.
  Future<void> _ackDelivered() async {
    final roomId = _roomId;
    if (roomId == null) return;

    String? newestId;
    for (final m in state.messages) {
      if (m.status == ChatSendStatus.sent && !m.id.startsWith('local:')) newestId = m.id;
    }
    if (newestId == null || newestId == _ackedDeliveredId) return;

    _ackedDeliveredId = newestId;
    try {
      await _repo.markDelivered(roomId, newestId);
    } catch (_) {
      // Best effort. The pointer only moves forward, so a later call supersedes this one.
    }
  }

  /// D-293 — delete for me. No broadcast follows, so the local list is the only thing to update.
  Future<void> hideMessage(String messageId) async {
    final roomId = _roomId;
    if (roomId == null) return;
    await _repo.hideMessage(roomId, messageId);
    if (mounted) state = state.copyWith(messages: _repo.cachedMessages(roomId));
  }

  /// D-301 - promote or demote a chat Moderator. Refreshes afterwards so the actor's own view of the
  /// room reflects the change at once; every OTHER client is refreshed by the server's `RoomUpdated`
  /// broadcast, which is what makes the new capabilities take effect with no reconnect.
  Future<void> setModerator(String userId, {required bool moderator}) async {
    final roomId = _roomId;
    if (roomId == null) return;
    await _repo.setModerator(roomId, userId, moderator: moderator);
    await refresh();
  }

  /// D-301 - silence one member for [minutes]. Moderator or Host: the server applies the ladder and
  /// refuses a Moderator aiming at a peer or a Host, so this decides what to OFFER, never what is
  /// allowed. Refreshed afterwards for the same reason as [setModerator].
  Future<void> muteMember(String userId, int minutes) async {
    final roomId = _roomId;
    if (roomId == null) return;
    await _repo.muteMember(roomId, userId, minutes);
    await refresh();
  }

  /// D-301 - remove a member from the room, or restore them. Removal also evicts their live sockets
  /// server-side, so the change reaches them without waiting for their next request.
  Future<void> setBanned(String userId, {required bool banned}) async {
    final roomId = _roomId;
    if (roomId == null) return;
    await _repo.setBanned(roomId, userId, banned: banned);
    await refresh();
  }

  /// Room settings: who may post, and whether the room is open. **Hosts only** (`canManageRoom`) —
  /// deliberately not implied by `canModerate`, because a Moderator moderates people, not the room.
  Future<void> updateRoom({String? postPolicy, String? status}) async {
    final roomId = _roomId;
    if (roomId == null) return;
    await _repo.updateRoom(roomId, postPolicy: postPolicy, status: status);
    await refresh();
  }

  Future<void> report(String messageId, String reason) => _repo.report(messageId, reason);

  /// D-296 — pins a message for the whole room. The room is refreshed rather than patched locally
  /// because pinning can also UNPIN the oldest of three, which is a change this client never saw.
  Future<void> pinMessage(String messageId, {required bool pin, int? durationHours}) async {
    await _repo.pinMessage(messageId, pin: pin, durationHours: durationHours);
    await refresh();
  }

  /// D-295 — forwards to another room. Nothing local changes: the copy lands in the TARGET room, and
  /// this room's history is untouched.
  Future<void> forward(String messageId, String targetRoomId) =>
      _repo.forward(messageId, targetRoomId);

  /// Re-reads room state (and therefore capabilities) plus the newest page.
  Future<void> refresh() async {
    try {
      final room = await _fetchRoom();
      if (!mounted) return;
      state = state.copyWith(room: room, clearError: true);
    } catch (_) {
      // Keep showing what we have; the message load below may still succeed.
    }
    await _loadLatest();
  }

  /// Advances the read pointer to the newest confirmed message. Local write is synchronous so the
  /// badge clears at once; the server call is best-effort and forward-only.
  void _markReadToNewest() {
    final roomId = _roomId;
    if (roomId == null) return;
    final confirmed = state.messages.where((m) => m.status == ChatSendStatus.sent);
    if (confirmed.isEmpty) return;
    final newest = confirmed.last.id;
    if (_repo.localReadPointer(roomId) == newest) return;
    unawaited(_repo.markRead(roomId, newest));
  }

  @override
  void dispose() {
    _events?.cancel();
    _reconnects?.cancel();
    _disconnects?.cancel();
    _typingPrune?.cancel();
    _heartbeat?.cancel();
    _typingStopDebounce?.cancel();
    unawaited(_hub?.dispose());
    super.dispose();
  }
}

final chatRoomControllerProvider = StateNotifierProvider.autoDispose
    .family<ChatRoomController, ChatRoomState, String>(
  (ref, routeId) => ChatRoomController(ref, routeId),
);
