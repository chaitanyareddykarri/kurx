namespace Kurx.Application.Abstractions;

/// <summary>
/// Event badges — the organizer-facing half of D-331's ID card (D-362).
///
/// <para><b>Organizer-only by design.</b> There is no holder-facing operation on this interface and no
/// route that serves a badge to the person it names. Badges are pre-printed onto lanyards by whoever runs
/// the event; a holder never views or downloads their own. That is a product decision, not an omission —
/// do not add a "my badge" path here without changing it deliberately.</para>
/// </summary>
public interface IIdCardService
{
    /// <summary>Everyone at an event who can be given a badge: ticket holders and accepted staff
    /// assignments. What the organizer picks from before printing.</summary>
    Task<ServiceResult<IReadOnlyList<BadgeRecipient>>> ListRecipientsAsync(
        Guid eventId, Guid actorId, bool isAdmin, CancellationToken ct = default);

    /// <summary>Issues real ID cards: creates an <c>id_cards</c> row per recipient, allocates its card
    /// number and verify code, renders the artefacts and stores them (D-362).
    ///
    /// <para><b>This is what makes a badge a record rather than a printout.</b> An issued card has an
    /// identity that outlives the PDF — it can be looked up, revoked, reissued, and verified from the code
    /// on its face. Rendering straight to a download, which is what this replaces, produced paper that the
    /// platform had no knowledge of the moment it was saved.</para>
    ///
    /// <para><b>Idempotent per (event, holder).</b> Re-running regenerates the artefacts and keeps the
    /// existing card number, so reprinting a damaged badge does not silently issue a second identity for
    /// the same person. A genuine reissue — after a loss — is a separate act that takes a new number
    /// (D-331), and is not this.</para></summary>
    Task<ServiceResult<BadgeIssueReport>> GenerateAsync(
        Guid eventId, Guid actorId, bool isAdmin, BadgeIssueRequest request, CancellationToken ct = default);

    /// <summary>Revokes an issued badge (D-386) — the write that <c>IdCard.IsRevoked</c> waited for.
    ///
    /// <para><b>What this does and does not stop.</b> It marks the <i>document</i> dead, so the public
    /// verification lookup reports it revoked rather than current (D-331 — a revoked card stays visible
    /// precisely so the revocation can be confirmed; hiding it would leave a verifier unable to tell a
    /// revoked badge from a forged one). It does <b>not</b> close a door on its own: an attendee's entry
    /// credential is their ticket and a staff member's is their assignment, so stopping someone entering
    /// means voiding the ticket or removing the assignment. Pretending otherwise would be the dangerous
    /// reading, which is why it is written down here.</para>
    ///
    /// <para>Undone by re-issuing, not by un-revoking: a reissue after a loss takes a new card number
    /// (D-331), and that is what lets a revoked card be told apart from its replacement.</para></summary>
    Task<ServiceResult<IssuedCard>> RevokeAsync(
        Guid eventId, Guid actorId, bool isAdmin, Guid recipientUserId, string? reason,
        CancellationToken ct = default);

    /// <summary>One badge, print-ready.</summary>
    Task<ServiceResult<byte[]>> RenderOneAsync(
        Guid eventId, Guid actorId, bool isAdmin, Guid recipientUserId, string sizeKey,
        CancellationToken ct = default);

    /// <summary>Every selected badge on shared A4 pages with cut guides — the pre-print run.
    ///
    /// <para>A combined sheet rather than one file per person: the workflow this serves is "print the
    /// lanyards for Saturday", which is one print job. A folder of two hundred PDFs is the same job with
    /// two hundred extra steps.</para></summary>
    Task<ServiceResult<byte[]>> RenderSheetAsync(
        Guid eventId, Guid actorId, bool isAdmin, BadgeSheetRequest request, CancellationToken ct = default);
}

/// <summary>Which side of the event a badge is for. The two differ in what they assert and in what their
/// QR encodes, not in how they are rendered (D-331).</summary>
public enum BadgeKind { Attendee, Staff }

