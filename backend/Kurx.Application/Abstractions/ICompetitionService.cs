namespace Kurx.Application.Abstractions;

/// <summary>The competition engine (V3 §10, Phase 11): Stage · Fixture · ScoringPolicy · Result. A Stage is NOT
/// registerable — competitors arrive by advancement, spectators by admission. Reuses Venue, EventParticipant
/// (officials/judges), Team (Phase 10), InventoryPool (spectator pool, config-only). Organiser actions reuse the
/// Phase-6 <c>event:manage</c> union; judging requires an Evaluation-class participant; voting is authenticated.
/// The scoring engine is the complete deterministic aggregation with enforceable transactional fraud controls;
/// statistical anomaly detection is a later analytics concern. Certificates/prerequisites read PUBLISHED results.</summary>
public interface ICompetitionService
{
    // ── Stages (§10.1, organiser) ─────────────────────────────────────────────
    Task<ServiceResult<StageView>> CreateStageAsync(Guid actorId, Guid eventId, bool isAdmin, StageInput input, CancellationToken ct = default);
    Task<ServiceResult<StageView>> UpdateStageAsync(Guid actorId, Guid stageId, bool isAdmin, StageInput input, CancellationToken ct = default);
    Task<ServiceResult<IReadOnlyList<StageView>>> ListStagesAsync(Guid eventId, CancellationToken ct = default);
    Task<ServiceResult<StageView>> GetStageAsync(Guid stageId, CancellationToken ct = default);
    Task<ServiceResult<StageView>> TransitionStageAsync(Guid actorId, Guid stageId, bool isAdmin, string action, CancellationToken ct = default);   // open | close
    Task<ServiceResult<bool>> DeleteStageAsync(Guid actorId, Guid stageId, bool isAdmin, CancellationToken ct = default);

    // ── Roster (§10.1) ────────────────────────────────────────────────────────
    Task<ServiceResult<bool>> AddStageParticipantAsync(Guid actorId, Guid stageId, bool isAdmin, string subjectType, Guid subjectId, int? seed, CancellationToken ct = default);
    Task<ServiceResult<bool>> RemoveStageParticipantAsync(Guid actorId, Guid participantRowId, bool isAdmin, CancellationToken ct = default);
    Task<ServiceResult<int>> SeedFromRegisteredAsync(Guid actorId, Guid stageId, bool isAdmin, CancellationToken ct = default);   // ALL_REGISTERED → the event's competition teams
    Task<IReadOnlyList<StageParticipantView>> ListStageParticipantsAsync(Guid stageId, CancellationToken ct = default);

    // ── ScoringPolicy (§10.3, organiser) ──────────────────────────────────────
    Task<ServiceResult<ScoringPolicyView>> CreateScoringPolicyAsync(Guid actorId, Guid eventId, bool isAdmin, ScoringPolicyInput input, CancellationToken ct = default);
    Task<ServiceResult<ScoringPolicyView>> UpdateScoringPolicyAsync(Guid actorId, Guid policyId, bool isAdmin, ScoringPolicyInput input, CancellationToken ct = default);
    Task<ServiceResult<IReadOnlyList<ScoringPolicyView>>> ListScoringPoliciesAsync(Guid actorId, Guid eventId, bool isAdmin, CancellationToken ct = default);

    // ── Fixtures (§10.2, organiser — manual scheduling + conflict detection) ───
    Task<ServiceResult<FixtureView>> CreateFixtureAsync(Guid actorId, Guid stageId, bool isAdmin, FixtureInput input, CancellationToken ct = default);
    Task<ServiceResult<FixtureView>> SetFixtureStateAsync(Guid actorId, Guid fixtureId, bool isAdmin, string state, string? resultJson, CancellationToken ct = default);
    Task<ServiceResult<IReadOnlyList<FixtureView>>> ListFixturesAsync(Guid stageId, CancellationToken ct = default);

    // ── Judging + voting (§10.3, enforceable transactional fraud controls) ─────
    Task<ServiceResult<bool>> SubmitScoreAsync(Guid judgeUserId, Guid stageId, ScoreInput input, CancellationToken ct = default);
    Task<ServiceResult<bool>> CastVoteAsync(Guid voterUserId, Guid stageId, string subjectType, Guid subjectId, CancellationToken ct = default);

