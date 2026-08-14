using System.Security.Claims;
using Kurx.Api.ExceptionHandling;
using Kurx.Application.Abstractions;

namespace Kurx.Api.Endpoints;

public record StageBody(string Name, int? Sequence, string? Format, string? ParticipantSource, Guid? AdvancedFromStageId,
    string? AdvancementRule, int? AdvancementThreshold, Guid? ScoringPolicyId, DateTime? StartsAt, DateTime? EndsAt,
    Guid? VenueId, string? Mode, string? ResultsVisibility, Guid? SpectatorPoolId);
public record StageTransitionBody(string Action);
public record AddParticipantBody(string SubjectType, Guid SubjectId, int? Seed);
public record ScoringPolicyBody(string? Name, IReadOnlyList<ScoreSourceInput>? Sources, string? Aggregation, string? Normalisation,
    IReadOnlyList<string>? TieBreak, string? VoteIdentityBinding, int? VoteRateLimitPerHour, int? VoteWeightCapPercent);
public record FixtureBody(int? RoundNo, string? Label, Guid? VenueId, DateTime? SlotStart, DateTime? SlotEnd,
    IReadOnlyList<FixtureSubjectInput>? Participants, IReadOnlyList<Guid>? OfficialParticipantIds);
public record FixtureStateBody(string State, string? ResultJson);
public record ScoreBody(Guid? FixtureId, string SubjectType, Guid SubjectId, decimal Score, string? BreakdownJson);
public record VoteBody(string SubjectType, Guid SubjectId);
public record DisputeBody(string Reason);
public record CorrectBody(int? NewRank, decimal? NewScore, string Reason);

/// <summary>V3 §10 (Phase 11) — the competition engine surface: Stage · roster · ScoringPolicy · Fixture (manual
/// scheduling + conflict detection) · judge scoring · public voting · deterministic results · corrections ·
/// advancement. Additive; the approved money path, InventoryPool authority, Registration→Admission flow, Pass, VAR
/// and §17.1 concurrency are untouched. Organiser gates (`event:manage`), judge eligibility, COI, one-vote-per-
/// identity and voting windows are enforced in the service. Results reads are viewer-gated by results visibility.</summary>
public static class CompetitionEndpoints
{
    public static void MapCompetitionEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/v1").WithTags("competition").RequireAuthorization();

        // ── Stages ──────────────────────────────────────────────────────────────
        g.MapPost("/events/{eventId:guid}/stages", async (Guid eventId, StageBody b, ClaimsPrincipal p, ICompetitionService svc, CancellationToken ct) =>
        {
            var r = await svc.CreateStageAsync(UserId(p), eventId, IsAdmin(p), ToStageInput(b), ct);
            return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
        }).Produces<StageView>();

        g.MapGet("/events/{eventId:guid}/stages", async (Guid eventId, ICompetitionService svc, CancellationToken ct) =>
        {
            var r = await svc.ListStagesAsync(eventId, ct);
            return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
        }).Produces<IReadOnlyList<StageView>>();

        g.MapGet("/stages/{stageId:guid}", async (Guid stageId, ICompetitionService svc, CancellationToken ct) =>
        {
            var r = await svc.GetStageAsync(stageId, ct);
            return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
        }).Produces<StageView>();

        g.MapPatch("/stages/{stageId:guid}", async (Guid stageId, StageBody b, ClaimsPrincipal p, ICompetitionService svc, CancellationToken ct) =>
        {
            var r = await svc.UpdateStageAsync(UserId(p), stageId, IsAdmin(p), ToStageInput(b), ct);
            return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
        }).Produces<StageView>();

        g.MapPost("/stages/{stageId:guid}/transition", async (Guid stageId, StageTransitionBody b, ClaimsPrincipal p, ICompetitionService svc, CancellationToken ct) =>
        {
            var r = await svc.TransitionStageAsync(UserId(p), stageId, IsAdmin(p), b.Action, ct);
            return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
        }).Produces<StageView>();

        g.MapDelete("/stages/{stageId:guid}", async (Guid stageId, ClaimsPrincipal p, ICompetitionService svc, CancellationToken ct) =>
        {
            var r = await svc.DeleteStageAsync(UserId(p), stageId, IsAdmin(p), ct);
            return r.Ok ? Results.NoContent() : Fail(r.Error);
        }).Produces(StatusCodes.Status204NoContent);

