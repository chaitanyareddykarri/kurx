using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Infrastructure.Events;

/// <summary>D-266 M7 — the reviewer's working checklist for one event.
///
/// <para><b>The items are derived on every read, never stored.</b> They project
/// <see cref="PolicyRequirementsView.ReviewerChecklist"/>, which is the same resolution the publish
/// blockers come from. Persisting the item list instead would freeze it at the moment the event entered
/// review: a rule added afterwards, or an event edited into needing one, would be invisible to the person
/// approving it — and the reviewer would tick a complete-looking list while the publish still refused.</para>
///
/// <para>Performs no authorization. The endpoints are <c>VerificationReviewer</c>-gated, which is the
/// authorization, exactly as the rest of the admin review surface works.</para></summary>
public class EventReviewChecklistService(KurxDbContext db, IEventPolicyService policy) : IEventReviewChecklistService
{
    public async Task<ServiceResult<ReviewChecklistView>> GetAsync(Guid reviewerId, Guid eventId,
        CancellationToken ct = default)
    {
        var req = await policy.GetForEventAsync(eventId, ct);
        if (!req.Ok) return ServiceResult<ReviewChecklistView>.Fail(req.Error!);

        var ticks = await db.EventReviewChecklistItems.AsNoTracking()
            .Where(i => i.EventId == eventId && i.ReviewerId == reviewerId)
            .ToDictionaryAsync(i => i.ItemKey, i => i, ct);

        return ServiceResult<ReviewChecklistView>.Success(Project(eventId, req.Value!, ticks));
    }

    public async Task<ServiceResult<ReviewChecklistView>> SetAsync(Guid reviewerId, Guid eventId, string itemKey,
        bool @checked, CancellationToken ct = default)
    {
        var req = await policy.GetForEventAsync(eventId, ct);
        if (!req.Ok) return ServiceResult<ReviewChecklistView>.Fail(req.Error!);

        // A tick against an item that is not on this event's checklist would count towards completeness
        // without the reviewer ever having read a real requirement — which is precisely the gate being
        // bypassed rather than satisfied.
        if (!Keys(req.Value!).Contains(itemKey))
            return ServiceResult<ReviewChecklistView>.Fail("unknown_checklist_item");

        var row = await db.EventReviewChecklistItems
            .FirstOrDefaultAsync(i => i.EventId == eventId && i.ReviewerId == reviewerId && i.ItemKey == itemKey, ct);
        if (row is null)
        {
            row = new EventReviewChecklistItem { EventId = eventId, ReviewerId = reviewerId, ItemKey = itemKey };
            db.EventReviewChecklistItems.Add(row);
        }
        row.Checked = @checked;
        // Cleared on untick: a timestamp surviving an untick would read as "confirmed at 14:03" against an
        // item nobody currently vouches for.
        row.CheckedAt = @checked ? DateTime.UtcNow : null;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException) when (db.Entry(row).State == EntityState.Added)
        {
            // A double-clicked checkbox sends the tick twice; both find no row and both insert, and the
            // unique (EventId, ReviewerId, ItemKey) index rejects the loser. The reviewer asked for one
            // state and both requests agree on it — failing them with a 500 mid-review would be noise.
            db.Entry(row).State = EntityState.Detached;
            var winner = await db.EventReviewChecklistItems
                .FirstOrDefaultAsync(i => i.EventId == eventId && i.ReviewerId == reviewerId && i.ItemKey == itemKey, ct);
            if (winner is null) throw;   // not the race — a genuine failure, and it must not be swallowed
            winner.Checked = @checked;
            winner.CheckedAt = @checked ? DateTime.UtcNow : null;
            await db.SaveChangesAsync(ct);
        }

        return await GetAsync(reviewerId, eventId, ct);
    }

    public async Task<bool> IsCompleteAsync(Guid reviewerId, Guid eventId, CancellationToken ct = default)
    {
        var req = await policy.GetForEventAsync(eventId, ct);
        // An unresolvable policy cannot be shown to be satisfied. Failing closed here matters more than
        // elsewhere: this is the last gate before an event goes live in an institution's name.
        if (!req.Ok) return false;

        var keys = Keys(req.Value!);
        if (keys.Count == 0) return true;   // nothing to confirm — an event with no requirements at all

        var ticked = await db.EventReviewChecklistItems.AsNoTracking()
            .Where(i => i.EventId == eventId && i.ReviewerId == reviewerId && i.Checked)
            .Select(i => i.ItemKey)
            .ToListAsync(ct);

        return keys.All(ticked.Contains);
    }

    /// <summary>The checklist keys for an event, de-duplicated. <c>ReviewerChecklist</c> concatenates
    /// requirements and violations, and a rule can legitimately appear in both.</summary>
    private static IReadOnlyList<string> Keys(PolicyRequirementsView v)
        => v.ReviewerChecklist.Distinct().ToList();

    private static ReviewChecklistView Project(Guid eventId, PolicyRequirementsView v,
        IReadOnlyDictionary<string, EventReviewChecklistItem> ticks)
    {
        var blocking = v.PublishBlockers.ToHashSet();
        var items = Keys(v).Select(k =>
        {
            var t = ticks.GetValueOrDefault(k);
            return new ReviewChecklistItemView(k, t?.Checked ?? false, t?.CheckedAt, blocking.Contains(k));
        }).ToList();

        return new ReviewChecklistView(eventId, items, items.All(i => i.Checked));
    }
}
