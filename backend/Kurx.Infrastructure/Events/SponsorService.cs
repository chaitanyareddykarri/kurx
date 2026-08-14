using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Infrastructure.Events;

public class SponsorService(KurxDbContext db, IEventAuthority authority, IAuditWriter audit) : ISponsorService
{
    public async Task<ServiceResult<SponsorView>> CreateAsync(Guid userId, Guid orgId, bool isAdmin, SponsorInput input, CancellationToken ct = default)
    {
        if (!(await authority.ResolveOrgAsync(userId, orgId, isAdmin, ct)).CanManage) return ServiceResult<SponsorView>.Fail("forbidden");
        var name = input.Name.Trim();
        if (name.Length is < 2 or > 150) return ServiceResult<SponsorView>.Fail("invalid_name");

        var tier = SponsorTier.Partner;
        if (input.Tier is not null && !Enum.TryParse(input.Tier, true, out tier)) return ServiceResult<SponsorView>.Fail("invalid_tier");

        var sponsor = new Sponsor
        {
            OrgId = orgId,
            Name = name,
            LogoKey = input.LogoKey,
            Website = input.Website ?? "",
            Tier = tier,
            Priority = input.Priority ?? 0,
        };
        db.Sponsors.Add(sponsor);
        WriteAudit("sponsor.create", sponsor.Id, userId, isAdmin, before: null, after: Snapshot(sponsor));
        await db.SaveChangesAsync(ct);
        return ServiceResult<SponsorView>.Success(ToView(sponsor));
    }

    public async Task<ServiceResult<SponsorView>> UpdateAsync(Guid userId, Guid orgId, Guid sponsorId, bool isAdmin, SponsorInput input, CancellationToken ct = default)
    {
        var sponsor = await db.Sponsors.FirstOrDefaultAsync(s => s.Id == sponsorId, ct);
        if (sponsor is null) return ServiceResult<SponsorView>.Fail("not_found");
        // The route carries {orgId}; enforce it instead of ignoring it. Mismatch is not_found rather than
        // forbidden so the endpoint never confirms that a sponsor exists under an org the caller guessed.
        if (sponsor.OrgId != orgId) return ServiceResult<SponsorView>.Fail("not_found");
        if (!(await authority.ResolveOrgAsync(userId, sponsor.OrgId, isAdmin, ct)).CanManage) return ServiceResult<SponsorView>.Fail("forbidden");

        var before = Snapshot(sponsor);
        var name = input.Name.Trim();
        if (name.Length is < 2 or > 150) return ServiceResult<SponsorView>.Fail("invalid_name");
        sponsor.Name = name;
        if (input.LogoKey is not null) sponsor.LogoKey = input.LogoKey;
        if (input.Website is not null) sponsor.Website = input.Website;
        if (input.Tier is not null)
        {
            if (!Enum.TryParse<SponsorTier>(input.Tier, true, out var tier)) return ServiceResult<SponsorView>.Fail("invalid_tier");
            sponsor.Tier = tier;
        }
        if (input.Priority is not null) sponsor.Priority = input.Priority.Value;

        WriteAudit("sponsor.update", sponsor.Id, userId, isAdmin, before, Snapshot(sponsor));
        await db.SaveChangesAsync(ct);
        return ServiceResult<SponsorView>.Success(ToView(sponsor));
    }

