# Roadmap

> **Architecture: event-first ([D-074](../DECISIONS.md); backend shipped as [D-075](../DECISIONS.md)).** No organizer/owner accounts — every person is a **User** who may *represent* verified organizations. A not-yet-verified institution is staged via `POST /v1/orgs/representation-requests` as a **hidden placeholder org** (`PendingReview`, not searchable, no Owner; submitter is a *pending* Representative) that an admin approves into the registry, linking the submitter as a **Verified Representative**. `POST /v1/orgs` is personal-only; search + public profile are Verified-only; a pending org's events can't publish until approval. **Every surface is user-first and event-first as of [D-267](../DECISIONS.md), [D-268](../DECISIONS.md) and [D-269](../DECISIONS.md)**: users own events (`events.created_by`; organizations never do), there is no personal-organization concept, event authorization is the single `IEventAuthority`, Workspace lists a person's own events, Create Event needs no organisation, and the org-first navigation (`/orgs`, `/org/create`, `/org/:id/events/create`, `Profile → My Organizations`, the "current organization" cookie) is removed from web and Flutter alike. **This file is the single canonical build-status authority** — the point-in-time snapshots that used to compete with it (`REMAINING_WORK.md`, `PRODUCTION_READINESS_AND_PARITY.md`, `BACKEND_ARCHITECTURE_COMPLETION.md`, `audits/SCREEN_AUDIT.md`, `audits/FLOW_COVERAGE.md`) were deleted on 2026-08-08 and live only in git history.

This is a snapshot of what's built vs. not. `docs/DECISIONS.md` is the authoritative record of *why* each built piece works the way it does — update it, not this file, when a new ambiguous call is made.

## Complete — Event Architecture V3 (D-131)

**All 18 phases (0–17) are ✅ complete as of 2026-07-29 (Phase 17 / D-192, the last).** The backend program
is done end-to-end; the §12 Client Integration Backlog items named as deferred in individual phase entries
(D-190 etc.) remain open as scoped, named future work, not silent gaps.

The event system is being rebuilt against [`../architecture/EVENT_ARCHITECTURE_V3.md`](../architecture/EVENT_ARCHITECTURE_V3.md) — **frozen, not to be redesigned** — delivered by the 18-phase [`V3_IMPLEMENTATION_ROADMAP.md`](../architecture/V3_IMPLEMENTATION_ROADMAP.md). **This table is the live phase status.** One phase at a time; each ends with build + full test suite + docs + a completion report, then stops for approval. Decision numbers **D-130…D-180** are reserved for the program (D-130/D-131 = guardrails, D-132 = the declarative-template law); Phases 15–17 are recorded as **D-183/D-184/D-190/D-192** in the shared D-181+ continuation range, with an
independent review pass recorded as **D-193**, followed by admin-scoped org read (**D-194**), the
web→admin trust/safety migration (**D-195**), and three verification-only audits with no code
changes — Developer Mode (**D-196**), full-platform functional verification (**D-197**), and
end-to-end commerce/event-lifecycle verification (**D-198**) — followed by the Refund HTTP surface and
certificate cold-start warm-up that closed both of D-198's findings (**D-199**), and a final polish pass
before commit — the D-197 RSC/DataTable bug fixed, a real D-018 gap in D-199's own new code closed, and a
documentation consistency sweep (**D-200**), the Professional Identity System — Allies
(mutual connections, new `AllyConnection` table), event/participation/attendance dedup, and
source-agnostic Achievements (**D-201, D-202, D-203, D-205, D-206**; D-204 unused), a deep
integration pass — notification wiring, identity exposed on every real-user list
(EventAssignment/OrgMember/AttendeeRow/Speaker), ally suggestions/batch-status/mutual-detail, and
public people search (**D-207, D-208, D-209**), a final repository-wide audit closing the
group/team-roster identity gap, a second camelCase-wire bug in gamification, and event-review
author profile links, with chat sender identity named as a deliberately deferred gap (**D-210**),
a stabilization pass closing an independent release-readiness audit's three blocking findings
(mutual-detail privacy, an unbounded batch endpoint, unindexed public search) plus a set of
Medium/Low fixes (**D-211**), and a repository-wide completion pass — six orphaned Flutter
organizer screens wired into navigation, a new Flutter Team/Staff Assignment screen, a full
gamification client-contract rebuild (points/badges/leaderboard), dead backend-code removal, and
`flutter analyze` taken to zero issues (**D-212**), a same-day fix for a `DefaultTabController`
length regression D-212 introduced, caught by an independent verification pass (**D-213**), and a
fix for mobile checkout being unreachable — `TicketTypeTile.onBook` was never wired to the existing
`checkout_page.dart`/route, found during a full-codebase read-through, plus deletion of the
superseded dead-code stub (`book_register_page.dart`) it sat next to (**D-214**), and a
security/correctness pass from a full-repository read-through — login OTP storage moved off
unsalted SHA-256 onto the HMAC-peppered `IOtpService` substrate with the login contract
deliberately unchanged (**D-215**), `TICKET_HMAC_SECRET` made mandatory in Production and resolved
through `ISecretProvider` (**D-216**), ADR-AM14's Production Redis fail-closed check finally
implemented (**D-217**), and `FixtureView.Participants` given a dedicated output DTO with a
server-resolved `SubjectName` (**D-218**), and the Professional Identity V2 program
(`architecture/PROFESSIONAL_IDENTITY_V2_REVIEW.md`) — the profile write surface: four visibility
flags, per-connection ally visibility, profile-image upload and the editable fields on `/v1/me`
(**D-219**); `EducationJson` retained as self-declared data, superseding D-041's promise to migrate
it into `membership_claims` (**D-220**); profile visibility unified into one central four-tier
resolver, with trust exposing positive signals only (**D-221**); four already-existing verified data
sources wired into the profile (**D-222**); the Professional Journey as a first-attainment ladder
rather than a second timeline (**D-223**); the profile fact-set — one materialisation of a person's
verified facts, projected by pure engines (**D-224**); the Identity, Experience and Metrics engines,
with the headline merged into identity labels (**D-225**); and professional relationships derived
rather than scored (**D-226**); client rendering for the derivation engines, making provenance
visible and "hidden" distinct from "zero" (**D-227**); and the Professional Resume and contributions
heatmap — the two surfaces that aggregate everything (**D-228**); and production-audit remediation —
one canonical pipeline, viewer parity, and privacy-safe caching (**D-229**); and the Allies
multi-party policy pass — the more private choice wins on per-connection visibility, moderation
reaching every display surface, and decline becoming a 30-day cooldown (**D-230**); and privacy writes
becoming one atomic jsonb merge, with the row lock that "fixed" it first recorded as forbidden
(**D-231**); the public allies route made viewer-aware while the counterparty gate stays on the
boolean (**D-232**), one batched visibility primitive replacing 15 scattered `ProfilePublic` reads
(**D-233**), profile metrics counting completed activity only (**D-234**), and a privacy refusal no
longer rendering like an outage (**D-235**).

**Zero-trust production-readiness audit and remediation (2026-08-02/03).** A full-repository audit run
against every layer, then remediated in two parallel workstreams. Two **P0 defects were reproduced
before being fixed**, both silent in production because the durable record stayed correct while a cache
or a guard did not:

