using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Infrastructure.Gamification;

public class GamificationService(
    KurxDbContext db, IRealtimeBroadcaster broadcaster, IProfileVisibilityResolver visibility) : IGamificationService
{
    public async Task<PointsSummary> GetPointsSummaryAsync(Guid userId, CancellationToken ct = default)
    {
        var total = await db.PointsLedger.AsNoTracking()
            .Where(p => p.UserId == userId)
            .SumAsync(p => p.Points, ct);

        var history = await db.PointsLedger.AsNoTracking()
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.CreatedAt)
            .Take(50)
            .Select(p => new PointsLogView(p.Id, p.Source, p.Points, p.Reason, p.CreatedAt))
            .ToListAsync(ct);

        return new PointsSummary(total, history);
    }

    public async Task<IReadOnlyList<BadgeView>> GetUserBadgesAsync(Guid userId, CancellationToken ct = default)
    {
        return await db.UserBadges.AsNoTracking()
            .Where(ub => ub.UserId == userId)
            .Join(db.Badges, ub => ub.BadgeId, b => b.Id, (ub, b) => new BadgeView(b.Id, b.Name, b.Type, b.Description, b.IconKey, ub.EarnedAt))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<LeaderboardEntryView>> GetGlobalLeaderboardAsync(int limit = 50, CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 100);
        var rows = await db.Leaderboards.AsNoTracking()
            .Where(l => l.Scope == "global")
            .OrderBy(l => l.Rank)
            .Take(limit)
            .Join(db.Users, l => l.UserId, u => u.Id,
                (l, u) => new { l.Rank, l.UserId, u.Name, u.Username, l.Points })
            .ToListAsync(ct);

        // Whether a row links to a profile is a visibility question, so it is answered by the resolver
        // rather than by `ProfilePublic` (D-233). One batched call for the page, not one per row.
        // Anonymous viewer: a leaderboard is a public artefact and this method has no caller identity,
        // so the answer is deliberately the same for everyone — identical to the previous behaviour.
        var linkable = await visibility.VisibleProfileIdsAsync(rows.Select(r => r.UserId).ToList(), null, ct);
        return rows
            .Select(r => new LeaderboardEntryView(
                r.Rank, r.UserId, r.Name, linkable.Contains(r.UserId) ? r.Username : null, r.Points))
            .ToList();
    }

    public async Task<IReadOnlyList<LeaderboardEntryView>> GetEventLeaderboardAsync(Guid eventId, int limit = 50, CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 100);
        var entries = await db.Tickets.AsNoTracking()
            .Where(t => t.EventId == eventId && t.State == TicketState.CheckedIn && t.UserId != null)
            .GroupBy(t => t.UserId)
            .Select(g => new { UserId = g.Key!.Value, Points = g.Count() * 100 })
            .OrderByDescending(x => x.Points)
            .Take(limit)
            .ToListAsync(ct);

        // Two batched queries for the page. This previously issued one user lookup per entry — an N+1
        // that grew with `limit` (up to 100) on a leaderboard that is read far more than it is written.
        var userIds = entries.Select(e => e.UserId).ToList();
        var users = await db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Name, u.Username })
            .ToDictionaryAsync(u => u.Id, ct);
        var linkable = await visibility.VisibleProfileIdsAsync(userIds, null, ct);

        var results = new List<LeaderboardEntryView>();
        int rank = 1;
        foreach (var entry in entries)
        {
            if (!users.TryGetValue(entry.UserId, out var u)) continue;
            results.Add(new LeaderboardEntryView(
                rank++, entry.UserId, u.Name,
                linkable.Contains(entry.UserId) ? u.Username : null, entry.Points));
        }
        return results;
    }

    public async Task<IReadOnlyList<OrgLeaderboardEntryView>> GetOrgLeaderboardAsync(int limit = 50, CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 100);
        return await db.Leaderboards.AsNoTracking()
            .Where(l => l.Scope == "organization")
            .OrderBy(l => l.Rank)
            .Take(limit)
            .Join(db.Organizations, l => l.UserId, o => o.Id, (l, o) => new OrgLeaderboardEntryView(l.Rank, o.Id, o.Name, o.Slug, 0, l.Points))
            .ToListAsync(ct);
    }

    public async Task<ServiceResult<bool>> ApplyReferralCodeAsync(Guid refereeUserId, string referralCode, CancellationToken ct = default)
    {
        var referrer = await db.Users.FirstOrDefaultAsync(u => u.Username != null && u.Username.ToLower() == referralCode.ToLower(), ct);
        if (referrer is null) return ServiceResult<bool>.Fail("invalid_referral_code");
        if (referrer.Id == refereeUserId) return ServiceResult<bool>.Fail("cannot_refer_self");

        var existing = await db.ReferralRewards.AnyAsync(r => r.RefereeUserId == refereeUserId, ct);
        if (existing) return ServiceResult<bool>.Fail("referral_already_applied");

        var reward = new ReferralReward
        {
            ReferrerUserId = referrer.Id,
            RefereeUserId = refereeUserId,
            Status = ReferralRewardStatus.Granted,
            AmountPaise = 5000 // ₹50 cashback
        };
        db.ReferralRewards.Add(reward);
        await db.SaveChangesAsync(ct);

        await AwardPointsAsync(referrer.Id, "Referral Signup", 150, $"Referred username @{referralCode}", ct);
        await AwardPointsAsync(refereeUserId, "Referral Signup", 50, "Signed up with referral code", ct);

        return ServiceResult<bool>.Success(true);
    }

    public async Task<IReadOnlyList<ReferralView>> GetReferralsAsync(Guid userId, CancellationToken ct = default)
    {
        return await db.ReferralRewards.AsNoTracking()
            .Where(r => r.ReferrerUserId == userId)
            .Join(db.Users, r => r.RefereeUserId, u => u.Id, (r, u) => new ReferralView(r.Id, u.Name, u.PhoneE164 ?? u.Phone, r.Status.ToString(), r.AmountPaise, r.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task AwardPointsAsync(Guid userId, string source, int points, string? reason = null, CancellationToken ct = default)
    {
        db.PointsLedger.Add(new PointsLedger
        {
            UserId = userId,
            Source = source,
            Points = points,
            Reason = reason
        });
        await db.SaveChangesAsync(ct);

        await EvaluateBadgesAsync(userId, ct);

        var summary = await GetPointsSummaryAsync(userId, ct);
        await broadcaster.BroadcastBadgeUnlockAsync(userId, new { points = summary.TotalPoints });
    }

    public async Task EvaluateBadgesAsync(Guid userId, CancellationToken ct = default)
    {
        var badgeTypes = new[]
        {
            new { Type = "First Event", Name = "First Event Badge", Desc = "Attended your first event on Kurx" },
            new { Type = "Top Organizer", Name = "Top Organizer Badge", Desc = "Hosted more than 5 events on Kurx" },
            new { Type = "Verified Organizer", Name = "Verified Organizer Badge", Desc = "Completed organization KYC verification" }
        };

        foreach (var bt in badgeTypes)
        {
            var b = await db.Badges.FirstOrDefaultAsync(x => x.Type == bt.Type, ct);
            if (b is null)
            {
                b = new Badge { Type = bt.Type, Name = bt.Name, Description = bt.Desc };
                db.Badges.Add(b);
                await db.SaveChangesAsync(ct);
            }
        }

        var hasCheckin = await db.Tickets.AnyAsync(t => t.UserId == userId && t.State == TicketState.CheckedIn, ct);
        if (hasCheckin)
        {
            var b = await db.Badges.FirstAsync(x => x.Type == "First Event", ct);
            if (!await db.UserBadges.AnyAsync(ub => ub.UserId == userId && ub.BadgeId == b.Id, ct))
            {
                db.UserBadges.Add(new UserBadge { UserId = userId, BadgeId = b.Id });
                await db.SaveChangesAsync(ct);
                await broadcaster.BroadcastBadgeUnlockAsync(userId, new { badge = b.Name, desc = b.Description });
            }
        }

        var hostedCount = await db.Events.CountAsync(e => e.CreatedBy == userId && e.Status == EventStatus.Published, ct);
        if (hostedCount >= 5)
        {
            var b = await db.Badges.FirstAsync(x => x.Type == "Top Organizer", ct);
            if (!await db.UserBadges.AnyAsync(ub => ub.UserId == userId && ub.BadgeId == b.Id, ct))
            {
                db.UserBadges.Add(new UserBadge { UserId = userId, BadgeId = b.Id });
                await db.SaveChangesAsync(ct);
                await broadcaster.BroadcastBadgeUnlockAsync(userId, new { badge = b.Name, desc = b.Description });
            }
        }
    }

    public async Task RefreshLeaderboardsAsync(CancellationToken ct = default)
    {
        // Use simpler truncation logic compatible with SQLite in test environment
        var isSqlite = db.Database.ProviderName?.Contains("Sqlite") == true;
        if (isSqlite)
        {
            await db.Database.ExecuteSqlRawAsync("DELETE FROM leaderboards;", ct);
        }
        else
        {
            await db.Database.ExecuteSqlRawAsync("TRUNCATE TABLE leaderboards;", ct);
        }

        var userScores = await db.PointsLedger.AsNoTracking()
            .GroupBy(p => p.UserId)
            .Select(g => new { UserId = g.Key, Points = g.Sum(x => x.Points) })
            .OrderByDescending(x => x.Points)
            .Take(100)
            .ToListAsync(ct);

        int rank = 1;
        foreach (var us in userScores)
        {
            db.Leaderboards.Add(new Leaderboard
            {
                Scope = "global",
                UserId = us.UserId,
                Points = us.Points,
                Rank = rank++
            });
        }

        // D-368 — rank real organizations only. Grouping published events by `RepresentingOrgId` with no
        // scope filter put self-representation rows on a PUBLIC leaderboard
        // (`GET /v1/gamification/leaderboards/organizations`), named after the person who created the
        // event, which is the concept D-268 says does not exist. Filtered at the writer rather than the
        // reader because a personal row must not consume one of the 100 ranked slots either; the rebuild
        // TRUNCATEs first, so no stale row survives a refresh.
        var realOrgIds = db.Organizations.Where(OrganizationScope.Real).Select(o => o.Id);
        var orgScores = await db.Events.AsNoTracking()
            .Where(e => e.Status == EventStatus.Published && realOrgIds.Contains(e.RepresentingOrgId))
            .GroupBy(e => e.RepresentingOrgId)
            .Select(g => new { OrgId = g.Key, Points = g.Count() * 500 })
            .OrderByDescending(x => x.Points)
            .Take(100)
            .ToListAsync(ct);

        rank = 1;
        foreach (var os in orgScores)
        {
            db.Leaderboards.Add(new Leaderboard
            {
                Scope = "organization",
                UserId = os.OrgId,
                Points = os.Points,
                Rank = rank++
            });
        }

        await db.SaveChangesAsync(ct);
    }
}
