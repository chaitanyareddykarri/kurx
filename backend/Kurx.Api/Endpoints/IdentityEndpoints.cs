using System.Security.Claims;
using Kurx.Application.Abstractions;

namespace Kurx.Api.Endpoints;

using Kurx.Api;
using Kurx.Api.ExceptionHandling;

public record GovtIdBody(string Kind, string IdNumber, string Name);
public record PanIdentityBody(string Pan, string Name);
public record BankIdentityBody(string AccountNumber, string Ifsc, string HolderName);

/// <summary>Person identity verification (M3, D-042): the caller's own KYC. Only masked last-4 values
/// are ever returned. Submissions run through IKycProvider (mock in dev) and are attempt-capped.</summary>
public static class IdentityEndpoints
{
    public static void MapIdentityEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/v1/me/identity").WithTags("identity").RequireAuthorization();

        // Returned directly: ToJson was a faithful 1:1 snake_case projection of IdentityStatusView,
        // and the response converter derives exactly the same keys from the property names.
        g.MapGet("/", async (ClaimsPrincipal p, IIdentityVerificationService svc, CancellationToken ct)
            => Results.Ok(await svc.GetStatusAsync(UserId(p), ct)))
            .Produces<IdentityStatusView>();

        g.MapPost("/government-id", async (GovtIdBody body, ClaimsPrincipal p, IIdentityVerificationService svc, CancellationToken ct) =>
        {
            var r = await svc.SubmitGovernmentIdAsync(UserId(p), body.Kind, body.IdNumber, body.Name, ct);
            return r.Ok ? Results.Ok(r.Value!) : Fail(r.Error);
        }).Produces<IdentityStatusView>();

        g.MapPost("/pan", async (PanIdentityBody body, ClaimsPrincipal p, IIdentityVerificationService svc, CancellationToken ct) =>
        {
            var r = await svc.SubmitPanAsync(UserId(p), body.Pan, body.Name, ct);
            return r.Ok ? Results.Ok(r.Value!) : Fail(r.Error);
        }).Produces<IdentityStatusView>();

        g.MapPost("/bank", async (BankIdentityBody body, ClaimsPrincipal p, IIdentityVerificationService svc, CancellationToken ct) =>
        {
            var r = await svc.SubmitBankAsync(UserId(p), body.AccountNumber, body.Ifsc, body.HolderName, ct);
            return r.Ok ? Results.Ok(r.Value!) : Fail(r.Error);
        }).Produces<IdentityStatusView>();

        // Verification history. The trail has been written to verification_reviews on every
        // submission since M3 and had no read path, so the one person entitled to see the decisions
        // made about their own identity could not. Caller-scoped: the service resolves the subject
        // from the token, never from a route parameter.
        g.MapGet("/history", async (int? limit, ClaimsPrincipal p, IIdentityVerificationService svc,
            CancellationToken ct) =>
        {
            var rows = await svc.GetHistoryAsync(UserId(p), limit ?? 50, ct);
            return Results.Ok(rows);
        }).Produces<IReadOnlyList<IdentityHistoryEntry>>();
    }

    private static Guid UserId(ClaimsPrincipal principal)
        => Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id : throw new ApiException(StatusCodes.Status401Unauthorized, "invalid_token");

    private static IResult Fail(string? error) => error switch
    {
        "too_many_attempts" => ProblemResults.Problem(error, StatusCodes.Status429TooManyRequests),
        _ => ProblemResults.Problem(error, StatusCodes.Status400BadRequest),
    };

}
