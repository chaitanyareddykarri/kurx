using System.Text.Json;
using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Auth;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Infrastructure.Events;

/// <summary>Participants (V3 §5, Phase 6). Assign/list/respond/remove against the platform role registry;
/// management authz is the §5.4 union via <see cref="IEventPermissionService"/> (org grant OR an event
/// ORGANISER participant grant). Additive: V2 <c>EventAssignment</c> and its endpoints are untouched and this
/// backfills from them, so both coexist during the strangler window.</summary>
public class ParticipantService(KurxDbContext db, IEventPermissionService permissions) : IParticipantService
{
    // ── Chat side effects go through the outbox (§17.1), never inline ──────────────────────────
    //
    // These used to be `await chat.X(...)` after SaveChanges. That call reaches the SignalR broadcaster,
    // which reads Redis-backed presence — so a Redis blip threw AFTER the participant row had already
    // committed, and nothing retried. A revoked organiser stayed a chat Host with ban and delete powers,
    // silently and permanently. Staged on the same unit of work as the state change, the two now commit
    // together, and OutboxDispatchJob retries delivery. Both handlers are idempotent, so the random key
    // suffix never needs to dedup — the same idiom as OrderService and SeatBlockService.

    /// <summary>D-300 — a participant joins as a MEMBER, so this is the SAME outbox type a ticket purchase
    /// uses. The `event.chat_join_host` type added by D-299 was deleted rather than left unused: a
    /// participant is not a Host, and a spare type meaning "join as Host" is exactly the thing a future
    /// caller reaches for by accident.</summary>
    private void EnqueueChatJoin(Guid eventId, Guid userId) => db.OutboxMessages.Add(new OutboxMessage
    {
        Type = "event.chat_join",
        PayloadJson = JsonSerializer.Serialize(new { eventId, userId }),
        IdempotencyKey = $"chat_join:{eventId}:{userId}:{Guid.NewGuid():N}",
    });

    private void EnqueueChatStaffRemoved(Guid eventId, Guid userId) => db.OutboxMessages.Add(new OutboxMessage
    {
        Type = "event.chat_staff_removed",
        PayloadJson = JsonSerializer.Serialize(new { eventId, userId }),
        IdempotencyKey = $"chat_staff_removed:{eventId}:{userId}:{Guid.NewGuid():N}",
    });

    public async Task<IReadOnlyList<ParticipantRoleView>> ListRolesAsync(CancellationToken ct = default)
    {
        var roles = await db.ParticipantRoles.AsNoTracking().Where(r => r.OrgId == null).OrderBy(r => r.Sort).ToListAsync(ct);
        return roles.Select(ToRoleView).ToList();
    }

