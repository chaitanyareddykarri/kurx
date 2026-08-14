using System.Security.Cryptography;
using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Auth;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Infrastructure.Orgs;

/// <summary>Org invitations (D-064). Accept creates the membership; possession of the token is not enough —
/// the accepting account's phone (or invited-user id) must match the invite, so a leaked token can't be
/// redeemed by someone else.</summary>
public class OrgInvitationService(KurxDbContext db) : IOrgInvitationService
{
    private static readonly TimeSpan Ttl = TimeSpan.FromDays(7);

    public async Task<ServiceResult<OrgInvitationView>> InviteAsync(Guid actorId, Guid orgId, string phone, OrgRole role, CancellationToken ct = default)
    {
        var actorRole = await RoleAsync(actorId, orgId, ct);
        // Owner invites any role; Manager invites Staff only (D-015).
        if (actorRole is not (OrgRole.Owner or OrgRole.Manager)) return ServiceResult<OrgInvitationView>.Fail("forbidden");
        if (actorRole == OrgRole.Manager && role != OrgRole.Staff) return ServiceResult<OrgInvitationView>.Fail("forbidden");

        phone = AuthService.NormalizePhone(phone);
        var invitedUserId = await db.Users.AsNoTracking().Where(u => u.Phone == phone).Select(u => (Guid?)u.Id).FirstOrDefaultAsync(ct);
        if (invitedUserId is Guid uid && await db.Memberships.AnyAsync(m => m.OrgId == orgId && m.UserId == uid, ct))
            return ServiceResult<OrgInvitationView>.Fail("already_member");
        if (await db.OrgInvitations.AnyAsync(i => i.OrgId == orgId && i.InvitedPhone == phone && i.Status == OrgInvitationStatus.Pending, ct))
            return ServiceResult<OrgInvitationView>.Fail("already_invited");

        var inv = new OrgInvitation
        {
            OrgId = orgId, InvitedBy = actorId, InvitedUserId = invitedUserId, InvitedPhone = phone, Role = role,
            Token = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant(),
            ExpiresAt = DateTime.UtcNow.Add(Ttl), Status = OrgInvitationStatus.Pending,
        };
        db.OrgInvitations.Add(inv);
        await db.SaveChangesAsync(ct);
        return ServiceResult<OrgInvitationView>.Success(ToView(inv, null));
    }

