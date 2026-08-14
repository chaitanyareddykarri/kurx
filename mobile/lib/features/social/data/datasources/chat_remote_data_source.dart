import 'package:dio/dio.dart';

import '../../../../core/network/api_guard.dart';
import '../../domain/entities/chat_attachment.dart';
import '../../domain/entities/chat_message.dart';
import '../../domain/entities/chat_room.dart';
import '../models/chat_mappers.dart';

/// REST access to the frozen chat contract (D-104). One method per endpoint, no extra shaping —
/// business decisions live in the repository, not here.
class ChatRemoteDataSource {
  /// [uploadClient] is a SEPARATE Dio on purpose: a presigned URL is absolute and carries its own
  /// signature, so it must not receive the app client's base URL or Authorization header — with real
  /// object storage an unexpected header can invalidate the signature. Injectable so tests can drive
  /// the upload without a live socket.
  ChatRemoteDataSource(this._dio, {Dio? uploadClient}) : _upload = uploadClient ?? Dio();

  final Dio _dio;
  final Dio _upload;

  /// D-306 — [archived] selects which side of this reader's filing to return, mirroring
  /// `/v1/me/dm?archived=`. Without it there was no way to see an archived event room again.
  Future<List<MyChat>> myChats({bool archived = false}) => guard(() async {
        final res = await _dio.get('/v1/me/chats', queryParameters: {'archived': archived});
        return (res.data as List? ?? const [])
            .whereType<Map>()
            .map((e) => ChatMappers.myChat(Map<String, dynamic>.from(e)))
            .toList();
      });

  /// One room by its OWN id (D-292) — the navigation primitive. A direct room has no event, so it
  /// is reachable no other way; event rooms resolve through here too, so there is one lookup rather
  /// than two that can drift.
  Future<ChatRoom> roomById(String roomId) => guard(() async {
        final res = await _dio.get('/v1/chat/rooms/$roomId');
        return ChatMappers.room(Map<String, dynamic>.from(res.data as Map));
      });

  /// Event-addressed lookup, kept for entry points that hold an eventId and no room id — the
  /// workspace tile and any deep link minted before the route was re-keyed. Prefer [roomById].
  Future<ChatRoom> room(String eventId) => guard(() async {
        final res = await _dio.get('/v1/events/$eventId/chat');
        return ChatMappers.room(Map<String, dynamic>.from(res.data as Map));
      });

  /// Keyset pagination. At most one of [before]/[after] may be supplied — the server rejects both
  /// with `cursor_conflict`. Cursors are opaque and are passed straight back as received.
  Future<ChatMessagePage> messages(
    String roomId, {
    String? before,
    String? after,
    int limit = 50,
  }) =>
      guard(() async {
        final res = await _dio.get(
          '/v1/chat/rooms/$roomId/messages',
          queryParameters: {
            'before': ?before,
            'after': ?after,
            'limit': limit,
          },
        );
        return ChatMappers.page(Map<String, dynamic>.from(res.data as Map));
      });

  /// Idempotent on [clientMessageId]: a retry returns the message the first attempt created rather
  /// than a duplicate, which is what makes the outbox safe to flush more than once.
  Future<ChatMessage> send(
    String roomId, {
    required String body,
    required String clientMessageId,
    String? replyToMessageId,
    List<String>? attachmentIds,
  }) =>
      guard(() async {
        final res = await _dio.post(
          '/v1/chat/rooms/$roomId/messages',
          data: {
            'body': body,
            'clientMessageId': clientMessageId,
            'replyToMessageId': ?replyToMessageId,
            if (attachmentIds != null && attachmentIds.isNotEmpty) 'attachmentIds': attachmentIds,
          },
        );
        return ChatMappers.message(Map<String, dynamic>.from(res.data as Map));
      });

  // ── Attachments (D-110) ─────────────────────────────────────────────────────────────────
  // Three steps, in this order, always: presign reserves a key, the bytes go to the URL the
  // server handed back, and confirm is where the server validates what actually arrived.

  Future<({String key, String url, Map<String, String> headers})> presignAttachment(
    String roomId, {
    required String fileName,
    required String contentType,
    required int sizeBytes,
  }) =>
      guard(() async {
        final res = await _dio.post(
          '/v1/chat/rooms/$roomId/attachments/presign',
          data: {'fileName': fileName, 'contentType': contentType, 'sizeBytes': sizeBytes},
        );
        final map = Map<String, dynamic>.from(res.data as Map);
        return (
          key: '${map['key']}',
          url: '${map['url']}',
          headers: Map<String, String>.from(
              (map['headers'] as Map?)?.cast<String, dynamic>().map((k, v) => MapEntry(k, '$v')) ?? {}),
        );
      });

