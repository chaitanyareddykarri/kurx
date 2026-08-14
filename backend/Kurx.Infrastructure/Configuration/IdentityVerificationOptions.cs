namespace Kurx.Infrastructure.Configuration;

/// <summary>D-323 — whether the identity <b>proofs</b> in the trust matrix are bypassed for a non-production
/// environment.
///
/// <para><b>Why this exists.</b> Government ID, PAN and bank ownership are all proved through
/// <c>IKycProvider</c>, which is still <c>MockKycProvider</c>: DigiLocker always returns approved, and PAN
/// and penny-drop pass for every value not ending <c>0000</c>. So the gate does not currently establish
/// anything — it costs a tester four submissions to reach a verdict the mock was always going to give. Until
/// a real DigiLocker/penny-drop adapter ships, enforcing it buys no assurance and blocks every public-event
/// and paid-checkout flow behind a formality.</para>
///
/// <para><b>What it does NOT bypass.</b> <c>fraudClear</c> — the blacklist and risk-score read (M13/D-052) —
/// is enforced with this flag on exactly as it is with it off. Those are real implementations with no mock
/// behind them, so switching them off would test less rather than more, and would hide a real regression in
/// the one part of the trust matrix that is finished.</para>
///
/// <para><b>It also does not rewrite the facts.</b> <c>TrustCapabilities.IdentityVerified</c> and
/// <c>BankVerified</c> keep reporting what the person has actually proved. Those answer "what is on file";
/// the capability flags answer "may this person act". The bypass relaxes the second and must never forge
/// the first — a profile that claims a verified identity nobody checked is the exact false assurance the
/// trust system exists to prevent.</para>
///
/// <para><b>Fails closed.</b> <c>AddKurxInfrastructure</c> throws at startup if this is enabled in
/// Production, the same shape as <c>SIGNING_KEY_PROTECTION=none</c> and a missing
/// <c>REDIS_CONNECTION</c>.</para></summary>
/// <param name="Bypass">True when <c>IDENTITY_VERIFICATION_BYPASS</c> is set outside Production.</param>
public sealed record IdentityVerificationOptions(bool Bypass)
{
    public const string EnvKey = "IDENTITY_VERIFICATION_BYPASS";

    /// <summary>Enforced is the default: an unset, empty or unrecognised value never opens the gate.</summary>
    public static IdentityVerificationOptions Enforced { get; } = new(false);
}
