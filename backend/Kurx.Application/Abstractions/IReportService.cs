namespace Kurx.Application.Abstractions;

public record ReportView(Guid Id, Guid ReporterId, string EntityType, Guid EntityId, string Reason,
    string? Details, string Status, Guid? ResolvedBy, DateTime? ResolvedAt, DateTime CreatedAt);

/// <summary>Content moderation (C-5, D-059). Any user files a report against a polymorphic subject
/// (event / review / chat_message / user / org / post / post_comment); Moderation staff triage it.
/// Resolution writes audit_log. Posts reuse this queue rather than owning a second one (D-262).</summary>
public interface IReportService
{
    Task<ServiceResult<ReportView>> CreateAsync(Guid reporterId, string entityType, Guid entityId,
        string reason, string? details, CancellationToken ct = default);

    Task<IReadOnlyList<ReportView>> ListAsync(string? status, int limit, CancellationToken ct = default);

    /// <param name="dismiss">false → status "resolved"; true → "dismissed". Both are terminal.</param>
    Task<ServiceResult<ReportView>> ResolveAsync(Guid reviewerId, Guid reportId, bool dismiss, CancellationToken ct = default);
}
