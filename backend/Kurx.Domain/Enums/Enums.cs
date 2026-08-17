namespace Kurx.Domain.Enums;

public enum OrgRole { Owner, Manager, Staff, Finance, Representative }
// V3 §7.1 (Phase 8) — the five orthogonal RegistrationPolicy axes replacing the four-value registration_model.
// Phase 8 stores + derives the policy from current config; gates/allocations needing later subsystems (LOTTERY
// draw §7.4, PREREQUISITE eval §7.3, TEAM §6, DELEGATED §7.5) are stored-but-not-enforced until Phase 9 cut-over.
public enum RegistrationSubject { Person, Team, OrgUnit, ExternalOrg }
public enum RegistrationGate { Open, Approval, Invite, Lottery, Prerequisite, Referral }
public enum IdentityRequirement { None, Contact, Account, Verified }
public enum AllocationPolicy { Fcfs, Lottery, Quota, Ranked, Assigned }
public enum PaymentPolicy { Free, Self, Delegated, Sponsored, Deferred }
// V3 §7.2 (Phase 8) — the registration → admission → credential chain, shadowing Order/Ticket.
public enum RegistrationState { Pending, Confirmed, Cancelled }
public enum AdmissionState { Active, CheckedIn, Void }
public enum CredentialState { Active, Revoked }
// V3 §8.1 (Phase 7) — InventoryPool dimensions. Phase 7 seeds one General/InPerson/PersonSlot/Event pool per
// TicketType as an additive shadow of the scalar Quantity/Sold; the other segments/scopes/units land later.
public enum InventoryScope { Event, SubEvent, Stage, AgendaItem, Venue, Zone }
public enum InventorySegment { General, Vip, QuotaOrgUnit, QuotaRole, Guest, Accessible, Press, Staff, WalkIn }
public enum InventoryChannel { InPerson, Virtual }
public enum InventoryUnit { PersonSlot, TeamSlot }
// V3 §8.4 — no-show release policy. Stored on the pool; the release automation is Phase 9 (money-path cut-over).
public enum NoShowPolicy { None, ReleaseAfter, Deposit, Strike }
// V3 §9.2 (Phase 9) — Pass (the commercial product) + AdmissionRight (what it grants). Phase 9 Option A implements
// SINGLE scope only; Subtree/Set/Query are the V3 vocabulary for multi-scope passes, deferred to a later wave
// (stored-but-never-created this phase). Channel EITHER is likewise reserved.
public enum PassVisibility { Public, Unlisted, CodeOnly, InviteOnly }
public enum AdmissionScope { Single, Subtree, Set, Query }
public enum AdmissionChannel { InPerson, Virtual, Either }
// V3 §6 (Phase 10) — the Team subsystem: the ONLY group entity, and only where competition exists. Additive; the
// purchase Group (RegistrationMode.Group) stays as a legacy compatibility mirror. Team registration as a purchase
// subject (team_slot inventory, §6.5) is deferred to the competitive-purchase phase — Phase 10 is formation only.
public enum TeamState { Forming, Complete, Locked, Competing, Eliminated, Disqualified, Withdrawn, Finalist }
public enum TeamRole { Captain, CoCaptain, Member, Substitute, Mentor }
public enum TeamMembershipState { Invited, Requested, Active, Replaced, Removed, Left }
public enum TeamInviteState { Pending, Accepted, Declined, Revoked, Expired }
public enum TeamJoinRequestState { Pending, Approved, Rejected }
public enum TeamFormationMode { Open, InviteOnly, OrganiserAssigned, RandomAllocation }
public enum TeamJoinApproval { None, Captain, Organiser }
public enum IncompleteTeamPolicy { BlockAtLock, AutoMerge, AllowUndersized, Waitlist }
// V3 §10 (Phase 11) — the competition engine: Stage · Fixture · ScoringPolicy · Result. A Stage is NOT registerable
// (competitors arrive by advancement, spectators by admission). Spectator pools are config-only this phase (the
// purchase flow is deferred); the scoring engine is the complete deterministic aggregation, with enforceable vote
// fraud controls but no statistical anomaly detection (a later analytics concern).
public enum StageFormat { SingleSubmission, JuryReview, Knockout, DoubleElim, RoundRobin, Swiss, League, TimeTrial, PublicVote }
public enum StageParticipantSource { AllRegistered, AdvancedFrom, Seeded, Wildcard }
public enum AdvancementRule { TopN, TopPercent, ScoreGte, Manual }
public enum ResultsVisibility { Live, OnStageClose, OnEventClose }
public enum StageState { Draft, Live, Closed }
public enum FixtureState { Scheduled, Live, Complete, Walkover, Abandoned, Disputed }
public enum CompetitionSubjectType { Person, Team }
public enum ScoreSourceType { Judge, PublicVote, Automated }
public enum ScoreAggregation { Sum, WeightedMean, TrimmedMean, Median, RankAggregation }
public enum ScoreNormalisation { None, PerJudgeZscore }
public enum ResultState { Provisional, Published, Disputed, Corrected }
public enum VoteIdentityBinding { Account, VerifiedContact }
// V3 §5.3 (Phase 6) — the 7 hardcoded participant classes. Capacity and permission logic branch on the
// class, so it is closed; the slug within a class is the platform registry (org-extensible later).
public enum ParticipantClass { Organiser, Operations, Content, Evaluation, Participant, Commercial, Observer }
// V3 §5.1 (Phase 6) — participation lifecycle.
public enum ParticipantState { Invited, Accepted, Declined, Active, Removed, Completed }
// V3 §5.1 (Phase 6) — the subject of a participation. Team is stored-for-Phase-10; Person and OrgUnit are usable today.
public enum ParticipantSubjectType { Person, Team, OrgUnit }
public enum ParticipantVisibility { Public, Internal }
// V3 §4.3 (Phase 5) — how a membership was established. Audience rules with require_verified reject
// SelfDeclared; PROVISIONED (SCIM/HRIS) is trusted on arrival.
public enum MembershipSource { SelfDeclared, Invited, Imported, Provisioned }
// V3 §4.4 [B15] (Phase 5) — the team-eligibility ruling on an AudienceRule. Only EveryMember is meaningful
// for individual registration; the team variants activate with Teams (Phase 10).
public enum AudienceAppliesTo { EveryMember, CaptainOnly, AtLeastN, TeamAttribute }
// V3 §4.2 — OrgUnit lifecycle. A unit that owns events is never deleted: it archives (stops accepting new
// events/memberships) or becomes a Tombstone after a merge so historical rollups still resolve. Phase 4
// ships the state field; the archive/merge/reparent operations are a later phase.
public enum OrgUnitState { Active, Archived, Tombstone }
public enum PayoutAccountStatus { None, Pending, Active }
public enum CategoryLevel { Audience, Category, Type }
// D-188 (Platform Taxonomy Management): lifecycle, orthogonal to IsVisible (which is display-only
// within an Active node — see EventCategory.IsVisible). Stored as text, so appended values are safe.
// `Retired` is written by MigrateCanonicalTaxonomy for nodes the canonical merge superseded: kept
// resolvable (analytics, certificates and historical events still reference the ids) but out of the
// Create Event picker. Distinct from Archived, which is an admin action via CategoryService.
public enum CategoryStatus { Active, Disabled, Archived, Retired }
// V3 §14.1 lifecycle. Published stays the AUTHORITATIVE registration-open state (the money path + discovery gate on it,
// unchanged); Phase 14 adds Scheduled/Live/Completed additively at the ends. Stored as text, so appended values are safe.
/// <summary>D-266 M4 (review lifecycle). The legacy <c>InReview</c> member was retired in Stage 4: it
/// conflated "submitted, waiting for a reviewer" with "a reviewer has claimed it", and the claim flow
/// needs those apart. Stage 3 migrated every stored row to <c>PendingReview</c>, and no transition targets
/// it any more, so no row can be written in the retired state.</summary>
public enum EventStatus
{
    Draft,
    Published, Closed, Archived, Cancelled, Scheduled, Live, Completed,

