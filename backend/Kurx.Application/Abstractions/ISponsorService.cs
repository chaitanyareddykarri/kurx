namespace Kurx.Application.Abstractions;

public record SponsorInput(string Name, string? LogoKey, string? Website, string? Tier, int? Priority);

public record SponsorView(Guid Id, Guid OrgId, string Name, string? LogoKey, string Website, string Tier, int Priority);

/// <summary>Org-scoped sponsor library, reusable across events. Owner/Manager manage; any org member reads.</summary>
public interface ISponsorService
{
    Task<ServiceResult<SponsorView>> CreateAsync(Guid userId, Guid orgId, bool isAdmin, SponsorInput input, CancellationToken ct = default);

    /// <summary><paramref name="orgId"/> comes from the route and IS enforced: a sponsor that belongs to a
    /// different org returns not_found, so the URL cannot claim a scope it does not apply.</summary>
    Task<ServiceResult<SponsorView>> UpdateAsync(Guid userId, Guid orgId, Guid sponsorId, bool isAdmin, SponsorInput input, CancellationToken ct = default);

    /// <summary>Route <paramref name="orgId"/> is enforced (see <see cref="UpdateAsync"/>). Hard delete —
    /// EventSponsor rows cascade — so the audit row carries the full prior state.</summary>
    Task<ServiceResult<bool>> DeleteAsync(Guid userId, Guid orgId, Guid sponsorId, bool isAdmin, CancellationToken ct = default);

    Task<ServiceResult<IReadOnlyList<SponsorView>>> ListForOrgAsync(Guid userId, Guid orgId, bool isAdmin, CancellationToken ct = default);

    /// <summary>Assigns a sponsor to an event; sort defaults to end-of-list unless given.</summary>
    Task<ServiceResult<bool>> AssignToEventAsync(Guid userId, Guid eventId, bool isAdmin, Guid sponsorId, int? sort, CancellationToken ct = default);

    Task<ServiceResult<bool>> RemoveFromEventAsync(Guid userId, Guid eventId, bool isAdmin, Guid sponsorId, CancellationToken ct = default);

    Task<IReadOnlyList<SponsorView>> ListForEventAsync(Guid eventId, CancellationToken ct = default);
}
