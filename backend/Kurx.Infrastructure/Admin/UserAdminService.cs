using System.Text.Json;
using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Infrastructure.Admin;

/// <summary>User administration + account moderation (D-060). The suspend/ban *enforcement* lives in
/// AuthService (login + refresh blocked); this service sets the flags, kills live sessions, and audits.</summary>
public class UserAdminService(KurxDbContext db, IPlatformRoleService roles) : IUserAdminService
{
    public async Task<IReadOnlyList<AdminUserView>> ListAsync(string? q, int limit, CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 100);
        var query = db.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            // A phone search has to survive the way people actually type numbers. Phones are stored as bare
            // digits, so "+65 9123 4567" — the form staff copy out of a ticket or an email — shares no
            // substring with the stored value and previously matched nothing at all. Reducing the term to
            // digits before comparing makes every written form of the same number find the same account.
            // The canonical column is searched too, so a row is findable however far the D-089 backfill got.
            var digits = new string(term.Where(char.IsAsciiDigit).ToArray());
            query = digits.Length > 0
                ? query.Where(u => EF.Functions.ILike(u.Name, $"%{term}%")
                    || (u.Username != null && EF.Functions.ILike(u.Username, $"%{term}%"))
                    || EF.Functions.ILike(u.Phone, $"%{digits}%")
                    || (u.PhoneE164 != null && EF.Functions.ILike(u.PhoneE164, $"%{digits}%")))
                : query.Where(u => EF.Functions.ILike(u.Name, $"%{term}%")
                    || (u.Username != null && EF.Functions.ILike(u.Username, $"%{term}%")));
        }
        else
        {
            // No search term → the currently-moderated accounts, not the whole table (privacy + perf).
            query = query.Where(u => u.SuspendedAt != null || u.BannedAt != null);
        }

        return await query.OrderByDescending(u => u.CreatedAt).Take(limit)
            // Canonical E.164 where the backfill has reached the row, so staff see "+6591234567" rather
            // than a bare digit string that hides which country an account belongs to.
            .Select(u => new AdminUserView(u.Id, u.PhoneE164 ?? u.Phone, u.Name, u.Username, u.Email,
                u.SuspendedAt != null, u.BannedAt != null, u.ModerationReason, u.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<ServiceResult<AdminUserView>> ModerateAsync(Guid actorId, Guid userId, string action,
        string? reason, CancellationToken ct = default)
    {
        action = (action ?? "").Trim().ToLowerInvariant();
        if (action is not ("suspend" or "ban" or "unban"))
            return ServiceResult<AdminUserView>.Fail("invalid_action");
        if (userId == actorId) return ServiceResult<AdminUserView>.Fail("cannot_moderate_self");

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return ServiceResult<AdminUserView>.Fail("not_found");

        // Never let staff suspend/ban a platform SuperAdmin — guards trusted accounts from abuse/lockout.
        if ((await roles.GetRolesAsync(userId, ct)).Contains(PlatformRole.SuperAdmin))
            return ServiceResult<AdminUserView>.Fail("cannot_moderate_superadmin");

        var now = DateTime.UtcNow;
        switch (action)
        {
            case "suspend": user.SuspendedAt = now; user.ModerationReason = reason; break;
            case "ban": user.BannedAt = now; user.ModerationReason = reason; break;
            case "unban": user.SuspendedAt = null; user.BannedAt = null; user.ModerationReason = null; break;
        }

        // Kill live sessions now so a suspend/ban bites within the access-token TTL, not only at login.
        if (action is "suspend" or "ban")
            await db.RefreshTokens.Where(t => t.UserId == userId && t.RevokedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);

        db.AuditLogs.Add(new AuditLog
        {
            ActorType = "admin", ActorId = actorId,
            Action = $"user.{action}", Entity = "users", EntityId = userId,
            DetailsJson = reason is null ? null : JsonSerializer.Serialize(new { reason }),
        });
        await db.SaveChangesAsync(ct);

        // Canonical column first, matching ListAsync — otherwise the same account renders a bare
        // digit string here and a "+65…" there depending on which call produced the row.
        return ServiceResult<AdminUserView>.Success(new AdminUserView(user.Id, user.PhoneE164 ?? user.Phone, user.Name,
            user.Username, user.Email, user.SuspendedAt != null, user.BannedAt != null, user.ModerationReason, user.CreatedAt));
    }
}