    // ── M4 review lifecycle ──────────────────────────────────────────────────────────────────
    /// <summary>Submitted by the organiser, waiting for a reviewer to claim it.</summary>
    PendingReview,
    /// <summary>A reviewer has claimed it. Edits are locked: PATCH returns 409 while in this state.</summary>
    UnderReview,
    /// <summary>Reviewer returned it with notes; the organiser edits and resubmits.</summary>
    ChangesRequested,
    /// <summary>Review passed. Publishing is now permitted — a Public product can never reach Published
    /// from Draft without passing through here.</summary>
    Approved,
    /// <summary>Review failed with a reason code. Terminal unless the organiser resubmits.</summary>
    Rejected,
}
// D-265: InviteOnly is an AUTHOR choice — discoverable to nobody, joinable only through an
// invitation. Distinct from Event.IsHidden, which is an ADMIN moderation flag; overloading the two
// would make "why can't anyone see my event" unanswerable.
/// <summary>D-266: <c>Listed</c> and <c>Unlisted</c> are the two discoverability settings of a Public
/// product; <c>InviteOnly</c> is reachable only through an invitation or invite link. The legacy
/// <c>Private</c> member was retired in M3 Step 5 once <see cref="Kurx.Domain.Entities.EventExposure"/>
/// made every exposure decision read Product+Visibility — at that point Private and InviteOnly behaved
/// identically, so the merge lost labelling, not behaviour.</summary>
public enum EventVisibility { Listed, Unlisted, InviteOnly }
// V3 §14.3 approval chains (Phase 14). Modes + step conditions + step/request states.
public enum ApprovalMode { Sequential, Parallel }
public enum ApprovalCondition { Always, IfPaid, IfExternal, IfBudgetGt, IfMinors }
public enum ApprovalStepState { Pending, Approved, Rejected, Bypassed }
public enum ApprovalRequestState { Pending, Approved, Rejected }
// V3 §13.1 event templates (Phase 15) — scope (most-specific wins) + lifecycle state.
public enum TemplateScope { Platform, Org, Unit, Personal }
public enum TemplateState { Draft, Published, Archived }
public enum EventMode { Offline, Online, Hybrid }        // D-064 A7
// V3 §11.2 — capability lifecycle state. REQUIRED (on, can't disable), ON (default on),
// OFF (available, off), LOCKED (has dependent data — hidden but never destroyed). Text-stored.
public enum CapabilityState { Required, On, Off, Locked }
public enum MediaKind { Banner, Gallery, Document, Poster, Brochure, RulesPdf, PromoVideo }
public enum ScheduleItemKind { Session, Break }
// V3 §13.2 (Phase 12) — EventSeries, one entity two modes. RECURRING: same content run again (rrule + per-occurrence
// timezone; one listing, pick a date). EDITIONS: same brand, different content (ordinal/label; separate listings).
public enum SeriesMode { Recurring, Editions }
// V3 §7.5 (Phase 13) — DelegatedRegistration / SeatBlock: an org unit reserves N unassigned admissions; the delegate
// console binds people later (governed by deadline + reassign limit + audit). Payment is data/authz only this phase —
// FREE or DEFERRED (org invoiced); no live collection (§9.7 gateway settlement is a payments-phase concern).
public enum SeatBlockState { Open, Closed, Cancelled }
public enum DelegatedPaymentMode { Free, Deferred }
public enum SponsorTier { Platinum, Gold, Silver, Bronze, Partner }
public enum PricingUnit { PerTicket, PerGroup }
// D-265: Both = an event that accepts solo entrants AND teams (a hackathon with a solo track).
public enum RegistrationMode { Individual, Group, Both }
public enum FormFieldType { Text, Number, Select, Checkbox, Date, File }
public enum FormFieldScope { PerRegistration, PerParticipant }
public enum OrderStatus { Pending, Paid, Failed, Refunded, PartiallyRefunded }
public enum RefundStatus { Initiated, Processed, Failed }
public enum TicketState { Issued, CheckedIn, Void }
public enum TransferStatus { OnHold, Released, Reversed }
public enum LedgerState { Collected, Available, Advanced, Reserved, Settled, Refunded, Disputed }
public enum KycKind { PennyDrop, PanMatch, Digilocker }
public enum KycStatus { Pending, Approved, Rejected }
public enum RiskFlagKind { UniqueBuyer, Velocity, Chargeback, Capacity }
public enum RiskFlagStatus { Open, Cleared, Frozen }
// IdCard is APPENDED, never inserted. The enum persists by integer value, so putting a new
// member ahead of Invite would silently reinterpret every stored row as a different kind of design.
public enum TemplateKind { Certificate, Invite, IdCard }
public enum TemplateMode { System, Custom }
public enum GeneratedCardKind { EventInvite, GroupCard }
public enum CertificateStatus { Generated, Emailed, Failed }
public enum CertificateKind { Participation, Winner, RunnerUp, Finalist, Volunteer, Organizer, Judge, Speaker, Sponsor, Appreciation, Completion }
public enum EmailStatus { Queued, Sent, Failed }
public enum WhatsAppMessageStatus { Queued, Sent, Delivered, Read, Failed }
public enum WhatsAppMessageKind { OrderConfirmation, TicketDelivery, EventReminder, GroupInvite, Certificate, Generic }
public enum SeatHoldStatus { Active, Consumed, Expired, Released }
public enum TicketTransferStatus { Pending, Claimed, Cancelled, Expired }

