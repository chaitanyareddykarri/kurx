using System.Text.Json;
using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Infrastructure.IdCards;

/// <summary>
/// The ID card design editor's server half (D-362) — one <c>DesignTemplate</c> per event, with
/// <c>Kind = TemplateKind.IdCard</c>.
///
/// <para><b>No new table.</b> <c>design_templates</c> already carries <c>Kind</c>, <c>DocumentJson</c>,
/// <c>EventId</c> and <c>AccentColor</c>, and <c>TemplateKind</c> already had an <c>IdCard</c> value: the
/// schema anticipated this design existing. Adding a table beside it would have meant two answers to
/// "what does this event's card look like".</para>
/// </summary>
public class IdCardTemplateService(
    KurxDbContext db,
    IEventAuthority authority,
    ICertificateDocumentRenderer renderer,
    IStorage storage) : IIdCardTemplateService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Preview at screen resolution, not print: it is shown in a browser panel while the
    /// organiser drags a colour around, and 300dpi would make every keystroke a slow download.</summary>
    private const int PreviewDpi = 96;

    public async Task<ServiceResult<IdCardTemplateSpec>> GetAsync(
        Guid eventId, Guid actorId, bool isAdmin, CancellationToken ct = default)
    {
        var gate = await RequireManagerAsync(eventId, actorId, isAdmin, ct);
        if (gate is not null) return ServiceResult<IdCardTemplateSpec>.Fail(gate);

        var row = await LoadAsync(eventId, ct);
        return ServiceResult<IdCardTemplateSpec>.Success(Deserialise(row?.PlacementsJson));
    }

    public async Task<ServiceResult<IdCardTemplateSpec>> SaveAsync(
        Guid eventId, Guid actorId, bool isAdmin, IdCardTemplateSpec spec, CancellationToken ct = default)
    {
        var gate = await RequireManagerAsync(eventId, actorId, isAdmin, ct);
        if (gate is not null) return ServiceResult<IdCardTemplateSpec>.Fail(gate);

        // Sanitised before storage, not just before rendering: an unchecked colour written to the row
        // would be handed to every later reader, including ones that have not been written yet.
        var clean = spec.Sanitised();

        // Both keys are storage paths the renderer will read, so both must be inside this event's own
        // prefix. Accepting an arbitrary one would make saving a design a read primitive over the bucket.
        if (clean.LogoKey is not null && !AssetKeyBelongsToEvent(clean.LogoKey, eventId))
            return ServiceResult<IdCardTemplateSpec>.Fail("invalid_logo_key");
        if (clean.BackgroundKey is not null && !AssetKeyBelongsToEvent(clean.BackgroundKey, eventId))
            return ServiceResult<IdCardTemplateSpec>.Fail("invalid_background_key");

        var row = await LoadAsync(eventId, ct);
        if (row is null)
        {
            var orgId = await db.Events.AsNoTracking()
                .Where(e => e.Id == eventId).Select(e => e.RepresentingOrgId).FirstAsync(ct);

            row = new DesignTemplate
            {
                EventId = eventId,
                OrgId = orgId,
                Kind = TemplateKind.IdCard,
                Mode = TemplateMode.Custom,
                Name = "Event ID card",
                IsActive = true,
            };
            db.DesignTemplates.Add(row);
        }

        row.PlacementsJson = JsonSerializer.Serialize(clean, Json);
        row.AccentColor = clean.AccentColor;

        db.AuditLogs.Add(new AuditLog
        {
            ActorType = "user", ActorId = actorId,
            Action = "idcard.template.save", Entity = "events", EntityId = eventId,
        });

        await db.SaveChangesAsync(ct);
        return ServiceResult<IdCardTemplateSpec>.Success(clean);
    }

    public async Task<ServiceResult<byte[]>> PreviewAsync(
        Guid eventId, Guid actorId, bool isAdmin, IdCardTemplateSpec spec, BadgeKind kind,
        CancellationToken ct = default)
    {
        var gate = await RequireManagerAsync(eventId, actorId, isAdmin, ct);
        if (gate is not null) return ServiceResult<byte[]>.Fail(gate);

        var clean = spec.Sanitised();
        var size = BadgeSize.FromKey(clean.SizeKey)!;   // Sanitised() guarantees a known key

        var ev = await db.Events.AsNoTracking()
            .Where(e => e.Id == eventId).Select(e => new { e.Title, e.StartsAt }).FirstAsync(ct);

        // Sample values, deliberately long-ish: a preview built from "Jo" would hide the truncation an
        // organiser is about to ship on two hundred real names.
        var sample = new BadgeRecipient(
            Guid.Empty, "Priya Raghunathan", kind,
            kind == BadgeKind.Staff ? "Volunteer" : "General Admission",
            kind == BadgeKind.Staff ? "All Access" : null,
            null, "preview");

        var values = new Dictionary<string, string>
        {
            [BadgeLayout.FieldName] = sample.Name,
            [BadgeLayout.FieldSubtitle] = sample.Subtitle ?? "",
            [BadgeLayout.FieldAccess] = sample.AccessLevel ?? "",
            [BadgeLayout.FieldEvent] = ev.Title,
            [BadgeLayout.FieldEventDate] = ev.StartsAt.ToString("dd MMM yyyy"),
            [BadgeLayout.FieldCardNumber] = "KRX-00001",
        };

        var images = new Dictionary<string, byte[]>();
        await TryAddImageAsync(images, BadgeLayout.LogoSlot, clean.LogoKey, ct);
        var background = await TryReadAsync(clean.BackgroundKey, ct);

        var png = await renderer.RenderPngAsync(
            BadgeLayout.Build(sample, size, clean, background),
            new CertificateRenderData(values, images, "preview-qr-payload"), PreviewDpi, ct);

        return ServiceResult<byte[]>.Success(png);
    }

    public async Task<ServiceResult<PresignedUpload>> PresignAssetAsync(
        Guid eventId, Guid actorId, bool isAdmin, string contentType, string? purpose,
        CancellationToken ct = default)
    {
        var gate = await RequireManagerAsync(eventId, actorId, isAdmin, ct);
        if (gate is not null) return ServiceResult<PresignedUpload>.Fail(gate);

        // Raster only. An SVG logo is a script-capable document, and this one is fetched by the renderer
        // and served back inside a page.
        var extension = contentType.Trim().ToLowerInvariant() switch
        {
            "image/png" => "png",
            "image/jpeg" or "image/jpg" => "jpg",
            "image/webp" => "webp",
            _ => null,
        };
        if (extension is null) return ServiceResult<PresignedUpload>.Fail("unsupported_content_type");

        var folder = purpose?.Trim().ToLowerInvariant() == "background" ? "background" : "logo";
        var key = $"events/{eventId}/id-cards/{folder}/{Guid.NewGuid():N}.{extension}";
        // Artwork is the whole card and is legitimately larger than a logo mark.
        var ceiling = folder == "background" ? MaxBackgroundBytes : MaxLogoBytes;
        var presign = await storage.PresignPutAsync(key, contentType, ceiling, ct);
        return ServiceResult<PresignedUpload>.Success(presign);
    }

    public async Task<ServiceResult<string>> AssetUrlAsync(
        Guid eventId, Guid actorId, bool isAdmin, string key, CancellationToken ct = default)
    {
        var gate = await RequireManagerAsync(eventId, actorId, isAdmin, ct);
        if (gate is not null) return ServiceResult<string>.Fail(gate);

        if (!AssetKeyBelongsToEvent(key, eventId)) return ServiceResult<string>.Fail("invalid_asset_key");
        if (!await storage.ExistsAsync(key, ct)) return ServiceResult<string>.Fail("not_found");

        return ServiceResult<string>.Success(await storage.PresignGetAsync(key, ct: ct));
    }

    /// <summary>2 MB. A logo printed at 14% of a badge's width needs nothing larger, and the ceiling is
    /// what stops the presign being a general-purpose upload slot.</summary>
    private const long MaxLogoBytes = 2 * 1024 * 1024;

    /// <summary>8 MB. Artwork covers the whole card and is often exported at print resolution.</summary>
    private const long MaxBackgroundBytes = 8 * 1024 * 1024;

    // ── Internals ───────────────────────────────────────────────────────────────────────────────────

    private Task<DesignTemplate?> LoadAsync(Guid eventId, CancellationToken ct) =>
        db.DesignTemplates.FirstOrDefaultAsync(
            t => t.EventId == eventId && t.Kind == TemplateKind.IdCard && t.IsActive, ct);

    private static IdCardTemplateSpec Deserialise(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "{}") return IdCardTemplateSpec.Default;
        try
        {
            return JsonSerializer.Deserialize<IdCardTemplateSpec>(json, Json)?.Sanitised()
                ?? IdCardTemplateSpec.Default;
        }
        catch (JsonException)
        {
            // A design that cannot be read must not take the badge page down with it: the default still
            // prints a correct card, which is the outcome that matters at a print deadline.
            return IdCardTemplateSpec.Default;
        }
    }

    private static bool AssetKeyBelongsToEvent(string key, Guid eventId) =>
        key.StartsWith($"events/{eventId}/id-cards/", StringComparison.Ordinal);

    private async Task TryAddImageAsync(
        Dictionary<string, byte[]> images, string slot, string? key, CancellationToken ct)
    {
        if (await TryReadAsync(key, ct) is { } bytes) images[slot] = bytes;
    }

    /// <summary>Reads an asset, or null. A preview missing its artwork beats a preview that fails —
    /// the organiser can see and fix a blank background; they cannot fix a spinner.</summary>
    private async Task<byte[]?> TryReadAsync(string? key, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        try
        {
            return await storage.ExistsAsync(key, ct) ? await storage.GetAsync(key, ct) : null;
        }
        catch { return null; }
    }

    private async Task<string?> RequireManagerAsync(Guid eventId, Guid actorId, bool isAdmin, CancellationToken ct)
    {
        var access = await authority.ResolveAsync(actorId, eventId, isAdmin, ct);
        if (!access.EventExists || !access.HasStanding) return "not_found";
        return access.Can(EventPermission.ManageContent) ? null : "forbidden";
    }
}
