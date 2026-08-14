using Hangfire;
using Kurx.Application.Abstractions;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kurx.Infrastructure.Jobs;

[DisableConcurrentExecution(timeoutInSeconds: 10)]  // one run at a time across every replica
[AutomaticRetry(Attempts = 0)]   // next tick is soon; a failed run is not worth replaying
public class EventReminderJob
{
    private readonly KurxDbContext _db;
    private readonly INotificationService _notificationService;
    private readonly ILogger<EventReminderJob> _log;

    public EventReminderJob(KurxDbContext db, INotificationService notificationService, ILogger<EventReminderJob> log)
    {
        _db = db;
        _notificationService = notificationService;
        _log = log;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        _log.LogInformation("Checking for events starting in the next 24 hours...");
        var startRange = DateTime.UtcNow;
        var endRange = startRange.AddHours(24);

        var upcomingEvents = await _db.Events.AsNoTracking()
            .Where(e => e.Status == EventStatus.Published && e.StartsAt >= startRange && e.StartsAt < endRange && e.DeletedAt == null)
            .ToListAsync(ct);

        _log.LogInformation("Found {Count} upcoming events for reminder distribution.", upcomingEvents.Count);

        foreach (var ev in upcomingEvents)
        {
            ct.ThrowIfCancellationRequested();

            var registeredUserIds = await _db.Tickets.AsNoTracking()
                .Where(t => t.EventId == ev.Id && t.State == TicketState.Issued && t.UserId != null)
                .Select(t => t.UserId!.Value)
                .Distinct()
                .ToListAsync(ct);
            if (registeredUserIds.Count == 0) continue;

            // DB-3: ONE query for the whole event's already-notified set, not one per registrant.
            //
            // This was `Notifications.AnyAsync(n => n.UserId == userId && n.Kind == "EventReminder" &&
            // n.DataJson.Contains(ev.Id.ToString()))` inside the per-user loop. Each probe was index-backed
            // on UserId, so the cost was round trips rather than scans — but an event with 10,000
            // registrants meant 10,000 sequential round trips, every hour, for every event starting within
            // 24 hours. Now it is a single indexed read on ix_notifications_dedup per event.
            var dedupKey = NotificationDedup.ForEventReminder(ev.Id);
            var alreadyNotified = (await _db.Notifications.AsNoTracking()
                .Where(n => n.DedupKey == dedupKey && registeredUserIds.Contains(n.UserId))
                .Select(n => n.UserId)
                .ToListAsync(ct)).ToHashSet();

            foreach (var userId in registeredUserIds)
            {
                if (alreadyNotified.Contains(userId)) continue;

                // The dedup key is passed through so the partial unique index — not this pre-check — is the
                // final authority. DisableConcurrentExecution keeps one run at a time per replica set, but
                // it is a lease with a timeout, not a mutual exclusion proof; the index closes the gap.
                //
                // NotifyAsync stays the writer, so reminders keep going through the one dispatch point that
                // applies the D-263 preference gate and pushes to devices. A user with the category muted
                // gets no row, so no dedup key is written and the next tick re-attempts — unchanged
                // behaviour, and still a no-op each time.
                await _notificationService.NotifyAsync(
                    userId,
                    NotificationDedup.EventReminderKind,
                    $"Reminder: {ev.Title} starts soon!",
                    $"Your event '{ev.Title}' starts at {ev.StartsAt:t} at {ev.VenueName ?? "the venue"}.",
                    new { eventId = ev.Id },
                    ct,
                    dedupKey);
            }
        }
    }
}