// ── ID cards (D-331) ─────────────────────────────────────────────────────────────────────────────
/// <summary>Draft is editable by the holder; Active is the issued, verifiable state. Expired is DERIVED
/// from ValidUntil at read time (IdCard.EffectiveStatus) and is stored only when an admin ends a card
/// early, so a row that nobody swept never verifies as current.</summary>
public enum IdCardStatus { Draft, PendingApproval, Active, Expired, Revoked, Rejected }

/// <summary>Rendering layouts. Extending this enum does not migrate anything — Template is the layout
/// key, and the renderer maps unknown-to-old-code values by falling back to StandardCollege.</summary>
public enum IdCardTemplate
{
    StandardCollege, ModernCollege, TechFest, CulturalFest, EventParticipant, StaffFaculty, Volunteer,
}

// Phase B
public enum InvitationChannel { Email, WhatsApp, Both }
public enum InvitationSendStatus { Pending, Queued, Sent, Failed }
public enum InvitationRsvpStatus { None, Accepted, Declined }
public enum InvitationStatus { Active, Revoked }
public enum AnnouncementAudience { AllTicketHolders, CheckedIn, NotCheckedIn }
public enum AnnouncementStatus { Queued, Sending, Sent, Cancelled, Failed }

// Phase C
public enum ChatPostPolicy { Everyone, HostsOnly }
// Lifecycle states, in order (D-122). Active -> Locked at the event's end -> Archived 7 days later.
// Appended, never reordered: the values are persisted as ints.
public enum ChatRoomStatus { Active, Locked, Archived }
/// <summary>The chat moderation ladder (D-301). Ordinal order IS the rank order, so `>=` and `>` read as
/// authority comparisons — see <c>ChatService.OutRanks</c>, which is the single place that decides whether
/// one member may act on another.
///
/// <para>Persisted as <c>text</c>, so inserting <c>Moderator</c> in the middle needs no migration and no
/// backfill: stored rows say "Member" and "Host", never 0 and 1.</para>
///
/// <para><c>Moderator</c> is granted only by an explicit Host promotion. No event role — speaker, judge,
/// mentor, volunteer, staff — ever confers it automatically (D-300).</para></summary>
public enum ChatMemberRole { Member, Moderator, Host }
public enum ChatMessageKind { Text, System }
// D-104: reserved so a second room per event never needs a breaking migration. Only General is
// created today — one event still means one room.
// `Direct` appended for 1:1 messaging (D-264). Stored as text, so a new member needs no migration —
// only the per-event uniqueness index moved, to exclude rooms that have no event.
public enum ChatRoomKind { General, Announcements, QA, Staff, Organizers, Direct }

