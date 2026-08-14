import 'dart:math';

import 'package:dio/dio.dart';

import '../../../../core/network/api_error.dart';
import '../../../../core/storage/chat_cache.dart';
import '../../domain/entities/chat_attachment.dart';
import '../../domain/entities/chat_message.dart';
import '../../domain/entities/chat_room.dart';
import '../../domain/repositories/chat_repository.dart';
import '../datasources/chat_remote_data_source.dart';

/// Cache-first chat repository.
///
/// The offline contract it implements comes straight from the frozen backend (D-104):
///   * ordering is `(createdAt, id)`, never `createdAt` alone;
///   * `clientMessageId` reconciles an optimistic row with the server's copy;
///   * `?after=` is the delta primitive used after a reconnect.
///
/// Network failure is never fatal on a read path: `ApiError.status == 0` (offline/timeout) falls
/// back to cache, matching the idiom `EventsRepositoryImpl` already established.
class ChatRepositoryImpl implements ChatRepository {
  ChatRepositoryImpl(this._remote, this._cache);

  final ChatRemoteDataSource _remote;
  final ChatCache _cache;

  static final _random = Random();

  /// RFC 4122 v4, formatted as the server expects a Guid. Generated at compose time — before the
  /// first network attempt — which is what makes a retry idempotent rather than duplicating.
  static String newClientMessageId() {
    final bytes = List<int>.generate(16, (_) => _random.nextInt(256));
    bytes[6] = (bytes[6] & 0x0f) | 0x40;
    bytes[8] = (bytes[8] & 0x3f) | 0x80;
    final hex = bytes.map((b) => b.toRadixString(16).padLeft(2, '0')).join();
    return '${hex.substring(0, 8)}-${hex.substring(8, 12)}-${hex.substring(12, 16)}'
        '-${hex.substring(16, 20)}-${hex.substring(20)}';
  }

  static bool _isOffline(ApiError e) => e.status == 0;

  @override
  Future<List<MyChat>> myChats({bool archived = false}) async {
    try {
      final rooms = await _remote.myChats(archived: archived);
      // Only the ACTIVE list is cached (D-306). The cache backs the offline inbox, and writing the
      // archived side to the same key would make filed-away rooms reappear there — the exact opposite
      // of what archiving them meant.
      if (!archived) await _cache.saveRooms(rooms);
      return rooms;
    } on ApiError catch (e) {
      // Same reason in reverse: there is no cached archived list, so an offline request for one has
      // nothing honest to return and the error stands rather than showing the active list under an
      // "Archived" heading.
      if (_isOffline(e) && !archived) return _cache.readRooms();
      rethrow;
    }
  }

  @override
  Future<ChatRoom> roomById(String roomId) => _remote.roomById(roomId);

  @override
  Future<ChatRoom> room(String eventId) => _remote.room(eventId);

  @override
  List<ChatMessage> cachedMessages(String roomId) =>
      ChatCache.mergeInto(_cache.readMessages(roomId), _cache.readOutbox(roomId));

  @override
  Future<List<ChatMessage>> latest(String roomId) async {
    try {
      final page = await _remote.messages(roomId, limit: 50);
      final merged = await _cache.mergeMessages(roomId, page.messages);
      // Outbox entries are merged for display but never persisted into the confirmed window —
      // they are not server truth until they come back with an id.
      return ChatCache.mergeInto(merged, _cache.readOutbox(roomId));
    } on ApiError catch (e) {
      if (_isOffline(e)) return cachedMessages(roomId);
      rethrow;
    }
  }

  @override
  Future<List<ChatMessage>> older(String roomId, String beforeCursor) async {
    final page = await _remote.messages(roomId, before: beforeCursor, limit: 50);
    if (page.messages.isEmpty) return const [];
    await _cache.mergeMessages(roomId, page.messages);
    return page.messages;
  }

  @override
  Future<List<ChatMessage>> since(String roomId, String afterCursor) async {
    final page = await _remote.messages(roomId, after: afterCursor, limit: 50);
    if (page.messages.isEmpty) return const [];
    await _cache.mergeMessages(roomId, page.messages);
    return page.messages;
  }

