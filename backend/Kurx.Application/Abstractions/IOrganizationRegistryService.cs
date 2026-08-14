namespace Kurx.Application.Abstractions;

/// <summary>A candidate organization returned by registry search. <paramref name="Score"/> is the
/// trigram similarity (1.0 for an exact normalized-name or domain match); <paramref name="MatchKind"/>
/// is how it matched (exact | domain | name | alias).</summary>
public record OrgSearchResult(
    Guid Id, string Name, string Slug, string? LogoKey, string Type, string? PrimaryDomain,
    string VerificationStatus, double Score, string MatchKind);

/// <summary>Organization registry (M4, D-043): fuzzy, alias- and domain-aware search so an organizer
/// finds and reuses an existing org instead of creating a duplicate (the "NSRIT ↔ full name" rule).</summary>
public interface IOrganizationRegistryService
{
    Task<IReadOnlyList<OrgSearchResult>> SearchAsync(string query, int limit = 10, CancellationToken ct = default);
}
