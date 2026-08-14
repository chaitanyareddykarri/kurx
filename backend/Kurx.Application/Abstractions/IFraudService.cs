namespace Kurx.Application.Abstractions;

public record BlacklistEntryView(Guid Id, string Kind, string Value, string? Reason, DateTime CreatedAt);

/// <summary>Fraud prevention (M13, D-052): hard blocklist + a risk score aggregated from fraud signals.
/// The trust layer (M7) reads <see cref="IsUserClearAsync"/> so a blacklisted or high-risk user loses
/// paid-organizing / payout capability, which flows into the event-publish (M8) and payment (M10) gates.</summary>
public interface IFraudService
{
    Task<bool> IsBlacklistedAsync(string kind, string value, CancellationToken ct = default);

    Task<ServiceResult<BlacklistEntryView>> AddBlacklistAsync(string kind, string value, string? reason, Guid createdBy, CancellationToken ct = default);
    Task<IReadOnlyList<BlacklistEntryView>> ListBlacklistAsync(CancellationToken ct = default);
    Task<bool> RemoveBlacklistAsync(Guid id, CancellationToken ct = default);

    Task<ServiceResult<int>> RecordSignalAsync(string subjectType, Guid subjectId, string kind, string? value, int score, CancellationToken ct = default);
    Task<int> GetRiskScoreAsync(string subjectType, Guid subjectId, CancellationToken ct = default);

    /// <summary>Batched form of <see cref="GetRiskScoreAsync"/> (D-186) — one grouped SUM query for a whole
    /// page of subjects (the admin event list's "Fraud Alerts" column), not N. Subjects with no signals are
    /// omitted (score 0), same as a single lookup returning 0.</summary>
    Task<IReadOnlyDictionary<Guid, int>> GetRiskScoresBatchAsync(string subjectType, IReadOnlyList<Guid> subjectIds, CancellationToken ct = default);

    /// <summary>The fraud-clear gate M7 reads: the user's phone/email are not blacklisted and their
    /// aggregate risk score is below the high-risk threshold. Read live.</summary>
    Task<bool> IsUserClearAsync(Guid userId, CancellationToken ct = default);
}
