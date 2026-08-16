using Kurx.Application.Abstractions;

namespace Kurx.Infrastructure.IdCards;

/// <summary>
/// Turns a recipient into a <see cref="CertificateDocument"/> the existing renderer can paint (D-362).
///
/// <para><b>Why there is no badge renderer.</b> <see cref="ICertificateDocumentRenderer"/> already takes a
/// page in millimetres (D-361) and paints <c>text</c>, <c>image</c> and <c>qrcode</c> elements positioned
/// as percentages. A badge is a small page with those elements on it. Writing a second renderer would have
/// meant a second font stack, a second QR path and a second set of rounding bugs, to produce the same
/// output — so this file is a layout, not an engine.</para>
///
/// <para><b>Percentages, not millimetres, for the elements.</b> The same layout therefore lands correctly
/// on a 3.5×5.5in lanyard and an 85.6×54mm PVC card without a branch per size — only the page changes.
/// The one thing that genuinely cannot survive the change is the stacking: CR80 is wider than it is tall,
/// so it gets its own arrangement rather than a squashed portrait one.</para>
/// </summary>
public static class BadgeLayout
{
    // Field keys. Values are supplied per recipient by IdCardService; a key with no value renders as its
    // own placeholder (see CertificateRenderData), which is what makes a missing name visible on the
    // proof sheet instead of arriving as a blank lanyard.
    public const string FieldName = "holder_name";
    public const string FieldSubtitle = "subtitle";
    public const string FieldAccess = "access_level";
    public const string FieldEvent = "event_name";
    public const string FieldEventDate = "event_date";
    public const string FieldCardNumber = "card_number";

    /// <summary>Storage-key slot for the holder's photo. Constant rather than the real key so the layout
    /// never has to know where an avatar lives.</summary>
    public const string PhotoSlot = "photo";

    /// <summary>Staff badges carry a colour band so a marshal can read authority across a room without
    /// reading words. Attendee badges deliberately do not — every attendee holds the same authority, and
    /// a band that always says the same thing is decoration that costs contrast.</summary>
    private static string AccessBandColor(string? accessLevel) => accessLevel?.Trim().ToLowerInvariant() switch
    {
        "all access" or "all-access" => "#7f1d1d",
        "backstage" => "#78350f",
        "vendor" => "#1e3a8a",
        "volunteer" => "#14532d",
        _ => "#1f2937",
    };

    public static CertificateDocument Build(BadgeRecipient recipient, BadgeSize size) =>
        size.IsLandscape ? Landscape(recipient, size) : Portrait(recipient, size);

    /// <summary>Lanyard and large-badge layout: event band, photo, name, role, QR, card number.</summary>
    private static CertificateDocument Portrait(BadgeRecipient r, BadgeSize size)
    {
        var isStaff = r.Kind == BadgeKind.Staff;
        var elements = new List<CertificateRenderElement>
        {
            Band(0, 0, 100, 11, isStaff ? AccessBandColor(r.AccessLevel) : "#111827"),
            Text(FieldEvent, 4, 2.2, 92, 6.5, 11, "bold", "#ffffff", "center"),

            Image(PhotoSlot, 27, 14, 46, 26),
            Text(FieldName, 4, 42, 92, 8, 16, "bold", "#111827", "center"),
            Text(FieldSubtitle, 4, 50.5, 92, 5.5, 10, "normal", "#4b5563", "center"),
        };

        if (isStaff)
        {
            elements.Add(Band(14, 58, 72, 7, AccessBandColor(r.AccessLevel)));
            elements.Add(Text(FieldAccess, 14, 59.2, 72, 5, 10, "bold", "#ffffff", "center"));
        }

        elements.Add(Qr(33, isStaff ? 68 : 62, 34, 22));
        elements.Add(Text(FieldCardNumber, 4, 92, 92, 4.5, 7, "normal", "#6b7280", "center"));
        elements.Add(Text(FieldEventDate, 4, 96, 92, 4, 7, "normal", "#9ca3af", "center"));

        return new CertificateDocument("custom", null, elements, size.WidthMm, size.HeightMm);
    }

    /// <summary>CR80: photo and QR flank the text because there is no vertical room to stack them.</summary>
    private static CertificateDocument Landscape(BadgeRecipient r, BadgeSize size)
    {
        var isStaff = r.Kind == BadgeKind.Staff;
        var elements = new List<CertificateRenderElement>
        {
            Band(0, 0, 100, 15, isStaff ? AccessBandColor(r.AccessLevel) : "#111827"),
            Text(FieldEvent, 3, 3, 94, 9, 8, "bold", "#ffffff", "center"),

            Image(PhotoSlot, 4, 22, 26, 46),
            Text(FieldName, 33, 22, 42, 11, 11, "bold", "#111827", "left"),
            Text(FieldSubtitle, 33, 34, 42, 8, 7.5, "normal", "#4b5563", "left"),
            Qr(77, 22, 20, 46),
        };

        if (isStaff)
            elements.Add(Text(FieldAccess, 33, 43, 42, 8, 8, "bold", AccessBandColor(r.AccessLevel), "left"));

        elements.Add(Text(FieldCardNumber, 4, 88, 92, 6, 6, "normal", "#6b7280", "left"));

        return new CertificateDocument("custom", null, elements, size.WidthMm, size.HeightMm);
    }

    // ── Element constructors. Named arguments throughout: CertificateRenderElement is positional and
    // twenty-odd fields wide, and positional calls to it are unreadable and easy to shift by one. ──────

    private static CertificateRenderElement Band(double x, double y, double w, double h, string color) =>
        new(Kind: "text", FieldKey: null, StaticText: "",
            X: x, Y: y, Width: w, Height: h, Rotation: 0, ZOrder: 0, IsMasking: false,
            BackgroundColor: color, ImageKey: null, FontFamily: null, FontSizePt: null, FontWeight: null,
            Color: null, HorizontalAlignment: "center", VerticalAlignment: "middle");

    private static CertificateRenderElement Text(
        string fieldKey, double x, double y, double w, double h,
        double sizePt, string weight, string color, string align) =>
        new(Kind: "dynamicfield", FieldKey: fieldKey, StaticText: null,
            X: x, Y: y, Width: w, Height: h, Rotation: 0, ZOrder: 1, IsMasking: false,
            BackgroundColor: null, ImageKey: null, FontFamily: null, FontSizePt: sizePt, FontWeight: weight,
            Color: color, HorizontalAlignment: align, VerticalAlignment: "middle");

    private static CertificateRenderElement Image(string key, double x, double y, double w, double h) =>
        new(Kind: "image", FieldKey: null, StaticText: null,
            X: x, Y: y, Width: w, Height: h, Rotation: 0, ZOrder: 1, IsMasking: false,
            BackgroundColor: null, ImageKey: key, FontFamily: null, FontSizePt: null, FontWeight: null,
            Color: null, HorizontalAlignment: "center", VerticalAlignment: "middle");

    private static CertificateRenderElement Qr(double x, double y, double w, double h) =>
        new(Kind: "qrcode", FieldKey: null, StaticText: null,
            X: x, Y: y, Width: w, Height: h, Rotation: 0, ZOrder: 2, IsMasking: false,
            BackgroundColor: null, ImageKey: null, FontFamily: null, FontSizePt: null, FontWeight: null,
            Color: null, HorizontalAlignment: "center", VerticalAlignment: "middle");
}
