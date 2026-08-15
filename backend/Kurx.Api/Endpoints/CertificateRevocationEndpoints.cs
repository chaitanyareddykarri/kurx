using System.Security.Claims;
using Kurx.Api.ExceptionHandling;
using Kurx.Application.Abstractions;

namespace Kurx.Api.Endpoints;

/// <summary>
/// Withdrawing a certificate, and correcting one (D-344, Phase 9).
///
/// <para>Revoke and reissue are separate verbs because they are separate public claims. Revoking says
/// "this should not be honoured"; reissuing says "a corrected one exists, here it is". Offering one
/// endpoint with a flag would make it easy to tell someone holding a merely-misspelled certificate that
/// theirs was withdrawn.</para>
///
/// <para>There is no edit endpoint, and there will not be. A certificate is a signed claim someone already
/// holds a copy of; changing its values in place would break the signature that exists to detect exactly
/// that.</para>
/// </summary>
public static class CertificateRevocationEndpoints
{
    public static void MapCertificateRevocationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/v1").RequireAuthorization().WithTags("certificate-revocations");

        group.MapPost("/certificates/{certificateRowId:guid}/revoke", async (
            Guid certificateRowId, RevokeCertificateRequest body, ClaimsPrincipal principal,
            ICertificateRevocationService revocations, CancellationToken ct) =>
            Respond(await revocations.RevokeAsync(
                UserId(principal), certificateRowId, body?.Reason ?? "", IsAdmin(principal), ct)))
        .WithSummary("Withdraw a certificate")
        .Produces<CertificateLineageView>();

        // The case this exists for is "all two hundred have the wrong date", where revoking one at a time
        // is not a real option.
        group.MapPost("/certificate-batches/{batchId:guid}/revoke", async (
            Guid batchId, RevokeCertificateRequest body, ClaimsPrincipal principal,
            ICertificateRevocationService revocations, CancellationToken ct) =>
        {
            var result = await revocations.RevokeBatchAsync(
                UserId(principal), batchId, body?.Reason ?? "", IsAdmin(principal), ct);
            return result.Ok
                ? Results.Ok(result.Value)
                : ProblemResults.Problem(result.Error!, StatusFor(result.Error!));
        })
        .RequireRateLimiting("heavy")
        .WithSummary("Withdraw every live certificate in a run")
        .Produces<CertificateBatchRevocationResult>();

        group.MapPost("/certificates/{certificateRowId:guid}/reissue", async (
            Guid certificateRowId, ReissueCertificateRequest body, ClaimsPrincipal principal,
            ICertificateRevocationService revocations, CancellationToken ct) =>
            Respond(await revocations.ReissueAsync(
                UserId(principal), certificateRowId,
                new CertificateCorrection(body?.RecipientName, body?.Values, body?.Reason ?? ""),
                IsAdmin(principal), ct), StatusCodes.Status201Created))
        .WithSummary("Issue a corrected replacement")
        .Produces<CertificateLineageView>(StatusCodes.Status201Created);

        group.MapGet("/certificates/{certificateRowId:guid}/lineage", async (
            Guid certificateRowId, ClaimsPrincipal principal,
            ICertificateRevocationService revocations, CancellationToken ct) =>
        {
            var result = await revocations.LineageAsync(UserId(principal), certificateRowId, IsAdmin(principal), ct);
            return result.Ok
                ? Results.Ok(result.Value)
                : ProblemResults.Problem(result.Error!, StatusFor(result.Error!));
        })
        .WithSummary("The full chain a certificate belongs to, oldest first")
        .Produces<IReadOnlyList<CertificateLineageView>>();
    }

    private static Guid UserId(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id : throw new ApiException(StatusCodes.Status401Unauthorized, "invalid_token");

    private static bool IsAdmin(ClaimsPrincipal principal) => principal.HasClaim("kurx_admin", "true");

    private static IResult Respond(
        ServiceResult<CertificateLineageView> result, int okStatus = StatusCodes.Status200OK) =>
        result.Ok
            ? okStatus == StatusCodes.Status201Created
                ? Results.Created($"/v1/certificates/{result.Value!.Id}", result.Value)
                : Results.Ok(result.Value)
            : ProblemResults.Problem(result.Error!, StatusFor(result.Error!));

    /// <summary>D-018: a certificate on an event the caller may not see is not-found, never forbidden.
    /// The state refusals are 409 — the request was well formed, the certificate is simply not in a state
    /// where it can happen.</summary>
    private static int StatusFor(string error) => error switch
    {
        "not_found" or "recipient_not_found" => StatusCodes.Status404NotFound,
        "already_revoked" or "certificate_revoked" or "certificate_superseded" => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status400BadRequest,
    };
}

public sealed record RevokeCertificateRequest(string? Reason);

/// <param name="Values">Only the fields being corrected. Anything omitted carries over unchanged.</param>
public sealed record ReissueCertificateRequest(
    string? RecipientName,
    Dictionary<string, string>? Values,
    string? Reason);
