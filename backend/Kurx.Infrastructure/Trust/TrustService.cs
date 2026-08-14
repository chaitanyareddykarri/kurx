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

        // D-323 — every proof above is answered by MockKycProvider today (DigiLocker always approves; PAN and
        // penny-drop pass for anything not ending "0000"), so outside Production the gate costs four
        // submissions and establishes nothing. The flag relaxes the PROOFS only; `fraudClear` is deliberately
        // outside it and is still ANDed into all three capabilities below, because the blacklist and risk
        // engine are real implementations and switching them off would test less rather than more.
        // Refused at startup in Production (DependencyInjection). Reasoning: IdentityVerificationOptions.
        var proofsSatisfied = identityOptions.Bypass || (identityVerified && panVerified && bankVerified);

        var canOrganizeFree = true;                                       // any account: free/private events, no KYC

        // Selling and settling now carry the SAME bar. They used to differ — payout asked only for a
        // bank account — so a user could receive money having proved nothing about who they were.
        // Both are "the platform is moving this person's money", and both require the full set.
        var canOrganizePaid = proofsSatisfied && fraudClear;
        var canReceivePayout = canOrganizePaid;

        // D-307 — publishing to the public is itself a trust event. A free public event still carries the
        // platform's name and reaches every user through discovery, so the harm is not bounded by whether
        // money moved; bounding verification by payment was the wrong axis.
        //
        // The predicate is IDENTICAL to canOrganizePaid today and is deliberately written out rather than
        // assigned from it. They answer different questions — may this person publish publicly, versus may
        // this person take money — and aliasing them would mean a future change to either silently moved
        // the other. `bankVerified` already carries the penny drop and the name match, so bank OWNERSHIP
        // is inside this, not beside it.
        var canCreatePublicEvent = proofsSatisfied && fraudClear;

        // Constant true: a Private event cannot be Listed, cannot take payment and appears on no discovery
        // surface (PolicyResolver + nine services), so there is no exposure to bound and no money to
        // settle. Carried as a flag anyway so both clients render one uniform gate.
        const bool canCreatePrivateEvent = true;

        // Label over the flags. L0 (guest) never applies to an authenticated user; L3+ need org context.
        var level = canOrganizePaid ? "L2" : "L1";

        return new TrustCapabilities(level, canOrganizeFree, canOrganizePaid, canReceivePayout,
            identityVerified, bankVerified, canCreatePublicEvent, canCreatePrivateEvent);
    }

    public async Task<OrgTrustCapabilities> GetOrgCapabilitiesAsync(Guid userId, Guid orgId, CancellationToken ct = default)
    {
        var membership = await db.Memberships.AsNoTracking()
            .FirstOrDefaultAsync(m => m.OrgId == orgId && m.UserId == userId, ct);
        var orgVerified = await db.Organizations.AsNoTracking()
            .AnyAsync(o => o.Id == orgId && o.DeletedAt == null && o.VerificationStatus == OrgVerificationStatus.Verified, ct);

        var canRepresent = membership?.IsVerified == true;
        return new OrgTrustCapabilities(canRepresent, canRepresent && orgVerified, orgVerified);
    }
}
