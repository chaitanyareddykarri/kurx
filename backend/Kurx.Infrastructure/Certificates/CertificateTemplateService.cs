using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp.Processing;

namespace Kurx.Infrastructure.Certificates;

/// <summary>
/// Certificate designs (D-344, Phase 3).
///
/// <para>See <see cref="ICertificateTemplateService"/> for the authority model. In short: an event
/// template is event content and resolves through <c>IEventAuthority</c>; a library template belongs to
/// no event and resolves through ownership.</para>
/// </summary>
public class CertificateTemplateService(
    KurxDbContext db,
    IEventAuthority authority,
    IStorage storage,
    ITextDetector textDetector) : ICertificateTemplateService
{
    /// <summary>What a design may be uploaded as. Narrower than the browser's idea of an image on
    /// purpose: the renderer must decode this at generation time, and an animated GIF or an AVIF it
    /// cannot read would fail long after the creator believed the design was finished.</summary>
    private static readonly string[] BackgroundContentTypes = ["image/png", "image/jpeg", "image/webp"];

    private const long MaxBackgroundBytes = 15 * 1024 * 1024;
    private const int MaxFields = 200;

    // ── Reads ───────────────────────────────────────────────────────────────────────────────────

    public async Task<ServiceResult<IReadOnlyList<CertificateTemplateView>>> ListForEventAsync(
        Guid userId, Guid eventId, bool isAdmin, CancellationToken ct = default)
    {
        var access = await authority.ResolveAsync(userId, eventId, isAdmin, ct);
        if (!access.EventExists) return ServiceResult<IReadOnlyList<CertificateTemplateView>>.Fail("not_found");
        if (!access.Can(EventPermission.ManageContent))
            return ServiceResult<IReadOnlyList<CertificateTemplateView>>.Fail("forbidden");

        var rows = await db.CertificateTemplates.AsNoTracking()
            .Where(t => t.EventId == eventId && t.Status != CertificateTemplateStatus.Archived)
            .OrderByDescending(t => t.UpdatedAt ?? t.CreatedAt)
            .ToListAsync(ct);

        return ServiceResult<IReadOnlyList<CertificateTemplateView>>.Success(await ProjectManyAsync(rows, ct));
    }

    public async Task<ServiceResult<IReadOnlyList<CertificateTemplateView>>> ListLibraryAsync(
        Guid userId, CancellationToken ct = default)
    {
        var rows = await db.CertificateTemplates.AsNoTracking()
            .Where(t => t.OwnerUserId == userId && t.EventId == null
                        && t.Status != CertificateTemplateStatus.Archived)
            .OrderByDescending(t => t.UpdatedAt ?? t.CreatedAt)
            .ToListAsync(ct);

        return ServiceResult<IReadOnlyList<CertificateTemplateView>>.Success(await ProjectManyAsync(rows, ct));
    }

    public async Task<ServiceResult<CertificateTemplateView>> GetAsync(
        Guid userId, Guid templateId, bool isAdmin, CancellationToken ct = default)
    {
        var (template, error) = await LoadAsync(userId, templateId, isAdmin, ct);
        return error is not null
            ? ServiceResult<CertificateTemplateView>.Fail(error)
            : ServiceResult<CertificateTemplateView>.Success(await ProjectAsync(template!, ct));
    }

    // ── Writes ──────────────────────────────────────────────────────────────────────────────────

    public async Task<ServiceResult<CertificateTemplateView>> CreateAsync(
        Guid userId, Guid? eventId, CertificateTemplateInput input, bool isAdmin, CancellationToken ct = default)
    {
        if (eventId is Guid id)
        {
            var access = await authority.ResolveAsync(userId, id, isAdmin, ct);
            if (!access.EventExists) return ServiceResult<CertificateTemplateView>.Fail("not_found");
            if (!access.Can(EventPermission.ManageContent))
                return ServiceResult<CertificateTemplateView>.Fail("forbidden");
        }

        var name = (input.Name ?? "").Trim();
        if (name.Length is < 1 or > 200) return ServiceResult<CertificateTemplateView>.Fail("invalid_name");

        if (ParsePageSize(input.PageSize) is not { } pageSize)
            return ServiceResult<CertificateTemplateView>.Fail("invalid_page_size");

        var template = new CertificateTemplate
        {
            EventId = eventId,
            OwnerUserId = userId,
            Name = name,
            PageSize = pageSize,
            Status = CertificateTemplateStatus.Draft,
        };
        db.CertificateTemplates.Add(template);
        await db.SaveChangesAsync(ct);

        return ServiceResult<CertificateTemplateView>.Success(await ProjectAsync(template, ct));
    }

    public async Task<ServiceResult<CertificateTemplateView>> UpdateAsync(
        Guid userId, Guid templateId, CertificateTemplateInput input, bool isAdmin, CancellationToken ct = default)
    {
        var (template, error) = await LoadAsync(userId, templateId, isAdmin, ct, tracked: true);
        if (error is not null) return ServiceResult<CertificateTemplateView>.Fail(error);

        if (input.Name is not null)
        {
            var name = input.Name.Trim();
            if (name.Length is < 1 or > 200) return ServiceResult<CertificateTemplateView>.Fail("invalid_name");
            template!.Name = name;
        }

        if (input.PageSize is not null)
        {
            if (ParsePageSize(input.PageSize) is not { } pageSize)
                return ServiceResult<CertificateTemplateView>.Fail("invalid_page_size");
            // The page size IS the coordinate space every percentage is relative to, so changing it moves
            // every field. That makes it a render-affecting change like any other.
            if (pageSize != template!.PageSize) await BumpVersionIfIssuedAsync(template, ct);
            template.PageSize = pageSize;
        }

        if (input.Status is not null)
        {
            var status = input.Status.Trim().ToLowerInvariant() switch
            {
                "draft" => (CertificateTemplateStatus?)CertificateTemplateStatus.Draft,
                "ready" => CertificateTemplateStatus.Ready,
                _ => null,
            };
            if (status is null) return ServiceResult<CertificateTemplateView>.Fail("invalid_status");
            // A template with no artwork is not something to issue from — the certificate would be text
            // on a blank page, which is never what an image-first design meant.
            if (status == CertificateTemplateStatus.Ready && template!.BackgroundStorageKey is null)
                return ServiceResult<CertificateTemplateView>.Fail("background_required");
            template!.Status = status.Value;
        }

        template!.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return ServiceResult<CertificateTemplateView>.Success(await ProjectAsync(template, ct));
    }

    public async Task<ServiceResult<PresignedUpload>> PresignBackgroundAsync(
        Guid userId, Guid templateId, string contentType, long maxBytes, bool isAdmin, CancellationToken ct = default)
    {
        var (template, error) = await LoadAsync(userId, templateId, isAdmin, ct);
        if (error is not null) return ServiceResult<PresignedUpload>.Fail(error);

        if (!BackgroundContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
            return ServiceResult<PresignedUpload>.Fail("invalid_content_type");

        // Keyed under the version this upload will become, so replacing artwork never overwrites the
        // bytes an already-issued certificate was rendered from.
        var nextVersion = await WillBumpAsync(template!, ct) ? template!.Version + 1 : template!.Version;
        var extension = contentType.ToLowerInvariant() switch
        {
            "image/png" => "png",
            "image/webp" => "webp",
            _ => "jpg",
        };

        var key = template.EventId is Guid eventId
            ? CertificateStorageKeys.TemplateBackground(eventId, template.Id, nextVersion, extension)
            : CertificateStorageKeys.LibraryTemplateBackground(template.OwnerUserId, template.Id, nextVersion, extension);

        return ServiceResult<PresignedUpload>.Success(
            await storage.PresignPutAsync(key, contentType, Math.Clamp(maxBytes, 1, MaxBackgroundBytes), ct));
    }

    public async Task<ServiceResult<CertificateTemplateView>> SetBackgroundAsync(
        Guid userId, Guid templateId, CertificateBackgroundInput input, bool isAdmin, CancellationToken ct = default)
    {
        var (template, error) = await LoadAsync(userId, templateId, isAdmin, ct, tracked: true);
        if (error is not null) return ServiceResult<CertificateTemplateView>.Fail(error);

        if (!BackgroundContentTypes.Contains(input.ContentType, StringComparer.OrdinalIgnoreCase))
            return ServiceResult<CertificateTemplateView>.Fail("invalid_content_type");
        if (input.WidthPx is <= 0 or > 20000 || input.HeightPx is <= 0 or > 20000)
            return ServiceResult<CertificateTemplateView>.Fail("invalid_dimensions");

        // The prefix pairing: only a key minted for THIS template is acceptable. Without it a creator
        // could attach any stored object — another event's uploads, someone's identity document — and the
        // renderer would fetch it into a document sent to hundreds of people.
        if (!CertificateStorageKeys.IsAcceptableTemplateKey(input.StorageKey, template!.EventId, template.OwnerUserId))
            return ServiceResult<CertificateTemplateView>.Fail("invalid_storage_key");

        // The bytes must actually be there. A template pointing at a key that was never written renders
        // as a blank page, and the creator would not find out until generation.
        if (!await storage.ExistsAsync(input.StorageKey, ct))
            return ServiceResult<CertificateTemplateView>.Fail("upload_not_found");

        await BumpVersionIfIssuedAsync(template, ct);
        template.BackgroundStorageKey = input.StorageKey;
        template.BackgroundContentType = input.ContentType;
        template.BackgroundWidthPx = input.WidthPx;
        template.BackgroundHeightPx = input.HeightPx;
        template.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return ServiceResult<CertificateTemplateView>.Success(await ProjectAsync(template, ct));
    }

    public async Task<ServiceResult<CertificateTemplateView>> ReplaceFieldsAsync(
        Guid userId, Guid templateId, IReadOnlyList<CertificateFieldInput> fields, bool isAdmin,
        CancellationToken ct = default)
    {
        var (template, error) = await LoadAsync(userId, templateId, isAdmin, ct, tracked: true);
        if (error is not null) return ServiceResult<CertificateTemplateView>.Fail(error);

        if (fields.Count > MaxFields) return ServiceResult<CertificateTemplateView>.Fail("too_many_fields");

        var parsed = new List<CertificateTemplateField>(fields.Count);
        foreach (var input in fields)
        {
            if (Validate(input) is { } invalid) return ServiceResult<CertificateTemplateView>.Fail(invalid);
            parsed.Add(ToEntity(template!.Id, input));
        }

        await BumpVersionIfIssuedAsync(template!, ct);

        // Replace wholesale. Ids are not preserved across a save because the canvas is the truth and
        // nothing outside this template references a field row — an issued certificate snapshots values,
        // never field ids.
        var existing = await db.CertificateTemplateFields.Where(f => f.TemplateId == template!.Id).ToListAsync(ct);
        db.CertificateTemplateFields.RemoveRange(existing);
        db.CertificateTemplateFields.AddRange(parsed);

        template!.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return ServiceResult<CertificateTemplateView>.Success(await ProjectAsync(template, ct));
    }

    public async Task<ServiceResult<bool>> ArchiveAsync(
        Guid userId, Guid templateId, bool isAdmin, CancellationToken ct = default)
    {
        var (template, error) = await LoadAsync(userId, templateId, isAdmin, ct, tracked: true);
        if (error is not null) return ServiceResult<bool>.Fail(error);

        template!.Status = CertificateTemplateStatus.Archived;
        template.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return ServiceResult<bool>.Success(true);
    }

    // ── Reuse (Phase 13) ────────────────────────────────────────────────────────────────────────

    public async Task<ServiceResult<CertificateTemplateView>> CopyToLibraryAsync(
        Guid userId, Guid templateId, string? name, bool isAdmin, CancellationToken ct = default)
    {
        var (source, error) = await LoadAsync(userId, templateId, isAdmin, ct);
        if (error is not null) return ServiceResult<CertificateTemplateView>.Fail(error);

        // The library is the CALLER'S, always. Copying someone else's event design into your own library
        // is the point — you were entitled to see it — but the copy belongs to you, not to its author.
        return await CopyAsync(source!, userId, eventId: null, name, ct);
    }

    public async Task<ServiceResult<CertificateTemplateView>> CopyToEventAsync(
        Guid userId, Guid templateId, Guid eventId, string? name, bool isAdmin, CancellationToken ct = default)
    {
        var (source, error) = await LoadAsync(userId, templateId, isAdmin, ct);
        if (error is not null) return ServiceResult<CertificateTemplateView>.Fail(error);

        // Two separate entitlements, checked separately: being allowed to read the design says nothing
        // about being allowed to add content to the destination event.
        var access = await authority.ResolveAsync(userId, eventId, isAdmin, ct);
        if (!access.EventExists) return ServiceResult<CertificateTemplateView>.Fail("not_found");
        if (!access.Can(EventPermission.ManageContent))
            return ServiceResult<CertificateTemplateView>.Fail("forbidden");

        return await CopyAsync(source!, userId, eventId, name, ct);
    }

    /// <summary>Duplicates a design, artwork included, into a new home.
    ///
    /// <para><b>The bytes are copied, not the key.</b> Sharing a storage key across the two would break
    /// the prefix-ownership convention — an object under <c>events/{id}/…</c> reachable from a personal
    /// library — and would mean the two designs could never be independently archived. It costs one copy
    /// of an image at the moment someone deliberately asks for a copy of a design.</para>
    ///
    /// <para>The copy starts at <b>version 1, draft</b>. Version tracks what a given certificate was
    /// rendered from; inheriting a source's version would attach that history to a design that never
    /// issued anything.</para></summary>
    private async Task<ServiceResult<CertificateTemplateView>> CopyAsync(
        CertificateTemplate source, Guid userId, Guid? eventId, string? name, CancellationToken ct)
    {
        var copyName = (name ?? source.Name).Trim();
        if (copyName.Length is < 1 or > 200)
            return ServiceResult<CertificateTemplateView>.Fail("invalid_name");

        var copy = new CertificateTemplate
        {
            EventId = eventId,
            OwnerUserId = userId,
            Name = copyName,
            PageSize = source.PageSize,
            BackgroundContentType = source.BackgroundContentType,
            BackgroundWidthPx = source.BackgroundWidthPx,
            BackgroundHeightPx = source.BackgroundHeightPx,
            Version = 1,
            Status = CertificateTemplateStatus.Draft,
        };

        if (source.BackgroundStorageKey is not null)
        {
            var extension = source.BackgroundContentType?.ToLowerInvariant() switch
            {
                "image/png" => "png",
                "image/webp" => "webp",
                _ => "jpg",
            };

            var destination = eventId is Guid target
                ? CertificateStorageKeys.TemplateBackground(target, copy.Id, copy.Version, extension)
                : CertificateStorageKeys.LibraryTemplateBackground(userId, copy.Id, copy.Version, extension);

            try
            {
                if (await storage.ExistsAsync(source.BackgroundStorageKey, ct))
                {
                    await storage.PutAsync(destination,
                        await storage.GetAsync(source.BackgroundStorageKey, ct),
                        source.BackgroundContentType ?? "image/png", ct);
                    copy.BackgroundStorageKey = destination;
                }
            }
            catch (Exception)
            {
                // A design whose artwork cannot be read is not a design worth copying: the result would
                // be a template that renders as a blank page, discovered at generation time.
                return ServiceResult<CertificateTemplateView>.Fail("artwork_unavailable");
            }

            if (copy.BackgroundStorageKey is null)
                return ServiceResult<CertificateTemplateView>.Fail("artwork_unavailable");
        }

        db.CertificateTemplates.Add(copy);

        var fields = await db.CertificateTemplateFields.AsNoTracking()
            .Where(f => f.TemplateId == source.Id)
            .OrderBy(f => f.ZOrder)
            .ToListAsync(ct);

        // New ids, same layout. Copying the rows rather than re-running validation because these values
        // were already validated when they were placed, and a stricter rule added later must not make an
        // existing design uncopyable.
        foreach (var field in fields)
        {
            db.CertificateTemplateFields.Add(new CertificateTemplateField
            {
                TemplateId = copy.Id,
                Kind = field.Kind,
                FieldKey = field.FieldKey,
                Label = field.Label,
                StaticText = field.StaticText,
                X = field.X,
                Y = field.Y,
                Width = field.Width,
                Height = field.Height,
                Rotation = field.Rotation,
                ZOrder = field.ZOrder,
                IsRequired = field.IsRequired,
                IsMasking = field.IsMasking,
                BackgroundColor = field.BackgroundColor,
                FontFamily = field.FontFamily,
                FontSizePt = field.FontSizePt,
                FontWeight = field.FontWeight,
                Color = field.Color,
                HorizontalAlignment = field.HorizontalAlignment,
                VerticalAlignment = field.VerticalAlignment,
            });
        }

        await db.SaveChangesAsync(ct);
        return ServiceResult<CertificateTemplateView>.Success(await ProjectAsync(copy, ct));
    }

    // ── Where the artwork already has something on it ───────────────────────────────────────────

    /// <summary>Grid resolution. Fine enough to tell a line of text from the gap above it on an A4
    /// landscape page — roughly 2.5% of the width per cell — and coarse enough that the whole map is a
    /// kilobyte and can be recomputed on demand rather than cached.</summary>
    private const int MapColumns = 40;
    private const int MapRows = 28;

    /// <summary>Working width the artwork is scaled to before sampling. The question is "is there ink in
    /// this region", which survives downsampling; decoding a 3508px scan at full size to answer it does
    /// not.</summary>
    private const int MapWorkingWidth = 400;

    /// <summary>How far a pixel must sit from the page's own background colour to count as printed.
    /// Generous on purpose: certificate stocks carry faint watermarks and paper texture, and flagging
    /// those would make the warning fire everywhere and therefore mean nothing.</summary>
    private const int InkDistance = 60;

    /// <summary>Fraction of a cell that must be ink before the cell counts as occupied. Text is mostly
    /// background even inside its own line, so this is deliberately low.</summary>
    private const double InkFraction = 0.06;

    public async Task<ServiceResult<CertificateArtworkMap>> ArtworkMapAsync(
        Guid userId, Guid templateId, bool isAdmin, CancellationToken ct = default)
    {
        var (template, error) = await LoadAsync(userId, templateId, isAdmin, ct);
        if (error is not null) return ServiceResult<CertificateArtworkMap>.Fail(error);

        var blank = new string('0', MapColumns * MapRows);

        if (template!.BackgroundStorageKey is null)
            return ServiceResult<CertificateArtworkMap>.Success(
                new CertificateArtworkMap(MapColumns, MapRows, blank, Analysed: false));

        try
        {
            if (!await storage.ExistsAsync(template.BackgroundStorageKey, ct))
                return ServiceResult<CertificateArtworkMap>.Success(
                    new CertificateArtworkMap(MapColumns, MapRows, blank, Analysed: false));

            var bytes = await storage.GetAsync(template.BackgroundStorageKey, ct);
            return ServiceResult<CertificateArtworkMap>.Success(Analyse(bytes));
        }
        catch (Exception)
        {
            // Unreadable artwork means no warning, never a wrong one. `Analysed: false` is what keeps the
            // editor from presenting an empty map as "your design is clear here".
            return ServiceResult<CertificateArtworkMap>.Success(
                new CertificateArtworkMap(MapColumns, MapRows, blank, Analysed: false));
        }
    }

    /// <summary>Marks each grid cell that carries something other than the page's background.
    ///
    /// <para>The background is taken as the most common colour in the image rather than assumed white:
    /// plenty of designs are cream, navy or a gradient, and assuming white would mark every one of them
    /// as entirely printed.</para></summary>
    private static CertificateArtworkMap Analyse(byte[] artwork)
    {
        using var image = SixLabors.ImageSharp.Image.Load<SixLabors.ImageSharp.PixelFormats.Rgba32>(artwork);

        var height = Math.Max(1, (int)Math.Round(image.Height * (MapWorkingWidth / (double)image.Width)));
        image.Mutate(x => x.Resize(MapWorkingWidth, height));

        // Modal colour, quantised to 32 levels per channel so near-identical shades of one background
        // count as the same background rather than as thousands of distinct ones.
        var histogram = new Dictionary<int, int>();
        image.ProcessPixelRows(rows =>
        {
            for (var y = 0; y < rows.Height; y++)
            {
                var row = rows.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                {
                    var key = ((row[x].R >> 3) << 10) | ((row[x].G >> 3) << 5) | (row[x].B >> 3);
                    histogram[key] = histogram.TryGetValue(key, out var n) ? n + 1 : 1;
                }
            }
        });

        var dominant = histogram.MaxBy(kv => kv.Value).Key;
        int bgR = ((dominant >> 10) & 31) << 3, bgG = ((dominant >> 5) & 31) << 3, bgB = (dominant & 31) << 3;

        var ink = new int[MapColumns * MapRows];
        var total = new int[MapColumns * MapRows];

        image.ProcessPixelRows(rows =>
        {
            for (var y = 0; y < rows.Height; y++)
            {
                var cellY = Math.Min(MapRows - 1, y * MapRows / rows.Height);
                var row = rows.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                {
                    var cell = cellY * MapColumns + Math.Min(MapColumns - 1, x * MapColumns / row.Length);
                    total[cell]++;

                    int dr = row[x].R - bgR, dg = row[x].G - bgG, db = row[x].B - bgB;
                    if (Math.Sqrt(dr * dr + dg * dg + db * db) > InkDistance) ink[cell]++;
                }
            }
        });

        var cells = new char[MapColumns * MapRows];
        for (var i = 0; i < cells.Length; i++)
            cells[i] = total[i] > 0 && ink[i] / (double)total[i] >= InkFraction ? '1' : '0';

        return new CertificateArtworkMap(MapColumns, MapRows, new string(cells), Analysed: true);
    }

    public async Task<ServiceResult<string>> ArtworkColourAsync(
        Guid userId, Guid templateId, CertificateRegion region, bool isAdmin, CancellationToken ct = default)
    {
        var (template, error) = await LoadAsync(userId, templateId, isAdmin, ct);
        if (error is not null) return ServiceResult<string>.Fail(error);

        if (template!.BackgroundStorageKey is null) return ServiceResult<string>.Fail("no_artwork");

        try
        {
            if (!await storage.ExistsAsync(template.BackgroundStorageKey, ct))
                return ServiceResult<string>.Fail("no_artwork");

            var bytes = await storage.GetAsync(template.BackgroundStorageKey, ct);
            return ServiceResult<string>.Success(SampleGround(bytes, region));
        }
        catch (Exception)
        {
            // No colour is better than a wrong one: a guessed patch is a smear on somebody's certificate.
            return ServiceResult<string>.Fail("artwork_unavailable");
        }
    }

    /// <summary>The paper colour around a region.
    ///
    /// <para>Sampled from a band just OUTSIDE the region and from its top and bottom margins, never from
    /// the middle. The middle is the printed text being covered, and including it would drag the patch
    /// toward the ink — the classic grey-box-over-black-text result.</para></summary>
    private static string SampleGround(byte[] artwork, CertificateRegion region)
    {
        using var image = SixLabors.ImageSharp.Image.Load<SixLabors.ImageSharp.PixelFormats.Rgba32>(artwork);

        int Left(double v) => Math.Clamp((int)Math.Round(v / 100.0 * image.Width), 0, image.Width - 1);
        int Top(double v) => Math.Clamp((int)Math.Round(v / 100.0 * image.Height), 0, image.Height - 1);

        var x0 = Left(region.X);
        var x1 = Left(region.X + region.Width);
        var y0 = Top(region.Y);
        var y1 = Top(region.Y + region.Height);

        // A margin proportional to the region, so the band is meaningful on a thin field and on a tall
        // one alike.
        var marginY = Math.Max(2, (y1 - y0) / 6);
        var marginX = Math.Max(2, (x1 - x0) / 12);

        var histogram = new Dictionary<int, int>();
        void Count(int x, int y)
        {
            if (x < 0 || y < 0 || x >= image.Width || y >= image.Height) return;
            var p = image[x, y];
            var key = ((p.R >> 3) << 10) | ((p.G >> 3) << 5) | (p.B >> 3);
            histogram[key] = histogram.TryGetValue(key, out var n) ? n + 1 : 1;
        }

        for (var x = x0 - marginX; x <= x1 + marginX; x++)
        {
            for (var d = 1; d <= marginY; d++) { Count(x, y0 - d); Count(x, y1 + d); }
        }
        for (var y = y0; y <= y1; y++)
        {
            for (var d = 1; d <= marginX; d++) { Count(x0 - d, y); Count(x1 + d, y); }
        }

        if (histogram.Count == 0) return "#FFFFFF";

        var dominant = histogram.MaxBy(kv => kv.Value).Key;
        int r = ((dominant >> 10) & 31) << 3, g = ((dominant >> 5) & 31) << 3, b = (dominant & 31) << 3;
        return $"#{r:X2}{g:X2}{b:X2}";
    }

    // ── OCR extension point (Phase 12) ──────────────────────────────────────────────────────────

    public async Task<ServiceResult<TextDetectionResult>> DetectBackgroundTextAsync(
        Guid userId, Guid templateId, bool isAdmin, CancellationToken ct = default)
    {
        var (template, error) = await LoadAsync(userId, templateId, isAdmin, ct);
        if (error is not null) return ServiceResult<TextDetectionResult>.Fail(error);

        // Asked before the artwork is fetched. With no engine configured there is nothing to analyse and
        // no reason to pull megabytes out of storage to prove it.
        if (!textDetector.IsAvailable)
            return ServiceResult<TextDetectionResult>.Success(
                TextDetectionResult.Unavailable(
                    "Automatic text detection is not enabled on this deployment. Place fields by hand."));

        if (template!.BackgroundStorageKey is null)
            return ServiceResult<TextDetectionResult>.Success(
                TextDetectionResult.Unavailable("This design has no artwork to look at yet."));

        byte[] artwork;
        try
        {
            if (!await storage.ExistsAsync(template.BackgroundStorageKey, ct))
                return ServiceResult<TextDetectionResult>.Success(
                    TextDetectionResult.Unavailable("The design's artwork could not be read."));
            artwork = await storage.GetAsync(template.BackgroundStorageKey, ct);
        }
        catch (Exception)
        {
            // Storage being unreachable must not break the editor that is showing the design. It is a
            // missing convenience, not a failed page.
            return ServiceResult<TextDetectionResult>.Success(
                TextDetectionResult.Unavailable("The design's artwork could not be read."));
        }

        try
        {
            return ServiceResult<TextDetectionResult>.Success(await textDetector.DetectAsync(
                artwork, template.BackgroundContentType ?? "image/png", ct));
        }
        catch (Exception)
        {
            // A future engine throwing is contained here for the same reason: detection is an assist, and
            // an assist that can break saving a design is worse than no assist.
            return ServiceResult<TextDetectionResult>.Success(
                TextDetectionResult.Unavailable("Automatic text detection did not run. Place fields by hand."));
        }
    }

    // ── Validation ──────────────────────────────────────────────────────────────────────────────

    /// <summary>Structural checks only — whether a layout is *good* is the creator's judgement.</summary>
    private static string? Validate(CertificateFieldInput f)
    {
        if (ParseKind(f.Kind) is not { } kind) return "invalid_field_kind";

        // A dynamic field with no key has nothing to substitute and would render as a blank space on
        // every certificate.
        if (kind == CertificateFieldKind.DynamicField && string.IsNullOrWhiteSpace(f.FieldKey))
            return "field_key_required";
        if (f.FieldKey is { Length: > 100 }) return "invalid_field_key";
        if (f.Label is { Length: > 200 }) return "invalid_label";

        // Percentages. A little overhang is legitimate — a rotated element extends past its box — but a
        // field at x=900 is a unit mistake, not a design.
        if (f.X is < -50 or > 150 || f.Y is < -50 or > 150) return "field_out_of_bounds";
        if (f.Width is <= 0 or > 200 || f.Height is <= 0 or > 200) return "field_out_of_bounds";
        if (f.FontSizePt is < 1 or > 400) return "invalid_font_size";

        foreach (var colour in new[] { f.Color, f.BackgroundColor })
            if (colour is not null && !IsHexColour(colour)) return "invalid_colour";

        if (f.HorizontalAlignment is not null && ParseHorizontal(f.HorizontalAlignment) is null)
            return "invalid_alignment";
        if (f.VerticalAlignment is not null && ParseVertical(f.VerticalAlignment) is null)
            return "invalid_alignment";

        return null;
    }

    /// <summary>`#RGB`, `#RRGGBB` or `#RRGGBBAA`. Validated rather than accepted-and-defaulted so a typo
    /// surfaces in the editor instead of as an unexpectedly black heading on two hundred certificates.</summary>
    private static bool IsHexColour(string value) =>
        value.StartsWith('#') && value.Length is 4 or 7 or 9 && value[1..].All(Uri.IsHexDigit);

    // ── Versioning ──────────────────────────────────────────────────────────────────────────────

    /// <summary>Whether the next render-affecting edit will produce a new version.
    ///
    /// <para>Only once something has been issued. Before that, a template is still being drafted and
    /// bumping on every keystroke would make the version number meaningless.</para></summary>
    private Task<bool> WillBumpAsync(CertificateTemplate template, CancellationToken ct) =>
        db.IssuedCertificates.AnyAsync(c => c.TemplateId == template.Id && c.TemplateVersion == template.Version, ct);

    /// <summary>Increments the version if certificates already exist at the current one.
    ///
    /// <para>This is what keeps history honest: an issued certificate records the version it was rendered
    /// from, so editing the template afterwards cannot retroactively change what that certificate claims
    /// to look like.</para></summary>
    private async Task BumpVersionIfIssuedAsync(CertificateTemplate template, CancellationToken ct)
    {
        if (await WillBumpAsync(template, ct)) template.Version++;
    }

    // ── Internals ───────────────────────────────────────────────────────────────────────────────

    /// <summary>Loads a template and checks the caller may manage it, collapsing "no such template",
    /// "someone else's" and "archived" into one <c>not_found</c> (D-018).</summary>
    private async Task<(CertificateTemplate? Template, string? Error)> LoadAsync(
        Guid userId, Guid templateId, bool isAdmin, CancellationToken ct, bool tracked = false)
    {
        var q = tracked ? db.CertificateTemplates : db.CertificateTemplates.AsNoTracking();
        var template = await q.FirstOrDefaultAsync(t => t.Id == templateId, ct);
        if (template is null) return (null, "not_found");

        if (template.EventId is Guid eventId)
        {
            var access = await authority.ResolveAsync(userId, eventId, isAdmin, ct);
            if (!access.EventExists || !access.Can(EventPermission.ManageContent)) return (null, "not_found");
            return (template, null);
        }

        // A library template has no event to resolve authority against, so ownership is the whole rule.
        // Platform admins are deliberately NOT granted access: a creator's private design library is not
        // event content, and nothing in the confirmed requirements asks staff to browse it.
        return template.OwnerUserId == userId ? (template, null) : (null, "not_found");
    }

    private async Task<IReadOnlyList<CertificateTemplateView>> ProjectManyAsync(
        IReadOnlyList<CertificateTemplate> templates, CancellationToken ct)
    {
        var views = new List<CertificateTemplateView>(templates.Count);
        foreach (var t in templates) views.Add(await ProjectAsync(t, ct));
        return views;
    }

    private async Task<CertificateTemplateView> ProjectAsync(CertificateTemplate t, CancellationToken ct)
    {
        var fields = await db.CertificateTemplateFields.AsNoTracking()
            .Where(f => f.TemplateId == t.Id)
            .OrderBy(f => f.ZOrder)
            .ToListAsync(ct);

        // Presigned here, never stored: a key is not fetchable (D-302), and a URL has an expiry that must
        // not be written into a row that outlives it.
        var backgroundUrl = t.BackgroundStorageKey is null
            ? null
            : await storage.PresignGetAsync(t.BackgroundStorageKey, null, ct);

        return new CertificateTemplateView(
            t.Id, t.EventId, t.OwnerUserId, t.Name,
            Slug(t.PageSize), t.Status.ToString().ToLowerInvariant(), t.Version,
            t.BackgroundStorageKey, backgroundUrl, t.BackgroundWidthPx, t.BackgroundHeightPx,
            await db.IssuedCertificates.AnyAsync(c => c.TemplateId == t.Id, ct),
            fields.Select(ToView).ToList(),
            t.CreatedAt, t.UpdatedAt);
    }

    private static CertificateFieldView ToView(CertificateTemplateField f) => new(
        f.Id, f.Kind.ToString().ToLowerInvariant(), f.FieldKey, f.Label, f.StaticText,
        f.X, f.Y, f.Width, f.Height, f.Rotation, f.ZOrder, f.IsRequired, f.IsMasking,
        f.BackgroundColor, f.FontFamily, f.FontSizePt, f.FontWeight, f.Color,
        f.HorizontalAlignment.ToString().ToLowerInvariant(),
        f.VerticalAlignment.ToString().ToLowerInvariant());

    private static CertificateTemplateField ToEntity(Guid templateId, CertificateFieldInput f) => new()
    {
        TemplateId = templateId,
        Kind = ParseKind(f.Kind)!.Value,
        FieldKey = Trim(f.FieldKey),
        Label = Trim(f.Label),
        StaticText = f.StaticText,
        X = f.X, Y = f.Y, Width = f.Width, Height = f.Height,
        Rotation = f.Rotation,
        ZOrder = f.ZOrder,
        IsRequired = f.IsRequired,
        IsMasking = f.IsMasking,
        BackgroundColor = Trim(f.BackgroundColor),
        FontFamily = Trim(f.FontFamily),
        FontSizePt = f.FontSizePt,
        FontWeight = Trim(f.FontWeight),
        Color = Trim(f.Color),
        HorizontalAlignment = ParseHorizontal(f.HorizontalAlignment) ?? CertificateHorizontalAlignment.Left,
        VerticalAlignment = ParseVertical(f.VerticalAlignment) ?? CertificateVerticalAlignment.Middle,
    };

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static CertificateFieldKind? ParseKind(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "text" => CertificateFieldKind.Text,
        "dynamicfield" or "dynamic_field" or "field" => CertificateFieldKind.DynamicField,
        "image" => CertificateFieldKind.Image,
        "qrcode" or "qr_code" or "qr" => CertificateFieldKind.QrCode,
        _ => null,
    };

    private static CertificateHorizontalAlignment? ParseHorizontal(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "left" => CertificateHorizontalAlignment.Left,
            "center" or "centre" => CertificateHorizontalAlignment.Center,
            "right" => CertificateHorizontalAlignment.Right,
            _ => null,
        };

    private static CertificateVerticalAlignment? ParseVertical(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "top" => CertificateVerticalAlignment.Top,
            "middle" or "center" or "centre" => CertificateVerticalAlignment.Middle,
            "bottom" => CertificateVerticalAlignment.Bottom,
            _ => null,
        };

    private static CertificatePageSize? ParsePageSize(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        null or "" or "a4-landscape" => CertificatePageSize.A4Landscape,
        "a4-portrait" => CertificatePageSize.A4Portrait,
        _ => null,
    };

    private static string Slug(CertificatePageSize size) =>
        size == CertificatePageSize.A4Portrait ? "a4-portrait" : "a4-landscape";
}
