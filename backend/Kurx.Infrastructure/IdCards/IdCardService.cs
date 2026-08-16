using Kurx.Application.Abstractions;
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

        return ServiceResult<IReadOnlyList<BadgeRecipient>>.Success(await LoadRecipientsAsync(eventId, ct));
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

        var header = await EventHeaderAsync(eventId, ct);
        var pdf = await renderer.RenderPdfAsync(
            BadgeLayout.Build(recipient, size), await RenderDataAsync(recipient, header, ct), ct);

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

        var wanted = request.UserIds is { Count: > 0 } ? request.UserIds.ToHashSet() : null;
        var recipients = (await LoadRecipientsAsync(eventId, ct))
            .Where(r => request.Kinds.Contains(r.Kind))
            .Where(r => wanted is null || wanted.Contains(r.UserId))
            .ToList();

        if (recipients.Count == 0) return ServiceResult<byte[]>.Fail("no_recipients");

        var header = await EventHeaderAsync(eventId, ct);
        var pngs = new List<byte[]>(recipients.Count);
        foreach (var r in recipients)
        {
            var data = await RenderDataAsync(r, header, ct);
            pngs.Add(await renderer.RenderPngAsync(BadgeLayout.Build(r, size), data, PrintDpi, ct));
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

    private async Task<CertificateRenderData> RenderDataAsync(
        BadgeRecipient r, EventHeader header, CancellationToken ct)
    {
        var values = new Dictionary<string, string>
        {
            [BadgeLayout.FieldName] = r.Name,
            [BadgeLayout.FieldSubtitle] = r.Subtitle ?? "",
            [BadgeLayout.FieldAccess] = r.AccessLevel ?? "",
            [BadgeLayout.FieldEvent] = header.Name,
            [BadgeLayout.FieldEventDate] = header.Date,
            // Not IdCard.CardNumber: no id_cards row is created for a badge print run. The person's
            // identifier at this event is their QR, and the printed line is a human-readable echo of it.
            [BadgeLayout.FieldCardNumber] = ShortCode(r.QrPayload),
        };

        var images = new Dictionary<string, byte[]>();
        if (!string.IsNullOrWhiteSpace(r.PhotoKey))
        {
            // A missing or unreadable photo must never fail a two-hundred-badge print run: the layout
            // simply renders no image, which is the documented no-photo case.
            try
            {
                if (await storage.ExistsAsync(r.PhotoKey, ct))
                    images[BadgeLayout.PhotoSlot] = await storage.GetAsync(r.PhotoKey, ct);
            }
            catch { /* print the badge without a photo */ }
        }

        return new CertificateRenderData(values, images, r.QrPayload);
    }

    /// <summary>The last eight characters of the payload, upper-cased — enough for a marshal to read one
    /// badge back over a radio, and short enough to fit the footer.</summary>
    private static string ShortCode(string payload)
    {
        var tail = payload.Split(':').Last();
        return tail.Length <= 8 ? tail.ToUpperInvariant() : tail[^8..].ToUpperInvariant();
    }
}
