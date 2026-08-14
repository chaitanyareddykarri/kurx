using Kurx.Domain.Enums;

namespace Kurx.Domain.Entities;

public class ChatRoom
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Null for a direct message (D-264). Still UNIQUE with <see cref="Kind"/> for event rooms —
    /// the index is filtered to non-null, so "one General room per event" means exactly what it did.</summary>
    public Guid? EventId { get; set; }

    public ChatRoomKind Kind { get; set; } = ChatRoomKind.General;
    public ChatPostPolicy PostPolicy { get; set; } = ChatPostPolicy.Everyone;
    public ChatRoomStatus Status { get; set; } = ChatRoomStatus.Active;
    public DateTime? LockedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ── Direct messages (D-264) ──────────────────────────────────────────────
    // The participant pair, canonicalised low < high, with a unique index over it. That index — not an
    // application "does a room already exist?" check — is what makes room creation idempotent: two
    // people tapping Message at the same instant both see nothing and both insert, and one loses.
    // The alternative failure is silent and unrecoverable: two rooms for one conversation, each holding
    // half the messages.

    public Guid? DirectLowUserId { get; set; }
    public Guid? DirectHighUserId { get; set; }

    /// <summary>Null for event rooms. A DM from a non-ally lands <see cref="DmRequestState.Pending"/>
    /// and notifies nobody until accepted — the spam control, and not optional.</summary>
    public DmRequestState? DmRequestState { get; set; }

    /// <summary>Who opened the conversation. The other party is the one who gets to accept, so this is
    /// what distinguishes "your request" from "a request for you".</summary>
    public Guid? DmInitiatedBy { get; set; }
}

public class ChatMember
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RoomId { get; set; }
    public Guid UserId { get; set; }
    public ChatMemberRole Role { get; set; } = ChatMemberRole.Member;
    public DateTime? MutedUntil { get; set; }
    public bool IsBanned { get; set; }
    /// <summary>Authoritative read position (D-104). Server-assigned and monotonic, so two devices
    /// merge by max() with no clock-skew failure mode. Intentionally has no FK: a dangling pointer is
    /// harmless (unread falls back to counting everything) and an FK here would add a second cascade
    /// path from chat_rooms.</summary>
    public Guid? LastReadMessageId { get; set; }
    /// <summary>Analytics only (D-104) — never used to compute unread. Written from a client-supplied
    /// timestamp, so it is not trustworthy for correctness.</summary>
    public DateTime LastReadAt { get; set; } = DateTime.UtcNow;
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    /// <summary>The user's own archive folder (D-264). Deliberately NOT
    /// <see cref="ChatRoomStatus.Archived"/>: that is room lifecycle, derived from the event's state,
    /// one-way and shared by every member (D-122/D-124). This is a personal filing decision, per member
    /// and reversible. Merging them would let one person archiving a conversation lock it for the other.</summary>
    public DateTime? ArchivedAt { get; set; }

    /// <summary>Pinned to the top of THIS member's inbox (D-295). Personal filing, like
    /// <see cref="ArchivedAt"/> — pinning a conversation says nothing to the other party. A timestamp
    /// rather than a bool so several pins order by when they were pinned.</summary>
    public DateTime? PinnedAt { get; set; }

    /// <summary>Notifications silenced for this member until this instant (D-295).
    ///
    /// <para><b>This is not <see cref="MutedUntil"/>.</b> That one is a HOST silencing a member — a
    /// moderation action that stops them posting. This one is a member silencing the ROOM for
    /// themselves, and it stops nothing but their own notifications. Opposite direction, opposite
    /// subject; sharing one column would have made "mute" mean two incompatible things.</para></summary>
    public DateTime? NotificationsMutedUntil { get; set; }

    /// <summary>Furthest message this member's device has acknowledged receiving (D-295) — the ✓✓ that
    /// precedes a read receipt. Mirrors <see cref="LastReadMessageId"/> exactly, including its
    /// monotonic forward-only rule, rather than a row per (message, member): the pointer is O(members)
    /// where the table would be O(members × messages) for the same information.</summary>
    public Guid? LastDeliveredMessageId { get; set; }
}

public class ChatMessage
{
    /// <summary>UUIDv7 (D-104): k-sortable, so it doubles as the pagination cursor. Rows created
    /// before D-104 hold v4 ids, which is why ordering is the (CreatedAt, Id) pair rather than Id alone.</summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid RoomId { get; set; }
    /// <summary>Client-supplied idempotency key, unique per room (D-104). Null for system messages.
    /// Makes a retried send safe and lets optimistic UI reconcile its placeholder.</summary>
    public Guid? ClientMessageId { get; set; }
    public Guid? SenderId { get; set; }                   // null = system message
    public ChatMessageKind Kind { get; set; } = ChatMessageKind.Text;
    public string Body { get; set; } = null!;             // max 1000 chars; cleared on delete
    public Guid? ReplyToMessageId { get; set; }
    public bool IsPinned { get; set; }

