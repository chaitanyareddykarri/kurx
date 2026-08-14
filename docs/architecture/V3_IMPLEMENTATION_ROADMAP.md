# Event Architecture V3 — Implementation Roadmap

> **ADOPTED — this is the authoritative delivery plan ([D-131](../DECISIONS.md)).**
>
> Source of truth for *design*: [`EVENT_ARCHITECTURE_V3.md`](EVENT_ARCHITECTURE_V3.md) (frozen, not to be
> redesigned). This document is the source of truth for *sequencing*.
>
> **The phase list is authoritative and immutable.** Phases are never shortened, merged, removed,
> renumbered, or reordered. Where the repository already contains work equivalent to a phase, that phase is
> **verified**, documented, and marked `COMPLETE` — it is not skipped and not renumbered.
>
> **Per-phase gate:** build → full test suite → documentation → completion report → **stop for approval**.
> One phase at a time. The repository compiles at every commit.
>
> **Live status table:** [`../roadmap/README.md`](../roadmap/README.md) §Event Architecture V3.
> **Provenance:** authored 2026-07-20; relocated from a session scratchpad into version control the same
> day, so no plan artifact lives outside the repository.

---

## 0. Status

| Phase | State | Evidence |
|---|---|---|
| **0 — Guardrails** | ✅ **COMPLETE** (2026-07-20) | verified in §2.1 below |
| **1 — Kind registry** | ✅ **COMPLETE** (2026-07-22) | see §11 |
| **2 — Capability registry** | ✅ **COMPLETE** (2026-07-22) | see §11 |
| **3 — Money** | ✅ **COMPLETE** (2026-07-22) | see §11 |
| **4 — OrgUnit tree** | ✅ **COMPLETE** (2026-07-22) | see §11 |
| **5 — Audience rules** | ✅ **COMPLETE** (2026-07-22) | see §11 |
| **6 — Participants** | ✅ **COMPLETE** (2026-07-22) | see §11 |
| **7 — Inventory** | ✅ **COMPLETE** (2026-07-23) | see §11 |
| **8 — Registration layer** | ✅ **COMPLETE** (2026-07-23) | see §11 |
| **9 — Passes, VAR, concurrency** | ✅ **APPROVED / COMPLETE** (2026-07-23) | see §11 — authority cut-over (Option A); engineering-reviewed + review-fixed + approved |
| **10 — Teams (retires `Group`)** | ✅ **APPROVED / COMPLETE** (2026-07-24) | see §11 — Team subsystem (formation only, Option A); Group retained as a legacy mirror |
| **11 — Stages, Fixtures, Scoring** | ✅ **APPROVED / COMPLETE** (2026-07-24) | see §11 — competition engine (Option A both forks); engineering-reviewed + review-fixed (C1/C2/H1/H2) + approved; spectator pool config-only; deterministic scoring; no anomaly detection |
| **12 — Structure & series** | ✅ **APPROVED / COMPLETE** (2026-07-24) | see §11 — AgendaItem (extend `EventSession`); composition depth ≤ 3; structural discovery rule; `EventSeries` (RECURRING/EDITIONS, rrule); engineering-reviewed + review-fixed (H1 member-list visibility) + approved |
| **13 — Delegated & walk-in** | ✅ **APPROVED / COMPLETE** (2026-07-24) | see §11 — SeatBlock (unassigned admissions + delegate console); staff walk-in (WalkIn pool, idempotent); reuses the authoritative Order→Ticket projection; engineering-reviewed + review-fixed (H1/M1/M3/M4) + approved |
| **14 — Lifecycle & approvals** | ✅ **APPROVED / COMPLETE** (2026-07-24) | see §11 — additive lifecycle (Scheduled/Live/Completed, Published preserved); five validation gates; OrgUnit-inherited approval chains; material change + refund window; engineering-reviewed + review-fixed (H1/H2/M1/M2/M4) + final-verified + approved |
| **15 — Templates & workspace** | ✅ **APPROVED / COMPLETE** (2026-07-24) | see §11 / D-183 — scoped + versioned templates; snapshot-at-creation (`created_from_template_version`); closed capability-registry validation; **backend-generated** workspace + **read-only** publish-checklist projection; engineering-reviewed + review-fixed (H1/H2/M1/M2/M3 + M5) + re-verified + approved; **backend only** — the 4-screen wizard is deferred client work (§12) |
| 16–17 | ⬜ Not started | — |

Decision numbers **D-130 … D-180** are reserved for this program. D-130 and D-131 are consumed.

---

## 1. Verified baseline

Everything below was measured in this session, not assumed.

