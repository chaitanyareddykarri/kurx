using System.Security.Claims;
using Kurx.Application.Abstractions;

namespace Kurx.Api.Endpoints;

using Kurx.Api.ExceptionHandling;

/// <summary>
/// Reading a participant list, without generating anything (D-355, Phase 6).
///
/// <para>The mapping step exists because a silent guess is dangerous: a column called "Name" holding a
/// team name is an ordinary spreadsheet, and applying that guess prints the wrong words on every
/// certificate in the run. So the file is read, the columns and a few real sample values come back with a
/// SUGGESTED mapping, and the organiser confirms or corrects it before anything is rendered.</para>
///
/// <para>Multipart rather than JSON: a 10MB spreadsheet becomes 13MB of base64 that must be held as a
/// string before it can be decoded, and the browser already has an encoding for exactly this.</para>
/// </summary>
public static class SpreadsheetEndpoints
{
    /// <summary>How many rows the mapping screen shows. Enough to see whether a column holds what its
    /// header claims; few enough that the response stays small.</summary>
    private const int SampleRows = 5;

    public static void MapSpreadsheetEndpoints(this WebApplication app)
    {
        app.MapPost("/v1/events/{eventId:guid}/certificate-participants/preview", async (
            Guid eventId, IFormFile file, ClaimsPrincipal principal,
            IEventAuthority authority, ISpreadsheetService spreadsheets, CancellationToken ct) =>
        {
            var userId = Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
                ? id : throw new ApiException(StatusCodes.Status401Unauthorized, "invalid_token");

            var access = await authority.ResolveAsync(userId, eventId, principal.HasClaim("kurx_admin", "true"), ct);
            if (!access.EventExists) return ProblemResults.Problem("not_found", StatusCodes.Status404NotFound);
            if (!access.Can(EventPermission.ManageContent))
                return ProblemResults.Problem("forbidden", StatusCodes.Status403Forbidden);

            var bytes = await ReadAsync(file, ct);
            if (bytes is null)
                return ProblemResults.Problem("file_too_large", StatusCodes.Status413PayloadTooLarge);

            var read = spreadsheets.Read(bytes, file.FileName);
            if (!read.Ok) return ProblemResults.Problem(read.Error, StatusCodes.Status400BadRequest);

            var table = read.Value!;
            return Results.Ok(new SpreadsheetPreviewResponse(
                table.Columns,
                table.Rows.Take(SampleRows).ToList(),
                table.Rows.Count,
                table.Truncated,
                spreadsheets.SuggestMapping(table.Columns)));
        })
        .RequireAuthorization()
        .DisableAntiforgery()
        .RequireRateLimiting("heavy")
        .WithTags("certificate-participants")
        .WithSummary("Read a participant list and propose a column mapping")
        .Produces<SpreadsheetPreviewResponse>();
    }

    /// <summary>Reads the upload, refusing anything oversized before it is fully in memory.
    ///
    /// <para>The declared length is checked first and the actual bytes again as they stream:
    /// <c>IFormFile.Length</c> is what the client claimed, and a claim is not a fact. Without the second
    /// check a lying Content-Length would put an arbitrarily large body into a byte array.</para></summary>
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

/// <param name="SuggestedMapping">Column name → field key. A proposal the organiser reviews, never
/// something applied on their behalf.</param>
/// <param name="Truncated">True when the file held more rows than were read. Surfaced so "we generated
/// 1000 of your 1500" is never discovered by counting.</param>
public sealed record SpreadsheetPreviewResponse(
    IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyList<string>> SampleRows,
    int TotalRows,
    bool Truncated,
    IReadOnlyDictionary<string, string> SuggestedMapping);
