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

        // No recipient on a preview, so no photo: the placeholder box is the honest preview of a field
        // whose content is per-person.
        var data = new CertificateRenderData(values, await ResolveImagesAsync(template, null, null, ct),
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
            values, await ResolveImagesAsync(template, recipient.UserId, recipient.NormalizedEmail, ct),
            links.VerificationUrl(allocation.CertificateId));

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
            // Template-level images only. A photo is per-recipient and cannot be shared across a plan,
            // so it is resolved per certificate in IssuePreparedAsync.
            await ResolveImagesAsync(template, null, null, ct),
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

        // The plan's images are shared by every certificate in the run; a photo is this person's alone,
        // so it is resolved here and layered on top. Only when the design actually places one — a bulk run
        // must not pay a storage read per recipient for a picture no element will paint.
        var images = plan.Images;
        if (plan.Document.Elements.Any(e => e.Kind == "image" && e.ImageKey == ParticipantPhotoSlot))
        {
            var recipient = await db.CertificateRecipients.AsNoTracking()
                .Where(r => r.Id == recipientId)
                .Select(r => new { r.UserId, r.NormalizedEmail })
                .FirstOrDefaultAsync(ct);

            var photo = await ResolveImagesAsync(
                new CertificateTemplate { EventId = plan.EventId },
                recipient?.UserId, recipient?.NormalizedEmail, ct);

            if (photo.Count > 0)
                images = new Dictionary<string, byte[]>(plan.Images, StringComparer.Ordinal)
                {
                    [ParticipantPhotoSlot] = photo[ParticipantPhotoSlot],
                };
        }

        var renderData = new CertificateRenderData(
            merged, images, links.VerificationUrl(allocation.CertificateId));

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
            CertificatePageSizes.SlugFor(template.PageSize),
            background,
            fields.Select(f => new CertificateRenderElement(
                f.Kind.ToString().ToLowerInvariant(),
                f.FieldKey, f.StaticText,
                f.X, f.Y, f.Width, f.Height, f.Rotation, f.ZOrder,
                f.IsMasking, f.BackgroundColor,
                // An image element's FieldKey names WHICH picture, exactly as a dynamic field's names
                // which value — `participant_photo` resolves to the recipient's own photo. Reusing the
                // column the field already has avoids a schema change for what is a slot name, not an
                // upload: a per-field uploaded image would need its own storage key and is not this.
                ImageKey: f.Kind == CertificateFieldKind.Image ? f.FieldKey : null,
                f.FontFamily, f.FontSizePt, f.FontWeight, f.Color,
                f.HorizontalAlignment.ToString().ToLowerInvariant(),
                f.VerticalAlignment.ToString().ToLowerInvariant(),
                f.FontStyle, f.Underline, f.LineHeight, f.LetterSpacing, f.MirrorsArtwork))
                .ToList(),
            // The size the renderer measures (D-361). The slug above is now only a label.
            template.PageWidthMm,
            template.PageHeightMm);
    }

    /// <summary>The slot an image field names to print the recipient's own photo.</summary>
    public const string ParticipantPhotoSlot = "participant_photo";

    /// <summary>Pictures for a render, keyed by the slot an image element names.
    ///
    /// <para>Only the participant photo exists today, and it comes from the recipient's linked account —
    /// a certificate issued to a bare name and email from a spreadsheet has no photo to print, and the
    /// field then renders nothing. That is the correct empty state, not an error: a bulk run of two
    /// hundred certificates must not fail because eleven recipients have no avatar.</para></summary>
    /// <param name="recipientUserId">The recipient's linked account, when one is known.</param>
    /// <param name="normalizedEmail">Used only to find a participant of THIS event when the recipient is
    /// not yet linked — see <see cref="ResolvePhotoUserAsync"/> for why that scope matters.</param>
    private async Task<IReadOnlyDictionary<string, byte[]>> ResolveImagesAsync(
        CertificateTemplate template, Guid? recipientUserId, string? normalizedEmail, CancellationToken ct)
    {
        var images = new Dictionary<string, byte[]>(StringComparer.Ordinal);

        var userId = await ResolvePhotoUserAsync(template.EventId, recipientUserId, normalizedEmail, ct);
        if (userId is null) return images;

        var avatarKey = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId).Select(u => u.AvatarKey).FirstOrDefaultAsync(ct);
        if (string.IsNullOrWhiteSpace(avatarKey)) return images;

        try
        {
            if (await storage.ExistsAsync(avatarKey, ct))
                images[ParticipantPhotoSlot] = await storage.GetAsync(avatarKey, ct);
        }
        catch { /* issue the certificate without the photo */ }

        return images;
    }

    /// <summary>Whose photo, if anyone's.
    ///
    /// <para>An explicit link wins: <c>CertificateRecipient.UserId</c> is set when a person claims their
    /// certificate, and is the authoritative answer.</para>
    ///
    /// <para>Otherwise the recipient's email is matched against **participants of this event only**. That
    /// scope is the whole safeguard. A recipient is a name and an email typed into a spreadsheet, and an
    /// email address is not proof of identity — matching globally would let a typo or a stale address put
    /// a stranger's face on a printed certificate. Requiring the match to also be someone the organiser
    /// already has at this event makes a wrong photo require two independent mistakes rather than one,
    /// and keeps the blast radius inside the event the organiser controls.</para></summary>
    private async Task<Guid?> ResolvePhotoUserAsync(
        Guid? eventId, Guid? recipientUserId, string? normalizedEmail, CancellationToken ct)
    {
        if (recipientUserId is { } linked) return linked;
        if (eventId is not { } id || string.IsNullOrWhiteSpace(normalizedEmail)) return null;

        return await db.Users.AsNoTracking()
            .Where(u => u.Email != null && u.Email.ToLower() == normalizedEmail)
            .Where(u => db.EventParticipants.Any(p =>
                p.EventId == id
                && p.SubjectType == ParticipantSubjectType.Person
                && p.SubjectId == u.Id))
            .Select(u => (Guid?)u.Id)
            .FirstOrDefaultAsync(ct);
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
