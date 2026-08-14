using System.Security.Claims;
using Kurx.Application.Abstractions;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Api.Endpoints;

using Kurx.Api.ExceptionHandling;
using Kurx.Api.Validation;

public record BroadcastNotificationBody(string Title, string Message, string? DataJson);

/// <summary>Platform analytics (D-063): read-only growth aggregates over the real users/orgs/events data.
/// No revenue metrics — money doesn't move yet (Razorpay is mocked), so they'd be fabricated; those arrive
/// with the Finance module. Any-staff gated (non-PII aggregates). Read-only, so the queries live here.</summary>
public static class AdminAnalyticsEndpoints
{
    public static void MapAdminAnalyticsEndpoints(this WebApplication app)
    {
        app.MapPost("/v1/admin/notifications/broadcast", async (BroadcastNotificationBody body, INotificationService notificationSvc, KurxDbContext db, CancellationToken ct) =>
        {
            var users = await db.Users.AsNoTracking().Select(u => u.Id).ToListAsync(ct);
            foreach (var userId in users)
            {
                await notificationSvc.NotifyAsync(userId, "AdminAnnouncement", body.Title, body.Message, body.DataJson, ct);
            }
            return Results.Ok(new { ok = true, sent_to_users_count = users.Count });
        }).RequireAuthorization("KurxAdmin").WithValidation<BroadcastNotificationBody>().WithTags("admin");

        app.MapGet("/v1/admin/analytics", async (int? days, ClaimsPrincipal principal, KurxDbContext db,
            IPlatformRoleService roles, CancellationToken ct) =>
        {
            var userId = UserId(principal);
            if ((await roles.GetRolesAsync(userId, ct)).Count == 0)
                return ProblemResults.Problem("forbidden", StatusCodes.Status403Forbidden);

            var window = Math.Clamp(days ?? 30, 1, 365);
            var cutoff = DateTime.UtcNow.Date.AddDays(-(window - 1));

            var signups = await db.Users.AsNoTracking().Where(u => u.CreatedAt >= cutoff)
                .GroupBy(u => u.CreatedAt.Date)
                .Select(g => new { Day = g.Key, Count = g.Count() })
                .OrderBy(x => x.Day).ToListAsync(ct);

            var eventsByDay = await db.Events.AsNoTracking().Where(e => e.DeletedAt == null && e.CreatedAt >= cutoff)
                .GroupBy(e => e.CreatedAt.Date)
                .Select(g => new { Day = g.Key, Count = g.Count() })
                .OrderBy(x => x.Day).ToListAsync(ct);

            var byStatusRaw = await db.Events.AsNoTracking().Where(e => e.DeletedAt == null)
                .GroupBy(e => e.Status).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);

            var topOrgRaw = await db.Events.AsNoTracking()
                .Where(e => e.Status == EventStatus.Published && e.DeletedAt == null)
                .GroupBy(e => e.RepresentingOrgId).Select(g => new { OrgId = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count).Take(5).ToListAsync(ct);
            var orgIds = topOrgRaw.Select(t => t.OrgId).ToList();
            var orgNames = await db.Organizations.AsNoTracking().Where(o => orgIds.Contains(o.Id))
                .ToDictionaryAsync(o => o.Id, o => o.Name, ct);

            // V3 §16 (Phase 17): views come from the EventViews leaf-fact table, not events.ViewCount — that
            // column was only ever kept fresh by the retired nightly rollup job and would otherwise go stale.
            var topViewCounts = await db.EventViews.AsNoTracking()
                .GroupBy(v => v.EventId).Select(g => new { EventId = g.Key, Views = g.Count() })
                .OrderByDescending(x => x.Views).Take(5).ToListAsync(ct);
            var topEventIds = topViewCounts.Select(x => x.EventId).ToList();
            var topEventTitles = await db.Events.AsNoTracking().Where(e => topEventIds.Contains(e.Id) && e.DeletedAt == null)
                .ToDictionaryAsync(e => e.Id, e => e.Title, ct);
            var topEvents = topViewCounts.Where(x => topEventTitles.ContainsKey(x.EventId))
                .Select(x => new AnalyticsTopEvent(x.EventId, topEventTitles[x.EventId], x.Views));

            // Enum→string + date→string formatting happen in memory (EF can't translate them).
            return Results.Ok(new PlatformAnalytics(
                window,
                await db.Users.CountAsync(ct),
                await db.Organizations.CountAsync(o => o.DeletedAt == null, ct),
                await db.Events.CountAsync(e => e.DeletedAt == null, ct),
                signups.Sum(s => s.Count),
                signups.Select(s => new AnalyticsDayCount(s.Day.ToString("yyyy-MM-dd"), s.Count)),
                eventsByDay.Select(s => new AnalyticsDayCount(s.Day.ToString("yyyy-MM-dd"), s.Count)),
                byStatusRaw.Select(x => new AnalyticsStatusCount(x.Key.ToString().ToLowerInvariant(), x.Count)),
                topOrgRaw.Select(t => new AnalyticsTopOrganizer(
                    t.OrgId, orgNames.GetValueOrDefault(t.OrgId, "—"), t.Count)),
                topEvents));
        }).RequireAuthorization().WithTags("admin").Produces<PlatformAnalytics>();
    }

    private static Guid UserId(ClaimsPrincipal principal)
        => Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id : throw new ApiException(StatusCodes.Status401Unauthorized, "invalid_token");
}
