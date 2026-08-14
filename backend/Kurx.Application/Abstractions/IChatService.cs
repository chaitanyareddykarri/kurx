namespace Kurx.Application.Abstractions;

/// <summary>What the caller may actually do in this room, computed server-side (D-104). Clients render
/// from this and never re-derive permissions from role + policy — that is what lets a future chat mode
/// (Q&amp;A, slow mode, scheduled open) ship without a client release.</summary>
public record ChatCapabilities(
    bool CanPost, bool CanReply, bool CanUpload, bool CanPin,
    bool CanDelete, bool CanModerate, bool CanMentionAll,
    /// <summary>D-301 — room lifecycle and settings: lock, post policy, slow mode. **Host only.** A
    /// Moderator moderates people, never the room itself, so this is deliberately NOT implied by
    /// <paramref name="CanModerate"/>. Defaulted so an older client is unaffected.</summary>
    bool CanManageRoom = false,
    /// <summary>D-301 — may promote a Member to Moderator, or demote one. **Host only**, and the reason
    /// the ladder cannot be climbed: a Moderator who could grant this could mint a peer.</summary>
    bool CanManageModerators = false,
    /// <summary>D-301 — this member's own role, so a client can render a Moderator badge without
    /// re-deriving it from the capability flags.</summary>
    string MyRole = "Member");

/// <summary>An attachment on a message (D-110). The array was reserved on the message view by D-104
/// precisely so filling it in would not be a breaking change to either client.
///
/// <paramref name="Url"/> is a short-lived signed download URL minted per read — never a permanent
/// public link, so revoking access is a matter of not minting another one.</summary>
public record ChatAttachmentView(
    Guid Id, string Url, string FileName, string ContentType, long SizeBytes,
    int? Width, int? Height);

/// <summary>What the client needs to upload one file: where to PUT the bytes, and the key to quote
/// back on confirm.</summary>
public record AttachmentUploadTicket(string Key, string Url, IReadOnlyDictionary<string, string> Headers);

public record ChatRoomView(
    Guid RoomId, string Kind, string Status, string PostPolicy, string MyRole,
    DateTime? MutedUntil, int UnreadCount, ChatCapabilities Capabilities,
    List<ChatMessageView> PinnedMessages,
    /// <summary>Who is connected right now (D-114). Empty when presence is unavailable, which is
    /// indistinguishable from an empty room — clients use <paramref name="PresenceEnabled"/> to tell
    /// "nobody is here" apart from "we cannot know".</summary>
    IReadOnlyList<Guid> OnlineUserIds,
    bool PresenceEnabled,
    /// <summary>D-292 — the direct room's request state (<c>pending</c>/<c>accepted</c>/<c>declined</c>),
    /// null for event rooms. Previously absent, which is why a request opened as an ordinary conversation:
    /// the client had no way to know it was looking at one, so it rendered a composer instead of
    /// Accept/Decline.</summary>
    string? DmRequestState = null,
    /// <summary>Who opened a direct conversation, null for event rooms. The client needs it to tell
    /// "your request, waiting on them" from "their request, waiting on you" — only the recipient decides.</summary>
    Guid? DmInitiatedBy = null);

public record ChatMessageView(
    Guid Id, Guid? ClientMessageId, Guid RoomId, Guid? SenderId, string? SenderName, string? SenderRole,
    string Kind, string Body, Guid? ReplyToMessageId,
    bool IsPinned, bool IsDeleted, IReadOnlyList<ChatAttachmentView> Attachments, DateTime CreatedAt,
    /// <summary>When the sender last edited this message, null if never (D-293). Clients render an
    /// "edited" marker from it. Optional-with-default so an older client that ignores it is unaffected.</summary>
    DateTime? EditedAt = null,
    /// <summary>Reaction summary (D-295), empty when none. Aggregated per emoji rather than one row per
    /// reactor, because that is what the UI renders and it keeps a busy message's payload flat.</summary>
    IReadOnlyList<ChatReactionView>? Reactions = null,
    /// <summary>Set when this message was forwarded (D-295). Null source name means the original is
    /// gone or lives somewhere the reader cannot see — the card then reads simply "Forwarded".</summary>
    ChatForwardView? ForwardedFrom = null,
    /// <summary>Sender-supplied link card (D-295), null when the message carries no link. The server
    /// never fetches the URL — see <c>ChatMessage.LinkUrl</c> for why.</summary>
    ChatLinkPreviewView? LinkPreview = null,
    /// <summary>When this message's pin expires (D-296), null when it is not pinned or the pin never
    /// expires. <see cref="IsPinned"/> is already computed against this instant, so a client needs it
    /// only to render "until Friday" — never to decide whether the pin still counts.</summary>
    DateTime? PinnedUntil = null);