  /// PUTs the bytes to the presigned URL.
  ///
  Future<void> uploadBytes(
    String url,
    List<int> bytes, {
    required Map<String, String> headers,
    void Function(int sent, int total)? onProgress,
    CancelToken? cancelToken,
  }) =>
      guard(() async {
        await _upload.put(
          url,
          data: Stream.fromIterable([bytes]),
          options: Options(
            headers: {...headers, Headers.contentLengthHeader: bytes.length},
            contentType: headers['Content-Type'],
          ),
          onSendProgress: onProgress,
          cancelToken: cancelToken,
        );
      });

  Future<ChatAttachment> confirmAttachment(String roomId, String storageKey) => guard(() async {
        final res = await _dio.post(
          '/v1/chat/rooms/$roomId/attachments/confirm',
          data: {'storageKey': storageKey},
        );
        return ChatMappers.attachment(Map<String, dynamic>.from(res.data as Map));
      });

  /// A fresh signed URL. Always called at the moment of use — never cached, because the signature
  /// expires and membership is re-checked server-side on every mint.
  Future<String> attachmentUrl(String attachmentId) => guard(() async {
        final res = await _dio.get('/v1/chat/attachments/$attachmentId/url');
        return '${(res.data as Map)['url']}';
      });

  /// The read pointer is a message id, not a timestamp, and the server only ever moves it forward.
  Future<void> markRead(String roomId, String lastReadMessageId) => guard(() async {
        await _dio.post(
          '/v1/chat/rooms/$roomId/read',
          data: {'lastReadMessageId': lastReadMessageId},
        );
      });

  /// D-293 — edit your own message. The server refuses a no-op with 409 and anything outside the
  /// edit window with 403, so nothing here needs to pre-judge either.
  Future<ChatMessage> editMessage(String messageId, String body) => guard(() async {
        final res = await _dio.patch('/v1/chat/messages/$messageId', data: {'body': body});
        return ChatMappers.message(Map<String, dynamic>.from(res.data as Map));
      });

  /// Delete for EVERYONE — redacts the message for the whole room.
  Future<void> deleteMessage(String messageId) =>
      guard(() async => _dio.delete('/v1/chat/messages/$messageId'));

  /// D-293 — delete for ME. Hides it from this reader only; idempotent server-side.
  Future<void> hideMessage(String messageId) =>
      guard(() async => _dio.delete('/v1/chat/messages/$messageId/for-me'));

  // ── D-295 ───────────────────────────────────────────────────────────────────

  /// One call, not add/remove: the server decides whether a tap means on or off, so two devices can
  /// never disagree about which to send. Returns the message's whole reaction summary.
  Future<List<ChatReaction>> toggleReaction(String messageId, String emoji) => guard(() async {
        final res = await _dio.post(
          '/v1/chat/messages/$messageId/reactions',
          data: {'emoji': emoji},
        );
        return (res.data as List? ?? const [])
            .whereType<Map>()
            .map((r) => ChatMappers.reaction(Map<String, dynamic>.from(r)))
            .toList();
      });

  /// Scoped server-side to the rooms the caller belongs to; [roomId] narrows it to one conversation.
  Future<List<ChatSearchHit>> search(String query, {String? roomId, int limit = 25}) =>
      guard(() async {
        final res = await _dio.get('/v1/chat/search', queryParameters: {
          'q': query,
          'roomId': ?roomId,
          'limit': limit,
        });
        return (res.data as List? ?? const [])
            .whereType<Map>()
            .map((h) => ChatMappers.searchHit(Map<String, dynamic>.from(h)))
            .toList();
      });

  /// The delivered receipt. Forward-only server-side, so a repeat is a no-op.
  Future<void> markDelivered(String roomId, String messageId) => guard(() async {
        await _dio.post('/v1/chat/rooms/$roomId/delivered', data: {'messageId': messageId});
      });

  Future<ChatMessage> forward(String messageId, String targetRoomId, {String? clientMessageId}) =>
      guard(() async {
        final res = await _dio.post('/v1/chat/messages/$messageId/forward', data: {
          'targetRoomId': targetRoomId,
          'clientMessageId': ?clientMessageId,
        });
        return ChatMappers.message(Map<String, dynamic>.from(res.data as Map));
      });