// V2 — Event Assignments
public enum AssignmentStatus { Invited, Accepted, Declined, Removed, Completed }

// V2 — Org Invitations
public enum OrgInvitationStatus { Pending, Accepted, Declined, Expired, Cancelled }

// V2 — Event Reviews
public enum EventReviewStatus { Published, Flagged, Removed }

// V2 — Ticket Waitlist
public enum WaitlistStatus { Waiting, Notified, Converted, Expired, Cancelled }

// Registration forms: the event-scoped registration_forms/* builder (D-024) was retired in M11
// (D-050) — ticket-scoped FormField (D-020) is the single registration-form system. Its
// RegistrationFieldType enum was removed with it.

// ── Trust & Verification substrate (M0) ─────────────────────────────────────
// Shared by every verification subsystem: identity KYC (M3), org verification (M5),
// membership verification (M6), event approval (M8), fraud (M13), admin console (M12).

/// <summary>What a verification document / review is about. SubjectId is polymorphic across the
/// owning tables (users, organizations, memberships, events) and carries no hard FK.</summary>
public enum VerificationSubjectType { UserIdentity, Organization, Membership, Event }

/// <summary>Lifecycle of a single uploaded evidence document. Superseded = replaced by a newer upload.</summary>
public enum VerificationDocumentStatus { Pending, Accepted, Rejected, Superseded }

/// <summary>A reviewer's (or automated system's) decision on a verification subject.</summary>
public enum VerificationDecision { Approve, Reject, RequestChanges }

/// <summary>Platform-wide (not org-scoped) roles, granted as data in <c>platform_roles</c> and read
/// LIVE per request — never trusted from a JWT — so a revoked role is effective on the next request
/// (M2). SuperAdmin implies every other platform capability.</summary>
public enum PlatformRole { SuperAdmin, VerificationReviewer, FinanceOps, Support, ReadOnlyAuditor }

// ── Person identity verification (M3) ───────────────────────────────────────

/// <summary>Graduated person-KYC ladder (ID0–ID4). Cost rises with the rung; capability gates (M7)
/// read the specific evidence flags on <c>user_identity_verifications</c>, not this coarse label.
/// Contact (email) and Liveness are modelled now but their flows are deferred (not faked).</summary>
public enum IdentityLevel { Phone, Contact, GovernmentId, Bank, Liveness }

/// <summary>State of a person's identity verification (the most recent submission).</summary>
public enum IdentityStatus { NotStarted, Submitted, UnderReview, Approved, Rejected, ChangesRequested, Expired, Revoked }

/// <summary>Result of a penny-drop test on a person's own bank account — a ₹1 credit whose success
/// proves the account exists, is live, and accepts deposits. Distinct from merely having submitted an
/// account number: <c>BankLast4</c> being present only ever meant "a number was typed".
///
/// <para><see cref="Pending"/> exists because a real provider settles asynchronously — the mock
/// answers inline, but the state machine must not have to change when a real adapter ships.</para></summary>
public enum PennyDropStatus { NotStarted, Pending, Passed, Failed }

/// <summary>Whether the account-holder name returned by the bank matched the name on the account
/// being verified. This is the control that catches a PAN and a bank account belonging to two
/// different people — the highest-value check in the financial set, and previously not modelled at
/// all: the holder name was passed to the provider and the answer discarded.</summary>
public enum NameMatchStatus { NotChecked, Match, PartialMatch, Mismatch }

/// <summary>Which component of a person's identity a status or history entry refers to. The aggregate
/// <see cref="IdentityStatus"/> cannot answer this — see <c>UserIdentity</c> for why per-component
/// status had to exist.</summary>
public enum IdentityComponent { GovernmentId, Pan, Bank }

// ── Organization registry (M4) ──────────────────────────────────────────────

/// <summary>Kind of entity an organization represents. Drives evidence requirements for membership
/// verification (M6) and org verification (M5). Open-ended via <see cref="Other"/>.</summary>
public enum OrganizationType { College, School, University, Company, Startup, NGO, Club, Community, Government, Other }

/// <summary>Provenance of an organization alias. Official = from the verified org itself; User =
/// crowd-entered; Import = bulk seed. Used when resolving "NSRIT" ↔ the full institution name.</summary>
public enum AliasSource { Official, User, Import }

/// <summary>Organization verification lifecycle (M5). Verified reserves the org's name/aliases and
/// unlocks org-scoped trust (M7); Suspended/Blacklisted block it (read live by event approval, M8).</summary>
public enum OrgVerificationStatus { Unverified, PendingReview, ChangesRequested, Verified, Rejected, Suspended, Blacklisted }