| Fact | Value |
|---|---|
| Backend build | ✅ `dotnet build backend/Kurx.sln` — **0 warnings, 0 errors**, 39s |
| Backend tests | **502** `[Fact]`/`[Theory]` across **59** files |
| Local test run | ✅ works — `EventTaxonomyTests` 3/3 passed (~2m6s incl. factory + migration + seed startup) |
| EF migrations | **29**, head = `20260720045954_AddPasswordAndTrustedBrowser` |
| Web scripts | `dev build start lint typecheck test(vitest)` — all present |
| Admin scripts | `dev build start lint typecheck` — **no test script** |
| Mobile tests | **29** test files |
| CI jobs | Backend (build+test), Web (typecheck/lint/build), Admin (typecheck/**build only**) |
| Running stack | postgres · redis · backend · web · admin (docker compose, healthy) |
| Source footprint | 50 endpoints · 95 infra files · 18 entity files · 60 abstractions · 217 web ts/tsx · 50 admin ts/tsx · 183 mobile dart |

---

## 2. Blockers — ALL RESOLVED (2026-07-20)

The six blockers below were raised before Phase 1 and are **all now closed**. The original text is retained
unedited as the record of what was decided and why; the resolution is stated against each.

| # | Blocker | Resolution |
|---|---|---|
| **B-1** | 127 uncommitted files | **Proceed on the current working tree.** Existing local changes are not discarded; V3 phases layer on top. Option C-variant — accepted knowingly, with the stated trade-off that phases are not individually revertable until the tree is committed. |
| **B-2** | `feat/template-engine` worktree diverges | **Scoped around — the worktree is ignored for this program.** ⚠️ *Standing risk:* `IEventAccessService`, `EventAccessService`, `TemplateRenderService`, its `[id]/[section]` routing and `EventTaxonomyTests` collide head-on with Phases 1, 2 and 15. If that branch is ever merged, the conflict is a rewrite of both. Re-raise before Phase 15. |
| **B-3** | CI cannot meet the verification bar | **Fixed in Phase 0** — `Mobile — analyze & test` job added; `npm run lint` added to the admin job. |
| **B-4** | Production data? | **No — development data only.** No production users, no real payments. Wave C (Phases 7–9) is therefore a **backfill, not a dual-write migration with reconciliation soak**; sizing drops from weeks to days. The strangler sequence in §6 is retained for correctness, not for production safety. |
| **B-5** | Smart App Control | Standing operational note, not a blocker. A local `dotnet test` failure is triaged for `FileLoadException 0x800711C7` before being treated as a code bug. CI is the authority; Docker is the deterministic local fallback. |
| **B-6** | D-number collisions | **D-130 … D-180 reserved** for V3. D-130 (view stream) and D-131 (V3 adoption) are consumed; next free is **D-132**. |

---

### 2.1 Phase 0 — completion evidence (verified 2026-07-20)

Phase 0 was found already implemented in the working tree and was **verified rather than reimplemented**:

| Phase 0 deliverable | State | Evidence |
|---|---|---|
| Mobile CI job | ✅ | `.github/workflows/ci.yml` — `Mobile — analyze & test` (`flutter analyze` + `flutter test`) |
| Admin lint in CI | ✅ | `.github/workflows/ci.yml` — admin job runs `npm run lint` |
| Delete `Random.Shared` analytics fabrication | ✅ | `AnalyticsService.cs` — Views/UniqueVisitors counted from `EventViews` |
| Retire the divergent 2nd analytics path | ✅ | one `AnalyticsService` remains; single authoritative path |
| `ViewCount` off the synchronous read path | ✅ | `EventService.cs` appends to the view stream; `events.ViewCount` is derived |
| View-events table | ✅ | migration `20260720055102_AddEventViewStream` |
| Tests | ✅ | `EventViewStreamTests.cs` |
| Decision recorded | ✅ | **D-130** |
| Design docs under version control | ✅ | **D-131** — this file, V3, and the V2 history moved out of a session scratchpad into `docs/` |

This closes V3 §22.7 release condition 3 (the three as-built analytics defects removed in Phase 0, not later).

---

### 2.2 Original blocker text (historical record)

### 🔴 B-1 — 127 uncommitted files on the working branch

```
branch: feat/dev-workspace-auth
main...HEAD: 0 ahead, 0 behind      ← the branch has NO commits
working tree: 127 files  (72 modified, 53 untracked, 2 deleted)
```

Every change is uncommitted. Starting a multi-phase program on top of this means **no phase can be isolated, reviewed, or rolled back** — my changes would be indistinguishable from the existing in-flight work, and `git checkout .` would destroy both.

**Options:**
| | Action | Consequence |
|---|---|---|
| **A** *(recommended)* | You commit the 127 files as the dev-workspace-auth work, then I branch from that | Clean base; existing work preserved and attributable |
| B | You stash/shelve them elsewhere | Clean base; risk of losing context (memory notes: **never `git stash` in this repo** — concurrent sessions) |
| C | I branch from `main` and ignore the working tree | Phases build on a base that doesn't match your machine; merge pain later |

**I cannot safely proceed until this is resolved.** I will not commit your work for you without being asked.

### 🔴 B-2 — The `template-engine` worktree diverges on the event system

`.claude/worktrees/template-engine/` (branch `feat/template-engine`, D-114 — *"verified, not merged"* per project memory) contains a **parallel event implementation**:

```
backend/Kurx.Application/Abstractions/IEventAccessService.cs      ← does not exist on this branch
backend/Kurx.Infrastructure/Events/EventAccessService.cs
backend/Kurx.Infrastructure/Events/TemplateRenderService.cs
backend/Kurx.Tests/EventTaxonomyTests.cs · EventAssetTests.cs
web/app/(app)/host/events/[id]/[section]/page.tsx                 ← different workspace routing
web/components/host/create-event-form.tsx                         ← DELETED on this branch
```

V3 rewrites `EventService`, the taxonomy, the workspace routing, and `EventTemplate` — all four collide head-on. **If that branch merges after Phase 2, the conflict is effectively a rewrite of both.**

**Decision needed:** merge it first, abandon it, or explicitly scope V3 around it. My recommendation: **merge or abandon before Phase 1.**

### ✅ B-3 — Your verification requirements exceed what CI can do — **RESOLVED**

*Original finding:* you require `flutter analyze` / `flutter test` and admin lint after every phase, and CI at the time had no mobile job at all while the admin job ran typecheck + build but not `npm run lint`.

**Fixed as proposed.** `ci.yml` now carries a `Mobile — analyze & test` job (`flutter analyze --no-fatal-infos` + `flutter test`, Flutter pinned to 3.44.6), and the admin job runs typecheck → lint → test → build. All four surfaces are verified in CI, not just locally on Windows.

### 🟡 B-4 — Is there production data?

This determines whether Wave C is a *migration* or a *rewrite*. If `orders`/`tickets` hold real money-bearing rows, the Registration/Admission/Pass replacement needs dual-write + reconciliation + a rollback plan (weeks). If it's dev-only data, it is a backfill (days).

**I need a yes/no.** I will not guess on the money path.

### 🟡 B-5 — Smart App Control (corrected this session)

SAC is still ON (`VerifiedAndReputablePolicyState = 1`), but local `dotnet test` **does** run — my earlier stored note saying it was impossible was wrong and has been corrected. The block is *intermittent*: a local **pass** is real evidence; a local **failure** must be triaged for `FileLoadException 0x800711C7` before being treated as a code bug. **CI remains the authority.** Docker is the deterministic local fallback.

### 🟡 B-6 — Concurrent workstream / D-number collisions

Project memory records a second session editing `docs/DECISIONS.md` with colliding D-numbers. This program needs **~15 new D-NNN entries**. Proposal: reserve a block (e.g. **D-130 … D-150**) for V3 and tell me the starting number.

---

## 3. Gap analysis — V3 vs the repository

| V3 concept | Exists today | Action | Difficulty |
|---|---|---|---|
| Kind registry (20 + aliases) | `EventCategory` 3-level / 145 types | **replace**, alias-mapped | M |
| Capability registry + `EventCapability` | — | **new** | L |
| `Money(amount, currency)` | `long` paise, no currency (D-004) | **extend** every money site | M (wide, shallow) |
| OrgUnit tree | `Organization` (flat) | **new**; org becomes root unit | M |
| `Membership.attributes` (cohort_year) | `Membership` has `Role` only | **extend** | S |
| `AudienceRule` | — | **new** | M |
| `ParticipantRole` / `EventParticipant` | `EventAssignment` (14 text roles) | **replace + migrate** | M |
| `InventoryPool` | `TicketType.Quantity/Sold`, `Event.Capacity` | **replace** | L |
| `SeatHold` | ✅ `SeatHold` + `ExpireSeatHoldsJob` | **reuse** | — |
| `Registration` (polymorphic subject) | `Order` + `Ticket` | **new layer** | XL |
| `Admission` / `Credential` | `Ticket` + `Ticket.Code` | **reshape** | L |
| `Pass` / `AdmissionRight` | `TicketType` (+ `IsAllAccess`) | **replace** | L |
| `RegistrationPolicy` (5 axes) | implicit in `TicketType` | **new** | M |
| `VAR` (value allocation) | — | **new** | M |
| `Team` / `TeamMembership` / invites | `Group`, `RegistrationMode.Group` | **new**; retire `Group` | L |
| `Stage` / `Fixture` / `ScoringPolicy` | — | **new** | XL |
| Sub-events | ✅ `Event.ParentEventId` | **reuse** + depth cap | S |
| `AgendaItem` | ✅ `EventSession` | **rename/extend** | S |
| `EventSeries` (recurring + editions) | — | **new** | M |
| `Template` (scoped, versioned) | `EventTemplate` (inert, `"[]"`) | **activate** | M |
| `ApprovalChain` | — | **new** | M |
| Five validation gates | one publish check | **replace** | S |
| Material change / refund window | — | **new** | M |
| Delegated registration / `SeatBlock` | — | **new** | L |
| Walk-in (offline-capable) | — | **new** | L |
| Dual-tree analytics | single-tree, partly fabricated | **rebuild** | L |
| Search index (outbox-fed) | `ILIKE` on title/subtitle | **new** | L |

---

## 4. Phase list — 18 phases, 5 waves

Every phase: compiles, runs, ships value, is independently revertable.

### Wave A — Foundations *(no behavioural change to events)*

| # | Phase | Delivers | DB | Risk |
|---|---|---|---|---|
| **0** ✅ | Guardrails — **COMPLETE** | Mobile CI job; admin lint in CI; **delete `Random.Shared` analytics fabrication**; retire the divergent 2nd analytics path; move `ViewCount` off the read path; reserve D-numbers | 1 migration (`AddEventViewStream`) | 🟢 |
| **1** | Kind registry | `event_kinds` (20) + `kind_aliases` (145); `Event.KindSlug` backfilled; category *derived*; `/v1/kinds`; old columns still written | additive | 🟢 |
| **2** | Capability registry | `capabilities` (~40), `kind_defaults`, `event_capabilities`; capability contract + `depends_on` DAG; presets applied to existing events | additive | 🟡 |
| **3** | Money | `currency` on every money-bearing table, default `INR`; `Money` value type; display at the edge | additive, wide | 🟡 |

### Wave B — Context & Access

| # | Phase | Delivers | DB | Risk |
|---|---|---|---|---|
| **4** ✅ | OrgUnit tree — **COMPLETE** | `org_units` (recursive, materialised path); every Org gets a root unit; `Event.OrgUnitId`; permission union walks the chain | additive | 🟡 |
| **5** ✅ | Audience rules — **COMPLETE** | `Membership.attributes` (incl. `cohort_year`); `audience_rules`; server-side eligibility at register **and** admission; `applies_to` | additive | 🔴 authz |
| **6** ✅ | Participants — **COMPLETE** | `participant_roles` (7 classes) + `event_participants`; migrate `EventAssignment`; capabilities *reference* roles; `counts_toward_capacity`; COI rules | migrate | 🟡 |

### Wave C — The money path  ⚠️ highest risk

| # | Phase | Delivers | DB | Risk |
|---|---|---|---|---|
| **7** ✅ | Inventory — **COMPLETE** | `inventory_pools` (segment/channel/unit); one `general` pool per TicketType, dual-written; release policy; waitlist re-pointed to pools | dual-write | 🔴 |
| **8** ✅ | Registration layer — **COMPLETE** | `registrations` (polymorphic subject) · `admissions` · `credentials`; `registration_policies` (5 axes); Order/Ticket become the legacy shadow | dual-write | 🔴 |
| **9** | Passes, VAR, concurrency | `passes` + `admission_rights` (scope/channel/window_policy/draws); **VAR** on order lines; refund allocation; **§17.1 contract** — hold → lock order → conditional decrement → idempotency → reconciliation job | cut-over | 🔴 |

### Wave D — Competitive & structural

| # | Phase | Delivers | DB | Risk |
|---|---|---|---|---|
| **10** | Teams | `teams` · `team_memberships` · invites · join requests · `TeamPolicy`; merge/split as guarded transactions; **retire `Group`** | migrate | 🟡 |
| **11** | Stages | `stages` · `fixtures` · `scoring_policies` · `results`; advancement; public-vote fraud controls; spectator pools | additive | 🟡 |
| **12** | Structure & series | `EventSession` → `AgendaItem`; depth-3 cap; discovery rules; `event_series` (RECURRING + EDITIONS), RRULE, per-occurrence timezone | migrate | 🟡 |
| **13** | Delegated & walk-in | `seat_blocks` + unassigned admissions + **delegate console** (3rd persona); offline-capable walk-in with idempotent reconcile | additive | 🟡 |

### Wave E — Governance & surfaces

| # | Phase | Delivers | DB | Risk |
|---|---|---|---|---|
| **14** | Lifecycle & approvals | `approval_chains` (OrgUnit-inherited); **five validation gates**; `SCHEDULED` vs `LIVE`; material-change + refund window | additive | 🟡 |
| **15** | Templates & builder | Scoped + versioned templates; `created_from_template_version`; workspace **generated** from `workspace_tab` declarations; 4-screen wizard | migrate | 🟡 |
| **16** | Search & discovery | Outbox-fed index (Postgres FTS + trigram); alias search; eligibility-aware feeds preserving 404-not-403; ranking replaces `ViewCount DESC` | additive | 🟡 |
| **17** | Analytics | Leaf-attributed facts; dual-tree rollup; kind/template/series/channel/academic_year dimensions; VAR-based revenue | migrate | 🟡 |

**Ordering constraints (hard):** 5→4 · 9→3,7 · 10→8 · 11→10,7 · 13→8,9 · 15→2 · 17→9,4

---

## 5. Change summary by surface

| Surface | Phases touched | Est. files | Nature |
|---|---|---|---|
| **Database** | all | ~22 migrations | Additive-first; 3 cut-overs (7, 8, 9); no destructive drops until a full release of parallel-run |
| **Backend — Domain** | 1,2,4,5,6,7,8,9,10,11,12 | ~18 existing + ~14 new entity files | New aggregates; `Enums.cs` grows ~15 enums |
| **Backend — Application** | all | ~20 of 60 abstractions | New service interfaces; `IEventService` splits |
| **Backend — Infrastructure** | all | ~45 of 95 | `EventService` (840 LOC) decomposes across Kind/Capability/Inventory/Registration services |
| **Backend — API** | 1,2,5,6,8,9,10,11,13,14,16 | ~28 of 50 endpoint files | ~40 new routes; existing `/v1/events/*` preserved via adapters through Wave C |
| **Backend — Tests** | all | 59 → ~110 files, 502 → **~950 tests** | Integration-only, real `kurx_test`; every authz branch |
| **Web** | 1,2,5,6,10,11,13,14,15,16 | ~70 of 217 | Wizard rebuild; workspace generated from capabilities; delegate console (new persona); discovery filters |
| **Admin** | 1,2,6,14,16,17 | ~20 of 50 | Kind/capability registry management; approval-chain config; **taxonomy admin UI (absent today)** |
| **Flutter** | 1,5,10,11,12,16 | ~45 of 183 | DTO regen (freezed/json_serializable); kind-aware discovery; team formation; stage results |
| **Background jobs** | 0,7,9,12,16,17 | 13 existing + ~5 new | New: inventory reconciliation, no-show release, series materialisation, index projection, VAR settlement |
| **Analytics** | 0,17 | ~8 | Delete fabrication (P0); rebuild on leaf facts (P17) |
| **Search** | 16 | ~10 | New index + outbox projector |
| **Permissions** | 4,5,6,14 | ~12 | Ancestor-chain union; participant grants as 4th source; live evaluation preserved (D-015) |

> **Delivery is backend-capability-first; client parity follows within these same phases (no extra phase).** The
> Wave-C backends (7 Inventory, 8 Registration, 9 Passes/VAR, 10 Teams) shipped **API-only**; their Web/Flutter/Admin
> surfaces land in the *existing* phases above (organiser config → **15**, attendee read/discovery → **16**,
> competitive attendee → **11**, finance/VAR → **17**). The explicit per-capability tracker is **§12 Client
> Integration Backlog**.

---

## 6. Migration strategy

**Strangler, per V3 §21.2. No big bang, no destructive drop until one full release of verified parallel-run.**

```
Per cut-over phase (7, 8, 9):
  1. ADD     new tables/columns; old path untouched
  2. BACKFILL from existing rows (idempotent, re-runnable)
  3. DUAL-WRITE both models; new model is authoritative for reads only behind a flag
  4. RECONCILE a job proves old ≡ new; alert on drift
  5. FLIP    reads to the new model; keep writing both
  6. VERIFY  one full release
  7. STOP    writing the old model; retain columns as legacy_* indefinitely
```

**V3 §21.1 — the six shapes that must land early** (they cannot be retrofitted into populated tables):

| Shape | Lands in |
|---|---|
| `Money(amount, currency)` | Phase 3 |
| `InventoryPool` | Phase 7 |
| `Registration.subject` polymorphic | Phase 8 |
| `RegistrationPolicy` 5 axes | Phase 8 |
| `VAR` on order lines | Phase 9 |
| Structural discriminator (Stage/AgendaItem) | Phase 11–12 |

Backward compatibility: `/v1/events/*`, `/v1/categories`, and `/v1/orgs/{id}/events/*` keep their current response shapes through Wave C via adapters. Mobile and web are cut over per-surface in Waves D–E, so no client is ever forced to ship in lockstep with the backend.

---

## 7. Risks

| # | Risk | Sev | Mitigation |
|---|---|---|---|
| 1 | **Wave C touches live money** (orders/tickets/ledger/payouts) | 🔴 | Depends on B-4. Dual-write + reconciliation + flag-flip + one-release soak. **No phase merges without a green reconciliation run.** |
| 2 | **Concurrency defects in Phase 9** — oversell / double-charge | 🔴 | V3 §17.1 implemented in full before the first paid event; reconciliation job with alerting; load test for the last-seat race |
| 3 | **Audience rules are an authz surface** (Phase 5) | 🔴 | Mandatory security review (CLAUDE.md §7); deny-default; server-side only; test every negative branch |
| 4 | **template-engine merge conflict** | 🔴 | **B-2 must be resolved before Phase 1** |
| 5 | **127 uncommitted files** | 🔴 | **B-1 must be resolved before Phase 1** |
| 6 | Capability combinatorics (~40) | 🟡 | `depends_on` shallow DAG enforced; one test per capability + declared edge; reject undeclared coupling at review |
| 7 | Test suite runtime (502 → ~950) | 🟡 | Cross-class parallelisation stays off (shared DB). Expect 25–45 min full runs. Per-phase: targeted filter locally, full suite in CI. |
| 8 | Flutter codegen churn (freezed/json_serializable) | 🟡 | Regenerate + `flutter analyze` per phase; mobile CI job from Phase 0 |
| 9 | Scope drift into V4 | 🟡 | V3 is frozen. Any implementation blocker → **STOP and report**, per your instruction. |
| 10 | Doc drift | 🟢 | Per-phase doc update is a completion gate, not a follow-up |

---

## 8. Verification protocol (every phase)

```
Backend   dotnet build backend/Kurx.sln -warnaserror     ← must be 0/0
          dotnet test  backend/Kurx.sln                  ← report before → after count
Web       npm run typecheck && npm run lint && npm run build && npm run test
Admin     npm run typecheck && npm run lint && npm run build
Flutter   flutter analyze && flutter test
Live      docker compose up + curl/browser the changed surface
```

A local `dotnet test` failure is triaged for SAC (`FileLoadException 0x800711C7`) before being treated as a code bug (B-5). **Any red gate stops the phase.** No phase is reported complete on "should work."

## 9. Documentation updated per phase

`docs/DECISIONS.md` (new D-NNN) · `docs/api/README.md` · `docs/architecture/overview.md` · `docs/PROJECT_HANDBOOK.md` · `CHANGELOG.md` · `.claude/memory/*.md` (owning convention file) · `docs/roadmap/README.md` · migration notes. Per CLAUDE.md §6 trigger matrix, run before each phase closes.

---

## 10. Honest scale estimate

**18 phases · ~22 migrations · ~450 files touched · ~450 new tests.** This is a multi-month program, not a sprint. Waves A–B (phases 0–6) are low-risk and deliver the core V3 value — type stops being inert, org context and audience rules land. **Wave C is where the real risk lives**, and its sizing depends entirely on your answer to B-4.

If the horizon is shorter than the program, V3 §21.3 supports stopping cleanly after **Phase 6**: Kind + Capabilities + OrgUnit + AudienceRules is a coherent, shippable subset that solves the college-market problem without touching the money path.

---

## 11. Current position

**Phase 0 and Phase 1 are COMPLETE and verified.** All six blockers are resolved (§2).

**Phase 1 — Kind registry (2026-07-22).** Delivered additively: `event_kinds` (20 Kinds) + `kind_aliases`
(145 legacy type names, data-driven) + `events.kind_slug` (backfilled) + `GET /v1/kinds`; `KindSlug` is
derived on create/clone/update. The legacy 3/13/145 taxonomy is untouched and still written, so the change
is backward-compatible and no client behaviour changes. New EF migration `AddKindRegistry`; 5 new tests.

**Scope clarification (recorded here, not as a new D-NNN — it introduces no architecture):** the V3 §21.1
"schema shapes in the first migration" urgency is relaxed by B-4 (no production data), so each shape lands
in its assigned phase per §6 (Money→3, Inventory→7, Registration→8, VAR→9, structural→11–12) rather than
being pre-landed in Phase 1.

**Phase 2 — Capability registry (2026-07-22).** Delivered additively: `capabilities` (~45, V3 §11/§19) +
`kind_capability_defaults` (the §19 Kind×Capability matrix) + `event_capabilities` (per-event set,
materialized on create/clone/update and backfilled). Resolution applies universal defaults, the §19 matrix,
mode-gating (§11.3), and the depends_on DAG (§11.4 — declared edges `stages→scoring`,
`certificates→attendance`). Read surfaces: `GET /v1/capabilities`, `/v1/kinds/{slug}/capabilities`,
`/v1/orgs/{orgId}/events/{eventId}/capabilities`. The pre-existing trust-capabilities (M7) and D-116
workspace-capabilities are a distinct concern and are untouched. New migration `AddCapabilityRegistry`;
6 new tests. No new D-NNN — implements V3 §11.

**Phase 3 — Money (2026-07-22).** Delivered additively per V3 §9.1: a `currency` column (ISO-4217, 3 chars,
default INR) on every money-bearing table (orders, order_items, ticket_types, refunds, transfers,
ledger_entries, withdrawals, organization_wallet, payout_schedules, event_analytics_daily,
organization_analytics) + `settlement_currency` on events and organizations; the `Money` value type
(`amount_minor` + currency); an event binds its settlement currency from its Org and exposes it at the edge
(`settlement_currency` on event detail); clones inherit it. Existing `*_paise` amounts (D-004) are untouched,
and multi-currency settlement on one event is out of scope. New migration `AddMoneyCurrency`; 4 new tests.
No new D-NNN — implements V3 §9.1.

**Phase 4 — OrgUnit tree (2026-07-22).** Delivered additively per V3 §4.1: the `org_units` table (id, org_id,
parent_id, kind, name, materialised `path`, state) with a partial unique index enforcing one root unit per
org; a nullable `events.org_unit_id` FK; every org's root unit materialised on first need (idempotent
`EnsureRootAsync`) + a one-time backfill of existing orgs/events; events auto-bind their owning unit on create
and inherit it on clone; and a permission chain-walk seam (`ResolveOrgRoleAsync`) that resolves a descendant
unit's effective org role by ancestry. Backend-only (no API/Web/Admin/Flutter, per §5); existing per-org authz
is unchanged because a one-node tree resolves to the same org-level grant. Unit-scoped grants + sub-unit
pickers + archive/merge/reparent are later phases. New migration `AddOrgUnitTree` (COMMIT-per-batch event
backfill; no `path` index until Phase 5's subtree query); `EnsureRootAsync` is an atomic `ON CONFLICT` upsert;
9 tests. No new D-NNN — implements V3 §4.1.

**Phase 5 — Audience rules & membership attributes (2026-07-22).** Delivered additively per V3 §4.4: a
per-event `audience_rules` table (predicate — unit_subtree_in, role_in, cohort_year_in, attribute_matches,
require_verified, external_orgs_allowed, guests, applies_to) + `memberships.attributes` (cohort_year, …) and
`memberships.source`. Eligibility is evaluated **server-side, DENY BY DEFAULT** when a rule exists and open
(unchanged) when none does — enforced at registration (`OrderService`, returns 403 `not_eligible` + logs the
denial) and re-checked at admission (`GateEntryService` flags an ineligible holder for the organiser, never
silently voids). New endpoints: audience CRUD, member-attributes PATCH, and a current-user eligibility check.
The organiser RBAC is unchanged — eligibility is a separate registration gate. Rules evaluate against org
memberships (staff today; affiliate memberships arrive with a later import); `unit_subtree_in` resolves at org
root granularity until unit-scoped memberships exist; team `applies_to` variants and guest pools are stored
but activate in Phases 10/7. New migration `AddAudienceRules`; 12 tests. No new D-NNN — implements V3 §4.4.

**Phase 6 — Participants (2026-07-22).** Delivered additively per V3 §5: a platform `participant_roles`
registry (7 hardcoded classes, ~30 slugs, with `counts_toward_capacity` / `inventory_segment` / `is_public` /
`default_permissions`) + `event_participants` (Person/OrgUnit subject today, Team stored-for-Phase-10; state /
scope / visibility). The §5.4 permission union adds participant grants as a **fourth, event-scoped source**
(an event ORGANISER participant can manage the event without an org membership), resolved live by
`IEventPermissionService` and consulted by the participant surface — the legacy per-service org authz is
unchanged. V2 `EventAssignment` is kept and backfilled from at startup (strangler parallel-run, no dual-write).
New endpoints: role registry, participant CRUD, respond, my-participations. **COI (§5.5) is deferred to
Phase 11** — its home is `ScoringPolicy.conflict_rules`, which needs Teams (Phase 10) + Stages (Phase 11)
subjects; the participant substrate it builds on is now in place. Team subjects, org-extensible custom slugs,
and capacity accounting (Phase 7) are stored-for-later affordances. New migration `AddParticipants`; 10 tests.
No new D-NNN — implements V3 §5.

**Phase 7 — Inventory pools (2026-07-23).** Delivered additively per V3 §8 as a **dual-write shadow** of the
scalar Quantity/Sold — the lowest-risk first step of Wave C. `inventory_pools` (scope/segment/channel/unit +
Total/Held/Allocated/Consumed + oversell/release/no-show/waitlist config); one General/InPerson/PersonSlot/Event
pool per TicketType, `Consumed` kept in lock-step with `Sold` at every sale (order create incl. paid hold, group
join), refund, and hold-expiry, committed atomically with the scalar change. `ticket_waitlist` re-pointed onto
pools (§8.5). A reconciliation job proves `consumed == count(active admissions)` per pool and alerts on drift
(§17.1). New endpoints: read the event's pools, set a pool's policy (authz reuses the Phase-6 `event:manage`
union — the first production consumer of `IEventPermissionService`). **The scalar was the oversell authority at
this phase** — the §17.1 conditional-decrement contract that made pools authoritative **landed in Phase 9 (done)**;
`Held`/`Allocated` became authoritative there too, while the release/no-show automation and the non-general
segments remain stored-for-later affordances (deferred, not yet implemented). New migration `AddInventoryPools`;
9 tests. No new D-NNN — implements V3 §8.

**Phase 8 — Registration layer (2026-07-23).** Delivered additively per V3 §7 as a **shadow** of the
then-authoritative Order/Ticket — the second Wave-C step (the Phase 9 cut-over below made the chain authoritative).
The registration → admission → credential chain +
`registration_policies` (the five axes: subject/gate/intake/allocation/payment/windows), one policy per ticket
type (the Pass analog) derived from current config and organiser-settable. Each order **projects** a
Registration (the act), an Admission per ticket (the right, linked to the Phase-7 pool), and one Credential per
person per event tree (the artifact) — projected **post-commit, never inline** (§17.1), so the shadow can never
break the money path; a reconciliation job proves the shadow matches the authority (registrations == orders,
active admissions == active tickets). New endpoints: read/set the policy, list registrations (authz reuses the
Phase-6 `event:manage` union). **Order/Ticket were authoritative at this phase** — the cut-over **landed in Phase 9
(done)**, and the projection is now produced in the money transaction (authoritative), not post-commit. Subject is
Person (a party booking is one Registration with N Admissions, §6.1); TEAM/ORG_UNIT and the gate/allocation values
needing later subsystems (LOTTERY §7.4, PREREQUISITE §7.3, DELEGATED §7.5, walk-in §7.6) are stored-but-not-
enforced; Passes/AdmissionRight/VAR (§9) **landed in Phase 9**. New migration `AddRegistrationLayer`; 12 tests. No new
D-NNN — implements V3 §7.

**Phase 9 — Passes, VAR, concurrency contract (§17.1 authority cut-over, Option A). APPROVED / COMPLETE (2026-07-23)
— engineering-reviewed, review-fixed, and approved.** The new model is authoritative: inventory pools own oversell
via a conditional decrement (never `Sold += 1`); registration/admission/credential are produced **in the money
transaction** (§6.1); a `Pass` + `AdmissionRight(SINGLE)` (§9.2) and an immutable `VAR` (§9.5) per order line are
written at purchase, the VAR being the sole refund basis. **Every availability read resolves from the authoritative
pool** (waitlist gate, public/org ticket-type availability, cap/sold/available, order pre-checks), never the legacy
`TicketType.Sold`. Order/Ticket/`TicketType.Sold` are retained as legacy mirrors (still written; removal is a later
phase). §17.1 in full: conditional decrement, reserve-first holds, ascending-pool lock order, **per-caller** client
idempotency (`Idempotency-Key`, scoped to the user or guest phone), duplicate-callback + full-refund idempotency
(atomic status claims), per-person credential advisory lock, outbox side effects; a late capture after hold expiry
consumes unconditionally to honour the paid ticket; the reconciliation job **self-heals** to
`Consumed == count(active admissions)`. Migrations `AddPassesVarConcurrency` + `ScopeOrderIdempotencyAndRefundIndex`
(per-caller idempotency indexes; `refunds.OrderId` non-unique, keeping V3 §9.6 partial refunds open). Option A defers
multi-scope Passes and the later-wave subsystems (§9 Subtree/Set/Query, window_policy, cross-event VAR, TEAM/ORG_UNIT,
delegated/walk-in, lottery/prerequisite, gate validation, no-show/release automation, analytics rebuild, legacy-mirror
removal). No new D-NNN — implements V3 §9/§17.1.

**Phase 10 — Teams (retires `Group`, ordering 10→8). APPROVED / COMPLETE (2026-07-24).**
The V3 §6 Team subsystem, added **additively** (Option A both forks): the purchase `Group` / `GroupMember` /
`RegistrationMode.Group` stay as **legacy compatibility mirrors** (party-booking flow + the approved Phase-9 money
path untouched), and this phase is **formation only** — team-slot inventory and the team-registration purchase flow
(§6.5) are deferred to the competitive-purchase phase. Delivers `Team` (identity + audited lifecycle FORMING→
COMPLETE→LOCKED→COMPETING→{ELIMINATED|DISQUALIFIED|WITHDRAWN|FINALIST}), `TeamMembership` (5 roles / 6 states;
substitution is an edge via `replaced_by_membership_id`, never a delete), `TeamInvite`, `TeamJoinRequest`, and
`TeamPolicy` (§6.3, on the existing `teams` capability); invites / join-requests / approvals; **merge/split** as
organiser-only guarded transactions blocked once a team is Competing (§6.4). Existing competition `Group`s are
back-filled into Teams at startup. New migration `AddTeams`; backend + API (client UI is the follow-on). No new
D-NNN — implements V3 §6.

**Phase 11 — Stages, Fixtures, Scoring (§10). APPROVED / COMPLETE (2026-07-24) — engineering-reviewed, review-fixed, and approved.**
The V3 §10 competition engine, added **additively** (Option A both forks) over the approved foundations — the money
path, InventoryPool authority, Registration→Admission→Credential flow, Pass, VAR and §17.1 concurrency are untouched.
Delivers `Stage` (§10.1 — **not registerable**: competitors arrive by advancement, spectators by admission; own
sequence/format/mode/venue/results-visibility + audited Draft→Live→Closed), `StageParticipant` (the roster),
`Fixture`/`FixtureParticipant`/`FixtureOfficial` (§10.2 — **manual scheduling with conflict detection**: an
overlapping slot that double-books a venue, official or participant is rejected; `Walkover`/`Abandoned` are
first-class so results are never falsified), `ScoringPolicy` (§10.3 — weighted sources, aggregation, normalisation,
ordered tie-breaks, public-vote bounds), `JudgeScore`, `PublicVote`, `StageResult`, `ResultCorrection`. The
**deterministic scoring engine** (`ScoreAggregator`) is a pure function of the stored scores/votes/policy: optional
per-judge z-score, aggregation (sum/weighted-mean/trimmed-mean/median/Borda rank), min-max blend with a **capped**
public-vote share, ranked with explicit tie-breaks then a deterministic subject-id fallback (**no tie is left
unresolved**). **Enforceable transactional fraud controls**: judge eligibility (Evaluation-class participant) +
conflict-of-interest (§5.5 — no scoring your own team/self), fixture-official assignment, one live score per
(stage, judge, subject) upsert, **one immutable vote per (stage, identity)** (unique index + race guard),
authenticated/verified-contact identity binding, per-hour vote rate limit, and a scoring/voting window (stage must
be Live). Results are append-only once **Published** — a `ResultCorrection` snapshots the prior value; certificates
and public reads see `Published`/`Corrected` only, gated by `results_visibility` (Live / OnStageClose / OnEventClose).
**Advancement** seeds the next stage's roster from the published set per rule (TopN / TopPercent / ScoreGte / Manual).
**Spectators (§10.4) are config-only this phase**: `Stage.spectator_pool_id` links an existing `InventoryPool` for
schema/validation/API; the spectator purchase flow (OBSERVER `AdmissionRight`, checkout, inventory consumption) is
deferred to the spectator-admission phase — **no money-path change**. Option A also defers **statistical anomaly
detection** (ML/behavioural/cross-event vote-fraud analytics — a later analytics concern; the audit trail it will
read is stored). Migrations `AddCompetition` (10 additive tables) + `RestrictScoreVoteFks` (H2 below), both fully
reversible. Attaches to the existing `stages`/`scoring`/`officials`/`submissions` capabilities; organiser gates reuse
the Phase-6 `event:manage` union. Backend + API only (competitive attendee/organiser client surfaces are the
follow-on, §12). No new D-NNN — implements V3 §10.

*Engineering review (Option B) → review-fix → approved.* Four required fixes were applied before approval:
**C1** — the public-vote weight cap no longer collapses a vote-only stage (the cap bounds influence *against judge
score*, so with no judge component votes are preserved and rank the stage); **C2** — V3 §6.4 enforced: a team
`Merge`/`Split` is now prohibited once any Stage scoring has begun for it (would orphan recorded scores), not only
once it is `Competing`; **H1** — result computation loads all aggregation inputs in a stable `OrderBy(Id)` so
recomputation is byte-identical regardless of DB row order; **H2** — `judge_scores → event_participants` and
`public_votes → users` foreign keys changed `Cascade → Restrict` (additive migration `RestrictScoreVoteFks`) so
scoring/vote evidence is never cascade-erased (participants are soft-removed; whole-stage deletion still cascades).
16 integration tests total (12 initial + 4 review-fix regressions), **702 pass / 1 skip / 0 fail** (up from 698).

**Phase 12 — Structure & series (§3, §13.2). APPROVED / COMPLETE (2026-07-24) — engineering-reviewed, review-fixed, and approved.**
The V3 §3 structural model + §13.2 EventSeries, added **additively** over the approved event model (money path,
InventoryPool authority, Registration→Admission→Credential, Pass, VAR, §17.1, Stage/Fixture/Team all untouched).
(1) **AgendaItem (§3.1 CONTAINMENT)** — the existing `EventSession` is reclassified and **extended** (no destructive
rename): it gains an optional `inventory_pool_id` so an AgendaItem **may hold a pool for a seat limit** (§3.4 rule 3;
still no Pass/Registration/Credential — pool link is config-only, seat-limit enforcement at scan is a later
attendance/gate concern). (2) **Composition depth ≤ 3 (§3.4 rule 1)** — enforced in the API on sub-event creation via
a `ParentEventId`-chain walk (`max_composition_depth`); a clone is a sibling of its source so depth is unchanged.
(3) **Structural discovery (§3.4 rule 2)** — a new `Event.ListedStandalone` (roots/editions default **true**, a
sub-event defaults **false**) filters the shared public discovery query; a sub-event surfaces inside its parent and
appears in a standalone feed only on organiser opt-in (event update). Direct access by slug/id is unaffected.
(4) **EventSeries (§13.2)** — new `event_series` (mode RECURRING | EDITIONS; RFC-5545 `rrule` + `exception_dates`
validated/stored; brand assets) + `event_series_followers` (carried across members); `Event` gains `SeriesId`
(an event belongs to **≤1 series**, §3.4 rule 5) + `EditionOrdinal`/`EditionLabel` (EDITIONS). **Per-occurrence
timezone reuses the existing `Event.Timezone`** (each RECURRING occurrence is its own event with its own inventory and
timezone). New service + API: series CRUD, attach/detach members, follow/unfollow; organiser gates reuse the org role
(Owner/Manager/Representative). New migration `AddStructureAndSeries` (2 tables + 5 additive columns + FKs
[`events → event_series` SetNull, `event_sessions → inventory_pools` Restrict]; fully reversible; **backfills existing
events to `ListedStandalone = true` to preserve current discoverability**). No new D-NNN — implements V3 §3/§13.2.
*Engineering review (Option B) → review-fix → approved.* One required fix applied before approval: **H1** — the public
series member list (`GET /v1/series/{id}/events`) no longer discloses non-public editions; `ListMembersAsync` now
reuses the org-role check (a manager of the series' org sees all members) and applies the Event subsystem's
`Published && Public` rule for everyone else, so Draft/Private/Unlisted editions never leak (no API-shape change).
7 integration tests total (6 initial + 1 review-fix regression), **709 pass / 1 skip / 0 fail** (up from 702).
**Deferred (later phases, not Phase 12):** push-to-children inheritance (§3.5, Phase 15), cancellation cascade +
material change (§3.5/§14.5, Phase 14), the outbox-fed search index and RECURRING one-listing collapse (§15, Phase 16),
series analytics rollup (§17), and RRULE auto-expansion into occurrence events.

**Phase 13 — Delegated & walk-in (§7.5, §7.6). APPROVED / COMPLETE (2026-07-24) — engineering-reviewed, review-fixed, and approved.**
The V3 §7.5 SeatBlock + §7.6 walk-in, added **additively** — the money path, InventoryPool authority,
Registration→Admission→Credential, Pass, VAR, §17.1, and the Stage/Team/Series subsystems are untouched. **The
existing `Order→Ticket` projection (`ProjectOrderInTransactionAsync`) is the ONLY authoritative minting path** — both
subsystems create an authoritative Order + Ticket(s) against a segment pool and reuse that projection (confirmed
Option A). **Walk-in (§7.6):** a staff <c>EventParticipant</c> (Operations class, e.g. `registration_desk`) or organiser
registers an attendee at the gate against the `WalkIn`-segment pool via `InventoryService.TryConsumeManyAsync` (§17.1),
producing Registration+Admission+Credential in one transaction (the admission's pool is corrected to the WalkIn pool it
consumed, keeping reconciliation exact). Offline-safe via a **staff-scoped idempotency key** reusing the Phase-9
per-caller idempotency — a replayed queued walk-in returns the original, never a duplicate. **SeatBlock (§7.5):** an
organiser reserves `Quantity` seats for an `registrant_org_unit_id`; the funding order mints **N unassigned admissions**
(PersonId null); the **delegate console** (`SeatBlockSeat`) binds people — assign/reassign/unassign/aggregate-status/
incomplete-list — **governed by `AssignmentDeadline` + `ReassignLimit` + a per-(re)assignment audit** (§7.5 rule 3).
Assignment sets the ticket holder + the admission's person and **re-runs the same projection** to bind the one-per-tree
Credential. **Delegated payment (§9.7) is data/authz only** (FREE | DEFERRED, `PayerId` + org context on the block);
**no live card collection / gateway / refund-to-org** this phase — the model is future-gateway-attachable. New migration
`AddDelegatedRegistration` (2 tables — `seat_blocks`, `seat_block_seats` — fully additive & reversible; **no change to
any Phase-8/9 entity**). No new D-NNN — implements V3 §7.5/§7.6.
*Engineering review (Option B) → review-fix → final verification → approved.* Four fixes applied before approval:
**H1** — a NONE-identity walk-in (both `UserId` and `GuestPhone` null) was not DB-deduped by the guest idempotency
index (Postgres NULLs distinct), so a concurrent replay could double-register + double-consume; closed with an
additive partial-unique index `ix_orders_walkin_idempotency` on `orders(EventId, IdempotencyKey)` for that case (the
existing `DbUpdateException` catch returns the winner, the loser's consume rolls back). **M1** — reassign/unassign now
revoke the previous assignee's tree credential when no non-void admission still references it (no orphaned Active
credential; history preserved; one-per-tree holds). **M3** — seat (re)assignment takes a per-seat `pg_advisory_xact_lock`
and re-reads under it, so the reassign-limit check + increment are atomic under concurrency. **M4** — the delegate block
list computes assigned-seat counts in one grouped query (no N+1). 7 integration tests total (4 initial + 3 review-fix
regressions), **716 pass / 1 skip / 0 fail** (up from 709). **Deferred (later phases, not Phase 13):** cross-event
prerequisites (§7.3), lottery (§7.4), SPONSORED & DEFERRED-collection payment beyond the data model (§9.7 gateway
settlement), and the client surfaces — the **Web delegate console** and **Flutter gate/offline mode** are the documented
follow-on.

**Phase 14 — Lifecycle, gates & approvals (§14). APPROVED / COMPLETE (2026-07-24) — engineering-reviewed, review-fixed, and final-verified.**
The V3 §14 lifecycle + validation gates + internal approval chains + material change, added **additively** (confirmed
Option A ×3) over the approved event model — the money path, InventoryPool authority, Registration→Admission→Credential,
Pass, VAR, §17.1, and the Stage/Team/Series/SeatBlock subsystems are untouched. **§14.1 lifecycle:** `EventStatus.Published`
stays the **authoritative registration-open state** (the 30 `Published`-gate sites — OrderService money path, discovery,
chat, reminders, gamification, analytics — are unchanged); Phase 14 adds `Scheduled`/`Live`/`Completed` states + the
`schedule`/`open_registration`/`go_live`/`complete` actions in `EventStatusWorkflow` (the existing `publish` direct path
is preserved for backward compatibility). **§14.2 five validation gates:** each transition enforces only what it needs —
Publish→SCHEDULED (description, venue-or-URL per mode, owner unit, **approval chain complete**), Open-registration
(≥1 Pass, ≥1 InventoryPool, currency), Go-live (staff assigned), Complete (results published if any Stage) — added to
`TransitionAsync` additively (the existing `publish` keeps its own readiness/org/paid checks and gains only the
no-op-without-chain approval gate). **§14.3 approval chains:** `ApprovalChain` on an OrgUnit, **inherited down the tree**
(resolved via the Phase-4 materialised path), `ApprovalStep` (approver role|user, condition
[always/if_paid/if_external/if_budget_gt/if_minors], SLA/escalation metadata), SEQUENTIAL|PARALLEL, per-event
`ApprovalRequest`/`ApprovalStepDecision`; approve/reject/**bypass** (bypass is org-Owner/admin-only and always audited);
order internal chain → platform review (§14.4, preserved) → published. **SLA/escalation are stored; the auto-escalation
timer is deferred.** **§14.5 material change:** a change to date/venue/mode after any Registration exists records the
before/after in the audit spine, notifies every registrant (existing notification infra), and opens a refund window
(`Event.RefundWindowEndsAt`, default 7 days or event start whichever sooner); refunds within it stay **registrant-initiated
via the existing RefundService** (no auto-refund, no money-path change). New migration `AddApprovalChainsAndMaterialChange`
(4 tables + 1 nullable column — fully additive & reversible; **no change to any approved entity**). No new D-NNN —
implements V3 §14.
*Engineering review (Option B) → review-fix → final verification (Option A) → approved.* Four required fixes + three
low-risk optionals were applied before approval (no schema change): **H1** — approval completeness is now re-evaluated on
every gate check, adding a `Pending` decision for any step whose condition (`if_paid`/`if_external`/`if_budget_gt`/
`if_minors`) becomes true after the request was materialised (closing the conditional-approval bypass — e.g. making a
free event paid re-opens the request); **H2** — a rejected request can be **resubmitted** into a fresh cycle
(`POST /events/{id}/approval/resubmit`, org Owner/Manager) with the prior decisions preserved in the append-only audit
spine; **M1** — completeness is derived from the current decision set (not just the stored flag), so a concurrent final
approval can never strand the request; **M2** — the concurrent request-insert race is caught and the winning request
returned (no 500); **N1** — deterministic SEQUENTIAL ordering on `(Sort, StepId)`; **N2** — a material change never
shortens an already-open refund window. 8 integration tests total (5 initial + 3 review-fix regressions), **724 pass /
1 skip / 0 fail** (up from 716). **Deferred as future work (outside approved Phase 14 scope, not defects):** the
SLA/escalation background-timer job; the remaining §14.2 gate sub-checks (M3 — audience-resolves / check-in-configured /
walk-in-policy / certificates-queued, which lack clear data representations today); outboxing the material-change
notifications (M5); additional approval concurrency stress tests; SCHEDULED public discovery (kept as a pre-Published
internal state per the additive model); and the client surfaces — the organiser **lifecycle/approval console** (Web) is
the documented follow-on.

**Phase 15 — Templates & workspace (§13.1/§20). APPROVED / COMPLETE (2026-07-24) — engineering-reviewed, review-fixed, and re-verified.**
The inert `EventTemplate` scaffold is activated **additively** into the authoritative Template system of §13.1 —
**backend only, no wizard/UI, no AI** (confirmed Option 1 ×3), over the approved event model (the money path,
InventoryPool authority, Registration→Admission→Credential, Pass, VAR, §17.1, lifecycle/gates/approvals, and the
Stage/Team/Series/SeatBlock subsystems are untouched). **Scoped + versioned:** a template is `Scope`
(PLATFORM|ORG|UNIT|PERSONAL, most-specific-wins) + a version family (versions share `RootTemplateId` = the v1 id
events reference; a Published version is immutable). **Declarative config only** ([D-132](../DECISIONS.md)) — `config_json`
carries a capability preset + defaults and **never** dates/slug/status/inventory/pools/financials; every write runs
`ValidateConfigAsync` against the **closed** capability registry (unknown/disabled slug, bad state, template-declared
`workspace_tab`, malformed JSON → **rejected, never repaired** — the exact pipeline a future AI generator reuses).
**Snapshot-at-creation:** creating an event with `templateId` (the family root, usable only if it has a Published
version) overlays that version's capability preset onto the just-materialised `event_capabilities` — **mode-gated
(§11.3, `CapabilityService` authoritative)** — applies declarative defaults (timezone), and records
`events.created_from_template_version`; no inventory/financial/registration rows are auto-created and a later template
edit never mutates the event. **§20 generated workspace:** `GET /v1/orgs/{orgId}/events/{eventId}/workspace` groups the
event's non-Off capabilities by `workspace_tab` (via `ICapabilityService.GetForEventAsync` — generated, never
hand-written per Kind) and attaches a **read-only** publish checklist that projects the Phase-14 §14.2 gates through the
same `TransitionGateAsync` (via a read-only approval evaluation — no writes). New migration `AddTemplateActivation`
(new `event_templates` columns + unique `(RootTemplateId, Version)` + scope index; `events.created_from_template_version`;
backfills pre-existing rows to `root=self, v1, Published`; fully additive & reversible). Recorded as **D-183** (implements
V3 §13.1/§20 under the D-132 declarative law).
*Engineering review (Option B) → review-fix → focused re-verification (Option A) → approved.* Five required fixes + the
missing tests were applied before approval (no schema change): **H1** — `NewVersionAsync` catches the
`(RootTemplateId, Version)` unique-index race and returns `draft_exists` (409, never a 500); **H2** — the workspace
checklist is a **pure read** (new `IApprovalService.IsCompleteAsync` shares the completeness rule via a read-only
`EvaluateCompletenessAsync`; `TransitionGateAsync(readOnly)`), so a `GET /workspace` never materialises approval rows;
**M1** — `ApplyToEventAsync` skips a capability unavailable in the event's mode (§11.3); **M2** — `CloneAsync` enforces a
view check (Platform=all · Personal=owner · Org/Unit=member) and hides otherwise (no disclosure); **M3** — create +
materialise + snapshot run in one transaction (no orphan on a failed apply); **M5** — added parallel-version-race,
parallel-publish, scope-precedence, snapshot-independence, deletion, and archival tests. **744 pass / 1 skip / 0 fail**
(up from 724 → 737 → 744). **Deferred (outside approved Phase 15 scope, not defects):** the 4-screen creation **wizard**
and all client workspace UI (Web/Admin/Flutter, §12 backlog); materialising template branding/audience/form-field/agenda/
stage declarations into their own subsystems beyond timezone (**M4 — awaiting architectural confirmation**); the
per-event `event_capabilities` override merge in `CapabilityService.GetForEventAsync` (a later-phase resolver step); and
AI-assisted template generation (the validation pipeline is AI-ready). The **B-2** `feat/template-engine` collision did
**not** materialise — V3 activated its own `EventTemplate` additively and that worktree remains unmerged.

Standing risks carried forward: **B-2** (`feat/template-engine` collides with Phases 1, 2, 15 — unmerged; Phase 15
shipped independently) and **B-5** (SAC triage on local test failures — confirmed intermittent: a full local run varied
208→31 failures on *identical* code, all in login-heavy classes that pass in isolation; **CI is the authority**).

---

## 12. Client Integration Backlog

**Delivery model — stated explicitly so the roadmap matches reality.** The V3 program is delivered
**backend-capability-first**: each phase's gate (§8) is backend build + full integration suite + docs, and the
Wave-C phases (**7 Inventory, 8 Registration, 9 Passes/VAR, 10 Teams**) each shipped **backend + API only**, by
design and as approved in their completion reports. **Client parity follows *within the existing roadmap phases*,
not in a new phase.** This section makes the deferred client work explicit and maps each backend capability to the
**existing** phase that already owns its client surface (per §5 "Change summary by surface"):

- **Organiser configuration/management surfaces** land in **Phase 15 — Templates & generated builder**, whose
  deliverable is the organiser *workspace generated from `workspace_tab`/capability declarations*. Inventory,
  registration policy, Pass, and Team are all **capabilities**, so their organiser tabs are generated there.
  **Phase 15 backend is APPROVED / COMPLETE (2026-07-24, D-183)** — the workspace **API** (`GET …/workspace`, capability
  tabs + read-only publish checklist) and the scoped/versioned Template CRUD ship server-side; the **client** rendering
  (the 4-screen wizard + the generated workspace UI in Web/Admin/Flutter) is the remaining deferred work this backlog
  tracks (still **Not started**).
- **Attendee read / discovery surfaces** land in **Phase 16 — Search & discovery** (the attendee's surface:
  eligibility-aware feeds, availability, ticket/pass/admission display).
- **Attendee competitive team-formation** lands in **Phase 11 — Stages** (the competitive attendee experience;
  Teams exist *only* where competition does, and §5 already assigns the competitive Flutter surfaces there).
- **Finance / VAR-attributed revenue read views** land in **Phase 17 — Analytics** (VAR-based revenue rebuild).

**No new roadmap phase is created and no phase is renumbered; no implementation status changes.** These are the
*existing* phases where §5 already scheduled the corresponding Web/Flutter/Admin work.

Status legend: **Done** · **Partial (legacy)** = an older, pre-V3 surface exists but does not yet consume the
authoritative Wave-C API · **Not started** · **N/A** = out of that client's scope per §5.

### Phase 7 — Inventory pools (backend ✅)
| Backend capability | Web | Flutter | Admin | Planned existing phase | Dependency |
|---|---|---|---|---|---|
| Inventory pool **config** (segments, oversell, no-show, release, waitlist) | Not started | N/A (organiser-only) | N/A (§5) | **Phase 15** (generated workspace) | Backend P7 ✅ |
| Availability **read** number (authoritative pool `available`) | Partial (legacy) — number rides the existing ticket-type display, which is pool-authoritative since P9; no dedicated pool view | Partial (legacy) | N/A | **Phase 16** (attendee) / **Phase 15** (organiser pool view) | Backend P7 + P9 ✅ |

### Phase 8 — Registration layer (backend ✅)
| Backend capability | Web | Flutter | Admin | Planned existing phase | Dependency |
|---|---|---|---|---|---|
| Registration **policies** (five axes) config | Not started | N/A (organiser-only) | N/A (§5) | **Phase 15** (generated workspace) | Backend P8 ✅ |
| Registration **lifecycle** (registrations list / state / admission chain) | Partial (legacy) — organiser sees legacy attendees/orders, not the authoritative registration view | Partial (legacy) — attendee sees legacy tickets, not registration/admission | N/A | **Phase 15** (organiser) / **Phase 16** (attendee) | Backend P8 ✅ |

### Phase 9 — Passes, VAR, §17.1 (backend ✅)
| Backend capability | Web | Flutter | Admin | Planned existing phase | Dependency |
|---|---|---|---|---|---|
| **Pass** product config | Not started | N/A (organiser-only) | N/A (§5) | **Phase 15** (generated workspace) | Backend P9 ✅ |
| **Pass / Admission** attendee display (my passes, credential, check-in resolution) | Partial (legacy) — legacy ticket/QR view | Partial (legacy) — legacy ticket/QR view | N/A | **Phase 16** (attendee) | Backend P9 ✅ |
| **VAR** read surfaces (allocated-per-event) | Not started | N/A | Not started | **Phase 17** (analytics) | Backend P9 ✅ |
| **Finance / read-only** views (VAR-attributed revenue) | Partial (legacy) — legacy wallet/revenue, not VAR-attributed | N/A | Partial (legacy) — legacy reports | **Phase 17** (analytics rebuild) | Backend P9 ✅ |

### Phase 10 — Teams (backend ✅, formation only)
| Backend capability | Web | Flutter | Admin | Planned existing phase | Dependency |
|---|---|---|---|---|---|
| **Team policy** config (§6.3) | Not started | N/A (organiser-only) | N/A (§5) | **Phase 15** (generated workspace, `teams` capability) | Backend P10 ✅ |
| **Team management** (organiser roster oversight, lifecycle lock/disqualify/withdraw, merge/split) | Not started | N/A (organiser is web) | N/A (§5) | **Phase 15** (generated workspace) | Backend P10 ✅ |
| **Team formation** (attendee create; captain flows) | Not started | Not started | N/A | **Phase 11** (competitive attendee) | Backend P10 ✅; *team registration/purchase (§6.5) is a separate deferred backend item* |
| **Invites** (send + accept by token) | Not started | Not started | N/A | **Phase 11** (competitive attendee) | Backend P10 ✅; **invite delivery** (email/SMS/push of the token link) still to wire |
| **Join requests** (request + captain/organiser decide) | Not started | Not started | N/A | **Phase 11** (competitive attendee) | Backend P10 ✅ |
| **Roster management** (view roster, substitution edge, leave/remove) | Not started | Not started | N/A | **Phase 11** (competitive attendee) | Backend P10 ✅ |

### Phase 11 — Stages, Fixtures, Scoring (backend ✅, spectators config-only)
| Backend capability | Web | Flutter | Admin | Planned existing phase | Dependency |
|---|---|---|---|---|---|
| **Stage / Fixture** config (schedule, roster, conflict detection) | Not started | N/A (organiser is web) | N/A (§5) | **Phase 15** (generated workspace, `stages` capability) | Backend P11 ✅ |
| **ScoringPolicy** config (sources, aggregation, tie-breaks, vote bounds) | Not started | N/A (organiser-only) | N/A (§5) | **Phase 15** (generated workspace, `scoring` capability) | Backend P11 ✅ |
| **Judge scoring** (score entry, rubric breakdown) | Not started | Not started | N/A | **Phase 11** (competitive — judge app surface) | Backend P11 ✅ |
| **Public voting** (cast a vote within the window) | Not started | Not started | N/A | **Phase 11** (competitive / spectator attendee) | Backend P11 ✅ |
| **Results / brackets / advancement** display | Not started | Not started | N/A | **Phase 11** (competitive attendee) / **Phase 16** (public results) | Backend P11 ✅ |
| **Spectator admission** (buy an OBSERVER seat via `spectator_pool_id`) | N/A | N/A | N/A | **spectator-admission phase** (deferred) | Backend P11 config-only — purchase flow deferred |

**Cross-cutting dependency:** the **team registration/purchase** flow (§6.5 — a team drawing one `team_slot` + N
`person_slots`) is a **deferred backend item** (the competitive-purchase phase), so the Phase-11 team client can
render **formation** on the existing order/ticket surface, but a team cannot *register/pay into* a competition until
that backend lands. **Invite delivery** (emailing/SMS/pushing the invite token) is an open backend/notification item
that should accompany the Phase-11 team client. The **spectator purchase flow** (OBSERVER `AdmissionRight`, checkout,
inventory consumption over `Stage.spectator_pool_id`) is likewise a deferred backend item — Phase 11 ships the
spectator pool as **configuration only**. None of this changes any phase number or the completed backend status of
Phases 7–11.
