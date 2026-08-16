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
    string QrPayload);

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
