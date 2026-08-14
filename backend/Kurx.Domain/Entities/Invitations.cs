using Kurx.Domain.Enums;

namespace Kurx.Domain.Entities;

public class EventInvitation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EventId { get; set; }
    public Guid InvitedBy { get; set; }
    public Guid? GroupId { get; set; }                     // set = competition team invite (D-036)
    public string Name { get; set; } = null!;

    /// <summary>D-266 M6 (D9 Method A) — the invited Kurx user, when the organiser invited by username
    /// rather than by contact detail. Exactly one of <see cref="InvitedUserId"/> / <see cref="Email"/> /
    /// <see cref="Phone"/> is set: an invitation addressed to nobody cannot be delivered, and one addressed
    /// two ways cannot say which identity accepted it.
    ///
    /// <para>Method A needs no email, no phone and no forwardable link — the invitee is already on Kurx and
    /// gets an in-app notification they accept or decline in place.</para></summary>
    public Guid? InvitedUserId { get; set; }

    public string? Email { get; set; }
    public string? Phone { get; set; }                    // E.164
    public InvitationChannel Channel { get; set; }
    public string InviteToken { get; set; } = null!;      // 12-char url-safe, unique
    public InvitationSendStatus SendStatus { get; set; } = InvitationSendStatus.Pending;
    public InvitationRsvpStatus RsvpStatus { get; set; } = InvitationRsvpStatus.None;
    public Guid? OrderId { get; set; }
    public int SendCount { get; set; }
    public InvitationStatus Status { get; set; } = InvitationStatus.Active;
    public DateTime? SentAt { get; set; }
    public DateTime? RespondedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>D-266 M6 (D9 Method B) — a shareable link granting permission to register.
///
/// <para><b>Deliberately not the per-invitee <see cref="EventInvitation.InviteToken"/>.</b> That token
/// names ONE person; a link with seats, expiry and a passcode is a different object with its own
/// accounting. Overloading the per-invitee token would make "how many seats are left" unanswerable and let
/// a forwarded token be redeemed by whoever received it.</para>
///
/// <para><b>It grants permission to register and nothing more.</b> An invited guest of a paid event still
/// pays — D9 has no "invited ⇒ free" path, and no ticket exists until the order settles.</para></summary>
public class EventInviteLink
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EventId { get; set; }
    public Guid CreatedBy { get; set; }

    /// <summary>URL-safe and unique. A bearer capability: anyone holding it may redeem, which is exactly
    /// why <see cref="PasscodeHash"/> exists for links that need a second factor.</summary>
    public string Token { get; set; } = null!;

    /// <summary>null = unlimited. The cap the SQL claim carries in its WHERE clause.</summary>
    public int? MaxSeats { get; set; }

    /// <summary>Claimed in SQL, never read-modify-write (D-240/D-261). Two people redeeming the last seat
    /// at once is the same bug class as coupon over-redemption and needs the same conditional UPDATE.</summary>
    public int UsedCount { get; set; }

    /// <summary>Dead after one successful redemption, whatever <see cref="MaxSeats"/> says.</summary>
    public bool SingleUse { get; set; }

    public DateTime? ExpiresAt { get; set; }

    /// <summary>Hashed, never stored in the clear: if the row leaks, possession of the link must not also
    /// hand over the factor that was meant to protect it. Verified server-side — a client-side check on a
    /// bearer link is no check at all.</summary>
    public string? PasscodeHash { get; set; }

    public InvitationStatus Status { get; set; } = InvitationStatus.Active;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>D-266 M6 — one user's redemption of one link.
///
/// <para>Exists so redeeming twice is idempotent (D9 rule 7). Without a row per (link, user) a refresh or a
/// double-tap would consume a second seat, and the organiser's seat count would measure clicks rather than
/// people.</para></summary>
public class EventInviteLinkRedemption
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InviteLinkId { get; set; }
    public Guid UserId { get; set; }
    public DateTime RedeemedAt { get; set; } = DateTime.UtcNow;
}

public class EventAnnouncement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EventId { get; set; }
    public Guid CreatedBy { get; set; }
    public string Title { get; set; } = null!;            // max 120 chars
    public string Body { get; set; } = null!;             // max 2000 chars plain text
    public AnnouncementAudience Audience { get; set; }
    public bool IncludeChildEvents { get; set; }
    public string[] Channels { get; set; } = [];          // push, email, whatsapp
    public AnnouncementStatus Status { get; set; } = AnnouncementStatus.Queued;
    public DateTime? ScheduledAt { get; set; }
    public int TotalRecipients { get; set; }
    public int SentPush { get; set; }
    public int SentEmail { get; set; }
    public int SentWhatsapp { get; set; }
    public int FailedCount { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
