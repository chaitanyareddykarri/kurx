using Kurx.Application.Abstractions;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Infrastructure.Events;

/// <summary>The one implementation of event authorization (D-269). See <see cref="IEventAuthority"/> for
/// why it exists and what it replaced.</summary>
public class EventAuthorityService(KurxDbContext db) : IEventAuthority
{
    public async Task<EventAccess> ResolveAsync(Guid? userId, Guid eventId, bool isAdmin, CancellationToken ct = default)
    {
        var ev = await db.Events.AsNoTracking()
            .Where(e => e.Id == eventId && e.DeletedAt == null)
            .Select(e => new { e.RepresentingOrgId, e.CreatedBy })
            .FirstOrDefaultAsync(ct);
        if (ev is null) return EventAccess.NotFound;

        // Admin outranks everything and needs no relationship to the event.
        if (isAdmin) return new EventAccess(true, EventAuthorityLevel.Admin, ev.RepresentingOrgId, ev.CreatedBy);
        if (userId is not { } uid) return new EventAccess(true, EventAuthorityLevel.None, ev.RepresentingOrgId, ev.CreatedBy);

        // Ownership first, and on its own: Kurx is user-first, so the creator's authority is not derived
        // from membership of anything (D-268). This is what lets a personally-represented event be managed
        // with no organization seat behind it at all.
        if (ev.CreatedBy == uid)
            return new EventAccess(true, EventAuthorityLevel.Manager, ev.RepresentingOrgId, ev.CreatedBy);

        // Then the representation: a seat in the organization the event represents.
        var role = await db.Memberships.AsNoTracking()
            .Where(m => m.OrgId == ev.RepresentingOrgId && m.UserId == uid)
            .Select(m => (OrgRole?)m.Role)
            .FirstOrDefaultAsync(ct);

        var level = EventAuthority.LevelFor(role);
        if (level > EventAuthorityLevel.None)
            return new EventAccess(true, level, ev.RepresentingOrgId, ev.CreatedBy);

        // Finally the audience floor (D-272): an accepted programme participation (V3 §5.1) — a
        // speaker/judge/mentor/volunteer — or a live ticket. Both mean "part of this event, with no
        // authority over it", so both land on Participant. Live per request (D-015): a refunded ticket
        // stops granting standing the moment it is voided, not at the next login.
        var participates = await db.EventParticipants.AsNoTracking()
            .AnyAsync(p => p.EventId == eventId
                && p.SubjectType == ParticipantSubjectType.Person
                && p.SubjectId == uid
                && p.State == ParticipantState.Active, ct);

        // Short-circuits, so the common resolve — a manager acting on their own event — never reaches
        // either of these queries.
        participates = participates || await db.Tickets.AsNoTracking()
            .AnyAsync(t => t.EventId == eventId && t.UserId == uid && t.State != TicketState.Void, ct);

        return new EventAccess(true,
            participates ? EventAuthorityLevel.Participant : EventAuthorityLevel.None,
            ev.RepresentingOrgId, ev.CreatedBy);
    }

    public async Task<OrgAuthority> ResolveOrgAsync(Guid userId, Guid orgId, bool isAdmin, CancellationToken ct = default)
    {
        var role = await db.Memberships.AsNoTracking()
            .Where(m => m.OrgId == orgId && m.UserId == userId)
            .Select(m => (OrgRole?)m.Role)
            .FirstOrDefaultAsync(ct);

        // Admin outranks any seat and needs none — but IsMember stays honest about whether one exists.
        if (isAdmin) return new OrgAuthority(role is not null, EventAuthorityLevel.Admin);
        return role is null ? OrgAuthority.None : new OrgAuthority(true, EventAuthority.LevelFor(role));
    }
}