- **Wallet lost updates** across payment capture, refund and the ledger job — six concurrent payments
  credited one (₹500 of ₹600 missing from the org's balance), six concurrent refunds left ₹500 of
  phantom withdrawable funds. Fixed with in-SQL increments; the money-side reconciliation job that
  inventory and registrations always had was added alongside (**D-240**).
- **Refresh rotation was not single-use** — two parallel refreshes of one token both returned 200,
  forking a stolen token into a live session with reuse detection never firing. Now an atomic claim;
  the race loser is refused but not treated as theft, with client single-flight in web and admin
  (**D-241**).

Also: count-based limits (`PerUserLimit`, group capacity) serialised with advisory locks (**D-242**),
recurring jobs made single-run and cadence-tuned (**D-243**), required secrets strength-checked rather
than merely present (**D-244**), own-list endpoints bounded and N+1s removed — which surfaced a live
bug where every buyer saw an empty My Tickets page because the web schema required a field the API
never sent (**D-245**), and response-schema coverage raised 18→44 of 426 operations behind a ratchet
(**D-246**).

In parallel: convergence backfills moved off the boot path (**D-250**), 429 brought into the one error
model (**D-251**), the dead admin throttle tier removed (**D-252**), Redis made lazy, single and
fault-tolerant (**D-253**), I/O removed from the signing-key resolver (**D-254**), rate limits counted
in Redis rather than per process (**D-255**), containers made unprivileged (**D-256**), currency
carried with every amount to every client (**D-257**), a stale "not implemented" note corrected
(**D-258**), and the API contract generated, committed and CI-gated (**D-259**).

*Verified:* backend 1017 passed / 0 failed / 1 skipped (1018 total); web and admin typecheck + build
clean; `flutter analyze` clean with 266 mobile tests passing. (D-236–D-239 and D-247–D-249 unused;
next free: **D-260**.)

**Known, still open after this pass:** `TeamService` carries the same count-then-act race that D-242
fixed in the Group path — and Team is the *authoritative* model, Group its legacy mirror.
`TicketType.Sold` remains a read-modify-write mirror that can undercount organizer-facing figures (it
cannot oversell — the inventory pool guards that). Public-vote rate limiting is bypassable under
concurrency. ~~`EventRegistrationService.MirrorOrderCoreAsync` resolves a Registration by `OrderId` with a
read-then-insert~~ — ✅ **closed by [D-336](../DECISIONS.md) (2026-08-12)**: three producers could run that
check-then-act for one order at once (the in-transaction money path, the boot-triggered `DataBackfillJob`,
and `RegistrationReconciliationJob`), and the loser threw `23505` as an unhandled `DbUpdateException` → 500
where §17.1 promises an idempotent no-op — a 500 that a gateway webhook answers with a retry. Now serialised
by the transaction-scoped advisory lock the same method already used for credentials, so the loser reads the
winner's committed row instead. Surfaced by `WalletConcurrencyTests` in a **full-suite** run only, which is
why it went unrecorded for so long. **Response-schema coverage was closed out in [D-313](../DECISIONS.md) (2026-08-09):
480 of 518 operations are now declared (439 JSON + 35 `204` + 6 binary), up from 122; 34 remain pending
plus 3 permanently intentional, and `scripts/openapi-response-check.mjs` fails CI on any new undeclared
response so the count can only go down.**

*Why:* the as-built taxonomy carried 145 event types read by no filter, sort, permission check, pricing rule, notification, or analytics grouping — the D-018 unfinished-scaffolding pattern at taxonomy scale. V3 replaces it with 20 capability-bearing Kinds plus ~40 capabilities, and adds the OrgUnit tree, audience rules, inventory pools, and a registration/admission/pass layer.

| Wave | # | Phase | State |
|---|---|---|---|
| **A — Foundations** | 0 | Guardrails — CI, analytics fabrication removal, view stream | ✅ **Complete** (D-130, D-131) |
| | 1 | Kind registry — 20 Kinds + 145 aliases | ✅ **Complete** (2026-07-22) |
| | 2 | Capability registry — ~45 caps + §19 matrix | ✅ **Complete** (2026-07-22) |
| | 3 | Money (amount + currency) | ✅ **Complete** (2026-07-22) |
| **B — Context & Access** | 4 | OrgUnit tree | ✅ **Complete** (2026-07-22) |
| | 5 | Audience rules 🔴 authz | ✅ **Complete** (2026-07-22) |
| | 6 | Participants | ✅ **Complete** (2026-07-22) |
| **C — Money path** ⚠️ | 7 | Inventory pools | ✅ **Complete** (2026-07-23) |
| | 8 | Registration / Admission / Credential | ✅ **Complete** (2026-07-23) |
| | 9 | Passes, VAR, concurrency contract | ✅ **Approved / Complete** (2026-07-23) — authority cut-over (Option A); reviewed + review-fixed + approved |
| **D — Competitive** | 10 | Teams (retires `Group`) | ✅ **Approved / Complete** (2026-07-24) — Team subsystem, formation only; Group kept as a legacy mirror |
| | 11 | Stages, fixtures, scoring | ✅ **Approved / Complete** (2026-07-24) — competition engine (Option A); reviewed + review-fixed (C1/C2/H1/H2) + approved; spectator pool config-only; deterministic scoring, no anomaly detection |
| | 12 | Structure & series | ✅ **Approved / Complete** (2026-07-24) — AgendaItem (extend `EventSession`); composition depth ≤ 3; structural discovery rule; `EventSeries` (RECURRING/EDITIONS, rrule); reviewed + review-fixed (H1) + approved |
| | 13 | Delegated & walk-in registration | ✅ **Approved / Complete** (2026-07-24) — SeatBlock (unassigned admissions + delegate console); staff walk-in (WalkIn pool, idempotent); reuses the authoritative Order→Ticket projection; reviewed + review-fixed (H1/M1/M3/M4) + approved |
| **E — Governance** | 14 | Lifecycle, gates & approvals | ✅ **Approved / Complete** (2026-07-24) — additive lifecycle (Scheduled/Live/Completed, Published preserved); five validation gates; OrgUnit-inherited approval chains; material change + refund window; reviewed + review-fixed (H1/H2/M1/M2/M4) + final-verified + approved |
| | 15 | Templates & generated builder | ✅ **Approved / Complete** (2026-07-24) — scoped + versioned templates; snapshot-at-creation (`created_from_template_version`); closed capability-registry validation; backend-generated workspace + read-only publish-checklist projection; reviewed + review-fixed (H1/H2/M1/M2/M3 + M5) + re-verified + approved; **backend only** — 4-screen wizard deferred to client work (§12) |
| | 16 | Search & discovery | ✅ **Complete, 2026-07-29** (D-184 backend 2026-07-25 + D-190 client 2026-07-29) — outbox-fed FTS+trigram index, deterministic ranking, `/for-you` eligibility feed, RECURRING listing collapse; Web + Flutter now consume kind/mode/price filters, the `/for-you` rail, and a Kind quick-browse rail. **Deferred, named in D-190**: proximity/location filtering, language picker, recent searches/saved filters, `Cache-Control` caching, a dedicated Web category/kind browse page, Flutter i18n |
| | 17 | Analytics rebuild | ✅ **Complete, 2026-07-29** (D-192) — `EventAnalyticsDaily`/`OrganizationAnalytics` pre-aggregated tables and their nightly job retired; `IAnalyticsFactSource`/`LeafFactSource` reads every number at request time from leaf-fact tables (VAR, Registration, Ticket, EventView, Refund); dual-tree org-unit revenue rollup (`includeDescendantUnits`); zero admin/web/mobile changes required (D-184's routes/DTOs unchanged) |

**Hard ordering constraints:** 5→4 · 9→3,7 · 10→8 · 11→10,7 · 13→8,9 · 15→2 · 17→9,4.

**Delivery model:** backend-capability-first. The Wave-C phases (7 Inventory, 8 Registration, 9 Passes/VAR, 10 Teams) shipped **API-only**; their Web/Flutter/Admin surfaces land within the *existing* later phases (organiser config → 15, attendee read/discovery → 16, competitive attendee → 11, finance/VAR → 17) — **no extra phase**. Per-capability tracker: [`V3_IMPLEMENTATION_ROADMAP.md` §12 "Client Integration Backlog"](../architecture/V3_IMPLEMENTATION_ROADMAP.md).

**Scale:** ~22 migrations, ~450 files, ~450 new tests. Waves A–B are low-risk; Wave C is the money path. Development data only — no production users or real payments (roadmap B-4), so Wave C is a backfill rather than a dual-write migration with a reconciliation soak.

**Standing risk:** the unmerged `feat/template-engine` worktree (D-114) holds a parallel event implementation that collides with Phases 1, 2 and 15. V3 is scoped around it; the collision did **not** materialise — Phase 15 activated its own `EventTemplate` additively and that worktree remains unmerged. Re-raise before any attempt to merge it.

**Phase 1 note (2026-07-22):** delivered additively — `event_kinds` (20), `kind_aliases` (145, data-driven), `events.kind_slug` (backfilled + derived on create/clone/update), `GET /v1/kinds`; the legacy 3/13/145 taxonomy is untouched. The V3 §21.1 "schema shapes in the first migration" urgency is relaxed by B-4 (no production data), so each shape lands in its assigned phase per the roadmap's own §6 (Money→3, Inventory→7, Registration→8, VAR→9, structural→11–12) rather than being pre-landed here. Recorded here (not a new D-NNN) since it introduces no architecture.

**Taxonomy admin note (2026-07-29, D-188/D-189):** the legacy 3/13/145 taxonomy — previously developer-seeded-only, per Phase 1 above — is now a fully admin-manageable Platform Console module (`/platform/event-taxonomy`), not still frozen/code-only. This is **not** a reversal of this program's Kind/Capability direction: taxonomy is the platform-admin-owned classification an organizer picks from in Create Event; Kind/Capability (this table, §14/§21.2) remains the closed, V3-governed *behavioral* engine. The two are complementary — see D-189 for the full reconciliation. §21.2's "freeze new type additions"/"legacy read-only shadow" language describes the eventual *resolution* path off `CategoryId`/`TypeId`/`AudienceLevelId` toward `kind_slug`-driven behavior, not a freeze on who may curate the human-facing catalog those columns still point at today.

**Phase 2 note (2026-07-22):** delivered additively — `capabilities` (~45, V3 §11/§19), `kind_capability_defaults` (the §19 matrix), `event_capabilities` (materialized per event on create/clone/update + backfilled). Resolution = universal defaults + §19 matrix + mode-gating (§11.3) + the depends_on DAG (§11.4). `GET /v1/capabilities` · `/v1/kinds/{slug}/capabilities` · `/v1/orgs/{orgId}/events/{eventId}/capabilities`. Distinct from — and leaves untouched — trust-capabilities (M7) and the D-116 workspace-capabilities. No new D-NNN — implements V3 §11.

**Phase 3 note (2026-07-22):** delivered additively per V3 §9.1 — a `currency` column (ISO-4217, default INR) on every money-bearing table (orders, order_items, ticket_types, refunds, transfers, ledger_entries, withdrawals, organization_wallet, payout_schedules, both analytics) + `settlement_currency` on events/organizations; the `Money` value type; events bind their settlement currency from the Org and expose it (`settlement_currency` on event detail); clones inherit it. Existing `*_paise` amounts (D-004) untouched; multi-currency settlement is out of scope. No new D-NNN — implements V3 §9.1.

## Done

### Password reset — the recovery code is optional, because the server always said so (D-329, 2026-08-12)

**Client-only. `git diff` on `backend/Kurx.Infrastructure/Auth/` is empty.** `PasswordResetService` has
always taken **either** factor — a recovery code when one is supplied, otherwise a satisfied step-up. Web,
admin and Flutter each required the code to *submit at all*, a rule the server never had.

**Why it mattered more than a stricter form usually does.** Recovery codes come from exactly one call
(`POST /v1/auth/recovery-codes` — authenticated *and* step-up-gated), and nothing issues them at
registration, so most accounts hold **zero**. The one screen whose premise is "this person cannot sign in"
therefore refused nearly everyone **before a request was sent**. Reproduced live: `remaining: 0`, `/reset`
dead, OTP sign-in working fine the whole time.

The field is now optional on all three clients and the server rules, answering `second_factor_required`
when neither factor holds. **Removing the recovery-code feature was considered and refused** — it is also
a second-factor login method (`LoginApprovalService.cs:168`) and the required half of `RedeemAsync`; and
OTP-alone reset was refused outright as the thing INV-B exists to forbid.

**The drift ran client → doc.** `AUTHENTICATION_API.md`, `AUTHENTICATION_ARCHITECTURE.md` and
`AUTHENTICATION_SECURITY.md` were all correct; only `AUTHENTICATION_UI.md`'s W8 row had copied the
clients' rule — so every check that compares the spec to the *server* had nothing to catch.

**Web had no reset test at all**, which is how it survived there; `web/test/password-reset.test.tsx` is
new, and Flutter gained the twin case.

*Verified:* web `tsc` clean + **514** (was 510) · admin `tsc` clean + **32** · `flutter analyze` clean +
**417** (was 416). Backend not rebuilt — nothing under `backend/` changed.

**Unchanged and deliberate:** an account with neither a recovery code nor a trusted device still has no
self-service reset. That is the documented posture, not a gap this closes — such a user signs in by OTP
and sets a password in the Security Center.

### DB-8 / DB-9 — state vocabulary in the schema, and drift that pages (D-328, 2026-08-12)

**DB-8.** Four CHECK constraints — `orders."Status"`, `tickets."State"`, `events."Status"`,
`ledger_entries."State"` — in one migration (`AddStateVocabularyCheckConstraints`), each derived from its
enum by a `StateVocabulary<TEnum>()` helper so the schema and the C# cannot drift apart. **Vocabulary only:
no transition or authorization rule moved into the database**, and the valid-value tests prove it by walking
each enum in declaration order (an event goes `Draft` → `Archived`; an order reaches `Refunded` never having
been `Paid`, and both are accepted). Existing values were inspected first: `events."Status"` is the only
column with a retired member, and `MigrateInReviewToPendingReview` had already rewritten every `InReview`
row on 2026-08-04. ~80 other status columns were **refused** — the pre-existing 23 constraints already cover
the financial and inventory invariants, and 85 more would be 85 things to keep in step for a bug that has
never occurred.

**DB-9.** The premise: `WalletReconciliationJob` said drift *"should page rather than sit in a dashboard
nobody reads"* — and then logged at `Error` and stopped, so nothing paged. Metrics now split across three
meters (and therefore three CloudWatch namespaces): `Kurx.Auth` (application, existing), `Kurx.Database`
(**engine facts AWS does not publish** — the CloudWatch `Deadlocks` metric is Aurora-only and bloat is not
an AWS concept, so both are sampled from `pg_stat_*` by a new 5-minute `DatabaseHealthProbeJob`), and
`Kurx.Reconciliation` (financial correctness — where green means the invariant was *checked and held*, not
that the database is up). All three reconciliation jobs now record `clean` / `drift` / `failed`, with
`failed` deliberately distinct because an invariant that could not be checked is **unverified, not held**.
**No automatic financial repair was added**; `IWalletService.RepairAsync` stays human-triggered.

**Replication lag: DEFERRED** — Kurx has no read replica, and an alarm on a dimension with no data would sit
in `INSUFFICIENT_DATA` while appearing on a dashboard as coverage. The design is recorded for when one
arrives.

*Verified:* backend Release `-warnaserror` clean; full suite **1743 total / 1737 passed / 1 skipped / 5
failed** (1 h 21 m) against the 2026-08-11 baseline of 1709 / 1697 / 1 / **11**. **Failures fell from 11 to
5**, and both remaining causes are environmental — 4 `ClamAvUploadPathTests` (no clamd; opt-in
`--profile scanning`) and 1 `NoContentDeclarationTests` (the `-p:ArtifactsPath` artifact). **The 3
`EventAudienceAuthorizationTests` the baseline recorded as failing on committed `HEAD` now pass.** This
phase added 33 tests across 3 classes, all passing.

**DB-8 is RUNTIME VERIFIED** — the migration is applied to the live dev database and all four constraints
read back out of `pg_constraint`; tests assert PostgreSQL's own `23514` on real seeded rows, including one
that applies the migration's `Up()` over a populated table *and* over one holding a legacy value, where it
correctly refuses. **DB-9's infrastructure half is CONFIGURED ONLY**: `terraform fmt` + `validate` pass
against the real AWS provider schema (v5.100.0), but this Terraform has never been applied — no alarm has
fired, no parameter group exists, `pg_stat_statements` is enabled nowhere, and **neither SNS topic has a
subscriber**. Deployment checklist:
[`../deployment/DATABASE_RUNBOOKS.md`](../deployment/DATABASE_RUNBOOKS.md).

### DB-6 / DB-7 — large lists, streaming export, and three indexes refused on evidence (D-325, 2026-08-11)

**Zero indexes added, and that is the finding.** All three the audit proposed were refused after inspection:
`notifications.CreatedAt` **already exists** (D-324 shipped it — confirmed in `pg_indexes`, not in a doc); a
GIN trigram index on `events.Title` was built and measured on a 200k-row dataset and the query plan **did not
change**, because the predicate is `ILIKE … OR org.Name ILIKE …` and an `OR` spanning two tables is evaluated
after the join; and `tags.Name` turned out to be case-insensitive *equality* inside a `foreach`, so the cost
was N round trips rather than a scan — batched into one query instead. Measured: 138 ms current shape →
138/179 ms with the index (inert) → 1.8 ms single-column → ~0.3 ms as a `UNION` of two indexed branches. The
query rewrite that would unlock the index is recorded with its numbers but **not done** — `ListForAdminAsync`
composes a dozen filters and three sort modes, and the endpoint is admin-only and already capped at 1,000.
No migration was needed, so the `KurxDbContextModelSnapshot` conflict with the concurrent session never arose.

**The attendee CSV was the only unbounded export** (admin events caps at 1,000; analytics is bounded by
day-rows). It built the whole roster, one `StringBuilder` and a `MemoryStream` — three copies resident at
once. It now returns a writer delegate and streams 500-row keyset batches, authorization resolved eagerly so
a refusal is still a clean 404 before any byte. Bytes are unchanged: UTF-8, no BOM, same header, same
escaping, same column order.

**Fourteen paginated orderings gained a tie-breaker** across `AttendeeService`, `EventReviewService`,
`PublicProfileService` (8), `OrderService`, `RefundService`, `WalletService`, `NotificationService`,
`OrgService`, `InvitationService` (2) and `AllyService` (4) — `ORDER BY CreatedAt` alone is not a total order,
and bulk writes make same-instant rows the norm rather than the exception. **Reviews deliberately stayed on
OFFSET**, reversing this phase's own initial proposal: no shipped client can request page 2 (web uses the
default page 1, its host tab hardcodes `(1, 50)`, Flutter renders one `ListView`), so a cursor contract would
have been complexity for a path nothing calls.

**No API or client change.** Nothing calls `attendees/export` from web, admin or Flutter today.

*Verified:* backend Release `-warnaserror` clean; full suite **1709 total / 1697 passed / 1 skipped / 11
failed** (25m24s) — of the 11, 4 are ClamAV-not-running, 1 is an `ArtifactsPath` runner artifact, and **3
`EventAudienceAuthorizationTests` fail on committed `HEAD`**, reproduced in a pristine worktree. Query plans
are **LOCAL VERIFIED** on a purpose-built 200k-row scratch database; production index-usage measurement is
**REQUIRES PRODUCTION OBSERVATION** and was not attempted. No existing index was removed.

### UI/UX redesign merged into the feature branch, with four refusals (D-314, 2026-08-09)

`origin/naveen` — the redesign program's phases 39–50, 299 files — merged into
`feat/messages-settings`. 26 conflicts resolved by hand. Web typecheck + 478 · admin typecheck + 27 ·
`flutter analyze` clean + 395 · backend build clean · backend suite 1576/1578 (1 skipped, 1 unconfirmed).

**Taken:** the redesign; the `SelectCard`/`SelectCardGroup` accessibility primitives (real radio groups
replacing grids of `<button>` on the wizard's five single-select steps); admin's first test suite;
the `OrgEndpoints` fix letting a self-representing host actually manage their own event's tickets; and
the deletion of five verified-orphan `/host` routes.

**Refused, with reasons in [D-314](../DECISIONS.md):** the incoming `D-288…D-291` (they collide with four
of ours dated two days earlier — theirs need renumbering); the deletion of `booking-form.tsx` and its page
(removed there as a fake checkout, which is the exact defect **D-302 had already fixed properly** — the
branch forked before that fix); and Workspace's "Create event" button (**D-305** removed it deliberately).

**Still open:** `InviteLinkConcurrencyTests` is **unconfirmed, not passing** — it under-filled (24 of 25
seats, so the cap held) while two suites and a live backend shared one Postgres. Needs an isolated re-run
per CLAUDE.md §9. Nothing pushed.

### Account creation time — exact to the owner, month-only in public (D-312, 2026-08-09)

Backend build clean · Flutter 410 · Web 486 · Admin 27 · contract-check 0 errors · openapi-response-check
0 new / 0 stale.

**No schema change.** `users.CreatedAt` already existed and was already immutable — a grep for
`.CreatedAt =` across `Kurx.Infrastructure` and `Kurx.Api` finds no assignment to a user's stamp outside
the entity default, so nothing on the login, onboarding, profile-edit, password or device paths writes
it. The gap was exposure, not storage: it was on no response.

`GET /v1/me` now serves `created_at` (full UTC) to the owner; the public profile serves `joined_at` at
**month precision** (`"2026-08"`), truncated in the projection so the exact signup instant is never on a
public wire. Both clients render "Joined Kurx · August 2026" and "Account created · <local date/time>".

**A year-month is a label, not an instant.** `new Date("2026-08")` is midnight UTC, so formatting it in
the viewer's zone renders the previous month west of Greenwich — and the previous *year* every January.
Web pins `timeZone: "UTC"`; Flutter builds the string from parsed parts. Both are tested against exactly
that slip. The exact timestamp is the opposite case and *is* localised, which forced web's settings
display into a client component: a server component would have rendered the server's timezone.

**Also closed:** `PATCH /v1/me/profile` ran no validator at all, leaving `name`, `headline` and `bio`
unbounded while only mobile's form capped bio at 300 — a UI hint, not a constraint. Now bounded
server-side (`ProfileValidators`), mirroring the organisation limits.

### First-time-user onboarding — one completion rule, mandatory password, date of birth (D-311, 2026-08-09)

On `feat/messages-settings`. Mobile: `flutter analyze` clean, **405 tests passed**. Backend: build clean,
`OnboardingCompletionTests` **15/15**.

**Onboarding completion had two implementations that had already drifted.** `GET /v1/me` tested
`Name == ""`; `GET /v1/auth/registration/status` tested `IsNullOrWhiteSpace(Name)`. Neither consulted the
credential store, so an account with no password reported itself fully set up — and the mobile ceremony,
on any failure of the status call, defaulted to *"assume they have a password"* and jumped to the success
screen, so a user could finish registration having never seen a password field. Both endpoints now call
`Kurx.Domain.Onboarding`, the single rule: name, username, date of birth, and a password.

**`users.DateOfBirth` added** (nullable `date`, migration `AddUserDateOfBirth`) — `DateOnly`, because a
`timestamptz` birth date shifts the calendar day outside UTC. Minimum age 13, server-validated. It is the
only fact on an account that an age-restricted event's existing `MinAge`/`MaxAge` eligibility can read.
`created_at` is now served by `/v1/me` too — the column was always written, never exposed.

**The "existing phone still gets an OTP" report was closed as by-design.** `otp/verify` is a combined
sign-in-or-register ceremony; a pre-check would be an account-enumeration oracle. The labels were the
defect and were fixed instead — "First time? Sign in with a code" → "Use a one-time code instead".

**Not retroactive.** Date of birth and password bind only accounts created at or after the
`AddUserDateOfBirth` migration timestamp; identity (name + username) still binds every account. An
established user is prompted via `remaining`, never blocked. **Web's wizard changed too** — its profile
step submitted no date of birth (which would have looped it on that step forever) and it had no password
step at all. Both fixed; the dead `completeProfileAction` duplicate was deleted rather than updated.

### Event review hardening — representative details, transition claims, review ownership (D-266 M4/M5 addenda, 2026-08-06)

On `feat/messages-settings`. Backend suite **1440 passed / 0 failed / 1 skipped**; Release build with
`-warnaserror` clean; web + admin typecheck and `flutter analyze` clean.

**Representative details are mandatory on an institutional authorization (D-266 M5 addendum).**
`OfficialPhone` is required in E.164 and `RepresentativeRole` comes from a closed 17-value vocabulary
validated server-side — an unvalidated list is free text that merely looks analysable. `Other` carries the
typed title. `RepresentativeUserId` optionally links the signatory's Kurx account and is **a link, never a
grant**: it confers no authority (D-269 keeps that). Changing the signatory resets an approval, because a
verdict describes the details a reviewer actually read. A free-mail official address **warns and never
refuses** — real colleges run on Gmail; the warning lives in the console because a validator can only refuse.
Omitting a document on a re-file now **keeps** the stored one rather than deleting it: a client is never
given the storage key, so treating omission as removal destroyed the letter when someone fixed a typo.
Migration `AddRepresentativeDetails`.

**A transition is claimed in SQL (D-266 M4 addendum).** `TransitionAsync` ran every gate against a snapshot
and then wrote unconditionally, so two reviewers deciding in the same moment both wrote — status went to
whoever committed last while **both** verdicts landed in `verification_reviews` (no unique index). The status
write is now a conditional `UPDATE … WHERE Status = <the one we read>`, answering **`transition_conflict`**
on 0 rows. Reuses the compare-and-swap idiom already in `ChatService`/`OrderService`/`RefundService`; EF
`RowVersion` and `xmin` were rejected to avoid a second mechanism, and row locks are ruled out by D-231.

**Review items have an owner.** `UnderReview` said an item was claimed but never by whom, so two reviewers
could work one event against per-reviewer checklists. `events.review_claimed_by`/`review_claimed_at` are set
and cleared in the *same statement* as the status; `release_review` and the three decisions refuse anyone
else with **`claimed_by_another_reviewer`** (admins may override so an offline reviewer cannot strand an
item); any decision releases the hold. The queue returns the holder and the console shows "You're reviewing
this" / "Held by …". Migration `AddReviewClaimOwnership`.

**Also fixed:** two unique-index insert races that returned 500 (wizard autosave from two tabs, and a
double-clicked review checkbox), both now adopting the winner; a mangled E.164 regex that had been refusing
*every* authorization submit; two admin console links (`/e/{slug}`, `/o/{slug}`) that were **relative** and
so 404'd on the console's own origin, leaving a reviewer unable to open the event they were judging — fixed
via `WEB_URL`, mirroring web's existing `NEXT_PUBLIC_ADMIN_URL` (D-195); and mobile publish refusals that had
no copy and no surface, which now name the blocker and point at the web Readiness tab.

### Main Application IA + Posts clients + Event creation (D-262, D-265, 2026-08-03)

Three pieces of client-and-contract work, on `feat/event-creation` (which carries `feat/posts-module`).

**Navigation restructured to the product flow's Main Application areas.** Bottom nav / sidebar is now
**Home · Community · Posts · Messages · Workspace**, with **Profile top-left and Notifications
top-right** as fixed app-bar corners on every screen. The flow names seven areas and a pill nav holds
five; Profile and Notifications are the two you visit and come back from rather than dwell in, so
they get corners instead of consuming a tab. Browse / Tickets / Saved stopped being tabs and moved
under Home and Profile, matching the flow's own nesting. Web mirrors the same order so both surfaces
teach one map. Flutter: `features/shell/presentation/app_shell.dart` +
`common/widgets/kurx_shell_app_bar.dart`; web: `components/layout/app-shell.tsx`.

**Workspace is role-scoped, not a screen.** *(Terminology updated by [D-305](../DECISIONS.md): this tab is
the **User Workspace**; what an event opens into is the **Event Host Workspace**. Create Event moved to
**Profile** and now runs an eligibility gate before the form. User Workspace is an activity hub and never an
organizer dashboard.)* The tab lists every event you have a role in:
registering unlocks that event's **participant workspace**; an event you host opens its **host
workspace only after admin approval** (pre-approval rows show their status instead of pretending to
be a working workspace). One person is routinely both, for different events — it is per-event role,
never an app-wide mode. The participant workspace is a launcher over screens that already exist;
tiles with no endpoint behind them (Tasks, Resources/Files, Attendance) are **absent rather than
dead**, and web's is deliberately thinner than Flutter's because web has no competition or
leaderboard page yet.

**Posts clients (D-262).** Full UI on web and Flutter against the frozen contract the backend session
built to: feed, composer (text / media / polls / visibility), post detail with one level of comment
replies, reshare, hashtag and event feeds, My Posts, Saved Posts, a user's posts, edit, report, and
trending. Flutter media uses the already-installed `file_picker` — no new dependency. Admin gets
**no** Posts moderation surface (product decision); the `hide`/`unhide` endpoints exist unused.

**Event creation (D-265).** 41 new columns on `Event`/`TicketType`/`Sponsor` and three new tables
(`registration_consents`, `coupons`, `coupon_redemptions`) — the fields the 18-step wizard needed and
could not previously write: tagline, short description, logo/thumbnail/promo video, rules, FAQ, the
whole legal block, event-level registration and check-in windows, result and certificate dates,
building/floor/room, meeting platform and password, age and gender eligibility, event-wide team cap,
platform fee, tax and prize pool. Delivered as six optional trailing input groups so every
pre-existing client body binds unchanged, applied by **one `ApplyFieldGroups` shared by create and
update**. Wizards: web **5 steps → 10**, Flutter **one 236-line form → 6 steps**.

Two defects were found and fixed during verification, both worth recording because the second is the
kind that ships quietly:
- The 41 fields were initially **write-only** — persisted, but absent from `EventDetail`/`ToEventJson`,
  so no edit form could prefill and no page could render them.
- Fixing that nearly **published `MeetingPassword`**: that projection also serves the public
  `GET /v1/events/{slug}`. `EventLocationDetailView` therefore has no password field at all, pinned
  by a test asserting the value never appears in a response body.

**Still open on event creation:** coupon service + endpoints (entities only — `RedeemedCount` must be
claimed in SQL when it gains a consumer, D-240/D-261), `GET …/preview`, the publish-checklist
extension for the new required-for-publish fields, the consent write at registration, a
registrant-gated meeting-password read, and admin event-creation surfaces.


### Production Re-Architecture — Trust, Verification, Payments, Admin, Fraud (M0–M13, D-039–D-052)

The backend's core differentiator — a real trust system — shipped as 13 dependency-ordered modules on branch `feat/production-rearchitecture`. **166/166 integration tests green.** Module-by-module detail: [`CHANGELOG.md`](../../CHANGELOG.md) and [`.claude/memory/trust-verification.md`](../../.claude/memory/trust-verification.md); each has a decision in [`DECISIONS.md`](../DECISIONS.md).

- **M0 (D-039)** — trust substrate: polymorphic `verification_documents` + `verification_reviews`.
- **M2 (D-040)** — platform roles (`platform_roles`) read live per request; `PlatformRoleClaimsTransformation` strips token-supplied claims.
- **M1 (D-041)** — dropped `users.IsKurxAdmin`; profile fields are authority-zero.
- **M3 (D-042)** — person identity verification (`user_identity_verifications`), masked last-4 only, `/v1/me/identity/*`.
- **M4 (D-043)** — organization registry: typed/canonical + `organization_aliases` + fuzzy `GET /v1/orgs/search`.
- **M5 (D-044)** — org verification lifecycle + reviewer actions (`/v1/admin/orgs/*`).
- **M6 (D-045)** — membership claims (`membership_claims`), evidence-backed, `/v1/orgs/{id}/membership-claims`.
- **M7 (D-046)** — trust capability matrix (`ITrustService`); L0–L5 labels; `/v1/orgs/{id}/my-capabilities`.
- **M8 (D-047)** — event approval + live payment gate; `GET .../payment-readiness`.
- **M9 (D-048)** — KYC term split: `kyc_records` → `org_bank_verifications`; dead `KycEndpoints` deleted.
- **M10 (D-049)** — paid checkout + ledger write-path: capture webhook (`POST /v1/webhooks/razorpay`) → ticket + Collected ledger + wallet. Drives `MockPaymentGateway` (real Razorpay adapter still pending).
- **M11 (D-050)** — forms convergence onto ticket-scoped `FormField`; `registration_forms/*` dropped (supersedes D-024).
- **M12 (D-051)** — admin verification console (backend): merge, blacklist, cross-subject history, VerificationReviewer-gated.
- **M13 (D-052)** — fraud: `blacklist_entries` + `fraud_signals`; blacklisted/high-risk users lose `CanOrganizePaid`, cascading to M8/M10 gates.

**Infrastructure & DevOps**
- **.NET 10**: All four backend projects (`Kurx.{Api,Application,Infrastructure,Domain}`) target `net10.0`; Docker images on `mcr.microsoft.com/dotnet/sdk:10.0` + `aspnet:10.0`; `global.json` pins SDK `10.0.301`.
- **EF Core 10 + Npgsql 10.0.2**: Aligned to .NET 10 stack; EF migrations auto-apply at startup with connectivity abort-on-failure.
- **GitHub Actions CI** (`.github/workflows/ci.yml`): Backend restore/build (`-warnaserror`)/test against Postgres 17; web typecheck/lint/build; admin typecheck/build; NuGet + npm caches; test artifact upload.
- **GitHub Actions CD** (`.github/workflows/cd.yml`): Docker build + push to GHCR; auto staging deploy on `main` push (SSH + `docker compose`); production deploy on GitHub Release gated by Environment manual approval with auto-rollback on health-check failure.
- **Per-user rate limiting**: Global `PartitionedRateLimiter` — 300 req/min per authenticated user (3 000 for `kurx_admin`), 60 req/min per IP anonymous; `"otp"` fixed-window per IP; `"heavy"` concurrency policy (5 permits, queue 2) on CSV import, bulk send, and announcement fan-out; `OnRejected` returns RFC 7807 ProblemDetails + `Retry-After` header.
- **i18n foundation**: Backend `IStringLocalizer<SharedResources>` with `en`/`hi` `.resx` files (invitation/announcement/push strings); frontend `next-intl` 3.26.3 with `localePrefix: "never"` (cookie-based locale, no URL changes), `en.json`/`hi.json` message files, `Intl`-based currency/date formatters, `LanguageSwitcher` component.

**Auth & orgs slice**
- WhatsApp OTP login (request/verify/refresh/logout, `/v1/me`), JWT access + rotating refresh tokens, reuse-detection revokes all sessions.
- Organizations: CRUD, memberships with an Owner/Manager/Staff/Finance role matrix, bank/PAN KYC with Razorpay Route linked-account activation, T1 payout schedule seeded at creation.

**Phase 1 — Foundation Hardening (complete, D-017)**
- Secret management: fail-closed production validation for `JWT_SECRET` and the Postgres connection string.
- Global exception handling: RFC7807 `ProblemDetails` for every error response, correlation IDs.
- FluentValidation on every request DTO.
- Safe JWT claim parsing, a `KurxAdmin` authorization policy, explicit token validation parameters.
- Next.js `(app)` route group actually enforces session auth (previously dead code).
- Real health checks (Postgres, Redis if configured, local-disk storage).
- Structured logging enriched with correlation id / user id / org id.
- SignalR hubs wired up, authenticated, and membership-checked.

**Phase 2 — Event Management (complete, D-018)**
- Event CRUD with the full content field set and the `Draft → InReview → Published → Closed → Archived` status workflow, including unpublish and delete-draft.
- Categories (3-level taxonomy with ordering/visibility), tags (auto-created, searchable), venues, speakers, sponsors, multi-day schedule (sessions/breaks), and media (gallery/documents via `IStorage` presign).
- Reusable event templates: 11 system-seeded plus org-custom ones.
- Public discovery API: search (keyword/category/city/date/organizer + pagination/sorting), upcoming, trending, featured, latest, event detail (by slug, with view counting), related.
- Organizer dashboard (web): real event list/create/edit pages with status-workflow actions and venue/schedule/speakers/sponsors/media management.

**Phase 3 — Ticketing & Registration (complete, commit 6b5a2e1, D-020)**
- `TicketType` CRUD: name, price (paise), pricing unit (per-ticket / per-group), registration mode (individual / group with min–max), quantity, sale window, per-user limit, all-access flag.
- Inventory guards: deletion blocked if `sold > 0`; quantity cannot be dropped below `sold`.
- `FormField` custom registration forms nested under a ticket type: snake_case key, label, type (Text/Number/Select/Checkbox/Date/File), scope (PerRegistration / PerParticipant), required, options, sort.
- Public read: only on-sale types for Published events; org-facing list returns all types.
- 11 integration tests.

**Phase B — Invitations & Announcements (complete)**
- `EventInvitation` model + `event_invitations` table: per-guest tracking with invite token, RSVP status, send/resend/revoke, CSV bulk import (up to 1 000 rows), RSVP public page.
- `EventAnnouncement` model + `event_announcements` table: broadcast to `AllRegistrants`, `CheckedIn`, `NotCheckedIn`, or `TicketType` audience; scheduled sends; push + email + WhatsApp channels with per-channel delivery counters.
- `whatsapp_messages` outbound log table (all WA send attempts).
- `seat_holds`, `ticket_transfers`, `gate_entries` operational tables added.
- `IStringLocalizer<SharedResources>` wired into `InvitationService` for bilingual email/WA message strings.
- REST endpoints: `/v1/events/{id}/invitations` (CRUD + import + bulk-send + resend + revoke + public RSVP), `/v1/events/{id}/announcements` (CRUD + stats).
- `"heavy"` rate limit applied to CSV import, bulk send, and announcement create.

**Phase C — Event Chat (shipped: backend, Flutter and web)**
- `ChatRoom` / `ChatMember` / `ChatMessage` domain model and tables.
- `ChatHub` (SignalR): exactly three methods — `JoinRoom` (membership re-checked at connect), `LeaveRoom`, `SendMessage`.
- `ChatService`: room create, text message history (`before`-pivot paging), pin, soft delete, mute/ban/unban, room policy + status.
- REST endpoints: `/v1/events/{id}/chat`, `/v1/chat/rooms/{id}/messages`, `/v1/me/chats`, plus pin/mute/ban/report routes. See [`docs/EVENT_CHAT_ARCHITECTURE.md`](../EVENT_CHAT_ARCHITECTURE.md).
- Per-room sliding-window rate limit in `ChatHub` (Redis-backed when `REDIS_CONNECTION` is set, always-allow fallback in single-instance dev mode).
- **Membership wired (Phase 2A, D-105):** publish creates the room and seeds hosts; staff join on acceptance; refund and ticket transfer keep membership correct. Accepting an invitation grants no chat access on its own — membership follows the resulting order.
- **Attachments (Phase 4, D-110–D-112):** `chat_attachments`, presign → PUT → confirm through `IStorage`, allow-list + magic-byte validation, orphan sweep. Clients on Flutter and web. **`IFileScanner` is no longer a no-op** — `FILE_SCANNER=clamav` is real INSTREAM scanning that fails closed (D-298); `none` stays the dev default and provides no protection.
- **Messaging features (D-292 – D-296):** edit, delete-for-me, unsend, reactions, forward, link previews, shared media, delivery + read receipts, message search (Postgres FTS + GIN), conversation pin/mute/archive, and expiring message pins (1 h–30 d, default 7 d, expiry evaluated at read time). Both clients.
- **Chat roles (D-300 / D-301):** `Member < Moderator < Host`. Automatic Host comes from four sources only — event creator, org Owner, Manager, Representative. Staff/Speaker/Judge/Mentor/Volunteer/Participant are Members; **Moderator is an explicit promotion by a Host**, and promotion is Host-only.
- **Authority sync (D-304):** an organization role change reaches already-published rooms through the transactional outbox, never an inline call after commit.
- **Archive is retrievable (D-306):** `GET /v1/me/chats?archived=` + `MyChatView.Archived`. Before it, archiving an event room removed it from the only list that could return it.
- **Presence (Phase 5, D-114 / D-118 / D-119):** online status, typing indicators and read receipts over the existing `ChatHub`, Redis-backed and cleanly absent without it. Clients on Flutter and web.
- **Clients:** Flutter (Phase 3A, D-108) and web (Phase 3B, D-109) chat clients are **shipped**, with attachment and presence parity.
- **Lifecycle (D-122 – D-124):** chat state is derived from event state — no room before approval, `Active` while the event runs, `Locked` the moment it ends, `Archived` seven days later, one-way. Enforced immediately by `ChatLifecycle.EffectiveStatus`; the hourly sweep and first-access both persist it through one compare-and-set.
- **Not built:** threads, end-to-end encryption, quiet hours, a members-listing endpoint, retention/deletion. Threads are excluded by design — a reply quotes a message, it does not open a sub-conversation.
- Canonical reference: [`docs/EVENT_CHAT_ARCHITECTURE.md`](../EVENT_CHAT_ARCHITECTURE.md) — now the **only** chat document (D-309).

> Corrected 2026-07-18 (D-104), again 2026-07-19 (D-120), and again **2026-08-08**: the "Not built" line
> still listed **reactions, search, promote/demote/kick and notification preferences**, all of which had
> shipped in D-292 – D-301, and called `IFileScanner` a no-op two weeks after ClamAV landed. This entry has
> now been wrong in the same direction three times — it lists what was true when a phase closed and is not
> revisited when a later phase fills it in. Treat a "Not built" list here as a claim to verify, not a fact.

**Posts — the social feed (backend shipped 2026-08-03, D-262)**
- 12 tables: `posts`, `post_media`, `post_polls`, `post_poll_options`, `post_poll_ballots`, `post_poll_votes`, `post_likes`, `post_comments`, `post_comment_likes`, `post_saves`, `post_hashtags`, `post_mentions`. Additive migration; no existing table altered.
- `IPostService` / `PostService`: feed, single post, my posts, saved, profile, per-event and per-hashtag listings, trending tags, create/edit/soft-delete, media, likes, saves, poll voting, comments and replies, and the moderation queue.
- **Visibility** — `public` / `connections` (accepted allies) / `event_participants` (live ticket check, D-015) / `only_me` — enforced by ONE predicate composed by every read path. A post the caller may not see is **404, never 403** (D-018).
- **The feed is an explicit graph**, not "everything public": own posts + accepted allies + followed orgs' event posts + ticketed events' posts. The single most consequential call in the module; reasoning in D-262.
- Keyset pagination on `(CreatedAt, Id)` with opaque cursors, the rule chat set in D-104.
- Media reuses `ChatAttachment`'s two-step upload (presign → PUT → confirm, all authoritative checks on confirm) with its own allow-list — posts carry video, chat does not — plus an hourly `post-media-cleanup` orphan sweep.
- Reporting **reuses** the existing polymorphic `Report` queue (`entityType` `post` / `post_comment`); no second report table.
- Counters (`LikeCount`, `CommentCount`, `ShareCount`, `VoteCount`, `TotalVotes`) all mutate in SQL (D-240), verified by `PostConcurrencyTests` firing real parallel requests. `PostPollBallot` exists so one voter cannot cast two ballots by racing disjoint selections.
- **Every value-returning route carries `.Produces<T>()`** — the module does not add to the platform-wide OpenAPI gap below.
- 71 integration tests (`PostTests.cs`, `PostConcurrencyTests.cs`).
- **Post search shipped 2026-08-08 (D-297):** `GET /v1/posts/search`, full-text over post bodies via
  `websearch_to_tsquery` with a GIN index on the same expression (migration `AddPostBodyFtsIndex`),
  reusing the one `VisibleTo` predicate so search cannot surface what the feed hides. Clients on web
  (`/posts?q=`) and Flutter (`/posts/search`). Results are chronological — ranking is not built.
- **Not built:** reactions beyond like, post editing of media/polls, video duration/dimension probing,
  feed ranking (deliberately chronological), search across post *comments*, notification preferences.
- **Clients:** the web client shipped alongside this in a parallel workstream; Flutter and admin are not wired yet.

**Account settings + Direct messages (shipped 2026-08-04, D-263 / D-264)**
- **Settings (D-263)** — notification preferences, blocked accounts, language, username history, email change, scheduled deletion. Backend + web + Flutter.
  - `notification_preferences` (row per user × category, 4 channels) and `user_blocks`. Additive migration `AddAccountSettings`.
  - **Preferences bind at the single dispatch point**: the check lives inside `INotificationService.NotifyAsync`, so all 23 existing emitters inherit it. `security` is un-mutable, enforced on both the write path and dispatch. `in_app`/`push` are enforced today; `email`/`whatsapp` are stored but not yet consulted by the senders that own those channels (named gap, D-263).
  - **Blocks are enforced symmetrically** on the live Posts feed, single reads, comments, ally requests and DMs.
  - **Deletion is scheduled, reversible for 30 days**, then a daily `account-deletion` job anonymises: PII cleared and authored posts/comments soft-deleted; orders, tickets, ledger, certificates and audit logs retained (statutory retention).
  - **No TOTP** — a deliberate decision, not a gap. Passkeys, trusted devices and recovery codes already ship; TOTP would be the weakest of four factors. Reasoning in D-263.
  - **Phone change was not rebuilt**: a complete ceremony already existed at `/v1/me/phone/verify`; D-263 added step-up and a change notice to it rather than shipping a parallel one (D-018).
- **Direct messages (D-264)** — 1:1 conversations. Backend + web + Flutter.
  - `ChatRoom.EventId` is now nullable with `ChatRoomKind.Direct`; the per-event uniqueness index is scoped to non-null. A DM **is** a `ChatRoom`, so messages, attachments, presence, read pointers, the hub and the offline outbox work on it unchanged.
  - **One room per pair**, enforced by a unique index on the canonical `(low, high)` pair — verified under concurrent creation from both sides.
  - **Message requests are the spam control**: a DM from a non-ally lands `pending` and notifies nobody until accepted. Allies skip it.
  - `ChatMember.ArchivedAt` is a per-user folder, deliberately distinct from `ChatRoomStatus.Archived`.
  - Clients: web `/chats` and Flutter `my_chats_page` both gained Direct · Events · Requests · Archived; a Message button opens a conversation from a public profile.
- **Not built:** TOTP (decided against), group DMs, admin messaging surface (product decision: none).

**Messaging Phase 2 (shipped 2026-08-08, D-292 · D-293 · D-294 · D-295 · D-296 · D-298)**
- **D-292 the request gate** — replying to a message request accepts it, the rule every messenger uses. Before this a room stayed `pending` forever, which is exactly the state the notification job refuses to notify on: the recipient answered, the sender was never told, the conversation died silently. Also re-keyed both clients from eventId to **roomId** (a DM has no event) and fixed a snake_case/camelCase break that made *every* web chat parse throw.
- **D-293** edit message (bounded window, sender only — a host removes a message, never rewrites one) and delete-for-me, applied to all six per-member reads so a hidden message cannot leak on one surface.
- **D-294** hub rate limiting (in-process, per connection — a connection cannot migrate between instances), Redis fail-open for presence, and eviction on member *removal*, which had leaked since D-106 while ban did not.
- **D-295** reactions · full-text search · delivery receipts · forwarding · link previews · per-member pin/mute/archive · shared media. Search scopes by membership **inside** the query, because filtering afterwards still leaks result counts.
- **D-296** a pinned message expires (1h–30d, default 7d), evaluated at read time so nothing sweeps and lapsed pins free their slot.
- **D-298** real malware scanning (`FILE_SCANNER=clamav`, **fails closed**), voice notes and video capture on both clients.
- **Event Rooms are cancelled, not deferred** — one event, one room, everyone in it. The moderation tools already give hosts what separate rooms were meant to provide.
- **Not built:** E2E encryption for DMs. It removes server-side DM search, push preview text and a moderator's ability to read a reported message — a product decision with irreversible consequences, not a feature to batch in.
- 37 backend tests (`AccountSettingsTests`, `DirectMessageTests`); web 145, Flutter 285, both clean.

**Event creation — Profile entry, gated (shipped 2026-08-08, D-305 · D-307)**
- **Create Event moved to Profile** and now opens a **gate**, not the form. Web's `/profile` was a redirect into Settings; it is now a real hub mirroring Flutter's. The Workspace header button and the primary-nav entry are gone — a creation button on Workspace is what made it read as an organizer dashboard.
- **The gate runs before the eleven steps**: ① eligibility from existing verification state (never re-asking a passed check) ② **Public or Private**. Enforced, not offered — web's gate *is* the page; Flutter's form route redirects back without a valid product.
- **D-307 — a free public event now requires the full verification set.** `can_create_public_event` = identity + PAN + bank (penny drop passed, holder name matched) + fraud-clear. Publishing to the public is itself a trust event; bounding verification by *payment* was the wrong axis. `can_create_private_event` is constant true — Private cannot be Listed, take payment, or reach any discovery surface. **`can_organize_paid` is unchanged**; the money path is untouched.
- **Public/Private maps to the existing `EventProduct`**, still derived from the Type's `ProductClass` (D-266 M1). The gate filters which Types are offered; it never overrides the derivation. No third concept, no merge with `EventVisibility`.
- **Private form constraints** now enforced up front rather than at publish: no `Listed` visibility, no Paid, no price field — previously the form accepted all three and the server refused eleven steps later.
- **The wizard now creates the event's first ticket.** It created none, and an event with no ticket type cannot be registered for — every event the wizard produced was unbookable and nothing said so.
- **Flutter aligned to web's eleven steps** (it had seven), adding six fields it never sent — `description` (required to publish, so every Flutter draft was unpublishable), `venueAddress`, `capacity`, `resultDate`, `certificateReleaseAt`, `cancellationPolicy` — plus the whole Pricing step. Also removed a dead `Private` visibility option that silently produced a **Listed** event.
- **Five backend surfaces web never reached** are now wired: participations, waitlist, org-invitations, points, badges.
- Backend `TrustTests` 9/9 · web 350 · Flutter 129 (organizer/events/core) · all builds clean.
- ⚠️ `MockKycProvider` approves unconditionally, so the bank chain passes for anyone in dev. Nothing here is evidence that real penny-drop verification works.

**Attendee event journey — end to end (shipped 2026-08-08, D-302)**
- **A storage key is not a URL.** Every event projection returned bare `banner_key` / `logo_key` / `thumbnail_key` / `promo_video_key` and the whole media gallery; every client rendered them as image sources. The platform stored event imagery, returned it on every response and displayed **none of it on any surface**. Presigned `*_url` companions now sit beside every key.
- **One card projection** (`SearchService.SummaryProjection`) shared by all seven discovery rails *and* saved events, which previously hand-built its own summary and lacked every field the other gained. One serializer renders it.
- **Card facts**: `event_mode`, `category_name`, `price_from_paise`, `currency`, `is_featured`. Mode and price were already *filters* — discovery let a person narrow by a value the card refused to show. **`price_from_paise: null` means no ticket type exists, not free.**
- **Representation named** — `representing.{name, slug, is_verified}`, previously a bare Guid, so no surface could name the institution behind an event. Self-represented resolves to `null` (D-268).
- **Booking is real** — `GET /v1/events/{id}/ticket-types` + `POST /v1/events/{id}/orders` via a server action with an idempotency key. It previously hardcoded two unrelated ticket types and opened Razorpay with **no order id and no amount**, reporting success for a registration that never happened.
- **The ticket QR worked on neither client** — web drew a decorative `lucide` glyph no scanner can read; Flutter used `Image.network` against an authorized endpoint, so every request 401'd and the fallback showed permanently. Both now fetch through the authenticated path (Flutter via Dio, so an expired token refreshes; web via a same-origin token-attaching proxy).
- **Avatars carry the identical bug and are deliberately untouched** — deferred to the Profile & Professional Identity workstream. See D-302 §"Deliberately not fixed here".
- Web 326 tests, Flutter 352, both clean.

**Frontend production integration (complete)**
- ⚠️ **The line below was wrong when written and is corrected by D-302.** Booking was a stub that faked success and `/tickets` drew a decorative QR — two fake paths on the primary attendee journey, both on routes this claimed were connected. "Connected to a real API" was verified per *page*, never per *action*, which is how a page that fetches real data and then submits nowhere passed.
- All Next.js `web/` pages connected to real backend APIs — no fake/hardcoded data on any route.
- `demo-data.ts` deleted (contained fake events, profile, org, certificate, and host metrics).
- Public pages (`/`, `/discover`, `/e/[slug]`, `/o/[slug]`, `/u/[username]`, `/verify/[code]`) fetch from the real API with `notFound()` on missing resources.
- Authenticated page (`/tickets`) calls `GET /v1/orders` via server-side `requireSession()`.
- `admin/` "Coming Soon" placeholder replaces the hardcoded stats grid.
- New backend endpoints added to support frontend: `GET /v1/public/orgs/{slug}` (`OrgEndpoints`), `GET /v1/certificates/{code}` (`CertificateEndpoints`).

**Phase 10 (partial) — Mobile attendee slices (D-019)**
- Flutter attendee app (`mobile/`): WhatsApp OTP login, secure token storage with silent 401→refresh, public event browse (upcoming, search & filters, detail, related, ticket types), public guest browse without login, discovery home dashboard (Featured carousel, Trending/Upcoming/Latest rows, category chips).
- `flutter analyze` clean, 29 tests green.
- Payments, ticket purchase/QR, check-in, wallet, notifications deliberately excluded (blocked on unbuilt backend).

**Orders, groups & ticket issuance — free-registration path (complete, D-021)**
- `OrderService`: free individual and group registration, real EF transaction against `Order`/`OrderItem`/`Group`/`GroupMember`/`Ticket`; `TicketType.Sold` incremented at issuance (individual) and incrementally per join (group), respecting the D-020 inventory guards.
- Ticket HMAC signing via the existing `TokenService.SignTicketCode` (shared with `GateEntryService`/`TicketTransferService`, so freshly-issued tickets verify at the gate).
- Ticket resend: real QR PNG (`IQrCodeGenerator`) persisted via `IStorage`, emailed as an attachment and WhatsApp-logged with a presigned link.
- Paid ticket types were rejected with `payment_not_supported_yet` in this D-021 slice — **superseded by M10/D-049**, which added the full paid-checkout write-path (capture webhook → ticket + Collected ledger + wallet) on the same `Order.Status`/`Payment`/`Refund` model. See the re-architecture section above.
- 10 integration tests (`Kurx.Tests/OrderTests.cs`).
- **Also fixed in this pass (D-034):** `Program.cs` rate-limiter middleware ran before authentication, so every authenticated request was bucketed under the 60/min anonymous-IP limit instead of the 300/min per-user one.

**Real certificate + ticket-QR rendering — system templates (complete, D-035)**
- `ICertificateRenderer` (`CertificateRenderer`) now renders real PDF + PNG certificates via QuestPDF, replacing the old stub that returned QR bytes labeled as a PDF. Two system layouts (`classic-certificate`, `modern-certificate`), seeded by the new `DesignTemplateSeeder`, both parameterized by `DesignTemplate.AccentColor`.
- `ICertificateService`/`CertificateService`: bulk, idempotent, organizer-triggered generation (`POST /v1/events/{eventId}/certificates/generate`) — resolves the event → org → system template (D-023), renders, persists via `IStorage`, emails when the recipient has an email on file.
- `GET /v1/certificates/{code}` now returns a presigned `pdf_url` instead of a raw storage key.
- `GET /v1/tickets/{code}/qr.png`: real scannable QR image via the already-real `IQrCodeGenerator`, replacing the icon/mock QR on web (`/tickets`) and mobile (`QrView`) — frontend wiring is a follow-up.
- 4 integration tests (`Kurx.Tests/CertificateTests.cs`), asserting real (non-placeholder) PDF/PNG bytes.
- Custom (org-uploaded PDF/image) templates and `GeneratedCard` (invite/group cards) remain unimplemented — deferred.

**Two-product registration model — guest checkout + competition teams (complete, D-036)**
- Guest checkout: `Order.UserId` nullable; a free, non-competition, `Individual`-mode ticket type may be purchased with no Kurx account (`GuestName`/`GuestPhone`/`GuestEmail` + a `GuestAccessToken`-secured `GET/POST /v1/orders/guest/{accessToken}` view/resend). `POST /v1/events/{eventId}/orders` is no longer behind `.RequireAuthorization()` — it branches on whether a valid Bearer token is present.
- `TicketType.IsCompetition`: the one flag driving both the (computed, not stored) account requirement — `IsCompetition || PricePaise > 0` — and competition-team routing.
- Competition teams are invite-only: `JoinGroupAsync`'s open `JoinCode` path rejects `IsCompetition` groups (`competition_requires_invitation`); captains invite named teammates by Kurx username or phone (`EventInvitation.GroupId`, `POST /v1/events/{eventId}/invitations`), who accept via `POST /v1/groups/invitations/{token}/accept` (phone-verified against the invite, mirroring `TicketTransferService`'s claim check).
- Chat auto-join wired for the first time: every authenticated ticket issuance now calls the previously-unused `ChatService.EnsureRoomExistsAsync`/`AddMemberByEventAsync`.
- `Certificate.Kind` (new `CertificateKind` enum) and revocation (`IsRevoked`/`RevokedReason`/`RevokedAt`, `POST /v1/certificates/{id}/revoke`) — revoked certificates stay visible (`200`) on the verify endpoint, never `404`.
- `Event.ShortCode`: unique 6-char voice/SMS-friendly reference, generated alongside `Slug` at creation.
- 10 new integration tests across `Kurx.Tests/OrderTests.cs`, `CertificateTests.cs`, `EventTests.cs`.

**Mandatory username onboarding + phone number change (complete, D-037)**
- `needs_onboarding` now requires both `Name` and `Username` (was Name-only, D-012); `Username` stays nullable in the DB, enforced only in the application layer.
- New unauthenticated `GET /v1/usernames/availability?username=` (available/unavailable/reserved/invalid) — mobile and web stopped probing availability via `PATCH /v1/me/profile`.
- The 30-day username reclaim hold (`UsernameHistory`, D-022) is now actually enforced by both the availability endpoint and `PATCH /v1/me/profile`, not just recorded.
- New `POST /v1/me/phone/verify`: authenticated phone-number change that reassigns `User.Phone` on the existing account (`Id`/relationships untouched), reusing the existing OTP-request endpoint. **Now also revokes all other sessions — see D-038 below.**
- Fixed a concurrent-registration race in `AuthService.VerifyOtpAsync` (two simultaneous logins for the same brand-new phone could throw an unhandled unique-constraint violation) — now merges gracefully into one account, mirroring `EventService.CreateAsync`'s existing collision-retry pattern.
- Mobile: `onboarding_page.dart` rewritten into a real Name + Username form (reusing `username_claim_page.dart`'s live-check UI); `SessionController.bootstrap()` now re-validates `/v1/me` on cold start so an already-logged-in, still-usernameless user is gated too.
- Web: new `/onboarding` route + form (previously didn't exist); `requireSession()` now redirects to it when `needs_onboarding` is true; `otp-panel.tsx` routes post-login based on the flag.
- 8 new integration tests in `Kurx.Tests/AuthTests.cs` (15 total, up from 7); also fixed a pre-existing constructor-ordering bug in that file (`ResetDatabase()` was called after `CreateClient()`, letting the app's one-time startup migrate against the pre-reset database) unrelated to this feature but blocking all verification in it.

**Email uniqueness + phone-change session revocation (complete, D-038)**
- `Users.Email` is now uniquely constrained, case-insensitively, via a raw-SQL functional index (`lower("Email")`, filtered `WHERE "Email" IS NOT NULL`) in the `AddEmailUniqueIndex` migration — `Test@x.com`/`test@x.com`/`TEST@x.com` collide. The migration fails loudly (named duplicate count, no auto-merge) if case-insensitive duplicates exist at deploy time; 0 exist today (verified against the live `kurx` database, which has 0 users).
- `POST /v1/me/phone/verify` now revokes every other active refresh token for the user (same `ExecuteUpdateAsync` bulk-revoke `RefreshAsync`'s reuse-detection already used) and returns a fresh access/refresh pair for the calling device, in the standard token-response shape — a stolen device on the old number can no longer refresh.
- `RATE_LIMIT_ANON_PER_MIN` added to make the global anonymous-IP rate limit test-configurable (mirrors the existing `RATE_LIMIT_OTP_PER_MIN` pattern) — found necessary while adding tests here, since `AuthTests.cs`'s growing request volume had crossed the hardcoded 60/min ceiling D-034 had already flagged as a risk. Production default unchanged.
- 5 new integration tests in `Kurx.Tests/AuthTests.cs` (18 total, up from 15).

**Addendum 02 — Schema v2 + Background Jobs + Financial/KYC Endpoints (shipped)**
- `AddV2Schema` EF Core migration: 14 new tables, soft-delete columns on 6 existing tables, location columns on events (`Country`/`State`/`District`/`PostalCode`), `design_templates.EventId` for event-scoped templates. 66 total tables.
- Soft deletes via `deleted_at TIMESTAMPTZ NULL` on `events`, `organizations`, `venues`, `speakers`, `sponsors`, `ticket_types` with partial indexes `WHERE "DeletedAt" IS NULL` (D-025).
- Hangfire wired with PostgreSQL storage and 3 recurring jobs: `ExpireSeatHoldsJob` (minutely), `ExpireWaitlistOffersJob` (every 5 min), `CollectedToAvailableLedgerJob` (daily) (D-029).
- `ICertificateRenderer`, `IQrCodeGenerator`, `IDocumentRasterizer` interfaces + stub implementations (real QR via QRCoder; PDF/PNG stubs returning placeholders).
- `IWalletService` / `WalletService`: wallet balance view, paginated ledger, withdrawal initiation; financial access gated to Owner+Finance only (D-028).
- `IKycService` / `KycService`: KYC status list, penny-drop, PAN match; last-4 masking enforced (D-016).
- Wallet, KYC, and Order endpoint files scaffolded and mapped: `GET/POST /v1/orgs/{orgId}/wallet`, `GET /v1/orgs/{orgId}/wallet/ledger`, `POST /v1/orgs/{orgId}/wallet/withdraw`, `GET/POST /v1/orgs/{orgId}/kyc`, order + group + ticket endpoints (stub).
- New decisions: D-022 (username audit trail), D-023 (design_templates.EventId), D-024 (registration forms 4-table), D-025 (soft deletes), D-026 (location columns), D-027 (org invitations flow), D-028 (wallet cache), D-029 (Hangfire jobs).

---

## Not built yet

This is the frontier after the M0–M13 re-architecture. It is **not** the old "60% scaffolding" list — most of that was either built or deliberately retired (see below).

- **Real network providers**: Razorpay (+ Route payouts), AWS SES, WhatsApp Cloud API (outbound), S3, and a DigiLocker/real-KYC adapter are all still `console`/`mock`/`localdisk` behind their `Kurx.Application.Abstractions` interfaces. (FCM is the exception: `FirebasePushSender` is a real adapter behind `PUSH_PROVIDER=firebase`, though nothing registers a device token yet.) The **paid-checkout write-path exists** (D-049) but drives `MockPaymentGateway`, so no real money moves. Full per-provider status: [`docs/EXTERNAL_SERVICES_AND_PROVIDERS.md`](../EXTERNAL_SERVICES_AND_PROVIDERS.md).
- **Client parity**: the web organizer dashboard and the Flutter app do **not** yet wire the trust endpoints (identity, org/membership verification, capabilities, payment-readiness). `admin/` now covers the staff side of that surface — verification queue, event approval, users/orgs, blacklist/fraud, staff & roles, audit, analytics are live, and **Phase 1 (2026-07-25)** added categories, certificates, broadcast, health and a user-detail route (`admin/STATUS.md`); its finance modules wait on real payments. Three admin modules are **backend-blocked, not frontend work** (`admin/STATUS.md` §5.3–§5.4): **Templates** — `GET /v1/templates` returns Published versions only, so every Draft the write actions create is unlistable and unmanageable; **Organization list/detail** — no admin-scoped org read exists (`/v1/orgs/{id}` 404s non-members by D-018, and the search registry is Verified-only). Certificate generate/revoke is wired but **unverified** pending an event with eligible tickets. Mobile is **not** a blanket "nothing wired": most attendee/organizer pages have real data layers over the shipped endpoints (including the event-chat client, D-108), and the specific gaps are (a) the trust endpoints above; (b) real payment for a priced ticket type, deliberately stopped pending a real gateway (D-070). *(This line previously also named `notifications/`/`bookmarks/` as having no data layer at all — both got real server-backed data layers before D-201, and checkout itself was unreachable end-to-end, not just payment-gated, until D-214 wired `TicketTypeTile.onBook`; corrected here rather than left to compound.)*
- **The capability engine gates no writes** (audited 2026-08-06, deliberately **not** implemented). D-266 M2 states the matrix exists so "a Workshop may never enable Leaderboard" is expressible — but `ICapabilityService` has **no gate method at all** (`ListRegistryAsync`/`GetForArchetypeAsync`/`GetForEventAsync`/`MaterializeForEventAsync` only), and no write path consults it. This is **not** a security hole: all 237 write routes are guarded by D-269 authority or domain preconditions (Teams, the apparent worst case, needs `IsCompetition` **and** an organiser-created `TeamPolicy`). The residual gap is product integrity — an organiser can opt *their own* event into a capability their archetype marks Unsupported. **Enforcement is blocked on a prerequisite**: `events.ArchetypeSlug` is nullable, `typeId` is optional in the create contract, and resolution defaults an unknown archetype to `Unsupported` for everything — so a blanket `RequireCapability` would refuse every capability-driven write on any archetype-less event, a failure dev data (7/7 events have archetypes) cannot reveal. Needs a `D-NNN` deciding whether `typeId` becomes mandatory, then backfill, then a **fail-open** service-level guard rolled out per capability.
- **Review-queue holder is unverified in a browser.** The API and the deployed admin bundle both carry it (verified against the running stack), but the badge has not been seen rendering — the Chrome extension was unreachable for the whole session.
- **Automated fraud-signal producers**: partially closed. **`DisposableContact` now has a producer** (D-317) — a verified email on a configured throwaway domain records one signal at score 10 against a threshold of 100, so it measures without blocking; the domain list is `DISPOSABLE_EMAIL_DOMAINS`, empty by default, which leaves the feature inert until an operator opts in. The device-fingerprint / velocity / duplicate-account producers remain a follow-up, and raising the disposable score to a blocking weight is its own decision against observed volume.
- ~~**`FixtureView.Participants` reuses an input contract**~~ — ✅ **Closed 2026-08-01 ([D-218](../DECISIONS.md)).** A dedicated `FixtureSubjectView(SubjectType, SubjectId, SubjectName, Seed)` output record now carries a server-resolved display name, reusing the same `ResolveSubjectNamesAsync` helper as `StageParticipantView`/`ResultView` and resolving once per fixture list rather than per fixture. `FixtureSubjectInput` is unchanged for request bodies. Fixture UI no longer needs per-participant lookups, so admin fixture *creation* is unblocked on contract grounds.
- ~~**Competition endpoints have no `.Produces<T>()` annotations**~~ — ✅ **Closed for competition, 2026-08-01 (D-218).** The 15 value-returning routes in `CompetitionEndpoints.cs` now carry `.Produces<T>()`, so `StageView`, `StageParticipantView`, `ResultView`, `FixtureView` and `ScoringPolicyView` appear in `swagger.json`. The remaining routes in that file return `204 No Content` or an anonymous counter object and need no annotation.
- **OpenAPI response schemas are missing platform-wide** (the general form of the item above): `.Produces<T>()` appears on **no** endpoint outside `CompetitionEndpoints.cs` and `PostEndpoints.cs` (D-262, fully annotated at birth), and `TypedResults` is used nowhere, so of ~424 routes only those 15 publish a response contract. The blocker is not mechanical: ~113 sites return anonymous objects (`Results.Ok(new { … })`), which have no nameable type to annotate. Closing this means **extracting response DTOs across 68 endpoint files first** — a scoped project, not an edit pass. Until then, generated clients get request contracts only. A concrete consequence worth knowing: `GET /v1/admin/events/pending` documents only `200: OK`, so the D-259 drift gate is blind to it — the review-claim fields added on 2026-08-06 are **not** in `openapi.json` and a client/server mismatch there would not fail the build.
- **Refunds & paid group tickets**: the `Refund` model exists; the refund flow and paid *group* checkout (`paid_group_not_supported_yet`) are deferred.
- **Generated cards**: `GeneratedCard` (invite/group cards) has no renderer/endpoints; custom (org-uploaded) certificate templates and `IDocumentRasterizer`/`Docnet.Core` rasterization remain stubbed (certificate rendering itself is real, D-035).
- **Device-token registration**: the HTTP surface exists (`POST /v1/devices/register`, `PATCH`/`DELETE /v1/devices/{id}`, `GET /v1/me/devices` in `DeviceEndpoints.cs`) and a real `FirebasePushSender` is selectable via `PUSH_PROVIDER=firebase` (default `console`). What's missing is the **client**: no app registers a device token, so push still can't be delivered end-to-end.
- **Ticket transfers**: table + `ITicketTransferService` + endpoints exist, and the claim flow is now **fixed and tested** (D-062). It previously shipped with the claimant unable to see the ticket they claimed while the sender kept the rotated code — found by exercising it live, fixed by listing tickets by `Ticket.UserId` instead of the order's buyer, and locked down by `TicketTransferTests.cs` (the class that never existed, which is why it shipped). Residual, tracked in D-062: a claimant sees the buyer's order metadata alongside their ticket; `ClaimAsync` still doesn't move `GroupMember.UserId`.
- ~~**Deferred scaffolding** (kept, no service): `event_assignments`, `event_reviews`, `saved_events`, `organization_followers`.~~ **Stale — corrected 2026-08-03.** All four have live services (`EventAssignmentService`, `EventReviewService`, `SocialService`) and mapped endpoints; a flow-vs-code audit found this line describing a state that ended some time ago.
- **Deploy target**: `cd.yml` is real, but no staging/production host is provisioned and the required GitHub secrets aren't set.

> **Dropped, not pending — do not re-add.** The four-table `registration_forms/*` builder was **retired in M11/D-050**; the single registration-form system is the ticket-scoped `FormField` (D-020). The standalone `KycEndpoints.cs` was deleted in M9/D-048 (org bank verification lives in `OrgEndpoints`).

---

## Explicitly out of scope for now

Real network providers (Razorpay, SES, WhatsApp Cloud, FCM, S3, DigiLocker) are a separate gated phase, so the app **cannot operate a live paid event end-to-end today** even though the trust, approval, and paid-checkout *write-paths* are complete and tested.

**Two consequences of that gap are now explicit rather than implicit (2026-08-11).**

- **[D-322](../DECISIONS.md)** — one taxonomy classification was wrong, not one provider. `Graduation Party` sat under *Private Events ▸ Family Celebrations* mapped to the `ceremonial` archetype, making it the only `Public` Type row in the private branch and — through `ceremonial.RequiresRepresentation` — the only family occasion demanding an organization and the full D-307 gate. Re-mapped to `private-gathering`, with a conditional repair for already-seeded databases and a structural test asserting the navigation branch and the product class agree in both directions (the previous test pinned four types by name, which is why it never noticed). No live event is reclassified: `Event.Product` is snapshotted at create.
- **[D-327](../DECISIONS.md)** — the Create Event wizard's `canNext` ended in `step >= 4`, an unconditional pass, so the three fields the server requires (title, start, end) could all be skipped and the wizard first refused on step 11 of 11 — the D-305 failure reproduced inside the form D-305 created. Each step now gates its own fields and names the missing one. Also: web never checked that the end is after the start (the server and Flutter both did), Flutter could deadlock forever on a category with no types, and five inputs are now hidden for Private events on both clients — the team cap, results date and certificate release because `private-gathering` marks `teams`/`scoring`/`certificates` Unsupported, and the two age bounds as a product judgment (there is no `age` capability; an age rule turns away a stranger who registered, and a private event's attendance is its invitation list). Client-only; nothing invalid could ever have been stored.
- **[D-326](../DECISIONS.md)** — Create Event was a dead end for an unverified account in *both* directions, and only one of them was the verification gate. `CategoryResponse`'s hand-written mapper dropped `ProductClass`, so `/v1/categories` served no `product_class` at all and the wizard's Private branch offered zero categories (the Public branch, symmetrically, offered all 19 private types). Fixed on both the public and admin wire records, pinned by a test that reads the **response** rather than the entity, and `openapi.json` regenerated — which also closed D-319's and D-321's deferred regeneration in the same pass. The contract gate proves *spec ↔ mapper*, never *mapper ↔ view*; that blind spot is now written into `docs/api/README.md` and `.claude/memory/api-conventions.md`.
- **[D-323](../DECISIONS.md)** — `IDENTITY_VERIFICATION_BYPASS` makes the mock-KYC gap testable instead of merely documented. With `MockKycProvider` answering every government-ID, PAN and penny-drop call, the trust gate is real logic over simulated evidence, so enforcing it outside Production blocked all public-event and paid-checkout testing while establishing nothing. The flag skips those three proofs only; `fraudClear` stays enforced, the reported `identity_verified`/`bank_verified` facts are never forged, unrecognised values mean enforced, and startup **throws** if it is set in Production. Default off — the suite runs enforced. **This is scaffolding with a defined end: delete it when a real DigiLocker/penny-drop adapter ships**, at which point the gate starts meaning something and the flag becomes a liability rather than a convenience.
