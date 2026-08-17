using System.Linq.Expressions;
using Kurx.Domain.Enums;

namespace Kurx.Domain.Entities;

public class Organization
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = null!;
    public string Slug { get; set; } = null!;
    public string? LogoKey { get; set; }
    public string? Bio { get; set; }
    public string? LinksJson { get; set; }

    // ── Registry (M4) ────────────────────────────────────────────────────────
    public OrganizationType Type { get; set; } = OrganizationType.Other;
    public string? LegalName { get; set; }              // full legal/registered name
    public string? PrimaryDomain { get; set; }          // e.g. nsrit.edu.in — globally unique among active orgs
    /// <summary>Points at the surviving org after a merge (M5); equals <see cref="Id"/> for a normal org.
    /// A duplicate merged away keeps its row (audit) but redirects here.</summary>
    public Guid CanonicalOrgId { get; set; }
    /// <summary>Lowercased, punctuation-stripped, whitespace-collapsed <see cref="Name"/>. Backs the
    /// pg_trgm fuzzy-dedup search so "NSRIT" and "N.S.R.I.T" collapse. Never shown to users.</summary>
    public string NormalizedName { get; set; } = "";
    /// <summary>A lightweight "just me" org auto-created for a personal/free event (D-055). It never
    /// enters verification and is excluded from registry search so it doesn't pollute dedup — the one
    /// reliable way to tell it apart from a freshly-created (still-unverified) real institution.</summary>
    public bool IsPersonal { get; set; }

    // ── Verification lifecycle (M5) ──────────────────────────────────────────
    public OrgVerificationStatus VerificationStatus { get; set; } = OrgVerificationStatus.Unverified;
    public Guid? VerificationReviewedBy { get; set; }   // platform VerificationReviewer
    public DateTime? VerificationReviewedAt { get; set; }
    public string? VerificationNotes { get; set; }      // reviewer note (admin-facing)

    // ── Payouts / org financial KYC (renamed to org_bank_verifications in M9) ──
    public string? RazorpayLinkedAccountId { get; set; }
    public PayoutAccountStatus PayoutAccountStatus { get; set; } = PayoutAccountStatus.None;
    public string? BankLast4 { get; set; }
    // V3 §9.1 — the org's settlement currency (ISO-4217); events bind their currency from here. INR today.
    public string SettlementCurrency { get; set; } = Money.DefaultCurrency;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }
}

/// <summary>D-368 — the one rule for what counts as an organization.
///
/// <para><b>A row in <c>organizations</c> is a real organization only when it is not soft-deleted and not
/// a self-representation row.</b> An <c>IsPersonal</c> row is persistence infrastructure — the thing that
/// satisfies the non-null <c>events."OrgId"</c> FK for an event hosted by a person (D-268/D-273b) — and
/// the domain says plainly that Kurx has no personal organizations.</para>
///
/// <para>This exists as one expression rather than a predicate each caller retypes because the copies had
/// already drifted: D-353 added <c>!IsPersonal</c> to every organization <i>list</i> — the admin registry,
/// <c>/v1/me/representations</c>, registry search — and to neither organization <i>count</i>, so the admin
/// dashboard reported three organizations over a registry holding one. One rule spelled two ways is how
/// that happens.</para>
///
/// <para><b>Never write the pair inline.</b> A query that asks "is this a real organization?" composes
/// <see cref="Real"/>. Anything else is a second source of truth.</para>
///
/// <para>Scope only. This says nothing about whether an organization is <i>verified</i> (that is
/// <c>VerificationStatus</c>, D-044) or whether a caller may act on it (that is org RBAC, D-015). A
/// self-representation row stays reachable by id for support, exactly as D-353 left it — this predicate
/// governs the surfaces that enumerate or count organizations, not the ones that fetch a known one.</para></summary>
public static class OrganizationScope
{
    /// <summary>EF-translatable. Compose into any query that lists or counts organizations:
    /// <c>db.Organizations.Where(OrganizationScope.Real)</c>,
    /// <c>db.Organizations.CountAsync(OrganizationScope.Real, ct)</c>, or chained with a further
    /// predicate — <c>.Where(OrganizationScope.Real).Where(o =&gt; o.Id == someId)</c>.
    ///
    /// <para>Deliberately expression-only. <c>EventExposure</c> carries an in-memory twin because it has
    /// materialised callers; this has none, and an unused second spelling of a rule about drift would be
    /// the joke writing itself.</para></summary>
    public static Expression<Func<Organization, bool>> Real =>
        o => o.DeletedAt == null && !o.IsPersonal;
}