    public async Task<ServiceResult<ParticipantView>> AssignAsync(Guid actorId, Guid orgId, Guid eventId, AssignParticipantInput input, CancellationToken ct = default)
    {
        if (!await db.Events.AnyAsync(e => e.Id == eventId && e.RepresentingOrgId == orgId && e.DeletedAt == null, ct))
            return ServiceResult<ParticipantView>.Fail("not_found");
        if (!await permissions.HasAsync(actorId, eventId, "participants:manage", ct))
            return ServiceResult<ParticipantView>.Fail("forbidden");

        var role = await db.ParticipantRoles.AsNoTracking().FirstOrDefaultAsync(r => r.OrgId == null && r.Slug == input.RoleSlug, ct);
        if (role is null) return ServiceResult<ParticipantView>.Fail("invalid_role");

        var visibility = ParticipantVisibility.Internal;
        if (input.Visibility is not null && !Enum.TryParse(input.Visibility, ignoreCase: true, out visibility))
            return ServiceResult<ParticipantView>.Fail("invalid_visibility");

        // Anti-amplification (V3 §5.4): you can never confer an *authority* permission you don't already hold.
        // So an ORGANISER participant with only participants:manage cannot mint owners/managers (which confer
        // event:manage) and escalate. This gates only management authority — assigning a judge or speaker
        // (functional grants like scoring:submit) is a normal appointment, not an escalation.
        string[] conferred = role.DefaultPermissionsJson is null
            ? []
            : JsonSerializer.Deserialize<string[]>(role.DefaultPermissionsJson) ?? [];
        foreach (var perm in conferred)
            if (AuthorityPermissions.Contains(perm) && !await permissions.HasAsync(actorId, eventId, perm, ct))
                return ServiceResult<ParticipantView>.Fail("forbidden");

        // Resolve the subject — exactly one of Person (by phone) or OrgUnit (by id). Team is Phase 10.
        ParticipantSubjectType subjectType;
        Guid subjectId;
        ParticipantState initialState;
        if (input.Phone is not null && input.OrgUnitId is null)
        {
            var normalized = AuthService.NormalizePhone(input.Phone);
            var uid = await db.Users.AsNoTracking().Where(u => u.Phone == normalized).Select(u => (Guid?)u.Id).FirstOrDefaultAsync(ct);
            if (uid is null) return ServiceResult<ParticipantView>.Fail("user_not_found");
            (subjectType, subjectId, initialState) = (ParticipantSubjectType.Person, uid.Value, ParticipantState.Invited);
        }
        else if (input.OrgUnitId is not null && input.Phone is null)
        {
            if (!await db.OrgUnits.AnyAsync(u => u.Id == input.OrgUnitId && u.OrgId == orgId, ct))
                return ServiceResult<ParticipantView>.Fail("invalid_unit");
            (subjectType, subjectId, initialState) = (ParticipantSubjectType.OrgUnit, input.OrgUnitId.Value, ParticipantState.Active);
        }
        else return ServiceResult<ParticipantView>.Fail("invalid_subject");

        var label = string.IsNullOrWhiteSpace(input.CustomLabel) ? null : input.CustomLabel!.Trim();
        var scope = input.WholeEvent ? null : JsonSerializer.Serialize(new { whole_event = false, sub_events = input.SubEvents ?? [] });
        var now = DateTime.UtcNow;

        // Unique per (event, subject, role): reactivate a removed/declined row, or return a live one (idempotent).
        var existing = await db.EventParticipants.FirstOrDefaultAsync(p => p.EventId == eventId
            && p.SubjectType == subjectType && p.SubjectId == subjectId && p.RoleSlug == role.Slug, ct);
        if (existing is not null)
        {
            if (existing.State is not (ParticipantState.Removed or ParticipantState.Declined))
                return ServiceResult<ParticipantView>.Success(ToView(existing, role));
            existing.State = initialState;
            existing.CustomLabel = label; existing.Visibility = visibility; existing.ScopeJson = scope;
            existing.InvitedBy = actorId; existing.AcceptedAt = initialState == ParticipantState.Active ? now : null;
            existing.UpdatedAt = now;
            await db.SaveChangesAsync(ct);
            return ServiceResult<ParticipantView>.Success(ToView(existing, role));
        }

        var p = new EventParticipant
        {
            EventId = eventId, SubjectType = subjectType, SubjectId = subjectId, RoleSlug = role.Slug,
            CustomLabel = label, State = initialState, ScopeJson = scope, Visibility = visibility, InvitedBy = actorId,
            AcceptedAt = initialState == ParticipantState.Active ? now : null,
        };
        db.EventParticipants.Add(p);
        db.AuditLogs.Add(new AuditLog
        {
            ActorType = "user", ActorId = actorId, Action = "participant.assign", Entity = "events", EntityId = eventId,
            DetailsJson = $"{{\"role\":\"{role.Slug}\",\"subject_type\":\"{subjectType}\"}}",
        });
        await db.SaveChangesAsync(ct);
        return ServiceResult<ParticipantView>.Success(ToView(p, role));
    }

