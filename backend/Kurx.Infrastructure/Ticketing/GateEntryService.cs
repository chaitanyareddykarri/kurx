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

        if (!await MayOperateGateAsync(scannedByUserId, scanEvent, ct))
            return new CheckInResult(false, false, "forbidden", null, null);

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

    /// <summary>Admits a staff member from the signed pass on their badge (D-385).</summary>
    public async Task<StaffCheckInResult> ScanStaffAsync(
        Guid scannedByUserId, Guid scanEventId, string pass, string? deviceInfo, CancellationToken ct = default)
    {
        var scanEvent = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == scanEventId, ct);
        if (scanEvent is null) return new StaffCheckInResult(false, false, "event_not_found");

        if (!await MayOperateGateAsync(scannedByUserId, scanEvent, ct))
            return new StaffCheckInResult(false, false, "forbidden");

        // The signature proves only that Kurx minted this pass. Everything that decides whether the door
        // opens is asked below, live.
        if (tokens.VerifyStaffPass(pass) is not { } assignmentId)
            return new StaffCheckInResult(false, false, "invalid_pass");

        var assignment = await db.EventAssignments.AsNoTracking()
            .Where(a => a.Id == assignmentId)
            .Join(db.Users.AsNoTracking(), a => a.UserId, u => u.Id, (a, u) => new
            {
                a.Id, a.EventId, a.Status, a.Role, a.CustomRole, u.Name, u.Username,
            })
            .FirstOrDefaultAsync(ct);

        // A pass whose assignment has been deleted is indistinguishable from a forged one, and is told
        // apart from a *revoked* one only in that there is nothing left to report.
        if (assignment is null) return new StaffCheckInResult(false, false, "invalid_pass");

        // A badge minted for another event must not open this door, however genuine its signature.
        if (assignment.EventId != scanEventId)
            return new StaffCheckInResult(false, false, "event_mismatch");

        // Revocation lives on the assignment's own Status (D-362) and is read per scan (D-015): removing
        // someone from the crew stops their already-printed badge working immediately.
        if (assignment.Status != AssignmentStatus.Accepted)
            return new StaffCheckInResult(false, false, "assignment_not_active");

        var role = string.Equals(assignment.Role, "Custom", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(assignment.CustomRole)
                ? assignment.CustomRole!
                : assignment.Role;
        var name = !string.IsNullOrWhiteSpace(assignment.Name) ? assignment.Name : assignment.Username ?? "";
        var access = StaffAccess.LevelFor(role);

        var already = await db.StaffGateEntries.AsNoTracking()
            .Where(s => s.AssignmentId == assignmentId && s.EventId == scanEventId)
            .Join(db.Users.AsNoTracking(), s => s.ScannedBy, u => u.Id, (s, u) => new { s.CreatedAt, u.Name })
            .FirstOrDefaultAsync(ct);

        // A staff member legitimately comes and goes all day, so a repeat scan is reported rather than
        // refused — the marshal still sees who they are and what the badge authorises.
        if (already is not null)
            return new StaffCheckInResult(
                false, true, null, name, role, access, already.CreatedAt, already.Name);

        var now = DateTime.UtcNow;
        db.StaffGateEntries.Add(new StaffGateEntry
        {
            AssignmentId = assignmentId,
            EventId = scanEventId,
            ScannedBy = scannedByUserId,
            DeviceInfo = deviceInfo,
            CreatedAt = now,
        });

        db.AuditLogs.Add(new AuditLog
        {
            ActorType = "user", ActorId = scannedByUserId,
            Action = "gate.staff_scan", Entity = "event_assignments", EntityId = assignmentId,
            DetailsJson = $"{{\"event_id\":\"{scanEventId}\"}}",
        });

        await db.SaveChangesAsync(ct);
        return new StaffCheckInResult(true, false, null, name, role, access);
    }

    /// <summary>Who may work a gate: any member of the organization the event represents, or anyone
    /// holding an accepted assignment on this specific event. Shared by both scan paths so a marshal who
    /// can admit an attendee can admit a colleague, and neither path can drift from the other.</summary>
    private async Task<bool> MayOperateGateAsync(Guid userId, Event scanEvent, CancellationToken ct)
    {
        if (await db.Memberships.AnyAsync(m => m.OrgId == scanEvent.RepresentingOrgId && m.UserId == userId, ct))
            return true;

        // External assignees (Security, Registration Desk, Volunteer, …) may scan only for the events they
        // were explicitly assigned to and accepted.
        return await db.EventAssignments.AnyAsync(
            a => a.EventId == scanEvent.Id && a.UserId == userId && a.Status == AssignmentStatus.Accepted, ct);
    }
}