    // ── Results + advancement (§10.5) ─────────────────────────────────────────
    Task<ServiceResult<IReadOnlyList<ResultView>>> ComputeResultsAsync(Guid actorId, Guid stageId, bool isAdmin, CancellationToken ct = default);
    Task<ServiceResult<IReadOnlyList<ResultView>>> PublishResultsAsync(Guid actorId, Guid stageId, bool isAdmin, CancellationToken ct = default);
    Task<ServiceResult<bool>> DisputeResultAsync(Guid actorId, Guid resultId, bool isAdmin, string reason, CancellationToken ct = default);
    Task<ServiceResult<bool>> CorrectResultAsync(Guid actorId, Guid resultId, bool isAdmin, int? newRank, decimal? newScore, string reason, CancellationToken ct = default);
    Task<ServiceResult<IReadOnlyList<ResultView>>> GetResultsAsync(Guid? viewerId, Guid stageId, bool isAdmin, CancellationToken ct = default);
    Task<ServiceResult<int>> AdvanceStageAsync(Guid actorId, Guid stageId, bool isAdmin, CancellationToken ct = default);
}

public record StageInput(string Name, int? Sequence, string? Format, string? ParticipantSource, Guid? AdvancedFromStageId,
    string? AdvancementRule, int? AdvancementThreshold, Guid? ScoringPolicyId, DateTime? StartsAt, DateTime? EndsAt,
    Guid? VenueId, string? Mode, string? ResultsVisibility, Guid? SpectatorPoolId);

public record StageView(Guid Id, Guid EventId, int Sequence, string Name, string Format, string ParticipantSource,
    Guid? AdvancedFromStageId, string AdvancementRule, int? AdvancementThreshold, Guid? ScoringPolicyId, DateTime? StartsAt,
    DateTime? EndsAt, Guid? VenueId, string Mode, string ResultsVisibility, Guid? SpectatorPoolId, string State, int ParticipantCount);

/// <summary>A roster row. <paramref name="SubjectName"/> is resolved server-side (person or team display
/// name) because <paramref name="SubjectId"/> alone is not renderable — and a client cannot resolve it:
/// there is no by-id person lookup on the admin surface. The id is kept for traceability and lookups.
/// Never null; falls back to the username, then "(unknown)" for a deleted subject.</summary>
public record StageParticipantView(Guid Id, Guid StageId, string SubjectType, Guid SubjectId, string SubjectName, int? Seed, bool Advanced);

public record ScoringPolicyInput(string? Name, IReadOnlyList<ScoreSourceInput>? Sources, string? Aggregation, string? Normalisation,
    IReadOnlyList<string>? TieBreak, string? VoteIdentityBinding, int? VoteRateLimitPerHour, int? VoteWeightCapPercent);

public record ScoreSourceInput(string Type, double Weight, Guid? RubricId);

public record ScoringPolicyView(Guid Id, Guid EventId, string Name, IReadOnlyList<ScoreSourceInput> Sources, string Aggregation,
    string Normalisation, IReadOnlyList<string> TieBreak, string VoteIdentityBinding, int VoteRateLimitPerHour, int VoteWeightCapPercent);

public record FixtureInput(int? RoundNo, string? Label, Guid? VenueId, DateTime? SlotStart, DateTime? SlotEnd,
    IReadOnlyList<FixtureSubjectInput>? Participants, IReadOnlyList<Guid>? OfficialParticipantIds);

public record FixtureSubjectInput(string SubjectType, Guid SubjectId, int? Seed);

/// <summary>A fixture's participant on the way OUT. Separate from <see cref="FixtureSubjectInput"/> because
/// that record is a REQUEST contract — adding a name to it would force every API caller to supply one. Carries
/// a server-resolved <paramref name="SubjectName"/> for the same reason <see cref="StageParticipantView"/> and
/// <see cref="ResultView"/> do: a bracket keyed only by GUID is not renderable.</summary>
public record FixtureSubjectView(string SubjectType, Guid SubjectId, string SubjectName, int? Seed);

public record FixtureView(Guid Id, Guid StageId, int RoundNo, string? Label, Guid? VenueId, DateTime? SlotStart, DateTime? SlotEnd,
    string State, string? ResultJson, IReadOnlyList<FixtureSubjectView> Participants, IReadOnlyList<Guid> Officials);

public record ScoreInput(Guid? FixtureId, string SubjectType, Guid SubjectId, decimal Score, string? BreakdownJson);

/// <summary>A leaderboard row. <paramref name="SubjectName"/> is resolved server-side for the same reason
/// as <see cref="StageParticipantView"/> — a rank table keyed only by GUID is not readable.</summary>
public record ResultView(Guid Id, Guid StageId, string SubjectType, Guid SubjectId, string SubjectName, int Rank, decimal? FinalScore,
    string? ScoreBreakdownJson, string State, DateTime? PublishedAt, int CorrectionCount);
