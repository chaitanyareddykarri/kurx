using System.Text.Json;
using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Infrastructure.Events;

/// <summary>The competition engine (V3 §10, Phase 11): Stage · Fixture · ScoringPolicy · Result. Additive over the
/// approved foundations — no money-path, InventoryPool, Registration→Admission, Pass, VAR or §17.1 change. Organiser
/// actions reuse the Phase-6 <c>event:manage</c> union; judging requires an Evaluation-class participant; voting is
/// authenticated with enforceable transactional fraud controls. The scoring engine is the complete deterministic
/// aggregation; statistical anomaly detection is a later analytics concern. Certificates read PUBLISHED results.</summary>
public class CompetitionService(KurxDbContext db, IEventPermissionService permissions) : ICompetitionService
{
    private static readonly TeamState[] SeedableTeamStates = [TeamState.Complete, TeamState.Locked, TeamState.Competing];
    private static readonly ParticipantState[] ActiveParticipantStates = [ParticipantState.Accepted, ParticipantState.Active];
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    // ── Stages (§10.1) ─────────────────────────────────────────────────────────
    public async Task<ServiceResult<StageView>> CreateStageAsync(Guid actorId, Guid eventId, bool isAdmin, StageInput input, CancellationToken ct = default)
    {
        if (!await IsOrganiserAsync(actorId, eventId, isAdmin, ct)) return ServiceResult<StageView>.Fail("forbidden");
        if (string.IsNullOrWhiteSpace(input.Name)) return ServiceResult<StageView>.Fail("name_required");
        if (!TryEnum(input.Format, out StageFormat format, StageFormat.SingleSubmission)) return ServiceResult<StageView>.Fail("invalid_format");
        if (!TryEnum(input.ParticipantSource, out StageParticipantSource source, StageParticipantSource.AllRegistered)) return ServiceResult<StageView>.Fail("invalid_participant_source");
        if (!TryEnum(input.AdvancementRule, out AdvancementRule rule, AdvancementRule.Manual)) return ServiceResult<StageView>.Fail("invalid_advancement_rule");
        if (!TryEnum(input.Mode, out InventoryChannel mode, InventoryChannel.InPerson)) return ServiceResult<StageView>.Fail("invalid_mode");
        if (!TryEnum(input.ResultsVisibility, out ResultsVisibility visibility, ResultsVisibility.OnStageClose)) return ServiceResult<StageView>.Fail("invalid_results_visibility");

        var validation = await ValidateStageRefsAsync(eventId, source, input.AdvancedFromStageId, input.ScoringPolicyId, input.VenueId, input.SpectatorPoolId, ct);
        if (validation is not null) return ServiceResult<StageView>.Fail(validation);

        var seq = input.Sequence ?? (await db.Stages.Where(s => s.EventId == eventId).MaxAsync(s => (int?)s.Sequence, ct) ?? 0) + 1;
        if (await db.Stages.AnyAsync(s => s.EventId == eventId && s.Sequence == seq, ct)) return ServiceResult<StageView>.Fail("sequence_taken");

        var stage = new Stage
        {
            EventId = eventId, Sequence = seq, Name = input.Name.Trim(), Format = format, ParticipantSource = source,
            AdvancedFromStageId = input.AdvancedFromStageId, AdvancementRule = rule, AdvancementThreshold = input.AdvancementThreshold,
            ScoringPolicyId = input.ScoringPolicyId,
            // Same Kind=Unspecified-vs-timestamptz issue as EventService.CreateAsync — see that fix's comment.
            StartsAt = input.StartsAt is { } sa ? DateTime.SpecifyKind(sa, DateTimeKind.Utc) : null,
            EndsAt = input.EndsAt is { } ea ? DateTime.SpecifyKind(ea, DateTimeKind.Utc) : null,
            VenueId = input.VenueId,
            Mode = mode, ResultsVisibility = visibility, SpectatorPoolId = input.SpectatorPoolId,
        };
        db.Stages.Add(stage);
        Audit(actorId, isAdmin ? "admin" : "user", "stage.create", stage.Id, JsonSerializer.Serialize(new { eventId, stage.Name, stage.Sequence }));
        await db.SaveChangesAsync(ct);
        return ServiceResult<StageView>.Success(await ToStageViewAsync(stage, ct));
    }

    public async Task<ServiceResult<StageView>> UpdateStageAsync(Guid actorId, Guid stageId, bool isAdmin, StageInput input, CancellationToken ct = default)
    {
        var stage = await db.Stages.FirstOrDefaultAsync(s => s.Id == stageId, ct);
        if (stage is null) return ServiceResult<StageView>.Fail("not_found");
        if (!await IsOrganiserAsync(actorId, stage.EventId, isAdmin, ct)) return ServiceResult<StageView>.Fail("forbidden");
        if (stage.State == StageState.Closed) return ServiceResult<StageView>.Fail("stage_closed");

        if (!TryEnum(input.Format, out StageFormat format, stage.Format)) return ServiceResult<StageView>.Fail("invalid_format");
        if (!TryEnum(input.ParticipantSource, out StageParticipantSource source, stage.ParticipantSource)) return ServiceResult<StageView>.Fail("invalid_participant_source");
        if (!TryEnum(input.AdvancementRule, out AdvancementRule rule, stage.AdvancementRule)) return ServiceResult<StageView>.Fail("invalid_advancement_rule");
        if (!TryEnum(input.Mode, out InventoryChannel mode, stage.Mode)) return ServiceResult<StageView>.Fail("invalid_mode");
        if (!TryEnum(input.ResultsVisibility, out ResultsVisibility visibility, stage.ResultsVisibility)) return ServiceResult<StageView>.Fail("invalid_results_visibility");

        var advancedFrom = input.AdvancedFromStageId ?? stage.AdvancedFromStageId;
        var scoringPolicy = input.ScoringPolicyId ?? stage.ScoringPolicyId;
        var venue = input.VenueId ?? stage.VenueId;
        var spectatorPool = input.SpectatorPoolId ?? stage.SpectatorPoolId;
        var validation = await ValidateStageRefsAsync(stage.EventId, source, advancedFrom, scoringPolicy, venue, spectatorPool, ct, stage.Id);
        if (validation is not null) return ServiceResult<StageView>.Fail(validation);

        if (input.Sequence is { } newSeq && newSeq != stage.Sequence)
        {
            if (await db.Stages.AnyAsync(s => s.EventId == stage.EventId && s.Sequence == newSeq && s.Id != stage.Id, ct)) return ServiceResult<StageView>.Fail("sequence_taken");
            stage.Sequence = newSeq;
        }
        if (!string.IsNullOrWhiteSpace(input.Name)) stage.Name = input.Name.Trim();
        stage.Format = format; stage.ParticipantSource = source; stage.AdvancedFromStageId = advancedFrom;
        stage.AdvancementRule = rule; stage.AdvancementThreshold = input.AdvancementThreshold ?? stage.AdvancementThreshold;
        stage.ScoringPolicyId = scoringPolicy;
        stage.StartsAt = input.StartsAt is { } stSa ? DateTime.SpecifyKind(stSa, DateTimeKind.Utc) : stage.StartsAt;
        stage.EndsAt = input.EndsAt is { } stEa ? DateTime.SpecifyKind(stEa, DateTimeKind.Utc) : stage.EndsAt;
        stage.VenueId = venue; stage.Mode = mode; stage.ResultsVisibility = visibility; stage.SpectatorPoolId = spectatorPool;
        stage.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return ServiceResult<StageView>.Success(await ToStageViewAsync(stage, ct));
    }