  Future<void> setPinned(String roomId, {required bool pinned}) => guard(() async {
        if (pinned) {
          await _dio.post('/v1/chat/rooms/$roomId/pin');
        } else {
          await _dio.delete('/v1/chat/rooms/$roomId/pin');
        }
      });

  Future<void> setMuted(String roomId, DateTime? until) => guard(() async {
        if (until != null) {
          await _dio.post('/v1/chat/rooms/$roomId/mute',
              data: {'until': until.toUtc().toIso8601String()});
        } else {
          await _dio.delete('/v1/chat/rooms/$roomId/mute');
        }
      });


  /// Shared media: every live attachment in the room, newest first.
  Future<List<ChatAttachment>> roomMedia(String roomId, {int limit = 60}) => guard(() async {
        final res = await _dio.get('/v1/chat/rooms/$roomId/media',
            queryParameters: {'limit': limit});
        return (res.data as List? ?? const [])
            .whereType<Map>()
            .map((a) => ChatMappers.attachment(Map<String, dynamic>.from(a)))
            .toList();
      });

  /// D-296 - pins a message for the whole room (hosts only). [durationHours] is how long the pin
  /// lasts; omitted takes the server's 7-day default. Distinct from [setPinned], which files a
  /// CONVERSATION in this reader's own list and is visible to nobody else.
  Future<void> pinMessage(String messageId, {required bool pin, int? durationHours}) =>
      guard(() async {
        if (pin) {
          await _dio.post('/v1/chat/messages/$messageId/pin',
              data: {'durationHours': durationHours});
        } else {
          await _dio.delete('/v1/chat/messages/$messageId/pin');
        }
      });

  /// D-301 - promotes a room MEMBER to chat Moderator, or demotes one. Hosts only; the server refuses
  /// a Moderator attempting either direction, so this is an affordance decision only.
  Future<void> setModerator(String roomId, String userId, {required bool moderator}) =>
      guard(() async {
        final url = '/v1/chat/rooms/$roomId/members/$userId/moderator';
        if (moderator) {
          await _dio.post(url);
        } else {
          await _dio.delete(url);
        }
      });

  /// Files a conversation out of the active list, or brings it back (D-295/D-306).
  ///
  /// The room-scoped route, not `/v1/dm/{id}/archive`: both reach the same
  /// `ChatService.SetArchivedAsync`, but only this one accepts an EVENT room. Safe to offer now that
  /// `myChats(archived: true)` can return what it hides — before D-306 this was a one-way trip.
  Future<void> setArchived(String roomId, {required bool archived}) => guard(() async {
        if (archived) {
          await _dio.post('/v1/chat/rooms/$roomId/archive');
        } else {
          await _dio.delete('/v1/chat/rooms/$roomId/archive');
        }
      });

  /// Room settings: who may post, and whether the room is open at all. **Hosts only** — the capability
  /// is `canManageRoom`, deliberately NOT implied by `canModerate` (D-301): a Moderator moderates
  /// people, never the room itself. Both fields are optional; sending one leaves the other untouched.
  Future<void> updateRoom(String roomId, {String? postPolicy, String? status}) => guard(() async {
        await _dio.patch('/v1/chat/rooms/$roomId', data: {
          'postPolicy': ?postPolicy,
          'status': ?status,
        });
      });

  /// D-301 - silences one member for [minutes]. Moderators and Hosts both reach this; the server
  /// applies the ladder, so a Moderator aiming at a peer or a Host is refused with
  /// `cannot_moderate_peer` regardless of what this client offers.
  Future<void> muteMember(String roomId, String userId, int minutes) => guard(() async {
        await _dio.post('/v1/chat/rooms/$roomId/members/$userId/mute', data: {'minutes': minutes});
      });

  /// Removes a member from the room and evicts their live sockets. Same ladder as [muteMember].
  Future<void> setBanned(String roomId, String userId, {required bool banned}) => guard(() async {
        final url = '/v1/chat/rooms/$roomId/members/$userId/ban';
        if (banned) {
          await _dio.post(url);
        } else {
          await _dio.delete(url);
        }
      });

  Future<void> report(String messageId, String reason) => guard(() async {
        await _dio.post('/v1/chat/messages/$messageId/report', data: {'reason': reason});
      });
}
