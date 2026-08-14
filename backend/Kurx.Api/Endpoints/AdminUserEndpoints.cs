using System.Security.Claims;
using Kurx.Application.Abstractions;

namespace Kurx.Api.Endpoints;

using Kurx.Api;
using Kurx.Api.ExceptionHandling;

public record ModerateUserBody(string? Reason);

/// <summary>Platform user administration (D-060). Search users, suspend / ban / unban accounts. Gated by
/// the Moderation policy (SuperAdmin / VerificationReviewer / Support). Enforcement of the block lives in
/// AuthService; this only sets the flags. Returns attendee PII (phone/email) — Moderation staff only.</summary>
public static class AdminUserEndpoints
{
    public static void MapAdminUserEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/v1/admin/users").WithTags("admin").RequireAuthorization("Moderation");

        g.MapGet("", async (string? q, int? limit, IUserAdminService svc, CancellationToken ct) =>
            Results.Ok((await svc.ListAsync(q, limit ?? 50, ct)))).Produces<IReadOnlyList<AdminUserView>>();

        g.MapPost("/{userId:guid}/{action}", async (Guid userId, string action, ModerateUserBody? body,
            ClaimsPrincipal p, IUserAdminService svc, CancellationToken ct) =>
        {
            var r = await svc.ModerateAsync(UserId(p), userId, action, body?.Reason, ct);
            return r.Ok ? Results.Ok(r.Value!) : Fail(r.Error);
        }).Produces<AdminUserView>();
    }

    private static IResult Fail(string? error) => error switch
    {
        "not_found" => ProblemResults.Problem(error, StatusCodes.Status404NotFound),
        "cannot_moderate_self" or "cannot_moderate_superadmin" => ProblemResults.Problem(error, StatusCodes.Status409Conflict),
        _ => ProblemResults.Problem(error, StatusCodes.Status400BadRequest),
    };


    private static Guid UserId(ClaimsPrincipal principal)
        => Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id : throw new ApiException(StatusCodes.Status401Unauthorized, "invalid_token");
}
