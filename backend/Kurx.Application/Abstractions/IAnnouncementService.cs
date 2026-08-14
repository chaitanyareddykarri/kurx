namespace Kurx.Application.Abstractions;

public record AnnouncementView(
    Guid Id, Guid EventId, string Title, string Body, string Audience,
    bool IncludeChildEvents, string[] Channels, string Status,
    DateTime? ScheduledAt, int TotalRecipients,
    int SentPush, int SentEmail, int SentWhatsapp, int FailedCount,
    DateTime? SentAt, DateTime CreatedAt);

public interface IAnnouncementService
{
    Task<ServiceResult<AnnouncementView>> CreateAsync(Guid createdBy, Guid eventId,
        string title, string body, string audience, string[] channels,
        DateTime? scheduledAt, bool includeChildEvents, CancellationToken ct = default);

    Task<ServiceResult<List<AnnouncementView>>> ListAsync(Guid userId, Guid eventId, CancellationToken ct = default);
    Task<ServiceResult<AnnouncementView>> GetAsync(Guid userId, Guid announcementId, CancellationToken ct = default);
    Task<ServiceResult<bool>> CancelAsync(Guid userId, Guid announcementId, CancellationToken ct = default);
    Task<ServiceResult<AnnouncementView>> UpdateAsync(Guid userId, Guid announcementId, string? title, string? body, DateTime? scheduledAt, CancellationToken ct = default);

    // Hangfire job entry-point
    Task SendAsync(Guid announcementId, CancellationToken ct = default);
}
