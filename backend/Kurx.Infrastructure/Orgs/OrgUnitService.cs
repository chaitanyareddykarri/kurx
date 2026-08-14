using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Infrastructure.Orgs;

/// <summary>OrgUnit tree (V3 §4.1, Phase 4). Additive and backend-only: it materialises the org's root unit
/// on demand and resolves the permission chain-walk. It deliberately does NOT rewire the existing per-org
/// authz (that is Phase 5's ancestor-union work) — with a one-node tree the walk resolves to the same
/// org-level membership, so nothing changes behaviourally today.</summary>
public class OrgUnitService(KurxDbContext db) : IOrgUnitService
{
    public async Task<Guid> EnsureRootAsync(Guid orgId, CancellationToken ct = default)
    {
        var existing = await db.OrgUnits.AsNoTracking()
            .Where(u => u.OrgId == orgId && u.ParentId == null)
            .Select(u => u.Id).FirstOrDefaultAsync(ct);
        if (existing != Guid.Empty) return existing;

        var name = db.Organizations.Local.FirstOrDefault(o => o.Id == orgId)?.Name
            ?? await db.Organizations.AsNoTracking().Where(o => o.Id == orgId).Select(o => o.Name).FirstOrDefaultAsync(ct)
            ?? "Organization";

        var id = Guid.NewGuid();
        var path = $"/{id}/";
        // Create-or-noop against ix_org_units_one_root_per_org. Two concurrent first-use requests can't produce
        // a duplicate root — the loser lands on ON CONFLICT DO NOTHING — nor surface as an unrelated
        // slug_conflict at the caller's SaveChanges, because the root is settled here in its own statement.
        // An org owning a root before its first event is valid, so committing separately is harmless.
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO org_units ("Id", "OrgId", "ParentId", "Kind", "Name", "Path", "State", "CreatedAt")
            VALUES ({id}, {orgId}, NULL, 'organization', {name}, {path}, 'Active', now())
            ON CONFLICT ("OrgId") WHERE "ParentId" IS NULL DO NOTHING
            """, ct);

        // Our insert won, or a concurrent creator's did — either way return the org's canonical root id.
        return await db.OrgUnits.AsNoTracking()
            .Where(u => u.OrgId == orgId && u.ParentId == null)
            .Select(u => u.Id).FirstAsync(ct);
    }

    public async Task<OrgRole?> ResolveOrgRoleAsync(Guid userId, Guid orgUnitId, CancellationToken ct = default)
    {
        var orgId = await db.OrgUnits.AsNoTracking()
            .Where(u => u.Id == orgUnitId).Select(u => (Guid?)u.OrgId).FirstOrDefaultAsync(ct);
        if (orgId is null) return null;

        // Union along the Org → OrgUnit ancestry. Grants are org-level today (Membership.OrgId), so a
        // descendant unit inherits the org grant. Phase 5 adds unit-scoped memberships and unions them
        // against the ancestor set parsed from OrgUnit.Path — still evaluated live per request (D-015).
        return await db.Memberships.AsNoTracking()
            .Where(m => m.OrgId == orgId && m.UserId == userId)
            .Select(m => (OrgRole?)m.Role).FirstOrDefaultAsync(ct);
    }
}
