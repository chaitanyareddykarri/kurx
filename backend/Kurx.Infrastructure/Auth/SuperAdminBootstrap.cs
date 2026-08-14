using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Kurx.Infrastructure.Auth;

/// <summary>
/// Grants the FIRST platform SuperAdmin from configuration (D-274), so a fresh deployment has a way
/// into the admin console without any authentication bypass. Deliberately narrow:
/// <list type="bullet">
/// <item>runs only while <c>platform_roles</c> holds zero live SuperAdmin rows — once one exists,
/// every further grant goes through <c>POST /v1/admin/staff/grant</c>;</item>
/// <item>never creates a user: the person signs in through the real OTP flow first, and this only
/// attaches a role to that already-registered account;</item>
/// <item>writes the same audit row the console path writes.</item>
/// </list>
/// Unset config is a no-op, which is the correct steady state after the first grant.
/// </summary>
public static class SuperAdminBootstrap
{
    public const string PhoneKey = "SUPERADMIN_BOOTSTRAP_PHONE";

    public static async Task RunAsync(KurxDbContext db, IPlatformRoleService roles, IConfiguration config,
        ILogger logger, CancellationToken ct = default)
    {
        if (config[PhoneKey] is not { Length: > 0 } configured) return;

        var now = DateTime.UtcNow;
        if (await db.PlatformRoles.AnyAsync(r => r.Role == PlatformRole.SuperAdmin
                && (r.ExpiresAt == null || r.ExpiresAt > now), ct))
        {
            logger.LogInformation("{Key} is set but a SuperAdmin already exists; bootstrap skipped.", PhoneKey);
            return;
        }

        // Same normalisation the login path applies, so the configured number matches how the account
        // was actually stored when it registered.
        var phone = AuthService.NormalizePhone(configured);
        var userId = await db.Users.Where(u => u.Phone == phone).Select(u => (Guid?)u.Id).FirstOrDefaultAsync(ct);
        if (userId is null)
        {
            // Never create the account — that would be a seeded identity. Sign in once through the
            // normal flow, then restart the API. Phone is PII, so it is not logged.
            logger.LogWarning("{Key} names a phone with no registered user; no SuperAdmin granted. "
                + "Sign in with that number first, then restart.", PhoneKey);
            return;
        }

        db.AuditLogs.Add(new AuditLog
        {
            ActorType = "system",
            ActorId = null,
            Action = "platform_role.bootstrap_granted",
            Entity = "users",
            EntityId = userId,
            DetailsJson = $"{{\"role\":\"SuperAdmin\",\"source\":\"{PhoneKey}\"}}",
        });
        await roles.GrantAsync(userId.Value, PlatformRole.SuperAdmin, grantedBy: null, ct: ct);

        logger.LogWarning("Bootstrapped SuperAdmin for user {UserId} from {Key}; unset it now that a "
            + "SuperAdmin exists.", userId, PhoneKey);
    }
}
