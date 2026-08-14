using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Infrastructure.Social;

/// <summary>Saved events + org follows (D-064). EF note: order by columns and map enums to strings in
/// memory — never enum.ToString() nor ORDER BY a constructor-projected property in SQL (CI-verified trap).</summary>
public class SocialService(KurxDbContext db, IStorage storage) : ISocialService
{
    public async Task<ServiceResult<bool>> SaveEventAsync(Guid userId, Guid eventId, CancellationToken ct = default)
    {
        var visible = await db.Events.AnyAsync(e => e.Id == eventId && e.Status == EventStatus.Published
            && e.Product == EventProduct.Public && e.Visibility == EventVisibility.Listed && e.DeletedAt == null, ct);
        if (!visible) return ServiceResult<bool>.Fail("not_found");
        if (await db.SavedEvents.AnyAsync(s => s.UserId == userId && s.EventId == eventId, ct))
            return ServiceResult<bool>.Success(false);
        db.SavedEvents.Add(new SavedEvent { UserId = userId, EventId = eventId });
        await db.SaveChangesAsync(ct);
        return ServiceResult<bool>.Success(true);
    }

    public async Task<bool> UnsaveEventAsync(Guid userId, Guid eventId, CancellationToken ct = default)
        => await db.SavedEvents.Where(s => s.UserId == userId && s.EventId == eventId).ExecuteDeleteAsync(ct) > 0;

    public async Task<IReadOnlyList<EventSummary>> ListSavedAsync(Guid userId, int limit, CancellationToken ct = default)
    {
        var events = await db.SavedEvents.AsNoTracking()
            .Where(s => s.UserId == userId)
            .Join(db.Events.AsNoTracking(), s => s.EventId, e => e.Id, (s, e) => new { s.CreatedAt, e })
            .Where(x => x.e.Status == EventStatus.Published && x.e.Product == EventProduct.Public && x.e.Visibility == EventVisibility.Listed && x.e.DeletedAt == null)
            .OrderByDescending(x => x.CreatedAt)
            .Take(Math.Clamp(limit, 1, 100))
            .Select(x => x.e)
            // The same card projection discovery uses (D-302) — a saved event and a searched event are the
            // same card, so they must not be two shapes. Previously this built the summary by hand and so
            // silently lacked every field added to the other one.
            .Select(Search.SearchService.SummaryProjection(db))
            .ToListAsync(ct);
        // Saved cards get the same presigned banner as searched ones (D-302).
        return await Search.SearchService.WithBannerUrlsAsync(storage, events, ct);
    }

    public async Task<ServiceResult<bool>> FollowOrgAsync(Guid userId, Guid orgId, CancellationToken ct = default)
    {
        if (!await db.Organizations.AnyAsync(o => o.Id == orgId && o.DeletedAt == null, ct))
            return ServiceResult<bool>.Fail("not_found");
        if (await db.OrganizationFollowers.AnyAsync(f => f.UserId == userId && f.OrgId == orgId, ct))
            return ServiceResult<bool>.Success(false);
        db.OrganizationFollowers.Add(new OrganizationFollower { UserId = userId, OrgId = orgId });
        await db.SaveChangesAsync(ct);
        return ServiceResult<bool>.Success(true);
    }

    public async Task<bool> UnfollowOrgAsync(Guid userId, Guid orgId, CancellationToken ct = default)
        => await db.OrganizationFollowers.Where(f => f.UserId == userId && f.OrgId == orgId).ExecuteDeleteAsync(ct) > 0;

    public async Task<IReadOnlyList<OrgFollowView>> ListFollowingAsync(Guid userId, int limit, CancellationToken ct = default)
    {
        var rows = await db.OrganizationFollowers.AsNoTracking()
            .Where(f => f.UserId == userId)
            .Join(db.Organizations.AsNoTracking(), f => f.OrgId, o => o.Id, (f, o) => new { f.CreatedAt, o })
            .Where(x => x.o.DeletedAt == null)
            .OrderByDescending(x => x.CreatedAt)
            .Take(Math.Clamp(limit, 1, 100))
            .Select(x => new { x.o.Id, x.o.Name, x.o.Slug, x.o.LogoKey })
            .ToListAsync(ct);
        return rows.Select(r => new OrgFollowView(r.Id, r.Name, r.Slug, r.LogoKey)).ToList();
    }
}
