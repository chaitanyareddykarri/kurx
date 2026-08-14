using Hangfire;
using System.Text.Json;
using Kurx.Application.Abstractions;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Kurx.Infrastructure.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kurx.Infrastructure.Jobs;

/// <summary>Transactional-outbox dispatcher (AM8, ADR-AM16). Security-critical notifications are written
/// to <c>outbox_messages</c> in the <i>same transaction</i> as the state change that caused them, so the
/// user is told about a new sign-in / a revoked device / an account recovery even if the push provider is
/// down at that moment. This job drains them at-least-once; <c>IdempotencyKey</c> is what makes a
/// re-delivery harmless downstream.</summary>
[DisableConcurrentExecution(timeoutInSeconds: 10)]  // one run at a time across every replica
[AutomaticRetry(Attempts = 0)]   // next tick is soon; a failed run is not worth replaying
public class OutboxDispatchJob(KurxDbContext db, IPushSender push, IChatService chat, ISearchIndexService searchIndex,
    IAuthTelemetry telemetry, ILogger<OutboxDispatchJob> log)
{
    private const int BatchSize = 100;
    private const int MaxAttempts = 5;

    public async Task RunAsync(CancellationToken ct = default)
    {
        var pending = await db.OutboxMessages
            .Where(m => m.Status == OutboxStatus.Pending && m.Attempts < MaxAttempts)
            .OrderBy(m => m.CreatedAt)
            .Take(BatchSize)
            .ToListAsync(ct);
        if (pending.Count == 0) return;

        foreach (var message in pending)
        {
            message.Attempts++;
            try
            {
                await DispatchAsync(message.Type, message.PayloadJson, ct);
                message.Status = OutboxStatus.Dispatched;
                message.DispatchedAt = DateTime.UtcNow;
                message.LastError = null;
            }
            catch (OutboxPermanentFailureException ex)
            {
                // Retrying cannot help: the handler is missing from the code, or the payload is
                // unaddressable. Park it immediately and alert.
                //
                // This branch exists because the previous behaviour was the opposite and worse. An
                // unrecognised type fell through a `_ => (null, null)` switch arm, returned early, and was
                // marked **Dispatched** — so `login.succeeded`, `password.changed` and `password.reset`
                // were all recorded as delivered while reaching nobody. A silent success in the one table
                // whose entire purpose is guaranteeing delivery is the worst possible failure mode: the
                // security alert a victim needs is exactly the message nobody notices the absence of.
                message.Status = OutboxStatus.Failed;
                message.LastError = Truncate(ex.Message);
                log.LogError(ex, "Outbox message {Id} ({Type}) was NOT delivered and will not be retried: {Reason}",
                    message.Id, message.Type, ex.Message);
                telemetry.RecordSecurityEvent("outbox.undeliverable", "critical");
            }
            catch (Exception ex)
            {
                // Transient: keep it Pending so the next run retries; only a message that exhausted its
                // attempts is parked as Failed for an operator to look at.
                message.LastError = Truncate(ex.Message);
                if (message.Attempts >= MaxAttempts)
                {
                    message.Status = OutboxStatus.Failed;
                    log.LogError(ex, "Outbox message {Id} ({Type}) failed permanently after {Attempts} attempts",
                        message.Id, message.Type, message.Attempts);
                    telemetry.RecordSecurityEvent("outbox.exhausted", "critical");
                }
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private static string Truncate(string message) => message.Length > 500 ? message[..500] : message;

    /// <summary>A message that will never succeed however many times it is retried — an unregistered type,
    /// or a payload with nobody to deliver to. Distinguished from a transient provider failure so it is
    /// parked and alerted on immediately rather than after five pointless attempts.</summary>
    private sealed class OutboxPermanentFailureException(string message) : Exception(message);

    private async Task DispatchAsync(string type, string payloadJson, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(payloadJson);
        var root = doc.RootElement;

        // V3 §15 (Phase 16): rebuild (or remove) one event's discovery document. Idempotent — a redelivery just
        // re-projects the current event state. This is the ONLY writer of the search index (never a dual-write).
        if (type == "search.reindex")
        {
            if (root.TryGetProperty("eventId", out var seEl) && seEl.TryGetGuid(out var searchEventId))
                await searchIndex.ProjectAsync(searchEventId, ct);
            return;
        }

        // V3 §17.1 (Phase 9): the order/refund admission side effects — joining or leaving the event chat — are
        // delivered here, never inline in the money path. Both handlers are idempotent.
        // `chat_staff_removed` is the participant lifecycle (D-299); the others are order/refund. All three
        // are idempotent and safe to redeliver.
        //
        // D-300 removed `event.chat_join_host`: a participant joins as a Member, which `event.chat_join`
        // already does. Any row of the old type still in flight when this deploys would hit the "no handler"
        // path and fail loudly rather than silently — acceptable, because the type existed for less than a
        // day and only ever between D-299 and D-300.
        if (type is "event.chat_join" or "event.chat_leave_if_no_tickets" or "event.chat_staff_removed")
        {
            if (!root.TryGetProperty("eventId", out var evEl) || !evEl.TryGetGuid(out var eventId)) return;
            if (!root.TryGetProperty("userId", out var uEl) || !uEl.TryGetGuid(out var chatUserId)) return;
            switch (type)
            {
                case "event.chat_join":
                    await chat.EnsureRoomExistsAsync(eventId, ct);
                    await chat.AddMemberByEventAsync(eventId, chatUserId, "Member", ct);
                    break;
                case "event.chat_staff_removed":
                    // Re-checks standing itself: a person who still holds the event grant keeps Host.
                    await chat.RemoveStaffMemberAsync(eventId, chatUserId, ct);
                    break;
                default:
                    await chat.RemoveMemberIfNoTicketsAsync(eventId, chatUserId, ct);
                    break;
            }
            return;
        }

        // D-304 — an organization authority change, converged across every published room that organization
        // represents. Distinct from the event-scoped types above because one row fans out across many rooms,
        // and because the payload names only WHO changed: the handler re-resolves their live authority per
        // event, which is what makes redelivery a no-op and concurrent changes settle on committed state
        // instead of on the order the rows happened to be written.
        if (type == "org.authority_changed")
        {
            if (!root.TryGetProperty("orgId", out var orgEl) || !orgEl.TryGetGuid(out var authorityOrgId)) return;
            if (!root.TryGetProperty("userId", out var auEl) || !auEl.TryGetGuid(out var authorityUserId)) return;
            await chat.SyncOrgAuthorityAsync(authorityOrgId, authorityUserId, ct);
            return;
        }

        // Every remaining type is user-addressed. A payload without a resolvable user is a producer bug,
        // not a transient condition, so it is surfaced rather than swallowed.
        if (!root.TryGetProperty("userId", out var userIdElement) || !userIdElement.TryGetGuid(out var userId))
            throw new OutboxPermanentFailureException(
                $"Outbox type '{type}' carries no resolvable userId, so it cannot be delivered.");

        var (title, body) = type switch
        {
            // Sign-in alerts. These are the messages a victim needs when it was not them, which is why an
            // unhandled type must never look delivered.
            "login.approved" => ("New sign-in approved", "A device just signed in to your Kurx account."),
            "login.succeeded" => ("New sign-in", "Your Kurx account was just signed in to."),
            // Credential changes.
            "password.changed" => ("Password changed", "Your Kurx password was just changed."),
            "password.reset" => ("Password reset", "Your Kurx password was just reset."),
            "device.revoked" => ("Device removed", "A trusted device was removed from your account."),
            "recovery.redeemed" => ("Account recovered", "Your account was recovered with a recovery code."),
            // No silent default. Adding a producer without a handler here now fails loudly on the first
            // dispatch instead of discarding the notification.
            _ => throw new OutboxPermanentFailureException(
                $"No handler is registered for outbox type '{type}'. The notification was not delivered."),
        };

        var fcmTokens = await db.Devices.AsNoTracking()
            .Where(d => d.UserId == userId && d.IsActive)
            .Select(d => d.FcmToken)
            .ToListAsync(ct);

        foreach (var token in fcmTokens)
        {
            try
            {
                await push.SendAsync(token, title, body!, new Dictionary<string, string> { ["type"] = type }, ct);
            }
            catch (InvalidFcmTokenException)
            {
                // Dead token: deactivate it and keep sending to the user's other devices. A dead token is a permanent
                // condition, so it must NOT abort the loop nor churn outbox retries. Mirrors NotificationService.
                try
                {
                    var dev = await db.Devices.FirstOrDefaultAsync(d => d.FcmToken == token, ct);
                    if (dev is not null) { dev.IsActive = false; await db.SaveChangesAsync(ct); }
                }
                catch (Exception dbEx)
                {
                    log.LogError(dbEx, "Failed to deactivate invalid FCM token {Token}", token);
                }
            }
        }
    }
}
