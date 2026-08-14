using System.Security.Claims;
using Kurx.Application.Abstractions;

namespace Kurx.Api.Endpoints;

using Kurx.Api;
using Kurx.Api.ExceptionHandling;

/// <summary>Security Center (Phase 2E). One authenticated place that summarizes every authentication factor
/// and channel, shows the user their own recent security activity, and offers "sign out everywhere". The
/// per-factor management endpoints live in their own modules (password, email, trusted-browsers, devices,
/// sessions, passkeys, recovery); this does not duplicate them.</summary>
public static class SecurityCenterEndpoints
{
    public static void MapSecurityCenterEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/v1/auth/security-center").WithTags("security-center").RequireAuthorization();

        g.MapGet("", async (ClaimsPrincipal p, ISecurityCenterService svc, CancellationToken ct) =>
        {
            var s = await svc.GetOverviewAsync(UserId(p), ct);
            // SecurityOverview is a proven 1:1 snake_case projection of all twelve properties.
            return Results.Ok(s);
        }).Produces<SecurityOverview>();

        g.MapGet("/activity", async (int? limit, ClaimsPrincipal p, ISecurityCenterService svc, CancellationToken ct) =>
        {
            var items = await svc.GetActivityAsync(UserId(p), limit ?? 50, ct);
            // SecurityActivityItem is a proven 1:1 projection (4/4).
            return Results.Ok(items);
        }).Produces<IReadOnlyList<SecurityActivityItem>>();

        g.MapPost("/sign-out-all", async (ClaimsPrincipal p, ISecurityCenterService svc, CancellationToken ct) =>
            Results.Ok(new SessionsRevokedResult(await svc.SignOutEverywhereAsync(UserId(p), ct)))).Produces<SessionsRevokedResult>();
    }

    private static Guid UserId(ClaimsPrincipal principal)
        => Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : throw new ApiException(StatusCodes.Status401Unauthorized, "invalid_token");
}