  @override
  Future<ChatAttachment> uploadAttachment(
    String roomId,
    ChatAttachment pending,
    List<int> bytes, {
    void Function(double progress)? onProgress,
    CancelToken? cancelToken,
  }) async {
    // The three steps run in this order, always. Confirm is where the server validates what
    // actually arrived, so skipping it would mean attaching bytes nobody checked.
    final ticket = await _remote.presignAttachment(
      roomId,
      fileName: pending.fileName,
      contentType: pending.contentType,
      sizeBytes: pending.sizeBytes,
    );

    await _remote.uploadBytes(
      ticket.url,
      bytes,
      headers: ticket.headers,
      onProgress: (sent, total) => onProgress?.call(total <= 0 ? 0 : sent / total),
      cancelToken: cancelToken,
    );

    return _remote.confirmAttachment(roomId, ticket.key);
  }

  @override
  Future<String> attachmentUrl(String attachmentId) => _remote.attachmentUrl(attachmentId);

  @override
  Future<ChatMessage> queueSend(String roomId, String body,
      {String? replyToMessageId, List<ChatAttachment> attachments = const []}) async {
    final clientMessageId = newClientMessageId();
    final optimistic = ChatMessage(
      // Temporary id, replaced by the server's UUIDv7 on confirmation. Prefixed so it can never be
      // mistaken for — or collide with — a real id if it reaches the cache.
      id: 'local:$clientMessageId',
      roomId: roomId,
      clientMessageId: clientMessageId,
      body: body,
      replyToMessageId: replyToMessageId,
      createdAt: DateTime.now().toUtc(),
      status: ChatSendStatus.pending,
      attachments: attachments,
    );
    await _cache.addToOutbox(roomId, optimistic);
    return optimistic;
  }

  @override
  Future<List<ChatMessage>> pending(String roomId) async => _cache.readOutbox(roomId);

  @override
  Future<List<ChatMessage>> flushOutbox(String roomId) async {
    final queued = _cache.readOutbox(roomId);
    if (queued.isEmpty) return const [];

    final confirmed = <ChatMessage>[];
    for (final message in queued) {
      if (message.status == ChatSendStatus.failed) continue;   // needs an explicit retry
      final clientMessageId = message.clientMessageId;
      if (clientMessageId == null) continue;

      try {
        // Only confirmed attachments can be referenced — anything still pending or failed has no
        // server id, so the message waits rather than being sent without its files.
        final attachmentIds = message.attachments
            .where((a) => a.status == AttachmentUploadStatus.uploaded)
            .map((a) => a.id)
            .toList();
        if (attachmentIds.length != message.attachments.length) continue;

        final sent = await _remote.send(
          roomId,
          body: message.body,
          clientMessageId: clientMessageId,
          replyToMessageId: message.replyToMessageId,
          attachmentIds: attachmentIds.isEmpty ? null : attachmentIds,
        );
        await _cache.removeFromOutbox(roomId, clientMessageId);
        await _cache.mergeMessages(roomId, [sent]);
        confirmed.add(sent);
      } on ApiError catch (e) {
        if (_isOffline(e)) break;   // still offline: leave the rest queued, try again later

        // A definite refusal (muted, locked, banned, too long). Retrying unchanged cannot succeed,
        // so mark it failed and let the user decide — silently dropping their text is worse.
        await _markFailed(roomId, clientMessageId);
      }
    }
    return confirmed;
  }

  Future<void> _markFailed(String roomId, String clientMessageId) async {
    final queued = _cache.readOutbox(roomId);
    final updated = queued
        .map((m) => m.clientMessageId == clientMessageId
            ? m.copyWith(status: ChatSendStatus.failed)
            : m)
        .toList();
    await _cache.saveOutbox(roomId, updated);
  }

  @override
  Future<void> retry(String roomId, String clientMessageId) async {
    final queued = _cache.readOutbox(roomId);
    final updated = queued
        .map((m) => m.clientMessageId == clientMessageId
            ? m.copyWith(status: ChatSendStatus.pending)
            : m)
        .toList();
    await _cache.saveOutbox(roomId, updated);
  }

  @override
  Future<void> discard(String roomId, String clientMessageId) =>
      _cache.removeFromOutbox(roomId, clientMessageId);

  @override
  Future<void> markRead(String roomId, String lastReadMessageId) async {
    if (lastReadMessageId.startsWith('local:')) return;   // never sync an unconfirmed id
    await _cache.savePointer(roomId, lastReadMessageId);
    try {
      await _remote.markRead(roomId, lastReadMessageId);
    } on ApiError {
      // Best effort. The pointer only moves forward server-side, so a later successful call with a
      // newer id supersedes this one — there is nothing to reconcile and nothing to retry.
    }
  }

