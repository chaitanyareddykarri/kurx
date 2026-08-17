using Kurx.Application.Abstractions;

namespace Kurx.Infrastructure.IdCards;

/// <summary>
/// Turns a recipient plus the event's design into a <see cref="CertificateDocument"/> the existing
/// renderer can paint (D-362).
///
/// <para><b>Why there is no badge renderer.</b> <see cref="ICertificateDocumentRenderer"/> already takes a
/// page in millimetres (D-361) and paints <c>text</c>, <c>image</c> and <c>qrcode</c> elements positioned
/// as percentages. A badge is a small page with those elements on it. A second renderer would have meant a
/// second font stack, a second QR path and a second set of rounding bugs, producing the same output — so
/// this file is a layout, not an engine.</para>
///
/// <para><b>Placements come from the editor.</b> When the design carries
/// <see cref="IdCardTemplateSpec.Fields"/>, those are the layout. When it does not, <see cref="Defaults"/>
/// supplies the built-in one — which is also what the editor loads on first open, so an organiser starts
/// from a working card and drags from there rather than from an empty page.</para>
/// </summary>
public static class BadgeLayout
{
    // Field keys the renderer substitutes values for. IdCardField.Keys is the editor-facing set; these
    // are the render-data keys, and the two agree by construction because these constants define both.
    public const string FieldName = IdCardField.Name;
    public const string FieldSubtitle = IdCardField.Subtitle;
    public const string FieldAccess = IdCardField.AccessLevel;
    public const string FieldEvent = IdCardField.EventName;
    public const string FieldEventDate = IdCardField.EventDate;
    public const string FieldCardNumber = IdCardField.CardNumber;

    /// <summary>Storage-key slots. Constants rather than real keys so the layout never has to know where
    /// an avatar or a logo lives.</summary>
    public const string PhotoSlot = "photo";
    public const string LogoSlot = "logo";
    public const string BackgroundSlot = "background";

    public static CertificateDocument Build(BadgeRecipient recipient, BadgeSize size) =>
        Build(recipient, size, null, null);

    /// <param name="background">The uploaded artwork's bytes, resolved by the caller. Passed on the
    /// document rather than in the render data because that is where the renderer paints it — beneath
    /// every element, <c>contain</c>, so nothing of the design is cropped.</param>
    public static CertificateDocument Build(
        BadgeRecipient recipient, BadgeSize size, IdCardTemplateSpec? spec, byte[]? background = null)
    {
        var s = (spec ?? IdCardTemplateSpec.Default).Sanitised();
        var isStaff = recipient.Kind == BadgeKind.Staff;
        var accent = s.AccentColor ?? (isStaff ? AccessBandColor(recipient.AccessLevel) : "#111827");
        var fields = s.Fields is { Count: > 0 } ? s.Fields : Defaults(size, isStaff);

        var elements = new List<CertificateRenderElement>();

        foreach (var f in fields.Where(f => f.Enabled))
        {
            // Staff-only fields are skipped on an attendee card rather than printing an empty stripe or a
            // blank line — an attendee has no access level, and a band saying nothing is worse than none.
            if (!isStaff && f.Key is IdCardField.AccessLevel) continue;
            if (f.Key == IdCardField.Logo && s.LogoKey is null) continue;

            elements.Add(f.Key switch
            {
                IdCardField.Photo => Image(PhotoSlot, f),
                IdCardField.Logo => Image(LogoSlot, f),
                IdCardField.Qr => Qr(f),
                IdCardField.AccentBand => Band(f, f.Color ?? accent),
                _ => Text(f, s, accent, isStaff),
            });
        }

        return new CertificateDocument("custom", background, elements, size.WidthMm, size.HeightMm);
    }

    /// <summary>The built-in layout, expressed as placements so the editor can load and move them. Portrait
    /// and landscape differ in arrangement, not in which fields exist: CR80 is wider than tall and cannot
    /// carry the stacked portrait layout legibly.</summary>
    public static IReadOnlyList<IdCardField> Defaults(BadgeSize size, bool isStaff) =>
        size.IsLandscape ? LandscapeDefaults(isStaff) : PortraitDefaults(isStaff);

    private static IReadOnlyList<IdCardField> PortraitDefaults(bool isStaff) =>
    [
        new(IdCardField.AccentBand, 0, 0, 100, 11, ZOrder: 0),
        new(IdCardField.Logo, 4, 1.8, 14, 7.4, ZOrder: 1),
        new(IdCardField.EventName, 20, 2.2, 76, 6.5, 11, "#ffffff", "center", "bold", 1),
        new(IdCardField.Photo, 27, 14, 46, 26, ZOrder: 1),
        new(IdCardField.Name, 4, 42, 92, 8, 16, null, "center", "bold", 1),
        new(IdCardField.Subtitle, 4, 50.5, 92, 5.5, 10, "#4b5563", "center", "normal", 1),
        new(IdCardField.AccessLevel, 14, 58, 72, 7, 10, "#ffffff", "center", "bold", 1, isStaff),
        new(IdCardField.Qr, 33, isStaff ? 68 : 62, 34, 22, ZOrder: 2),
        new(IdCardField.CardNumber, 4, 92, 92, 4.5, 7, "#6b7280", "center", "normal", 1),
        new(IdCardField.EventDate, 4, 96, 92, 4, 7, "#9ca3af", "center", "normal", 1),
    ];

