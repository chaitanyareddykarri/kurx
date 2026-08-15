using System.Security.Claims;
using Kurx.Api.ExceptionHandling;
using Kurx.Application.Abstractions;

namespace Kurx.Api.Endpoints;

/// <summary>
/// How the person named on a certificate reaches it (D-344, Phase 10).
///
/// <para>The capability endpoint is anonymous on purpose: holding the token IS the authorisation, which is
/// the only way someone who was on a spreadsheet and never had an account can collect what was issued to
/// them. It is rate limited for the same reason the verification endpoint is — an unauthenticated endpoint
/// keyed on a secret is a guessing target, and 256 bits of entropy is the real defence with the limit as
/// the backstop.</para>
/// </summary>
public static class CertificateParticipantEndpoints
{
    public static void MapCertificateParticipantEndpoints(this WebApplication app)
    {
        // Anonymous. The token in the path is the credential.
        app.MapGet("/v1/certificate-access/{token}", async (
            string token, ICertificateParticipantService participants, CancellationToken ct) =>
        {
            var result = await participants.ResolveAccessAsync(token, ct);
            return result.Ok
                ? Results.Ok(result.Value)
                : ProblemResults.Problem("not_found", StatusCodes.Status404NotFound);
        })
        .AllowAnonymous()
        .RequireRateLimiting("verify")
        .WithTags("certificate-access")
        .WithSummary("A participant's certificates, reached by capability link")
        .Produces<ParticipantCertificatesView>();

        var group = app.MapGroup("/v1").RequireAuthorization().WithTags("certificate-access");

        group.MapGet("/me/certificates", async (
            ClaimsPrincipal principal, ICertificateParticipantService participants, CancellationToken ct) =>
        {
            var result = await participants.ListMineAsync(UserId(principal), ct);
            return result.Ok ? Results.Ok(result.Value) : ProblemResults.Problem(result.Error!, 400);
        })
        .WithSummary("The signed-in user's own certificates")
        .Produces<ParticipantCertificatesView>();

        group.MapPost("/certificate-recipients/{recipientId:guid}/access-link", async (
            Guid recipientId, ClaimsPrincipal principal,
            ICertificateParticipantService participants, CancellationToken ct) =>
        {
            var result = await participants.CreateAccessLinkAsync(
                UserId(principal), recipientId, IsAdmin(principal), ct);
            return result.Ok
                ? Results.Created($"/v1/certificate-recipients/{recipientId}/access-links", result.Value)
                : ProblemResults.Problem(result.Error!, StatusFor(result.Error!));
        })
        .WithSummary("Mint a capability link (the token is shown once)")
        .Produces<CertificateAccessLinkView>(StatusCodes.Status201Created);

        group.MapGet("/certificate-recipients/{recipientId:guid}/access-links", async (
            Guid recipientId, ClaimsPrincipal principal,
            ICertificateParticipantService participants, CancellationToken ct) =>
        {
            var result = await participants.ListAccessLinksAsync(
                UserId(principal), recipientId, IsAdmin(principal), ct);
            return result.Ok
                ? Results.Ok(result.Value)
                : ProblemResults.Problem(result.Error!, StatusFor(result.Error!));
        })
        .WithSummary("A recipient's links, without their tokens")
        .Produces<IReadOnlyList<CertificateAccessLinkView>>();

        group.MapDelete("/certificate-access-links/{linkId:guid}", async (
            Guid linkId, ClaimsPrincipal principal,
            ICertificateParticipantService participants, CancellationToken ct) =>
        {
            var result = await participants.RevokeAccessLinkAsync(
                UserId(principal), linkId, IsAdmin(principal), ct);
            return result.Ok
                ? Results.NoContent()
                : ProblemResults.Problem(result.Error!, StatusFor(result.Error!));
        })
        .WithSummary("Close a capability link (the certificates are untouched)");
    }

    private static Guid UserId(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id : throw new ApiException(StatusCodes.Status401Unauthorized, "invalid_token");

    private static bool IsAdmin(ClaimsPrincipal principal) => principal.HasClaim("kurx_admin", "true");

    private static int StatusFor(string error) => error switch
    {
        "not_found" => StatusCodes.Status404NotFound,
        _ => StatusCodes.Status400BadRequest,
    };
}
