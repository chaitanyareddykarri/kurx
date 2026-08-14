using Kurx.Domain.Enums;

namespace Kurx.Domain.Entities;

/// <summary>A competition Stage (V3 §10.1) — its own participants (by advancement), results, schedule, venue and
/// mode, but <b>not registerable</b> (competitors arrive by advancement, spectators by admission). Attaches to the
/// existing <c>stages</c> capability. Phase 11: <c>SpectatorPoolId</c> is config-only (the spectator purchase flow is
/// deferred to the spectator-admission phase — no money-path change).</summary>
public class Stage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EventId { get; set; }
    public int Sequence { get; set; }
    public string Name { get; set; } = null!;
    public StageFormat Format { get; set; }
    public StageParticipantSource ParticipantSource { get; set; } = StageParticipantSource.AllRegistered;
    public Guid? AdvancedFromStageId { get; set; }              // when ParticipantSource = AdvancedFrom
    public AdvancementRule AdvancementRule { get; set; } = AdvancementRule.Manual;
    public int? AdvancementThreshold { get; set; }             // TOP_N n · TOP_PERCENT p · SCORE_GTE score
    public Guid? ScoringPolicyId { get; set; }
    public DateTime? StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }
    public Guid? VenueId { get; set; }
    public InventoryChannel Mode { get; set; } = InventoryChannel.InPerson;   // "own mode" — a Final may differ from Quals
    public ResultsVisibility ResultsVisibility { get; set; } = ResultsVisibility.OnStageClose;
    public Guid? SpectatorPoolId { get; set; }                 // §10.4 — config only this phase
    public StageState State { get; set; } = StageState.Draft;
    public DateTime? ClosedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>A scheduled contest within a Stage (V3 §10.2). Participants and officials are join rows (queryable for
/// conflict detection). <c>Walkover</c>/<c>Abandoned</c> are first-class so organisers never falsify results.</summary>
public class Fixture
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StageId { get; set; }
    public int RoundNo { get; set; } = 1;
    public string? Label { get; set; }
    public Guid? VenueId { get; set; }
    public DateTime? SlotStart { get; set; }
    public DateTime? SlotEnd { get; set; }
    public FixtureState State { get; set; } = FixtureState.Scheduled;
    public string? ResultJson { get; set; }                    // free-form scoreline/winner summary
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>A Stage's roster (V3 §10.1 "a Stage has its own participants, by advancement"). Populated by
/// advancement from the prior stage, by seeding/wildcard (organiser), or from all registered competitors. Fixtures
/// pair up subjects drawn from this roster.</summary>
public class StageParticipant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StageId { get; set; }
    public CompetitionSubjectType SubjectType { get; set; }
    public Guid SubjectId { get; set; }                        // personId or teamId
    public int? Seed { get; set; }
    public bool Advanced { get; set; }                         // placed by advancement (vs seeded/manual)
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>A participant in a Fixture (person or team, V3 §10.2).</summary>
public class FixtureParticipant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FixtureId { get; set; }
    public CompetitionSubjectType SubjectType { get; set; }
    public Guid SubjectId { get; set; }                        // personId or teamId
    public int? Seed { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>An official (judge/referee) assigned to a Fixture (V3 §10.2) — references an <see cref="EventParticipant"/>.</summary>
public class FixtureOfficial
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FixtureId { get; set; }
    public Guid ParticipantId { get; set; }                    // EventParticipant of the official
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>The scoring configuration for a Stage (V3 §10.3). Sources (weighted judge/public-vote/automated),
/// aggregation, normalisation, ordered explicit tie-breaks. Public-vote rules bound influence and bind identity;
/// statistical anomaly detection is a later analytics concern (the audit trail it would read is stored here).</summary>
public class ScoringPolicy
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EventId { get; set; }
    public string Name { get; set; } = null!;
    public string SourcesJson { get; set; } = "[]";            // [{ type, weight, rubricId? }]
    public ScoreAggregation Aggregation { get; set; } = ScoreAggregation.WeightedMean;
    public ScoreNormalisation Normalisation { get; set; } = ScoreNormalisation.None;
    public string? TieBreakJson { get; set; }                  // ordered tie-break keys — an unresolved tie is a defect
    public string? ConflictRulesJson { get; set; }             // §5.5
    // PublicVoteRules (§10.3) — enforceable bounds; the anomaly DETECTOR is deferred.
    public VoteIdentityBinding VoteIdentityBinding { get; set; } = VoteIdentityBinding.Account;
    public int VoteRateLimitPerHour { get; set; } = 60;
    public int VoteWeightCapPercent { get; set; } = 100;       // caps public-vote share of the final score
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>A judge's score for a subject in a Stage (V3 §10.3). One live score per (stage, judge, subject) — a
/// re-submission updates it. Eligibility + conflict-of-interest (§5.5) enforced at submission.</summary>
public class JudgeScore
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StageId { get; set; }
    public Guid? FixtureId { get; set; }
    public Guid JudgeParticipantId { get; set; }               // EventParticipant of the judge
    public CompetitionSubjectType SubjectType { get; set; }
    public Guid SubjectId { get; set; }
    public decimal Score { get; set; }
    public string? BreakdownJson { get; set; }                 // rubric breakdown
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>A public vote for a subject in a Stage (V3 §10.3). <b>Immutable</b> (append-only tally audit); exactly one
/// per (stage, voter) — the enforceable one-vote-per-identity rule (unique index). IP is never the identity.</summary>
public class PublicVote
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StageId { get; set; }
    public Guid VoterUserId { get; set; }                      // authenticated voter (identity_binding = ACCOUNT)
    public CompetitionSubjectType SubjectType { get; set; }
    public Guid SubjectId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>A Stage result (V3 §10.5). Immutable once <c>Published</c> except through a recorded correction.
/// Certificates and prerequisites read <c>Published</c> results only.</summary>
public class StageResult
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StageId { get; set; }
    public CompetitionSubjectType SubjectType { get; set; }
    public Guid SubjectId { get; set; }
    public int Rank { get; set; }
    public decimal? FinalScore { get; set; }
    public string? ScoreBreakdownJson { get; set; }
    public ResultState State { get; set; } = ResultState.Provisional;
    public DateTime? PublishedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>An append-only correction to a published result (V3 §10.5) — never a silent edit.</summary>
public class ResultCorrection
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ResultId { get; set; }
    public Guid CorrectedBy { get; set; }
    public string Reason { get; set; } = null!;
    public string? PreviousValueJson { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