/// <summary>One emoji on a message, with who reacted (D-295). <paramref name="Mine"/> is computed for
/// the requesting caller so the client can render the toggled state without scanning the id list.</summary>
public record ChatReactionView(string Emoji, int Count, bool Mine, IReadOnlyList<Guid> UserIds);

/// <summary>Where a forwarded message came from (D-295). Both fields are nullable because the source
/// may be deleted, or in a room this reader has no access to — attribution degrades rather than leaks.</summary>
public record ChatForwardView(Guid MessageId, string? SenderName);

/// <summary>A sender-supplied link card (D-295). <paramref name="Host"/> is derived server-side from
/// <paramref name="Url"/> and is the one field a malicious sender cannot fake — clients render it so a
/// preview can misdescribe the page but never mislead about where the tap goes.</summary>
public record ChatLinkPreviewView(string Url, string Host, string? Title, string? Description, string? ImageUrl);

/// <summary>A link card supplied by the SENDER's client on send (D-295). The server stores it and
/// derives the host, but never fetches <paramref name="Url"/> — unfurling server-side would mean the
/// API issuing outbound requests to arbitrary user-controlled addresses, which is SSRF.</summary>
public record ChatLinkPreviewInput(string Url, string? Title, string? Description, string? ImageUrl);

/// <summary>One search result (D-295): the message plus enough room context to render a result row
/// without a second lookup per hit.</summary>
public record ChatSearchHit(
    Guid MessageId, Guid RoomId, string RoomLabel, Guid? SenderId, string? SenderName,
    string Body, DateTime CreatedAt);

/// <summary>One page of history plus the cursors to continue in either direction. Cursors are opaque
/// strings (D-104) — clients must not parse, order, or construct them.</summary>
public record ChatMessagePage(
    List<ChatMessageView> Messages, string? OlderCursor, string? NewerCursor);

public record MyChatView(
    Guid RoomId, Guid EventId, string EventTitle, string? BannerUrl,
    string? LastMessagePreview, int UnreadCount, DateTime? LastActivity,
    /// <summary>D-295 — pinned by THIS member. Without it on the wire the list could not order pinned
    /// rooms first, which is the entire point of pinning one.</summary>
    bool Pinned = false,
    /// <summary>D-295 — this member silenced their own notifications for the room. Not the host mute,
    /// which stops a member posting.</summary>
    bool NotificationsMuted = false,
    /// <summary>D-306 — filed out of the active list by THIS member. Mirrors <c>DmRoomView.Archived</c>;
    /// without it on the wire a client cannot tell an archived room from an active one, which is half of
    /// why archiving an event room was a one-way trip.</summary>
    bool Archived = false);

public interface IChatService
{
    // Room membership automation (called from payment/event flows)
    Task EnsureRoomExistsAsync(Guid eventId, CancellationToken ct = default);
    Task AddMemberAsync(Guid roomId, Guid userId, string role, CancellationToken ct = default);
    Task AddMemberByEventAsync(Guid eventId, Guid userId, string role, CancellationToken ct = default);
    Task RemoveMemberIfNoTicketsAsync(Guid eventId, Guid userId, CancellationToken ct = default);
    /// <summary>Seeds the room at publish: the event's owner and every <see cref="EventAuthority.ChatHostRoles"/>
    /// seat as Hosts, and Staff seats as Members (D-300). Takes no orgId — the event's representation is not
    /// the caller's to supply, and the owner's Host seat does not come from a membership at all (D-272).
    /// Idempotent.
    ///
    /// <para>Seeded from <c>ModeratorRoles</c> until D-300, which made every Staff seat a chat Host.</para></summary>
    Task AddEventHostsAsync(Guid eventId, CancellationToken ct = default);

