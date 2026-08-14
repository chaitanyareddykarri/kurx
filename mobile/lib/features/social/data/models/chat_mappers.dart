import '../../domain/entities/chat_attachment.dart';
import '../../domain/entities/chat_message.dart';
import '../../domain/entities/chat_room.dart';

/// JSON ↔ entity mapping for the frozen chat contract (D-104).
///
/// Deliberately plain rather than freezed, unlike the events DTOs: these types are mapping-only —
/// nothing needs `copyWith`, equality or unions on the wire shape, so generating code for them would
/// add committed `.g.dart`/`.freezed.dart` files for no benefit.
///
/// Keys are read through [_k], which accepts **either** spelling — snake_case first, camelCase second.
/// That is not indecision, it is the migration:
///
/// * The wire moved to snake_case when the API stopped letting ASP.NET's default naming policy show
///   through on record responses (D-259 addendum). Chat was one of the endpoints affected.
/// * The **local outbox** is the other reader of these same parsers. [messageToJson] /
///   [attachmentToJson] have been persisting camelCase to devices for as long as the feature has
///   shipped, so a hard switch would have made every queued message and pending attachment already on
///   a user's phone unreadable — silent data loss on upgrade, in the one place the app promises not to
///   lose anything.
///
/// Accepting both costs one null-coalesce per field and needs no migration step. The camelCase fallback
/// can be dropped once no supported build still has a pre-migration cache.
///
/// Every parser is tolerant: an unexpected or missing field yields a usable object rather than an
/// exception. A malformed message must not be able to take down a whole room's history.
class ChatMappers {
  /// Snake_case (current wire) first, camelCase (legacy wire, and the on-device cache) second.
  static Object? _k(Map<String, dynamic> json, String snake, String camel) =>
      json[snake] ?? json[camel];

  static DateTime _date(Object? v) =>
      v is String ? (DateTime.tryParse(v)?.toUtc() ?? DateTime.now().toUtc()) : DateTime.now().toUtc();

  static DateTime? _dateOrNull(Object? v) =>
      v is String ? DateTime.tryParse(v)?.toUtc() : null;

  static bool _bool(Object? v) => v == true;
  static int _int(Object? v) => v is int ? v : int.tryParse('${v ?? ''}') ?? 0;
  static String? _str(Object? v) => v == null ? null : '$v';

  static ChatCapabilities capabilities(Map<String, dynamic>? json) {
    if (json == null) return const ChatCapabilities.none();
    return ChatCapabilities(
      canPost: _bool(_k(json, 'can_post', 'canPost')),
      canReply: _bool(_k(json, 'can_reply', 'canReply')),
      canUpload: _bool(_k(json, 'can_upload', 'canUpload')),
      canPin: _bool(_k(json, 'can_pin', 'canPin')),
      canDelete: _bool(_k(json, 'can_delete', 'canDelete')),
      canModerate: _bool(_k(json, 'can_moderate', 'canModerate')),
      canMentionAll: _bool(_k(json, 'can_mention_all', 'canMentionAll')),
      // D-301. Absent on an older server, which then reads as "no host powers" — the safe direction.
      canManageRoom: _bool(_k(json, 'can_manage_room', 'canManageRoom')),
      canManageModerators: _bool(_k(json, 'can_manage_moderators', 'canManageModerators')),
      myRole: _str(_k(json, 'my_role', 'myRole')) ?? 'Member',
    );
  }

  static ChatAttachment attachment(Map<String, dynamic> json) => ChatAttachment(
        id: '${json['id']}',
        fileName: _str(_k(json, 'file_name', 'fileName')) ?? 'file',
        contentType: _str(_k(json, 'content_type', 'contentType')) ?? 'application/octet-stream',
        sizeBytes: _int(_k(json, 'size_bytes', 'sizeBytes')),
        url: _str(json['url']),
        width: json['width'] is int ? json['width'] as int : null,
        height: json['height'] is int ? json['height'] as int : null,
      );

  /// Persisted without [ChatAttachment.url]: signed URLs are short-lived, and a cached one would
  /// outlive its signature and fail confusingly. They are re-fetched on demand instead.
  ///
  /// Upload status IS persisted. Without it a pending attachment read back from the outbox would
  /// look confirmed, and the message would be sent referencing an id the server never issued.
  static Map<String, dynamic> attachmentToJson(ChatAttachment a) => {
        'id': a.id,
        'fileName': a.fileName,
        'contentType': a.contentType,
        'sizeBytes': a.sizeBytes,
        'width': a.width,
        'height': a.height,
        'status': a.status.name,
        'localPath': a.localPath,
        'error': a.error,
      };

