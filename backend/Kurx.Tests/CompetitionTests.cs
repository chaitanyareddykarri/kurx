using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>V3 §10 (Phase 11) — the competition engine: Stage · roster · ScoringPolicy · Fixture (manual scheduling +
/// conflict detection) · judge scoring · public voting · deterministic results · append-only corrections · advancement.
/// Additive — the approved money path, InventoryPool authority, Registration→Admission flow, Pass, VAR and §17.1
/// concurrency are untouched. Enforceable transactional fraud controls; no statistical anomaly detection (later phase).</summary>
public class CompetitionTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;

    public CompetitionTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock) { if (!_reset) { factory.ResetDatabase(); _reset = true; } }
    }

    private static readonly object ResetLock = new();
    private static bool _reset;

    private static async Task<JsonElement> Json(HttpResponseMessage res) => await res.Content.ReadFromJsonAsync<JsonElement>();
    private ICompetitionService Svc(IServiceScope s) => s.ServiceProvider.GetRequiredService<ICompetitionService>();
    private static StageInput Stage(string name, string? source = null, string? visibility = null, string? rule = null, int? threshold = null, Guid? advancedFrom = null, Guid? policyId = null, int? seq = null)
        => new(name, seq, null, source, advancedFrom, rule, threshold, policyId, null, null, null, null, visibility, null);

    // ── Stages ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_stage_requires_manage_and_auto_sequences()
    {
        var (owner, orgId, ownerId) = await LoginOrgAsync("9700011001", "Stage Org");
        var eventId = await CreateEventAsync(owner, orgId);
        var (_, strangerId) = await LoginAsync("9700011002");

        using var scope = _factory.Services.CreateScope();
        var svc = Svc(scope);
        Assert.Equal("forbidden", (await svc.CreateStageAsync(strangerId, eventId, false, Stage("Quals"))).Error);
        var s1 = await svc.CreateStageAsync(ownerId, eventId, false, Stage("Quals"));
        var s2 = await svc.CreateStageAsync(ownerId, eventId, false, Stage("Finals"));
        Assert.True(s1.Ok && s2.Ok);
        Assert.Equal(1, s1.Value!.Sequence);
        Assert.Equal(2, s2.Value!.Sequence);
        Assert.Equal("Draft", s1.Value!.State);
    }

    [Fact]
    public async Task Stage_sequence_collision_is_rejected()
    {
        var (owner, orgId, ownerId) = await LoginOrgAsync("9700011003", "Seq Org");
        var eventId = await CreateEventAsync(owner, orgId);

        using var scope = _factory.Services.CreateScope();
        var svc = Svc(scope);
        Assert.True((await svc.CreateStageAsync(ownerId, eventId, false, Stage("A", seq: 5))).Ok);
        Assert.Equal("sequence_taken", (await svc.CreateStageAsync(ownerId, eventId, false, Stage("B", seq: 5))).Error);
    }

    // ── Roster ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Roster_add_list_and_remove()
    {
        var (owner, orgId, ownerId) = await LoginOrgAsync("9700011010", "Roster Org");
        var eventId = await CreateEventAsync(owner, orgId);
        var (_, personId) = await LoginAsync("9700011011");

        using var scope = _factory.Services.CreateScope();
        var svc = Svc(scope);
        var stage = (await svc.CreateStageAsync(ownerId, eventId, false, Stage("R"))).Value!;
        Assert.True((await svc.AddStageParticipantAsync(ownerId, stage.Id, false, "Person", personId, seed: 1, default)).Ok);
        Assert.Equal("already_on_stage", (await svc.AddStageParticipantAsync(ownerId, stage.Id, false, "Person", personId, 1, default)).Error);
        var roster = await svc.ListStageParticipantsAsync(stage.Id);
        Assert.Single(roster);
        // The roster is keyed by GUID but has to be renderable, so SubjectName is resolved server-side and
        // always yields something — real name, else @username, else "(unknown)". Never blank.
        Assert.False(string.IsNullOrWhiteSpace(roster[0].SubjectName));
        Assert.True((await svc.RemoveStageParticipantAsync(ownerId, roster[0].Id, false, default)).Ok);
        Assert.Empty(await svc.ListStageParticipantsAsync(stage.Id));
    }

    [Fact]
    public async Task Seed_from_registered_adds_competition_teams()
    {
        var (owner, orgId, ownerId) = await LoginOrgAsync("9700011012", "Seed Org");
        var (eventId, ttId) = await PublishCompetitionAsync(owner, orgId);
        var teamA = await SeedTeamAsync(eventId, ttId, "Alpha", TeamState.Complete);
        await SeedTeamAsync(eventId, ttId, "Bravo", TeamState.Locked);
        await SeedTeamAsync(eventId, ttId, "Forming", TeamState.Forming);   // not seedable

        using var scope = _factory.Services.CreateScope();
        var svc = Svc(scope);
        var stage = (await svc.CreateStageAsync(ownerId, eventId, false, Stage("Group", source: "AllRegistered"))).Value!;
        var seeded = await svc.SeedFromRegisteredAsync(ownerId, stage.Id, false);
        Assert.True(seeded.Ok);
        Assert.Equal(2, seeded.Value);   // only Complete + Locked
        Assert.Equal(0, (await svc.SeedFromRegisteredAsync(ownerId, stage.Id, false)).Value);   // idempotent
        // Team subjects resolve to the real team name, not the raw id.
        Assert.Contains(await svc.ListStageParticipantsAsync(stage.Id),
            p => p.SubjectId == teamA && p.SubjectType == "Team" && p.SubjectName == "Alpha");
    }

    // ── ScoringPolicy ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Scoring_policy_validates_and_persists()
    {
        var (owner, orgId, ownerId) = await LoginOrgAsync("9700011020", "Policy Org");
        var eventId = await CreateEventAsync(owner, orgId);

        using var scope = _factory.Services.CreateScope();
        var svc = Svc(scope);
        Assert.Equal("invalid_weight_cap", (await svc.CreateScoringPolicyAsync(ownerId, eventId, false, Policy("P") with { VoteWeightCapPercent = 150 })).Error);
        Assert.Equal("invalid_source_type", (await svc.CreateScoringPolicyAsync(ownerId, eventId, false, Policy("P") with { Sources = [new("NONSENSE", 1, null)] })).Error);
        var ok = await svc.CreateScoringPolicyAsync(ownerId, eventId, false, Policy("Blend") with
        {
            Sources = [new("JUDGE", 0.7, null), new("PUBLIC_VOTE", 0.3, null)], Aggregation = "TrimmedMean", VoteWeightCapPercent = 40,
        });
        Assert.True(ok.Ok);
        Assert.Equal("TrimmedMean", ok.Value!.Aggregation);
        Assert.Equal(40, ok.Value!.VoteWeightCapPercent);
        Assert.Equal(2, ok.Value!.Sources.Count);
    }

    // ── Fixtures + conflict detection ────────────────────────────────────────────

    [Fact]
    public async Task Fixture_conflict_detection_flags_double_booking()
    {
        var (owner, orgId, ownerId) = await LoginOrgAsync("9700011030", "Fixture Org");
        var eventId = await CreateEventAsync(owner, orgId);
        var (_, p1) = await LoginAsync("9700011031");
        var (_, p2) = await LoginAsync("9700011032");
        var (_, p3) = await LoginAsync("9700011033");
        var venueId = await SeedVenueAsync(orgId);

        using var scope = _factory.Services.CreateScope();
        var svc = Svc(scope);
        var stage = (await svc.CreateStageAsync(ownerId, eventId, false, Stage("Bracket"))).Value!;
        var t0 = DateTime.UtcNow.AddDays(10);
        var f1 = await svc.CreateFixtureAsync(ownerId, stage.Id, false, new FixtureInput(1, "F1", venueId, t0, t0.AddHours(1),
            [new("Person", p1, 1), new("Person", p2, 2)], null));
        Assert.True(f1.Ok);
        // Same venue, overlapping slot → venue conflict.
        var venueClash = await svc.CreateFixtureAsync(ownerId, stage.Id, false, new FixtureInput(1, "F2", venueId, t0.AddMinutes(30), t0.AddHours(2), null, null));
        Assert.Equal("venue_double_booked", venueClash.Error);
        // Shared participant, overlapping slot (different/no venue) → participant conflict.
        var partClash = await svc.CreateFixtureAsync(ownerId, stage.Id, false, new FixtureInput(1, "F3", null, t0.AddMinutes(30), t0.AddHours(2),
            [new("Person", p1, 1), new("Person", p3, 2)], null));
        Assert.Equal("participant_double_booked", partClash.Error);
        // Non-overlapping slot → allowed.
        Assert.True((await svc.CreateFixtureAsync(ownerId, stage.Id, false, new FixtureInput(1, "F4", venueId, t0.AddHours(3), t0.AddHours(4), null, null))).Ok);
    }

    // ── Judging ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Only_a_live_stage_judge_can_score_and_resubmission_updates()
    {
        var (owner, orgId, ownerId) = await LoginOrgAsync("9700011040", "Judge Org");
        var eventId = await CreateEventAsync(owner, orgId);
        var (_, judgeId) = await LoginAsync("9700011041");
        var (_, competitorId) = await LoginAsync("9700011042");
        var (_, notJudgeId) = await LoginAsync("9700011043");
        await SeedJudgeAsync(eventId, judgeId);

        using var scope = _factory.Services.CreateScope();
        var svc = Svc(scope);
        var stage = (await svc.CreateStageAsync(ownerId, eventId, false, Stage("Score"))).Value!;
        var score = new ScoreInput(null, "Person", competitorId, 8.0m, null);
        Assert.Equal("stage_not_live", (await svc.SubmitScoreAsync(judgeId, stage.Id, score)).Error);   // window closed
        await svc.TransitionStageAsync(ownerId, stage.Id, false, "open");
        Assert.Equal("not_a_judge", (await svc.SubmitScoreAsync(notJudgeId, stage.Id, score)).Error);
        Assert.True((await svc.SubmitScoreAsync(judgeId, stage.Id, score)).Ok);
        Assert.True((await svc.SubmitScoreAsync(judgeId, stage.Id, score with { Score = 9.5m })).Ok);   // upsert, not duplicate

        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var row = await db.JudgeScores.SingleAsync(j => j.StageId == stage.Id && j.SubjectId == competitorId);
        Assert.Equal(9.5m, row.Score);
    }

    [Fact]
    public async Task Judge_cannot_score_a_conflicted_subject()
    {
        var (owner, orgId, ownerId) = await LoginOrgAsync("9700011044", "COI Org");
        var (eventId, ttId) = await PublishCompetitionAsync(owner, orgId);
        var (_, judgeId) = await LoginAsync("9700011045");
        await SeedJudgeAsync(eventId, judgeId);
        var ownTeam = await SeedTeamAsync(eventId, ttId, "Judges Team", TeamState.Complete, memberId: judgeId);

        using var scope = _factory.Services.CreateScope();
        var svc = Svc(scope);
        var stage = (await svc.CreateStageAsync(ownerId, eventId, false, Stage("COI"))).Value!;
        await svc.TransitionStageAsync(ownerId, stage.Id, false, "open");
        Assert.Equal("conflict_of_interest", (await svc.SubmitScoreAsync(judgeId, stage.Id, new ScoreInput(null, "Team", ownTeam, 10m, null))).Error);
        Assert.Equal("conflict_of_interest", (await svc.SubmitScoreAsync(judgeId, stage.Id, new ScoreInput(null, "Person", judgeId, 10m, null))).Error);   // self
    }

    // ── Voting ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Public_vote_is_one_per_identity_within_the_window()
    {
        var (owner, orgId, ownerId) = await LoginOrgAsync("9700011050", "Vote Org");
        var eventId = await CreateEventAsync(owner, orgId);
        var (_, voterId) = await LoginAsync("9700011051");
        var (_, subjectA) = await LoginAsync("9700011052");
        var (_, subjectB) = await LoginAsync("9700011053");

        using var scope = _factory.Services.CreateScope();
        var svc = Svc(scope);
        var stage = (await svc.CreateStageAsync(ownerId, eventId, false, Stage("Vote"))).Value!;
        await svc.AddStageParticipantAsync(ownerId, stage.Id, false, "Person", subjectA, null, default);
        await svc.AddStageParticipantAsync(ownerId, stage.Id, false, "Person", subjectB, null, default);
        Assert.Equal("voting_closed", (await svc.CastVoteAsync(voterId, stage.Id, "Person", subjectA)).Error);
        await svc.TransitionStageAsync(ownerId, stage.Id, false, "open");
        Assert.Equal("invalid_subject", (await svc.CastVoteAsync(voterId, stage.Id, "Person", Guid.NewGuid())).Error);
        Assert.True((await svc.CastVoteAsync(voterId, stage.Id, "Person", subjectA)).Ok);
        Assert.Equal("already_voted", (await svc.CastVoteAsync(voterId, stage.Id, "Person", subjectB)).Error);   // one per identity
    }

    // ── Deterministic results + visibility + advancement ─────────────────────────

    [Fact]
    public async Task Compute_ranks_deterministically_publish_gates_visibility()
    {
        var (owner, orgId, ownerId) = await LoginOrgAsync("9700011060", "Result Org");
        var eventId = await CreateEventAsync(owner, orgId);
        var (_, j1) = await LoginAsync("9700011061");
        var (_, j2) = await LoginAsync("9700011062");
        var (_, hi) = await LoginAsync("9700011063");
        var (_, lo) = await LoginAsync("9700011064");
        await SeedJudgeAsync(eventId, j1);
        await SeedJudgeAsync(eventId, j2);

        using var scope = _factory.Services.CreateScope();
        var svc = Svc(scope);
        var stage = (await svc.CreateStageAsync(ownerId, eventId, false, Stage("Judged", visibility: "OnStageClose"))).Value!;
        foreach (var s in new[] { hi, lo }) await svc.AddStageParticipantAsync(ownerId, stage.Id, false, "Person", s, null, default);
        await svc.TransitionStageAsync(ownerId, stage.Id, false, "open");
        foreach (var j in new[] { j1, j2 })
        {
            Assert.True((await svc.SubmitScoreAsync(j, stage.Id, new ScoreInput(null, "Person", hi, 9m, null))).Ok);
            Assert.True((await svc.SubmitScoreAsync(j, stage.Id, new ScoreInput(null, "Person", lo, 3m, null))).Ok);
        }

        var computed = await svc.ComputeResultsAsync(ownerId, stage.Id, false);
        Assert.True(computed.Ok);
        Assert.Equal(hi, computed.Value!.Single(r => r.Rank == 1).SubjectId);
        Assert.Equal(lo, computed.Value!.Single(r => r.Rank == 2).SubjectId);
        // A leaderboard keyed only by GUID is unreadable, so every result row carries a resolved name.
        Assert.All(computed.Value!, r => Assert.False(string.IsNullOrWhiteSpace(r.SubjectName)));

        // Public cannot see provisional, nor anything before the stage closes (OnStageClose).
        Assert.Empty((await svc.GetResultsAsync(null, stage.Id, false)).Value!);
        Assert.True((await svc.PublishResultsAsync(ownerId, stage.Id, false)).Ok);
        Assert.Empty((await svc.GetResultsAsync(null, stage.Id, false)).Value!);   // published but stage still live
        await svc.TransitionStageAsync(ownerId, stage.Id, false, "close");
        Assert.Equal(2, (await svc.GetResultsAsync(null, stage.Id, false)).Value!.Count);   // now visible
    }

    [Fact]
    public async Task Correction_is_append_only_and_advancement_seeds_the_next_stage()
    {
        var (owner, orgId, ownerId) = await LoginOrgAsync("9700011070", "Advance Org");
        var eventId = await CreateEventAsync(owner, orgId);
        var (_, judgeId) = await LoginAsync("9700011071");
        var (_, a) = await LoginAsync("9700011072");
        var (_, b) = await LoginAsync("9700011073");
        var (_, c) = await LoginAsync("9700011074");
        await SeedJudgeAsync(eventId, judgeId);

        using var scope = _factory.Services.CreateScope();
        var svc = Svc(scope);
        var quals = (await svc.CreateStageAsync(ownerId, eventId, false, Stage("Quals", rule: "TopN", threshold: 2))).Value!;
        var finals = (await svc.CreateStageAsync(ownerId, eventId, false, Stage("Finals", source: "AdvancedFrom", advancedFrom: quals.Id))).Value!;
        foreach (var s in new[] { a, b, c }) await svc.AddStageParticipantAsync(ownerId, quals.Id, false, "Person", s, null, default);
        await svc.TransitionStageAsync(ownerId, quals.Id, false, "open");
        await svc.SubmitScoreAsync(judgeId, quals.Id, new ScoreInput(null, "Person", a, 9m, null));
        await svc.SubmitScoreAsync(judgeId, quals.Id, new ScoreInput(null, "Person", b, 6m, null));
        await svc.SubmitScoreAsync(judgeId, quals.Id, new ScoreInput(null, "Person", c, 1m, null));

        Assert.Equal("results_not_published", (await svc.AdvanceStageAsync(ownerId, quals.Id, false)).Error);
        await svc.ComputeResultsAsync(ownerId, quals.Id, false);
        await svc.PublishResultsAsync(ownerId, quals.Id, false);

        // Append-only correction: bump c's rank; the prior value is preserved, state becomes Corrected.
        var cResult = (await svc.GetResultsAsync(ownerId, quals.Id, false)).Value!.Single(r => r.SubjectId == c);
        Assert.True((await svc.CorrectResultAsync(ownerId, cResult.Id, false, newRank: 1, null, "scoring error")).Ok);
        var corrected = (await svc.GetResultsAsync(ownerId, quals.Id, false)).Value!.Single(r => r.SubjectId == c);
        Assert.Equal("Corrected", corrected.State);
        Assert.Equal(1, corrected.CorrectionCount);

        // TopN=2 advances the two top-ranked into Finals.
        var advanced = await svc.AdvanceStageAsync(ownerId, quals.Id, false);
        Assert.True(advanced.Ok);
        Assert.Equal(2, advanced.Value);
        Assert.Equal(2, (await svc.ListStageParticipantsAsync(finals.Id)).Count);
    }

    // ── HTTP surface ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Stage_endpoint_works_over_http()
    {
        var (owner, orgId, _) = await LoginOrgAsync("9700011080", "Http Stage Org");
        var eventId = await CreateEventAsync(owner, orgId);

        var res = await owner.PostAsJsonAsync($"/v1/events/{eventId}/stages", new { name = "HTTP Stage" });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var stage = await Json(res);
        Assert.Equal("Draft", stage.GetProperty("state").GetString());

        var list = (await Json(await owner.GetAsync($"/v1/events/{eventId}/stages"))).EnumerateArray().ToList();
        Assert.Single(list);
        Assert.Equal(stage.GetProperty("id").GetGuid(), list[0].GetProperty("id").GetGuid());
    }

    // ── Review-fix regressions (C1 · C2 · H1 · H2) ───────────────────────────

    [Fact]   // C1 — a vote-only stage must rank by votes even with a weight cap (the cap has no judge score to bound).
    public async Task C1_vote_only_stage_ranks_by_votes_despite_a_weight_cap()
    {
        var (owner, orgId, ownerId) = await LoginOrgAsync("9700011090", "VoteCap Org");
        var eventId = await CreateEventAsync(owner, orgId);
        var (_, a) = await LoginAsync("9700011091");
        var (_, b) = await LoginAsync("9700011092");
        var (_, v1) = await LoginAsync("9700011093");
        var (_, v2) = await LoginAsync("9700011094");
        var (_, v3) = await LoginAsync("9700011095");

        using var scope = _factory.Services.CreateScope();
        var svc = Svc(scope);
        var policy = (await svc.CreateScoringPolicyAsync(ownerId, eventId, false,
            Policy("VoteOnly") with { Sources = [new("PUBLIC_VOTE", 1, null)], VoteWeightCapPercent = 40 })).Value!;   // cap < 100
        var stage = (await svc.CreateStageAsync(ownerId, eventId, false, Stage("Vote", policyId: policy.Id))).Value!;
        foreach (var s in new[] { a, b }) await svc.AddStageParticipantAsync(ownerId, stage.Id, false, "Person", s, null, default);
        await svc.TransitionStageAsync(ownerId, stage.Id, false, "open");
        Assert.True((await svc.CastVoteAsync(v1, stage.Id, "Person", a)).Ok);
        Assert.True((await svc.CastVoteAsync(v2, stage.Id, "Person", a)).Ok);
        Assert.True((await svc.CastVoteAsync(v3, stage.Id, "Person", b)).Ok);   // a:2 votes, b:1 vote

        var results = (await svc.ComputeResultsAsync(ownerId, stage.Id, false)).Value!;
        var ra = results.Single(r => r.SubjectId == a);
        var rb = results.Single(r => r.SubjectId == b);
        Assert.Equal(1, ra.Rank);
        Assert.Equal(2, rb.Rank);
        Assert.True(ra.FinalScore > rb.FinalScore);   // votes drove the ranking — the cap did NOT zero them (pre-fix: both 100, tied)
    }

    [Fact]   // C2 — §6.4: once a team has been scored, merge and split are prohibited (they would orphan recorded scores).
    public async Task C2_merge_and_split_blocked_once_a_team_is_scored()
    {
        var (owner, orgId, ownerId) = await LoginOrgAsync("9700011096", "MergeGuard Org");
        var (eventId, ttId) = await PublishCompetitionAsync(owner, orgId, min: 1, max: 4);
        var (_, capA) = await LoginAsync("9700011097");
        var (_, capB) = await LoginAsync("9700011098");
        var (_, capC) = await LoginAsync("9700011099");
        var (_, capD) = await LoginAsync("9700011100");
        var (_, judgeId) = await LoginAsync("9700011101");
        await SeedJudgeAsync(eventId, judgeId);
        var teamA = await SeedTeamAsync(eventId, ttId, "A", TeamState.Complete, memberId: capA);
        var teamB = await SeedTeamAsync(eventId, ttId, "B", TeamState.Complete, memberId: capB);
        var teamC = await SeedTeamAsync(eventId, ttId, "C", TeamState.Complete, memberId: capC);
        var teamD = await SeedTeamAsync(eventId, ttId, "D", TeamState.Complete, memberId: capD);

        using var scope = _factory.Services.CreateScope();
        var svc = Svc(scope);
        var team = scope.ServiceProvider.GetRequiredService<ITeamService>();
        var stage = (await svc.CreateStageAsync(ownerId, eventId, false, Stage("Sc"))).Value!;
        await svc.TransitionStageAsync(ownerId, stage.Id, false, "open");
        Assert.True((await svc.SubmitScoreAsync(judgeId, stage.Id, new ScoreInput(null, "Team", teamA, 8m, null))).Ok);

        Assert.Equal("merge_after_scoring_forbidden", (await team.MergeAsync(ownerId, teamA, teamB, false)).Error);
        Assert.Equal("split_after_scoring_forbidden", (await team.SplitAsync(ownerId, teamA, [capA], "Splinter", false)).Error);
        Assert.True((await team.MergeAsync(ownerId, teamC, teamD, false)).Ok);   // unscored teams still merge (guard is scoped)
    }

    [Fact]   // H1 — recomputation of identical data is byte-identical (inputs loaded in a stable, DB-order-independent order).
    public async Task H1_recomputation_is_byte_identical()
    {
        var (owner, orgId, ownerId) = await LoginOrgAsync("9700011110", "Determinism Org");
        var eventId = await CreateEventAsync(owner, orgId);
        var (_, j1) = await LoginAsync("9700011111");
        var (_, j2) = await LoginAsync("9700011112");
        var (_, j3) = await LoginAsync("9700011113");
        var (_, x) = await LoginAsync("9700011114");
        var (_, y) = await LoginAsync("9700011115");
        var (_, z) = await LoginAsync("9700011116");
        foreach (var j in new[] { j1, j2, j3 }) await SeedJudgeAsync(eventId, j);

        using var scope = _factory.Services.CreateScope();
        var svc = Svc(scope);
        var policy = (await svc.CreateScoringPolicyAsync(ownerId, eventId, false,
            Policy("Z") with { Sources = [new("JUDGE", 1, null)], Normalisation = "PerJudgeZscore" })).Value!;   // float-heavy path
        var stage = (await svc.CreateStageAsync(ownerId, eventId, false, Stage("Det", policyId: policy.Id))).Value!;
        foreach (var s in new[] { x, y, z }) await svc.AddStageParticipantAsync(ownerId, stage.Id, false, "Person", s, null, default);
        await svc.TransitionStageAsync(ownerId, stage.Id, false, "open");
        var scores = new (Guid J, Guid S, decimal V)[] { (j1, x, 9.3m), (j1, y, 7.1m), (j1, z, 4.8m), (j2, x, 8.7m), (j2, y, 8.9m), (j2, z, 2.2m), (j3, x, 6.6m), (j3, y, 9.9m), (j3, z, 5.5m) };
        foreach (var (j, s, v) in scores) Assert.True((await svc.SubmitScoreAsync(j, stage.Id, new ScoreInput(null, "Person", s, v, null))).Ok);

        var run1 = Snapshot((await svc.ComputeResultsAsync(ownerId, stage.Id, false)).Value!);
        var run2 = Snapshot((await svc.ComputeResultsAsync(ownerId, stage.Id, false)).Value!);
        var run3 = Snapshot((await svc.ComputeResultsAsync(ownerId, stage.Id, false)).Value!);
        Assert.Equal(run1, run2);
        Assert.Equal(run2, run3);   // identical rank + score + breakdown across every recompute
    }

    [Fact]   // H2 — judge scores and public votes are immutable evidence: their parent FKs are Restrict, never Cascade.
    public async Task H2_scores_and_votes_are_not_cascade_deleted()
    {
        var (owner, orgId, ownerId) = await LoginOrgAsync("9700011120", "Immutable Org");
        var eventId = await CreateEventAsync(owner, orgId);
        var (_, judgeId) = await LoginAsync("9700011121");
        var (_, subject) = await LoginAsync("9700011122");
        await SeedJudgeAsync(eventId, judgeId);

        using var scope = _factory.Services.CreateScope();
        var svc = Svc(scope);
        var stage = (await svc.CreateStageAsync(ownerId, eventId, false, Stage("Imm"))).Value!;
        await svc.TransitionStageAsync(ownerId, stage.Id, false, "open");
        Assert.True((await svc.SubmitScoreAsync(judgeId, stage.Id, new ScoreInput(null, "Person", subject, 7m, null))).Ok);

        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        // Model contract: neither evidentiary FK cascades from its parent.
        Assert.Equal(DeleteBehavior.Restrict, db.Model.FindEntityType(typeof(JudgeScore))!.GetForeignKeys().Single(f => f.PrincipalEntityType.ClrType == typeof(EventParticipant)).DeleteBehavior);
        Assert.Equal(DeleteBehavior.Restrict, db.Model.FindEntityType(typeof(PublicVote))!.GetForeignKeys().Single(f => f.PrincipalEntityType.ClrType == typeof(User)).DeleteBehavior);

        // Behaviour (fresh context so the DB constraint — not EF's in-memory cascade — is what blocks): hard-deleting
        // the judge participant while a score references it raises the FK Restrict; the score survives.
        using (var delScope = _factory.Services.CreateScope())
        {
            var db2 = delScope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var participant = await db2.EventParticipants.SingleAsync(p => p.EventId == eventId && p.SubjectId == judgeId && p.RoleSlug == "judge");
            db2.EventParticipants.Remove(participant);
            await Assert.ThrowsAsync<DbUpdateException>(() => db2.SaveChangesAsync());
        }
        using var chkScope = _factory.Services.CreateScope();
        var db3 = chkScope.ServiceProvider.GetRequiredService<KurxDbContext>();
        Assert.True(await db3.JudgeScores.AnyAsync(s => s.StageId == stage.Id && s.SubjectId == subject));
    }

    // ── helpers ─────────────────────────────────────────────────────────────

    private static string Snapshot(IReadOnlyList<ResultView> r)
        => string.Join("|", r.OrderBy(x => x.SubjectId).Select(x => $"{x.SubjectId}:{x.Rank}:{x.FinalScore}:{x.ScoreBreakdownJson}"));

    private static ScoringPolicyInput Policy(string name) => new(name, null, null, null, null, null, null, null);

    private async Task<Guid> SeedVenueAsync(Guid orgId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var venue = new Venue { OrgId = orgId, Name = "Arena " + Guid.NewGuid().ToString("N")[..6], City = "C" };
        db.Venues.Add(venue);
        await db.SaveChangesAsync();
        return venue.Id;
    }

    private async Task SeedJudgeAsync(Guid eventId, Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        db.EventParticipants.Add(new EventParticipant { EventId = eventId, SubjectType = ParticipantSubjectType.Person, SubjectId = userId, RoleSlug = "judge", State = ParticipantState.Active });
        await db.SaveChangesAsync();
    }

    private async Task<Guid> SeedTeamAsync(Guid eventId, Guid ttId, string name, TeamState state, Guid? memberId = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var team = new Team { EventId = eventId, TicketTypeId = ttId, Name = name, Slug = name.ToLowerInvariant().Replace(' ', '-') + "-" + Guid.NewGuid().ToString("N")[..4], State = state };
        db.Teams.Add(team);
        if (memberId is { } m) db.TeamMemberships.Add(new TeamMembership { TeamId = team.Id, PersonId = m, Role = TeamRole.Captain, State = TeamMembershipState.Active });
        await db.SaveChangesAsync();
        return team.Id;
    }

    private async Task<(Guid EventId, Guid TicketTypeId)> PublishCompetitionAsync(HttpClient owner, Guid orgId, int min = 2, int max = 4)
    {
        var eventId = await CreateEventAsync(owner, orgId);
        var res = await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/ticket-types", new
        {
            name = "Comp", pricePaise = 0L, pricingUnit = "PerTicket", registrationMode = "Group",
            groupMin = min, groupMax = max, quantity = 200,
            saleStarts = DateTime.UtcNow.AddDays(-1), saleEnds = DateTime.UtcNow.AddDays(30), perUserLimit = 50,
            isAllAccess = false, isCompetition = true,
        });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var ttId = (await Json(res)).GetProperty("id").GetGuid();
        await owner.PostAsJsonAsync($"/v1/orgs/{orgId}/events/{eventId}/transition", new { action = "publish" });
        return (eventId, ttId);
    }

    private async Task<Guid> CreateEventAsync(HttpClient owner, Guid orgId)
    {
        var (catId, typeId) = await TaxonAsync("hackathon");
        var res = await owner.CreateEventAsync(orgId, new
        {
            title = "Comp " + Guid.NewGuid().ToString("N")[..6], description = "x", categoryId = catId, typeId,
            venueName = "V", city = "C", startsAt = DateTime.UtcNow.AddDays(20), endsAt = DateTime.UtcNow.AddDays(20).AddHours(2),
        });
        return (await Json(res)).GetProperty("id").GetGuid();
    }

    private async Task<(HttpClient Client, Guid UserId)> LoginAsync(string phone)
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        var code = _factory.WhatsApp.LastOtpFor(phone);
        var tokens = await Json(await client.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code }));
        client.DefaultRequestHeaders.Authorization = new("Bearer", tokens.GetProperty("access_token").GetString());
        return (client, tokens.GetProperty("user_id").GetGuid());
    }

    private async Task<(HttpClient Client, Guid OrgId, Guid UserId)> LoginOrgAsync(string phone, string name)
    {
        var (client, userId) = await LoginAsync(phone);
        return (client, _factory.SeedVerifiedOrgForClient(client, name), userId);
    }

    private async Task<(Guid CatId, Guid TypeId)> TaxonAsync(string typeSlug)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var t = await db.EventCategories.Where(c => c.Slug == typeSlug).Select(c => new { c.Id, c.ParentId }).SingleAsync();
        return (t.ParentId!.Value, t.Id);
    }
}