    /// <summary>Needed at publish because a user may accept while the event is still a draft, when no room
    /// exists yet. Idempotent.</summary>
    /// <summary>Joins every accepted event assignment to the room as a **Member** (D-300). Was
    /// <c>AddAcceptedStaffAsHostsAsync</c>, which made the whole assignment catalogue — volunteers,
    /// media team, support — chat Hosts.</summary>
    Task AddAcceptedStaffAsMembersAsync(Guid eventId, CancellationToken ct = default);

    /// <summary>Undoes a staff assignment's chat effect. Anyone who holds Host by standing on the event
    /// (<see cref="EventPermission.ModerateAudience"/>) keeps it — they did not hold it by the assignment;
    /// anyone else is demoted to Member and then dropped if they hold no ticket. Idempotent.</summary>
    Task RemoveStaffMemberAsync(Guid eventId, Guid userId, CancellationToken ct = default);

    /// <summary>Converges one user's chat membership across **every published room** an organization
    /// represents, against their live authority (D-304).
    ///
    /// <para>Called from the outbox, never inline: an organization role change commits in its own
    /// transaction and this is the consequence, so it is staged on that unit of work and retried by
    /// <c>OutboxDispatchJob</c> like every other post-commit chat effect (D-299).</para>
    ///
    /// <para>Takes no role. The caller's authority is re-resolved here through <see cref="IEventAuthority"/>
    /// at dispatch time, which is what makes redelivery a no-op and concurrent changes converge on the final
    /// committed state rather than on message order. Raises to Host, or demotes and evicts, per event —
    /// never both, and never below what standing on the event itself already grants.</para></summary>
    Task SyncOrgAuthorityAsync(Guid orgId, Guid userId, CancellationToken ct = default);

    /// <summary>Locks an event's room immediately (cancellation, archive). Idempotent — a room that is
    /// already locked is left untouched, so no duplicate system message or broadcast is produced.</summary>
    Task LockRoomForEventAsync(Guid eventId, string reason, CancellationToken ct = default);

    // REST API
    Task<ServiceResult<ChatRoomView>> GetRoomAsync(Guid eventId, Guid userId, CancellationToken ct = default);

    /// <summary>The same view addressed by room id (D-264). Direct rooms have no event to resolve
    /// through, and event chat now routes here too so there is one view builder rather than two.</summary>
    Task<ServiceResult<ChatRoomView>> GetRoomByIdAsync(Guid roomId, Guid userId, CancellationToken ct = default);

    /// <summary>Keyset pagination on (CreatedAt, Id). Exactly one of <paramref name="before"/> /
    /// <paramref name="after"/> may be supplied; <c>after</c> is the delta-sync primitive a reconnecting
    /// client uses to fetch only what it missed (D-104).</summary>
    Task<ServiceResult<ChatMessagePage>> GetMessagesAsync(Guid roomId, Guid userId, string? before, string? after, int limit, CancellationToken ct = default);

    /// <summary>Idempotent on (roomId, clientMessageId): a repeat returns the original message rather
    /// than creating a duplicate, which is what makes an offline retry queue safe (D-104).</summary>
    Task<ServiceResult<ChatMessageView>> SendMessageAsync(Guid roomId, Guid senderId, string body, Guid? replyToId, Guid? clientMessageId, IReadOnlyList<Guid>? attachmentIds = null, ChatLinkPreviewInput? linkPreview = null, CancellationToken ct = default);

    // ── Attachments (D-110) ────────────────────────────────────────────────────────────────

    /// <summary>Step 1: reserve a storage key and hand back a presigned upload URL. Validates that
    /// the caller may post here and that the declared type is on the allow-list — but the declared
    /// type is only a hint, and is re-derived from the bytes on confirm.</summary>
    Task<ServiceResult<AttachmentUploadTicket>> PresignAttachmentAsync(
        Guid roomId, Guid userId, string fileName, string contentType, long sizeBytes, CancellationToken ct = default);

    /// <summary>Step 2: the upload finished. This is where every authoritative check happens —
    /// ownership, size, MIME, extension, magic bytes, image dimensions and the malware scan — because
    /// it is the first moment the server can look at the actual object. Idempotent per storage key.</summary>
    Task<ServiceResult<ChatAttachmentView>> ConfirmAttachmentAsync(
        Guid roomId, Guid userId, string storageKey, CancellationToken ct = default);

