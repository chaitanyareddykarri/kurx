using Kurx.Application.Abstractions;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Configuration;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Infrastructure.Trust;

/// <summary>Composes the verification subsystems into capabilities, read LIVE per request (M7, D-046).
///
/// <para><b>The money bar (D-298):</b> creating a paid event and receiving a payout both require
/// <c>identity ∧ PAN ∧ bank ∧ penny-drop-passed ∧ name-not-mismatched ∧ fraud-clear</c>. Previously
/// payout asked only whether a bank account had been recorded, so a person could be paid having
/// proved nothing about who they were; and every check read the presence of a masked value rather
/// than a verification outcome.</para>
///
/// <para><b>Migration.</b> The <c>AddIdentityComponentStatusAndPennyDrop</c> backfill derived each
/// component from evidence already on the row — a masked value is only ever written on provider
/// approval — so anyone who had completed PAN and bank stays eligible with no action. Two cohorts
/// lose eligibility and both are correct to lose it: users verified by government ID with no PAN
/// (cannot be tax-reported), and users whose bank was recorded without a passed drop (impossible via
/// the current path, possible for rows predating it). Both see the exact missing step in the
/// Verification section rather than a silent refusal at checkout.</para>
/// Capability flags are the source of truth; the L0–L5 label is derived from them. Gates (event
/// approval M8, payouts M10) call this rather than re-deriving the rules.
///
/// <para>Fraud-clear is read live from <c>IFraudService</c> (M13): a blacklisted or high-risk user
/// fails it, which drops <c>CanOrganizePaid</c> and flows into the event-publish (M8) and payment (M10)
/// gates on the next request.</para></summary>
public class TrustService(KurxDbContext db, IFraudService fraud, IdentityVerificationOptions identityOptions)
    : ITrustService
{
    public async Task<TrustCapabilities> GetUserCapabilitiesAsync(Guid userId, CancellationToken ct = default)
    {
        var identity = await db.UserIdentities.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId, ct);

        // Gates read per-component STATUS, not the presence of a masked value and not the coarse
        // Level (M3, D-042). Presence was always a weaker test than it looked: `BankLast4 != null`
        // meant "an account number was recorded", which is not the same claim as "that account
        // exists, is live, and belongs to this person".
        var identityVerified = identity is not null
            && (identity.GovtIdStatus == IdentityStatus.Approved || identity.PanStatus == IdentityStatus.Approved);

        // PAN is its own requirement for taking money, not a substitute for government ID. Indian
        // tax reporting is keyed on PAN, so a payout to someone who has only proved a passport is a
        // payment the platform cannot account for.
        var panVerified = identity?.PanStatus == IdentityStatus.Approved;

        // A bank account is proved by the penny drop landing AND the holder name matching — the two
        // together are what distinguish "this account is real and theirs" from "these digits parse".
        // A mismatch is disqualifying on its own: it is the signal that a PAN and an account belong
        // to two different people, which is the exact fraud this check exists to catch.
        var bankVerified = identity is not null
            && identity.BankStatus == IdentityStatus.Approved
            && identity.PennyDropStatus == PennyDropStatus.Passed
            && identity.BankNameMatch != NameMatchStatus.Mismatch;

        var fraudClear = await fraud.IsUserClearAsync(userId, ct);   // live blocklist + risk-score read (M13)

        // D-343 — the proofs are TWO tiers, split by what each one actually establishes, because the two
        // questions they answer are different:
        //
        //   identity  — "who is behind this event"      → govt ID or PAN
        //   financial — "whose account receives money"  → PAN + bank + penny drop + holder-name match
        //
        // They were one lump (`proofsSatisfied`), which made a FREE public event prove ownership of a bank
        // account that would never receive a rupee — four submissions to establish a fact the event cannot
        // use. D-307's reasoning is kept intact and is the reason the identity tier still gates Public: a
        // free public event carries the platform's name into discovery, so the harm it can do is not bounded
        // by whether money moved. What D-307 over-applied was the FINANCIAL half; nothing settles on a free
        // event, so there is no account to own.
        //
        // D-323 — every proof is answered by MockKycProvider today (DigiLocker always approves; PAN and
        // penny-drop pass for anything not ending "0000"), so outside Production the gate costs submissions
        // and establishes nothing. The flag relaxes the PROOFS only; `fraudClear` is deliberately outside it
        // and is still ANDed into all three capabilities below, because the blacklist and risk engine are
        // real implementations and switching them off would test less rather than more. Refused at startup
        // in Production (DependencyInjection). Reasoning: IdentityVerificationOptions.
        var identityProofs = identityOptions.Bypass || identityVerified;
        var financialProofs = identityOptions.Bypass || (identityVerified && panVerified && bankVerified);

        var canOrganizeFree = true;                                       // any account: free/private events, no KYC

        // Selling and settling carry the SAME bar. They used to differ — payout asked only for a bank
        // account — so a user could receive money having proved nothing about who they were. Both are
        // "the platform is moving this person's money", and both require the full financial tier.
        var canOrganizePaid = financialProofs && fraudClear;
        var canReceivePayout = canOrganizePaid;

        // Identity tier only (D-343). PAN is deliberately NOT required here: PAN is a tax identity, a free
        // event reports no income, and demanding it would ask for a tax document for a non-taxable act —
        // which also locks out anyone holding a passport or Aadhaar but no PAN. Written out rather than
        // assigned from `identityProofs` so the predicate stays readable next to the one below it.
        var canCreatePublicEvent = identityProofs && fraudClear;

        // Constant true: a Private event cannot be Listed, cannot take payment and appears on no discovery
        // surface (PolicyResolver + nine services), so there is no exposure to bound and no money to
        // settle. Carried as a flag anyway so both clients render one uniform gate.
        const bool canCreatePrivateEvent = true;

        // Label over the flags. L0 (guest) never applies to an authenticated user; L3+ need org context.
        var level = canOrganizePaid ? "L2" : "L1";

        // D-353/D-352 — the representation rule is real logic, but it needs an admin-approved organization
        // to satisfy, which is exactly the kind of human-reviewed gate the bypass exists to lift.
        var requiresRepresentation = !identityOptions.Bypass;

        return new TrustCapabilities(level, canOrganizeFree, canOrganizePaid, canReceivePayout,
            identityVerified, bankVerified, canCreatePublicEvent, canCreatePrivateEvent,
            requiresRepresentation);
    }

    public async Task<OrgTrustCapabilities> GetOrgCapabilitiesAsync(Guid userId, Guid orgId, CancellationToken ct = default)
    {
        var membership = await db.Memberships.AsNoTracking()
            .FirstOrDefaultAsync(m => m.OrgId == orgId && m.UserId == userId, ct);
        // D-352 — the bypass reaches ORG verification too, not just the person's proofs. Same reason as
        // D-323: approving an organization requires a reviewer working a queue against documents that
        // `MockKycProvider` and a stubbed rasterizer produce, so enforcing it outside Production costs a
        // manual approval per test org and establishes nothing about a real institution.
        //
        // Deliberately the SAME flag rather than a second one: one switch, one Production guard, one
        // thing to delete when real adapters ship. A separate `ORG_VERIFICATION_BYPASS` would be a
        // second door to remember to lock.
        //
        // It relaxes the CAPABILITY only. `VerificationStatus` on the row is untouched, so the admin
        // console and the org profile keep reporting what is actually on file — the D-323 rule that a
        // bypass may open a gate but must never forge a fact.
        var orgVerified = identityOptions.Bypass || await db.Organizations.AsNoTracking()
            .AnyAsync(o => o.Id == orgId && o.DeletedAt == null && o.VerificationStatus == OrgVerificationStatus.Verified, ct);

        // `canRepresent` stays real: it reads the caller's own membership row, which no provider mocks
        // and which a test seeds directly. Relaxing it would let anyone represent anyone.
        var canRepresent = membership?.IsVerified == true;
        return new OrgTrustCapabilities(canRepresent, canRepresent && orgVerified, orgVerified);
    }
}
