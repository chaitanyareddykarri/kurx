using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Infrastructure.Events;

public class VenueService(KurxDbContext db, IEventAuthority authority, IAuditWriter audit) : IVenueService
{
    public async Task<ServiceResult<VenueView>> CreateAsync(Guid userId, Guid orgId, bool isAdmin, VenueInput input, CancellationToken ct = default)
    {
        if (!(await authority.ResolveOrgAsync(userId, orgId, isAdmin, ct)).CanManage) return ServiceResult<VenueView>.Fail("forbidden");
        var name = input.Name.Trim();
        if (name.Length is < 2 or > 150) return ServiceResult<VenueView>.Fail("invalid_name");

        var venue = new Venue
        {
            OrgId = orgId,
            Name = name,
            Address = input.Address?.Trim() ?? "",
            City = input.City?.Trim() ?? "",
            Lat = input.Lat,
            Lng = input.Lng,
            GoogleMapsUrl = input.GoogleMapsUrl,
            Capacity = input.Capacity,
            HasParking = input.HasParking ?? false,
            IsAccessible = input.IsAccessible ?? false,
            Notes = input.Notes ?? "",
        };
        db.Venues.Add(venue);
        WriteAudit("venue.create", venue.Id, userId, isAdmin, before: null, after: Snapshot(venue));
        await db.SaveChangesAsync(ct);
        return ServiceResult<VenueView>.Success(ToView(venue, []));
    }

    public async Task<ServiceResult<VenueView>> UpdateAsync(Guid userId, Guid venueId, bool isAdmin, VenueInput input, CancellationToken ct = default)
    {
        var venue = await db.Venues.FirstOrDefaultAsync(v => v.Id == venueId, ct);
        if (venue is null) return ServiceResult<VenueView>.Fail("not_found");
        if (!(await authority.ResolveOrgAsync(userId, venue.OrgId, isAdmin, ct)).CanManage) return ServiceResult<VenueView>.Fail("forbidden");

        var before = Snapshot(venue);
        var name = input.Name.Trim();
        if (name.Length is < 2 or > 150) return ServiceResult<VenueView>.Fail("invalid_name");
        venue.Name = name;
        if (input.Address is not null) venue.Address = input.Address.Trim();
        if (input.City is not null) venue.City = input.City.Trim();
        if (input.Lat is not null) venue.Lat = input.Lat;
        if (input.Lng is not null) venue.Lng = input.Lng;
        if (input.GoogleMapsUrl is not null) venue.GoogleMapsUrl = input.GoogleMapsUrl;
        if (input.Capacity is not null) venue.Capacity = input.Capacity;
        if (input.HasParking is not null) venue.HasParking = input.HasParking.Value;
        if (input.IsAccessible is not null) venue.IsAccessible = input.IsAccessible.Value;
        if (input.Notes is not null) venue.Notes = input.Notes;

        WriteAudit("venue.update", venue.Id, userId, isAdmin, before, Snapshot(venue));
        await db.SaveChangesAsync(ct);
        var images = await ImageKeysAsync(venueId, ct);
        return ServiceResult<VenueView>.Success(ToView(venue, images));
    }

