namespace Kurx.Application.Abstractions;

public record VenueInput(string Name, string? Address, string? City, double? Lat, double? Lng,
    string? GoogleMapsUrl, int? Capacity, bool? HasParking, bool? IsAccessible, string? Notes);

public record VenueView(Guid Id, Guid OrgId, string Name, string Address, string City, double? Lat, double? Lng,
    string? GoogleMapsUrl, int? Capacity, bool HasParking, bool IsAccessible, string Notes, IReadOnlyList<string> ImageKeys);

/// <summary>Org-scoped venue library, reusable across events. Owner/Manager manage; any org member reads.</summary>
public interface IVenueService
{
    Task<ServiceResult<VenueView>> CreateAsync(Guid userId, Guid orgId, bool isAdmin, VenueInput input, CancellationToken ct = default);

    Task<ServiceResult<VenueView>> UpdateAsync(Guid userId, Guid venueId, bool isAdmin, VenueInput input, CancellationToken ct = default);

    Task<ServiceResult<bool>> DeleteAsync(Guid userId, Guid venueId, bool isAdmin, CancellationToken ct = default);

    Task<ServiceResult<VenueView>> GetAsync(Guid userId, Guid venueId, bool isAdmin, CancellationToken ct = default);

    Task<ServiceResult<IReadOnlyList<VenueView>>> ListForOrgAsync(Guid userId, Guid orgId, bool isAdmin, CancellationToken ct = default);

    Task<ServiceResult<bool>> AddImageAsync(Guid userId, Guid venueId, bool isAdmin, string key, CancellationToken ct = default);

    Task<ServiceResult<bool>> RemoveImageAsync(Guid userId, Guid venueId, bool isAdmin, Guid imageId, CancellationToken ct = default);
}