  /// Inverse of [attachmentToJson]. An unknown status falls back to `uploaded` only for rows that
  /// carry a server-shaped id; anything else stays pending so it can never be sent by mistake.
  static ChatAttachment attachmentFromCache(Map<String, dynamic> json) {
    final base = attachment(json);
    final status = AttachmentUploadStatus.values.firstWhere(
      (s) => s.name == json['status'],
      orElse: () => AttachmentUploadStatus.uploaded,
    );
    return ChatAttachment(
      id: base.id,
      fileName: base.fileName,
      contentType: base.contentType,
      sizeBytes: base.sizeBytes,
      width: base.width,
      height: base.height,
      localPath: _str(_k(json, 'local_path', 'localPath')),
      status: status,
      error: _str(json['error']),
    );
  }

  static ChatMessage message(Map<String, dynamic> json) => ChatMessage(
        id: '${json['id']}',
        roomId: '${_k(json, 'room_id', 'roomId')}',
        clientMessageId: _str(_k(json, 'client_message_id', 'clientMessageId')),
        senderId: _str(_k(json, 'sender_id', 'senderId')),
        senderName: _str(_k(json, 'sender_name', 'senderName')),
        senderRole: _str(_k(json, 'sender_role', 'senderRole')),
        kind: _str(json['kind']) ?? 'Text',
        body: _str(json['body']) ?? '',
        replyToMessageId: _str(_k(json, 'reply_to_message_id', 'replyToMessageId')),
        isPinned: _bool(_k(json, 'is_pinned', 'isPinned')),
        isDeleted: _bool(_k(json, 'is_deleted', 'isDeleted')),
        createdAt: _date(_k(json, 'created_at', 'createdAt')),
        editedAt: _dateOrNull(_k(json, 'edited_at', 'editedAt')),
        pinnedUntil: _dateOrNull(_k(json, 'pinned_until', 'pinnedUntil')),
        reactions: (_k(json, 'reactions', 'reactions') as List? ?? const [])
            .whereType<Map>()
            .map((r) => reaction(Map<String, dynamic>.from(r)))
            .toList(),
        forwardedFrom: forwardSource(
            (_k(json, 'forwarded_from', 'forwardedFrom') as Map?)?.cast<String, dynamic>()),
        linkPreview: linkPreview(
            (_k(json, 'link_preview', 'linkPreview') as Map?)?.cast<String, dynamic>()),
        attachments: (json['attachments'] as List? ?? const [])
            .whereType<Map>()
            .map((a) => attachment(Map<String, dynamic>.from(a)))
            .toList(),
      );

  static Map<String, dynamic> messageToJson(ChatMessage m) => {
        'id': m.id,
        'roomId': m.roomId,
        'clientMessageId': m.clientMessageId,
        'senderId': m.senderId,
        'senderName': m.senderName,
        'senderRole': m.senderRole,
        'kind': m.kind,
        'body': m.body,
        'replyToMessageId': m.replyToMessageId,
        'isPinned': m.isPinned,
        'isDeleted': m.isDeleted,
        'createdAt': m.createdAt.toIso8601String(),
        'editedAt': m.editedAt?.toIso8601String(),
        'pinnedUntil': m.pinnedUntil?.toIso8601String(),
        // Persisted so a pending/failed send survives an app restart and can be retried.
        'status': m.status.name,
        'attachments': m.attachments.map(attachmentToJson).toList(),
      };

  /// Inverse of [messageToJson]. Unknown status values fall back to `sent`, so a cache written by a
  /// newer build cannot strand a message in an unrenderable state.
  static ChatMessage messageFromCache(Map<String, dynamic> json) {
    final base = message(json);
    final status = ChatSendStatus.values.firstWhere(
      (s) => s.name == json['status'],
      orElse: () => ChatSendStatus.sent,
    );
    return base.copyWith(
      status: status,
      attachments: (json['attachments'] as List? ?? const [])
          .whereType<Map>()
          .map((a) => attachmentFromCache(Map<String, dynamic>.from(a)))
          .toList(),
    );
  }

  static ChatReaction reaction(Map<String, dynamic> json) => ChatReaction(
        emoji: _str(json['emoji']) ?? '',
        count: _int(json['count']),
        mine: _bool(json['mine']),
        userIds: (_k(json, 'user_ids', 'userIds') as List? ?? const []).map((v) => '$v').toList(),
      );

  static ChatForwardSource? forwardSource(Map<String, dynamic>? json) {
    if (json == null) return null;
    final id = _str(_k(json, 'message_id', 'messageId'));
    if (id == null) return null;
    return ChatForwardSource(
      messageId: id,
      senderName: _str(_k(json, 'sender_name', 'senderName')),
    );
  }

