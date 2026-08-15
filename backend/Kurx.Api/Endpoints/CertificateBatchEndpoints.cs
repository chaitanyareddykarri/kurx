using System.Security.Claims;
using System.Text.Json;
using Kurx.Api.ExceptionHandling;
using Kurx.Application.Abstractions;

namespace Kurx.Api.Endpoints;

/// <summary>
/// Generating certificates for a whole participant list (D-344, Phase 7).
///
/// <para>The shape of this surface is the confirmed flow: create → preview → approve → (background run) →
/// poll. Approval is its own call because it is the irreversible one, and a separate verb is what lets it
/// be a deliberate act rather than a side effect of uploading a file.</para>
///
/// <para>There is no "generate now" endpoint. The full run only ever starts from an approval, and only
/// ever in the background.</para>
/// </summary>
public static class CertificateBatchEndpoints
{
    public static void MapCertificateBatchEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/v1").RequireAuthorization().WithTags("certificate-batches");

        group.MapPost("/events/{eventId:guid}/certificate-batches", async (
            Guid eventId, IFormFile file, string name, Guid templateId, string mapping,
            ClaimsPrincipal principal, ICertificateBatchService batches, CancellationToken ct) =>
        {
            var bytes = await ReadAsync(file, ct);
            if (bytes is null)
                return ProblemResults.Problem("file_too_large", StatusCodes.Status413PayloadTooLarge);

            Dictionary<string, string>? columns;
            try
            {
                columns = JsonSerializer.Deserialize<Dictionary<string, string>>(mapping ?? "");
            }
            catch (JsonException)
            {
                return ProblemResults.Problem("invalid_column_mapping", StatusCodes.Status400BadRequest);
            }
            if (columns is null)
                return ProblemResults.Problem("invalid_column_mapping", StatusCodes.Status400BadRequest);

            var result = await batches.CreateAsync(UserId(principal), eventId,
                new CertificateBatchInput(name ?? "", templateId, file.FileName, bytes, columns),
                IsAdmin(principal), ct);

            return Respond(result, StatusCodes.Status201Created);
        })
        .DisableAntiforgery()
        .RequireRateLimiting("heavy")
        .WithSummary("Create a certificate run from a participant list")
        .Produces<CertificateBatchView>(StatusCodes.Status201Created);

        group.MapGet("/events/{eventId:guid}/certificate-batches", async (
            Guid eventId, ClaimsPrincipal principal, ICertificateBatchService batches, CancellationToken ct) =>
        {
            var result = await batches.ListAsync(UserId(principal), eventId, IsAdmin(principal), ct);
            return result.Ok
                ? Results.Ok(result.Value)
                : ProblemResults.Problem(result.Error!, StatusFor(result.Error!));
        })
        .WithSummary("List an event's certificate runs");

        /// The poll target while a run is generating.
        group.MapGet("/certificate-batches/{batchId:guid}", async (
            Guid batchId, ClaimsPrincipal principal, ICertificateBatchService batches, CancellationToken ct) =>
            Respond(await batches.GetAsync(UserId(principal), batchId, IsAdmin(principal), ct)))
        .WithSummary("Read a certificate run's status")
        .Produces<CertificateBatchView>();

        group.MapPost("/certificate-batches/{batchId:guid}/preview", async (
            Guid batchId, ClaimsPrincipal principal, ICertificateBatchService batches, CancellationToken ct) =>
            Respond(await batches.GeneratePreviewAsync(UserId(principal), batchId, IsAdmin(principal), ct)))
        .RequireRateLimiting("heavy")
        .WithSummary("Render sample certificates for inspection")
        .Produces<CertificateBatchView>();

        // The irreversible one. Everything before it is a parsed file and three sample renders; after it,
        // certificates exist.
        group.MapPost("/certificate-batches/{batchId:guid}/approve", async (
            Guid batchId, ClaimsPrincipal principal, ICertificateBatchService batches, CancellationToken ct) =>
            Respond(await batches.ApproveAsync(UserId(principal), batchId, IsAdmin(principal), ct)))
        .WithSummary("Approve the preview and queue the full run")
        .Produces<CertificateBatchView>();

        group.MapPost("/certificate-batches/{batchId:guid}/cancel", async (
            Guid batchId, ClaimsPrincipal principal, ICertificateBatchService batches, CancellationToken ct) =>
            Respond(await batches.CancelAsync(UserId(principal), batchId, IsAdmin(principal), ct)))
        .WithSummary("Stop a run that has not finished")
        .Produces<CertificateBatchView>();
    }

    private static Guid UserId(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id : throw new ApiException(StatusCodes.Status401Unauthorized, "invalid_token");

    private static bool IsAdmin(ClaimsPrincipal principal) => principal.HasClaim("kurx_admin", "true");

    private static IResult Respond(ServiceResult<CertificateBatchView> result, int okStatus = StatusCodes.Status200OK) =>
        result.Ok
            ? okStatus == StatusCodes.Status201Created
                ? Results.Created($"/v1/certificate-batches/{result.Value!.Id}", result.Value)
                : Results.Ok(result.Value)
            : ProblemResults.Problem(result.Error!, StatusFor(result.Error!));

    /// <summary>D-018: a batch on an event the caller may not see is reported as not-found, never as
    /// forbidden — the 403 would confirm it exists.</summary>
    private static int StatusFor(string error) => error switch
    {
        "not_found" or "template_not_found" => StatusCodes.Status404NotFound,
        "forbidden" => StatusCodes.Status403Forbidden,
        _ => StatusCodes.Status400BadRequest,
    };

    /// <summary>Reads the upload, refusing anything oversized before it is fully in memory. The declared
    /// length is checked first and the actual bytes again as they stream — a Content-Length is what the
    /// client claimed, and a claim is not a fact.</summary>
    private static async Task<byte[]?> ReadAsync(IFormFile file, CancellationToken ct)
    {
        if (file.Length > Kurx.Infrastructure.Certificates.SpreadsheetService.MaxFileBytes) return null;

        using var buffer = new MemoryStream();
        await using var source = file.OpenReadStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await source.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + read > Kurx.Infrastructure.Certificates.SpreadsheetService.MaxFileBytes) return null;
            await buffer.WriteAsync(chunk.AsMemory(0, read), ct);
        }
        return buffer.ToArray();
    }
}
