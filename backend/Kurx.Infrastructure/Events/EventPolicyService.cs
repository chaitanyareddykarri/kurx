using System.Text.Json;
using Kurx.Application.Abstractions;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Infrastructure.Events;

/// <summary>D-266 M3 Step 3 — resolves one event's policy requirements.
///
/// <para><b>The stored per-type allow-list is the runtime source of truth.</b> Archetype defaults exist
/// only to generate those values during seeding; this service never consults
/// <see cref="RegistrationPolicySeeder"/> or an archetype default at request time. If a type's list is
/// edited by an admin (D-188), the change takes effect immediately and nothing silently re-derives it.</para>
///
/// <para>Every value returned is a projection of one <see cref="PolicyResolver.PolicyResolution"/> —
/// publish requirements, reviewer requirements, gates and validation all come from the same resolve call,
/// so they cannot disagree about whether an event is publishable.</para>
///
/// <para>Performs no authorization. Who may read or edit this event is D-269's concern; the endpoint does
/// the event read first and this service is only reached once that succeeded.</para></summary>
public class EventPolicyService(KurxDbContext db) : IEventPolicyService
{
    public async Task<ServiceResult<PolicyRequirementsView>> GetForEventAsync(Guid eventId, CancellationToken ct = default)
    {
        var ev = await db.Events.AsNoTracking()
            .Where(e => e.Id == eventId)
            .Select(e => new { e.Id, e.TypeId, e.Product, e.Visibility, e.IsPaid, e.RegistrationPolicy,
                               e.RepresentingOrgId, e.ArchetypeSlug, e.FinancialReviewStatus })
            .FirstOrDefaultAsync(ct);
        if (ev is null) return ServiceResult<PolicyRequirementsView>.Fail("not_found");

        // Runtime truth: the stored column, not a re-derivation from the archetype.
        var storedJson = ev.TypeId is null ? null : await db.EventCategories.AsNoTracking()
            .Where(c => c.Id == ev.TypeId)
            .Select(c => c.AllowedRegistrationPoliciesJson)
            .FirstOrDefaultAsync(ct);

        var allowList = Parse(storedJson);

        // D-266 M5 — the representation facts are read here and decided in PolicyResolver. Gathering them
        // in the service keeps the resolver pure; deciding them in the resolver keeps the rule in the one
        // place both checklists and the transition gate already project from.
        // "Personal" is a persistence row, never a domain organization (D-268), so a self-represented event
        // represents no third party and there is nobody whose consent could be required.
        var representsInstitution = await db.Organizations.AsNoTracking()
            .AnyAsync(o => o.Id == ev.RepresentingOrgId && !o.IsPersonal, ct);

        // One read of the archetype's rule flags rather than a query per rule.
        var archetype = ev.ArchetypeSlug is null ? null : await db.EventArchetypes.AsNoTracking()
            .Where(a => a.Slug == ev.ArchetypeSlug)
            .Select(a => new { a.RequiresRepresentation, a.RequiresFinancialReview })
            .FirstOrDefaultAsync(ct);

        var facts = new PolicyResolver.RepresentationFacts(
            RepresentsInstitution: representsInstitution,
            HasApprovedAuthorization: representsInstitution && await db.EventAuthorizations.AsNoTracking()
                .AnyAsync(a => a.EventId == ev.Id && a.Status == EventAuthorizationStatus.Approved, ct),
            ArchetypeRequiresRepresentation: archetype?.RequiresRepresentation ?? false,
            ArchetypeRequiresFinancialReview: archetype?.RequiresFinancialReview ?? false,
            FinancialReviewPassed: ev.FinancialReviewStatus == FinancialReviewStatus.Passed);

        var r = PolicyResolver.Resolve(ev.Product, allowList, ev.RegistrationPolicy, ev.Visibility, ev.IsPaid, facts);

        return ServiceResult<PolicyRequirementsView>.Success(new PolicyRequirementsView(
            EventId: ev.Id,
            Product: ev.Product.ToString(),
            SelectedPolicy: r.Selected.ToString(),
            AllowedPolicies: r.Allowed.Select(p => p.ToString()).ToList(),
            // Derived, never stored: the gates are a function of the selected policy alone.
            RegistrationGates: r.Gates.Select(g => g.ToString()).ToList(),
            IdentityRequirement: r.Identity.ToString(),
            RegistrationRequirements: r.Requirements,
            // Both checklists project the same resolution. PublishBlockers is what stops a publish;
            // ReviewerChecklist is what a reviewer works through. Same array, two views.
            PublishBlockers: PolicyResolver.PublishBlockers(r),
            ReviewerChecklist: PolicyResolver.ReviewerChecklist(r),
            IsValid: r.Violations.Count == 0));
    }

    /// <summary>A null or unparseable column means "no allow-list stored yet", which the resolver treats as
    /// "the product's limits still apply" — never as "anything goes".</summary>
    private static IReadOnlyList<EventRegistrationPolicy>? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<string[]>(json)
                ?.Select(s => Enum.TryParse<EventRegistrationPolicy>(s, out var p) ? (EventRegistrationPolicy?)p : null)
                .Where(p => p is not null).Select(p => p!.Value).ToList();
        }
        catch (JsonException) { return null; }
    }
}