        // ── Roster ──────────────────────────────────────────────────────────────
        g.MapGet("/stages/{stageId:guid}/participants", async (Guid stageId, ICompetitionService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListStageParticipantsAsync(stageId, ct))).Produces<IReadOnlyList<StageParticipantView>>();

        g.MapPost("/stages/{stageId:guid}/participants", async (Guid stageId, AddParticipantBody b, ClaimsPrincipal p, ICompetitionService svc, CancellationToken ct) =>
        {
            var r = await svc.AddStageParticipantAsync(UserId(p), stageId, IsAdmin(p), b.SubjectType, b.SubjectId, b.Seed, ct);
            return r.Ok ? Results.NoContent() : Fail(r.Error);
        }).Produces(StatusCodes.Status204NoContent);

        g.MapPost("/stages/{stageId:guid}/participants/seed-registered", async (Guid stageId, ClaimsPrincipal p, ICompetitionService svc, CancellationToken ct) =>
        {
            var r = await svc.SeedFromRegisteredAsync(UserId(p), stageId, IsAdmin(p), ct);
            return r.Ok ? Results.Ok(new { added = r.Value }) : Fail(r.Error);
        });

        g.MapDelete("/stages/participants/{rowId:guid}", async (Guid rowId, ClaimsPrincipal p, ICompetitionService svc, CancellationToken ct) =>
        {
            var r = await svc.RemoveStageParticipantAsync(UserId(p), rowId, IsAdmin(p), ct);
            return r.Ok ? Results.NoContent() : Fail(r.Error);
        }).Produces(StatusCodes.Status204NoContent);

        // ── ScoringPolicy ─────────────────────────────────────────────────────────
        g.MapPost("/events/{eventId:guid}/scoring-policies", async (Guid eventId, ScoringPolicyBody b, ClaimsPrincipal p, ICompetitionService svc, CancellationToken ct) =>
        {
            var r = await svc.CreateScoringPolicyAsync(UserId(p), eventId, IsAdmin(p), ToPolicyInput(b), ct);
            return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
        }).Produces<ScoringPolicyView>();

        g.MapGet("/events/{eventId:guid}/scoring-policies", async (Guid eventId, ClaimsPrincipal p, ICompetitionService svc, CancellationToken ct) =>
        {
            var r = await svc.ListScoringPoliciesAsync(UserId(p), eventId, IsAdmin(p), ct);
            return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
        }).Produces<IReadOnlyList<ScoringPolicyView>>();

        g.MapPatch("/scoring-policies/{policyId:guid}", async (Guid policyId, ScoringPolicyBody b, ClaimsPrincipal p, ICompetitionService svc, CancellationToken ct) =>
        {
            var r = await svc.UpdateScoringPolicyAsync(UserId(p), policyId, IsAdmin(p), ToPolicyInput(b), ct);
            return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
        }).Produces<ScoringPolicyView>();

        // ── Fixtures ──────────────────────────────────────────────────────────────
        g.MapPost("/stages/{stageId:guid}/fixtures", async (Guid stageId, FixtureBody b, ClaimsPrincipal p, ICompetitionService svc, CancellationToken ct) =>
        {
            var r = await svc.CreateFixtureAsync(UserId(p), stageId, IsAdmin(p),
                new FixtureInput(b.RoundNo, b.Label, b.VenueId, b.SlotStart, b.SlotEnd, b.Participants, b.OfficialParticipantIds), ct);
            return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
        }).Produces<FixtureView>();

        g.MapGet("/stages/{stageId:guid}/fixtures", async (Guid stageId, ICompetitionService svc, CancellationToken ct) =>
        {
            var r = await svc.ListFixturesAsync(stageId, ct);
            return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
        }).Produces<IReadOnlyList<FixtureView>>();

        g.MapPost("/fixtures/{fixtureId:guid}/state", async (Guid fixtureId, FixtureStateBody b, ClaimsPrincipal p, ICompetitionService svc, CancellationToken ct) =>
        {
            var r = await svc.SetFixtureStateAsync(UserId(p), fixtureId, IsAdmin(p), b.State, b.ResultJson, ct);
            return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
        }).Produces<FixtureView>();

        // ── Judging + voting ──────────────────────────────────────────────────────
        g.MapPost("/stages/{stageId:guid}/scores", async (Guid stageId, ScoreBody b, ClaimsPrincipal p, ICompetitionService svc, CancellationToken ct) =>
        {
            var r = await svc.SubmitScoreAsync(UserId(p), stageId, new ScoreInput(b.FixtureId, b.SubjectType, b.SubjectId, b.Score, b.BreakdownJson), ct);
            return r.Ok ? Results.NoContent() : Fail(r.Error);
        }).Produces(StatusCodes.Status204NoContent);

