namespace Kurx.Application.Abstractions;

/// <summary>
/// The editable design behind an event's ID cards (D-362).
///
/// <para><b>Stored on <c>DesignTemplate</c> with <c>Kind = TemplateKind.IdCard</c></b> — the entity the
/// schema already anticipated for exactly this, rather than a new table. One design per event; a card
/// rendered without one falls back to <see cref="IdCardTemplateSpec.Default"/>, so badges print correctly
/// before anyone opens the editor.</para>
///
/// <para><b>Why a structured spec and not free placement.</b> An ID card is a fixed-zone document — photo,
/// name, role, access band, QR — read at a glance by someone standing at a door. What an organiser
/// actually needs to change is which of those print, in what colours, under whose logo. Free placement
/// would let them produce a badge with the QR behind the photo, and would mean a second canvas editor
/// beside the certificate module's.</para>
/// </summary>
public interface IIdCardTemplateService
{
    /// <summary>The event's design, or the default when none has been saved.</summary>
    Task<ServiceResult<IdCardTemplateSpec>> GetAsync(
        Guid eventId, Guid actorId, bool isAdmin, CancellationToken ct = default);

    Task<ServiceResult<IdCardTemplateSpec>> SaveAsync(
        Guid eventId, Guid actorId, bool isAdmin, IdCardTemplateSpec spec, CancellationToken ct = default);

    /// <summary>Renders a sample badge from an <b>unsaved</b> spec, through the same renderer that prints
    /// the real thing. The preview matching the output is true by construction rather than by two code
    /// paths agreeing to stay in step.</summary>
    Task<ServiceResult<byte[]>> PreviewAsync(
        Guid eventId, Guid actorId, bool isAdmin, IdCardTemplateSpec spec, BadgeKind kind,
        CancellationToken ct = default);

    /// <summary>A presigned upload for the card's artwork or logo. The key it returns is what
    /// <see cref="IdCardTemplateSpec.BackgroundKey"/> or <see cref="IdCardTemplateSpec.LogoKey"/> holds.</summary>
    /// <param name="purpose"><c>background</c> or <c>logo</c> (the default). They differ in size ceiling
    /// and in where the key lands, and both are checked against the event on save.</param>
    Task<ServiceResult<PresignedUpload>> PresignAssetAsync(
        Guid eventId, Guid actorId, bool isAdmin, string contentType, string? purpose,
        CancellationToken ct = default);

    /// <summary>A readable URL for an already-uploaded asset, so the editor's canvas can show the artwork
    /// the fields are being dragged over. The key is checked against the event first — this returns a
    /// signed URL, so an unchecked key would read anything in the bucket.</summary>
    Task<ServiceResult<string>> AssetUrlAsync(
        Guid eventId, Guid actorId, bool isAdmin, string key, CancellationToken ct = default);
}

/// <summary>
/// What an organiser can change about their event's ID cards.
///
/// <para>Defaults are the layout that shipped before the editor existed, so saving nothing and saving the
/// defaults produce the same badge.</para>
/// </summary>
/// <param name="AccentColor">The header band, and the access band when that is shown. Hex, e.g.
/// <c>#111827</c>. Null uses the per-access-level defaults.</param>
/// <param name="LogoKey">Storage key of the organiser's logo. Null prints none — a stretched placeholder
/// is worse than no logo.</param>
/// <param name="BackgroundKey">Uploaded artwork painted beneath everything, <c>contain</c> so the whole
/// design is visible. This is the card the organiser actually designed; cropping it damages their work.</param>
/// <param name="Fields">Where each element sits, as percentages of the card. Null means "the built-in
/// layout for this size" — so a card prints correctly before anyone opens the editor, and the editor
/// opens on that layout rather than on an empty page.</param>
public sealed record IdCardTemplateSpec(
    string? AccentColor = null,
    string? TextColor = null,
    string? LogoKey = null,
    string? BackgroundKey = null,
    string SizeKey = "lanyard",
    IReadOnlyList<IdCardField>? Fields = null)
{
    public static readonly IdCardTemplateSpec Default = new();

    /// <summary>Guards everything that reaches a renderer. Colours and geometry are free-form on the wire,
    /// and an unchecked value would be written straight into a printed document.</summary>
    public IdCardTemplateSpec Sanitised() => this with
    {
        AccentColor = Hex(AccentColor),
        TextColor = Hex(TextColor),
        SizeKey = BadgeSize.FromKey(SizeKey)?.Key ?? BadgeSize.Lanyard.Key,
        Fields = Fields?.Where(f => IdCardField.Keys.Contains(f.Key)).Select(f => f.Sanitised()).ToList(),
    };

    internal static string? Hex(string? value)
    {
        var v = value?.Trim();
        if (string.IsNullOrEmpty(v)) return null;
        if (v.Length is not (4 or 7) || v[0] != '#') return null;
        return v[1..].All(Uri.IsHexDigit) ? v : null;
    }
}

/// <summary>One placed element on the card.
///
/// <para>Geometry is percentages of the page, exactly as the renderer measures — so the same placement
/// lands identically on an 88.9mm lanyard, a CR80 card, and the browser canvas the organiser dragged it
/// on. Nothing here carries a resolution.</para></summary>
/// <param name="Key">One of <see cref="Keys"/>. An unknown key is dropped rather than rendered as a
/// placeholder: it means a client sent something this build does not have.</param>
public sealed record IdCardField(
    string Key,
    double X, double Y, double Width, double Height,
    double? FontSizePt = null,
    string? Color = null,
    string Align = "center",
    string? Weight = null,
    int ZOrder = 1,
    bool Enabled = true)
{
    public const string Photo = "photo";
    public const string Logo = "logo";
    public const string Qr = "qr";
    public const string Name = "holder_name";
    public const string Subtitle = "subtitle";
    public const string AccessLevel = "access_level";
    public const string EventName = "event_name";
    public const string EventDate = "event_date";
    public const string CardNumber = "card_number";
    public const string AccentBand = "accent_band";

    public static readonly IReadOnlySet<string> Keys = new HashSet<string>
    {
        Photo, Logo, Qr, Name, Subtitle, AccessLevel, EventName, EventDate, CardNumber, AccentBand,
    };

    /// <summary>Clamps a placement onto the card. A field dragged off the page would otherwise render
    /// partly or wholly outside it, and the organiser would only find out on paper.</summary>
    public IdCardField Sanitised() => this with
    {
        X = Clamp(X), Y = Clamp(Y),
        Width = Math.Clamp(Width, 1, 100), Height = Math.Clamp(Height, 1, 100),
        FontSizePt = FontSizePt is { } f ? Math.Clamp(f, 4, 72) : null,
        Color = IdCardTemplateSpec.Hex(Color),
        Align = Align?.Trim().ToLowerInvariant() is "left" or "right" or "center" ? Align.Trim().ToLowerInvariant() : "center",
        Weight = Weight?.Trim().ToLowerInvariant() is "bold" or "normal" ? Weight.Trim().ToLowerInvariant() : null,
    };

    private static double Clamp(double v) => Math.Clamp(v, 0, 99);
}
