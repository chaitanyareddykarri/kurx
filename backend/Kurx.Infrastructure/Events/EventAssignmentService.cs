using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Auth;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Infrastructure.Events;

/// <summary>Event assignments (D-064). Owner/Manager invite; the invited user accepts/declines their own.</summary>
public class EventAssignmentService(
    KurxDbContext db, IEventAuthority authority, IChatService chat, IProfileVisibilityResolver visibility,
    INotificationService notifications) : IEventAssignmentService
{
    private static readonly HashSet<string> ValidRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "Volunteer", "Judge", "Moderator", "Registration Desk", "Stage Manager", "Security", "Photographer",
        "Videographer", "Host", "Media Team", "Speaker Coordinator", "Technical Team", "Support Team", "Custom",
    };

    public async Task<ServiceResult<AssignmentView>> AssignAsync(Guid actorId, Guid orgId, Guid eventId, string phone,
        string role, string? customRole, string? notes, CancellationToken ct = default)
    {
        if (!(await authority.ResolveAsync(actorId, eventId, isAdmin: false, ct)).Can(EventPermission.ManageContent))
            return ServiceResult<AssignmentView>.Fail("forbidden");
        if (!ValidRoles.Contains(role)) return ServiceResult<AssignmentView>.Fail("invalid_role");
        if (role.Equals("Custom", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(customRole))
            return ServiceResult<AssignmentView>.Fail("custom_role_required");
        if (!await db.Events.AnyAsync(e => e.Id == eventId && e.RepresentingOrgId == orgId && e.DeletedAt == null, ct))
            return ServiceResult<AssignmentView>.Fail("not_found");

        var normalized = AuthService.NormalizePhone(phone);
        var userId = await db.Users.AsNoTracking().Where(u => u.Phone == normalized).Select(u => (Guid?)u.Id).FirstOrDefaultAsync(ct);
        if (userId is null) return ServiceResult<AssignmentView>.Fail("user_not_found");

        // Idempotent per (event, user, role) among live (Invited/Accepted) assignments.
        var existing = await db.EventAssignments.FirstOrDefaultAsync(a => a.EventId == eventId && a.UserId == userId
            && a.Role == role && (a.Status == AssignmentStatus.Invited || a.Status == AssignmentStatus.Accepted), ct);
        if (existing is not null) return ServiceResult<AssignmentView>.Success(await ToViewAsync(existing, ct));

        var assignment = new EventAssignment
        {
            EventId = eventId, OrgId = orgId, UserId = userId.Value, InvitedBy = actorId,
            Role = role, CustomRole = customRole, Notes = notes, Status = AssignmentStatus.Invited,
        };
        db.EventAssignments.Add(assignment);
        await db.SaveChangesAsync(ct);
        var view = await ToViewAsync(assignment, ct);

        // D-319 — the row used to be written and nothing else happened: no push, no in-app notification,
        // and no screen anywhere that listed it. The invitee is the ONLY actor who can move this to
        // Accepted (RespondAsync refuses everyone else), and the go-live gate waits on that acceptance —
        // so an invite they could never discover stalled the event lifecycle silently. Sent after the
        // save, so a notification failure cannot lose the assignment; re-inviting is idempotent above.
        await notifications.NotifyAsync(userId.Value, NotificationKinds.StaffInvited,
            "You've been invited to an event team",
            $"{view.EventTitle} — as {view.CustomRole ?? view.Role}. Accept or decline it from your assignments.",
            new { route = "/assignments", assignment_id = assignment.Id, event_id = eventId }, ct);

        return ServiceResult<AssignmentView>.Success(view);
    }

    public async Task<ServiceResult<IReadOnlyList<AssignmentView>>> ListForEventAsync(Guid actorId, Guid orgId, Guid eventId, CancellationToken ct = default)
    {
        if (!(await authority.ResolveAsync(actorId, eventId, isAdmin: false, ct)).Can(EventPermission.ManageContent))
            return ServiceResult<IReadOnlyList<AssignmentView>>.Fail("forbidden");
        var rows = await db.EventAssignments.AsNoTracking()
            .Where(a => a.EventId == eventId && a.Status != AssignmentStatus.Removed)
            .Join(db.Users.AsNoTracking(), a => a.UserId, u => u.Id, (a, u) => new { a, u })
            .OrderByDescending(x => x.a.CreatedAt)
            .ToListAsync(ct);
        var linkable = await LinkableAsync(rows.Select(x => x.u.Id).ToList(), ct);
        var events = await EventContextAsync(rows.Select(x => x.a.EventId).Distinct().ToList(), ct);
        IReadOnlyList<AssignmentView> views = rows.Select(x => ToView(x.a, x.u, linkable, events)).ToList();
        return ServiceResult<IReadOnlyList<AssignmentView>>.Success(views);
    }

    public async Task<ServiceResult<bool>> RemoveAsync(Guid actorId, Guid orgId, Guid eventId, Guid assignmentId, CancellationToken ct = default)
    {
        if (!(await authority.ResolveAsync(actorId, eventId, isAdmin: false, ct)).Can(EventPermission.ManageContent))
            return ServiceResult<bool>.Fail("forbidden");
        var a = await db.EventAssignments.FirstOrDefaultAsync(x => x.Id == assignmentId && x.EventId == eventId && x.OrgId == orgId, ct);
        if (a is null) return ServiceResult<bool>.Fail("not_found");
        var wasAccepted = a.Status == AssignmentStatus.Accepted;
        a.Status = AssignmentStatus.Removed;
        a.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        // Only an accepted assignment ever granted chat access, and only if no *other* live assignment
        // still justifies it (a user may hold several roles on one event).
        if (wasAccepted && !await db.EventAssignments.AnyAsync(x => x.EventId == a.EventId
                && x.UserId == a.UserId && x.Status == AssignmentStatus.Accepted, ct))
            await chat.RemoveStaffMemberAsync(a.EventId, a.UserId, ct);

        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<AssignmentView>> RespondAsync(Guid userId, Guid assignmentId, bool accept, CancellationToken ct = default)
    {
        var a = await db.EventAssignments.FirstOrDefaultAsync(x => x.Id == assignmentId, ct);
        if (a is null) return ServiceResult<AssignmentView>.Fail("not_found");
        if (a.UserId != userId) return ServiceResult<AssignmentView>.Fail("forbidden");
        if (a.Status != AssignmentStatus.Invited) return ServiceResult<AssignmentView>.Fail("not_pending");
        a.Status = accept ? AssignmentStatus.Accepted : AssignmentStatus.Declined;
        if (accept) a.AcceptedAt = DateTime.UtcNow;
        a.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        // Chat access is granted on acceptance, not invitation — an invite the user never answered
        // must not put them in the room. AddMemberByEventAsync upgrades an existing Member to Host.
        // D-300 — an accepted assignment joins the chat as a MEMBER. Any assignment role used to arrive
        // as a Host, so accepting "Videographer" granted ban and delete-anyone powers.
        if (accept) await chat.AddMemberByEventAsync(a.EventId, a.UserId, "Member", ct);

        return ServiceResult<AssignmentView>.Success(await ToViewAsync(a, ct));
    }

    public async Task<IReadOnlyList<AssignmentView>> ListMineAsync(Guid userId, CancellationToken ct = default)
    {
        var rows = await db.EventAssignments.AsNoTracking()
            .Where(a => a.UserId == userId && (a.Status == AssignmentStatus.Invited || a.Status == AssignmentStatus.Accepted))
            .Join(db.Users.AsNoTracking(), a => a.UserId, u => u.Id, (a, u) => new { a, u })
            .OrderByDescending(x => x.a.CreatedAt)
            .ToListAsync(ct);
        var linkable = await LinkableAsync(rows.Select(x => x.u.Id).ToList(), ct);
        var events = await EventContextAsync(rows.Select(x => x.a.EventId).Distinct().ToList(), ct);
        return rows.Select(x => ToView(x.a, x.u, linkable, events)).ToList();
    }

    private async Task<AssignmentView> ToViewAsync(EventAssignment a, CancellationToken ct)
    {
        var u = await db.Users.AsNoTracking().FirstAsync(x => x.Id == a.UserId, ct);
        return ToView(a, u, await LinkableAsync([a.UserId], ct), await EventContextAsync([a.EventId], ct));
    }

    /// <summary>Which of these assignees may be shown with a public identity (D-233). Batched — one
    /// call per list, never one per row. Anonymous viewer: an assignment view has no caller identity
    /// threaded through it, so the answer is the same for everyone, exactly as `ProfilePublic` was.
    /// </summary>
    private async Task<IReadOnlySet<Guid>> LinkableAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct)
        => await visibility.VisibleProfileIdsAsync(userIds, null, ct);

    /// <summary>Title/date/host of the events these assignments point at (D-319). Batched for the same
    /// reason <see cref="LinkableAsync"/> is — one query per list, never one per row.</summary>
    private async Task<IReadOnlyDictionary<Guid, EventContext>> EventContextAsync(
        IReadOnlyCollection<Guid> eventIds, CancellationToken ct)
        => await db.Events.AsNoTracking().Where(e => eventIds.Contains(e.Id))
            .Join(db.Organizations.AsNoTracking(), e => e.RepresentingOrgId, o => o.Id,
                (e, o) => new { e.Id, e.Title, e.Slug, e.StartsAt, OrgName = o.Name, o.IsPersonal })
            .ToDictionaryAsync(x => x.Id,
                x => new EventContext(x.Title, x.Slug, x.StartsAt, x.IsPersonal ? null : x.OrgName), ct);

    private sealed record EventContext(string Title, string? Slug, DateTime StartsAt, string? OrgName);

    private static AssignmentView ToView(EventAssignment a, User u, IReadOnlySet<Guid> linkable,
        IReadOnlyDictionary<Guid, EventContext> events)
    {
        var show = linkable.Contains(u.Id);
        // A deleted event can still be pointed at by an old assignment row; the invite stays listed
        // rather than vanishing, so the caller sees what they were invited to and can decline it.
        var ev = events.GetValueOrDefault(a.EventId);
        return new(a.Id, a.EventId, a.OrgId, a.UserId,
            a.Role, a.CustomRole, a.Status.ToString(), a.ShowOnProfile, a.Notes, a.CreatedAt,
            u.Name, show ? u.Username : null, show ? u.AvatarKey : null,
            ev?.Title ?? "(event unavailable)", ev?.Slug, ev?.StartsAt ?? default, ev?.OrgName);
    }
}
