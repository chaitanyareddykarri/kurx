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

        var existing = OldestPerHolder(await db.IdCards.Where(c => c.EventId == eventId).ToListAsync(ct));

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

            var data = await RenderDataAsync(r, header, card, spec, ct);
            var doc = BadgeLayout.Build(r, size, spec, artwork, data.Values);
            var pdf = await renderer.RenderPdfAsync(doc, data, ct);
            var png = await renderer.RenderPngAsync(doc, data, PrintDpi, ct);

            card.PdfKey = IdCardStorageKeys.Pdf(eventId, card.Id, size.Key);
            card.PngKey = IdCardStorageKeys.Png(eventId, card.Id, size.Key);
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

    /// <summary>One card per holder, oldest first (D-386).
    ///
    /// <para>Until the unique index landed, two concurrent <c>generate</c> calls could each see no
    /// existing card and create one. `ToDictionaryAsync(c =&gt; c.UserId)` then threw on the duplicate key,
    /// and because the roster read it too, the badge page failed permanently for that event — a transient
    /// race turned into a dead surface. The index prevents new duplicates; this keeps any row already
    /// written from taking the page down, and picks the <b>oldest</b> so the card number people are
    /// holding stays the authoritative one.</para></summary>
    private static Dictionary<Guid, IdCard> OldestPerHolder(IEnumerable<IdCard> cards) =>
        cards.GroupBy(c => c.UserId)
            .ToDictionary(g => g.Key, g => g.OrderBy(c => c.CreatedAt).ThenBy(c => c.Id).First());

    /// <summary>What kind of card this is, recorded on the row. Derived from what the holder is at this
    /// event.
    ///
    /// <para><b>It does not choose the layout</b> (D-386 — the old comment here said it did). Since D-362
    /// the rendered design comes from the event's <c>DesignTemplate</c> via <c>SpecAsync</c>, and
    /// <see cref="BadgeLayout"/> never consults this value. It is written and kept because it records what
    /// was issued — useful on a roster and in an audit — and read as anything more it would be wrong.</para></summary>
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
        var cards = OldestPerHolder(
            await db.IdCards.AsNoTracking().Where(c => c.EventId == eventId).ToListAsync(ct));

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

    public async Task<ServiceResult<IssuedCard>> RevokeAsync(
        Guid eventId, Guid actorId, bool isAdmin, Guid recipientUserId, string? reason,
        CancellationToken ct = default)
    {
        var gate = await RequireManagerAsync(eventId, actorId, isAdmin, ct);
        if (gate is not null) return ServiceResult<IssuedCard>.Fail(gate);

        // Ordered rather than a bare FirstOrDefault: it must revoke the same card the roster shows.
        var card = await db.IdCards
            .Where(c => c.EventId == eventId && c.UserId == recipientUserId)
            .OrderBy(c => c.CreatedAt).ThenBy(c => c.Id)
            .FirstOrDefaultAsync(ct);
        // Nothing was issued, so there is nothing to revoke — distinct from an already-revoked card, and
        // the console shows the two as different states.
        if (card is null) return ServiceResult<IssuedCard>.Fail("card_not_issued");
        if (card.IsRevoked) return ServiceResult<IssuedCard>.Fail("already_revoked");

        var now = DateTime.UtcNow;
        card.IsRevoked = true;
        card.Status = IdCardStatus.Revoked;
        // Trimmed to what the column and a verification page can carry; a null reason is allowed because
        // "revoked" is the fact and the why is often operational.
        card.RevokedReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim()[..Math.Min(reason.Trim().Length, 500)];
        card.RevokedAt = now;
        card.RevokedBy = actorId;
        card.UpdatedAt = now;

        db.AuditLogs.Add(new AuditLog
        {
            ActorType = "user", ActorId = actorId,
            Action = "idcard.revoke", Entity = "id_cards", EntityId = card.Id,
            DetailsJson = $"{{\"event_id\":\"{eventId}\",\"user_id\":\"{recipientUserId}\"}}",
        });

        await db.SaveChangesAsync(ct);

        return ServiceResult<IssuedCard>.Success(new IssuedCard(
            card.Id, card.CardNumber, card.VerifyCode,
            card.EffectiveStatus(DateOnly.FromDateTime(now)).ToString(),
            card.IsRevoked, card.GeneratedAt));
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

        // Serve the issued artefact when one exists AT THIS SIZE: what the organizer downloads must be the
        // same bytes the card was issued as, not a fresh render that could differ after a template change.
        // The size is in the key (D-385), so a request for a size this card was never issued at misses and
        // renders fresh rather than serving a raster of the wrong physical shape.
        if (card is not null)
        {
            var key = IdCardStorageKeys.Pdf(eventId, card.Id, size.Key);
            if (await storage.ExistsAsync(key, ct))
                return ServiceResult<byte[]>.Success(await storage.GetAsync(key, ct));
        }

        var header = await EventHeaderAsync(eventId, ct);
        var spec = await SpecAsync(eventId, ct);
        var data = await RenderDataAsync(recipient, header, card, spec, ct);
        var pdf = await renderer.RenderPdfAsync(
            BadgeLayout.Build(recipient, size, spec, await TryReadAsync(spec.BackgroundKey, ct), data.Values),
            data, ct);

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

        var cards = OldestPerHolder(
            await db.IdCards.AsNoTracking().Where(c => c.EventId == eventId).ToListAsync(ct));

        var header = await EventHeaderAsync(eventId, ct);
        var spec = await SpecAsync(eventId, ct);
        var artwork = await TryReadAsync(spec.BackgroundKey, ct);
        var pngs = new List<byte[]>(recipients.Count);
        foreach (var r in recipients)
        {
            cards.TryGetValue(r.UserId, out var card);

            // An issued card prints from its stored raster, so the sheet and the individual download are
            // the same artefact — but only the raster issued AT THIS SIZE (D-385). Anything not yet issued,
            // or issued at another size, still renders, which is what makes the page usable before the
            // organizer commits to issuing and what keeps the size selector honest afterwards.
            if (card is not null)
            {
                var key = IdCardStorageKeys.Png(eventId, card.Id, size.Key);
                if (await storage.ExistsAsync(key, ct))
                {
                    pngs.Add(await storage.GetAsync(key, ct));
                    continue;
                }
            }

            var data = await RenderDataAsync(r, header, card, spec, ct);
            pngs.Add(await renderer.RenderPngAsync(
                BadgeLayout.Build(r, size, spec, artwork, data.Values), data, PrintDpi, ct));
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
                a.Id, a.UserId, a.Role, a.CustomRole, u.Name, u.Username, u.AvatarKey,
            })
            .ToListAsync(ct);

        var staffCards = staff.Select(s =>
        {
            var role = string.Equals(s.Role, "Custom", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(s.CustomRole)
                ? s.CustomRole!
                : s.Role;
            return new BadgeRecipient(
                s.UserId, DisplayName(s.Name, s.Username), BadgeKind.Staff, role, StaffAccess.LevelFor(role), s.AvatarKey,
                // The signature is recomputed at the gate from the assignment id, so the badge carries
                // both and needs no stored column (D-362).
                $"staff:{s.Id}:{tokens.SignStaffPass(s.Id)}");
        }).ToList();

        var staffUserIds = staffCards.Select(s => s.UserId).ToHashSet();

        var attendees = await db.Tickets.AsNoTracking()
            .Where(t => t.EventId == eventId && t.UserId != null && t.State != TicketState.Void)
            .Join(db.Users.AsNoTracking(), t => t.UserId, u => u.Id, (t, u) => new { t.Code, t.UserId, t.OrderItemId, u.Name, u.Username, u.AvatarKey })
            .Join(db.OrderItems.AsNoTracking(), t => t.OrderItemId, oi => oi.Id, (t, oi) => new { t, oi.TicketTypeId })
            .Join(db.TicketTypes.AsNoTracking(), x => x.TicketTypeId, tt => tt.Id, (x, tt) => new
            {
                x.t.Code, x.t.UserId, x.t.Name, x.t.Username, x.t.AvatarKey, Tier = tt.Name,
            })
            .ToListAsync(ct);

        var attendeeCards = attendees
            .Where(a => !staffUserIds.Contains(a.UserId!.Value))
            // One badge per person even if they hold several tickets — the badge identifies the human.
            .GroupBy(a => a.UserId!.Value)
            .Select(g => g.First())
            .Select(a => new BadgeRecipient(
                a.UserId!.Value, DisplayName(a.Name, a.Username), BadgeKind.Attendee, a.Tier, null, a.AvatarKey,
                // Exactly what TicketQrEndpoints encodes, so the existing gate scan resolves a printed
                // badge with no change at all.
                a.Code.ToString()))
            .ToList();

        return [.. staffCards.OrderBy(s => s.Name), .. attendeeCards.OrderBy(a => a.Name)];
    }

    /// <summary>The name a badge prints. An account whose display name was never filled in falls back to
    /// its public handle, which is real data rather than an invented one; when there is neither, the empty
    /// string travels on and <see cref="BadgeLayout"/> drops the field rather than printing a placeholder
    /// (D-385). What must never happen is a printed badge reading <c>{holder_name}</c>.</summary>
    private static string DisplayName(string? name, string? username) =>
        !string.IsNullOrWhiteSpace(name) ? name
        : !string.IsNullOrWhiteSpace(username) ? username
        : "";

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
