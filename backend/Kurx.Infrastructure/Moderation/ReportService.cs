using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Infrastructure.Moderation;

/// <summary>Content moderation reports (C-5, D-059). Create is any-user; triage is Moderation-staff-only
/// (gated at the endpoint). Resolving a report appends an <c>audit_log</c> row (who / when / action).</summary>
public class ReportService(KurxDbContext db) : IReportService
{
    // The subjects a report may target (matches the Report entity's documented EntityType values).
    // "post" / "post_comment" were added with the Posts module (D-262) rather than giving posts their
    // own report table — one moderation queue is the whole point of a polymorphic subject.
    private static readonly HashSet<string> EntityTypes =
        new(StringComparer.OrdinalIgnoreCase) { "event", "review", "chat_message", "user", "org", "post", "post_comment" };

    public async Task<ServiceResult<ReportView>> CreateAsync(Guid reporterId, string entityType, Guid entityId,
        string reason, string? details, CancellationToken ct = default)
    {
        entityType = (entityType ?? "").Trim().ToLowerInvariant();
        if (!EntityTypes.Contains(entityType)) return ServiceResult<ReportView>.Fail("invalid_entity_type");
        if (string.IsNullOrWhiteSpace(reason)) return ServiceResult<ReportView>.Fail("reason_required");

        // One open report per (reporter, entity) — keeps a single user from flooding the queue.
        var dup = await db.Reports.AnyAsync(r => r.ReporterId == reporterId && r.EntityType == entityType
            && r.EntityId == entityId && r.Status == "open", ct);
        if (dup) return ServiceResult<ReportView>.Fail("already_reported");

        var report = new Report
        {
            ReporterId = reporterId, EntityType = entityType, EntityId = entityId,
            Reason = reason.Trim(), Details = details, Status = "open",
        };
        db.Reports.Add(report);
        await db.SaveChangesAsync(ct);
        return ServiceResult<ReportView>.Success(ToView(report));
    }

    public async Task<IReadOnlyList<ReportView>> ListAsync(string? status, int limit, CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 200);
        var q = db.Reports.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(status))
        {
            var s = status.Trim().ToLowerInvariant();
            q = q.Where(r => r.Status == s);
        }
        return await q.OrderByDescending(r => r.CreatedAt).Take(limit)
            .Select(r => new ReportView(r.Id, r.ReporterId, r.EntityType, r.EntityId, r.Reason, r.Details,
                r.Status, r.ResolvedBy, r.ResolvedAt, r.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<ServiceResult<ReportView>> ResolveAsync(Guid reviewerId, Guid reportId, bool dismiss, CancellationToken ct = default)
    {
        var report = await db.Reports.FirstOrDefaultAsync(r => r.Id == reportId, ct);
        if (report is null) return ServiceResult<ReportView>.Fail("not_found");
        if (report.Status is "resolved" or "dismissed") return ServiceResult<ReportView>.Fail("already_closed");

        report.Status = dismiss ? "dismissed" : "resolved";
        report.ResolvedBy = reviewerId;
        report.ResolvedAt = DateTime.UtcNow;
        db.AuditLogs.Add(new AuditLog
        {
            ActorType = "admin", ActorId = reviewerId,
            Action = dismiss ? "report.dismiss" : "report.resolve",
            Entity = "reports", EntityId = report.Id,
        });
        await db.SaveChangesAsync(ct);
        return ServiceResult<ReportView>.Success(ToView(report));
    }

    private static ReportView ToView(Report r) => new(r.Id, r.ReporterId, r.EntityType, r.EntityId,
        r.Reason, r.Details, r.Status, r.ResolvedBy, r.ResolvedAt, r.CreatedAt);
}