/// <summary>What a staff role authorises on site.
///
/// <para>Derived from the role rather than stored, because <c>EventAssignment</c> has no access-level
/// column and inventing one would put a second, drifting answer next to the role that already decides
/// this. It lives here, rather than privately inside the badge service, because two readers now need the
/// same answer (D-385): the badge <b>prints</b> the level and the gate <b>reports</b> it to the marshal
/// scanning that badge. Two copies of this switch would eventually disagree, and the failure would be a
/// door opened on an authority the lanyard does not claim.</para>
///
/// <para><b>Every role the platform can actually issue is mapped (D-386).</b> The first version of this
/// switch named <c>manager</c>, <c>vendor</c> and <c>speaker</c> — none of which
/// <c>EventAssignmentService.ValidRoles</c> can produce. Only <c>Volunteer</c> and <c>Judge</c>
/// intersected, so every other crew member printed <c>Staff</c> and the colour band a marshal is supposed
/// to read across a room never varied. The arms below cover all fourteen valid roles; the unmatched names
/// are kept because a free-text <c>CustomRole</c> may legitimately be "Vendor" or "Speaker".</para>
///
/// <para><b>The bands answer "where may this person go", not "how senior are they".</b> That is the
/// question a door asks, and it is why <c>Security</c> outranks <c>Registration Desk</c> here while
/// neither has any authority in the permission model — this is signage, and
/// <see cref="IEventAuthority"/> remains the only thing that decides what anyone may *do*.</para>
///
/// <para>An unknown role falls to <c>Staff</c>, the narrowest badge that still admits someone to the crew
/// areas. Defaulting the other way would let a typo print an all-access lanyard.</para></summary>
public static class StaffAccess
{
    public static string LevelFor(string role) => role.Trim().ToLowerInvariant() switch
    {
        // Run the event, and are expected anywhere in the venue.
        "owner" or "manager" or "organizer" or "stage manager" or "host" or "security" => "All Access",

        // Work the stage, the green room and the press pit — not operations or the cash desk.
        "speaker" or "judge" or "performer" or "moderator" or "speaker coordinator"
            or "photographer" or "videographer" or "media team" or "technical team" => "Backstage",

        // Not in ValidRoles; reachable through a CustomRole, which is exactly how a stall is staffed.
        "sponsor" or "vendor" or "exhibitor" => "Vendor",

        "volunteer" => "Volunteer",

        // Registration Desk, Support Team, Custom, and anything this build has not seen.
        _ => "Staff",
    };
}

/// <param name="SizeKey">The size the cards are rendered at. Stored artefacts are size-specific, so
/// changing it and regenerating replaces them.</param>
/// <param name="UserIds">Null or empty issues to everyone matching <paramref name="Kinds"/>.</param>
public sealed record BadgeIssueRequest(
    string SizeKey,
    IReadOnlyList<BadgeKind> Kinds,
    IReadOnlyList<Guid>? UserIds = null);

/// <param name="Issued">Cards created for the first time.</param>
/// <param name="Regenerated">Existing cards whose artefacts were re-rendered, keeping their number.</param>
public sealed record BadgeIssueReport(int Issued, int Regenerated);

/// <param name="Kind">Attendee or staff.</param>
/// <param name="Subtitle">Ticket tier for an attendee, role for a staff member — the line under the name.</param>
/// <param name="AccessLevel">Staff only. What the badge authorises on site.</param>
/// <param name="PhotoKey">Storage key of the holder's avatar, or null. A badge without a photo is normal
/// and prints a monogram rather than an empty frame.</param>
/// <param name="QrPayload">Exactly what the badge's QR encodes. Resolved here, at the one place that knows
/// which scheme applies to which kind, so no caller has to choose — see <see cref="BadgeKind"/>.</param>
public sealed record BadgeRecipient(
    Guid UserId,
    string Name,
    BadgeKind Kind,
    string? Subtitle,
    string? AccessLevel,
    string? PhotoKey,
    string QrPayload,
    /// <summary>The issued card, when one exists. Null means this person has no <c>id_cards</c> row yet —
    /// which is what the console shows as "not issued", and is a different state from "issued and
    /// revoked".</summary>
    IssuedCard? Card = null);

/// <param name="CardNumber">The human-facing number printed on the card.</param>
/// <param name="VerifyCode">The code its QR-adjacent lookup resolves. Ten characters, same shape as a
/// certificate's, so one verification convention covers both (D-331).</param>
public sealed record IssuedCard(
    Guid Id,
    string CardNumber,
    string VerifyCode,
    string Status,
    bool IsRevoked,
    DateTime? GeneratedAt);

/// <param name="SizeKey">One of <see cref="BadgeSize.All"/>. The organizer picks per print run — badge
/// stock differs by event and by printer, so this is a choice rather than a constant.</param>
/// <param name="UserIds">Null or empty prints everyone matching <paramref name="Kinds"/>.</param>
public sealed record BadgeSheetRequest(
    string SizeKey,
    IReadOnlyList<BadgeKind> Kinds,
    IReadOnlyList<Guid>? UserIds = null);

/// <summary>A physical badge size in millimetres.
///
/// <para>Millimetres because that is what the renderer measures (D-361); the inch names are what badge
/// stock is actually sold as, so both are carried and neither is derived at a call site.</para></summary>
public sealed record BadgeSize(string Key, string Label, double WidthMm, double HeightMm)
{
    /// <summary>Standard lanyard insert. The common case.</summary>
    public static readonly BadgeSize Lanyard = new("lanyard", "Lanyard badge — 3.5 × 5.5 in", 88.9, 139.7);

    /// <summary>Large badge on 4×6 photo stock. More room for a QR someone scans at arm's length.</summary>
    public static readonly BadgeSize Large = new("large", "Large badge — 4 × 6 in", 101.6, 152.4);

    /// <summary>CR80, the credit-card size a PVC card printer takes. Landscape, and much tighter.</summary>
    public static readonly BadgeSize Card = new("card", "PVC card — CR80, 85.6 × 54 mm", 85.6, 54.0);

    public static readonly IReadOnlyList<BadgeSize> All = [Lanyard, Large, Card];

    public static BadgeSize? FromKey(string? key) =>
        All.FirstOrDefault(s => string.Equals(s.Key, key?.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>CR80 is wider than tall and cannot carry the stacked portrait layout legibly.</summary>
    public bool IsLandscape => WidthMm > HeightMm;
}
