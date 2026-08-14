using System.Security.Claims;
using Kurx.Application.Abstractions;

namespace Kurx.Api.Endpoints;

using Kurx.Api;
using Kurx.Api.ExceptionHandling;

/// <summary>Trusted-browser management (Phase 2A, D-127). A trusted browser satisfies <b>factor 2 only</b>
/// — it never bypasses the password (INV-A). The cookie is <i>issued</i> by the login flow (Phase 2B);
/// these authenticated routes are the self-service management surface (part of the Security Center).</summary>
public static class TrustedBrowserEndpoints
{
    /// <summary>The factor-2 cookie the login flow sets and the browser presents: HttpOnly + Secure +
    /// SameSite, an opaque token whose SHA-256 is all the server stores.</summary>
    public const string CookieName = "kurx_tb";

    public static void MapTrustedBrowserEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/v1/auth/trusted-browsers").WithTags("trusted-browsers").RequireAuthorization();

        g.MapGet("", async (HttpContext http, ClaimsPrincipal p, ITrustedBrowserService svc, CancellationToken ct) =>
        {
            var current = http.Request.Cookies.TryGetValue(CookieName, out var c) ? c : null;
            return Results.Ok(await svc.ListAsync(UserId(p), current, ct));
        }).Produces<IReadOnlyList<TrustedBrowserView>>();

        g.MapPost("/{id:guid}/revoke", async (Guid id, ClaimsPrincipal p, ITrustedBrowserService svc, CancellationToken ct) =>
        {
            var r = await svc.RevokeAsync(UserId(p), id, ct);
            return r.Ok
                ? Results.Ok(OperationAck.Success)
                : ProblemResults.Problem(r.Error, r.Error == "not_found" ? StatusCodes.Status404NotFound : StatusCodes.Status400BadRequest);
        }).Produces<OperationAck>();
    }

    private static Guid UserId(ClaimsPrincipal principal)
        => Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : throw new ApiException(StatusCodes.Status401Unauthorized, "invalid_token");
}