        g.MapPost("/stages/{stageId:guid}/votes", async (Guid stageId, VoteBody b, ClaimsPrincipal p, ICompetitionService svc, CancellationToken ct) =>
        {
            var r = await svc.CastVoteAsync(UserId(p), stageId, b.SubjectType, b.SubjectId, ct);
            return r.Ok ? Results.NoContent() : Fail(r.Error);
        }).Produces(StatusCodes.Status204NoContent);

        // ── Results + advancement ───────────────────────────────────────────────────
        g.MapPost("/stages/{stageId:guid}/results/compute", async (Guid stageId, ClaimsPrincipal p, ICompetitionService svc, CancellationToken ct) =>
        {
            var r = await svc.ComputeResultsAsync(UserId(p), stageId, IsAdmin(p), ct);
            return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
        }).Produces<IReadOnlyList<ResultView>>();

        g.MapPost("/stages/{stageId:guid}/results/publish", async (Guid stageId, ClaimsPrincipal p, ICompetitionService svc, CancellationToken ct) =>
        {
            var r = await svc.PublishResultsAsync(UserId(p), stageId, IsAdmin(p), ct);
            return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
        }).Produces<IReadOnlyList<ResultView>>();

        // Public/spectator read — visibility-gated in the service; anonymous callers see published results only.
        g.MapGet("/stages/{stageId:guid}/results", async (Guid stageId, ClaimsPrincipal p, ICompetitionService svc, CancellationToken ct) =>
        {
            var r = await svc.GetResultsAsync(OptionalUserId(p), stageId, IsAdmin(p), ct);
            return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
        }).AllowAnonymous().Produces<IReadOnlyList<ResultView>>();

        g.MapPost("/results/{resultId:guid}/dispute", async (Guid resultId, DisputeBody b, ClaimsPrincipal p, ICompetitionService svc, CancellationToken ct) =>
        {
            var r = await svc.DisputeResultAsync(UserId(p), resultId, IsAdmin(p), b.Reason, ct);
            return r.Ok ? Results.NoContent() : Fail(r.Error);
        }).Produces(StatusCodes.Status204NoContent);

        g.MapPost("/results/{resultId:guid}/correct", async (Guid resultId, CorrectBody b, ClaimsPrincipal p, ICompetitionService svc, CancellationToken ct) =>
        {
            var r = await svc.CorrectResultAsync(UserId(p), resultId, IsAdmin(p), b.NewRank, b.NewScore, b.Reason, ct);
            return r.Ok ? Results.NoContent() : Fail(r.Error);
        }).Produces(StatusCodes.Status204NoContent);

        g.MapPost("/stages/{stageId:guid}/advance", async (Guid stageId, ClaimsPrincipal p, ICompetitionService svc, CancellationToken ct) =>
        {
            var r = await svc.AdvanceStageAsync(UserId(p), stageId, IsAdmin(p), ct);
            return r.Ok ? Results.Ok(new { advanced = r.Value }) : Fail(r.Error);
        });
    }

    private static StageInput ToStageInput(StageBody b) => new(b.Name, b.Sequence, b.Format, b.ParticipantSource, b.AdvancedFromStageId,
        b.AdvancementRule, b.AdvancementThreshold, b.ScoringPolicyId, b.StartsAt, b.EndsAt, b.VenueId, b.Mode, b.ResultsVisibility, b.SpectatorPoolId);

    private static ScoringPolicyInput ToPolicyInput(ScoringPolicyBody b) => new(b.Name, b.Sources, b.Aggregation, b.Normalisation,
        b.TieBreak, b.VoteIdentityBinding, b.VoteRateLimitPerHour, b.VoteWeightCapPercent);

    private static Guid UserId(ClaimsPrincipal principal)
        => Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : throw new ApiException(StatusCodes.Status401Unauthorized, "invalid_token");

    private static Guid? OptionalUserId(ClaimsPrincipal principal)
        => Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    private static bool IsAdmin(ClaimsPrincipal principal) => principal.HasClaim("kurx_admin", "true");

    private static IResult Fail(string? error) => error switch
    {
        "forbidden" => ProblemResults.Problem(error, StatusCodes.Status403Forbidden),
        "not_found" => ProblemResults.Problem(error, StatusCodes.Status404NotFound),
        _ => ProblemResults.Problem(error, StatusCodes.Status400BadRequest),
    };
}
