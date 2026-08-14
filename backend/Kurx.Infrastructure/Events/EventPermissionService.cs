using System.Text.Json;
using Kurx.Application.Abstractions;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Infrastructure.Events;

/// <summary>Effective event-scoped permission (V3 §5.4, Phase 6). Additive: it unions the caller's org grant
/// with a NEW fourth source — ACTIVE participant grants on the event — evaluated live. It does NOT rewire the
/// D-269 org authz (that is now IEventAuthority, which this delegates to); it is the resolver the participant surface consults so an
/// event ORGANISER participant can act even without an org membership. A participant grant is strictly
/// event-scoped and never leaks to org level (§5.4 rule 1).</summary>
public class EventPermissionService(KurxDbContext db, IEventAuthority authority) : IEventPermissionService
{
    // Grants an EventAuthorityLevel.Manager caller holds — the event's creator (D-268), a Representative,
    // or an Owner/Manager seat in the represented organization. Resolved by IEventAuthority (D-269).
    private static readonly string[] ManagerGrants = ["participants:manage", "event:manage"];

    public async Task<bool> HasAsync(Guid userId, Guid eventId, string permission, CancellationToken ct = default)
    {
        // (0)+(1) Owner and organization grants, both from the single event authority (D-269). It resolves
        // creator-first (D-268), so an owner who represents nobody still holds every manager grant here.
        var access = await authority.ResolveAsync(userId, eventId, isAdmin: false, ct);
        if (!access.EventExists) return false;
        if (access.Can(EventPermission.ManageContent) && ManagerGrants.Contains(permission)) return true;

        // (2) Participant grant (§5.4) — an ACTIVE participation whose role's default_permissions include it.
        var slugs = await db.EventParticipants.AsNoTracking()
            .Where(p => p.EventId == eventId && p.SubjectType == ParticipantSubjectType.Person
                && p.SubjectId == userId && p.State == ParticipantState.Active)
            .Select(p => p.RoleSlug).Distinct().ToListAsync(ct);
        if (slugs.Count == 0) return false;

        var grants = await db.ParticipantRoles.AsNoTracking()
            .Where(r => r.OrgId == null && slugs.Contains(r.Slug) && r.DefaultPermissionsJson != null)
            .Select(r => r.DefaultPermissionsJson!).ToListAsync(ct);
        return grants.Any(json => (JsonSerializer.Deserialize<string[]>(json) ?? []).Contains(permission));
    }
}