    public async Task<ServiceResult<bool>> DeleteAsync(Guid userId, Guid orgId, Guid sponsorId, bool isAdmin, CancellationToken ct = default)
    {
        var sponsor = await db.Sponsors.FirstOrDefaultAsync(s => s.Id == sponsorId, ct);
        if (sponsor is null) return ServiceResult<bool>.Fail("not_found");
        if (sponsor.OrgId != orgId) return ServiceResult<bool>.Fail("not_found");
        if (!(await authority.ResolveOrgAsync(userId, sponsor.OrgId, isAdmin, ct)).CanManage) return ServiceResult<bool>.Fail("forbidden");

        // Deliberately a HARD delete (EventSponsor rows cascade), so the audit row is the only surviving
        // record of what was removed — it carries the full prior state for exactly that reason.
        WriteAudit("sponsor.delete", sponsor.Id, userId, isAdmin, before: Snapshot(sponsor), after: null);
        db.Sponsors.Remove(sponsor);
        await db.SaveChangesAsync(ct);
        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<IReadOnlyList<SponsorView>>> ListForOrgAsync(Guid userId, Guid orgId, bool isAdmin, CancellationToken ct = default)
    {
        if (!(await authority.ResolveOrgAsync(userId, orgId, isAdmin, ct)).IsMember && !isAdmin) return ServiceResult<IReadOnlyList<SponsorView>>.Fail("forbidden");
        // The DeletedAt predicate is what makes ix_sponsors_org_active usable — it is a PARTIAL index
        // (WHERE "DeletedAt" IS NULL), so without this the planner cannot prove the rows qualify and falls
        // back to a sequential scan over every sponsor on the platform (verified with EXPLAIN). Deletion is
        // currently hard, so the filter changes no result today; it makes the existing index live and keeps
        // the query correct if soft-delete is ever adopted.
        var sponsors = await db.Sponsors.AsNoTracking().Where(s => s.OrgId == orgId && s.DeletedAt == null)
            .OrderBy(s => s.Tier).ThenBy(s => s.Priority).ToListAsync(ct);
        return ServiceResult<IReadOnlyList<SponsorView>>.Success(sponsors.Select(ToView).ToList());
    }

    public async Task<ServiceResult<bool>> AssignToEventAsync(Guid userId, Guid eventId, bool isAdmin, Guid sponsorId, int? sort, CancellationToken ct = default)
    {
        var ev = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventId, ct);
        if (ev is null) return ServiceResult<bool>.Fail("not_found");
        if (!(await authority.ResolveAsync(userId, ev.Id, isAdmin, ct)).Can(EventPermission.ManageContent)) return ServiceResult<bool>.Fail("forbidden");
        if (!await db.Sponsors.AnyAsync(s => s.Id == sponsorId && s.OrgId == ev.RepresentingOrgId, ct)) return ServiceResult<bool>.Fail("invalid_sponsor");

        var existing = await db.EventSponsors.FirstOrDefaultAsync(es => es.EventId == eventId && es.SponsorId == sponsorId, ct);
        if (existing is null)
        {
            var defaultSort = sort ?? await db.EventSponsors.Where(es => es.EventId == eventId).CountAsync(ct);
            db.EventSponsors.Add(new EventSponsor { EventId = eventId, SponsorId = sponsorId, Sort = defaultSort });
            WriteAudit("sponsor.event.assign", sponsorId, userId, isAdmin,
                before: null, after: new { orgId = ev.RepresentingOrgId, eventId, sponsorId, sort = defaultSort });
        }
        else if (sort is not null)
        {
            var previousSort = existing.Sort;
            existing.Sort = sort.Value;
            // Re-assigning an already-linked sponsor only reorders it; audited as a reorder, not an assign,
            // so the trail does not claim a link was created twice.
            WriteAudit("sponsor.event.reorder", sponsorId, userId, isAdmin,
                before: new { orgId = ev.RepresentingOrgId, eventId, sponsorId, sort = previousSort },
                after: new { orgId = ev.RepresentingOrgId, eventId, sponsorId, sort = sort.Value });
        }

        await db.SaveChangesAsync(ct);
        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<bool>> RemoveFromEventAsync(Guid userId, Guid eventId, bool isAdmin, Guid sponsorId, CancellationToken ct = default)
    {
        var ev = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventId, ct);
        if (ev is null) return ServiceResult<bool>.Fail("not_found");
        if (!(await authority.ResolveAsync(userId, ev.Id, isAdmin, ct)).Can(EventPermission.ManageContent)) return ServiceResult<bool>.Fail("forbidden");

        var link = await db.EventSponsors.FirstOrDefaultAsync(es => es.EventId == eventId && es.SponsorId == sponsorId, ct);
        if (link is null) return ServiceResult<bool>.Fail("not_found");
        WriteAudit("sponsor.event.remove", sponsorId, userId, isAdmin,
            before: new { orgId = ev.RepresentingOrgId, eventId, sponsorId, sort = link.Sort }, after: null);
        db.EventSponsors.Remove(link);
        await db.SaveChangesAsync(ct);
        return ServiceResult<bool>.Success(true);
    }

    public async Task<IReadOnlyList<SponsorView>> ListForEventAsync(Guid eventId, CancellationToken ct = default)
        => await db.EventSponsors.AsNoTracking().Where(es => es.EventId == eventId).OrderBy(es => es.Sort)
            .Join(db.Sponsors, es => es.SponsorId, s => s.Id, (es, s) => s)
            .Select(s => new SponsorView(s.Id, s.OrgId, s.Name, s.LogoKey, s.Website, s.Tier.ToString(), s.Priority))
            .ToListAsync(ct);

    private static SponsorView ToView(Sponsor s) => new(s.Id, s.OrgId, s.Name, s.LogoKey, s.Website, s.Tier.ToString(), s.Priority);

    /// <summary>Audit payload for a sponsor. Carries the owning org so the trail is queryable per
    /// organisation, and no PII — a sponsor is a company, not a person (D-102 redaction rule).</summary>
    private static object Snapshot(Sponsor s) => new
    {
        orgId = s.OrgId, name = s.Name, tier = s.Tier.ToString(), priority = s.Priority,
        website = s.Website, logoKey = s.LogoKey,
    };

    /// <summary>Staged on the caller's unit of work, so the audit row commits in the SAME transaction as
    /// the change it describes — see <see cref="IAuditWriter"/>. `admin` marks the cross-org kurx_admin
    /// bypass, which is the case an auditor most needs to see.</summary>
    private void WriteAudit(string action, Guid sponsorId, Guid actorId, bool isAdmin, object? before, object? after)
        => audit.Write(new AuditEvent(action, "sponsors", sponsorId,
            ActorType: isAdmin ? "admin" : "user", ActorId: actorId, Before: before, After: after));
}
