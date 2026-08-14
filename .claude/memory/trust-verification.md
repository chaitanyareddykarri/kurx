# Trust & Verification — conventions

Single source of truth for the trust subsystem introduced by the production
re-architecture. Extended module-by-module; the original design dossiers were deleted
on 2026-08-08 once every module shipped and live only in git history.

> **Event-first (D-074 target, D-075 backend shipped, 2026-07-18).** Every person is a **User**; there are no
> organizer/owner accounts. A not-yet-verified institution is staged as a **hidden placeholder org**
> (`PendingReview`, not searchable, **no Owner**; the submitter is a *pending* `Representative` with
> `IsVerified = false`) via `POST /v1/orgs/representation-requests`, and becomes a real registry org only on
> **admin approval**, which flips the submitter to a **Verified Representative** (never "Owner"). `POST /v1/orgs`
> is now personal-org-only; search + public profile are Verified-only; events under an unverified org can't
> publish. The M4–M6/M12 substrate below is reused unchanged (D-075 chose **not** to add a parallel
> `OrganizationVerificationRequest` table — the org row *is* the staging record). See **D-074**/**D-075**.
> **Legacy note:** where this file says "Owner"/"org creation", that path is now reachable only for **personal**
> orgs; institutional Owner-at-creation is retired. Web/mobile clients are still org-first (D-076/D-077 pending).

## The five separated concepts (never conflate)

1. **Authentication** — do you control this phone? (OTP + JWT; unchanged.)
2. **Identity Verification** — are you a real, KYC'd person? (`user_identity_verifications`, M3.)
3. **Organization Verification** — is this a real institution? (`organizations.verification_*`, M5.)
4. **Membership Verification** — do you represent this org, in what role? (`membership_claims`, M6.)
5. **Event Approval** — may this specific event publish/sell? (event state machine, M8.)

Each has its own state machine and audit trail. None is inferred from another.
None is ever baked into a JWT — all trust/authorization state is read **live per
request** (extends the D-015 live-RBAC pattern) so suspension/revocation is
effective on the next request.

**Bio is never proof.** Profile `bio`/`education`/`links` are display-only,
authority-zero. Verification reads only from the `verification_*` tables and
subject-specific claim tables — never from profile columns.

## Shared substrate (M0, D-039) — always reuse, never duplicate

Two polymorphic tables back every verification subsystem:

- **`verification_documents`** — evidence. `(SubjectType, SubjectId)` polymorphic
  across users/organizations/memberships/events; **no FK on SubjectId**. File in
  private storage (StorageKey only); `Sha256` for forgery/dup detection (M13);
  `ExtractedJson` for OCR/provider fields. Status: Pending/Accepted/Rejected/Superseded.
- **`verification_reviews`** — append-only decisions (Approve/Reject/RequestChanges).
  The audit spine of the admin console (M12). `ReviewerId` null = system/automated.

`SubjectType`/`Status`/`Decision` are enums (text); `DocType`/`ReasonCode` are open
strings (subject-specific, extensible). Every verification state transition in
M3/M5/M6 writes a `verification_reviews` row + an `audit_log` row.

## Trust model = capability matrix (labels L0–L5)

Capability flags (`can_organize_free`, `can_organize_paid`, `can_receive_payout`,
`can_represent_org`, …) are the **source of truth**, derived live from concepts
2–5 + fraud. L0–L5 are presentation labels over them (M7). Never store a single
linear trust score as truth. Liveness/selfie is **risk-gated only**, never blanket.

## Platform roles (M2, D-040)

`platform_roles` (SuperAdmin/VerificationReviewer/FinanceOps/Support/ReadOnlyAuditor)
is read **live per request** by `PlatformRoleClaimsTransformation` (in the Api layer):
it strips any token-supplied `platform_role`/`kurx_admin` claim (anti-forgery) and
re-adds the user's current roles from the DB. Authority is never in the JWT →
grant/revoke effective next request. `IPlatformRoleService` reads/writes the table.
SuperAdmin implies all. Org roles (Owner/Manager/Staff/Finance) are separate (D-015).

## Module status
- **M0 substrate** — DONE (D-039).
- **M2 platform roles** — DONE (D-040).
- **M1 identity boundary** — DONE (D-041): dropped `users.IsKurxAdmin`; profile fields
  (Headline/Bio/EducationJson/Skills/Links) are authority-zero, self-declared, never
  read by authz/verification. EducationJson stays until M6 migrates it to claims.
- **M3 person KYC** — DONE (D-042): `user_identity_verifications` (1:1 user), ID0–ID4,
  masked last-4 ONLY, mock `IKycProvider` (real adapter is a gated integration task),
  every decision writes a `verification_reviews` row, `/v1/me/identity/*` endpoints,
  `/v1/me` identity summary. Capability gates (M7) read `*_last4` presence, not `level`.
- **M4 org registry** — DONE (D-043): `Organization` gains type/legal_name/primary_domain/
  canonical_org_id/normalized_name; `organization_aliases`; pg_trgm fuzzy search
  (`GET /v1/orgs/search`). Hard dedup on domain only; name dedup is soft (search) with
  hard name-dedup enforced at verification (M5). Type optional (default Other).
- **M5 org verification** — DONE (D-044): `Organization.VerificationStatus` state machine
  (Unverified→PendingReview→Verified/Rejected/Suspended/Blacklisted); Owner submit +
  evidence; reviewer (VerificationReviewer role) approve/reject/request-changes/suspend via
  `/v1/admin/orgs/*`; hard name-dedup at approval; audit via verification_reviews. Merge → M12.
- **M6 membership** — DONE (D-045): `membership_claims` (evidence-backed, role-typed);
  approve → verified read-only Staff seat (organizer rights stay an org-Owner act); email-
  domain fast-track hint; bio structurally excluded; `/v1/orgs/{id}/membership-claims` +
  `/v1/admin/membership-claims/*`. `AddMemberAsync` kept immediate (operational). M7 reads
  approved claims + `Membership.IsVerified`.
- **M7 trust** — DONE (D-046): `ITrustService` composes identity/org/membership into live
  capability flags (CanOrganizeFree/Paid, CanReceivePayout, CanRepresentOrg, IsOrgVerifiedRep);
  L0–L5 are labels. Gates read component flags, not coarse Level. Fraud no-op until M13
  (only tightens CanOrganizePaid). Surfaced on `/v1/me` + `/v1/orgs/{id}/my-capabilities`.
  **M8 event-approval and M10 payouts call this instead of re-deriving rules.**
  **The identity PROOFS are bypassable outside Production** (D-323, `IDENTITY_VERIFICATION_BYPASS`):
  `proofsSatisfied = bypass || (identityVerified && panVerified && bankVerified)` feeds
  `CanOrganizePaid`, `CanReceivePayout` and `CanCreatePublicEvent`. It exists because all three
  proofs are answered by `MockKycProvider` today, so enforcing them establishes nothing while
  blocking every public-event and paid-checkout test. Three properties make it safe and none of
  them is optional: **`fraudClear` is outside it** and is still ANDed into all three (blacklist and
  risk are real code — bypassing them would test *less*); **the reported facts are not forged** —
  `IdentityVerified`/`BankVerified` keep saying what is actually on file, because a verified badge
  nobody checked is worse than an open dev gate; and **it fails closed** — startup throws if it is
  set in Production, and every unset/unrecognised value lands on enforced. Default off, so the whole
  suite still proves the real rules. Retire it when a real DigiLocker/penny-drop adapter ships.
- **M8 event approval** — DONE (D-047): free events publish directly; PAID events (any priced
  ticket type) can't self-publish → `submit_review` (gated on organizer CanOrganizePaid + org
  Verified) → platform reviewer publishes. `payments_enabled` computed LIVE (not stored) via
  `GET /v1/orgs/{org}/events/{id}/payment-readiness`; M10 reuses it at charge time. Reused
  existing EventStatus states (no enum change / no migration).
- **M9 KYC split** — DONE (D-048): `kyc_records`→`org_bank_verifications`; deleted dead duplicate.
- **M10 payments** — DONE (D-049): paid checkout → capture webhook → ticket + Collected ledger +
  wallet, idempotent, live-gated. Real Razorpay adapter deferred.
- **M11 forms** — DONE (D-050): one form system (`FormField`); dropped dead `registration_forms/*`.
- **M12 admin console** — DONE (D-051): org merge (fresh dupes), blacklist, cross-subject
  verification history (`/v1/admin/verifications/{type}/{id}/history`), all VerificationReviewer-gated.
  admin/ Next.js UI = Pending Stitch UI.
- **M13 fraud** — DONE (D-052): `blacklist_entries` (hard blocks, normalized) + `fraud_signals`
  (polymorphic risk). `IFraudService.IsUserClearAsync` = M7's now-live `fraudClear` → blacklisted/
  high-risk (score ≥ 100) user loses CanOrganizePaid → cascades to M8/M10 gates. Blacklisted org
  names blocked at creation. Admin `/v1/admin/{blacklist,fraud-signals}` (VerificationReviewer).
  Automated signal producers (device/velocity/dup-account) are follow-ups feeding RecordSignalAsync.

**ALL 13 MODULES COMPLETE.** Trust foundation + event approval + payments + admin + fraud shipped;
build clean, 166/166 tests green, D-039–D-052. Deferred (documented): real provider adapters
(DigiLocker KYC, Razorpay), client UIs (web/mobile/admin = Pending Stitch UI), automated fraud
signal generation, paid-group tickets, refunds.

## Payment gate (read live at charge time — M10 must honor)
A paid event may accept money only when, read LIVE: it is Published, has ≥1 priced ticket
type, its organizer (event.CreatedBy) has `CanOrganizePaid`, and its org is Verified. This
is `EventService.GetPaymentReadinessAsync`. Never trust a stored flag — suspension/fraud
must stop payments on the next request.