    /// <summary>Fresh signed download URL for one attachment the caller is allowed to see.</summary>
    Task<ServiceResult<string>> GetAttachmentUrlAsync(Guid attachmentId, Guid userId, CancellationToken ct = default);

    Task<ServiceResult<bool>> MarkReadAsync(Guid roomId, Guid userId, Guid lastReadMessageId, CancellationToken ct = default);

    /// <summary>Edits the body of the caller's OWN message (D-293). Refuses a no-op, a message that is
    /// already deleted, a system message, and anyone who is not the sender — a host may remove an
    /// offending message but must never be able to put words in someone's mouth.
    ///
    /// <para>Gated on the same right as posting: a locked room, a ban, a mute or a hosts-only policy all
    /// refuse an edit exactly as they refuse a send. Bounded by the same window as delete, so a
    /// conversation cannot be rewritten long after anyone has read it.</para></summary>
    Task<ServiceResult<ChatMessageView>> EditMessageAsync(Guid messageId, Guid userId, string body, CancellationToken ct = default);

    /// <summary>Delete for everyone: redacts the body for all members and drops the attachments.</summary>
    Task<ServiceResult<bool>> DeleteMessageAsync(Guid messageId, Guid userId, CancellationToken ct = default);

    /// <summary>Hides a message from the CALLER only — "delete for me" (D-293). Idempotent. The message
    /// itself is untouched, so every other member still sees it, moderation still reads it and the audit
    /// trail is unaffected; only this member's reads filter it out. Never broadcast: no one else's view
    /// changed, and telling the room would leak a private filing decision.</summary>
    Task<ServiceResult<bool>> HideMessageAsync(Guid messageId, Guid userId, CancellationToken ct = default);
    /// <summary>Every event room this caller belongs to. <paramref name="archived"/> selects which side
    /// of their filing to return — false (the default) is the active list, true is what they archived.
    ///
    /// <para>D-306: the parameter is the whole point. Archiving already worked and already removed the
    /// room from this list; nothing could return it, so archiving an event chat destroyed access to it.
    /// Mirrors <c>IDmService.ListAsync</c>, which has had this since DMs shipped, rather than inventing a
    /// second shape for the same idea. Defaulted, so every existing caller keeps the active list.</para></summary>
    Task<ServiceResult<List<MyChatView>>> GetMyChatsAsync(Guid userId, bool archived = false, CancellationToken ct = default);

    // ── D-295 ──────────────────────────────────────────────────────────────────

    /// <summary>Toggles one emoji reaction by this member. Tapping the same emoji again removes it, so
    /// there is one call rather than an add/remove pair the client must choose between. Returns the
    /// message's whole reaction summary, so the caller renders server truth rather than a local guess.</summary>
    Task<ServiceResult<IReadOnlyList<ChatReactionView>>> ToggleReactionAsync(
        Guid messageId, Guid userId, string emoji, CancellationToken ct = default);

    /// <summary>Full-text search across the rooms the caller belongs to, or one room when
    /// <paramref name="roomId"/> is supplied. Membership is part of the QUERY, not a filter applied
    /// afterwards — filtering later would still have let result counts leak the existence of matches in
    /// rooms the caller cannot see.</summary>
    Task<ServiceResult<IReadOnlyList<ChatSearchHit>>> SearchMessagesAsync(
        Guid userId, string query, Guid? roomId, int limit, CancellationToken ct = default);

    /// <summary>Moves this member's DELIVERED pointer forward — the ✓✓ that precedes a read receipt.
    /// Monotonic like the read pointer: an older id is discarded rather than applied, so an
    /// out-of-order acknowledgement from a second device cannot walk the receipt backwards.</summary>
    Task<ServiceResult<bool>> MarkDeliveredAsync(Guid roomId, Guid userId, Guid messageId, CancellationToken ct = default);

    /// <summary>Forwards a message into another room the caller belongs to. The body is re-sent as a new
    /// message carrying <c>ForwardedFromMessageId</c>. <b>Attachments are deliberately not carried</b>:
    /// re-pointing them would let a forward smuggle a file into a room its uploader never had access to.</summary>
    Task<ServiceResult<ChatMessageView>> ForwardMessageAsync(
        Guid messageId, Guid userId, Guid targetRoomId, Guid? clientMessageId, CancellationToken ct = default);