/// <summary>A node in an organisation's structural tree (V3 §4.1, Phase 4). Universities and companies are
/// the SAME recursive tree with different <see cref="Kind"/> labels — building two hierarchies is prohibited.
/// Every organisation has exactly one root unit (<see cref="ParentId"/> null, DB-enforced); a solo organiser's
/// tree is that single node and costs nothing (the picker never renders, <see cref="Event.OrgUnitId"/>
/// auto-sets). <see cref="Path"/> is the materialised ancestor path so ancestor/subtree queries are a linear
/// prefix match, never a recursive CTE. Ownership lives here; audience — who may register — is always a
/// separate AudienceRule (§4.4), never conflated. Matrix orgs are solved by multiple memberships, never a DAG.</summary>
public class OrgUnit
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrgId { get; set; }
    public Guid? ParentId { get; set; }                 // null = the org's root unit
    /// <summary>Open list (V3 §4.1): organization, university, campus, department, program, club, company,
    /// division, team, committee, chapter, … The root defaults to "organization"; sub-units set their own.</summary>
    public string Kind { get; set; } = "organization";
    public string Name { get; set; } = null!;
    /// <summary>Materialised path of ancestor ids incl. self, '/'-delimited: "/{rootId}/" for a root,
    /// "/{rootId}/{childId}/" for its child. Ancestors are every id in the path except the last (self).</summary>
    public string Path { get; set; } = "";
    public OrgUnitState State { get; set; } = OrgUnitState.Active;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>An alternate name an organization is known by (acronym, former name, full expansion).
/// Search resolves any alias to the org's canonical row so future organizers reuse one entity
/// instead of creating duplicates (the "NSRIT" ↔ full-name rule, M4).</summary>
public class OrganizationAlias
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrgId { get; set; }
    public string Alias { get; set; } = null!;          // display form
    public string NormalizedAlias { get; set; } = "";   // normalized for matching
    public AliasSource Source { get; set; } = AliasSource.User;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Operational membership = a seat in the org's Kurx workspace with an RBAC role
/// (Owner/Manager/Staff/Finance, D-015). <see cref="IsVerified"/> is set when the seat is backed by
/// an approved evidence-based affiliation claim (M6) — separate from what the seat can *do*.</summary>
public class Membership
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid OrgId { get; set; }
    public OrgRole Role { get; set; }
    public bool ShowOnProfile { get; set; } = true;
    // Verified affiliation (M6): backed by an approved MembershipClaim.
    public bool IsVerified { get; set; }
    public Guid? SourceClaimId { get; set; }
    /// <summary>When <see cref="IsVerified"/> was flipped true. Null for a never-verified seat — a
    /// boolean alone can't answer "since when," which the profile's timeline needs (D-201).</summary>
    public DateTime? VerifiedAt { get; set; }
    public DateTime? ValidUntil { get; set; }           // e.g. student membership expires at graduation
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ── Audience / eligibility (V3 §4.3, Phase 5) ────────────────────────────
    /// <summary>Immutable attributes audience rules match on: { cohort_year, section, roll_no, employee_id,
    /// grade, ... }. cohort_year is the JOINING batch (e.g. 2026), NEVER an ordinal year — ordinal year is
    /// derived from cohort + calendar, so a rule anchored on "Year 4" silently breaks every promotion cycle.
    /// jsonb, nullable.</summary>
    public string? AttributesJson { get; set; }
    public MembershipSource Source { get; set; } = MembershipSource.SelfDeclared;
}

