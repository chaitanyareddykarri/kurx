using System.Linq.Expressions;
using System.Text;
using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Infrastructure.Orgs;

/// <summary>Organization registry search (M4, D-043). Matches on exact normalized name, primary domain,
/// pg_trgm fuzzy name similarity, and aliases — resolving every hit to the org's canonical row so
/// future organizers reuse one entity. Read-only; org creation lives in <c>OrgService</c>.</summary>
public class OrganizationRegistryService(KurxDbContext db) : IOrganizationRegistryService
{
    public async Task<IReadOnlyList<OrgSearchResult>> SearchAsync(string query, int limit = 10, CancellationToken ct = default)
    {
        var normalized = Normalize(query);
        if (normalized.Length == 0) return [];
        limit = Math.Clamp(limit, 1, 50);
        var domain = query.Trim().ToLowerInvariant();

        // D-074/D-075: the registry is the *verified* org registry. Only admin-approved (Verified) orgs are
        // discoverable — one predicate excludes personal orgs, staged representation-request placeholders
        // (PendingReview), and rejected/suspended/blacklisted orgs, replacing the old IsPersonal special-case.
        Expression<Func<Organization, bool>> discoverable = o => o.VerificationStatus == OrgVerificationStatus.Verified;

        // Direct org matches: exact normalized name, domain, or trigram-similar name.
        var orgRows = await db.Organizations.AsNoTracking()
            .Where(o => o.DeletedAt == null && o.CanonicalOrgId == o.Id)
            .Where(discoverable)
            .Where(o => o.NormalizedName == normalized
                     || o.PrimaryDomain == domain
                     || EF.Functions.TrigramsAreSimilar(o.NormalizedName, normalized))
            .Select(o => new
            {
                o.Id, o.Name, o.Slug, o.LogoKey, o.Type, o.PrimaryDomain, o.NormalizedName, o.VerificationStatus,
                Sim = EF.Functions.TrigramsSimilarity(o.NormalizedName, normalized),
            })
            .ToListAsync(ct);

        // Alias matches → resolve to the canonical org.
        var aliasRows = await db.OrganizationAliases.AsNoTracking()
            .Where(a => a.NormalizedAlias == normalized || EF.Functions.TrigramsAreSimilar(a.NormalizedAlias, normalized))
            .Join(db.Organizations.Where(o => o.DeletedAt == null && o.CanonicalOrgId == o.Id).Where(discoverable),
                a => a.OrgId, o => o.Id,
                (a, o) => new
                {
                    o.Id, o.Name, o.Slug, o.LogoKey, o.Type, o.PrimaryDomain, o.VerificationStatus,
                    Sim = EF.Functions.TrigramsSimilarity(a.NormalizedAlias, normalized),
                    Exact = a.NormalizedAlias == normalized,
                })
            .ToListAsync(ct);

        IEnumerable<Hit> Hits()
        {
            foreach (var o in orgRows)
                yield return o.PrimaryDomain == domain
                    ? new Hit(o.Id, o.Name, o.Slug, o.LogoKey, o.Type, o.PrimaryDomain, o.VerificationStatus, 1.0, "domain")
                    : o.NormalizedName == normalized
                        ? new Hit(o.Id, o.Name, o.Slug, o.LogoKey, o.Type, o.PrimaryDomain, o.VerificationStatus, 1.0, "exact")
                        : new Hit(o.Id, o.Name, o.Slug, o.LogoKey, o.Type, o.PrimaryDomain, o.VerificationStatus, o.Sim, "name");
            foreach (var a in aliasRows)
                yield return new Hit(a.Id, a.Name, a.Slug, a.LogoKey, a.Type, a.PrimaryDomain, a.VerificationStatus,
                    a.Exact ? 0.95 : a.Sim, "alias");
        }

        return Hits()
            .GroupBy(h => h.Id)
            .Select(g => g.OrderByDescending(h => h.Score).First())    // best match per org
            .OrderByDescending(h => h.Score)
            .Take(limit)
            .Select(h => new OrgSearchResult(h.Id, h.Name, h.Slug, h.LogoKey, h.Type.ToString(),
                h.PrimaryDomain, h.VerificationStatus.ToString(), h.Score, h.MatchKind))
            .ToList();
    }

    private record Hit(Guid Id, string Name, string Slug, string? LogoKey, OrganizationType Type,
        string? PrimaryDomain, OrgVerificationStatus VerificationStatus, double Score, string MatchKind);

    /// <summary>Lowercase, non-alphanumeric → single space, collapse, trim. Shared by search and by
    /// org creation (which stores it). "N.S.R.I.T" / "NSRIT " both normalize to "nsrit"; acronym↔full
    /// name resolution comes from aliases, not fuzzy name matching.</summary>
    public static string Normalize(string s)
    {
        var sb = new StringBuilder(s.Length);
        var pendingSpace = false;
        foreach (var ch in s.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch))
            {
                if (pendingSpace && sb.Length > 0) sb.Append(' ');
                sb.Append(ch);
                pendingSpace = false;
            }
            else
            {
                pendingSpace = true;
            }
        }
        return sb.ToString();
    }
}