// ── Membership verification (M6) ────────────────────────────────────────────

/// <summary>The role a person claims to hold at an organization — an evidence-backed CLAIM, never
/// inferred from profile bio (M6). Distinct from operational org RBAC (Owner/Manager/Staff/Finance).</summary>
public enum MembershipClaimRole { Student, Faculty, Employee, Alumni, Founder, Director, Coordinator, Volunteer, ClubPresident, EventLead, Other }

/// <summary>Lifecycle of a membership-affiliation claim.</summary>
public enum MembershipClaimStatus { Submitted, UnderReview, OfficialContactVerification, Approved, Rejected, Appealed }

/// <summary>Ally connection state machine (D-201). Revoked covers both "cancelled while pending"
/// and "removed after accepted" — one terminal non-active state, not two.</summary>
public enum AllyStatus { Pending, Accepted, Declined, Revoked }

/// <summary>Per-connection display override — lets a user hide one ally from their public list
/// without disabling <c>User.ShowAllies</c> entirely.</summary>
public enum AllyVisibility { Public, Hidden }

// ── Profile section visibility (D-221) ──────────────────────────────────────

/// <summary>Every gated region of the public profile. This enum <b>is the registry</b>: a section
/// with no entry in <c>ProfileVisibilityResolver</c>'s default table fails closed, and a test asserts
/// every member has one — so adding a member here without deciding its visibility is a build failure,
/// not a silent leak.</summary>
public enum ProfileSection
{
    /// <summary>The profile as a whole. Not visible ⇒ the entire profile is 404, per D-018.</summary>
    Profile,
    Attended,
    Certificates,
    Network,
    Events,
    Organizations,
    Achievements,
    Timeline,
    /// <summary>Counts, rates and the Event DNA distribution (D-225).</summary>
    Metrics,
    /// <summary>Daily contribution density (D-228).</summary>
    Contributions,
}

/// <summary>Who may see one section. Ordered least→most restrictive; the numeric order is not
/// meaningful to the resolver, which branches explicitly.</summary>
public enum SectionVisibility
{
    Public,
    /// <summary>Accepted <c>AllyConnection</c> between owner and viewer.</summary>
    Connections,
    /// <summary>Owner and viewer share at least one public event.</summary>
    EventParticipants,
    OnlyMe,
}

// ── Fraud prevention (M13) ──────────────────────────────────────────────────

/// <summary>What a blacklist entry blocks — a hard block on a known-bad identifier.</summary>
public enum BlacklistKind { Phone, Email, Device, OrgName, DocHash }

/// <summary>Kind of fraud signal contributing to a subject's risk score.</summary>
public enum FraudSignalKind { Device, Ip, Velocity, DuplicateAccount, DocHash, DisposableContact, GeoMismatch, Manual }

// V2 — Gamification
public enum ReferralRewardStatus { Pending, Granted, Expired, Rejected }

// ── Trusted Device Authentication (AM0) ─────────────────────────────────────
// Stored as text (KurxDbContext enum convention). See docs/auth/AUTHENTICATION_DATABASE.md.

/// <summary>Explicit device lifecycle (ADR-AM12) — never a bare "trusted" bool. Every transition
/// writes a security_event + audit_log row. Compromised is terminal-for-reuse (blocks re-enrolling
/// the same key). Deleted is the soft-deleted terminal state.</summary>
public enum DeviceLifecycleState { PendingRegistration, PendingVerification, Trusted, Suspended, Revoked, Compromised, Deleted }

/// <summary>Which credential rail a stored public key belongs to (ADR-A3): DeviceKey = the custom
/// Keystore/Enclave challenge-response rail; WebAuthn = a FIDO2/passkey credential.</summary>
public enum DeviceCredentialType { DeviceKey, WebAuthn }

/// <summary>What a challenge authorizes. Login = trusted-device push-approval sign-in; StepUp =
/// high-risk re-auth (AM6); DeviceEnroll = binding a new device's key.</summary>
/// <summary>What a challenge was issued for. Purpose is enforced centrally on verification (D-088),
/// so a challenge minted for one ceremony can never be redeemed in another.
///
/// <para><b>The two credential rails have separate purposes on purpose (D-098).</b> The device-key
/// rail (Login/DeviceEnroll) and the WebAuthn rail (PasskeyLogin/PasskeyRegister) both issue
/// challenges against the same table, and sharing values let one rail consume the other's ceremony:
/// a passkey sign-in surfaced in the push-approval pending list, and a device-key challenge routed
/// into the passkey verifier crashed on non-WebAuthn context JSON.</para>
///
/// Stored as text (KurxDbContext convention), so adding members needs no migration.</summary>
/// <summary>Signing-key lifecycle (AM10, D-099). Exactly one key is Active; Retiring keys still
/// validate so a rotation never invalidates live tokens; Compromised is removed from JWKS at once.</summary>
public enum SigningKeyState { Pending, Active, Retiring, Retired, Compromised }

