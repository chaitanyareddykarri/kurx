using System.Text.Json.Serialization;

namespace Kurx.Application.Abstractions;

// Wire names declared on the record — see DeviceView in INotificationService.cs for why (D-259 addendum).
// Only multi-word names need an attribute; a single word is identical in camelCase and snake_case.
public record PointsSummary(
    [property: JsonPropertyName("total_points")] int TotalPoints,
    IReadOnlyList<PointsLogView> History);

public record PointsLogView(
    Guid Id, string Source, int Points, string? Reason,
    [property: JsonPropertyName("created_at")] DateTime CreatedAt);
public record BadgeView(Guid Id, string Name, string Type, string Description, string? IconKey, DateTime EarnedAt);
public record LeaderboardEntryView(int Rank, Guid UserId, string Name, string? Username, int Points);
public record OrgLeaderboardEntryView(int Rank, Guid OrgId, string Name, string Slug, int EventCount, int Points);
public record ReferralView(Guid Id, string RefereeName, string RefereePhone, string Status, long RewardPaise, DateTime CreatedAt);

public interface IGamificationService
{
    Task<PointsSummary> GetPointsSummaryAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<BadgeView>> GetUserBadgesAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<LeaderboardEntryView>> GetGlobalLeaderboardAsync(int limit = 50, CancellationToken ct = default);
    Task<IReadOnlyList<LeaderboardEntryView>> GetEventLeaderboardAsync(Guid eventId, int limit = 50, CancellationToken ct = default);
    Task<IReadOnlyList<OrgLeaderboardEntryView>> GetOrgLeaderboardAsync(int limit = 50, CancellationToken ct = default);
    
    Task<ServiceResult<bool>> ApplyReferralCodeAsync(Guid refereeUserId, string referralCode, CancellationToken ct = default);
    Task<IReadOnlyList<ReferralView>> GetReferralsAsync(Guid userId, CancellationToken ct = default);
    
    Task AwardPointsAsync(Guid userId, string source, int points, string? reason = null, CancellationToken ct = default);
    Task EvaluateBadgesAsync(Guid userId, CancellationToken ct = default);
    Task RefreshLeaderboardsAsync(CancellationToken ct = default);
}