/// <summary>An evidence-backed CLAIM that a user holds a role at an organization (M6, D-045). Reviewed
/// independently of profile bio ("bio is never proof"). On approval it marks the user's operational
/// membership (creating a read-only Staff seat if none) as verified; organizer rights stay an explicit
/// org-Owner act. Evidence lives in <c>verification_documents</c> (subject = Membership, id = claim id).</summary>
public class MembershipClaim
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid OrgId { get; set; }
    public MembershipClaimRole ClaimedRole { get; set; }
    public MembershipClaimStatus Status { get; set; } = MembershipClaimStatus.Submitted;
    /// <summary>Set when the user's email domain matched the org's primary domain at submit — a
    /// lower-friction review hint (still reviewed, never auto-approved). Bio is never a signal.</summary>
    public bool FastTrack { get; set; }
    public DateTime? ContactVerifiedAt { get; set; }    // official-contact verification (future workflow)
    public DateTime? ValidUntil { get; set; }
    public Guid? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Organization BANK verification — penny-drop / PAN-match against the org's payout account
/// (D-016). Renamed from KycRecord in M9 (D-048): "KYC" now means *person* identity (M3, `/v1/me/identity`);
/// this is the org's financial verification, reached via `/v1/orgs/{orgId}/kyc/*`.</summary>
public class OrgBankVerification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrgId { get; set; }
    public KycKind Kind { get; set; }
    public KycStatus Status { get; set; } = KycStatus.Pending;
    public string? PayloadJson { get; set; }
    public Guid? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class RiskFlag
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrgId { get; set; }
    public Guid? EventId { get; set; }
    public RiskFlagKind Kind { get; set; }
    public string Severity { get; set; } = "medium";    // low | medium | high
    public string? DetailsJson { get; set; }
    public RiskFlagStatus Status { get; set; } = RiskFlagStatus.Open;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class PayoutSchedule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrgId { get; set; }
    public int Tier { get; set; } = 1;
    public int AdvancePct { get; set; }
    public long CapPaise { get; set; }
    public string Currency { get; set; } = Money.DefaultCurrency;       // ISO-4217 (V3 §9.1)
    public int ReservePct { get; set; }
    public DateTime? NextRunAt { get; set; }
    /// <summary>Completed events with no open risk flags/chargebacks — drives tier promotion.</summary>
    public int CleanEventCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Cached balance for fast wallet reads. Updated atomically in the same transaction as
/// every ledger_entries insert. Never read ledger_entries SUM on the hot path.</summary>
public class OrganizationWallet
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrgId { get; set; }
    public string Currency { get; set; } = Money.DefaultCurrency;       // ISO-4217 — one currency per wallet (V3 §9.1)
    // Balance by ledger state (paise)
    public long CollectedPaise { get; set; }
    public long AvailablePaise { get; set; }
    public long AdvancedPaise { get; set; }
    public long ReservedPaise { get; set; }
    public long SettledPaise { get; set; }
    // Lifetime counters (append-only, never decrease)
    public long LifetimeEarnedPaise { get; set; }
    public long LifetimeWithdrawnPaise { get; set; }
    // Audit link: last ledger entry included in this snapshot
    public Guid? LastLedgerEntryId { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>GitHub-style org membership invitation. Membership is NEVER created automatically —
/// only created when the invited user explicitly accepts.</summary>
public class OrgInvitation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrgId { get; set; }
    public Guid InvitedBy { get; set; }
    public Guid? InvitedUserId { get; set; }            // existing Kurx user
    public string? InvitedPhone { get; set; }           // phone-based invite (user may not exist yet)
    public OrgRole Role { get; set; } = OrgRole.Staff;
    public OrgInvitationStatus Status { get; set; } = OrgInvitationStatus.Pending;
    public string Token { get; set; } = null!;          // unique 32-char acceptance token
    public DateTime ExpiresAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>User following an organization to receive event notifications.</summary>
public class OrganizationFollower
{
    public Guid UserId { get; set; }
    public Guid OrgId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