    public async Task<ServiceResult<IReadOnlyList<OrgInvitationView>>> ListForOrgAsync(Guid actorId, Guid orgId, CancellationToken ct = default)
    {
        if (await RoleAsync(actorId, orgId, ct) is not (OrgRole.Owner or OrgRole.Manager))
            return ServiceResult<IReadOnlyList<OrgInvitationView>>.Fail("forbidden");
        var rows = await db.OrgInvitations.AsNoTracking()
            .Where(i => i.OrgId == orgId && i.Status == OrgInvitationStatus.Pending)
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new { i.Id, i.OrgId, i.InvitedPhone, i.Role, i.Status, i.ExpiresAt, i.CreatedAt })
            .ToListAsync(ct);
        IReadOnlyList<OrgInvitationView> views = rows.Select(i => new OrgInvitationView(i.Id, i.OrgId, null,
            i.InvitedPhone, i.Role.ToString(), i.Status.ToString(), i.ExpiresAt, i.CreatedAt)).ToList();
        return ServiceResult<IReadOnlyList<OrgInvitationView>>.Success(views);
    }

    public async Task<ServiceResult<bool>> CancelAsync(Guid actorId, Guid orgId, Guid invitationId, CancellationToken ct = default)
    {
        if (await RoleAsync(actorId, orgId, ct) is not (OrgRole.Owner or OrgRole.Manager))
            return ServiceResult<bool>.Fail("forbidden");
        var inv = await db.OrgInvitations.FirstOrDefaultAsync(i => i.Id == invitationId && i.OrgId == orgId, ct);
        if (inv is null) return ServiceResult<bool>.Fail("not_found");
        if (inv.Status == OrgInvitationStatus.Pending) { inv.Status = OrgInvitationStatus.Cancelled; await db.SaveChangesAsync(ct); }
        return ServiceResult<bool>.Success(true);
    }

    public async Task<IReadOnlyList<OrgInvitationView>> ListMineAsync(Guid userId, CancellationToken ct = default)
    {
        var phone = await db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => u.Phone).FirstOrDefaultAsync(ct);
        var rows = await db.OrgInvitations.AsNoTracking()
            .Where(i => i.Status == OrgInvitationStatus.Pending && (i.InvitedUserId == userId || i.InvitedPhone == phone))
            .Join(db.Organizations.AsNoTracking(), i => i.OrgId, o => o.Id,
                (i, o) => new { i.Id, i.OrgId, OrgName = o.Name, i.InvitedPhone, i.Role, i.Status, i.ExpiresAt, i.CreatedAt })
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);
        return rows.Select(x => new OrgInvitationView(x.Id, x.OrgId, x.OrgName, x.InvitedPhone,
            x.Role.ToString(), x.Status.ToString(), x.ExpiresAt, x.CreatedAt)).ToList();
    }

    public async Task<ServiceResult<OrgInvitationView>> AcceptAsync(Guid userId, string token, CancellationToken ct = default)
    {
        var inv = await db.OrgInvitations.FirstOrDefaultAsync(i => i.Token == token, ct);
        if (inv is null) return ServiceResult<OrgInvitationView>.Fail("not_found");
        if (inv.Status != OrgInvitationStatus.Pending) return ServiceResult<OrgInvitationView>.Fail("not_pending");
        if (inv.ExpiresAt < DateTime.UtcNow)
        {
            inv.Status = OrgInvitationStatus.Expired;
            await db.SaveChangesAsync(ct);
            return ServiceResult<OrgInvitationView>.Fail("expired");
        }

        var user = await db.Users.AsNoTracking().FirstAsync(u => u.Id == userId, ct);
        var matches = inv.InvitedUserId == userId || (inv.InvitedUserId is null && inv.InvitedPhone == user.Phone);
        if (!matches) return ServiceResult<OrgInvitationView>.Fail("not_your_invitation");

        if (!await db.Memberships.AnyAsync(m => m.OrgId == inv.OrgId && m.UserId == userId, ct))
        {
            var membership = new Membership { OrgId = inv.OrgId, UserId = userId, Role = inv.Role };
            db.Memberships.Add(membership);
            db.AuditLogs.Add(new AuditLog
            {
                ActorType = "user", ActorId = userId,
                Action = "membership.add", Entity = "memberships", EntityId = membership.Id,
                DetailsJson = $"{{\"org_id\":\"{inv.OrgId}\",\"via\":\"invitation\",\"role\":\"{inv.Role}\"}}",
            });
            // D-304 case 7 — inside the `if`, so re-accepting an already-honoured invitation stages nothing.
            // A Host-level invited role reaches the organization's published rooms from here; anything else
            // resolves to Member. Staged on this transaction, never called inline afterwards.
            OrgAuthorityOutbox.Stage(db, inv.OrgId, userId);
        }
        inv.Status = OrgInvitationStatus.Accepted;
        inv.AcceptedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return ServiceResult<OrgInvitationView>.Success(ToView(inv, null));
    }

    public async Task<ServiceResult<bool>> DeclineAsync(Guid userId, string token, CancellationToken ct = default)
    {
        var inv = await db.OrgInvitations.FirstOrDefaultAsync(i => i.Token == token, ct);
        if (inv is null) return ServiceResult<bool>.Fail("not_found");
        if (inv.Status != OrgInvitationStatus.Pending) return ServiceResult<bool>.Fail("not_pending");

        var user = await db.Users.AsNoTracking().FirstAsync(u => u.Id == userId, ct);
        if (inv.InvitedUserId != userId && !(inv.InvitedUserId is null && inv.InvitedPhone == user.Phone))
            return ServiceResult<bool>.Fail("not_your_invitation");
        inv.Status = OrgInvitationStatus.Declined;
        await db.SaveChangesAsync(ct);
        return ServiceResult<bool>.Success(true);
    }

    private async Task<OrgRole?> RoleAsync(Guid userId, Guid orgId, CancellationToken ct)
        => await db.Memberships.AsNoTracking()
            .Where(m => m.UserId == userId && m.OrgId == orgId)
            .Select(m => (OrgRole?)m.Role).FirstOrDefaultAsync(ct);

    private static OrgInvitationView ToView(OrgInvitation i, string? orgName) => new(i.Id, i.OrgId, orgName,
        i.InvitedPhone, i.Role.ToString(), i.Status.ToString(), i.ExpiresAt, i.CreatedAt);
}
