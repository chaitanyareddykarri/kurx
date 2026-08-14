using Kurx.Application.Abstractions;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Api.Endpoints;

/// <summary>Admin audit-log viewer (D-062). Read-only over the <c>audit_log</c> the rest of the platform
/// already writes (logins, event lifecycle, moderation, user suspensions, …). Gated by the Audit policy
/// (SuperAdmin + ReadOnlyAuditor). Read-only, so the query lives here rather than in a service.</summary>
public static class AdminAuditEndpoints
{
    public static void MapAdminAuditEndpoints(this WebApplication app)
    {
        app.MapGet("/v1/admin/audit", async (string? actor, string? entity, Guid? entityId, string? action, int? limit,
            KurxDbContext db, CancellationToken ct) =>
        {
            var take = Math.Clamp(limit ?? 100, 1, 200);
            var query = db.AuditLogs.AsNoTracking();
            if (Guid.TryParse(actor, out var actorId)) query = query.Where(a => a.ActorId == actorId);
            if (!string.IsNullOrWhiteSpace(entity)) query = query.Where(a => a.Entity == entity.Trim());
            // D-191: a real per-entity history query — the event Timeline tab used to fetch every
            // `events`-entity row and filter client-side.
            if (entityId is not null) query = query.Where(a => a.EntityId == entityId);
            if (!string.IsNullOrWhiteSpace(action)) query = query.Where(a => EF.Functions.ILike(a.Action, $"%{action.Trim()}%"));

            var rows = await query.OrderByDescending(a => a.CreatedAt).Take(take)
                .Select(a => new AuditLogEntry(
                    a.Id, a.ActorType, a.ActorId, a.Action, a.Entity, a.EntityId, a.DetailsJson, a.CreatedAt))
                .ToListAsync(ct);
            return Results.Ok(rows);
        }).RequireAuthorization("Audit").WithTags("admin").Produces<IReadOnlyList<AuditLogEntry>>();
    }
}
