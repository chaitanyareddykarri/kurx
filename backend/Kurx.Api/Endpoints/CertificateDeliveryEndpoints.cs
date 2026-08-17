using System.Security.Claims;
using Kurx.Api.ExceptionHandling;
using Kurx.Application.Abstractions;

namespace Kurx.Api.Endpoints;

/// <summary>
/// Sending certificates to the people they were issued to (D-355, Phase 8).
///
/// <para>Sending QUEUES; it does not send. The response says what was queued, and the summary endpoint is
/// how the organiser watches it drain. A request that tried to send four hundred emails inline would time
/// out, and the retry would double-send to everyone it had already reached.</para>
/// </summary>
public static class CertificateDeliveryEndpoints
{
    public static void MapCertificateDeliveryEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/v1").RequireAuthorization().WithTags("certificate-deliveries");

        group.MapPost("/certificate-batches/{batchId:guid}/send", async (
            Guid batchId, ClaimsPrincipal principal,
            ICertificateDeliveryService deliveries, CancellationToken ct) =>
        {
            var result = await deliveries.QueueBatchAsync(UserId(principal), batchId, IsAdmin(principal), ct);
            return result.Ok
                ? Results.Accepted($"/v1/certificate-batches/{batchId}/deliveries", result.Value)
                : ProblemResults.Problem(result.Error!, StatusFor(result.Error!));
        })
        .RequireRateLimiting("heavy")
        .WithSummary("Queue a run's certificates for emailing")
        .Produces<CertificateDeliverySummary>(StatusCodes.Status202Accepted);

        group.MapGet("/certificate-batches/{batchId:guid}/deliveries", async (
            Guid batchId, ClaimsPrincipal principal,
            ICertificateDeliveryService deliveries, CancellationToken ct) =>
        {
            var result = await deliveries.SummariseBatchAsync(UserId(principal), batchId, IsAdmin(principal), ct);
            return result.Ok
                ? Results.Ok(result.Value)
                : ProblemResults.Problem(result.Error!, StatusFor(result.Error!));
        })
        .WithSummary("What happened to a run's sends")
        .Produces<CertificateDeliverySummary>();

        // A deliberate resend — usually because the first address was wrong — so it always queues a new
        // attempt rather than being blocked by an earlier one.
        group.MapPost("/certificates/{certificateId:guid}/send", async (
            Guid certificateId, ResendCertificateRequest? body, ClaimsPrincipal principal,
            ICertificateDeliveryService deliveries, CancellationToken ct) =>
        {
            var result = await deliveries.ResendAsync(
                UserId(principal), certificateId, body?.Destination, IsAdmin(principal), ct);
            return result.Ok
                ? Results.Accepted($"/v1/certificates/{certificateId}", result.Value)
                : ProblemResults.Problem(result.Error!, StatusFor(result.Error!));
        })
        .WithSummary("Queue one certificate for emailing")
        .Produces<CertificateDeliveryView>(StatusCodes.Status202Accepted);
    }

    private static Guid UserId(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id : throw new ApiException(StatusCodes.Status401Unauthorized, "invalid_token");

    private static bool IsAdmin(ClaimsPrincipal principal) => principal.HasClaim("kurx_admin", "true");

    /// <summary>D-018: a certificate on an event the caller may not see is not-found, never forbidden.</summary>
    private static int StatusFor(string error) => error switch
    {
        "not_found" => StatusCodes.Status404NotFound,
        "forbidden" => StatusCodes.Status403Forbidden,
        _ => StatusCodes.Status400BadRequest,
    };
}

/// <param name="Destination">A corrected address, or null to use the one on the recipient row.</param>
public sealed record ResendCertificateRequest(string? Destination);