/// <summary><see cref="PasswordReset"/> is deliberately distinct from <see cref="StepUp"/> (D-330). A
/// step-up strengthens a session that already exists and is therefore scoped to an account, not to a
/// request; the reset ceremony is anonymous, so its second factor must name the exact reset it authorizes
/// or it degrades into "this account was active recently" — which a SIM-swap attacker can ride. Appended
/// last: the column is an int, so existing rows keep their values and no migration is needed.</summary>
public enum AuthChallengePurpose { Login, StepUp, DeviceEnroll, PasskeyRegister, PasskeyLogin, SecondFactor, PasswordReset }

/// <summary>Single-use challenge lifecycle. Durable copy in Postgres; hot state in Redis (AM14).</summary>
public enum AuthChallengeStatus { Pending, Approved, Rejected, Expired, Consumed }

/// <summary>Why an OTP was issued — OTP is bootstrap/recovery only (ADR-A4), never trusted-device login.</summary>
/// <summary>What a one-time code is for. Purpose is part of the issue/verify lookup key, so a code minted
/// for one ceremony can never be redeemed against another — the cross-ceremony replay defect D-098 found and
/// closed for challenges.
///
/// <para><see cref="EmailLogin"/> is deliberately distinct from <see cref="EmailVerification"/> (D-282):
/// verification asks "do you control this address?" while authenticated, at registration or on change; login
/// asks "are you the account holder?" after a password and before a session exists. Sharing one value would
/// let a verification code authenticate a login.</para></summary>
public enum OtpPurpose { Registration, PhoneVerification, EmailVerification, AccountRecovery, DeviceEnrollment, StepUp, PasswordReset, EmailLogin }

/// <summary>Delivery channel for an OTP. SMS routes through ISmsProvider (AWS SNS, AM1). Which channel a
/// purpose uses is decided in one place — see <c>OtpChannelPolicy</c> (D-281); WhatsApp is never selected
/// for authentication.</summary>
public enum OtpChannel { Sms, WhatsApp, Email }

/// <summary>Transactional-outbox dispatch state (ADR-AM16) for security-critical events.</summary>
public enum OutboxStatus { Pending, Dispatched, Failed }

// ── Posts (D-262) ─────────────────────────────────────────────────────────────

/// <summary>What a post IS, fixed at creation and never edited. Drives which optional payload the
/// client renders; the payload tables are the authority, this is the discriminator.</summary>
public enum PostKind { Text, Images, Video, Document, Poll, Event, Share }

/// <summary>Who may read a post. Enforced server-side on EVERY read path (D-262) — a post the caller
/// may not see returns 404, never 403 (D-018), so visibility never leaks by status code.</summary>
public enum PostVisibility { Public, Connections, EventParticipants, OnlyMe }

/// <summary>Media class, derived from the bytes on confirm — never from the client's declaration.</summary>
public enum PostMediaKind { Image, Video, Document }


// ── Event creation (D-265) ───────────────────────────────────────────────────

/// <summary>Demographic eligibility gate. <see cref="Any"/> is the default and means no gate at all —
/// a restriction is only ever stored because an organiser deliberately set one, and the reason is
/// shown to a rejected registrant so the refusal is never unexplained.</summary>
public enum GenderRestriction { Any, Male, Female, NonBinary }

/// <summary>What a ticket type IS, orthogonal to its price. <c>Donation</c> takes a payer-chosen
/// amount above a floor; <c>InviteOnly</c> cannot be self-selected at checkout and is claimed through
/// an invitation. Free/Paid remain what <c>PricePaise == 0</c> already implied, now stated.</summary>
public enum TicketKind { Free, Paid, Donation, InviteOnly }

public enum CouponKind { Percent, Flat }

// ── Account settings (D-263) ──────────────────────────────────────────────────

/// <summary>What a notification is ABOUT, as the user thinks of it — the unit they get to switch off.
/// Distinct from the free-form <c>Notification.Kind</c>, which is what the code emits: many kinds map
/// to one category, and <c>NotificationCategories</c> owns that mapping.
///
/// <para><see cref="Security"/> is never suppressible (D-263). It carries the messages that tell a user
/// they are under attack, so a preference to silence it is not a preference the server honours.</para></summary>
public enum NotificationCategory
{
    EventUpdates, Invitations, StaffInvitations, TeamInvitations, ConnectionRequests,
    Payments, Certificates, Messages, Posts, Announcements, Security, System,
}

/// <summary>Where a direct-message room stands with its recipient (D-264). Null for event rooms.
/// A DM from a non-ally lands <see cref="Pending"/> and notifies nobody until accepted — the spam
/// control. Allies skip straight to <see cref="Accepted"/>.</summary>
public enum DmRequestState { Pending, Accepted, Declined }


// ── Event product (D-266 D8/D12) ─────────────────────────────────────────────

/// <summary>The two Kurx event products. Determines capabilities, moderation and discoverability —
/// NOT who may register, which is the separate registration-policy axis. There is deliberately no
/// third value: a restricted hackathon is a Public product with a restrictive registration policy,
/// never a "Private Hackathon".</summary>
public enum EventProduct { Public, Private }

/// <summary>D-266 M2 — what the D12 archetype matrix *permits* for a capability. Distinct from
/// <see cref="CapabilityState"/>, which is what an organiser has actually set on one event
/// (Required/On/Off/Locked): this is the rule that decides which of those states are reachable at all.
/// Absence of a matrix cell means <c>Unsupported</c>, never "off".</summary>
public enum CapabilityRule { Unsupported, Optional, Required }

