using System.Security.Cryptography;
using System.Text.Json;
using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Infrastructure.Events;

public class CertificateService(
    KurxDbContext db,
    IEventAuthority authority,
    ICertificateRenderer renderer,
    IStorage storage,
    IEmailSender emailSender) : ICertificateService
{
    public async Task<ServiceResult<IReadOnlyList<CertificateRosterView>>> ListForEventAsync(Guid userId, Guid eventId, bool isAdmin, CancellationToken ct = default)
    {
        var ev = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventId, ct);
        if (ev is null) return ServiceResult<IReadOnlyList<CertificateRosterView>>.Fail("not_found");
        if (!(await authority.ResolveAsync(userId, ev.Id, isAdmin, ct)).Can(EventPermission.ManageContent))
            return ServiceResult<IReadOnlyList<CertificateRosterView>>.Fail("forbidden");

        var rows = await db.Certificates.AsNoTracking()
            .Where(c => c.EventId == eventId)
            .Join(db.Users.AsNoTracking(), c => c.UserId, u => u.Id, (c, u) => new { c, HolderName = u.Name })
            .OrderByDescending(x => x.c.CreatedAt)
            .ToListAsync(ct);

        IReadOnlyList<CertificateRosterView> views = rows.Select(x => new CertificateRosterView(x.c.Id, x.c.EventId,
            x.c.VerifyCode, x.c.UserId, x.HolderName, x.c.Kind.ToString(), x.c.Status.ToString(),
            x.c.IsRevoked, x.c.RevokedReason, x.c.CreatedAt)).ToList();
        return ServiceResult<IReadOnlyList<CertificateRosterView>>.Success(views);
    }

    public async Task<ServiceResult<int>> GenerateForEventAsync(Guid userId, Guid eventId, bool isAdmin, CancellationToken ct = default)
    {
        var ev = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventId, ct);
        if (ev is null) return ServiceResult<int>.Fail("not_found");

        if (!(await authority.ResolveAsync(userId, ev.Id, isAdmin, ct)).Can(EventPermission.ManageContent))
            return ServiceResult<int>.Fail("forbidden");

        // Eligibility: if the event requires check-in for certificates, only checked-in tickets
        // qualify; otherwise any non-void issued ticket does. Guest (no UserId) tickets are skipped
        // — a certificate always belongs to a specific user.
        // Projected, not whole entities: only Id and UserId are used below, and a Ticket carries a jsonb
        // answers payload that would otherwise be loaded for every attendee and never read.
        var eligibleTickets = await db.Tickets.AsNoTracking()
            .Where(t => t.EventId == eventId && t.UserId != null
                && (ev.CertificatesEnabled ? t.State == TicketState.CheckedIn : t.State != TicketState.Void))
            .Select(t => new { t.Id, UserId = t.UserId!.Value })
            .ToListAsync(ct);

        if (eligibleTickets.Count == 0) return ServiceResult<int>.Success(0);

        var alreadyGenerated = await db.Certificates.AsNoTracking()
            .Where(c => c.EventId == eventId)
            .Select(c => c.TicketId)
            .ToListAsync(ct);
        var alreadyGeneratedSet = alreadyGenerated.ToHashSet();

        var pending = eligibleTickets.Where(t => !alreadyGeneratedSet.Contains(t.Id)).ToList();
        if (pending.Count == 0) return ServiceResult<int>.Success(0);

        // DB-3: batch-hydrate the recipients instead of one query per ticket.
        //
        // This was `db.Users.FirstOrDefaultAsync(u => u.Id == ticket.UserId)` INSIDE the loop below — at a
        // 5,000-attendee event, 5,000 round trips for data that is two columns wide. Distinct() matters:
        // one user can hold several tickets to the same event, and the old code re-fetched them per ticket.
        // Same batched-hydration shape as AttendeeService.ComposeRowsAsync, deliberately — that is the
        // established idiom here, so this introduces no new data-loading pattern.
        var recipientIds = pending.Select(t => t.UserId).Distinct().ToList();
        var recipients = await db.Users.AsNoTracking()
            .Where(u => recipientIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Name, u.Email })
            .ToDictionaryAsync(u => u.Id, ct);

        // DB-3: the verify-code uniqueness probe was a SECOND N+1 — one `Certificates.AnyAsync(c =>
        // c.VerifyCode == code)` per certificate. Loading the event's existing codes once turns the
        // collision check into a HashSet probe. Codes are globally unique, so the in-memory set is seeded
        // from the full column; it is bounded by total certificates issued, which is what the old per-row
        // query scanned anyway.
        var usedVerifyCodes = (await db.Certificates.AsNoTracking()
            .Select(c => c.VerifyCode).ToListAsync(ct)).ToHashSet(StringComparer.Ordinal);

        var template = await ResolveTemplateAsync(eventId, ev.RepresentingOrgId, ct);
        var generated = 0;
        var sinceLastSave = 0;

        foreach (var ticket in pending)
        {
            ct.ThrowIfCancellationRequested();

            if (!recipients.TryGetValue(ticket.UserId, out var user)) continue;   // deleted mid-run

            var verifyCode = NextVerifyCode(usedVerifyCodes);
            var request = new CertificateRenderRequest(
                TemplateLayoutKey: template?.BaseLayout ?? "classic-certificate",
                RecipientName: user.Name,
                EventName: ev.Title,
                EventDate: ev.StartsAt.ToString("d MMM yyyy"),
                SignatoryName: SignatoryField(template, "name"),
                SignatoryTitle: SignatoryField(template, "title"),
                LogoKey: null,
                BackgroundKey: template?.FileKey,
                AccentColor: template?.AccentColor,
                QrDataUrl: $"https://kurx.in/verify/{verifyCode}");

            var rendered = await renderer.RenderAsync(request, ct);
            var pdfKey = $"certificates/{ticket.Id:N}.pdf";
            await storage.PutAsync(pdfKey, rendered.PdfBytes, "application/pdf", ct);

            var cert = new Certificate
            {
                EventId = eventId,
                TicketId = ticket.Id,
                UserId = ticket.UserId,
                TemplateId = template?.Id,
                VerifyCode = verifyCode,
                PdfKey = pdfKey,
                // Only Participation is derivable from data that exists today — Winner/RunnerUp/Finalist
                // need a Judging module (unbuilt); Volunteer/Judge/Speaker/Organizer need EventAssignment
                // eligibility sourcing (also unbuilt). Not faked here (D-036).
                Kind = CertificateKind.Participation,
                Status = CertificateStatus.Generated,
            };
            db.Certificates.Add(cert);
            generated++;

            if (!string.IsNullOrWhiteSpace(user.Email))
            {
                try
                {
                    await emailSender.SendAsync(user.Email, $"Your certificate for {ev.Title}",
                        $"<p>Hi {user.Name},</p><p>Your certificate for <strong>{ev.Title}</strong> is attached.</p>",
                        new[] { new EmailAttachment("certificate.pdf", "application/pdf", rendered.PdfBytes) }, ct);
                    cert.Status = CertificateStatus.Emailed;
                    cert.EmailedAt = DateTime.UtcNow;
                }
                catch
                {
                    cert.Status = CertificateStatus.Failed;
                    cert.EmailRetryCount++;
                }
            }

            // DB-3: commit incrementally rather than once at the very end.
            //
            // Previously a single SaveChangesAsync ran after the whole loop, so a failure at attendee 4,999
            // discarded all 4,998 certificate rows — while their PDFs had already been written to storage
            // and their emails already sent. The `alreadyGeneratedSet` idempotency guard reads committed
            // rows, so on a retry it protected nothing and every one of those emails was sent again.
            // Committing per batch makes that guard actually work: a re-run resumes rather than restarts.
            if (++sinceLastSave >= SaveBatchSize)
            {
                await db.SaveChangesAsync(ct);
                sinceLastSave = 0;
            }
        }

        if (sinceLastSave > 0) await db.SaveChangesAsync(ct);
        return ServiceResult<int>.Success(generated);
    }

    /// <summary>Rows per SaveChanges during generation. Small enough that a failure loses at most this many
    /// certificates' database rows (their PDFs survive in storage and are re-rendered on the next run);
    /// large enough that a big event does not pay a round trip per attendee. The rendering cost per
    /// certificate dwarfs the write, so a modest batch is free in practice.</summary>
    private const int SaveBatchSize = 50;

    /// <summary>Next unused verify code, checked against the in-memory set rather than a per-row query.
    ///
    /// <para>The set is mutated as codes are handed out, so two certificates in one run cannot collide.
    /// Cross-run and cross-replica collisions remain impossible for the same reason they always were —
    /// <c>certificates.VerifyCode</c> carries a unique index, so a genuine collision surfaces as a write
    /// failure rather than a duplicate. This is the cheap check; the index is the guarantee.</para></summary>
    private static string NextVerifyCode(HashSet<string> used)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var code = NewVerifyCode();
            if (used.Add(code)) return code;
        }
        // 100 consecutive collisions against a random code space is not bad luck; it means the generator
        // has lost its entropy. Failing loudly beats issuing a certificate whose verify URL resolves to
        // somebody else's.
        throw new InvalidOperationException(
            "Could not generate a unique certificate verify code after 100 attempts.");
    }

    public async Task<ServiceResult<bool>> RevokeAsync(Guid userId, Guid certificateId, string reason, bool isAdmin, CancellationToken ct = default)
    {
        var cert = await db.Certificates.FirstOrDefaultAsync(c => c.Id == certificateId, ct);
        if (cert is null) return ServiceResult<bool>.Fail("not_found");

        var ev = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == cert.EventId, ct);
        if (ev is null) return ServiceResult<bool>.Fail("not_found");

        if (!(await authority.ResolveAsync(userId, ev.Id, isAdmin, ct)).Can(EventPermission.ManageContent))
            return ServiceResult<bool>.Fail("forbidden");

        cert.IsRevoked = true;
        cert.RevokedReason = reason;
        cert.RevokedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return ServiceResult<bool>.Success(true);
    }

    /// <summary>Event-scoped → org-wide → system, per D-023's precedence.</summary>
    private async Task<DesignTemplate?> ResolveTemplateAsync(Guid eventId, Guid orgId, CancellationToken ct)
    {
        var eventScoped = await db.DesignTemplates.AsNoTracking()
            .Where(t => t.Kind == TemplateKind.Certificate && t.IsActive && t.EventId == eventId)
            .FirstOrDefaultAsync(ct);
        if (eventScoped is not null) return eventScoped;

        var orgWide = await db.DesignTemplates.AsNoTracking()
            .Where(t => t.Kind == TemplateKind.Certificate && t.IsActive && t.OrgId == orgId && t.EventId == null)
            .FirstOrDefaultAsync(ct);
        if (orgWide is not null) return orgWide;

        return await db.DesignTemplates.AsNoTracking()
            .Where(t => t.Kind == TemplateKind.Certificate && t.IsActive && t.OrgId == null && t.EventId == null)
            .OrderBy(t => t.Name)
            .FirstOrDefaultAsync(ct);
    }

    private static string? SignatoryField(DesignTemplate? template, string key)
    {
        if (string.IsNullOrWhiteSpace(template?.SignatoryJson)) return null;
        try
        {
            using var doc = JsonDocument.Parse(template.SignatoryJson);
            return doc.RootElement.TryGetProperty(key, out var v) ? v.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>A candidate verify code. Alphabet and length are unchanged from the original generator —
    /// 10 characters over a 32-symbol set excluding 0/O/1/I, which people read off a printed certificate.</summary>
    private static string NewVerifyCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // no 0/O/1/I ambiguity
        var bytes = RandomNumberGenerator.GetBytes(10);
        return new string(bytes.Select(b => chars[b % chars.Length]).ToArray());
    }
}