    public async Task<ServiceResult<IReadOnlyList<ParticipantView>>> ListForEventAsync(Guid actorId, Guid eventId, CancellationToken ct = default)
    {
        if (!await permissions.HasAsync(actorId, eventId, "participants:manage", ct))
            return ServiceResult<IReadOnlyList<ParticipantView>>.Fail("forbidden");
        var rows = await db.EventParticipants.AsNoTracking()
            .Where(p => p.EventId == eventId && p.State != ParticipantState.Removed)
            .OrderByDescending(p => p.CreatedAt).ToListAsync(ct);
        var roleMap = await RoleMapAsync(ct);
        IReadOnlyList<ParticipantView> views = rows.Select(p => ToView(p, roleMap[p.RoleSlug])).ToList();
        return ServiceResult<IReadOnlyList<ParticipantView>>.Success(views);
    }

    public async Task<ServiceResult<ParticipantView>> RespondAsync(Guid userId, Guid participantId, bool accept, CancellationToken ct = default)
    {
        var p = await db.EventParticipants.FirstOrDefaultAsync(x => x.Id == participantId, ct);
        if (p is null) return ServiceResult<ParticipantView>.Fail("not_found");
        if (p.SubjectType != ParticipantSubjectType.Person || p.SubjectId != userId) return ServiceResult<ParticipantView>.Fail("forbidden");
        if (p.State != ParticipantState.Invited) return ServiceResult<ParticipantView>.Fail("not_pending");

        p.State = accept ? ParticipantState.Active : ParticipantState.Declined;
        if (accept) p.AcceptedAt = DateTime.UtcNow;
        p.UpdatedAt = DateTime.UtcNow;

        var role = await db.ParticipantRoles.AsNoTracking().FirstAsync(r => r.OrgId == null && r.Slug == p.RoleSlug, ct);
        // Working roles (not attendees/observers) get event-chat access on acceptance — mirrors
        // EventAssignment. As a MEMBER (D-300): Content is speakers and Evaluation is judges, and both
        // used to arrive as Hosts, so accepting a speaking slot granted the power to ban attendees.
        if (accept && role.Class is ParticipantClass.Organiser or ParticipantClass.Operations
                or ParticipantClass.Content or ParticipantClass.Evaluation)
            EnqueueChatJoin(p.EventId, userId);

        // One SaveChanges, so the state change and the chat side effect commit or fail together.
        await db.SaveChangesAsync(ct);

        return ServiceResult<ParticipantView>.Success(ToView(p, role));
    }

    public async Task<ServiceResult<bool>> RemoveAsync(Guid actorId, Guid eventId, Guid participantId, CancellationToken ct = default)
    {
        if (!await permissions.HasAsync(actorId, eventId, "participants:manage", ct)) return ServiceResult<bool>.Fail("forbidden");
        var p = await db.EventParticipants.FirstOrDefaultAsync(x => x.Id == participantId && x.EventId == eventId, ct);
        if (p is null) return ServiceResult<bool>.Fail("not_found");

        var wasActivePerson = p.State == ParticipantState.Active && p.SubjectType == ParticipantSubjectType.Person;
        var subjectId = p.SubjectId;
        p.State = ParticipantState.Removed;
        p.UpdatedAt = DateTime.UtcNow;

        // Drop chat only if no other active participation for this person still justifies it. `x.Id != p.Id`
        // excludes the row being removed: the check now runs BEFORE SaveChanges, so the database still shows
        // this participation as Active.
        if (wasActivePerson && !await db.EventParticipants.AnyAsync(x => x.Id != p.Id && x.EventId == eventId
                && x.SubjectType == ParticipantSubjectType.Person && x.SubjectId == subjectId && x.State == ParticipantState.Active, ct))
            EnqueueChatStaffRemoved(eventId, subjectId);

        // One SaveChanges. Revocation and its chat consequence commit together or not at all.
        await db.SaveChangesAsync(ct);

        return ServiceResult<bool>.Success(true);
    }