/// <summary>Who may register for an <b>event</b> (D-266 D8 §three axes). Orthogonal to visibility: a
/// Listed event with CollegeRestricted registration is discoverable by everyone and registerable only by
/// that college. There is no OrganizationMembersOnly — Kurx has no organization membership to check
/// against (D-266).
///
/// <para><b>Not to be confused with <see cref="RegistrationGate"/></b>, which is the composable
/// <i>ticket-type</i>-level mechanism (<c>RegistrationPolicy.GatesJson</c>). This enum states the event
/// author's single declared intent, from which the per-ticket gates are derived; the derivation itself is
/// M3 (policy engine) work, so for now this is the taxonomy allow-list vocabulary only.</para></summary>
public enum EventRegistrationPolicy { Open, InviteOnly, ApprovalRequired, CollegeRestricted, DepartmentRestricted, AlumniOnly, VerifiedOnly }

/// <summary>D-266 M5 — the review state of one event's institutional authorization.
///
/// <para>Deliberately its own enum rather than a reuse of <see cref="OrgVerificationStatus"/>: that one
/// carries <c>Suspended</c> and <c>Blacklisted</c>, which are standing sanctions against an organization
/// and meaningless for a single letter about a single event. Reusing it would put four unreachable states
/// on this row and invite a caller to set one.</para>
///
/// <para>There is no <c>Draft</c>. An authorization exists once it is filed; a half-typed form is client
/// state, and persisting it would make "has this been submitted for review?" unanswerable.</para></summary>
public enum EventAuthorizationStatus { Submitted, Approved, Rejected, ChangesRequested }

/// <summary>D-266 M7 — FinanceOps' verdict on an event that solicits money for a cause (D12 §6, A11).
/// Absent (null) means never reviewed, which is distinct from <see cref="Failed"/>: one has not been
/// looked at, the other has and was refused.</summary>
public enum FinancialReviewStatus { Passed, Failed }


/// <summary>D-266 M4 — why a reviewer rejected an event. A closed vocabulary rather than free text, so
/// rejections are analysable and the organiser-facing message can be localised. Stored in
/// <c>VerificationReview.ReasonCode</c>, which already exists for exactly this purpose.</summary>
public enum EventReviewReason
{
    /// <summary>Details are missing or too thin to evaluate.</summary>
    Incomplete,
    /// <summary>Content breaches the code of conduct or platform policy.</summary>
    ProhibitedContent,
    /// <summary>The organiser cannot be verified as entitled to run this event.</summary>
    UnverifiedOrganiser,
    /// <summary>Claims an affiliation it has no authorization for.</summary>
    MisrepresentedAffiliation,
    /// <summary>Pricing, refund terms or settlement details are invalid.</summary>
    InvalidCommerce,
    /// <summary>Duplicate of an existing event.</summary>
    Duplicate,
    /// <summary>Anything the vocabulary does not yet name; requires notes.</summary>
    Other,
}

/// <summary>What an <see cref="Entities.EntitlementProduct"/> gives its holder (D-334).
///
/// <para><b>Named "Entitlement", not "Coupon".</b> <see cref="Entities.Coupon"/> is already taken by
/// D-265's discount codes — a percentage off an order, which is a different thing entirely from a
/// claim on a lunch. The organiser-facing UI still says "coupon"; only the domain avoids the
/// collision.</para></summary>
public enum EntitlementKind
{
    /// <summary>A meal at a named sitting — see <see cref="MealSlot"/>.</summary>
    Meal,
    /// <summary>Food that is not a sitting: a snack stall, a drinks token.</summary>
    Food,
    Merchandise,
    /// <summary>Access to a space or session that admission alone does not grant.</summary>
    Access,
    /// <summary>Organiser-defined; <c>CustomLabel</c> carries the name.</summary>
    Custom,
}

/// <summary>Which sitting a <see cref="EntitlementKind.Meal"/> entitlement is for. <see cref="None"/> for
/// every non-meal kind, so the column is never null and grouping never has to special-case it.</summary>
public enum MealSlot
{
    None,
    Breakfast,
    Lunch,
    Dinner,
    Snack,
    Refreshment,
}

/// <summary>How a product reaches its holder (D-334 §12).
///
/// <para><see cref="Both"/> is a <b>rendering</b> choice, never a second row: one grant backs the printed
/// coupon and the in-app one, so redeeming either decrements the same counter. Two rows would let a
/// holder redeem the paper and the phone independently, which is the whole failure this enum exists to
/// prevent.</para></summary>
public enum EntitlementDelivery
{
    Physical,
    Digital,
    Both,
}

/// <summary>How a holder comes to have the entitlement (D-334 §16). Distinct from payment status: an
/// <see cref="IncludedInRegistration"/> product can still be priced for revenue reporting, and a
/// <see cref="FreeClaim"/> one is claimed without an order at all.</summary>
public enum EntitlementInclusion
{
    /// <summary>Granted automatically when the registration is confirmed.</summary>
    IncludedInRegistration,
    /// <summary>Offered during registration checkout; the holder opts in.</summary>
    OptionalAddOn,
    /// <summary>Bought on its own, after and independently of registration.</summary>
    SeparatePurchase,
    /// <summary>Claimed by the holder for no payment.</summary>
    FreeClaim,
    /// <summary>Defined but not obtainable — the organiser's off switch that keeps history intact.</summary>
    NotAvailable,
}