    public async Task<ServiceResult<IReadOnlyList<StageView>>> ListStagesAsync(Guid eventId, CancellationToken ct = default)
    {
        var stages = await db.Stages.AsNoTracking().Where(s => s.EventId == eventId).OrderBy(s => s.Sequence).ToListAsync(ct);
        var counts = await db.StageParticipants.AsNoTracking().Where(p => stages.Select(s => s.Id).Contains(p.StageId))
            .GroupBy(p => p.StageId).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N, ct);
        return ServiceResult<IReadOnlyList<StageView>>.Success(stages.Select(s => ToStageView(s, counts.GetValueOrDefault(s.Id))).ToList());
    }

    public async Task<ServiceResult<StageView>> GetStageAsync(Guid stageId, CancellationToken ct = default)
    {
        var stage = await db.Stages.AsNoTracking().FirstOrDefaultAsync(s => s.Id == stageId, ct);
        return stage is null ? ServiceResult<StageView>.Fail("not_found") : ServiceResult<StageView>.Success(await ToStageViewAsync(stage, ct));
    }

    public async Task<ServiceResult<StageView>> TransitionStageAsync(Guid actorId, Guid stageId, bool isAdmin, string action, CancellationToken ct = default)
    {
        var stage = await db.Stages.FirstOrDefaultAsync(s => s.Id == stageId, ct);
        if (stage is null) return ServiceResult<StageView>.Fail("not_found");
        if (!await IsOrganiserAsync(actorId, stage.EventId, isAdmin, ct)) return ServiceResult<StageView>.Fail("forbidden");

        var to = action?.Trim().ToLowerInvariant() switch
        {
            "open" when stage.State == StageState.Draft => StageState.Live,
            "close" when stage.State == StageState.Live => StageState.Closed,
            _ => (StageState?)null,
        };
        if (to is null) return ServiceResult<StageView>.Fail("invalid_transition");
        stage.State = to.Value;
        if (to == StageState.Closed) stage.ClosedAt = DateTime.UtcNow;
        stage.UpdatedAt = DateTime.UtcNow;
        Audit(actorId, isAdmin ? "admin" : "user", $"stage.{action!.ToLowerInvariant()}", stage.Id, null);
        await db.SaveChangesAsync(ct);
        return ServiceResult<StageView>.Success(await ToStageViewAsync(stage, ct));
    }

    public async Task<ServiceResult<bool>> DeleteStageAsync(Guid actorId, Guid stageId, bool isAdmin, CancellationToken ct = default)
    {
        var stage = await db.Stages.FirstOrDefaultAsync(s => s.Id == stageId, ct);
        if (stage is null) return ServiceResult<bool>.Fail("not_found");
        if (!await IsOrganiserAsync(actorId, stage.EventId, isAdmin, ct)) return ServiceResult<bool>.Fail("forbidden");
        if (await db.StageResults.AnyAsync(r => r.StageId == stageId && r.State != ResultState.Provisional, ct)) return ServiceResult<bool>.Fail("has_published_results");
        if (await db.Stages.AnyAsync(s => s.AdvancedFromStageId == stageId, ct)) return ServiceResult<bool>.Fail("is_advancement_source");
        db.Stages.Remove(stage);   // FK cascade drops fixtures/roster/scores/votes/results
        Audit(actorId, isAdmin ? "admin" : "user", "stage.delete", stageId, null);
        await db.SaveChangesAsync(ct);
        return ServiceResult<bool>.Success(true);
    }

    // ── Roster (§10.1) ─────────────────────────────────────────────────────────
    public async Task<ServiceResult<bool>> AddStageParticipantAsync(Guid actorId, Guid stageId, bool isAdmin, string subjectType, Guid subjectId, int? seed, CancellationToken ct = default)
    {
        var stage = await db.Stages.FirstOrDefaultAsync(s => s.Id == stageId, ct);
        if (stage is null) return ServiceResult<bool>.Fail("not_found");
        if (!await IsOrganiserAsync(actorId, stage.EventId, isAdmin, ct)) return ServiceResult<bool>.Fail("forbidden");
        if (!TryEnum(subjectType, out CompetitionSubjectType type, CompetitionSubjectType.Person, allowDefaultOnEmpty: false)) return ServiceResult<bool>.Fail("invalid_subject_type");
        var subjectError = await ValidateSubjectAsync(stage.EventId, type, subjectId, ct);
        if (subjectError is not null) return ServiceResult<bool>.Fail(subjectError);
        if (await db.StageParticipants.AnyAsync(p => p.StageId == stageId && p.SubjectType == type && p.SubjectId == subjectId, ct)) return ServiceResult<bool>.Fail("already_on_stage");

        db.StageParticipants.Add(new StageParticipant { StageId = stageId, SubjectType = type, SubjectId = subjectId, Seed = seed });
        await db.SaveChangesAsync(ct);
        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<bool>> RemoveStageParticipantAsync(Guid actorId, Guid participantRowId, bool isAdmin, CancellationToken ct = default)
    {
        var row = await db.StageParticipants.FirstOrDefaultAsync(p => p.Id == participantRowId, ct);
        if (row is null) return ServiceResult<bool>.Fail("not_found");
        var stage = await db.Stages.FirstOrDefaultAsync(s => s.Id == row.StageId, ct);
        if (stage is null || !await IsOrganiserAsync(actorId, stage.EventId, isAdmin, ct)) return ServiceResult<bool>.Fail("forbidden");
        db.StageParticipants.Remove(row);
        await db.SaveChangesAsync(ct);
        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<int>> SeedFromRegisteredAsync(Guid actorId, Guid stageId, bool isAdmin, CancellationToken ct = default)
    {
        var stage = await db.Stages.FirstOrDefaultAsync(s => s.Id == stageId, ct);
        if (stage is null) return ServiceResult<int>.Fail("not_found");
        if (!await IsOrganiserAsync(actorId, stage.EventId, isAdmin, ct)) return ServiceResult<int>.Fail("forbidden");
        if (stage.ParticipantSource != StageParticipantSource.AllRegistered) return ServiceResult<int>.Fail("not_all_registered");

        var teams = await db.Teams.AsNoTracking().Where(t => t.EventId == stage.EventId && SeedableTeamStates.Contains(t.State)).Select(t => t.Id).ToListAsync(ct);
        var existing = await db.StageParticipants.Where(p => p.StageId == stageId && p.SubjectType == CompetitionSubjectType.Team).Select(p => p.SubjectId).ToListAsync(ct);
        var added = 0;
        foreach (var teamId in teams.Except(existing))
        {
            db.StageParticipants.Add(new StageParticipant { StageId = stageId, SubjectType = CompetitionSubjectType.Team, SubjectId = teamId });
            added++;
        }
        if (added > 0) await db.SaveChangesAsync(ct);
        return ServiceResult<int>.Success(added);
    }

    public async Task<IReadOnlyList<StageParticipantView>> ListStageParticipantsAsync(Guid stageId, CancellationToken ct = default)
    {
        var rows = await db.StageParticipants.AsNoTracking().Where(p => p.StageId == stageId)
            .OrderBy(p => p.Seed).ThenBy(p => p.CreatedAt).ToListAsync(ct);
        var names = await ResolveSubjectNamesAsync(rows.Select(r => (r.SubjectType, r.SubjectId)), ct);
        return rows.Select(p => new StageParticipantView(p.Id, p.StageId, p.SubjectType.ToString(), p.SubjectId,
            SubjectName(names, p.SubjectType, p.SubjectId), p.Seed, p.Advanced)).ToList();
    }

    /// <summary>Display names for competition subjects in <b>two</b> queries regardless of roster size — one for
    /// people, one for teams — so a large stage never degrades into an N+1. There is deliberately no OrgUnit
    /// branch: <see cref="CompetitionSubjectType"/> has exactly two members, so one would be unreachable code
    /// (OrgUnit is a member of the *event-participant* enum, which is a different type).</summary>
    private async Task<Dictionary<(CompetitionSubjectType, Guid), string>> ResolveSubjectNamesAsync(
        IEnumerable<(CompetitionSubjectType Type, Guid Id)> subjects, CancellationToken ct)
    {
        var map = new Dictionary<(CompetitionSubjectType, Guid), string>();
        var list = subjects as IReadOnlyCollection<(CompetitionSubjectType Type, Guid Id)> ?? subjects.ToList();
        if (list.Count == 0) return map;

        var personIds = list.Where(s => s.Type == CompetitionSubjectType.Person).Select(s => s.Id).Distinct().ToList();
        if (personIds.Count > 0)
            foreach (var u in await db.Users.AsNoTracking().Where(u => personIds.Contains(u.Id))
                         .Select(u => new { u.Id, u.Name, u.Username }).ToListAsync(ct))
                // Name is blank until onboarding completes, so the handle is the next-best identifier.
                map[(CompetitionSubjectType.Person, u.Id)] =
                    !string.IsNullOrWhiteSpace(u.Name) ? u.Name
                    : !string.IsNullOrWhiteSpace(u.Username) ? "@" + u.Username
                    : UnknownSubject;

        var teamIds = list.Where(s => s.Type == CompetitionSubjectType.Team).Select(s => s.Id).Distinct().ToList();
        if (teamIds.Count > 0)
            foreach (var t in await db.Teams.AsNoTracking().Where(t => teamIds.Contains(t.Id))
                         .Select(t => new { t.Id, t.Name }).ToListAsync(ct))
                map[(CompetitionSubjectType.Team, t.Id)] = string.IsNullOrWhiteSpace(t.Name) ? UnknownSubject : t.Name;

        return map;
    }

    /// <summary>Never null: a subject row can be missing (deleted person/team) while its result stays for the
    /// audit trail, and a leaderboard still has to render that row.</summary>
    private const string UnknownSubject = "(unknown)";

    private static string SubjectName(IReadOnlyDictionary<(CompetitionSubjectType, Guid), string> names,
        CompetitionSubjectType type, Guid id)
        => names.TryGetValue((type, id), out var n) ? n : UnknownSubject;

    // ── ScoringPolicy (§10.3) ──────────────────────────────────────────────────
    public async Task<ServiceResult<ScoringPolicyView>> CreateScoringPolicyAsync(Guid actorId, Guid eventId, bool isAdmin, ScoringPolicyInput input, CancellationToken ct = default)
    {
        if (!await IsOrganiserAsync(actorId, eventId, isAdmin, ct)) return ServiceResult<ScoringPolicyView>.Fail("forbidden");
        if (string.IsNullOrWhiteSpace(input.Name)) return ServiceResult<ScoringPolicyView>.Fail("name_required");
        var policy = new ScoringPolicy { EventId = eventId, Name = input.Name!.Trim() };
        var error = ApplyPolicyInput(policy, input);
        if (error is not null) return ServiceResult<ScoringPolicyView>.Fail(error);
        db.ScoringPolicies.Add(policy);
        await db.SaveChangesAsync(ct);
        return ServiceResult<ScoringPolicyView>.Success(ToPolicyView(policy));
    }

    public async Task<ServiceResult<ScoringPolicyView>> UpdateScoringPolicyAsync(Guid actorId, Guid policyId, bool isAdmin, ScoringPolicyInput input, CancellationToken ct = default)
    {
        var policy = await db.ScoringPolicies.FirstOrDefaultAsync(p => p.Id == policyId, ct);
        if (policy is null) return ServiceResult<ScoringPolicyView>.Fail("not_found");
        if (!await IsOrganiserAsync(actorId, policy.EventId, isAdmin, ct)) return ServiceResult<ScoringPolicyView>.Fail("forbidden");
        if (!string.IsNullOrWhiteSpace(input.Name)) policy.Name = input.Name!.Trim();
        var error = ApplyPolicyInput(policy, input);
        if (error is not null) return ServiceResult<ScoringPolicyView>.Fail(error);
        policy.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return ServiceResult<ScoringPolicyView>.Success(ToPolicyView(policy));
    }

    public async Task<ServiceResult<IReadOnlyList<ScoringPolicyView>>> ListScoringPoliciesAsync(Guid actorId, Guid eventId, bool isAdmin, CancellationToken ct = default)
    {
        if (!await IsOrganiserAsync(actorId, eventId, isAdmin, ct)) return ServiceResult<IReadOnlyList<ScoringPolicyView>>.Fail("forbidden");
        var list = await db.ScoringPolicies.AsNoTracking().Where(p => p.EventId == eventId).OrderBy(p => p.CreatedAt).ToListAsync(ct);
        return ServiceResult<IReadOnlyList<ScoringPolicyView>>.Success(list.Select(ToPolicyView).ToList());
    }

    // ── Fixtures (§10.2) ───────────────────────────────────────────────────────
    public async Task<ServiceResult<FixtureView>> CreateFixtureAsync(Guid actorId, Guid stageId, bool isAdmin, FixtureInput input, CancellationToken ct = default)
    {
        var stage = await db.Stages.FirstOrDefaultAsync(s => s.Id == stageId, ct);
        if (stage is null) return ServiceResult<FixtureView>.Fail("not_found");
        if (!await IsOrganiserAsync(actorId, stage.EventId, isAdmin, ct)) return ServiceResult<FixtureView>.Fail("forbidden");
        if (input.SlotStart is { } ss && input.SlotEnd is { } se && se <= ss) return ServiceResult<FixtureView>.Fail("invalid_slot");
        if (input.VenueId is { } vId && !await db.Venues.AnyAsync(v => v.Id == vId, ct)) return ServiceResult<FixtureView>.Fail("venue_not_found");

        var participants = input.Participants ?? [];
        var officials = (input.OfficialParticipantIds ?? []).Distinct().ToList();
        foreach (var p in participants)
            if (!TryEnum(p.SubjectType, out CompetitionSubjectType _, CompetitionSubjectType.Person, allowDefaultOnEmpty: false)) return ServiceResult<FixtureView>.Fail("invalid_subject_type");
        if (officials.Count > 0)
        {
            var validOfficials = await db.EventParticipants.CountAsync(ep => ep.EventId == stage.EventId && officials.Contains(ep.Id), ct);
            if (validOfficials != officials.Count) return ServiceResult<FixtureView>.Fail("official_not_found");
        }

        // Manual scheduling with conflict detection (§10.2) — double-booked venue/official/participant in an overlapping slot.
        var conflict = await DetectFixtureConflictAsync(stage.EventId, null, input.VenueId, input.SlotStart, input.SlotEnd,
            participants.Select(p => (Enum.Parse<CompetitionSubjectType>(p.SubjectType, true), p.SubjectId)).ToList(), officials, ct);
        if (conflict is not null) return ServiceResult<FixtureView>.Fail(conflict);

        var fixture = new Fixture
        {
            StageId = stageId, RoundNo = input.RoundNo ?? 1, Label = input.Label?.Trim(),
            VenueId = input.VenueId, SlotStart = input.SlotStart, SlotEnd = input.SlotEnd,
        };
        db.Fixtures.Add(fixture);
        foreach (var p in participants)
            db.FixtureParticipants.Add(new FixtureParticipant { FixtureId = fixture.Id, SubjectType = Enum.Parse<CompetitionSubjectType>(p.SubjectType, true), SubjectId = p.SubjectId, Seed = p.Seed });
        foreach (var o in officials)
            db.FixtureOfficials.Add(new FixtureOfficial { FixtureId = fixture.Id, ParticipantId = o });
        Audit(actorId, isAdmin ? "admin" : "user", "fixture.create", fixture.Id, JsonSerializer.Serialize(new { stageId, fixture.RoundNo }));
        await db.SaveChangesAsync(ct);
        return ServiceResult<FixtureView>.Success(await ToFixtureViewAsync(fixture, ct));
    }

    public async Task<ServiceResult<FixtureView>> SetFixtureStateAsync(Guid actorId, Guid fixtureId, bool isAdmin, string state, string? resultJson, CancellationToken ct = default)
    {
        var fixture = await db.Fixtures.FirstOrDefaultAsync(f => f.Id == fixtureId, ct);
        if (fixture is null) return ServiceResult<FixtureView>.Fail("not_found");
        var stage = await db.Stages.FirstOrDefaultAsync(s => s.Id == fixture.StageId, ct);
        if (stage is null || !await IsOrganiserAsync(actorId, stage.EventId, isAdmin, ct)) return ServiceResult<FixtureView>.Fail("forbidden");
        if (!TryEnum(state, out FixtureState to, fixture.State, allowDefaultOnEmpty: false)) return ServiceResult<FixtureView>.Fail("invalid_state");
        if (resultJson is not null && !IsValidJson(resultJson)) return ServiceResult<FixtureView>.Fail("invalid_result_json");
        fixture.State = to;
        if (resultJson is not null) fixture.ResultJson = resultJson;
        fixture.UpdatedAt = DateTime.UtcNow;
        Audit(actorId, isAdmin ? "admin" : "user", "fixture.state", fixture.Id, JsonSerializer.Serialize(new { state = to.ToString() }));
        await db.SaveChangesAsync(ct);
        return ServiceResult<FixtureView>.Success(await ToFixtureViewAsync(fixture, ct));
    }

    public async Task<ServiceResult<IReadOnlyList<FixtureView>>> ListFixturesAsync(Guid stageId, CancellationToken ct = default)
    {
        var fixtures = await db.Fixtures.AsNoTracking().Where(f => f.StageId == stageId).OrderBy(f => f.RoundNo).ThenBy(f => f.SlotStart).ToListAsync(ct);
        var ids = fixtures.Select(f => f.Id).ToList();
        var parts = await db.FixtureParticipants.AsNoTracking().Where(p => ids.Contains(p.FixtureId)).ToListAsync(ct);
        var offs = await db.FixtureOfficials.AsNoTracking().Where(o => ids.Contains(o.FixtureId)).ToListAsync(ct);
        // One grouped resolve for every fixture's participants, not one per fixture — no N+1.
        var names = await ResolveSubjectNamesAsync(parts.Select(p => (p.SubjectType, p.SubjectId)), ct);
        var views = fixtures.Select(f => new FixtureView(f.Id, f.StageId, f.RoundNo, f.Label, f.VenueId, f.SlotStart, f.SlotEnd, f.State.ToString(), f.ResultJson,
            parts.Where(p => p.FixtureId == f.Id).Select(p => new FixtureSubjectView(p.SubjectType.ToString(), p.SubjectId,
                SubjectName(names, p.SubjectType, p.SubjectId), p.Seed)).ToList(),
            offs.Where(o => o.FixtureId == f.Id).Select(o => o.ParticipantId).ToList())).ToList();
        return ServiceResult<IReadOnlyList<FixtureView>>.Success(views);
    }

    // ── Judging (§10.3) ────────────────────────────────────────────────────────
    public async Task<ServiceResult<bool>> SubmitScoreAsync(Guid judgeUserId, Guid stageId, ScoreInput input, CancellationToken ct = default)
    {
        var stage = await db.Stages.FirstOrDefaultAsync(s => s.Id == stageId, ct);
        if (stage is null) return ServiceResult<bool>.Fail("not_found");
        if (stage.State != StageState.Live) return ServiceResult<bool>.Fail("stage_not_live");   // scoring window
        if (!TryEnum(input.SubjectType, out CompetitionSubjectType type, CompetitionSubjectType.Person, allowDefaultOnEmpty: false)) return ServiceResult<bool>.Fail("invalid_subject_type");

        // Eligibility: an Evaluation-class participant of this event (or an assigned official on the given fixture).
        var judge = await GetEvaluationParticipantAsync(stage.EventId, judgeUserId, ct);
        if (judge is null) return ServiceResult<bool>.Fail("not_a_judge");
        if (input.FixtureId is { } fid)
        {
            var fixture = await db.Fixtures.FirstOrDefaultAsync(f => f.Id == fid && f.StageId == stageId, ct);
            if (fixture is null) return ServiceResult<bool>.Fail("fixture_not_found");
            var hasOfficials = await db.FixtureOfficials.AnyAsync(o => o.FixtureId == fid, ct);
            if (hasOfficials && !await db.FixtureOfficials.AnyAsync(o => o.FixtureId == fid && o.ParticipantId == judge.Id, ct)) return ServiceResult<bool>.Fail("not_assigned_to_fixture");
        }
        // Conflict of interest (§5.5): a judge may not score their own team or themselves.
        if (await IsConflictedAsync(judgeUserId, type, input.SubjectId, ct)) return ServiceResult<bool>.Fail("conflict_of_interest");

        // Duplicate prevention: one live score per (stage, judge, subject) — a re-submission updates it.
        var existing = await db.JudgeScores.FirstOrDefaultAsync(js => js.StageId == stageId && js.JudgeParticipantId == judge.Id && js.SubjectType == type && js.SubjectId == input.SubjectId, ct);
        if (existing is null)
            db.JudgeScores.Add(new JudgeScore { StageId = stageId, FixtureId = input.FixtureId, JudgeParticipantId = judge.Id, SubjectType = type, SubjectId = input.SubjectId, Score = input.Score, BreakdownJson = input.BreakdownJson });
        else { existing.Score = input.Score; existing.BreakdownJson = input.BreakdownJson; existing.FixtureId = input.FixtureId; existing.UpdatedAt = DateTime.UtcNow; }
        await db.SaveChangesAsync(ct);
        return ServiceResult<bool>.Success(true);
    }

    // ── Voting (§10.3, enforceable transactional fraud controls) ───────────────
    public async Task<ServiceResult<bool>> CastVoteAsync(Guid voterUserId, Guid stageId, string subjectType, Guid subjectId, CancellationToken ct = default)
    {
        var stage = await db.Stages.FirstOrDefaultAsync(s => s.Id == stageId, ct);
        if (stage is null) return ServiceResult<bool>.Fail("not_found");
        if (stage.State != StageState.Live) return ServiceResult<bool>.Fail("voting_closed");   // voting window
        if (!TryEnum(subjectType, out CompetitionSubjectType type, CompetitionSubjectType.Person, allowDefaultOnEmpty: false)) return ServiceResult<bool>.Fail("invalid_subject_type");

        var policy = stage.ScoringPolicyId is { } pid ? await db.ScoringPolicies.AsNoTracking().FirstOrDefaultAsync(p => p.Id == pid, ct) : null;
        var identityBinding = policy?.VoteIdentityBinding ?? VoteIdentityBinding.Account;
        var ratePerHour = policy?.VoteRateLimitPerHour ?? 60;

        // Identity binding (§10.3): ACCOUNT is any authenticated account (phone-first ⇒ inherently a verified phone);
        // VERIFIED_CONTACT raises the bar to a proven email — a stricter gate against throwaway accounts.
        if (identityBinding == VoteIdentityBinding.VerifiedContact)
        {
            var verified = await db.Users.AnyAsync(u => u.Id == voterUserId && u.EmailVerifiedAt != null, ct);
            if (!verified) return ServiceResult<bool>.Fail("unverified_contact");
        }
        // Eligibility: the subject must be a real competitor on this stage.
        if (!await db.StageParticipants.AnyAsync(p => p.StageId == stageId && p.SubjectType == type && p.SubjectId == subjectId, ct)) return ServiceResult<bool>.Fail("invalid_subject");
        // Replay/rate limit: bound votes-per-identity per hour across the event's stages.
        var since = DateTime.UtcNow.AddHours(-1);
        if (await db.PublicVotes.CountAsync(v => v.VoterUserId == voterUserId && v.CreatedAt >= since, ct) >= ratePerHour) return ServiceResult<bool>.Fail("rate_limited");
        // One vote per identity per stage (immutable) — pre-check plus the unique index as the race guard.
        if (await db.PublicVotes.AnyAsync(v => v.StageId == stageId && v.VoterUserId == voterUserId, ct)) return ServiceResult<bool>.Fail("already_voted");

        db.PublicVotes.Add(new PublicVote { StageId = stageId, VoterUserId = voterUserId, SubjectType = type, SubjectId = subjectId });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { return ServiceResult<bool>.Fail("already_voted"); }   // lost the one-per-identity race
        return ServiceResult<bool>.Success(true);
    }

    // ── Results (§10.5) ────────────────────────────────────────────────────────
    public async Task<ServiceResult<IReadOnlyList<ResultView>>> ComputeResultsAsync(Guid actorId, Guid stageId, bool isAdmin, CancellationToken ct = default)
    {
        var stage = await db.Stages.FirstOrDefaultAsync(s => s.Id == stageId, ct);
        if (stage is null) return ServiceResult<IReadOnlyList<ResultView>>.Fail("not_found");
        if (!await IsOrganiserAsync(actorId, stage.EventId, isAdmin, ct)) return ServiceResult<IReadOnlyList<ResultView>>.Fail("forbidden");
        if (await db.StageResults.AnyAsync(r => r.StageId == stageId && r.State != ResultState.Provisional, ct)) return ServiceResult<IReadOnlyList<ResultView>>.Fail("already_published");

        var policy = stage.ScoringPolicyId is { } pid ? await db.ScoringPolicies.AsNoTracking().FirstOrDefaultAsync(p => p.Id == pid, ct) : null;
        // Load every aggregation input in a stable, DB-order-independent order (H1) — the scoring engine sums floats,
        // so identical input rows must be enumerated identically for recomputation to be byte-identical.
        var roster = await db.StageParticipants.AsNoTracking().Where(p => p.StageId == stageId).OrderBy(p => p.Id).ToListAsync(ct);
        var scores = await db.JudgeScores.AsNoTracking().Where(s => s.StageId == stageId).OrderBy(s => s.Id).ToListAsync(ct);
        var votes = await db.PublicVotes.AsNoTracking().Where(v => v.StageId == stageId).OrderBy(v => v.Id).ToListAsync(ct);
        var ranked = ScoreAggregator.Compute(policy, roster, scores, votes);
        if (ranked.Count == 0) return ServiceResult<IReadOnlyList<ResultView>>.Fail("nothing_to_score");

        var stale = await db.StageResults.Where(r => r.StageId == stageId).ToListAsync(ct);   // all Provisional (guarded above)
        db.StageResults.RemoveRange(stale);
        foreach (var r in ranked)
            db.StageResults.Add(new StageResult { StageId = stageId, SubjectType = r.Type, SubjectId = r.Id, Rank = r.Rank, FinalScore = r.Final, ScoreBreakdownJson = r.Breakdown });
        Audit(actorId, isAdmin ? "admin" : "user", "results.compute", stageId, JsonSerializer.Serialize(new { subjects = ranked.Count }));
        await db.SaveChangesAsync(ct);
        return await GetResultsAsync(actorId, stageId, isAdmin, ct);
    }

    public async Task<ServiceResult<IReadOnlyList<ResultView>>> PublishResultsAsync(Guid actorId, Guid stageId, bool isAdmin, CancellationToken ct = default)
    {
        var stage = await db.Stages.FirstOrDefaultAsync(s => s.Id == stageId, ct);
        if (stage is null) return ServiceResult<IReadOnlyList<ResultView>>.Fail("not_found");
        if (!await IsOrganiserAsync(actorId, stage.EventId, isAdmin, ct)) return ServiceResult<IReadOnlyList<ResultView>>.Fail("forbidden");
        var results = await db.StageResults.Where(r => r.StageId == stageId && r.State == ResultState.Provisional).ToListAsync(ct);
        if (results.Count == 0) return ServiceResult<IReadOnlyList<ResultView>>.Fail("nothing_to_publish");
        var now = DateTime.UtcNow;
        foreach (var r in results) { r.State = ResultState.Published; r.PublishedAt = now; r.UpdatedAt = now; }
        Audit(actorId, isAdmin ? "admin" : "user", "results.publish", stageId, JsonSerializer.Serialize(new { published = results.Count }));
        await db.SaveChangesAsync(ct);
        return await GetResultsAsync(actorId, stageId, isAdmin, ct);
    }

    public async Task<ServiceResult<bool>> DisputeResultAsync(Guid actorId, Guid resultId, bool isAdmin, string reason, CancellationToken ct = default)
    {
        var result = await db.StageResults.FirstOrDefaultAsync(r => r.Id == resultId, ct);
        if (result is null) return ServiceResult<bool>.Fail("not_found");
        var stage = await db.Stages.FirstOrDefaultAsync(s => s.Id == result.StageId, ct);
        if (stage is null || !await IsOrganiserAsync(actorId, stage.EventId, isAdmin, ct)) return ServiceResult<bool>.Fail("forbidden");
        if (result.State is not (ResultState.Published or ResultState.Corrected)) return ServiceResult<bool>.Fail("not_published");
        if (string.IsNullOrWhiteSpace(reason)) return ServiceResult<bool>.Fail("reason_required");
        result.State = ResultState.Disputed; result.UpdatedAt = DateTime.UtcNow;
        db.ResultCorrections.Add(new ResultCorrection { ResultId = resultId, CorrectedBy = actorId, Reason = reason.Trim(), PreviousValueJson = SnapshotJson(result) });
        Audit(actorId, isAdmin ? "admin" : "user", "results.dispute", resultId, null);
        await db.SaveChangesAsync(ct);
        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<bool>> CorrectResultAsync(Guid actorId, Guid resultId, bool isAdmin, int? newRank, decimal? newScore, string reason, CancellationToken ct = default)
    {
        var result = await db.StageResults.FirstOrDefaultAsync(r => r.Id == resultId, ct);
        if (result is null) return ServiceResult<bool>.Fail("not_found");
        var stage = await db.Stages.FirstOrDefaultAsync(s => s.Id == result.StageId, ct);
        if (stage is null || !await IsOrganiserAsync(actorId, stage.EventId, isAdmin, ct)) return ServiceResult<bool>.Fail("forbidden");
        if (result.State is not (ResultState.Published or ResultState.Disputed or ResultState.Corrected)) return ServiceResult<bool>.Fail("not_published");
        if (string.IsNullOrWhiteSpace(reason)) return ServiceResult<bool>.Fail("reason_required");
        if (newRank is null && newScore is null) return ServiceResult<bool>.Fail("nothing_to_correct");

        // Append-only: snapshot the prior value BEFORE mutating, never a silent edit (§10.5).
        db.ResultCorrections.Add(new ResultCorrection { ResultId = resultId, CorrectedBy = actorId, Reason = reason.Trim(), PreviousValueJson = SnapshotJson(result) });
        if (newRank is { } rk) result.Rank = rk;
        if (newScore is { } sc) result.FinalScore = sc;
        result.State = ResultState.Corrected; result.UpdatedAt = DateTime.UtcNow;
        Audit(actorId, isAdmin ? "admin" : "user", "results.correct", resultId, JsonSerializer.Serialize(new { newRank, newScore }));
        await db.SaveChangesAsync(ct);
        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<IReadOnlyList<ResultView>>> GetResultsAsync(Guid? viewerId, Guid stageId, bool isAdmin, CancellationToken ct = default)
    {
        var stage = await db.Stages.AsNoTracking().FirstOrDefaultAsync(s => s.Id == stageId, ct);
        if (stage is null) return ServiceResult<IReadOnlyList<ResultView>>.Fail("not_found");
        var isOrganiser = isAdmin || (viewerId is { } vid && await permissions.HasAsync(vid, stage.EventId, "event:manage", ct));

        var query = db.StageResults.AsNoTracking().Where(r => r.StageId == stageId);
        if (!isOrganiser)
        {
            if (!await IsPublicResultsVisibleAsync(stage, ct)) return ServiceResult<IReadOnlyList<ResultView>>.Success([]);
            query = query.Where(r => r.State == ResultState.Published || r.State == ResultState.Corrected);   // public never sees provisional/disputed
        }
        var results = await query.OrderBy(r => r.Rank).ToListAsync(ct);
        var corrections = await db.ResultCorrections.AsNoTracking().Where(c => results.Select(r => r.Id).Contains(c.ResultId))
            .GroupBy(c => c.ResultId).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N, ct);
        var names = await ResolveSubjectNamesAsync(results.Select(r => (r.SubjectType, r.SubjectId)), ct);
        var views = results.Select(r => new ResultView(r.Id, r.StageId, r.SubjectType.ToString(), r.SubjectId,
            SubjectName(names, r.SubjectType, r.SubjectId), r.Rank, r.FinalScore, r.ScoreBreakdownJson, r.State.ToString(), r.PublishedAt, corrections.GetValueOrDefault(r.Id))).ToList();
        return ServiceResult<IReadOnlyList<ResultView>>.Success(views);
    }

    // ── Advancement (§10.5) ────────────────────────────────────────────────────
    public async Task<ServiceResult<int>> AdvanceStageAsync(Guid actorId, Guid stageId, bool isAdmin, CancellationToken ct = default)
    {
        var stage = await db.Stages.FirstOrDefaultAsync(s => s.Id == stageId, ct);
        if (stage is null) return ServiceResult<int>.Fail("not_found");
        if (!await IsOrganiserAsync(actorId, stage.EventId, isAdmin, ct)) return ServiceResult<int>.Fail("forbidden");
        if (stage.AdvancementRule == AdvancementRule.Manual) return ServiceResult<int>.Fail("manual_advancement");

        var next = await db.Stages.FirstOrDefaultAsync(s => s.AdvancedFromStageId == stageId, ct);
        if (next is null) return ServiceResult<int>.Fail("no_next_stage");

        // Advance from the authoritative published set only (§10.5).
        var published = await db.StageResults.AsNoTracking().Where(r => r.StageId == stageId && (r.State == ResultState.Published || r.State == ResultState.Corrected)).OrderBy(r => r.Rank).ToListAsync(ct);
        if (published.Count == 0) return ServiceResult<int>.Fail("results_not_published");

        var advancers = stage.AdvancementRule switch
        {
            AdvancementRule.TopN => published.Take(Math.Max(0, stage.AdvancementThreshold ?? 0)),
            AdvancementRule.TopPercent => published.Take((int)Math.Ceiling(published.Count * Math.Clamp(stage.AdvancementThreshold ?? 0, 0, 100) / 100.0)),
            AdvancementRule.ScoreGte => published.Where(r => r.FinalScore >= (stage.AdvancementThreshold ?? 0)),
            _ => [],
        };
        var existing = await db.StageParticipants.Where(p => p.StageId == next.Id).Select(p => new { p.SubjectType, p.SubjectId }).ToListAsync(ct);
        var added = 0;
        foreach (var r in advancers)
        {
            if (existing.Any(e => e.SubjectType == r.SubjectType && e.SubjectId == r.SubjectId)) continue;
            db.StageParticipants.Add(new StageParticipant { StageId = next.Id, SubjectType = r.SubjectType, SubjectId = r.SubjectId, Seed = r.Rank, Advanced = true });
            added++;
        }
        Audit(actorId, isAdmin ? "admin" : "user", "stage.advance", stageId, JsonSerializer.Serialize(new { nextStageId = next.Id, advanced = added }));
        if (added > 0) await db.SaveChangesAsync(ct);
        return ServiceResult<int>.Success(added);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────
    private async Task<bool> IsOrganiserAsync(Guid actorId, Guid eventId, bool isAdmin, CancellationToken ct) => isAdmin || await permissions.HasAsync(actorId, eventId, "event:manage", ct);

    private async Task<string?> ValidateStageRefsAsync(Guid eventId, StageParticipantSource source, Guid? advancedFrom, Guid? scoringPolicyId, Guid? venueId, Guid? spectatorPoolId, CancellationToken ct, Guid? selfId = null)
    {
        if (source == StageParticipantSource.AdvancedFrom)
        {
            if (advancedFrom is null) return "advanced_from_required";
            if (!await db.Stages.AnyAsync(s => s.Id == advancedFrom && s.EventId == eventId && s.Id != selfId, ct)) return "advanced_from_not_found";
        }
        if (scoringPolicyId is { } spid && !await db.ScoringPolicies.AnyAsync(p => p.Id == spid && p.EventId == eventId, ct)) return "scoring_policy_not_found";
        if (venueId is { } vid && !await db.Venues.AnyAsync(v => v.Id == vid, ct)) return "venue_not_found";
        if (spectatorPoolId is { } poolId && !await db.InventoryPools.AnyAsync(p => p.Id == poolId && p.EventId == eventId, ct)) return "spectator_pool_not_found";
        return null;
    }

    private async Task<string?> ValidateSubjectAsync(Guid eventId, CompetitionSubjectType type, Guid subjectId, CancellationToken ct)
    {
        if (type == CompetitionSubjectType.Team)
            return await db.Teams.AnyAsync(t => t.Id == subjectId && t.EventId == eventId, ct) ? null : "team_not_found";
        return await db.Users.AnyAsync(u => u.Id == subjectId, ct) ? null : "person_not_found";
    }

    /// <summary>The judge's Evaluation-class <see cref="EventParticipant"/> for this event, or null if not a judge.</summary>
    private async Task<EventParticipant?> GetEvaluationParticipantAsync(Guid eventId, Guid userId, CancellationToken ct)
    {
        var participants = await db.EventParticipants.AsNoTracking()
            .Where(ep => ep.EventId == eventId && ep.SubjectType == ParticipantSubjectType.Person && ep.SubjectId == userId && ActiveParticipantStates.Contains(ep.State))
            .ToListAsync(ct);
        if (participants.Count == 0) return null;
        var evalSlugs = await db.ParticipantRoles.AsNoTracking().Where(r => r.Class == ParticipantClass.Evaluation).Select(r => r.Slug).ToListAsync(ct);
        return participants.FirstOrDefault(ep => evalSlugs.Contains(ep.RoleSlug));
    }

    private async Task<bool> IsConflictedAsync(Guid judgeUserId, CompetitionSubjectType type, Guid subjectId, CancellationToken ct)
    {
        if (type == CompetitionSubjectType.Person) return subjectId == judgeUserId;   // no self-scoring
        return await db.TeamMemberships.AnyAsync(m => m.TeamId == subjectId && m.PersonId == judgeUserId && m.State == TeamMembershipState.Active, ct);   // no scoring own team
    }

    /// <summary>Returns a conflict code if the proposed fixture double-books a venue, official or participant with any
    /// existing fixture in the same event whose time slot overlaps (§10.2). Slot-less fixtures never conflict on time.</summary>
    private async Task<string?> DetectFixtureConflictAsync(Guid eventId, Guid? excludeFixtureId, Guid? venueId, DateTime? slotStart, DateTime? slotEnd,
        List<(CompetitionSubjectType Type, Guid Id)> participants, List<Guid> officials, CancellationToken ct)
    {
        if (slotStart is not { } start || slotEnd is not { } end) return null;   // no slot ⇒ manual, unschedulable overlap
        var stageIds = await db.Stages.Where(s => s.EventId == eventId).Select(s => s.Id).ToListAsync(ct);
        var overlapping = await db.Fixtures.AsNoTracking()
            .Where(f => stageIds.Contains(f.StageId) && f.Id != excludeFixtureId && f.SlotStart != null && f.SlotEnd != null && f.SlotStart < end && start < f.SlotEnd)
            .Select(f => f.Id).ToListAsync(ct);
        if (overlapping.Count == 0) return null;

        if (venueId is { } vid && await db.Fixtures.AnyAsync(f => overlapping.Contains(f.Id) && f.VenueId == vid, ct)) return "venue_double_booked";
        if (officials.Count > 0 && await db.FixtureOfficials.AnyAsync(o => overlapping.Contains(o.FixtureId) && officials.Contains(o.ParticipantId), ct)) return "official_double_booked";
        if (participants.Count > 0)
        {
            var busy = await db.FixtureParticipants.AsNoTracking().Where(p => overlapping.Contains(p.FixtureId)).Select(p => new { p.SubjectType, p.SubjectId }).ToListAsync(ct);
            if (participants.Any(p => busy.Any(b => b.SubjectType == p.Type && b.SubjectId == p.Id))) return "participant_double_booked";
        }
        return null;
    }

    private async Task<bool> IsPublicResultsVisibleAsync(Stage stage, CancellationToken ct) => stage.ResultsVisibility switch
    {
        ResultsVisibility.Live => true,
        ResultsVisibility.OnStageClose => stage.State == StageState.Closed,
        ResultsVisibility.OnEventClose => await db.Events.AnyAsync(e => e.Id == stage.EventId && e.EndsAt < DateTime.UtcNow, ct),
        _ => false,
    };

    private string? ApplyPolicyInput(ScoringPolicy policy, ScoringPolicyInput input)
    {
        if (input.Sources is { } sources)
        {
            foreach (var s in sources)
            {
                if (!Enum.TryParse<ScoreSourceType>(s.Type?.Replace("_", "").Replace("-", ""), true, out _)) return "invalid_source_type";
                if (s.Weight < 0) return "invalid_source_weight";
            }
            policy.SourcesJson = JsonSerializer.Serialize(sources.Select(s => new { type = s.Type.ToUpperInvariant(), weight = s.Weight, rubricId = s.RubricId }));
        }
        if (input.Aggregation is not null)
        {
            if (!TryEnum(input.Aggregation, out ScoreAggregation agg, policy.Aggregation, allowDefaultOnEmpty: false)) return "invalid_aggregation";
            policy.Aggregation = agg;
        }
        if (input.Normalisation is not null)
        {
            if (!TryEnum(input.Normalisation, out ScoreNormalisation norm, policy.Normalisation, allowDefaultOnEmpty: false)) return "invalid_normalisation";
            policy.Normalisation = norm;
        }
        if (input.TieBreak is { } tb) policy.TieBreakJson = JsonSerializer.Serialize(tb);
        if (input.VoteIdentityBinding is not null)
        {
            if (!TryEnum(input.VoteIdentityBinding, out VoteIdentityBinding binding, policy.VoteIdentityBinding, allowDefaultOnEmpty: false)) return "invalid_identity_binding";
            policy.VoteIdentityBinding = binding;
        }
        if (input.VoteRateLimitPerHour is { } rl) { if (rl < 1) return "invalid_rate_limit"; policy.VoteRateLimitPerHour = rl; }
        if (input.VoteWeightCapPercent is { } cap) { if (cap is < 0 or > 100) return "invalid_weight_cap"; policy.VoteWeightCapPercent = cap; }
        return null;
    }

    private static string SnapshotJson(StageResult r) => JsonSerializer.Serialize(new { r.Rank, r.FinalScore, state = r.State.ToString() });

    private static bool IsValidJson(string s) { try { using var _ = JsonDocument.Parse(s); return true; } catch { return false; } }

    private void Audit(Guid actorId, string actorType, string action, Guid entityId, string? detailsJson)
        => db.AuditLogs.Add(new AuditLog { ActorType = actorType, ActorId = actorId, Action = action, Entity = "competition", EntityId = entityId, DetailsJson = detailsJson });

    private async Task<StageView> ToStageViewAsync(Stage s, CancellationToken ct)
        => ToStageView(s, await db.StageParticipants.CountAsync(p => p.StageId == s.Id, ct));

    private static StageView ToStageView(Stage s, int participantCount) => new(s.Id, s.EventId, s.Sequence, s.Name, s.Format.ToString(),
        s.ParticipantSource.ToString(), s.AdvancedFromStageId, s.AdvancementRule.ToString(), s.AdvancementThreshold, s.ScoringPolicyId,
        s.StartsAt, s.EndsAt, s.VenueId, s.Mode.ToString(), s.ResultsVisibility.ToString(), s.SpectatorPoolId, s.State.ToString(), participantCount);

    private async Task<FixtureView> ToFixtureViewAsync(Fixture f, CancellationToken ct)
    {
        var parts = await db.FixtureParticipants.AsNoTracking().Where(p => p.FixtureId == f.Id).ToListAsync(ct);
        var offs = await db.FixtureOfficials.AsNoTracking().Where(o => o.FixtureId == f.Id).Select(o => o.ParticipantId).ToListAsync(ct);
        var names = await ResolveSubjectNamesAsync(parts.Select(p => (p.SubjectType, p.SubjectId)), ct);
        return new FixtureView(f.Id, f.StageId, f.RoundNo, f.Label, f.VenueId, f.SlotStart, f.SlotEnd, f.State.ToString(), f.ResultJson,
            parts.Select(p => new FixtureSubjectView(p.SubjectType.ToString(), p.SubjectId,
                SubjectName(names, p.SubjectType, p.SubjectId), p.Seed)).ToList(), offs);
    }

    private static ScoringPolicyView ToPolicyView(ScoringPolicy p)
    {
        var sources = JsonSerializer.Deserialize<List<ScoreSourceInput>>(p.SourcesJson, Json) ?? [];
        var tieBreak = p.TieBreakJson is null ? [] : JsonSerializer.Deserialize<List<string>>(p.TieBreakJson) ?? new List<string>();
        return new ScoringPolicyView(p.Id, p.EventId, p.Name, sources, p.Aggregation.ToString(), p.Normalisation.ToString(), tieBreak,
            p.VoteIdentityBinding.ToString(), p.VoteRateLimitPerHour, p.VoteWeightCapPercent);
    }

    private static bool TryEnum<T>(string? s, out T value, T current, bool allowDefaultOnEmpty = true) where T : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(s)) { value = current; return allowDefaultOnEmpty; }
        return Enum.TryParse(s.Replace("_", "").Replace("-", ""), true, out value);
    }
}
