using Kurx.Domain.Enums;

namespace Kurx.Application.Abstractions;

/// <summary>OrgUnit tree operations (V3 §4.1, Phase 4). The root unit is materialised on first need and
/// the permission chain-walk is the seam Phase 5 (audience rules) and Phase 6 (participants) build
/// unit-scoped grants on. No public API this phase — events auto-bind their owning unit internally.</summary>
public interface IOrgUnitService
{
    /// <summary>Returns the org's root unit id, materialising it on first need (idempotent). The new unit
    /// is added to the caller's DbContext and committed by the caller's SaveChanges — never on its own.</summary>
    Task<Guid> EnsureRootAsync(Guid orgId, CancellationToken ct = default);

    /// <summary>Effective org role for a user AT an OrgUnit — the union of grants along the
    /// Org → OrgUnit ancestry (V3 §4.1 [B14]), evaluated live per request (D-015), never from a token.
    /// Grants live at the org root today, so a descendant unit inherits the org grant; unit-scoped grants
    /// union in here in Phase 5. Null = no grant anywhere on the chain.</summary>
    Task<OrgRole?> ResolveOrgRoleAsync(Guid userId, Guid orgUnitId, CancellationToken ct = default);
}