/// <summary>The lifecycle of one holder's <see cref="Entities.EntitlementGrant"/> (D-334 §17).
///
/// <para><see cref="Expired"/> is DERIVED at read time from the validity window, never stored — the same
/// rule <see cref="IdCardStatus"/> follows, and for the same reason: a stored expiry needs a sweeper and
/// is wrong between runs.</para></summary>
public enum EntitlementGrantStatus
{
    /// <summary>Inventory held for an in-flight checkout; not yet the holder's.</summary>
    Reserved,
    /// <summary>The holder's, with nothing redeemed yet.</summary>
    Issued,
    /// <summary>Some but not all of the quantity is spent (only when the product allows it).</summary>
    PartiallyRedeemed,
    /// <summary>Fully spent.</summary>
    Redeemed,
    /// <summary>Withdrawn by an organiser, or superseded by a reissue.</summary>
    Cancelled,
    /// <summary>Money returned; can never be redeemed again (D-334 §28).</summary>
    Refunded,
}

// ── Certificate module (D-355) ───────────────────────────────────────────────────────────────────
// Every enum below persists as its member NAME, not its ordinal — the loop at the top of
// OnModelCreating sets the provider type to string for all enums. Members may therefore be reordered
// safely, but must NEVER be renamed without a data migration, and a state vocabulary must be kept in
// step with its CHECK constraint (which is generated from these names).

/// <summary>The page a certificate design renders onto. A named size, not a pixel pair: the document
/// carries no resolution and the renderer owns the millimetres.</summary>
/// <summary>Which page a certificate prints onto (D-361). This is the LABEL — the authoritative size is
/// <c>CertificateTemplate.PageWidthMm</c>/<c>PageHeightMm</c>, which is why <see cref="Custom"/> can
/// exist at all.
///
/// <para><b>Appended, never reordered.</b> The column stores the member NAME, so inserting a value ahead
/// of an existing one would silently reinterpret every stored template as a different page.</para></summary>
public enum CertificatePageSize
{
    A4Landscape, A4Portrait,
    A5Landscape, A5Portrait,
    LetterLandscape, LetterPortrait,
    LegalLandscape, LegalPortrait,
    // Photo-print sizes, named in inches the way a print shop names them.
    Photo8x10Landscape, Photo8x10Portrait,
    Photo11x14Landscape, Photo11x14Portrait,
    Photo12x16Landscape, Photo12x16Portrait,
    /// <summary>A page with no standard name. The dimensions on the template are the whole truth.</summary>
    Custom,
}

/// <summary>Draft is editable and cannot issue. Ready may issue. Archived is retained for the
/// certificates already issued from it but offered nowhere new.</summary>
public enum CertificateTemplateStatus { Draft, Ready, Archived }

/// <summary>What an element on a template draws. <c>Text</c> is identical on every copy;
/// <c>DynamicField</c> is substituted per recipient; <c>Image</c> is a stored asset such as a signature
/// or logo; <c>QrCode</c> resolves to the online verification page.</summary>
public enum CertificateFieldKind { Text, DynamicField, Image, QrCode }

public enum CertificateHorizontalAlignment { Left, Center, Right }
public enum CertificateVerticalAlignment { Top, Middle, Bottom }

/// <summary>A generation run's lifecycle. The confirmed flow is preview → inspect → approve → full run,
/// so <c>Generating</c> is reachable only from <c>Approved</c>.</summary>
public enum CertificateBatchStatus
{
    Draft, Mapping, PreviewReady, Approved, Generating, Completed, Failed, Cancelled,
}

/// <summary>An issued certificate's standing. <c>Superseded</c> is distinct from <c>Revoked</c>: a
/// superseded certificate was replaced as part of a correction, and the verification page has to be able
/// to say which happened.</summary>
public enum IssuedCertificateStatus { Issued, Revoked, Superseded }

/// <summary><c>Account</c> is an availability rather than a send — the certificate simply appears for a
/// linked user, with nothing transmitted.</summary>
public enum CertificateDeliveryChannel { Email, Account }

/// <summary><c>Sent</c> means the provider ACCEPTED the message, never that anyone received it. Without a
/// bounce pipeline there is no state that can honestly claim delivery, so none is offered.</summary>
public enum CertificateDeliveryStatus { Pending, Sent, Failed, Bounced }

/// <summary>What the dashboard counts. Append-only; carries no identifying data about who did it.</summary>
public enum CertificateEventType { Viewed, Downloaded, Verified, Shared }

/// <summary>A certificate signing key's standing (D-355). <c>Retired</c> no longer signs but still
/// verifies; <c>Compromised</c> also still verifies — a certificate signed before the compromise really
/// was issued by the platform, and reporting it as fake would be the wrong lie.</summary>
public enum CertificateSigningKeyState { Active, Retired, Compromised }
