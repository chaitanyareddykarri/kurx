import 'package:dio/dio.dart';

import '../entities/chat_attachment.dart';
import '../entities/chat_message.dart';
import '../entities/chat_room.dart';

abstract class ChatRepository {
  /// Rooms the signed-in user belongs to. Falls back to cache when offline.
  /// D-306 — [archived] selects which side of this reader's own filing to return.
  Future<List<MyChat>> myChats({bool archived = false});

  /// One room by its own id (D-292). The canonical lookup — a direct room has no event.
  Future<ChatRoom> roomById(String roomId);

  /// Event-addressed lookup, for entry points that hold only an eventId.
  Future<ChatRoom> room(String eventId);

  /// Newest page, cache-first: cached messages render immediately and the server page is merged in.
  Future<List<ChatMessage>> latest(String roomId);

  /// Older history for infinite scroll. Returns an empty list at the start of the room.
  Future<List<ChatMessage>> older(String roomId, String beforeCursor);

  /// Everything after [afterCursor] — the reconnect/delta path.
  Future<List<ChatMessage>> since(String roomId, String afterCursor);

  /// Queues a message, returning the optimistic row immediately. The caller renders it as pending;
  /// [flushOutbox] confirms or fails it.
  Future<ChatMessage> queueSend(String roomId, String body,
      {String? replyToMessageId, List<ChatAttachment> attachments});

  /// Runs presign → upload → confirm for one file. Progress is reported as 0..1.
  ///
  /// Throws [ApiError] on a server refusal so the caller can surface the exact reason
  /// (`file_too_large`, `unsupported_file_type`, `file_infected`, …) rather than a generic failure.
  Future<ChatAttachment> uploadAttachment(
    String roomId,
    ChatAttachment pending,
    List<int> bytes, {
    void Function(double progress)? onProgress,
    CancelToken? cancelToken,
  });

  /// A fresh signed download URL. Never cached — the signature expires and the server re-checks
  /// room membership on every mint.
  Future<String> attachmentUrl(String attachmentId);

  /// Attempts every queued message for the room. Returns the confirmed messages. Safe to call
  /// repeatedly — sends are idempotent on `clientMessageId`.
  Future<List<ChatMessage>> flushOutbox(String roomId);

  Future<List<ChatMessage>> pending(String roomId);

  /// Re-queues a failed message so the next flush retries it.
  Future<void> retry(String roomId, String clientMessageId);

  Future<void> discard(String roomId, String clientMessageId);

  /// Moves the read pointer. Local first so the badge clears instantly; the server call is
  /// best-effort because the pointer only ever moves forward and re-sends are harmless.
  Future<void> markRead(String roomId, String lastReadMessageId);

  String? localReadPointer(String roomId);

  /// D-293 — edit your own message; returns the server's updated copy.
  Future<ChatMessage> editMessage(String roomId, String messageId, String body);

  /// Delete for everyone.
  Future<void> deleteMessage(String roomId, String messageId);

  /// D-293 — delete for me. Hides it from this reader only.
  Future<void> hideMessage(String roomId, String messageId);

  // ── D-295 ───────────────────────────────────────────────────────────────────

  /// Toggles one emoji; returns the message's whole reaction summary from the server.
  Future<List<ChatReaction>> toggleReaction(String roomId, String messageId, String emoji);

  /// Scoped server-side to the rooms the caller belongs to.
  Future<List<ChatSearchHit>> search(String query, {String? roomId});

  /// Acknowledges receipt. Best-effort and forward-only, so a repeat costs nothing.
  Future<void> markDelivered(String roomId, String messageId);

  Future<ChatMessage> forward(String messageId, String targetRoomId);

  Future<void> setPinned(String roomId, {required bool pinned});

  /// D-296 - pins a MESSAGE for the whole room, for [durationHours] (server default when omitted).
  /// Not to be confused with [setPinned], which files a conversation in this reader's own list.
  Future<void> pinMessage(String messageId, {required bool pin, int? durationHours});

  Future<void> setMuted(String roomId, DateTime? until);

  /// Files a conversation out of the active list, or restores it (D-295/D-306). Works for an event
  /// room as well as a DM — the room-scoped route, which is the half no client called.
  Future<void> setArchived(String roomId, {required bool archived});

  /// Room settings: post policy and lifecycle. **Hosts only** (`canManageRoom`), never a Moderator.
  Future<void> updateRoom(String roomId, {String? postPolicy, String? status});

  Future<List<ChatAttachment>> roomMedia(String roomId);

  /// D-301 - promote a Member to chat Moderator, or demote one. Hosts only.
  Future<void> setModerator(String roomId, String userId, {required bool moderator});

  /// D-301 - silence one member for [minutes]. Moderator or Host; the server applies the ladder.
  Future<void> muteMember(String roomId, String userId, int minutes);

  /// D-301 - remove a member from the room and evict their sockets, or restore them.
  Future<void> setBanned(String roomId, String userId, {required bool banned});

  Future<void> report(String messageId, String reason);

  List<ChatMessage> cachedMessages(String roomId);
}