  static ChatLinkPreview? linkPreview(Map<String, dynamic>? json) {
    if (json == null) return null;
    final url = _str(json['url']);
    final host = _str(json['host']);
    // Both are required for a card to mean anything: without the host there is nothing honest to
    // show about the destination, which is the whole reason the card is safe to render at all.
    if (url == null || host == null) return null;
    return ChatLinkPreview(
      url: url,
      host: host,
      title: _str(json['title']),
      description: _str(json['description']),
      imageUrl: _str(_k(json, 'image_url', 'imageUrl')),
    );
  }

  static ChatSearchHit searchHit(Map<String, dynamic> json) => ChatSearchHit(
        messageId: '${_k(json, 'message_id', 'messageId')}',
        roomId: '${_k(json, 'room_id', 'roomId')}',
        roomLabel: _str(_k(json, 'room_label', 'roomLabel')) ?? 'Conversation',
        body: _str(json['body']) ?? '',
        createdAt: _date(_k(json, 'created_at', 'createdAt')),
        senderId: _str(_k(json, 'sender_id', 'senderId')),
        senderName: _str(_k(json, 'sender_name', 'senderName')),
      );

  static ChatMessagePage page(Map<String, dynamic> json) => ChatMessagePage(
        messages: (json['messages'] as List? ?? const [])
            .whereType<Map>()
            .map((m) => message(Map<String, dynamic>.from(m)))
            .toList(),
        olderCursor: _str(_k(json, 'older_cursor', 'olderCursor')),
        newerCursor: _str(_k(json, 'newer_cursor', 'newerCursor')),
      );

  static ChatRoom room(Map<String, dynamic> json) => ChatRoom(
        roomId: '${_k(json, 'room_id', 'roomId')}',
        kind: _str(json['kind']) ?? 'General',
        status: _str(json['status']) ?? 'Active',
        postPolicy: _str(_k(json, 'post_policy', 'postPolicy')) ?? 'Everyone',
        myRole: _str(_k(json, 'my_role', 'myRole')) ?? 'Member',
        unreadCount: _int(_k(json, 'unread_count', 'unreadCount')),
        capabilities: capabilities(
            (json['capabilities'] as Map?)?.cast<String, dynamic>()),
        mutedUntil: _dateOrNull(_k(json, 'muted_until', 'mutedUntil')),
        pinnedMessages: (_k(json, 'pinned_messages', 'pinnedMessages') as List? ?? const [])
            .whereType<Map>()
            .map((m) => message(Map<String, dynamic>.from(m)))
            .toList(),
        onlineUserIds: (_k(json, 'online_user_ids', 'onlineUserIds') as List? ?? const [])
            .map((id) => '$id')
            .toList(),
        presenceEnabled: _bool(_k(json, 'presence_enabled', 'presenceEnabled')),
        dmRequestState: _str(_k(json, 'dm_request_state', 'dmRequestState')),
        dmInitiatedBy: _str(_k(json, 'dm_initiated_by', 'dmInitiatedBy')),
      );

  static MyChat myChat(Map<String, dynamic> json) => MyChat(
        roomId: '${_k(json, 'room_id', 'roomId')}',
        eventId: '${_k(json, 'event_id', 'eventId')}',
        eventTitle: _str(_k(json, 'event_title', 'eventTitle')) ?? '',
        unreadCount: _int(_k(json, 'unread_count', 'unreadCount')),
        bannerUrl: _str(_k(json, 'banner_url', 'bannerUrl')),
        lastMessagePreview: _str(_k(json, 'last_message_preview', 'lastMessagePreview')),
        lastActivity: _dateOrNull(_k(json, 'last_activity', 'lastActivity')),
        pinned: _bool(_k(json, 'pinned', 'pinned')),
        notificationsMuted: _bool(_k(json, 'notifications_muted', 'notificationsMuted')),
        archived: _bool(_k(json, 'archived', 'archived')),   // D-306
      );

  static Map<String, dynamic> myChatToJson(MyChat c) => {
        'roomId': c.roomId,
        'eventId': c.eventId,
        'eventTitle': c.eventTitle,
        'unreadCount': c.unreadCount,
        'bannerUrl': c.bannerUrl,
        'lastMessagePreview': c.lastMessagePreview,
        'lastActivity': c.lastActivity?.toIso8601String(),
        // Cached too, so an offline list still shows the pin and the mute it showed last time.
        'pinned': c.pinned,
        'notificationsMuted': c.notificationsMuted,
      };
}
