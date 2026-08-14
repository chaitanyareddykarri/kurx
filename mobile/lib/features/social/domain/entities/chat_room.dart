import 'chat_message.dart';

/// What the signed-in user may do in a room, as computed by the server (D-104).
///
/// The client renders from these flags and never re-derives permission from role + policy. That is
/// the whole point of the field: a future chat mode (Q&A, slow mode, scheduled open) changes only
/// the server, with no app release.
class ChatCapabilities {
  const ChatCapabilities({
    required this.canPost,
    required this.canReply,
    required this.canUpload,
    required this.canPin,
    required this.canDelete,
    required this.canModerate,
    required this.canMentionAll,
    this.canManageRoom = false,
    this.canManageModerators = false,
    this.myRole = 'Member',
  });

  /// Everything denied — the safe default when a room could not be loaded.
  const ChatCapabilities.none()
      : canPost = false,
        canReply = false,
        canUpload = false,
        canPin = false,
        canDelete = false,
        canModerate = false,
        canMentionAll = false,
        canManageRoom = false,
        canManageModerators = false,
        myRole = 'Member';

  final bool canPost;
  final bool canReply;
  final bool canUpload;
  final bool canPin;
  final bool canDelete;
  final bool canModerate;
  final bool canMentionAll;

  /// D-301 — room lifecycle and settings: lock, post policy, slow mode. **Host only**, and deliberately
  /// NOT implied by [canModerate]: a Moderator moderates people, never the room. A UI rendering
  /// lock-room off [canModerate] alone would offer a control the server refuses.
  final bool canManageRoom;

  /// D-301 — may promote a Member to Moderator or demote one. **Host only**, and the reason the ladder
  /// cannot be climbed: a Moderator who could grant this could mint a peer.
  final bool canManageModerators;

  /// D-301 — this member's own chat role, so a badge renders without re-deriving it from the flags.
  final String myRole;
}

/// A room as seen by one member.
class ChatRoom {
  const ChatRoom({
    required this.roomId,
    required this.kind,
    required this.status,
    required this.postPolicy,
    required this.myRole,
    required this.unreadCount,
    required this.capabilities,
    this.mutedUntil,
    this.pinnedMessages = const [],
    this.onlineUserIds = const [],
    this.presenceEnabled = false,
    this.dmRequestState,
    this.dmInitiatedBy,
  });

  final String roomId;
  final String kind;
  final String status;      // Active | Locked | Archived
  final String postPolicy;  // Everyone | HostsOnly
  final String myRole;      // Member | Moderator | Host  (D-301)
  final int unreadCount;
  final ChatCapabilities capabilities;
  final DateTime? mutedUntil;
  /// Messages the hosts pinned, newest first, at most three. Typed as of D-296 — it was `List<dynamic>`,
  /// which parsed correctly and could not be rendered, which is why no pinned strip existed. The server
  /// has already dropped any whose window lapsed, so everything here is still in force.
  final List<ChatMessage> pinnedMessages;

  /// Who was connected when the room was fetched (D-114). This is the initial synchronisation —
  /// everything after it arrives as a `PresenceChanged` event.
  final List<String> onlineUserIds;

  /// False when the server has no shared presence store. An empty roster then means "we cannot
  /// know", not "nobody is here" — which is why the UI hides indicators entirely rather than
  /// showing everyone as offline.
  final bool presenceEnabled;

  /// The direct room's request gate (D-292): `pending` | `accepted` | `declined`, null for an event
  /// room. Null is meaningful — "not a conversation with a gate" is not the same as any state of one.
  final String? dmRequestState;

  /// Who opened a direct conversation, null for an event room. Only the recipient may accept, so the
  /// UI needs this to tell "your request, waiting on them" from "their request, waiting on you".
  final String? dmInitiatedBy;

  bool get isLocked => status == 'Locked';

  /// A request awaiting THIS user's decision. The initiator's own pending request is not one: they
  /// get the ordinary composer, because only the recipient decides.
  bool awaitingMyDecision(String? myUserId) =>
      dmRequestState == 'pending' && dmInitiatedBy != null && dmInitiatedBy != myUserId;

  /// Frozen seven days after the event ended (D-122). History stays readable; nothing else happens
  /// here — no presence, no typing, no notifications.
  bool get isArchived => status == 'Archived';
  bool get isMuted => mutedUntil != null && mutedUntil!.isAfter(DateTime.now().toUtc());

  /// Why the composer is disabled, or null when the user may post. Ordered most-specific first so
  /// the message a user sees names the actual reason rather than a generic refusal.
  String? get cannotPostReason {
    if (capabilities.canPost) return null;
    // Ahead of the room-state reasons: the room is Active and the user is neither muted nor
    // policy-blocked, so without this an honest refusal read as the generic catch-all (D-292).
    if (dmRequestState == 'declined') return 'This person declined your message request.';
    if (isArchived) return 'This chat has been archived. You can still read the history.';
    if (isLocked) return 'This event has ended. Chat is read-only.';
    if (isMuted) return 'You are muted in this chat.';
    if (postPolicy == 'HostsOnly') return 'Only hosts can post right now.';
    return 'You cannot post in this chat.';
  }
}

/// One row in the "my chats" list.
class MyChat {
  const MyChat({
    required this.roomId,
    required this.eventId,
    required this.eventTitle,
    required this.unreadCount,
    this.bannerUrl,
    this.lastMessagePreview,
    this.lastActivity,
    this.pinned = false,
    this.notificationsMuted = false,
    this.archived = false,
  });

  final String roomId;
  final String eventId;
  final String eventTitle;
  final int unreadCount;
  final String? bannerUrl;
  final String? lastMessagePreview;

  /// Server-supplied, and the last MESSAGE's timestamp as of D-292 — it was the event's updated-at,
  /// which is why this once carried a documented-debt note. The list still prefers locally-observed
  /// activity where it has any, because a just-sent message is known here before the next fetch.
  final DateTime? lastActivity;

  /// D-295 — pinned to the top of THIS reader's list. Personal filing: pinning says nothing to anyone
  /// else in the room, and the server sorts pinned first so the list order matches after a refresh.
  final bool pinned;

  /// D-295 — notifications silenced for this room by this reader. Distinct from the room being locked
  /// or the reader being muted BY a host: this one is the reader's own choice and hides nothing.
  final bool notificationsMuted;

  /// D-306 — filed out of the active list by this reader. Personal and reversible, exactly like
  /// [pinned] and [notificationsMuted]. Emphatically NOT `ChatRoom.status == 'Archived'`, which is the
  /// room's own lifecycle (D-122) and belongs to the event's clock rather than to anyone's filing.
  final bool archived;

  MyChat copyWith({bool? pinned, bool? notificationsMuted, bool? archived}) => MyChat(
        roomId: roomId,
        eventId: eventId,
        eventTitle: eventTitle,
        unreadCount: unreadCount,
        bannerUrl: bannerUrl,
        lastMessagePreview: lastMessagePreview,
        lastActivity: lastActivity,
        pinned: pinned ?? this.pinned,
        notificationsMuted: notificationsMuted ?? this.notificationsMuted,
        archived: archived ?? this.archived,
      );
}