    public async Task<IReadOnlyList<ParticipantView>> ListMineAsync(Guid userId, CancellationToken ct = default)
    {
        var rows = await db.EventParticipants.AsNoTracking()
            .Where(p => p.SubjectType == ParticipantSubjectType.Person && p.SubjectId == userId
                && (p.State == ParticipantState.Invited || p.State == ParticipantState.Accepted || p.State == ParticipantState.Active))
            .OrderByDescending(p => p.CreatedAt).ToListAsync(ct);
        var roleMap = await RoleMapAsync(ct);
        return rows.Select(p => ToView(p, roleMap[p.RoleSlug])).ToList();
    }

    public async Task<int> BackfillFromAssignmentsAsync(CancellationToken ct = default)
    {
        var assignments = await db.EventAssignments.AsNoTracking()
            .Where(a => a.Status == AssignmentStatus.Invited || a.Status == AssignmentStatus.Accepted)
            .ToListAsync(ct);
        if (assignments.Count == 0) return 0;

        // Load the existing person-participant keys once (not per row) — the set also dedups two V2 roles that
        // collapse to one slug for the same person+event. Idempotent + additive: only missing rows are added.
        var seen = (await db.EventParticipants.AsNoTracking()
                .Where(p => p.SubjectType == ParticipantSubjectType.Person)
                .Select(p => new { p.EventId, p.SubjectId, p.RoleSlug }).ToListAsync(ct))
            .Select(x => $"{x.EventId}|{x.SubjectId}|{x.RoleSlug}").ToHashSet(StringComparer.Ordinal);

        var migrated = 0;
        foreach (var a in assignments)
        {
            var slug = ParticipantRoleCatalog.AssignmentRoleMap.TryGetValue(a.Role, out var s) ? s : "staff";
            if (!seen.Add($"{a.EventId}|{a.UserId}|{slug}")) continue;   // already present (in DB or queued this run)

            db.EventParticipants.Add(new EventParticipant
            {
                EventId = a.EventId, SubjectType = ParticipantSubjectType.Person, SubjectId = a.UserId, RoleSlug = slug,
                CustomLabel = a.CustomRole,
                State = a.Status == AssignmentStatus.Accepted ? ParticipantState.Active : ParticipantState.Invited,
                Visibility = a.ShowOnProfile ? ParticipantVisibility.Public : ParticipantVisibility.Internal,
                InvitedBy = a.InvitedBy, AcceptedAt = a.AcceptedAt, CreatedAt = a.CreatedAt,
            });
            migrated++;
        }
        if (migrated > 0) await db.SaveChangesAsync(ct);
        return migrated;
    }

    // Escalation-sensitive grants — subject to the §5.4 anti-amplification check. Functional grants
    // (e.g. scoring:submit) are not authority and are freely appointable by any manager.
    private static readonly HashSet<string> AuthorityPermissions = new(StringComparer.Ordinal) { "participants:manage", "event:manage" };

    private async Task<Dictionary<string, ParticipantRole>> RoleMapAsync(CancellationToken ct)
        => await db.ParticipantRoles.AsNoTracking().Where(r => r.OrgId == null).ToDictionaryAsync(r => r.Slug, r => r, ct);

    private static ParticipantView ToView(EventParticipant p, ParticipantRole role) => new(
        p.Id, p.EventId, p.SubjectType.ToString(), p.SubjectId, p.RoleSlug, role.Class.ToString(), p.CustomLabel,
        p.State.ToString(), p.Visibility.ToString(), role.CountsTowardCapacity, role.InventorySegment, p.CreatedAt);

    private static ParticipantRoleView ToRoleView(ParticipantRole r) => new(
        r.Slug, r.Name, r.Class.ToString(), r.IsPublic, r.CountsTowardCapacity, r.InventorySegment,
        r.DefaultPermissionsJson is null ? [] : JsonSerializer.Deserialize<string[]>(r.DefaultPermissionsJson) ?? []);
}
