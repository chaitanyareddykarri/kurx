using System.Security.Cryptography;
using System.Text;
using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Auth;
using Kurx.Infrastructure.Localization;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Kurx.Infrastructure.Events;

public class InvitationService(
    KurxDbContext db,
    IEventAuthority authority,
    IWhatsAppLogService waLog,
    IEmailSender emailSender,
    IPasswordHasher hasher,
    IStringLocalizer<SharedResources> loc) : IInvitationService
{
    private const int MaxSendCount = 3;
    private const int ResendCooldownHours = 24;
    private const int CsvMaxRows = 2000;

    public async Task<ServiceResult<InvitationView>> AddAsync(Guid invitedBy, Guid eventId, string? name, string? emailAddr, string? phone, string channel,
        string? username = null, Guid? groupId = null, CancellationToken ct = default)
    {
        // Competition team invite (D-036): authorized by the group's captain, not just org managers;
        // a Kurx username may resolve the recipient instead of requiring raw phone/email.
        if (groupId is not null)
        {
            if (!await IsGroupCaptainAsync(invitedBy, groupId.Value, ct) && !await IsOrgManagerAsync(invitedBy, eventId, ct))
                return ServiceResult<InvitationView>.Fail("forbidden");

            if (!string.IsNullOrWhiteSpace(username))
            {
                var found = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Username == username, ct);
                if (found is null) return ServiceResult<InvitationView>.Fail("username_not_found");
                phone = found.Phone;
                if (string.IsNullOrWhiteSpace(name)) name = found.Name;
            }
        }
        else if (!await IsOrgManagerAsync(invitedBy, eventId, ct))
        {
            return ServiceResult<InvitationView>.Fail("forbidden");
        }

        // D-266 M6 (D9 Method A) — invite an existing Kurx user by username. Needs no email address, no
        // phone number and no forwardable link: the invitee is already here, so they get an in-app
        // notification and accept or decline in place. Distinct from the D-036 team path above, which uses
        // a username merely to LOOK UP a phone; here the user id is the address the invitation is sent to.
        Guid? invitedUserId = null;
        if (groupId is null && !string.IsNullOrWhiteSpace(username))
        {
            var target = await db.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Username == username, ct);
            if (target is null) return ServiceResult<InvitationView>.Fail("username_not_found");
            invitedUserId = target.Id;
            // Method A needs no name — the invited user IS the identity — but Name is a non-null display
            // column, so fall back to the username. A user who signed up by OTP and never set a display
            // name has Name = "", and taking it verbatim would refuse the invitation for `name_required`:
            // a rule that exists for contact-detail invitations, where nothing else identifies the guest.
            if (string.IsNullOrWhiteSpace(name))
                name = string.IsNullOrWhiteSpace(target.Name) ? $"@{target.Username}" : target.Name;

            if (await db.EventInvitations.AnyAsync(i => i.EventId == eventId && i.InvitedUserId == target.Id, ct))
                return ServiceResult<InvitationView>.Fail("duplicate_invitation");
        }

        // Exactly one address per invitation (D9 rule 3). One addressed to nobody cannot be delivered; one
        // addressed two ways cannot say which identity accepted it.
        if (invitedUserId is null && string.IsNullOrWhiteSpace(emailAddr) && string.IsNullOrWhiteSpace(phone))
            return ServiceResult<InvitationView>.Fail("invite_target_required");
        if (string.IsNullOrWhiteSpace(name))
            return ServiceResult<InvitationView>.Fail("name_required");

        if (!Enum.TryParse<InvitationChannel>(channel, true, out var channelEnum))
            return ServiceResult<InvitationView>.Fail("invalid_channel");

        // Channel requirements apply to the contact-detail methods only. A Method A invitation is delivered
        // in-app, so demanding an email address for it would be demanding a detail the method exists to
        // avoid needing.
        if (invitedUserId is null)
        {
            if (channelEnum == InvitationChannel.Email && string.IsNullOrWhiteSpace(emailAddr))
                return ServiceResult<InvitationView>.Fail("email_required_for_channel");

            if (channelEnum == InvitationChannel.WhatsApp && string.IsNullOrWhiteSpace(phone))
                return ServiceResult<InvitationView>.Fail("phone_required_for_channel");
        }

        var normalizedEmail = emailAddr?.Trim().ToLowerInvariant();
        if (normalizedEmail is not null && await db.EventInvitations.AnyAsync(i => i.EventId == eventId && i.Email != null && i.Email.ToLower() == normalizedEmail, ct))
            return ServiceResult<InvitationView>.Fail("duplicate_email");

        var normalizedPhone = string.IsNullOrWhiteSpace(phone) ? null : AuthService.NormalizePhone(phone);
        if (normalizedPhone is not null && await db.EventInvitations.AnyAsync(i => i.EventId == eventId && i.Phone == normalizedPhone, ct))
            return ServiceResult<InvitationView>.Fail("duplicate_phone");

        var inv = new EventInvitation
        {
            EventId = eventId, InvitedBy = invitedBy, GroupId = groupId, Name = name,
            InvitedUserId = invitedUserId,
            Email = normalizedEmail, Phone = normalizedPhone, Channel = channelEnum,
            InviteToken = GenerateToken(),
        };
        db.EventInvitations.Add(inv);

        // D9: the invitee is told in-app. Method A's whole point is that no contact detail is needed, so
        // the notification IS the delivery — not a courtesy on top of an email.
        if (invitedUserId is { } notifyUserId)
        {
            var title = await db.Events.AsNoTracking().Where(e => e.Id == eventId)
                .Select(e => e.Title).FirstOrDefaultAsync(ct) ?? "an event";
            db.Notifications.Add(new Notification
            {
                UserId = notifyUserId,
                Kind = "invitation_received",
                Title = "You're invited",
                Body = $"You've been invited to {title}.",
                DataJson = $"{{\"event_id\":\"{eventId}\",\"invitation_id\":\"{inv.Id}\"}}",
            });
        }

        await db.SaveChangesAsync(ct);
        return ServiceResult<InvitationView>.Success(ToView(inv));
    }

    public async Task<ServiceResult<InvitationImportResult>> ImportCsvAsync(Guid invitedBy, Guid eventId, Stream csv, CancellationToken ct = default)
    {
        if (!await IsOrgManagerAsync(invitedBy, eventId, ct))
            return ServiceResult<InvitationImportResult>.Fail("forbidden");

        var rows = new List<(string name, string? email, string? phone, string channel)>();
        var errors = new List<ImportRowError>();
        using var reader = new StreamReader(csv, Encoding.UTF8);
        int line = 0;
        string? raw;
        while (rows.Count < CsvMaxRows && (raw = await reader.ReadLineAsync(ct)) != null)
        {
            line++;
            if (line == 1 && raw?.StartsWith("name", StringComparison.OrdinalIgnoreCase) == true) continue;
            if (string.IsNullOrWhiteSpace(raw)) continue;
            var parts = raw.Split(',');
            var name = parts[0].Trim();
            var emailPart = parts.Length > 1 ? parts[1].Trim() : "";
            var phonePart = parts.Length > 2 ? parts[2].Trim() : "";
            var channelPart = parts.Length > 3 ? parts[3].Trim() : "Email";
            if (string.IsNullOrEmpty(name)) { errors.Add(new(line, "name_required")); continue; }
            if (string.IsNullOrEmpty(emailPart) && string.IsNullOrEmpty(phonePart)) { errors.Add(new(line, "email_or_phone_required")); continue; }
            if (!Enum.TryParse<InvitationChannel>(channelPart, true, out _)) { errors.Add(new(line, "invalid_channel")); continue; }
            rows.Add((name, string.IsNullOrEmpty(emailPart) ? null : emailPart.ToLowerInvariant(), string.IsNullOrEmpty(phonePart) ? null : phonePart, channelPart));
        }

        var existingEmails = (await db.EventInvitations
            .Where(i => i.EventId == eventId && i.Email != null)
            .Select(i => i.Email!.ToLower()).ToListAsync(ct)).ToHashSet();
        var existingPhones = (await db.EventInvitations
            .Where(i => i.EventId == eventId && i.Phone != null)
            .Select(i => i.Phone!).ToListAsync(ct)).ToHashSet();

        int created = 0, dupes = 0;
        var seenEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenPhones = new HashSet<string>();

        foreach (var (name, emailR, phone, channel) in rows)
        {
            bool isDupe = false;
            if (emailR is not null && (existingEmails.Contains(emailR) || !seenEmails.Add(emailR))) isDupe = true;
            if (phone is not null)
            {
                var np = AuthService.NormalizePhone(phone);
                if (existingPhones.Contains(np) || !seenPhones.Add(np)) isDupe = true;
            }
            if (isDupe) { dupes++; continue; }

            db.EventInvitations.Add(new EventInvitation
            {
                EventId = eventId, InvitedBy = invitedBy, Name = name,
                Email = emailR, Phone = phone is null ? null : AuthService.NormalizePhone(phone),
                Channel = Enum.Parse<InvitationChannel>(channel, true),
                InviteToken = GenerateToken(),
            });
            created++;
        }
        if (created > 0) await db.SaveChangesAsync(ct);
        return ServiceResult<InvitationImportResult>.Success(new(created, dupes, errors));
    }

    public async Task<ServiceResult<int>> EnqueueSendAsync(Guid userId, Guid eventId, Guid[]? invitationIds, CancellationToken ct = default)
    {
        if (!await IsOrgManagerAsync(userId, eventId, ct))
            return ServiceResult<int>.Fail("forbidden");

        var q = db.EventInvitations.Where(i => i.EventId == eventId && i.Status == InvitationStatus.Active);
        q = invitationIds?.Length > 0
            ? q.Where(i => invitationIds.Contains(i.Id))
            : q.Where(i => i.SendStatus == InvitationSendStatus.Pending);

        var list = await q.ToListAsync(ct);
        foreach (var inv in list) inv.SendStatus = InvitationSendStatus.Queued;
        await db.SaveChangesAsync(ct);

        var ev = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventId, ct);
        foreach (var inv in list)
            await SendOneAsync(inv, ev, ct);

        return ServiceResult<int>.Success(list.Count);
    }

    public async Task<ServiceResult<InvitationView>> ResendAsync(Guid userId, Guid invitationId, CancellationToken ct = default)
    {
        var inv = await db.EventInvitations.FindAsync([invitationId], ct);
        if (inv is null) return ServiceResult<InvitationView>.Fail("not_found");
        if (!await IsOrgManagerAsync(userId, inv.EventId, ct)) return ServiceResult<InvitationView>.Fail("forbidden");
        if (inv.Status == InvitationStatus.Revoked) return ServiceResult<InvitationView>.Fail("revoked");
        if (inv.SendCount >= MaxSendCount) return ServiceResult<InvitationView>.Fail("max_sends_reached");
        if (inv.SentAt.HasValue && (DateTime.UtcNow - inv.SentAt.Value).TotalHours < ResendCooldownHours)
            return ServiceResult<InvitationView>.Fail("resend_too_soon");

        var ev = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == inv.EventId, ct);
        await SendOneAsync(inv, ev, ct);
        return ServiceResult<InvitationView>.Success(ToView(inv));
    }

    public async Task<ServiceResult<bool>> RevokeAsync(Guid userId, Guid invitationId, CancellationToken ct = default)
    {
        var inv = await db.EventInvitations.FindAsync([invitationId], ct);
        if (inv is null) return ServiceResult<bool>.Fail("not_found");
        if (!await IsOrgManagerAsync(userId, inv.EventId, ct)) return ServiceResult<bool>.Fail("forbidden");
        inv.Status = InvitationStatus.Revoked;
        db.AuditLogs.Add(new AuditLog
        {
            ActorType = "user", ActorId = userId, Action = "invitation.revoke",
            Entity = "event_invitations", EntityId = invitationId,
        });
        await db.SaveChangesAsync(ct);
        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<(List<InvitationView> Items, int Total, InvitationFunnel Funnel)>> ListAsync(
        Guid userId, Guid eventId, string? status, string? rsvp, string? search, int page, int pageSize, CancellationToken ct = default)
    {
        if (!await IsOrgManagerAsync(userId, eventId, ct))
            return ServiceResult<(List<InvitationView>, int, InvitationFunnel)>.Fail("forbidden");

        var q = db.EventInvitations.Where(i => i.EventId == eventId);
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<InvitationStatus>(status, true, out var s)) q = q.Where(i => i.Status == s);
        if (!string.IsNullOrWhiteSpace(rsvp) && Enum.TryParse<InvitationRsvpStatus>(rsvp, true, out var r)) q = q.Where(i => i.RsvpStatus == r);
        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(i => i.Name.Contains(search) || (i.Email != null && i.Email.Contains(search)) || (i.Phone != null && i.Phone.Contains(search)));

        var total = await q.CountAsync(ct);
        // DB-6: CSV import creates up to 2,000 invitations in one pass, all sharing a CreatedAt tick.
        var items = await q.OrderByDescending(i => i.CreatedAt).ThenByDescending(i => i.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        var funnelData = await db.EventInvitations.Where(i => i.EventId == eventId)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Sent = g.Count(i => i.SendStatus == InvitationSendStatus.Sent),
                Accepted = g.Count(i => i.RsvpStatus == InvitationRsvpStatus.Accepted),
                Declined = g.Count(i => i.RsvpStatus == InvitationRsvpStatus.Declined),
                Registered = g.Count(i => i.OrderId != null),
            })
            .FirstOrDefaultAsync(ct);

        var funnel = funnelData is null
            ? new InvitationFunnel(0, 0, 0, 0, 0)
            : new InvitationFunnel(funnelData.Total, funnelData.Sent, funnelData.Accepted, funnelData.Declined, funnelData.Registered);

        return ServiceResult<(List<InvitationView>, int, InvitationFunnel)>.Success((items.Select(ToView).ToList(), total, funnel));
    }

    public async Task<ServiceResult<PublicInvitationView>> GetPublicAsync(string token, CancellationToken ct = default)
    {
        var inv = await db.EventInvitations.AsNoTracking().FirstOrDefaultAsync(i => i.InviteToken == token, ct);
        if (inv is null || inv.Status == InvitationStatus.Revoked) return ServiceResult<PublicInvitationView>.Fail("not_found");

        var ev = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == inv.EventId, ct);
        if (ev is null) return ServiceResult<PublicInvitationView>.Fail("not_found");
        if (ev.EndsAt < DateTime.UtcNow) return ServiceResult<PublicInvitationView>.Fail("event_ended");

        var orgName = await db.Organizations.AsNoTracking().Where(o => o.Id == ev.RepresentingOrgId).Select(o => o.Name).FirstOrDefaultAsync(ct) ?? "";

        return ServiceResult<PublicInvitationView>.Success(new(
            inv.Name, inv.RsvpStatus.ToString(),
            ev.Title, ev.Slug, ev.BannerKey,
            ev.StartsAt, ev.VenueName, ev.City, orgName));
    }

    public async Task<ServiceResult<string>> RsvpAsync(string token, string response, CancellationToken ct = default)
    {
        var inv = await db.EventInvitations.FirstOrDefaultAsync(i => i.InviteToken == token, ct);
        if (inv is null || inv.Status == InvitationStatus.Revoked) return ServiceResult<string>.Fail("not_found");
        if (!Enum.TryParse<InvitationRsvpStatus>(response, true, out var rsvp) || rsvp == InvitationRsvpStatus.None)
            return ServiceResult<string>.Fail("invalid_response");
        inv.RsvpStatus = rsvp;
        inv.RespondedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        var registrationUrl = rsvp == InvitationRsvpStatus.Accepted ? $"/events/{inv.EventId}?inv={token}" : "";
        return ServiceResult<string>.Success(registrationUrl);
    }

    public async Task LinkOrderAsync(string inviteToken, Guid orderId, CancellationToken ct = default)
    {
        var inv = await db.EventInvitations.FirstOrDefaultAsync(i => i.InviteToken == inviteToken, ct);
        if (inv is null) return;
        inv.OrderId = orderId;
        if (inv.RsvpStatus == InvitationRsvpStatus.None || inv.RsvpStatus == InvitationRsvpStatus.Declined)
            inv.RsvpStatus = InvitationRsvpStatus.Accepted;
        await db.SaveChangesAsync(ct);
    }

    private async Task SendOneAsync(EventInvitation inv, Event? ev, CancellationToken ct)
    {
        if (ev is null) return;
        var link = $"https://kurx.in/i/{inv.InviteToken}";
        var dateStr = ev.StartsAt.ToString("d MMM yyyy");
        var waText = string.Format(loc["invitation_wa_message"].Value, inv.Name, ev.Title, dateStr, link);
        var subject = string.Format(loc["invitation_subject"].Value, ev.Title);
        var greeting = string.Format(loc["invitation_greeting"].Value, inv.Name);
        var bodyLine = string.Format(loc["invitation_body_line1"].Value, ev.Title, dateStr);
        var cta = loc["invitation_body_cta"].Value;
        var html = $"<p>{greeting}</p><p>{bodyLine}</p><p><a href=\"{link}\">{cta}</a></p>";

        try
        {
            if ((inv.Channel == InvitationChannel.Email || inv.Channel == InvitationChannel.Both) && inv.Email is not null)
                await emailSender.SendAsync(inv.Email, subject, html, null, ct);

            if ((inv.Channel == InvitationChannel.WhatsApp || inv.Channel == InvitationChannel.Both) && inv.Phone is not null)
                await waLog.SendAndLogAsync(inv.Phone, waText, WhatsAppMessageKind.Generic, "invitation", inv.Id, ct: ct);

            inv.SendStatus = InvitationSendStatus.Sent;
            inv.SentAt = DateTime.UtcNow;
            inv.SendCount++;
        }
        catch
        {
            inv.SendStatus = InvitationSendStatus.Failed;
        }
        await db.SaveChangesAsync(ct);
    }

    // ── D-266 M6 · Method A: in-app accept / decline ─────────────────────────────────────────

    public async Task<IReadOnlyList<MyInvitationView>> ListMineAsync(Guid userId, bool pendingOnly, int page, int pageSize,
        CancellationToken ct = default)
    {
        var q = db.EventInvitations.AsNoTracking()
            .Where(i => i.InvitedUserId == userId && i.Status == InvitationStatus.Active);
        if (pendingOnly) q = q.Where(i => i.RsvpStatus == InvitationRsvpStatus.None);

        // DB-6: same bulk-import collision as the list above.
        return await q.OrderByDescending(i => i.CreatedAt).ThenByDescending(i => i.Id)
            .Skip((Math.Max(page, 1) - 1) * pageSize).Take(pageSize)
            .Join(db.Events.AsNoTracking(), i => i.EventId, e => e.Id, (i, e) => new { i, e })
            .Join(db.Users.AsNoTracking(), x => x.i.InvitedBy, u => u.Id, (x, u) => new MyInvitationView(
                x.i.Id, x.e.Id, x.e.Title, x.e.Slug, x.e.BannerKey,
                x.e.StartsAt, x.e.VenueName, x.e.City,
                u.Name, x.i.RsvpStatus.ToString(), x.i.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<ServiceResult<MyInvitationView>> RespondAsync(Guid userId, Guid invitationId, bool accept,
        CancellationToken ct = default)
    {
        var inv = await db.EventInvitations.FirstOrDefaultAsync(i => i.Id == invitationId, ct);
        // Not addressed to the caller ⇒ 404, never 403: confirming an invitation exists to someone who was
        // not invited discloses the guest list one probe at a time (D-018).
        if (inv is null || inv.InvitedUserId != userId || inv.Status == InvitationStatus.Revoked)
            return ServiceResult<MyInvitationView>.Fail("not_invited");
        if (inv.RsvpStatus != InvitationRsvpStatus.None)
            return ServiceResult<MyInvitationView>.Fail("invitation_already_responded");

        inv.RsvpStatus = accept ? InvitationRsvpStatus.Accepted : InvitationRsvpStatus.Declined;
        inv.RespondedAt = DateTime.UtcNow;

        // The organiser hears back. Without this an invite-only event's host has no way to know whether the
        // guest list is settled except by polling the roster.
        var ev = await db.Events.AsNoTracking().Where(e => e.Id == inv.EventId)
            .Select(e => new { e.Title }).FirstOrDefaultAsync(ct);
        var responder = await db.Users.AsNoTracking().Where(u => u.Id == userId)
            .Select(u => u.Name).FirstOrDefaultAsync(ct) ?? "A guest";
        db.Notifications.Add(new Notification
        {
            UserId = inv.InvitedBy,
            Kind = accept ? "invitation_accepted" : "invitation_declined",
            Title = accept ? "Invitation accepted" : "Invitation declined",
            Body = $"{responder} {(accept ? "accepted" : "declined")} your invitation to {ev?.Title ?? "your event"}.",
            DataJson = $"{{\"event_id\":\"{inv.EventId}\",\"invitation_id\":\"{inv.Id}\"}}",
        });

        await db.SaveChangesAsync(ct);

        var mine = await ListMineAsync(userId, pendingOnly: false, 1, 200, ct);
        var row = mine.FirstOrDefault(m => m.Id == inv.Id);
        return row is null
            ? ServiceResult<MyInvitationView>.Fail("not_found")
            : ServiceResult<MyInvitationView>.Success(row);
    }

    // ── D-266 M6 · Method B: the shareable invite link ───────────────────────────────────────

    public async Task<ServiceResult<InviteLinkView>> CreateLinkAsync(Guid userId, Guid eventId, bool isAdmin,
        CreateInviteLinkInput input, CancellationToken ct = default)
    {
        if (!await IsOrgManagerAsync(userId, eventId, ct) && !isAdmin)
            return ServiceResult<InviteLinkView>.Fail("forbidden");
        if (input.MaxSeats is <= 0) return ServiceResult<InviteLinkView>.Fail("invalid_max_seats");
        if (input.ExpiresAt is { } exp && exp <= DateTime.UtcNow)
            return ServiceResult<InviteLinkView>.Fail("invalid_expiry");

        var link = new EventInviteLink
        {
            EventId = eventId,
            CreatedBy = userId,
            Token = GenerateLinkToken(),
            MaxSeats = input.MaxSeats,
            SingleUse = input.SingleUse,
            ExpiresAt = input.ExpiresAt,
            // Hashed, never stored in the clear: if the row leaks, possession of the link must not also
            // hand over the factor that existed to protect it.
            PasscodeHash = string.IsNullOrWhiteSpace(input.Passcode) ? null : hasher.Hash(input.Passcode!),
        };
        db.EventInviteLinks.Add(link);
        await db.SaveChangesAsync(ct);
        return ServiceResult<InviteLinkView>.Success(ToLinkView(link));
    }

    public async Task<ServiceResult<IReadOnlyList<InviteLinkView>>> ListLinksAsync(Guid userId, Guid eventId, bool isAdmin,
        CancellationToken ct = default)
    {
        if (!await IsOrgManagerAsync(userId, eventId, ct) && !isAdmin)
            return ServiceResult<IReadOnlyList<InviteLinkView>>.Fail("forbidden");

        var links = await db.EventInviteLinks.AsNoTracking()
            .Where(l => l.EventId == eventId).OrderByDescending(l => l.CreatedAt).ToListAsync(ct);
        return ServiceResult<IReadOnlyList<InviteLinkView>>.Success(links.Select(ToLinkView).ToList());
    }

    public async Task<ServiceResult<bool>> RevokeLinkAsync(Guid userId, Guid linkId, bool isAdmin, CancellationToken ct = default)
    {
        var link = await db.EventInviteLinks.FirstOrDefaultAsync(l => l.Id == linkId, ct);
        if (link is null) return ServiceResult<bool>.Fail("not_found");
        if (!await IsOrgManagerAsync(userId, link.EventId, ct) && !isAdmin)
            return ServiceResult<bool>.Fail("forbidden");

        // Revocation is one-way and does NOT retract seats already claimed: someone who redeemed and
        // registered keeps their place. Killing the link stops new arrivals, it does not undo history.
        link.Status = InvitationStatus.Revoked;
        await db.SaveChangesAsync(ct);
        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<PublicInviteLinkView>> GetPublicLinkAsync(string token, CancellationToken ct = default)
    {
        var link = await db.EventInviteLinks.AsNoTracking().FirstOrDefaultAsync(l => l.Token == token, ct);
        if (link is null) return ServiceResult<PublicInviteLinkView>.Fail("not_found");

        var ev = await db.Events.AsNoTracking().Where(e => e.Id == link.EventId)
            .Select(e => new { e.Title, e.Slug, e.StartsAt, e.VenueName, e.City }).FirstOrDefaultAsync(ct);
        if (ev is null) return ServiceResult<PublicInviteLinkView>.Fail("not_found");

        var reason = UnusableReason(link, DateTime.UtcNow);
        return ServiceResult<PublicInviteLinkView>.Success(new PublicInviteLinkView(
            ev.Title, ev.Slug, ev.StartsAt, ev.VenueName, ev.City,
            RequiresPasscode: link.PasscodeHash is not null,
            IsUsable: reason is null,
            Reason: reason,
            SeatsRemaining: link.MaxSeats is { } cap ? Math.Max(cap - link.UsedCount, 0) : null));
    }

    public async Task<ServiceResult<InviteLinkView>> RedeemLinkAsync(Guid userId, string token, string? passcode,
        CancellationToken ct = default)
    {
        var link = await db.EventInviteLinks.AsNoTracking().FirstOrDefaultAsync(l => l.Token == token, ct);
        if (link is null) return ServiceResult<InviteLinkView>.Fail("not_found");

        // A passcode is verified SERVER-SIDE (D9 rule 5). Checked before the seat claim so a wrong passcode
        // never consumes one.
        if (link.PasscodeHash is not null)
        {
            if (string.IsNullOrWhiteSpace(passcode))
                return ServiceResult<InviteLinkView>.Fail("invite_link_passcode_required");
            if (!hasher.Verify(passcode!, link.PasscodeHash).Ok)
                return ServiceResult<InviteLinkView>.Fail("invite_link_passcode_invalid");
        }

        // Idempotent per user (D9 rule 7): a refresh or a double-tap must not consume a second seat, or the
        // organiser's seat count measures clicks rather than people.
        if (await db.EventInviteLinkRedemptions.AsNoTracking()
                .AnyAsync(r => r.InviteLinkId == link.Id && r.UserId == userId, ct))
            return ServiceResult<InviteLinkView>.Success(ToLinkView(
                await db.EventInviteLinks.AsNoTracking().FirstAsync(l => l.Id == link.Id, ct)));

        var now = DateTime.UtcNow;

        // ── The claim (D-240/D-261) ──────────────────────────────────────────
        // Conditional UPDATE, never read-modify-write. Every refusal condition rides in the WHERE, so the
        // DATABASE decides who gets the last seat: N concurrent redeemers issue N updates and exactly
        // (MaxSeats - UsedCount) of them report a row changed. Reading the count and then writing count+1
        // would let every concurrent caller read the same value and all succeed — the coupon bug, again.
        var claimed = await db.EventInviteLinks
            .Where(l => l.Id == link.Id
                        && l.Status == InvitationStatus.Active
                        && (l.ExpiresAt == null || l.ExpiresAt > now)
                        && (l.MaxSeats == null || l.UsedCount < l.MaxSeats)
                        && (!l.SingleUse || l.UsedCount == 0))
            .ExecuteUpdateAsync(s => s.SetProperty(l => l.UsedCount, l => l.UsedCount + 1), ct);

        if (claimed == 0)
        {
            // Re-read to say WHICH refusal it was. The claim can only report "no row changed"; the caller
            // needs to know whether to ask for a different link, a passcode, or nothing at all.
            var fresh = await db.EventInviteLinks.AsNoTracking().FirstOrDefaultAsync(l => l.Id == link.Id, ct);
            return ServiceResult<InviteLinkView>.Fail(
                fresh is null ? "not_found" : UnusableReason(fresh, DateTime.UtcNow) ?? "invite_link_exhausted");
        }

        db.EventInviteLinkRedemptions.Add(new EventInviteLinkRedemption { InviteLinkId = link.Id, UserId = userId });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // The unique (link, user) index caught a redemption that raced this one. The seat this call
            // claimed belongs to a duplicate, so hand it back rather than leaving the count overstated.
            await db.EventInviteLinks.Where(l => l.Id == link.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(l => l.UsedCount, l => l.UsedCount - 1), ct);
        }

        return ServiceResult<InviteLinkView>.Success(ToLinkView(
            await db.EventInviteLinks.AsNoTracking().FirstAsync(l => l.Id == link.Id, ct)));
    }

    public async Task<bool> HasAccessAsync(Guid userId, Guid eventId, CancellationToken ct = default)
    {
        // Either method lets a person in, and this is the ONE place that says so. If the eligibility engine
        // queried the invitation tables itself there would be two definitions of "invited" and they would
        // drift the first time a method changed.
        if (await db.EventInvitations.AsNoTracking().AnyAsync(i =>
                i.EventId == eventId && i.InvitedUserId == userId
                && i.Status == InvitationStatus.Active
                && i.RsvpStatus == InvitationRsvpStatus.Accepted, ct))
            return true;

        return await db.EventInviteLinkRedemptions.AsNoTracking()
            .Join(db.EventInviteLinks.AsNoTracking(), r => r.InviteLinkId, l => l.Id, (r, l) => new { r, l })
            .AnyAsync(x => x.r.UserId == userId && x.l.EventId == eventId, ct);
    }

    /// <summary>Why a link cannot be redeemed, or null when it can. One function, so the pre-flight and the
    /// post-claim refusal can never disagree about what is wrong with a link.</summary>
    private static string? UnusableReason(EventInviteLink l, DateTime now) =>
        l.Status == InvitationStatus.Revoked ? "invite_link_revoked"
        : l.ExpiresAt is { } exp && exp <= now ? "invite_link_expired"
        : l.SingleUse && l.UsedCount > 0 ? "invite_link_exhausted"
        : l.MaxSeats is { } cap && l.UsedCount >= cap ? "invite_link_exhausted"
        : null;

    /// <summary>32 chars, not the per-invitee token's 12. A shareable link is a bearer capability with a
    /// far larger exposure surface — it lives in group chats and inboxes — so guessing must be hopeless.</summary>
    private static string GenerateLinkToken()
    {
        const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var bytes = RandomNumberGenerator.GetBytes(32);
        return new string(bytes.Select(b => chars[b % chars.Length]).ToArray());
    }

    private static InviteLinkView ToLinkView(EventInviteLink l) => new(
        l.Id, l.EventId, l.Token, l.MaxSeats, l.UsedCount, l.SingleUse, l.ExpiresAt,
        RequiresPasscode: l.PasscodeHash is not null, l.Status.ToString(), l.CreatedAt);

    /// <summary>D-269: delegates to the single event authority. **No admin bypass** — preserved exactly,
    /// because these endpoints never passed one. Group captains keep their separate, orthogonal grant.</summary>
    private async Task<bool> IsOrgManagerAsync(Guid userId, Guid eventId, CancellationToken ct)
        => (await authority.ResolveAsync(userId, eventId, isAdmin: false, ct)).Can(EventPermission.ManageContent);

    private async Task<bool> IsGroupCaptainAsync(Guid userId, Guid groupId, CancellationToken ct)
        => await db.Groups.AnyAsync(g => g.Id == groupId && g.LeaderUserId == userId, ct);

    private static string GenerateToken()
    {
        const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var bytes = RandomNumberGenerator.GetBytes(12);
        return new string(bytes.Select(b => chars[b % chars.Length]).ToArray());
    }

    private static InvitationView ToView(EventInvitation i) => new(
        i.Id, i.EventId, i.Name, i.Email, i.Phone,
        i.Channel.ToString(), i.InviteToken, i.SendStatus.ToString(),
        i.RsvpStatus.ToString(), i.Status.ToString(),
        i.SendCount, i.SentAt, i.RespondedAt, i.CreatedAt, i.GroupId);
}