    /// <summary>Pins or unpins a conversation in this member's own inbox. Personal, like archiving.</summary>
    Task<ServiceResult<bool>> SetPinnedAsync(Guid roomId, Guid userId, bool pinned, CancellationToken ct = default);

    /// <summary>Silences this member's own notifications for a room until <paramref name="until"/>, or
    /// clears it when null. Distinct from host mute, which stops them posting.</summary>
    Task<ServiceResult<bool>> SetNotificationsMutedAsync(
        Guid roomId, Guid userId, DateTime? until, CancellationToken ct = default);

    /// <summary>Every attachment in a room, newest first — the shared-media grid. Membership-gated, and
    /// it honours the caller's own hidden messages (D-293) like every other per-member read.</summary>
    Task<ServiceResult<IReadOnlyList<ChatAttachmentView>>> RoomMediaAsync(
        Guid roomId, Guid userId, int limit, CancellationToken ct = default);

    /// <summary>Files a room into the caller's own archive, or takes it back out (D-292). Per-member and
    /// reversible, so it says nothing to anyone else in the room — deliberately not
    /// <see cref="Kurx.Domain.Enums.ChatRoomStatus.Archived"/>, which is shared lifecycle derived from the
    /// event and one-way.
    ///
    /// <para>Lives here rather than on <see cref="IDmService"/> because it writes <c>ChatMember</c>, which
    /// this service owns, and applies to any room kind. It was DM-only by accident of where it was first
    /// needed, which is why an event chat could not be filed away.</para></summary>
    Task<ServiceResult<bool>> SetArchivedAsync(Guid roomId, Guid userId, bool archived, CancellationToken ct = default);

    // Host moderation
    Task<ServiceResult<bool>> UpdateRoomAsync(Guid roomId, Guid userId, string? postPolicy, string? status, CancellationToken ct = default);
    Task<ServiceResult<bool>> MuteMemberAsync(Guid roomId, Guid actorId, Guid targetUserId, int minutes, CancellationToken ct = default);
    Task<ServiceResult<bool>> BanMemberAsync(Guid roomId, Guid actorId, Guid targetUserId, CancellationToken ct = default);
    Task<ServiceResult<bool>> UnbanMemberAsync(Guid roomId, Guid actorId, Guid targetUserId, CancellationToken ct = default);
    /// <summary>Pins or unpins a message for the whole room (hosts only, at most three at a time).
    ///
    /// <para><paramref name="duration"/> is how long the pin lasts (D-296). Null takes the 7-day default
    /// rather than pinning forever: a permanent pin is the one outcome a host never revisits, and the
    /// default is what most callers will send. Ignored when unpinning. Out-of-range durations are
    /// rejected, not clamped — silently pinning for 30 days when the caller asked for a year would be a
    /// worse answer than saying no.</para></summary>
    Task<ServiceResult<bool>> PinMessageAsync(Guid messageId, Guid userId, bool pin, TimeSpan? duration = null, CancellationToken ct = default);
    /// <summary>Promotes a Member to Moderator, or demotes one back (D-301). **Hosts only** — never a
    /// Moderator, or the "cannot act on an equal" rule becomes bypassable in two steps.
    ///
    /// <para>Idempotent: re-issuing the current role succeeds without a second audit row or notification.
    /// A Host's role cannot be changed here at all (<c>cannot_change_host</c>) — that would be a transfer
    /// of room ownership wearing a moderation button.</para></summary>
    Task<ServiceResult<bool>> SetModeratorAsync(Guid roomId, Guid actorId, Guid targetUserId, bool moderator, CancellationToken ct = default);

    Task<ServiceResult<bool>> ReportMessageAsync(Guid messageId, Guid reporterId, string reason, CancellationToken ct = default);

    // Lifecycle jobs
    Task LockExpiredRoomsAsync(CancellationToken ct = default);

    /// <summary>Removes storage objects for deleted messages and for uploads that were never claimed
    /// by a message. Both are the direct consequence of confirm happening before send.</summary>
    Task CleanupAttachmentsAsync(CancellationToken ct = default);
}
