using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Auth;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Infrastructure.Ticketing;

public class GateEntryService(KurxDbContext db, TokenService tokens, IAudienceService audience,
    IRealtimeBroadcaster realtime) : IGateEntryService
{
    public async Task<CheckInResult> ScanAsync(Guid scannedByUserId, Guid scanEventId, Guid ticketCode, string? deviceInfo, CancellationToken ct = default)
    {
        // Resolve the event first — needed for authorization (orgId) and event-match logic.
        var scanEvent = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == scanEventId, ct);
        if (scanEvent is null)
            return new CheckInResult(false, false, "event_not_found", null, null);

        // Authorization: caller must be an org member (any role) OR have an accepted
        // EventAssignment for this specific event. Any org member can operate a gate;
        // external assignees (Security, Registration Desk, Volunteer, etc.) may scan
        // only for events they were explicitly assigned to and accepted.
        var isOrgMember = await db.Memberships.AnyAsync(
            m => m.OrgId == scanEvent.RepresentingOrgId && m.UserId == scannedByUserId, ct);

        if (!isOrgMember)
        {
            var hasAcceptedAssignment = await db.EventAssignments.AnyAsync(
                a => a.EventId == scanEventId
                     && a.UserId == scannedByUserId
                     && a.Status == AssignmentStatus.Accepted, ct);

            if (!hasAcceptedAssignment)
                return new CheckInResult(false, false, "forbidden", null, null);
        }

        var ticket = await db.Tickets.FirstOrDefaultAsync(t => t.Code == ticketCode, ct);
        if (ticket is null)
            return new CheckInResult(false, false, "invalid_code", null, null);

        var expectedHmac = tokens.SignTicketCode(ticketCode);
        if (ticket.HmacSig != expectedHmac)
            return new CheckInResult(false, false, "invalid_signature", null, null);

        if (ticket.State == TicketState.Void)
            return new CheckInResult(false, false, "ticket_void", null, null);

        // Validate event match — direct ticket or an all-access fest pass at a child event.
        bool eventMatch = ticket.EventId == scanEventId;
        if (!eventMatch)
        {
            var isChildEvent = scanEvent.ParentEventId == ticket.EventId;
            var ticketType = await db.TicketTypes.AsNoTracking()
                .Where(tt => tt.EventId == ticket.EventId)
                .Select(tt => tt.IsAllAccess)
                .FirstOrDefaultAsync(ct);
            if (!isChildEvent || !ticketType)
                return new CheckInResult(false, false, "event_mismatch", null, null);
        }

        // Check for duplicate — unique(ticket_id, event_id) constraint is the guard.
        var existing = await db.GateEntries.AsNoTracking()
            .Where(g => g.TicketId == ticket.Id && g.EventId == scanEventId)
            .Join(db.Users.AsNoTracking(), g => g.ScannedBy, u => u.Id,
                (g, u) => new { g.CreatedAt, u.Name })
            .FirstOrDefaultAsync(ct);

        if (existing is not null)
            return new CheckInResult(false, true, null, existing.CreatedAt, existing.Name);

        var now = DateTime.UtcNow;
        db.GateEntries.Add(new GateEntry
        {
            TicketId = ticket.Id,
            EventId = scanEventId,
            ScannedBy = scannedByUserId,
            DeviceInfo = deviceInfo,
            CreatedAt = now,
        });

        // Denormalize first check-in onto the ticket row.
        if (ticket.State != TicketState.CheckedIn)
        {
            ticket.State = TicketState.CheckedIn;
            ticket.CheckedInAt = now;
            ticket.CheckedInBy = scannedByUserId;
        }

        db.AuditLogs.Add(new AuditLog
        {
            ActorType = "user", ActorId = scannedByUserId,
            Action = "gate.scan", Entity = "tickets", EntityId = ticket.Id,
            DetailsJson = $"{{\"event_id\":\"{scanEventId}\"}}",
        });

        // V3 §4.4 re-evaluation policy: re-check audience eligibility against the event the holder registered
        // for. If they lapsed since registration, FLAG the admission for the organiser — never silently void
        // it (nor silently admit the ineligible). Only account holders under an audience-gated event pay a cost.
        string? eligibilityFlag = null;
        if (ticket.UserId is not null && await db.AudienceRules.AsNoTracking().AnyAsync(r => r.EventId == ticket.EventId, ct))
        {
            var elig = await audience.EvaluateAsync(ticket.UserId, ticket.EventId, ct);
            if (!elig.Allowed)
            {
                eligibilityFlag = elig.Reason;
                db.AuditLogs.Add(new AuditLog
                {
                    ActorType = "user", ActorId = scannedByUserId,
                    Action = "gate.eligibility_flag", Entity = "tickets", EntityId = ticket.Id,
                    DetailsJson = $"{{\"reason\":\"{elig.Reason}\"}}",
                });
            }
        }

        await db.SaveChangesAsync(ct);

        // D-186: first real caller of BroadcastScanAsync — previously defined, never invoked (D-017 audit).
        // Best-effort: a dashboard missing one live tick is a UX gap, not a data-loss bug, so a hub outage
        // must never fail a check-in.
        try
        {
            await realtime.BroadcastScanAsync(scanEventId, new
            {
                ticket_id = ticket.Id, event_id = scanEventId, scanned_at = now, eligibility_flag = eligibilityFlag,
            }, ct);
        }
        catch { /* live dashboard tick only — the check-in itself already committed above */ }

        return new CheckInResult(true, false, null, null, null, eligibilityFlag);
    }
}
