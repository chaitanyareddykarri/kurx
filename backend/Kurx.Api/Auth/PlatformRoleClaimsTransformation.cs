using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Kurx.Application.Abstractions;
using Kurx.Domain.Enums;
using Microsoft.AspNetCore.Authentication;

namespace Kurx.Api.Auth;

/// <summary>
/// Populates platform-role claims LIVE from <c>platform_roles</c> on every authenticated request (M2,
/// D-040). Two responsibilities:
///   1. STRIP any <c>platform_role</c>/<c>kurx_admin</c> claim the presented token carries — platform
///      authority is never trusted from a JWT, only from the database — which defeats a forged claim.
///   2. RE-ADD the user's current platform roles from the DB (one <c>platform_role</c> claim per role,
///      plus a live <c>kurx_admin</c> claim for SuperAdmin so the existing per-endpoint IsAdmin helpers
///      keep working with zero churn).
/// Because roles are re-read each request, a grant/revoke is effective on the very next request.
/// </summary>
public class PlatformRoleClaimsTransformation(IPlatformRoleService platformRoles) : IClaimsTransformation
{
    public const string PlatformRoleClaim = "platform_role";
    public const string AdminClaim = "kurx_admin";          // live-populated for SuperAdmin (IsAdmin helpers)
    private const string ResolvedMarker = "platform_roles_resolved";

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        var identity = principal.Identity;
        if (identity is null || !identity.IsAuthenticated) return principal;

        // IClaimsTransformation can run multiple times per request; do the DB read once.
        if (principal.HasClaim(c => c.Type == ResolvedMarker)) return principal;

        // Rebuild a single clean identity that drops any token-supplied platform claims (anti-forgery).
        var kept = principal.Claims.Where(c => c.Type is not (PlatformRoleClaim or AdminClaim));
        var ci = identity as ClaimsIdentity;
        var rebuilt = new ClaimsIdentity(kept, identity.AuthenticationType,
            ci?.NameClaimType ?? ClaimTypes.Name, ci?.RoleClaimType ?? ClaimTypes.Role);
        rebuilt.AddClaim(new Claim(ResolvedMarker, "1"));

        var sub = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (Guid.TryParse(sub, out var userId))
        {
            var roles = await platformRoles.GetRolesAsync(userId);
            foreach (var role in roles)
                rebuilt.AddClaim(new Claim(PlatformRoleClaim, role.ToString()));
            if (roles.Contains(PlatformRole.SuperAdmin))
                rebuilt.AddClaim(new Claim(AdminClaim, "true"));
        }

        return new ClaimsPrincipal(rebuilt);
    }
}