    private static IReadOnlyList<IdCardField> LandscapeDefaults(bool isStaff) =>
    [
        new(IdCardField.AccentBand, 0, 0, 100, 15, ZOrder: 0),
        new(IdCardField.Logo, 3, 2.5, 12, 10, ZOrder: 1),
        new(IdCardField.EventName, 17, 3, 80, 9, 8, "#ffffff", "center", "bold", 1),
        new(IdCardField.Photo, 4, 22, 26, 46, ZOrder: 1),
        new(IdCardField.Name, 33, 22, 42, 11, 11, null, "left", "bold", 1),
        new(IdCardField.Subtitle, 33, 34, 42, 8, 7.5, "#4b5563", "left", "normal", 1),
        new(IdCardField.AccessLevel, 33, 43, 42, 8, 8, null, "left", "bold", 1, isStaff),
        new(IdCardField.Qr, 77, 22, 20, 46, ZOrder: 2),
        new(IdCardField.CardNumber, 4, 88, 92, 6, 6, "#6b7280", "left", "normal", 1),
    ];

    /// <summary>Staff badges carry a colour band so a marshal can read authority across a room without
    /// reading words. Used only when the design has not set its own accent.</summary>
    private static string AccessBandColor(string? accessLevel) => accessLevel?.Trim().ToLowerInvariant() switch
    {
        "all access" or "all-access" => "#7f1d1d",
        "backstage" => "#78350f",
        "vendor" => "#1e3a8a",
        "volunteer" => "#14532d",
        _ => "#1f2937",
    };

    // ── Element constructors. Named arguments throughout: CertificateRenderElement is positional and
    // twenty-odd fields wide, and positional calls to it are unreadable and easy to shift by one. ──────

    private static CertificateRenderElement Band(IdCardField f, string color) =>
        new(Kind: "text", FieldKey: null, StaticText: "",
            X: f.X, Y: f.Y, Width: f.Width, Height: f.Height, Rotation: 0, ZOrder: f.ZOrder, IsMasking: false,
            BackgroundColor: color, ImageKey: null, FontFamily: null, FontSizePt: null, FontWeight: null,
            Color: null, HorizontalAlignment: "center", VerticalAlignment: "middle");

    private static CertificateRenderElement Text(
        IdCardField f, IdCardTemplateSpec s, string accent, bool isStaff)
    {
        // The access level sits on its own band, so it needs the band drawn behind it.
        var colour = f.Color
            ?? (f.Key == IdCardField.Name ? s.TextColor : null)
            ?? "#111827";

        return new(Kind: "dynamicfield", FieldKey: f.Key, StaticText: null,
            X: f.X, Y: f.Y, Width: f.Width, Height: f.Height, Rotation: 0, ZOrder: f.ZOrder,
            IsMasking: false,
            // An access-level field paints its own band behind the text: it is one element in the editor,
            // and splitting it into two would let someone drag the words off their own stripe.
            BackgroundColor: f.Key == IdCardField.AccessLevel && isStaff ? accent : null,
            ImageKey: null, FontFamily: null, FontSizePt: f.FontSizePt, FontWeight: f.Weight ?? "normal",
            Color: colour, HorizontalAlignment: f.Align, VerticalAlignment: "middle");
    }

    private static CertificateRenderElement Image(string slot, IdCardField f) =>
        new(Kind: "image", FieldKey: null, StaticText: null,
            X: f.X, Y: f.Y, Width: f.Width, Height: f.Height, Rotation: 0, ZOrder: f.ZOrder, IsMasking: false,
            BackgroundColor: null, ImageKey: slot, FontFamily: null, FontSizePt: null, FontWeight: null,
            Color: null, HorizontalAlignment: "center", VerticalAlignment: "middle");

    private static CertificateRenderElement Qr(IdCardField f) =>
        new(Kind: "qrcode", FieldKey: null, StaticText: null,
            X: f.X, Y: f.Y, Width: f.Width, Height: f.Height, Rotation: 0, ZOrder: f.ZOrder, IsMasking: false,
            BackgroundColor: null, ImageKey: null, FontFamily: null, FontSizePt: null, FontWeight: null,
            Color: null, HorizontalAlignment: "center", VerticalAlignment: "middle");
}
