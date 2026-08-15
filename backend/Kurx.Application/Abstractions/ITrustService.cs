namespace Kurx.Application.Abstractions;

/// <summary>User-level trust capabilities (M7, D-046), derived LIVE from identity verification (M3),
/// and — once wired — fraud signals (M13). Capability flags are the source of truth; <see cref="Level"/>
/// (L0–L5) is a presentation label over them.</summary>
public record TrustCapabilities(
    string Level,
    bool CanOrganizeFree,
    bool CanOrganizePaid,
    bool CanReceivePayout,
    bool IdentityVerified,
    bool BankVerified,
    /// <summary>May this person create a **public** event — free or paid (D-307).
    ///
    /// <para>Publishing to the public is itself a trust event: a free public event still carries the
    /// platform's name and reaches every user through discovery, so the harm a bad actor can do with one
    /// is not bounded by whether money moved. Bounding verification by *payment* was the wrong axis.</para>
    ///
    /// <para><b>Deliberately a separate field from <see cref="CanOrganizePaid"/>, not an alias.</b> The
    /// two predicates are identical today and that is a coincidence of current requirements, not an
    /// identity — they answer different questions (*may this person publish publicly* vs *may this person
    /// take money*), and one field would mean a future change to either silently moved the other.</para>
    ///
    /// <para>Optional and trailing so every existing construction site compiles unchanged.</para></summary>
    bool CanCreatePublicEvent = false,
    /// <summary>May this person create a **private** event (D-307). Constant `true`: a Private event
    /// cannot be Listed, cannot take payment and appears on no discovery surface, so there is no public
    /// exposure to bound and no money to settle — the financial chain has nothing to verify.
    ///
    /// <para>Carried as a real flag rather than assumed, so both clients render one uniform gate instead
    /// of special-casing a branch, and so a future non-financial requirement has an obvious home.</para></summary>
    bool CanCreatePrivateEvent = true,

    /// <summary>Whether a PUBLIC event must name a verified organization (D-353). True in Production,
    /// always. False only under D-352's bypass, where no admin-approved organization exists to name and
    /// the whole public/paid flow would otherwise be untestable.
    ///
    /// <para>Carried as a server statement rather than left for each client to assume, because the
    /// clients cannot see the flag and were adding a requirement the server had already lifted — the
    /// gate refused Paid while <c>CreateAsync</c> would have accepted it.</para></summary>
    bool RequiresRepresentation = true);

/// <summary>A user's trust capabilities *in the context of a specific org* (M7). Composes membership
/// verification (M6) with org verification (M5).</summary>
public record OrgTrustCapabilities(bool CanRepresentOrg, bool IsOrgVerifiedRep, bool IsOrgVerified);

/// <summary>Live composition of the verification subsystems into capabilities. Never reads a token or a
/// stored score — always the current DB state — so a revocation/suspension is effective next request.</summary>
public interface ITrustService
{
    Task<TrustCapabilities> GetUserCapabilitiesAsync(Guid userId, CancellationToken ct = default);
    Task<OrgTrustCapabilities> GetOrgCapabilitiesAsync(Guid userId, Guid orgId, CancellationToken ct = default);
}