    /// <summary>D-296 — when this pin stops applying. A pin is an announcement ("read this now"), and an
    /// announcement nobody cleared is stale within days: without an expiry the top of every busy room
    /// silts up with last month's notice and readers learn to ignore the pinned strip entirely.
    ///
    /// <para>Expiry is evaluated at READ time, not by a sweeper. A row whose instant has passed simply
    /// stops matching the pinned filter — so an expired pin needs no job to run, cannot be missed by one
    /// that failed, and un-expires correctly if a clock or a duration is ever corrected. <see cref="IsPinned"/>
    /// stays the flag; this is the window it is true for. Null means no expiry, which only pre-D-296 rows
    /// carry — every pin written now gets one.</para></summary>
    public DateTime? PinnedUntil { get; set; }

    public bool IsDeleted { get; set; }
    public Guid? DeletedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When the sender last edited the body (D-293). Null means never edited, which is what
    /// every client renders the "edited" marker from — the same shape <see cref="Post.EditedAt"/> uses,
    /// so the two surfaces read alike.
    ///
    /// <para>Deliberately NOT part of the sort key. Ordering is (CreatedAt, Id) and stays that way: an
    /// edit must not move a message, or a correction typed an hour later would jump to the bottom of
    /// everyone's history and read as a new message.</para></summary>
    public DateTime? EditedAt { get; set; }

    /// <summary>The message this one was forwarded from (D-295), null when composed directly. Kept as a
    /// pointer rather than a copied "forwarded from X" string so the attribution cannot go stale, and
    /// deliberately WITHOUT a foreign key: the source may live in a room the reader cannot see, and may
    /// be deleted later — a dangling pointer degrades to "Forwarded", which is the honest fallback.</summary>
    public Guid? ForwardedFromMessageId { get; set; }

    // ── Link preview (D-295) ──────────────────────────────────────────────────
    // Supplied by the SENDER's client, never fetched by the server. Server-side unfurling means the
    // API issues an outbound request to an arbitrary user-controlled URL, which is textbook SSRF —
    // internal metadata endpoints, private ranges, redirect chains. Sender-side is also what WhatsApp
    // does, for the same reason.
    //
    // The trade is that a malicious client can send a preview that misdescribes its target. That is
    // contained by rendering <see cref="LinkUrl"/>'s real host in the card on every client: the
    // preview may lie about the page, it can never lie about where the tap goes.

    /// <summary>Absolute http(s) URL the card points at. Non-null whenever any other Link* field is.</summary>
    public string? LinkUrl { get; set; }
    public string? LinkTitle { get; set; }
    public string? LinkDescription { get; set; }
    /// <summary>Absolute image URL for the card. Rendered by the client, which is already sandboxed
    /// against arbitrary remote images; the server neither fetches nor proxies it.</summary>
    public string? LinkImageUrl { get; set; }
}

/// <summary>One emoji reaction by one member on one message (D-295).
///
/// <para>The unique triple (message, user, emoji) is the fact and the idempotency guard: tapping the
/// same emoji twice removes it rather than stacking, and two taps racing converge on one row instead
/// of erroring. Emoji is stored as text rather than an enum — the set is not ours to fix, and a
/// closed list would need a migration every time a client added one.</para></summary>
public class ChatMessageReaction
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid MessageId { get; set; }
    public Guid UserId { get; set; }
    /// <summary>A single rendered emoji. Length-capped and validated at the boundary — this is echoed
    /// to every member of the room, so it is not a free text field.</summary>
    public string Emoji { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>One member hiding one message for themselves — "delete for me" (D-293).
///
/// <para>A row here is a <b>view</b> decision and nothing else. The message is untouched: other members
/// still see it, the sender still sees theirs, moderation still reads the body, and the audit trail is
/// unaffected. That is the whole distinction from <see cref="ChatMessage.IsDeleted"/>, which is
/// delete-for-everyone and does redact the body.</para>
///
/// <para>A separate table rather than a column, because the fact is per (message, member) and a message
/// has no bounded member count. The unique pair is also what makes hiding idempotent — a retried tap
/// loses the insert race and converges rather than erroring.</para></summary>
public class ChatMessageHide
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid MessageId { get; set; }
    public Guid UserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// A file attached to a chat message (D-110).
///
/// <see cref="MessageId"/> is nullable because the upload completes BEFORE the message exists: the
/// client confirms an upload, then posts a message referencing it. An attachment that never gets
/// claimed is an orphan, and the cleanup job is what stops those accumulating in storage — that
/// nullable column is the reason garbage collection is needed at all.
///
/// Only metadata lives here. Binary content is always in <c>IStorage</c> under <see cref="StorageKey"/>.
/// </summary>
public class ChatAttachment
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    /// <summary>Null until a message claims it. Exactly one message, once set.</summary>
    public Guid? MessageId { get; set; }
    /// <summary>Known at upload time, so an orphan can still be scoped and swept.</summary>
    public Guid RoomId { get; set; }
    public string StorageKey { get; set; } = null!;
    /// <summary>Display only — sanitized on confirm and never used to build a path.</summary>
    public string FileName { get; set; } = null!;
    /// <summary>Server-verified against the magic bytes, not the client's claim.</summary>
    public string ContentType { get; set; } = null!;
    public long SizeBytes { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public Guid UploadedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    /// <summary>Set when the owning message is deleted. The storage object is removed by the
    /// cleanup job, so deletion stays fast and the sweep stays idempotent.</summary>
    public DateTime? DeletedAt { get; set; }
}
