using System.Text.Json;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Infrastructure.Events;

/// <summary>D-363 §4 — an approved event that changes materially goes back to the queue.
///
/// <para>Shared because the reviewed substance of an event is spread across three services: the event row
/// itself (<c>EventService</c>), what it costs and how many may enter (<c>TicketTypeService</c>), and who
/// may enter (<c>AudienceService</c>). §3's lesson was that a guard on the one path you thought of holds
/// only until someone adds a second — so this is one definition all three call, rather than three
/// spellings of it that can drift apart.</para></summary>
internal static class EventReviewReopen
{
    /// <summary>Returns true if the event was moved. Conditional on <c>Approved</c> IN SQL, not on the
    /// caller's snapshot: a concurrent transition may already have moved it, and overwriting that would
    /// resurrect a state the event has left.
    ///
    /// <para>The caller's tracked copy — if it has one — still holds the old status afterwards, exactly as
    /// with the transition CAS. Reload before projecting a response from it.</para></summary>
    public static async Task<bool> IfApprovedAsync(KurxDbContext db, Guid eventId, EventStatus status,
        Guid actorId, string what, CancellationToken ct)
    {
        if (status != EventStatus.Approved) return false;

        var moved = await db.Events
            .Where(e => e.Id == eventId && e.Status == EventStatus.Approved)
            .ExecuteUpdateAsync(s => s
                .SetProperty(e => e.Status, EventStatus.PendingReview)
                // The claim is released for the same reason every other leg releases it: the item is back
                // in the queue, and an item in the queue is nobody's work in progress.
                .SetProperty(e => e.ReviewClaimedBy, (Guid?)null)
                .SetProperty(e => e.ReviewClaimedAt, (DateTime?)null)
                .SetProperty(e => e.UpdatedAt, DateTime.UtcNow), ct);
        if (moved == 0) return false;

        // A reviewer seeing an item reappear needs to know it came back because it changed, not because
        // nobody decided it. Without the reason the two are indistinguishable in the queue.
        db.AuditLogs.Add(new AuditLog
        {
            ActorType = "user", ActorId = actorId,
            Action = "event.review.reopened_by_edit", Entity = "events", EntityId = eventId,
            DetailsJson = JsonSerializer.Serialize(new { reason = $"{what} changed after approval (D-363)" }),
        });
        await db.SaveChangesAsync(ct);
        return true;
    }
}
