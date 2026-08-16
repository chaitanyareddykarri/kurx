using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Auth;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Infrastructure.IdCards;

/// <summary>
/// Event badges for the people running and attending an event (D-362, finishing D-331's event-badge half).
///
/// <para><b>Every operation here is organizer-only.</b> Manager authority, checked live through
/// <see cref="IEventAuthority"/> per D-015 — not Staff, even though Staff can already view the attendee
/// list. A badge is an entry credential: whoever can mint one can mint one for anybody, which is a
/// different power from being able to read a name.</para>
/// </summary>
public class IdCardService(
    KurxDbContext db,
    IEventAuthority authority,
    TokenService tokens,
    ICertificateDocumentRenderer renderer,
    IStorage storage) : IIdCardService
{
    /// <summary>Printed at 300dpi: these are guillotined onto lanyards, and 150dpi text is visibly soft
    /// on paper at badge size.</summary>
    private const int PrintDpi = 300;

    public async Task<ServiceResult<IReadOnlyList<BadgeRecipient>>> ListRecipientsAsync(
        Guid eventId, Guid actorId, bool isAdmin, CancellationToken ct = default)
    {
        var gate = await RequireManagerAsync(eventId, actorId, isAdmin, ct);
        if (gate is not null) return ServiceResult<IReadOnlyList<BadgeRecipient>>.Fail(gate);

        return ServiceResult<IReadOnlyList<BadgeRecipient>>.Success(
            await AttachIssuedCardsAsync(eventId, await LoadRecipientsAsync(eventId, ct), ct));
    }

    public async Task<ServiceResult<BadgeIssueReport>> GenerateAsync(
        Guid eventId, Guid actorId, bool isAdmin, BadgeIssueRequest request, CancellationToken ct = default)
    {
        var gate = await RequireManagerAsync(eventId, actorId, isAdmin, ct);
        if (gate is not null) return ServiceResult<BadgeIssueReport>.Fail(gate);

        var size = BadgeSize.FromKey(request.SizeKey);
        if (size is null) return ServiceResult<BadgeIssueReport>.Fail("unknown_badge_size");
        if (request.Kinds is null || request.Kinds.Count == 0)
            return ServiceResult<BadgeIssueReport>.Fail("no_audience_selected");

        var recipients = Filter(await LoadRecipientsAsync(eventId, ct), request.Kinds, request.UserIds);
        if (recipients.Count == 0) return ServiceResult<BadgeIssueReport>.Fail("no_recipients");

        var ev = await db.Events.AsNoTracking()
            .Where(e => e.Id == eventId)
            .Select(e => new { e.Title, e.StartsAt, e.RepresentingOrgId })
            .FirstAsync(ct);
        var header = new EventHeader(ev.Title, ev.StartsAt.ToString("dd MMM yyyy"));

        var existing = await db.IdCards
            .Where(c => c.EventId == eventId)
            .ToDictionaryAsync(c => c.UserId, ct);

        var spec = await SpecAsync(eventId, ct);
        // Read once for the whole run rather than per badge: the artwork cannot change mid-print, and a
        // two-hundred-card run would otherwise fetch the same image two hundred times.
        var artwork = await TryReadAsync(spec.BackgroundKey, ct);
        var newCount = recipients.Count(r => !existing.ContainsKey(r.UserId));
        var numbers = await IdCardCodes.AllocateCardNumbersAsync(db, ev.RepresentingOrgId, newCount, ct);

        var now = DateTime.UtcNow;
        int issued = 0, regenerated = 0;

        foreach (var r in recipients)
        {
            if (!existing.TryGetValue(r.UserId, out var card))
            {
                card = new IdCard
                {
                    OrgId = ev.RepresentingOrgId,
                    UserId = r.UserId,
                    EventId = eventId,
                    CardNumber = numbers.Dequeue(),
                    VerifyCode = IdCardCodes.NewVerifyCode(),
                    IssuedBy = actorId,
                    // Snapshotted at issue time (D-331): changing an avatar later must not silently
                    // invalidate every badge already printed from it.
                    PhotoKey = r.PhotoKey,
                    Template = TemplateFor(r),
                    Status = IdCardStatus.Active,
                    CreatedAt = now,
                };
                db.IdCards.Add(card);
                existing[r.UserId] = card;
                issued++;
            }
            else
            {
                regenerated++;
            }

            var doc = BadgeLayout.Build(r, size, spec, artwork);
            var data = await RenderDataAsync(r, header, card, spec, ct);
            var pdf = await renderer.RenderPdfAsync(doc, data, ct);
            var png = await renderer.RenderPngAsync(doc, data, PrintDpi, ct);

            card.PdfKey = IdCardStorageKeys.Pdf(eventId, card.Id);
            card.PngKey = IdCardStorageKeys.Png(eventId, card.Id);
            await storage.PutAsync(card.PdfKey, pdf, "application/pdf", ct);
            await storage.PutAsync(card.PngKey, png, "image/png", ct);

            card.GeneratedAt = now;
            card.UpdatedAt = now;
        }

        db.AuditLogs.Add(new AuditLog
        {
            ActorType = "user", ActorId = actorId,
            Action = "idcard.generate", Entity = "events", EntityId = eventId,
            DetailsJson = $"{{\"issued\":{issued},\"regenerated\":{regenerated},\"size\":\"{size.Key}\"}}",
        });

        await db.SaveChangesAsync(ct);
        return ServiceResult<BadgeIssueReport>.Success(new BadgeIssueReport(issued, regenerated));
    }

    /// <summary>Which rendering layout a recipient's card uses. Derived from what they are at this event;
    /// the editor will later let an organizer override it per card via <c>IdCard.Template</c>.</summary>
    private static IdCardTemplate TemplateFor(BadgeRecipient r) =>
        r.Kind == BadgeKind.Attendee ? IdCardTemplate.EventParticipant
        : r.AccessLevel == "Volunteer" ? IdCardTemplate.Volunteer
        : IdCardTemplate.StaffFaculty;

    private static List<BadgeRecipient> Filter(
        IReadOnlyList<BadgeRecipient> all, IReadOnlyList<BadgeKind> kinds, IReadOnlyList<Guid>? userIds)
    {
        var wanted = userIds is { Count: > 0 } ? userIds.ToHashSet() : null;
        return all.Where(r => kinds.Contains(r.Kind))
            .Where(r => wanted is null || wanted.Contains(r.UserId))
            .ToList();
    }

    /// <summary>Joins each recipient to their issued card, so the console can show what exists rather than
    /// only who could be printed.</summary>
    private async Task<IReadOnlyList<BadgeRecipient>> AttachIssuedCardsAsync(
        Guid eventId, IReadOnlyList<BadgeRecipient> recipients, CancellationToken ct)
    {
        var cards = await db.IdCards.AsNoTracking()
            .Where(c => c.EventId == eventId)
            .ToDictionaryAsync(c => c.UserId, ct);

        return recipients.Select(r => cards.TryGetValue(r.UserId, out var c)
            ? r with
            {
                Card = new IssuedCard(
                    c.Id, c.CardNumber, c.VerifyCode,
                    c.EffectiveStatus(DateOnly.FromDateTime(DateTime.UtcNow)).ToString(),
                    c.IsRevoked, c.GeneratedAt),
            }
            : r).ToList();
    }

    public async Task<ServiceResult<byte[]>> RenderOneAsync(
        Guid eventId, Guid actorId, bool isAdmin, Guid recipientUserId, string sizeKey,
        CancellationToken ct = default)
    {
        var gate = await RequireManagerAsync(eventId, actorId, isAdmin, ct);
        if (gate is not null) return ServiceResult<byte[]>.Fail(gate);

        var size = BadgeSize.FromKey(sizeKey);
        if (size is null) return ServiceResult<byte[]>.Fail("unknown_badge_size");

        var recipient = (await LoadRecipientsAsync(eventId, ct)).FirstOrDefault(r => r.UserId == recipientUserId);
        if (recipient is null) return ServiceResult<byte[]>.Fail("recipient_not_found");

        var card = await db.IdCards.AsNoTracking()
            .FirstOrDefaultAsync(c => c.EventId == eventId && c.UserId == recipientUserId, ct);

        // Serve the issued artefact when one exists: what the organizer downloads must be the same bytes
        // the card was issued as, not a fresh render that could differ after a template change.
        if (card?.PdfKey is { } key && await storage.ExistsAsync(key, ct))
            return ServiceResult<byte[]>.Success(await storage.GetAsync(key, ct));

        var header = await EventHeaderAsync(eventId, ct);
        var spec = await SpecAsync(eventId, ct);
        var pdf = await renderer.RenderPdfAsync(
            BadgeLayout.Build(recipient, size, spec, await TryReadAsync(spec.BackgroundKey, ct)),
            await RenderDataAsync(recipient, header, card, spec, ct), ct);

        return ServiceResult<byte[]>.Success(pdf);
    }

    public async Task<ServiceResult<byte[]>> RenderSheetAsync(
        Guid eventId, Guid actorId, bool isAdmin, BadgeSheetRequest request, CancellationToken ct = default)
    {
        var gate = await RequireManagerAsync(eventId, actorId, isAdmin, ct);
        if (gate is not null) return ServiceResult<byte[]>.Fail(gate);

        var size = BadgeSize.FromKey(request.SizeKey);
        if (size is null) return ServiceResult<byte[]>.Fail("unknown_badge_size");
        if (request.Kinds is null || request.Kinds.Count == 0)
            return ServiceResult<byte[]>.Fail("no_audience_selected");

        var recipients = Filter(await LoadRecipientsAsync(eventId, ct), request.Kinds, request.UserIds);
        if (recipients.Count == 0) return ServiceResult<byte[]>.Fail("no_recipients");

        var cards = await db.IdCards.AsNoTracking()
            .Where(c => c.EventId == eventId)
            .ToDictionaryAsync(c => c.UserId, ct);

        var header = await EventHeaderAsync(eventId, ct);
        var spec = await SpecAsync(eventId, ct);
        var artwork = await TryReadAsync(spec.BackgroundKey, ct);
        var pngs = new List<byte[]>(recipients.Count);
        foreach (var r in recipients)
        {
            cards.TryGetValue(r.UserId, out var card);

            // An issued card prints from its stored raster, so the sheet and the individual download are
            // the same artefact. Anything not yet issued still previews, which is what makes the page
            // usable before the organizer commits to issuing.
            if (card?.PngKey is { } key && await storage.ExistsAsync(key, ct))
            {
                pngs.Add(await storage.GetAsync(key, ct));
                continue;
            }

            var data = await RenderDataAsync(r, header, card, spec, ct);
            pngs.Add(await renderer.RenderPngAsync(
                BadgeLayout.Build(r, size, spec, artwork), data, PrintDpi, ct));
        }

        return ServiceResult<byte[]>.Success(
            BadgeSheetComposer.Compose(pngs, size.WidthMm, size.HeightMm));
    }

    // ── Internals ───────────────────────────────────────────────────────────────────────────────────

    private async Task<string?> RequireManagerAsync(Guid eventId, Guid actorId, bool isAdmin, CancellationToken ct)
    {
        var access = await authority.ResolveAsync(actorId, eventId, isAdmin, ct);
        if (!access.EventExists) return "not_found";
        // 404, not 403, for a caller with no standing — D-018: a hidden resource must not confirm it exists.
        if (!access.HasStanding) return "not_found";
        return access.Can(EventPermission.ManageContent) ? null : "forbidden";
    }

    private sealed record EventHeader(string Name, string Date);

    private async Task<EventHeader> EventHeaderAsync(Guid eventId, CancellationToken ct)
    {
        var e = await db.Events.AsNoTracking()
            .Where(x => x.Id == eventId)
            .Select(x => new { x.Title, x.StartsAt })
            .FirstAsync(ct);
        return new EventHeader(e.Title, e.StartsAt.ToString("dd MMM yyyy"));
    }

    /// <summary>Ticket holders and accepted staff, in one list.
    ///
    /// <para>A person holding both a ticket and a staff assignment appears once, as staff: the badge that
    /// matters at a door is the one asserting the greater authority, and printing someone two lanyards
    /// invites them to wear the wrong one.</para></summary>
    private async Task<IReadOnlyList<BadgeRecipient>> LoadRecipientsAsync(Guid eventId, CancellationToken ct)
    {
        var staff = await db.EventAssignments.AsNoTracking()
            .Where(a => a.EventId == eventId && a.Status == AssignmentStatus.Accepted)
            .Join(db.Users.AsNoTracking(), a => a.UserId, u => u.Id, (a, u) => new
            {
                a.Id, a.UserId, a.Role, a.CustomRole, u.Name, u.AvatarKey,
            })
            .ToListAsync(ct);

        var staffCards = staff.Select(s =>
        {
            var role = string.Equals(s.Role, "Custom", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(s.CustomRole)
                ? s.CustomRole!
                : s.Role;
            return new BadgeRecipient(
                s.UserId, s.Name, BadgeKind.Staff, role, AccessLevelFor(role), s.AvatarKey,
                // The signature is recomputed at the gate from the assignment id, so the badge carries
                // both and needs no stored column (D-362).
                $"staff:{s.Id}:{tokens.SignStaffPass(s.Id)}");
        }).ToList();

        var staffUserIds = staffCards.Select(s => s.UserId).ToHashSet();

        var attendees = await db.Tickets.AsNoTracking()
            .Where(t => t.EventId == eventId && t.UserId != null && t.State != TicketState.Void)
            .Join(db.Users.AsNoTracking(), t => t.UserId, u => u.Id, (t, u) => new { t.Code, t.UserId, t.OrderItemId, u.Name, u.AvatarKey })
            .Join(db.OrderItems.AsNoTracking(), t => t.OrderItemId, oi => oi.Id, (t, oi) => new { t, oi.TicketTypeId })
            .Join(db.TicketTypes.AsNoTracking(), x => x.TicketTypeId, tt => tt.Id, (x, tt) => new
            {
                x.t.Code, x.t.UserId, x.t.Name, x.t.AvatarKey, Tier = tt.Name,
            })
            .ToListAsync(ct);

        var attendeeCards = attendees
            .Where(a => !staffUserIds.Contains(a.UserId!.Value))
            // One badge per person even if they hold several tickets — the badge identifies the human.
            .GroupBy(a => a.UserId!.Value)
            .Select(g => g.First())
            .Select(a => new BadgeRecipient(
                a.UserId!.Value, a.Name, BadgeKind.Attendee, a.Tier, null, a.AvatarKey,
                // Exactly what TicketQrEndpoints encodes, so the existing gate scan resolves a printed
                // badge with no change at all.
                a.Code.ToString()))
            .ToList();

        return [.. staffCards.OrderBy(s => s.Name), .. attendeeCards.OrderBy(a => a.Name)];
    }

    /// <summary>What a staff role authorises on site. Derived from the role rather than stored, because
    /// <c>EventAssignment</c> has no access-level column and inventing one would put a second, drifting
    /// answer next to the role that already decides this.</summary>
    private static string AccessLevelFor(string role) => role.Trim().ToLowerInvariant() switch
    {
        "owner" or "manager" or "organizer" => "All Access",
        "speaker" or "judge" or "performer" => "Backstage",
        "sponsor" or "vendor" or "exhibitor" => "Vendor",
        "volunteer" => "Volunteer",
        _ => "Staff",
    };

    /// <summary>The event's saved card design, or the shipped default when the editor has never been
    /// opened (D-362). Read once per operation rather than per badge: a print run renders hundreds, and
    /// the design cannot change mid-run.</summary>
    private async Task<IdCardTemplateSpec> SpecAsync(Guid eventId, CancellationToken ct)
    {
        var json = await db.DesignTemplates.AsNoTracking()
            .Where(t => t.EventId == eventId && t.Kind == TemplateKind.IdCard && t.IsActive)
            .Select(t => t.PlacementsJson)
            .FirstOrDefaultAsync(ct);

        if (string.IsNullOrWhiteSpace(json) || json == "{}") return IdCardTemplateSpec.Default;
        try
        {
            return System.Text.Json.JsonSerializer
                .Deserialize<IdCardTemplateSpec>(json, new System.Text.Json.JsonSerializerOptions(
                    System.Text.Json.JsonSerializerDefaults.Web))?.Sanitised()
                ?? IdCardTemplateSpec.Default;
        }
        catch (System.Text.Json.JsonException)
        {
            return IdCardTemplateSpec.Default;
        }
    }

    private async Task<CertificateRenderData> RenderDataAsync(
        BadgeRecipient r, EventHeader header, IdCard? card, IdCardTemplateSpec spec, CancellationToken ct)
    {
        var values = new Dictionary<string, string>
        {
            [BadgeLayout.FieldName] = r.Name,
            [BadgeLayout.FieldSubtitle] = r.Subtitle ?? "",
            [BadgeLayout.FieldAccess] = r.AccessLevel ?? "",
            [BadgeLayout.FieldEvent] = header.Name,
            [BadgeLayout.FieldEventDate] = header.Date,
            // The issued card's real number once one exists. The QR-derived fallback only covers the
            // preview path, where nothing has been issued yet — a printed badge always carries the number
            // its id_cards row was allocated.
            [BadgeLayout.FieldCardNumber] = card?.CardNumber ?? ShortCode(r.QrPayload),
        };

        // A missing or unreadable image must never fail a two-hundred-badge print run: the layout simply
        // renders nothing there, which is the documented no-photo case.
        var images = new Dictionary<string, byte[]>();
        await TryAddImageAsync(images, BadgeLayout.PhotoSlot, r.PhotoKey, ct);
        await TryAddImageAsync(images, BadgeLayout.LogoSlot, spec.LogoKey, ct);

        return new CertificateRenderData(values, images, r.QrPayload);
    }

    private async Task TryAddImageAsync(
        Dictionary<string, byte[]> images, string slot, string? key, CancellationToken ct)
    {
        if (await TryReadAsync(key, ct) is { } bytes) images[slot] = bytes;
    }

    /// <summary>Reads an asset, or null. A missing image must never fail a print run — the badge simply
    /// prints without it, which is the documented no-photo case.</summary>
    private async Task<byte[]?> TryReadAsync(string? key, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        try
        {
            return await storage.ExistsAsync(key, ct) ? await storage.GetAsync(key, ct) : null;
        }
        catch { return null; }
    }

    /// <summary>The last eight characters of the payload, upper-cased — enough for a marshal to read one
    /// badge back over a radio, and short enough to fit the footer.</summary>
    private static string ShortCode(string payload)
    {
        var tail = payload.Split(':').Last();
        return tail.Length <= 8 ? tail.ToUpperInvariant() : tail[^8..].ToUpperInvariant();
    }
}
