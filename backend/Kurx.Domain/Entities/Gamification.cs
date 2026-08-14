using System;
using Kurx.Domain.Enums;

namespace Kurx.Domain.Entities;

public class PointsLedger
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Source { get; set; } = null!;
    public int Points { get; set; }
    public string? Reason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Badge
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = null!;
    public string Type { get; set; } = null!;              // Badge type identifier
    public string Description { get; set; } = null!;
    public string? IconKey { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class UserBadge
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid BadgeId { get; set; }
    public DateTime EarnedAt { get; set; } = DateTime.UtcNow;
}

public class Leaderboard
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Scope { get; set; } = null!;             // global | city | college
    public string? ScopeId { get; set; }                    // City/College name, or null for global
    public Guid UserId { get; set; }
    public int Points { get; set; }
    public int Rank { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class ReferralReward
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ReferrerUserId { get; set; }
    public Guid RefereeUserId { get; set; }
    public ReferralRewardStatus Status { get; set; } = ReferralRewardStatus.Pending;
    public long AmountPaise { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