  @override
  String? localReadPointer(String roomId) => _cache.readPointer(roomId);

  /// D-293. The server's copy replaces the cached one wholesale rather than being patched locally:
  /// it carries the authoritative body and EditedAt, so there is nothing to re-derive here.
  @override
  Future<ChatMessage> editMessage(String roomId, String messageId, String body) async {
    final edited = await _remote.editMessage(messageId, body);
    final updated = _cache
        .readMessages(roomId)
        .map((m) => m.id == messageId ? edited : m)
        .toList();
    await _cache.clearMessages(roomId);
    await _cache.mergeMessages(roomId, updated);
    return edited;
  }

  @override
  Future<void> deleteMessage(String roomId, String messageId) async {
    await _remote.deleteMessage(messageId);
    final updated = _cache
        .readMessages(roomId)
        .map((m) => m.id == messageId ? m.copyWith(isDeleted: true, body: '') : m)
        .toList();
    await _cache.clearMessages(roomId);
    await _cache.mergeMessages(roomId, updated);
  }

  /// D-293 — delete for me. Dropped from the local cache entirely rather than tombstoned: the server
  /// will not send it again, so a tombstone would be a permanent gap in this reader's history for a
  /// message they chose to remove.
  @override
  Future<void> hideMessage(String roomId, String messageId) async {
    await _remote.hideMessage(messageId);
    final remaining = _cache.readMessages(roomId).where((m) => m.id != messageId).toList();
    await _cache.clearMessages(roomId);
    await _cache.mergeMessages(roomId, remaining);
  }

  // ── D-295 ───────────────────────────────────────────────────────────────────

  /// The server's summary replaces the cached message's reactions wholesale — it is authoritative,
  /// and merging a delta locally would let two devices drift on the same message.
  @override
  Future<List<ChatReaction>> toggleReaction(String roomId, String messageId, String emoji) async {
    final reactions = await _remote.toggleReaction(messageId, emoji);
    final updated = _cache
        .readMessages(roomId)
        .map((m) => m.id == messageId ? m.copyWith(reactions: reactions) : m)
        .toList();
    await _cache.clearMessages(roomId);
    await _cache.mergeMessages(roomId, updated);
    return reactions;
  }

  /// Not cached: results are a view over other rooms' messages, and persisting them here would put
  /// a copy of one room's history into another room's cache.
  @override
  Future<List<ChatSearchHit>> search(String query, {String? roomId}) =>
      _remote.search(query, roomId: roomId);

  @override
  Future<void> markDelivered(String roomId, String messageId) =>
      _remote.markDelivered(roomId, messageId);

  @override
  Future<ChatMessage> forward(String messageId, String targetRoomId) =>
      _remote.forward(messageId, targetRoomId);

  @override
  Future<void> setPinned(String roomId, {required bool pinned}) =>
      _remote.setPinned(roomId, pinned: pinned);

  @override
  Future<void> pinMessage(String messageId, {required bool pin, int? durationHours}) =>
      _remote.pinMessage(messageId, pin: pin, durationHours: durationHours);

  @override
  Future<void> setModerator(String roomId, String userId, {required bool moderator}) =>
      _remote.setModerator(roomId, userId, moderator: moderator);

  @override
  Future<void> setMuted(String roomId, DateTime? until) => _remote.setMuted(roomId, until);

  @override
  Future<void> setArchived(String roomId, {required bool archived}) =>
      _remote.setArchived(roomId, archived: archived);

  @override
  Future<void> updateRoom(String roomId, {String? postPolicy, String? status}) =>
      _remote.updateRoom(roomId, postPolicy: postPolicy, status: status);

  @override
  Future<void> muteMember(String roomId, String userId, int minutes) =>
      _remote.muteMember(roomId, userId, minutes);

  @override
  Future<void> setBanned(String roomId, String userId, {required bool banned}) =>
      _remote.setBanned(roomId, userId, banned: banned);

  @override
  Future<List<ChatAttachment>> roomMedia(String roomId) => _remote.roomMedia(roomId);

  @override
  Future<void> report(String messageId, String reason) => _remote.report(messageId, reason);
}
