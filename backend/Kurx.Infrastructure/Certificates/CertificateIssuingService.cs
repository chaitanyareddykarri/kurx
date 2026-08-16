using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Infrastructure.Certificates;

/// <summary>
/// Producing one certificate (D-355, Phase 4).
///
/// <para><b>One assembly path, two callers.</b> <see cref="BuildDocumentAsync"/> turns a template into a
/// renderable document, and both preview and issue go through it. Nothing else may assemble a document —
/// that is the whole mechanism behind "the preview matches the certificate", and a second assembly, however
/// carefully written, would be a second thing to keep in step.</para>
/// </summary>
public class CertificateIssuingService(
    KurxDbContext db,
    IEventAuthority authority,
    IStorage storage,
    ICertificateDocumentRenderer renderer,
    ICertificateIdAllocator ids,
    ICertificateSigner signer,
    ICertificateVerificationLinks links) : ICertificateIssuingService
{
    /// <summary>Print resolution for the artefact a recipient receives.</summary>
    private const int PrintDpi = 300;

    /// <summary>Screen resolution for a preview. Sharp enough to show whether type collides, small enough
    /// to hand to a browser without a spinner.</summary>
    private const int PreviewDpi = 150;

    /// <summary>Sample values for a preview. Named people rather than <c>Lorem ipsum</c>: a preview is
    /// judged on whether the layout survives a real name, and "XXXX" always fits.</summary>
    private static readonly IReadOnlyDictionary<string, string> SampleValues =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["participant_name"] = "Ananya Rao",
            ["achievement"] = "First Place",
            ["role"] = "Participant",
            ["team_name"] = "ByteBuilders",
            ["organization"] = "Example Institute",
            ["certificate_id"] = "CERT-2026-00042",
            ["registration_id"] = "REG-2026-0042",
            ["issue_date"] = "15 August 2026",
        };

    // ── Preview ─────────────────────────────────────────────────────────────────────────────────

    public async Task<ServiceResult<CertificatePreview>> PreviewAsync(
        Guid userId, Guid templateId, string format, bool isAdmin, CancellationToken ct = default)
    {
        var (template, error) = await LoadTemplateAsync(userId, templateId, isAdmin, ct);
        if (error is not null) return ServiceResult<CertificatePreview>.Fail(error);

        var document = await BuildDocumentAsync(template!, ct);

        // The event's own facts replace the matching samples where there is an event: an organiser
        // previewing a design for THEIR event should see their event on it, otherwise the preview cannot
        // answer "does the real title fit on that line", which is the question being asked.
        var values = new Dictionary<string, string>(SampleValues, StringComparer.Ordinal);
        if (template!.EventId is Guid eventId) ApplyEventValues(values, await LoadEventAsync(eventId, ct));

        var data = new CertificateRenderData(values, await ResolveImagesAsync(template, ct),
            QrPayload: links.VerificationUrl("SAMPLE"));

        var wantsPdf = string.Equals(format, "pdf", StringComparison.OrdinalIgnoreCase);
        var bytes = wantsPdf
            ? await renderer.RenderPdfAsync(document, data, ct)
            : await renderer.RenderPngAsync(document, data, PreviewDpi, ct);

        var safeName = string.Concat(template.Name.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_' or ' ')).Trim();
        return ServiceResult<CertificatePreview>.Success(new CertificatePreview(
            bytes,
            wantsPdf ? "application/pdf" : "image/png",
            $"{(safeName.Length == 0 ? "certificate" : safeName)} preview.{(wantsPdf ? "pdf" : "png")}"));
    }

    // ── Issue ───────────────────────────────────────────────────────────────────────────────────

    public async Task<ServiceResult<IssuedCertificateView>> IssueAsync(
        Guid userId, Guid templateId, IssueCertificateInput input, bool isAdmin, CancellationToken ct = default)
    {
        var (template, error) = await LoadTemplateAsync(userId, templateId, isAdmin, ct);
        if (error is not null) return ServiceResult<IssuedCertificateView>.Fail(error);

        // A library template has no event, and a certificate must belong to one: it is the event that the
        // certificate certifies participation in, and the thing authority is resolved against.
        if (template!.EventId is not Guid eventId)
            return ServiceResult<IssuedCertificateView>.Fail("template_not_attached_to_event");

        if (template.BackgroundStorageKey is null)
            return ServiceResult<IssuedCertificateView>.Fail("background_required");

        var recipientName = (input.RecipientName ?? "").Trim();
        if (recipientName.Length is < 1 or > 200)
            return ServiceResult<IssuedCertificateView>.Fail("invalid_recipient_name");

        var document = await BuildDocumentAsync(template, ct);
        var ev = await LoadEventAsync(eventId, ct);

        var issuedAt = DateTime.UtcNow;
        var allocation = await ids.AllocateAsync(eventId, issuedAt, ct);

        // Event facts first, then the caller's values — so an organiser can override a date for a specific
        // certificate, but does not have to restate the event's name on every one.
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        ApplyEventValues(values, ev);
        values["participant_name"] = recipientName;
        values["certificate_id"] = allocation.CertificateId;
        values["issue_date"] = issuedAt.ToString("d MMMM yyyy");
        foreach (var (key, value) in input.Values) values[key] = value;

        // A required field with nothing behind it stops the certificate. A blank space where a name
        // belongs is worse than a refusal the organiser can act on.
        var missing = document.Elements
            .Where(e => e.Kind == "dynamicfield" && e.FieldKey is not null)
            .Select(e => e.FieldKey!)
            .Where(k => !values.TryGetValue(k, out var v) || string.IsNullOrWhiteSpace(v))
            .Where(k => RequiredKeys(template.Id).Contains(k))
            .ToList();
        if (missing.Count > 0) return ServiceResult<IssuedCertificateView>.Fail("missing_required_values");

        var recipient = new CertificateRecipient
        {
            EventId = eventId,
            FullName = recipientName,
            Email = input.Email,
            NormalizedEmail = string.IsNullOrWhiteSpace(input.Email) ? null : input.Email.Trim().ToLowerInvariant(),
        };
        db.CertificateRecipients.Add(recipient);

        var certificate = new IssuedCertificate
        {
            CertificateId = allocation.CertificateId,
            EventId = eventId,
            TemplateId = template.Id,
            TemplateVersion = template.Version,
            RecipientId = recipient.Id,
            FieldValuesJson = System.Text.Json.JsonSerializer.Serialize(values),
            Status = IssuedCertificateStatus.Issued,
            IssuedAt = issuedAt,
        };

        // Signed BEFORE rendering, over the values that will be printed. The signature covers exactly
        // what the verification page displays, so an altered row fails verification.
        var signature = await signer.SignAsync(CertificateCanonicalPayload.Build(
            certificate.CertificateId, eventId, template.Id, template.Version, issuedAt, values), ct);
        certificate.SignatureKeyId = signature.KeyId;
        certificate.Signature = signature.Signature;

        // The QR resolves to this certificate's own verification page, at whatever public origin this
        // deployment is configured with.
        var renderData = new CertificateRenderData(
            values, await ResolveImagesAsync(template, ct), links.VerificationUrl(allocation.CertificateId));

        var pdf = await renderer.RenderPdfAsync(document, renderData, ct);
        var png = await renderer.RenderPngAsync(document, renderData, PrintDpi, ct);

        var pdfKey = CertificateStorageKeys.CertificatePdf(eventId, certificate.Id);
        var pngKey = CertificateStorageKeys.CertificatePng(eventId, certificate.Id);
        await storage.PutAsync(pdfKey, pdf, "application/pdf", ct);
        await storage.PutAsync(pngKey, png, "image/png", ct);

        certificate.PdfStorageKey = pdfKey;
        certificate.PngStorageKey = pngKey;
        certificate.DocumentSha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(pdf)).ToLowerInvariant();

        db.IssuedCertificates.Add(certificate);
        await db.SaveChangesAsync(ct);

        return ServiceResult<IssuedCertificateView>.Success(await ProjectAsync(certificate, recipientName, ct));
    }

    // ── Bulk: assemble once, render many (Phase 7) ──────────────────────────────────────────────

    public async Task<ServiceResult<CertificateRenderPlan>> PrepareAsync(
        Guid templateId, CancellationToken ct = default)
    {
        var template = await db.CertificateTemplates.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == templateId, ct);

        if (template is null) return ServiceResult<CertificateRenderPlan>.Fail("not_found");
        if (template.EventId is not Guid eventId)
            return ServiceResult<CertificateRenderPlan>.Fail("template_not_attached_to_event");
        if (template.BackgroundStorageKey is null)
            return ServiceResult<CertificateRenderPlan>.Fail("background_required");

        var eventValues = new Dictionary<string, string>(StringComparer.Ordinal);
        ApplyEventValues(eventValues, await LoadEventAsync(eventId, ct));

        return ServiceResult<CertificateRenderPlan>.Success(new CertificateRenderPlan(
            template.Id,
            template.Version,
            eventId,
            await BuildDocumentAsync(template, ct),
            await ResolveImagesAsync(template, ct),
            eventValues,
            RequiredKeys(template.Id)));
    }

    public async Task<ServiceResult<IssuedCertificateView>> IssuePreparedAsync(
        CertificateRenderPlan plan, Guid recipientId, Guid? batchId, string recipientName,
        IReadOnlyDictionary<string, string> values, CancellationToken ct = default)
    {
        var name = (recipientName ?? "").Trim();
        if (name.Length is < 1 or > 200)
            return ServiceResult<IssuedCertificateView>.Fail("invalid_recipient_name");

        var issuedAt = DateTime.UtcNow;
        var allocation = await ids.AllocateAsync(plan.EventId, issuedAt, ct);

        // Same precedence as the single-issue path: event facts first, then the row's own values.
        var merged = new Dictionary<string, string>(plan.EventValues, StringComparer.Ordinal)
        {
            ["participant_name"] = name,
            ["certificate_id"] = allocation.CertificateId,
            ["issue_date"] = issuedAt.ToString("d MMMM yyyy"),
        };
        foreach (var (key, value) in values) merged[key] = value;

        var missing = plan.RequiredKeys
            .Where(k => !merged.TryGetValue(k, out var v) || string.IsNullOrWhiteSpace(v))
            .ToList();
        if (missing.Count > 0) return ServiceResult<IssuedCertificateView>.Fail("missing_required_values");

        var certificate = new IssuedCertificate
        {
            CertificateId = allocation.CertificateId,
            EventId = plan.EventId,
            TemplateId = plan.TemplateId,
            TemplateVersion = plan.TemplateVersion,
            BatchId = batchId,
            RecipientId = recipientId,
            FieldValuesJson = System.Text.Json.JsonSerializer.Serialize(merged),
            Status = IssuedCertificateStatus.Issued,
            IssuedAt = issuedAt,
        };

        var signature = await signer.SignAsync(CertificateCanonicalPayload.Build(
            certificate.CertificateId, plan.EventId, plan.TemplateId, plan.TemplateVersion, issuedAt, merged), ct);
        certificate.SignatureKeyId = signature.KeyId;
        certificate.Signature = signature.Signature;

        var renderData = new CertificateRenderData(
            merged, plan.Images, links.VerificationUrl(allocation.CertificateId));

        var pdf = await renderer.RenderPdfAsync(plan.Document, renderData, ct);
        var png = await renderer.RenderPngAsync(plan.Document, renderData, PrintDpi, ct);

        var pdfKey = CertificateStorageKeys.CertificatePdf(plan.EventId, certificate.Id);
        var pngKey = CertificateStorageKeys.CertificatePng(plan.EventId, certificate.Id);
        await storage.PutAsync(pdfKey, pdf, "application/pdf", ct);
        await storage.PutAsync(pngKey, png, "image/png", ct);

        certificate.PdfStorageKey = pdfKey;
        certificate.PngStorageKey = pngKey;
        certificate.DocumentSha256 =
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(pdf)).ToLowerInvariant();

        db.IssuedCertificates.Add(certificate);
        await db.SaveChangesAsync(ct);

        return ServiceResult<IssuedCertificateView>.Success(await ProjectAsync(certificate, name, ct));
    }

    /// <summary>Which of a template's dynamic fields refuse to render empty. Read separately because the
    /// render document does not carry it — the renderer has no business enforcing policy.</summary>
    private IReadOnlySet<string> RequiredKeys(Guid templateId) =>
        db.CertificateTemplateFields.AsNoTracking()
            .Where(f => f.TemplateId == templateId && f.IsRequired && f.FieldKey != null)
            .Select(f => f.FieldKey!)
            .ToHashSet(StringComparer.Ordinal);

    // ── The one assembly path ───────────────────────────────────────────────────────────────────

    /// <summary>Turns a stored template into a renderable document. The only place this happens.</summary>
    private async Task<CertificateDocument> BuildDocumentAsync(CertificateTemplate template, CancellationToken ct)
    {
        var fields = await db.CertificateTemplateFields.AsNoTracking()
            .Where(f => f.TemplateId == template.Id)
            .OrderBy(f => f.ZOrder)
            .ToListAsync(ct);

        byte[]? background = null;
        if (template.BackgroundStorageKey is not null)
        {
            try
            {
                if (await storage.ExistsAsync(template.BackgroundStorageKey, ct))
                    background = await storage.GetAsync(template.BackgroundStorageKey, ct);
            }
            catch (Exception)
            {
                // A design that outlived its artwork renders without it rather than not at all. Failing
                // the whole batch because one object is briefly unreachable helps nobody.
            }
        }

        return new CertificateDocument(
            template.PageSize == CertificatePageSize.A4Portrait ? "a4-portrait" : "a4-landscape",
            background,
            fields.Select(f => new CertificateRenderElement(
                f.Kind.ToString().ToLowerInvariant(),
                f.FieldKey, f.StaticText,
                f.X, f.Y, f.Width, f.Height, f.Rotation, f.ZOrder,
                f.IsMasking, f.BackgroundColor,
                // Image elements carry their own storage key on the field once Phase 3's editor supports
                // uploading one; until then they render nothing, which is the correct empty state.
                ImageKey: null,
                f.FontFamily, f.FontSizePt, f.FontWeight, f.Color,
                f.HorizontalAlignment.ToString().ToLowerInvariant(),
                f.VerticalAlignment.ToString().ToLowerInvariant(),
                f.FontStyle, f.Underline, f.LineHeight, f.LetterSpacing, f.MirrorsArtwork))
                .ToList());
    }

    private async Task<IReadOnlyDictionary<string, byte[]>> ResolveImagesAsync(
        CertificateTemplate template, CancellationToken ct)
    {
        // Placed elements do not carry their own uploads yet (Phase 3 places text, dynamic fields and
        // QR). Kept as the seam so adding one is a change here rather than in the renderer.
        await Task.CompletedTask;
        return new Dictionary<string, byte[]>(StringComparer.Ordinal);
    }

    // ── Event facts ─────────────────────────────────────────────────────────────────────────────

    private sealed record EventFacts(string Title, DateTime StartsAt, DateTime EndsAt, string Venue, string Organizer);

    private async Task<EventFacts?> LoadEventAsync(Guid eventId, CancellationToken ct)
    {
        var ev = await db.Events.AsNoTracking()
            .Where(e => e.Id == eventId)
            .Select(e => new { e.Title, e.StartsAt, e.EndsAt, e.VenueName, e.City, e.CreatedBy })
            .FirstOrDefaultAsync(ct);
        if (ev is null) return null;

        // D-271: a USER owns an event, so the organizer is that person — not the venue, and not the
        // organization the event represents.
        var organizer = await db.Users.AsNoTracking()
            .Where(u => u.Id == ev.CreatedBy).Select(u => u.Name).FirstOrDefaultAsync(ct);

        return new EventFacts(ev.Title, ev.StartsAt, ev.EndsAt,
            string.Join(", ", new[] { ev.VenueName, ev.City }.Where(v => !string.IsNullOrWhiteSpace(v))),
            organizer ?? "");
    }

    /// <summary>Writes the event-derived half of the substitution table. Shared by preview and issue so
    /// "how is a two-day event dated" has exactly one answer — two copies is the drift that stops a
    /// preview predicting its output.</summary>
    private static void ApplyEventValues(IDictionary<string, string> values, EventFacts? ev)
    {
        if (ev is null) return;
        values["event_name"] = ev.Title;
        values["event_date"] = ev.StartsAt.Date == ev.EndsAt.Date
            ? ev.StartsAt.ToString("d MMMM yyyy")
            : $"{ev.StartsAt:d MMMM} – {ev.EndsAt:d MMMM yyyy}";
        values["start_date"] = ev.StartsAt.ToString("d MMMM yyyy");
        values["end_date"] = ev.EndsAt.ToString("d MMMM yyyy");
        values["venue"] = ev.Venue;
        values["organizer_name"] = ev.Organizer;
    }

    // ── Internals ───────────────────────────────────────────────────────────────────────────────

    private async Task<(CertificateTemplate? Template, string? Error)> LoadTemplateAsync(
        Guid userId, Guid templateId, bool isAdmin, CancellationToken ct)
    {
        var template = await db.CertificateTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.Id == templateId, ct);
        if (template is null) return (null, "not_found");

        if (template.EventId is Guid eventId)
        {
            var access = await authority.ResolveAsync(userId, eventId, isAdmin, ct);
            if (!access.EventExists || !access.Can(EventPermission.ManageContent)) return (null, "not_found");
            return (template, null);
        }

        return template.OwnerUserId == userId ? (template, null) : (null, "not_found");
    }

    private async Task<IssuedCertificateView> ProjectAsync(
        IssuedCertificate c, string recipientName, CancellationToken ct) => new(
        c.Id, c.CertificateId, c.EventId, c.TemplateId, c.TemplateVersion, recipientName,
        c.Status.ToString().ToLowerInvariant(), c.IssuedAt,
        c.PdfStorageKey is null ? null : await storage.PresignGetAsync(c.PdfStorageKey, null, ct),
        c.PngStorageKey is null ? null : await storage.PresignGetAsync(c.PngStorageKey, null, ct));
}
