import 'chat_attachment.dart';

/// Where a message is in its send lifecycle. Only outgoing messages are ever anything but [sent].
enum ChatSendStatus { sent, pending, failed }

/// A chat message.
///
/// Ordering is always `(createdAt, id)` — never `createdAt` alone. Server ids are UUIDv7 and so are
/// k-sortable, but rows created before D-104 hold v4 ids, which is why the pair is the sort key
/// (the same rule the backend's keyset pagination uses).
class ChatMessage {
  const ChatMessage({
    required this.id,
    required this.roomId,
    required this.body,
    required this.createdAt,
    this.clientMessageId,
    this.senderId,
    this.senderName,
    this.senderRole,
    this.kind = 'Text',
    this.replyToMessageId,
    this.isPinned = false,
    this.isDeleted = false,
    this.status = ChatSendStatus.sent,
    this.attachments = const [],
    this.editedAt,
    this.reactions = const [],
    this.forwardedFrom,
    this.linkPreview,
    this.pinnedUntil,
  });

  final String id;
  final String roomId;
  final String body;
  final DateTime createdAt;

  /// Set on messages this device sent. Survives the round trip, and is what reconciles an optimistic
  /// row with the server's copy — the server treats it as an idempotency key per room (D-104).
  final String? clientMessageId;

  final String? senderId;
  final String? senderName;
  final String? senderRole;
  final String kind;        // Text | System
  final String? replyToMessageId;
  final bool isPinned;
  final bool isDeleted;
  final ChatSendStatus status;

  /// Files on this message. Reserved as an always-empty array by D-104 so filling it in was not a
  /// breaking change; populated for real since D-110.
  final List<ChatAttachment> attachments;

  /// When the sender last edited the body, null if never (D-293). Rendered as an "edited" marker.
  /// NOT part of the sort key: an edit must not move a message in anyone's history.
  final DateTime? editedAt;

  /// D-295 - aggregated per emoji by the server, so the client renders counts without grouping.
  final List<ChatReaction> reactions;

  /// D-295 - set when this message was forwarded. A null sender name means the original is gone or
  /// lives somewhere this reader cannot see, and the card reads simply "Forwarded".
  final ChatForwardSource? forwardedFrom;

  /// D-295 - sender-supplied link card. The server never fetches the URL; it only derives the host,
  /// which is the one part a sender cannot fake and therefore the part worth showing.
  final ChatLinkPreview? linkPreview;

  /// D-296 - when this message's pin lapses, null when it is not pinned. [isPinned] is already
  /// computed against this instant by the server, so this is only ever rendered, never used to decide
  /// whether the pin still holds.
  final DateTime? pinnedUntil;

  bool get isSystem => kind == 'System';
  bool get isHostMessage => senderRole == 'Host';

  /// D-301 — an explicitly promoted chat Moderator. A distinct badge from Host because they are
  /// distinct authorities: a Moderator moderates Members and cannot touch a Host or a peer.
  bool get isModeratorMessage => senderRole == 'Moderator';

  ChatMessage copyWith({
    String? id,
    String? body,
    DateTime? createdAt,
    bool? isPinned,
    bool? isDeleted,
    ChatSendStatus? status,
    String? senderName,
    String? senderRole,
    List<ChatAttachment>? attachments,
    List<ChatReaction>? reactions,
  }) =>
      ChatMessage(
        id: id ?? this.id,
        roomId: roomId,
        body: body ?? this.body,
        createdAt: createdAt ?? this.createdAt,
        clientMessageId: clientMessageId,
        senderId: senderId,
        senderName: senderName ?? this.senderName,
        senderRole: senderRole ?? this.senderRole,
        kind: kind,
        replyToMessageId: replyToMessageId,
        isPinned: isPinned ?? this.isPinned,
        isDeleted: isDeleted ?? this.isDeleted,
        status: status ?? this.status,
        attachments: attachments ?? this.attachments,
        // Carried, not parameters: nothing copies a message in order to change any of these, and
        // omitting them would silently strip reactions or a link card on a plain status change.
        editedAt: editedAt,
        reactions: reactions ?? this.reactions,
        forwardedFrom: forwardedFrom,
        linkPreview: linkPreview,
        pinnedUntil: pinnedUntil,
      );

  /// Total order over a room's messages. Used for merge, insert position and de-duplication.
  int compareTo(ChatMessage other) {
    final byTime = createdAt.compareTo(other.createdAt);
    return byTime != 0 ? byTime : id.compareTo(other.id);
  }
}

/// One page of history plus the cursors to continue in either direction.
///
/// Cursors are opaque strings by contract — never parsed, ordered or constructed on the client.
class ChatMessagePage {
  const ChatMessagePage({
    required this.messages,
    this.olderCursor,
    this.newerCursor,
  });

  final List<ChatMessage> messages;
  final String? olderCursor;
  final String? newerCursor;
}

/// One emoji on a message with its tally (D-295). [mine] is server-computed for the requesting
/// caller, so the toggled state needs no scan of [userIds].
class ChatReaction {
  const ChatReaction({
    required this.emoji,
    required this.count,
    this.mine = false,
    this.userIds = const [],
  });

  final String emoji;
  final int count;
  final bool mine;
  final List<String> userIds;
}

/// Where a forwarded message came from (D-295). Both parts degrade rather than leak.
class ChatForwardSource {
  const ChatForwardSource({required this.messageId, this.senderName});

  final String messageId;
  final String? senderName;
}

/// A sender-supplied link card (D-295). [host] is derived server-side from [url] and is what the UI
/// must show: the title can misdescribe the page, the host cannot lie about where the tap goes.
class ChatLinkPreview {
  const ChatLinkPreview({
    required this.url,
    required this.host,
    this.title,
    this.description,
    this.imageUrl,
  });

  final String url;
  final String host;
  final String? title;
  final String? description;
  final String? imageUrl;
}

/// One search result (D-295). Carries enough room context to render a result row without a second
/// lookup per hit — the server resolves the label, because only it knows whether a direct room
/// should read as the other person's name.
class ChatSearchHit {
  const ChatSearchHit({
    required this.messageId,
    required this.roomId,
    required this.roomLabel,
    required this.body,
    required this.createdAt,
    this.senderId,
    this.senderName,
  });

  final String messageId;
  final String roomId;
  final String roomLabel;
  final String body;
  final DateTime createdAt;
  final String? senderId;
  final String? senderName;
}
