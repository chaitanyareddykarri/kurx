using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Infrastructure.Social;

/// <summary>Event reviews (D-064). Eligibility = the user holds an issued ticket for the event
/// (Ticket.EventId + Ticket.UserId are denormalized, so it's a single AnyAsync). One review per
/// (user, event), upserted so a re-post edits.</summary>
public class EventReviewService(
    KurxDbContext db, IProfileVisibilityResolver visibility, IStorage storage) : IEventReviewService
{
    public async Task<ServiceResult<ReviewView>> UpsertAsync(Guid userId, Guid eventId, int rating, string? title,
        string? body, bool isAnonymous, CancellationToken ct = default)
    {
        if (rating < 1 || rating > 5) return ServiceResult<ReviewView>.Fail("invalid_rating");

        var ticketId = await db.Tickets.AsNoTracking()
            .Where(t => t.EventId == eventId && t.UserId == userId)
            .Select(t => (Guid?)t.Id).FirstOrDefaultAsync(ct);
        if (ticketId is null) return ServiceResult<ReviewView>.Fail("review_requires_ticket");

        var review = await db.EventReviews.FirstOrDefaultAsync(
            r => r.EventId == eventId && r.UserId == userId && r.DeletedAt == null, ct);
        if (review is null)
        {
            review = new EventReview { EventId = eventId, UserId = userId };
            db.EventReviews.Add(review);
        }
        review.Rating = rating;
        review.Title = title;
        review.Body = body;
        review.IsAnonymous = isAnonymous;
        review.TicketId = ticketId;
        review.IsVerified = true;
        review.Status = EventReviewStatus.Published;
        await db.SaveChangesAsync(ct);

        var author = await db.Users.AsNoTracking().Where(u => u.Id == userId)
            .Select(u => new { u.Name, u.Username, u.AvatarKey }).FirstOrDefaultAsync(ct);
        // Resolved against an anonymous viewer, not against the author (D-233). A review's byline is a
        // decision about what *the public* sees; resolving it against the author would show them a
        // byline in their own response that no other reader gets.
        var authorLinkable = await visibility.VisibleProfileIdsAsync([userId], null, ct);
        var showIdentity = !isAnonymous && authorLinkable.Contains(userId);
        var authorAvatarKey = showIdentity ? author?.AvatarKey : null;
        return ServiceResult<ReviewView>.Success(new ReviewView(review.Id, eventId, rating, title, body,
            isAnonymous, true, isAnonymous ? null : author?.Name, review.CreatedAt,
            showIdentity ? author?.Username : null, authorAvatarKey,
            await storage.PresignOrNullAsync(authorAvatarKey, ct)));
    }

    public async Task<(IReadOnlyList<ReviewView> Items, ReviewSummary Summary, int Total)> ListAsync(
        Guid eventId, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);
        var q = db.EventReviews.AsNoTracking()
            .Where(r => r.EventId == eventId && r.DeletedAt == null && r.Status == EventReviewStatus.Published);

        var total = await q.CountAsync(ct);
        var avg = total == 0 ? 0 : await q.AverageAsync(r => (double)r.Rating, ct);

        // OFFSET is kept deliberately (DB-6). Reviews are public and insert-heavy, which normally argues
        // for keyset — but no shipped client can request page 2: web's public page calls this with the
        // default page 1, web's host tab hardcodes (1, 50), and Flutter renders one ListView with no
        // infinite scroll. Deep paging is unreachable, not merely unlikely, so a cursor contract would add
        // client complexity to optimise a path nothing calls. Revisit when a client paginates.
        //
        // The tie-breaker is still required: reviews arrive in bursts when an event ends, so same-tick
        // CreatedAt values are common and ordering on it alone lets a row repeat or vanish between pages.
        var rows = await q.OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Join(db.Users.AsNoTracking(), r => r.UserId, u => u.Id,
                (r, u) => new { r.Id, r.EventId, r.Rating, r.Title, r.Body, r.IsAnonymous, r.IsVerified, AuthorName = u.Name, UserId = u.Id, u.Username, u.AvatarKey, r.CreatedAt })
            .ToListAsync(ct);

        // One batched call for the page's authors (D-233), anonymous viewer — a review list is public
        // and this method carries no caller identity, so every reader sees the same bylines.
        var linkable = await visibility.VisibleProfileIdsAsync(
            rows.Where(x => !x.IsAnonymous).Select(x => x.UserId).Distinct().ToList(), null, ct);

        // Mirrors the database ordering exactly. LINQ-to-Objects' sort is stable, so CreatedAt alone would
        // happen to preserve the ties the query already ordered — but relying on that leaves the page's
        // order depending on an implementation detail of a different sort than the one that produced it.
        var items = rows.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).Select(x =>
        {
            var showIdentity = !x.IsAnonymous && linkable.Contains(x.UserId);
            return new ReviewView(x.Id, x.EventId, x.Rating, x.Title, x.Body, x.IsAnonymous, x.IsVerified,
                x.IsAnonymous ? null : x.AuthorName, x.CreatedAt, showIdentity ? x.Username : null, showIdentity ? x.AvatarKey : null);
        }).ToList();
        // D-302, second pass: the projection above is a synchronous lambda. An anonymous review carries
        // no key, so it gets no URL — the byline and the picture hide together, as they must.
        for (var i = 0; i < items.Count; i++)
            items[i] = items[i] with { AuthorAvatarUrl = await storage.PresignOrNullAsync(items[i].AuthorAvatarKey, ct) };

        return (items, new ReviewSummary(Math.Round(avg, 2), total), total);
    }

    public async Task<bool> DeleteMineAsync(Guid userId, Guid eventId, CancellationToken ct = default)
        => await db.EventReviews.Where(r => r.EventId == eventId && r.UserId == userId && r.DeletedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.DeletedAt, DateTime.UtcNow), ct) > 0;
}
