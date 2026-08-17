namespace Kurx.Application.Abstractions;

public record ReviewView(Guid Id, Guid EventId, int Rating, string? Title, string? Body,
    bool IsAnonymous, bool IsVerified, string? AuthorName, DateTime CreatedAt,
    string? AuthorUsername = null, string? AuthorAvatarKey = null,
    /// <summary>Presigned companion to <c>AuthorAvatarKey</c> (D-302). Null when there is no key.</summary>
    string? AuthorAvatarUrl = null);

public record ReviewSummary(double Average, int Count);

/// <summary>Event reviews / ratings (D-064). A verified ticket-holder leaves one 1–5 review per event
/// (upsert = edit); anyone reads them. Finishes the scaffolded <c>event_reviews</c> table.</summary>
public interface IEventReviewService
{
    /// <returns>Fail("invalid_rating") if not 1–5; Fail("review_requires_ticket") if the user holds no ticket for the event.</returns>
    Task<ServiceResult<ReviewView>> UpsertAsync(Guid userId, Guid eventId, int rating, string? title, string? body,
        bool isAnonymous, CancellationToken ct = default);

    Task<(IReadOnlyList<ReviewView> Items, ReviewSummary Summary, int Total)> ListAsync(Guid eventId, int page, int pageSize, CancellationToken ct = default);

    Task<bool> DeleteMineAsync(Guid userId, Guid eventId, CancellationToken ct = default);
}