    public async Task<ServiceResult<bool>> DeleteAsync(Guid userId, Guid venueId, bool isAdmin, CancellationToken ct = default)
    {
        var venue = await db.Venues.FirstOrDefaultAsync(v => v.Id == venueId, ct);
        if (venue is null) return ServiceResult<bool>.Fail("not_found");
        if (!(await authority.ResolveOrgAsync(userId, venue.OrgId, isAdmin, ct)).CanManage) return ServiceResult<bool>.Fail("forbidden");
        if (await db.Events.AnyAsync(e => e.VenueId == venueId, ct)) return ServiceResult<bool>.Fail("venue_in_use");

        WriteAudit("venue.delete", venue.Id, userId, isAdmin, before: Snapshot(venue), after: null);
        db.Venues.Remove(venue);
        await db.SaveChangesAsync(ct);
        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<VenueView>> GetAsync(Guid userId, Guid venueId, bool isAdmin, CancellationToken ct = default)
    {
        var venue = await db.Venues.AsNoTracking().FirstOrDefaultAsync(v => v.Id == venueId, ct);
        if (venue is null) return ServiceResult<VenueView>.Fail("not_found");
        if (!(await authority.ResolveOrgAsync(userId, venue.OrgId, isAdmin, ct)).IsMember && !isAdmin) return ServiceResult<VenueView>.Fail("forbidden");

        return ServiceResult<VenueView>.Success(ToView(venue, await ImageKeysAsync(venueId, ct)));
    }

    public async Task<ServiceResult<IReadOnlyList<VenueView>>> ListForOrgAsync(Guid userId, Guid orgId, bool isAdmin, CancellationToken ct = default)
    {
        if (!(await authority.ResolveOrgAsync(userId, orgId, isAdmin, ct)).IsMember && !isAdmin) return ServiceResult<IReadOnlyList<VenueView>>.Fail("forbidden");

        var venues = await db.Venues.AsNoTracking().Where(v => v.OrgId == orgId).OrderBy(v => v.Name).ToListAsync(ct);
        var images = await db.VenueImages.AsNoTracking().Where(i => venues.Select(v => v.Id).Contains(i.VenueId))
            .OrderBy(i => i.Sort).ToListAsync(ct);
        var byVenue = images.GroupBy(i => i.VenueId).ToDictionary(g => g.Key, g => g.Select(i => i.Key).ToList());
        return ServiceResult<IReadOnlyList<VenueView>>.Success(
            venues.Select(v => ToView(v, byVenue.TryGetValue(v.Id, out var keys) ? keys : [])).ToList());
    }

    public async Task<ServiceResult<bool>> AddImageAsync(Guid userId, Guid venueId, bool isAdmin, string key, CancellationToken ct = default)
    {
        var venue = await db.Venues.AsNoTracking().FirstOrDefaultAsync(v => v.Id == venueId, ct);
        if (venue is null) return ServiceResult<bool>.Fail("not_found");
        if (!(await authority.ResolveOrgAsync(userId, venue.OrgId, isAdmin, ct)).CanManage) return ServiceResult<bool>.Fail("forbidden");

        var sort = await db.VenueImages.Where(i => i.VenueId == venueId).CountAsync(ct);
        db.VenueImages.Add(new VenueImage { VenueId = venueId, Key = key, Sort = sort });
        await db.SaveChangesAsync(ct);
        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<bool>> RemoveImageAsync(Guid userId, Guid venueId, bool isAdmin, Guid imageId, CancellationToken ct = default)
    {
        var venue = await db.Venues.AsNoTracking().FirstOrDefaultAsync(v => v.Id == venueId, ct);
        if (venue is null) return ServiceResult<bool>.Fail("not_found");
        if (!(await authority.ResolveOrgAsync(userId, venue.OrgId, isAdmin, ct)).CanManage) return ServiceResult<bool>.Fail("forbidden");

        var image = await db.VenueImages.FirstOrDefaultAsync(i => i.Id == imageId && i.VenueId == venueId, ct);
        if (image is null) return ServiceResult<bool>.Fail("not_found");
        db.VenueImages.Remove(image);
        await db.SaveChangesAsync(ct);
        return ServiceResult<bool>.Success(true);
    }

    private async Task<List<string>> ImageKeysAsync(Guid venueId, CancellationToken ct)
        => await db.VenueImages.AsNoTracking().Where(i => i.VenueId == venueId).OrderBy(i => i.Sort).Select(i => i.Key).ToListAsync(ct);

    /// <summary>Audit payload — venue identity and location, no PII (D-102 redaction rule).</summary>
    private static object Snapshot(Venue v) => new
    {
        orgId = v.OrgId, name = v.Name, address = v.Address, city = v.City, capacity = v.Capacity,
    };

    private void WriteAudit(string action, Guid venueId, Guid actorId, bool isAdmin, object? before, object? after)
        => audit.Write(new AuditEvent(action, "venues", venueId,
            ActorType: isAdmin ? "admin" : "user", ActorId: actorId, Before: before, After: after));

    private static VenueView ToView(Venue v, IReadOnlyList<string> images) => new(v.Id, v.OrgId, v.Name, v.Address,
        v.City, v.Lat, v.Lng, v.GoogleMapsUrl, v.Capacity, v.HasParking, v.IsAccessible, v.Notes, images);
}
