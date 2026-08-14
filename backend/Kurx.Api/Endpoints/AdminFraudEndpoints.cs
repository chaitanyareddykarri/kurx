using System.Security.Claims;
using Kurx.Application.Abstractions;

namespace Kurx.Api.Endpoints;

using Kurx.Api;
using Kurx.Api.ExceptionHandling;

public record AddBlacklistBody(string Kind, string Value, string? Reason);
public record RecordSignalBody(string SubjectType, Guid SubjectId, string Kind, string? Value, int Score);

/// <summary>Fraud console (M13, D-052): manage the hard blocklist and record fraud signals. Gated by
/// the VerificationReviewer platform role. The trust layer reads this live so blocks take effect
/// immediately (a blacklisted/high-risk user loses paid-organizing capability).</summary>
public static class AdminFraudEndpoints
{
    public static void MapAdminFraudEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/v1/admin").WithTags("admin").RequireAuthorization("VerificationReviewer");

        g.MapPost("/blacklist", async (AddBlacklistBody body, ClaimsPrincipal p, IFraudService svc, CancellationToken ct) =>
        {
            var r = await svc.AddBlacklistAsync(body.Kind, body.Value, body.Reason, UserId(p), ct);
            return r.Ok ? Results.Ok(ToJson(r.Value!)) : ProblemResults.Problem(r.Error, StatusCodes.Status400BadRequest);
        }).Produces<BlacklistEntryResponse>();

        g.MapGet("/blacklist", async (IFraudService svc, CancellationToken ct) =>
            Results.Ok((await svc.ListBlacklistAsync(ct)).Select(ToJson))).Produces<IReadOnlyList<BlacklistEntryResponse>>();

        g.MapDelete("/blacklist/{id:guid}", async (Guid id, IFraudService svc, CancellationToken ct) =>
            await svc.RemoveBlacklistAsync(id, ct) ? Results.Ok(OperationAck.Success) : Results.NotFound()).Produces<OperationAck>();

        g.MapPost("/fraud-signals", async (RecordSignalBody body, IFraudService svc, CancellationToken ct) =>
        {
            var r = await svc.RecordSignalAsync(body.SubjectType, body.SubjectId, body.Kind, body.Value, body.Score, ct);
            return r.Ok ? Results.Ok(new { risk_score = r.Value }) : ProblemResults.Problem(r.Error, StatusCodes.Status400BadRequest);
        }).Produces<OperationAck>();

        // D-186: exposes the already-implemented GetRiskScoreAsync (previously unwired to any endpoint) for
        // the event workspace's Overview/Moderation tabs, plus a batched form for the event list's column.
        g.MapGet("/fraud-signals/score", async (string subjectType, Guid subjectId, IFraudService svc, CancellationToken ct) =>
            Results.Ok(new SubjectRiskScore(subjectType, subjectId, await svc.GetRiskScoreAsync(subjectType, subjectId, ct)))).Produces<SubjectRiskScore>();

        g.MapGet("/fraud-signals/scores", async (string subjectType, string subjectIds, IFraudService svc, CancellationToken ct) =>
        {
            var ids = subjectIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => Guid.TryParse(s, out var id) ? id : (Guid?)null).Where(id => id is not null).Select(id => id!.Value)
                .Distinct().Take(200).ToList();
            var scores = await svc.GetRiskScoresBatchAsync(subjectType, ids, ct);
            return Results.Ok(ids.Select(id => new SubjectRiskScoreEntry(id, scores.GetValueOrDefault(id))));
        }).Produces<IReadOnlyList<SubjectRiskScoreEntry>>();
    }

    private static Guid UserId(ClaimsPrincipal principal)
        => Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id : throw new ApiException(StatusCodes.Status401Unauthorized, "invalid_token");

    private static BlacklistEntryResponse ToJson(BlacklistEntryView b) => new(
        b.Id,
        b.Kind.ToLowerInvariant(),
        b.Value,
        b.Reason,
        b.CreatedAt);
}
