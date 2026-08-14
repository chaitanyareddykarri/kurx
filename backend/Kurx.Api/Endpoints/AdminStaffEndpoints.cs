using System.Security.Claims;
using Kurx.Application.Abstractions;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Auth;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Api.Endpoints;

using Kurx.Api;
using Kurx.Api.ExceptionHandling;

public record GrantStaffBody(string Phone, string Role, DateTime? ExpiresAt);

/// <summary>Staff &amp; role management (admin console M12 / D-056). SuperAdmin-only: list platform staff,
/// grant a platform role to an existing user (by phone), revoke one. Platform authority is the live
/// <c>platform_roles</c> table (M2, D-040) — a grant/revoke takes effect on the target's next request.
/// Making someone staff == granting a platform role; there is no separate "staff account". The target
/// must already be a registered Kurx user (they log in once via OTP first); we don't provision here.</summary>
public static class AdminStaffEndpoints
{
    public static void MapAdminStaffEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/v1/admin/staff").WithTags("admin").RequireAuthorization("SuperAdmin");

        // List everyone holding at least one active platform role. Optional q (name/phone/username) + role filter.
        g.MapGet("", async (string? q, string? role, KurxDbContext db, CancellationToken ct) =>
        {
            var now = DateTime.UtcNow;
            var rows = await db.PlatformRoles.AsNoTracking()
                .Where(r => r.ExpiresAt == null || r.ExpiresAt > now)
                .Join(db.Users.AsNoTracking(), r => r.UserId, u => u.Id,
                    (r, u) => new { u.Id, u.Name, u.Phone, u.Username, r.Role, r.GrantedAt })
                .ToListAsync(ct);

            var staff = rows
                .GroupBy(x => new { x.Id, x.Name, x.Phone, x.Username })
                .Select(gr => new
                {
                    user_id = gr.Key.Id,
                    name = gr.Key.Name,
                    phone = gr.Key.Phone,
                    username = gr.Key.Username,
                    roles = gr.Select(x => x.Role.ToString()).OrderBy(s => s).ToArray(),
                    granted_at = gr.Max(x => x.GrantedAt),
                })
                .ToList();

            if (!string.IsNullOrWhiteSpace(role))
                staff = staff.Where(s => s.roles.Contains(role, StringComparer.OrdinalIgnoreCase)).ToList();
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();
                staff = staff.Where(s =>
                    (s.name?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
                    || s.phone.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || (s.username?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)).ToList();
            }

            return Results.Ok(staff.OrderByDescending(s => s.granted_at));
        });

        // Grant a platform role to an existing user, found by phone (same normalization as login).
        g.MapPost("/grant", async (GrantStaffBody body, ClaimsPrincipal p, KurxDbContext db,
            IPlatformRoleService svc, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(body.Phone))
                return ProblemResults.Problem("phone_required", StatusCodes.Status400BadRequest);
            if (!TryParseRole(body.Role, out var role))
                return ProblemResults.Problem("invalid_role", StatusCodes.Status400BadRequest);

            var phone = AuthService.NormalizePhone(body.Phone);
            var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Phone == phone, ct);
            if (user is null)
                return ProblemResults.Problem("user_not_found", StatusCodes.Status404NotFound);

            await svc.GrantAsync(user.Id, role, UserId(p), body.ExpiresAt, ct);
            return Results.Ok(await StaffJsonAsync(db, user.Id, ct));
        });

        // Revoke one role. A platform can't be left with zero SuperAdmins (self-lockout / lockout guard).
        g.MapDelete("/{userId:guid}/roles/{role}", async (Guid userId, string role, KurxDbContext db,
            IPlatformRoleService svc, CancellationToken ct) =>
        {
            if (!TryParseRole(role, out var parsed))
                return ProblemResults.Problem("invalid_role", StatusCodes.Status400BadRequest);

            if (parsed == PlatformRole.SuperAdmin)
            {
                var now = DateTime.UtcNow;
                var superAdmins = await db.PlatformRoles.AsNoTracking()
                    .CountAsync(r => r.Role == PlatformRole.SuperAdmin && (r.ExpiresAt == null || r.ExpiresAt > now), ct);
                if (superAdmins <= 1)
                    return ProblemResults.Problem("cannot_revoke_last_superadmin", StatusCodes.Status409Conflict);
            }

            await svc.RevokeAsync(userId, parsed, ct);
            return Results.Ok(await StaffJsonAsync(db, userId, ct) ?? (object)new { user_id = userId, roles = Array.Empty<string>() });
        });
    }

    private static bool TryParseRole(string s, out PlatformRole role)
        => Enum.TryParse(s, ignoreCase: true, out role) && Enum.IsDefined(role);

    private static async Task<object?> StaffJsonAsync(KurxDbContext db, Guid userId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return null;
        var roles = await db.PlatformRoles.AsNoTracking()
            .Where(r => r.UserId == userId && (r.ExpiresAt == null || r.ExpiresAt > now))
            .Select(r => r.Role).ToListAsync(ct);
        return new
        {
            user_id = user.Id,
            name = user.Name,
            phone = user.Phone,
            username = user.Username,
            roles = roles.Select(r => r.ToString()).OrderBy(s => s).ToArray(),
        };
    }

    private static Guid UserId(ClaimsPrincipal principal)
        => Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id : throw new ApiException(StatusCodes.Status401Unauthorized, "invalid_token");
}
