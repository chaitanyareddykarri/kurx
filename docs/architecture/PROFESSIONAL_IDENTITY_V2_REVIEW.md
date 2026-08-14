# Professional Identity V2 — Definitive Architecture & Implementation Specification

> **Status: Phases 0–3A shipped. This document owns the Phase 0–2 record and the overall roadmap.**
> **The detailed design for everything still outstanding lives in
> [`PROFESSIONAL_IDENTITY_V2_PHASE3_5_DESIGN.md`](PROFESSIONAL_IDENTITY_V2_PHASE3_5_DESIGN.md)**,
> which supersedes §8–§14 of this document for the remaining phases — it cuts Passport and DNA as
> subsystems, merges the headline into the identity engine, and moves Skills behind a tag-curation
> prerequisite.
> Supersedes the two earlier drafts at this path (one source of truth per the doc rule).
> Extends the shipped system (`PROFESSIONAL_IDENTITY_SPEC.md`, D-201..D-213) in place per D-205 —
> never a parallel system. All open decisions are resolved in §23.

## Revision history within this document

| Rev | Change |
|---|---|
| 1 | Initial review — triage of 18 requested sections against real data |
| 2 | Projects excluded · no person-level ratings · testimonials constrained to verified pairs · named derivation engines · public-trust list · **Professional Journey** added as flagship |
| 3 (this) | **Professional Connections** promoted to first-class (§4) · **Identity Graph** (§5) · **Profile Adaptation** (§12) · **Derived Metrics catalogue** (§13) · Resume expanded (§14) · **Architecture Validation** (§21) · all decisions resolved (§23) |

---

## 1. Current implementation assessment

The shipped read layer is sound and needs no restructuring. `PublicProfileService` (647 lines)
implements `PROFESSIONAL_IDENTITY_SPEC.md` §1–§7 completely — verified line by line:

| Spec | Implementation | Status |
|---|---|---|
| §1 three event lanes, dedup by `EventId` | `:215, :239, :298` | ✅ every lane `GroupBy(EventId)` first |
| §2 org cards, `ClaimedRole` → `OrgRole` fallback | `:434, :459, :474` | ✅ |
| §3 certificates (public, non-revoked) | `:176` | ✅ |
| §4 achievements — two swappable source methods | `:335–366` | ✅ per D-203 |
| §5 allies — state machine, 404-not-403, ≥200 cap, `FirstSharedEventId` | `AllyService.cs:14, 32, 59, 165, 378–406` | ✅ 10 endpoints |
| §6 identity labels — derived, zero new queries | `:385–401` | ✅ |
| §7 timeline — 7 lanes + `IsFirstEvent` | `PublicProfileEndpoints.cs:28` | ✅ |

**Why it extends cleanly:** already written as per-source private methods, owns zero tables, caches
summaries on a 10-minute TTL.

**Open defects** (all still present):

| # | Defect | Location |
|---|---|---|
| A | No endpoint sets any of the 4 privacy flags; `ShowAttended` defaults `false`, so one of three event lanes is dark for 100% of users | `Users.cs:54-57`, `AuthEndpoints.cs:124` |
| B | `AllyConnection.Visibility` spec'd + stored + enforced, no write endpoint | `AllyService.cs:165`, `AllyEndpoints.cs` |
| C | **Silent data loss** — `/settings` prefills via `getPublicProfile().catch(() => null)`; on failure the form renders empty and save overwrites headline/bio/skills | `settings/page.tsx:11`, `profile-actions.ts:24`, `AuthEndpoints.cs:166` |
| D | Avatar/cover accepted by API, no upload endpoint, rendered by neither client | `MediaEndpoints.cs`, `u/[username]/page.tsx:57,60` |
| E | `links_json` accepted, returned, in both client schemas, rendered/editable nowhere | `api.ts:752`, `public_profile_dto.dart:109` |
| F | Flutter has no profile editor; `ProfileController.update({bio})` writes to Hive, zero callers | `features/profile/` |
| G | D-041 promised `EducationJson` → `membership_claims` "in M6 (D-044)"; D-044 is org verification (M5), M6 is D-045 which never mentions it | `DECISIONS.md:397` |
| **H** | **Doc contradicts code (found in this revision's audit):** `IAllyService.GetMutualDetailAsync`'s XML says *"not gated on being allies"*, but D-211 changed it to require an `Accepted` connection (`AllyService.cs:212–221`). Violates the "no doc contradicts the code" rule | `IAllyService.cs:47-50` |

---

## 2. Existing reusable backend capabilities

Shipped, tested, with an HTTP surface. **Primary source of truth for Phase 2 — no new model may
duplicate any of it.**

| Source | Entity / fields | Proves | On profile today |
|---|---|---|---|
| **Competition results** | `StageResult` (`Rank`, `FinalScore`, `State=Published`, `PublishedAt`), `ResultCorrection`, `JudgeScore` | Actual placement, with correction trail | ❌ inferred from `Certificate.Kind` only |
| **Event assignments** | `EventAssignment` (14 roles, `Status`, `AcceptedAt`, `CompletedAt`, `ShowOnProfile`) | Service history **with completion** | ⚠️ roles leak via `ParticipantService:190`; `CompletedAt` + 14-role vocabulary lost |
| **Speaker sessions** | `Speaker.UserId` → `EventSpeaker` / `EventSessionSpeaker` → `EventSession` (`Title`, `StartsAt`, `EndsAt`) | Speaking history with titles and durations | ❌ absent |
| **Event media** | `EventMedia` (`Kind ∈ Banner/Gallery/Document/Poster/Brochure/RulesPdf`) | Slides, documents, photos | ❌ |
| **Attendance** | `Ticket.State`, `Ticket.CheckedInAt`, `GateEntry.CreatedAt` | Show-rate, attendance timestamps | ❌ |
| **Completion** | `ParticipantState.Completed` | Follow-through | ❌ |
| **Teams** | `Team`, `TeamMembership`, `TeamRole ∈ {Captain, CoCaptain, Member, Substitute, Mentor}` | Team leadership, teammates | ❌ |
| **Connections** | `AllyConnection` + `AllyService` (433 lines, 10 endpoints) | Consented professional relationships | ⚠️ flat list only — see §4 |
| **Gamification** | `PointsLedger` (`Source`, `Reason`, `CreatedAt`), `UserBadge`, `Leaderboard` (`Scope ∈ global/city/college`) | Activity ledger, cohort rank | ⚠️ badges only |
| **Series** | `EventSeries`, `EventSeriesFollower` | Recurring-event continuity | ❌ |
| **Email proof** | `User.EmailVerifiedAt` | Verified recovery channel | ❌ (confirmed unused) |
| **Trust capabilities** | `ITrustService` → `OrgTrustCapabilities(CanRepresentOrg, IsOrgVerifiedRep, IsOrgVerified)` | Org-verified representation | ⚠️ partial |
| **Identity** | `UserIdentity` (ID0–ID4, masked last-4 only) | Government-ID / bank verification | ⚠️ one boolean |
| **PDF toolchain** | QuestPDF 2026.7.0 + ImageSharp (`Providers/DocumentProviders.cs`) | Resume needs **no new dependency** | n/a |
| **UI primitives** | `@kurx/ui`: `user-card`, `stat-card`, `sparkline`, `chip`, `badge`, `tabs`, `switch` · Flutter: `kurx_user_tile`, `kurx_avatar`, `kurx_card`, `kurx_chip`, `shimmer` | Most sections need no new primitive | partial |

**Consequence: Phases 0–4 require exactly one new database column.**

---

## 3. Missing profile capabilities

### 3.1 Derivable from §2 — no new domain
Professional Journey · Professional Connections (all 10 relationship types) · Identity Graph ·
Verified Skills · Headline · Experience Level · Profile Adaptation · Passport · Contribution heatmap ·
Competition results · Speaker sessions · Assignment history · Event media · Attendance & completion
metrics · Resume · the 8 public trust signals · geographic reach.

### 3.2 Requires a new domain — approval-gated, Phase 5 only
| Capability | Why not derivable | Gate |
|---|---|---|
| **Projects** | No `Project` entity. Competitions are **not** projects | Independent domain with its own models. Excluded from Phases 0–4 |
| **Testimonials** | `EventReview` rates an *event*, not a person | New table, hard-gated to four verified interaction pairs (§4.8) |
| **Contribution hours** | Nothing captures duration of service | `EventAssignment.Hours` + organizer capture flow |

### 3.3 Never public
Risk score · fraud score · reports · trusted-device details · security posture · moderation flags ·
KYC component breakdown. These live in `admin/` and the owner-only Security Center, both existing.

---

## 4. Professional Connections — first-class capability

### 4.1 Audit of the existing connection layer

**Person ↔ person (the only consented relationship):**
`AllyConnection` — canonical unordered pair (`UserLowId < UserHighId`, DB check constraint),
`Status ∈ {Pending, Accepted, Declined, Revoked}`, `Visibility ∈ {Public, Hidden}`, `ConnectedVia`
(`"request" | "mutual_request"`), `FirstSharedEventId`, `LastInteractionAt` (reserved, unwritten),
4 timestamps.

**Relationship-bearing tables already carrying professional context:**

| Table | Relationship | Carries |
|---|---|---|
| `EventParticipant` | person ↔ event | `RoleSlug` (29 slugs), `Visibility`, `State` |
| `EventAssignment` | person ↔ event | 14 roles, `Status`, `CompletedAt`, `ShowOnProfile` |
| `EventSessionSpeaker` → `Speaker.UserId` | person ↔ session | session title, times |
| `TeamMembership` | person ↔ team | `TeamRole`, `TeamMembershipState` |
| `Membership` | person ↔ org | `OrgRole`, `ClaimedRole`, `IsVerified`, `VerifiedAt`, `ShowOnProfile` |
| `StageResult` | person ↔ competition | `Rank`, `State` |
| `OrganizationFollower`, `EventSeriesFollower` | **unverified follow** | — **explicitly NOT professional** (D-064) |

**Service (`AllyService`, 433 lines) — reusable primitives already built:**

| Primitive | What it does | Reuse for |
|---|---|---|
| `UserEventIdsAsync(userId)` | a user's public event set (participation ∪ checked-in tickets) | every intersection below |
| `MembershipOrgIdsAsync(userId)` | a user's `ShowOnProfile` org set | shared organizations |
| `GetMutualDetailAsync` | shared events + shared orgs, **Accepted-gated (D-211)** | Mutual Context |
| `GetSuggestionsAsync` | set-based shared-event + shared-org counts **per candidate** | Collaboration Insights — already computes the graph |
| `GetStatusBatchAsync` | batch relationship status for a whole list | every person-list surface |
| `TryAttachFirstSharedEventAsync` | earliest shared event at Accept | Relationship Timeline origin |

**Endpoints (10):** request / accept / decline / revoke · incoming / outgoing / mine ·
status-batch · mutual · suggestions · plus the public `/allies` sub-resource.

**Privacy rules today:** `ShowAllies` (global) AND `AllyConnection.Visibility` (per-row) AND both
parties `ProfilePublic` (D-201's one genuinely non-obvious call). Mutual detail additionally
requires an `Accepted` connection (D-211).

**Workflows:** 4-state machine with reactivation and crossed-request auto-accept · 4 notification
kinds through `INotificationService` with generic `route` deep-links (D-207) · ≥200 pending-outgoing
abuse cap · pg_trgm-indexed people search (D-211).

### 4.2 The gap

Kurx has a **connection** (yes / no) but not a **professional relationship** (what kind, proved
how). Every one of the ten requested relationship types is the intersection of two users' role sets
over a shared verified context. **Zero new tables.**

### 4.3 Relationship type derivation

For a pair (A, B), intersect on each shared context and pair their roles. Directional — "A judged B"
is not symmetric — so an edge carries both roles and renders from the viewer's perspective.

| Derived relationship | Condition on a shared context |
|---|---|
| Worked together | shared event, both roles in any non-attendee class |
| Organized together | both roles ∈ organiser class (`owner`, `manager`) |
| Volunteered together | both `volunteer` |
| Competed together | both `competitor` |
| Judge ↔ Participant | A `judge` × B `competitor` (directional) |
| Speaker ↔ Organizer | A `speaker` / `EventSessionSpeaker` × B organiser class (directional) |
| Mentor ↔ Mentee | A `mentor` or `TeamRole.Mentor` × B `competitor` / team member (directional) |
| Shared teams | same `Team`, both `TeamMembership.State = Active`; `Captain` × `Member` renders as team lead |
| Shared organizations | same `Membership.OrgId`, both `ShowOnProfile` |
| Shared communities | shared org where `Organization.IsVerified` — the verified subset |

**Rule:** an edge with no resolvable evidence row is not emitted. A pair with a connection but no
shared context renders as a plain connection with no relationship label — honest, not blank-filled.

### 4.4 Mutual Context
Extends the shipped `MutualDetail` **additively** — same endpoint, same Accepted gate (D-211 must
not be loosened): `shared_events[]` and `shared_orgs[]` stay; add `shared_teams[]`,
`relationships[]` (§4.3), and per-context role pairs.

### 4.5 Relationship Timeline
Chronological list of shared contexts — first shared event (`FirstSharedEventId` is the stored
anchor), each subsequent shared event, each shared org overlap window, each shared team.

**Explicit decision: `LastInteractionAt` stays unwritten.** The timeline is fully derivable from
existing rows; giving the column a writer would create a second source of truth for the same fact
and re-open the question D-201 deliberately left closed ("no existing subsystem defines
interaction"). Derive, don't store.

### 4.6 Collaboration Insights
Counts over the same intersections `GetSuggestionsAsync` already computes: top collaborators by
shared-event count, most frequent co-organizer, organizations where the most collaborators overlap,
first-collaboration date, collaboration recency. All derived, all cached on the existing summary
TTL, all evidence-linked.

### 4.7 Connection provenance
A useful distinction the provenance model makes explicit:

- **Connection existence → `verified`.** A real `AllyConnection` row with mutual consent;
  `ConnectedVia` (`"request"` / `"mutual_request"`) is already stored provenance.
- **Relationship type → `derived`,** with `evidence` naming the shared event / org / team that
  produced it.

So the network shows *that* two people connected (verified) and *why they know each other*
(derived, evidence-linked) — never a claimed relationship.

### 4.8 Testimonials eligibility (Phase 5) reuses §4.3
The four approved pairs are exactly four §4.3 edges: Organizer ↔ Volunteer, Speaker ↔ Organizer,
Judge ↔ Participant, Team Lead ↔ Team Member. Eligibility is a query over existing tables; only the
testimonial rows are new.

### 4.9 Connection privacy through the central resolver
A new `network` section key with the four tiers, composed as **AND** with everything that exists —
never replacing it:

```
visible(connection) =
      Resolve(owner, "network", viewer)      // new section tier
  AND connection.Visibility == Public        // existing per-row flag
  AND both parties ProfilePublic             // existing D-201 rule
```
Mutual Context keeps its independent `Accepted` gate (D-211). Relationship types inherit the
visibility of the context that produced them — a relationship derived from a hidden event is
omitted, not summarised.

### 4.10 No parallel system
No new connection table, no new connection service, no second state machine. Professional
Connections is `AllyService` plus a derivation layer over intersections it already computes.
Defect H (the stale `GetMutualDetailAsync` doc) is fixed in Phase 0.

---

## 5. Identity Graph

The Identity Graph is the derivation DAG — the formal statement that **every derived value traces
to verified evidence**. It is a computation model, not a stored graph, and owns no tables.

```
VERIFIED FACT NODES (existing rows, the only roots)
  EventParticipant · EventAssignment · StageResult · EventSessionSpeaker · Ticket/GateEntry
  Certificate · Membership(+VerifiedAt) · TeamMembership · UserIdentity · UserBadge
  PointsLedger · EventTag · Event(City, Kind, StartsAt) · AllyConnection

        │  (each edge records which node ids produced the value)
        ▼
DERIVED OUTPUTS
  Professional Journey  ← participant ∪ assignment ∪ result ∪ session ∪ team ∪ membership
  Skills                ← EventTag ∩ certificates ∩ role slugs
  Leadership            ← organiser-class roles ∪ TeamRole.Captain ∪ OrgRole
  Experience            ← event count × years active × role seniority
  Trust                 ← UserIdentity ∪ Membership.IsVerified ∪ ITrustService ∪ EmailVerifiedAt
  Event DNA             ← Event.Kind / category distribution
  Achievements          ← certificates ∪ badges ∪ StageResult      (D-203 merge)
  Connections           ← AllyConnection × shared-context role pairs (§4)
  Resume                ← a projection over all of the above
```

**Invariants:**
1. A derived node with an empty `evidence` array is **not emitted**. This single rule is what keeps
   the graph honest.
2. No derived value is ever an input to another derived value **except** the Resume, which is a
   pure projection and adds no new inference.
3. Every edge is a pure function — the graph is reproducible from the fact nodes at any time, so
   no recomputation state is stored and no backfill is ever needed.
4. Privacy is applied at emission, not at derivation: a hidden fact node is excluded from the
   viewer's graph, so counts shrink rather than leaking through an aggregate.

---

## 6. API changes

**Governing rule: the root response is frozen.** `GET /v1/public/users/{username}` keeps every
existing key with identical semantics, permanently. Extension is by **new sub-resource** — the
pattern `/timeline`, `/events`, `/certificates`, `/allies` already established.

### 6.1 New public sub-resources (under `/v1/public/users/{username}`)
| Route | Phase | Source |
|---|---|---|
| `/trust` | 1 | the 8 positive signals (§8.4) |
| `/competitions` | 2 | `StageResult` where `State=Published` |
| `/sessions` | 2 | `EventSessionSpeaker` → `EventSession` |
| `/assignments` | 2 | `EventAssignment` where `ShowOnProfile` |
| `/media` | 2 | `EventMedia` via session→speaker attribution |
| `/network` | 2 | §4 — connections grouped by derived relationship type |
| `/journey` | 3 | §11 |
| `/skills` | 3 | derived — `EventTag` ∩ certificates ∩ role slugs |
| `/metrics` | 3 | derived — §13 |
| `/contributions` | 3 | derived — heatmap buckets |
| `/passport` | 3 | derived — role/venue badges |
| `/resume` | 4 | QuestPDF, `Accept: application/pdf` |

### 6.2 Extended existing endpoints (additive only)
| Route | Change |
|---|---|
| `GET /v1/me/allies/mutual/{otherUserId}` | + `shared_teams[]`, `relationships[]`, `timeline[]` (§4.4/4.5). Existing keys unchanged, Accepted gate unchanged |
| `GET /v1/me/allies` | + derived `relationship` label per row |
| `GET /v1/me/allies/suggestions` | + `relationship_hint` from the same intersection it already computes |

### 6.3 New authenticated write endpoints (Phase 0)
| Route | Fixes |
|---|---|
| `GET /v1/me` extended with `headline, bio, skills, links_json, education_json, avatar_key, cover_key` | Defect C — root cause of the silent wipe |
| `PATCH /v1/me/privacy` — the 4 flags (Phase 0), section visibility (Phase 1) | Defect A |
| `PATCH /v1/allies/{id}/visibility` | Defect B |
| `POST /v1/me/avatar/presign`, `POST /v1/me/cover/presign` | Defect D |

### 6.4 Backward compatibility
No route removed. No response key removed or retyped. `PATCH /v1/me/profile` keeps its shape and
gains only optional fields. Provenance ships additively (§9.3).

---

## 7. Database changes

**Total across Phases 0–4: one column.**

| Phase | Change |
|---|---|
| 0 | **none** — every defect fix is a write surface over existing columns |
| 1 | `users.section_visibility jsonb NOT NULL DEFAULT '{}'` |
| 2 | **none** — pure reads |
| 3 | **none** — pure derivation |
| 4 | **none** — QuestPDF already a dependency |
| 5 | `testimonials` table, `event_assignments.hours` — **approval-gated, out of scope here** |

The 4 privacy booleans are retained and dual-written for one release, then dropped. `LastInteractionAt`
stays unwritten (§4.5). No backfill anywhere.

---

## 8. Privacy architecture

### 8.1 Today
4 global booleans with **no write surface**, 5 per-row flags, and permission logic scattered across
`PublicProfileService` (6 sites) and `AllyService` (3 sites).

### 8.2 Target — one column, one resolver
```
users.section_visibility jsonb
  { "attended": "only_me", "certificates": "public", "network": "connections", ... }
  absent key = that section's documented default
```
One column, not a table: a small bounded map always read together, matching the existing
`LinksJson` / `EducationJson` / `ScoreBreakdownJson` convention. A table adds a join to every
profile read for no benefit.

```
ProfileVisibilityResolver.Resolve(owner, section, viewerId) -> bool
  Public            -> true
  Connections       -> an Accepted AllyConnection between owner and viewer
  EventParticipants -> owner and viewer share >= 1 public event
                       (reuses AllyService's UserEventIdsAsync — no new query shape)
  OnlyMe            -> viewerId == owner.Id
```

### 8.3 The resolver is the only security boundary
- Every section read passes through it; the 9 scattered checks are **migrated into it**, not duplicated.
- **Fails closed.** Section data is returned inside a `SectionView<T>` constructible only by the
  resolver, so "forgot to check" is a compile error, not a leak.
- Per-row flags compose as **AND**, never replaced (§4.9 shows the three-way composition).
- Exhaustiveness test asserts every section-enum member has a resolver entry (§18).

### 8.4 Public trust — positive signals only
| Signal | Source | Status |
|---|---|---|
| Identity Verified | `UserIdentity.Status == Approved` | ✅ shipped |
| Phone Verified | true by construction (OTP-only login) | ✅ |
| Email Verified | `User.EmailVerifiedAt` | ⚠️ exists, unused |
| Organization Verified | `Membership.IsVerified` | ✅ shipped |
| Speaker Verified | `Speaker.UserId` linked by an organizer (D-208) | ❌ new, real |
| Community Verified | `ITrustService.IsOrgVerifiedRep` — verified claim **and** verified org | ⚠️ computed, unexposed |
| Trusted Member Since | `User.CreatedAt` | ✅ shipped |
| Certificate Verified | `Certificate.VerifyCode`, non-revoked | ✅ shipped |

No negative signal is ever emitted — absence of a badge is indistinguishable from a privacy choice,
deliberately.

---

## 9. Provenance architecture

### 9.1 Shape
```jsonc
{
  "value": "Hackathon organizer · 12 events · IIIT Hyderabad",
  "source": "derived",                              // verified | self_declared | derived
  "evidence": [
    { "kind": "events.organized", "count": 12, "ref": "/u/asha/events?type=conducted" },
    { "kind": "membership.verified", "count": 1, "ref": "/o/iiith" }
  ]
}
```

### 9.2 Why structural, not a UI convention
D-041 made "the profile is display-only" structural for *authorization*; this does the same for
*presentation*. A client cannot render a self-declared claim as proof — the badge is driven by
`source`. One shared renderer per category per client. `evidence` is the "show me the proof"
affordance the product exists for. **A derived value with empty `evidence` is not rendered.** The
write endpoint rejects any field whose `source != self_declared` in one guard.

### 9.3 Backward compatibility
New sub-resources use `{value, source, evidence}` natively. The frozen root keeps flat keys and
gains a parallel `_meta` object, so today's Zod and freezed schemas keep parsing unchanged.

---

## 10. Derivation engine architecture

Each engine is a **pure function over already-fetched aggregates** — no engine opens a connection
or issues a query. Composed by one facade, `IProfileDerivationService`.

```
                     ┌── IdentityEngine       -> profile emphasis, identity labels (§12)
                     ├── SkillsEngine         -> skills[] with evidence
                     ├── AchievementEngine    -> shipped 2-source merge + competitions
  aggregate bundle ──┼── ExperienceEngine     -> experience level, years active
  (already loaded)   ├── JourneyEngine        -> Professional Journey (§11)
                     ├── ConnectionEngine     -> relationship types, mutual context (§4)
                     ├── EventDnaEngine       -> the shipped BuildDnaAsync, lifted
                     ├── IdentityEngine.BuildHeadline  (static; no interface — D-229)
                     └── ResumeEngine         -> QuestPDF composition (§14)
```

**No interfaces — corrected in D-229.** This plan named `IHeadlineGenerator` as the one interface
because it was believed to be
the only engine with a stated future second implementation. The rest are `internal sealed` classes
behind the one facade. Nine interfaces with one implementation each would violate this repo's own
coding standard (*"no interface with one implementation, no factory for one product"*) and add nine
DI registrations with no seam behind them. **Resolved as recommended — see §23.1.**

- **Headline:** `DeterministicHeadlineGenerator`, a priority ladder over verified counts extending
  the shipped `BuildSummary`. Fully explainable, every headline carries its `evidence`. **No LLM** —
  there is no AI provider anywhere in this repo (verified), adding one is a D-121 provider decision
  needing credentials that do not exist offline, and a hallucinated headline on a trust product is
  a liability. The interface exists so that decision can be made later without a rewrite.
- **Skills:** evidence-ranked, **no new taxonomy table** — `EventTag`/`Tag` ∩ certificates ∩ role
  slugs. No ratings, no endorsements, no self-assessment.
- **Achievements:** the shipped D-203 merge gains **one method**,
  `AchievementsFromCompetitionResultsAsync`. No DTO, consumer, or endpoint change.
- **Experience:** below the first threshold renders "Building", never a fabricated tier — the D-212
  precedent (the fictional level/tier concept was deleted, not quietly patched).

---

## 11. Professional Journey design

The flagship. **Zero new tables.**

### 11.1 Definition
A chronological **first-attainment ladder**: the first time a user provably reached each
professional tier, anchored to the row that proved it. Deliberately *not* a second timeline — the
Timeline answers "what have they done?", the Journey answers "how have they grown?". Shared source
data, no duplicated aggregation.

### 11.2 Tiers, each with a real backing source
| Tier | Source |
|---|---|
| Attendee | `Ticket.State = CheckedIn` |
| Participant | `EventParticipant.RoleSlug ∈ {attendee, competitor}` |
| Volunteer | slug `volunteer` / `EventAssignment.Role = Volunteer` |
| Team Lead | `TeamMembership.Role = Captain` |
| Competition Winner | `StageResult.Rank = 1`, `State = Published` |
| Speaker | slug `speaker` / `EventSessionSpeaker` → `Speaker.UserId` |
| Judge | slug `judge` / `EventAssignment.Role = Judge` |
| Mentor | slug `mentor` / `TeamRole.Mentor` |
| Organizer | slug `manager` |
| Host | slug `owner` (whose event it legally is, D-202) |
| Verified Member | `Membership.VerifiedAt` (D-201) |

### 11.3 Node shape
```jsonc
{
  "tier": "speaker",
  "first_attained_at": "2026-03-14T00:00:00Z",
  "occurrences": 7,
  "source": "verified",
  "evidence": { "kind": "event_session_speaker", "event_slug": "fossconf-26",
                "session_title": "Offline-first Flutter", "ref": "/e/fossconf-26" }
}
```

### 11.4 Design rules
- **Chronological by `first_attained_at`, never a canonical career ladder.** Someone can organize
  before ever volunteering. Tier weight affects visual emphasis only, never sequence.
- Every node links to evidence; a tier with no resolvable evidence row is omitted.
- **No suggested next step** — "2 events from Organizer" is gamification, which D-212 deleted.
- **Honest empty state:** one checked-in ticket is a one-node journey. Correct output, not a failure.
- Passes through the resolver; a node sourced from a hidden section is omitted for that viewer.

---

## 12. Profile adaptation

**No profile-type entity, no stored enum, no branching templates, no separate layouts.** One
adaptive ordering function.

```
SectionEmphasis(section) = f(verified volume, recency, tier reached)
render order = fixed header sections, then sections by emphasis desc, empty sections omitted
```

- **Fixed floor:** hero, trust signals, and Professional Journey always render first, in that
  order — identity before activity, for every user.
- **Everything else is ordered by evidence weight.** A student with 14 hackathon participations and
  one certificate leads with competitions; an organizer with 30 events leads with organized events
  and their network; a speaker leads with sessions.
- **The "Student / Organizer / Speaker / Judge / Mentor / Community Leader / Founder / Professional"
  labels are `IdentityEngine` output** (the shipped `BuildIdentityLabels`, extended) — used for
  *emphasis and display*, never as a stored type and never as a template selector.
- **Empty sections are omitted, not shown empty.** A user with no competitions has no competition
  section, rather than a zero-state card.

Why not profile types: a stored type is a second source of truth that drifts from the activity it
claims to describe, needs migration when someone's role changes, and forces a user into one identity
when real people are several at once. Ordering by evidence is self-maintaining — the profile adapts
the moment an event completes, with no write anywhere.

---

## 13. Derived metrics catalogue

Every metric below is computable from existing rows with **no schema change**.

| Metric | Source | Engine |
|---|---|---|
| Events organized / participated / attended | `EventParticipant`, `Ticket` | shipped |
| Attendance rate | `Ticket.State = CheckedIn` ÷ issued | Experience |
| Completion rate | `ParticipantState.Completed` ÷ accepted | Experience |
| Assignments completed | `EventAssignment.CompletedAt` | Experience |
| Competitions entered / won / podium | `StageResult.Rank`, `State=Published` | Achievement |
| Speaker sessions, total session minutes | `EventSession.StartsAt/EndsAt` via `EventSessionSpeaker` | Experience |
| Volunteer assignments | `EventAssignment.Role=Volunteer` | Experience |
| Certificates earned / achievement-kind | `Certificate.Kind` | shipped |
| Organizations, verified orgs, tenure | `Membership.CreatedAt`, `VerifiedAt`, `ValidUntil` | shipped |
| Teams, team-lead count | `TeamMembership.Role` | Journey |
| Years active, first/last activity | min/max across fact nodes | Experience |
| Event DNA (kind distribution) | `Event.Kind` / category | shipped |
| **Geographic reach** (cities, "active in X") | `Event.City` across the user's events | Identity |
| **Contribution heatmap** | timestamps: `Ticket.CheckedInAt`, `GateEntry.CreatedAt`, `Certificate.CreatedAt`, `EventAssignment.CompletedAt`, `StageResult.PublishedAt`, `PointsLedger.CreatedAt` | EventDna |
| **Activity ledger** | `PointsLedger` (`Source`, `Reason`, `CreatedAt`) | EventDna |
| **Cohort rank** (city / college) | `Leaderboard.Scope ∈ {global, city, college}` | Identity |
| **Series continuity** ("organized 4 editions") | `EventSeries` + member events | Experience |
| Collaborators, top collaborator, first collaboration | §4.6 intersections | Connection |
| Relationship type distribution | §4.3 | Connection |
| Trust signals (8) | §8.4 | Identity |

**Two hazards recorded:**

1. **Do not compute attendance from `Registration` / `Credential`.** These are the V3 admission
   layer running parallel to `Ticket` for the same real-world fact; using both double-counts.
   `Ticket` is authoritative for attendance today. Any future switch is a single-source migration,
   not an addition.
2. **Deliberately excluded as non-accomplishment data:** `SavedEvent` (intent, not achievement),
   `EventView` (analytics), `TicketTransfer`, `TicketWaitlist`, `OrganizationFollower` /
   `EventSeriesFollower` (unverified follows, D-064), chat activity (D-209 rejected chat integration
   explicitly). None of these belong on a verified professional identity.

---

## 14. Resume architecture

**The resume is a projection of the Identity Graph, not a template.** It introduces no model, no
DTO, and no aggregation of its own — `ResumeEngine` consumes exactly the same aggregate bundle every
other engine consumes, and renders it through QuestPDF (already a dependency, already used by the
certificate path in `Providers/DocumentProviders.cs`).

### 14.1 Composition — all from §13/§2 sources
```
Header       name · headline (derived, DeterministicHeadlineGenerator) · trust signals (8) · member since
Journey      the §11 first-attainment ladder — the resume's spine, and its differentiator
Experience   events by role: organized · spoke · judged · mentored · volunteered · competed
Competitions StageResult rank + event + date, published results only
Organizations Membership with role, tenure, verified marker
Speaking     EventSession titles, dates, durations
Certificates issuer, date, VerifyCode
Achievements the D-203 merge (certificates ∪ badges ∪ competition results)
Skills       derived, each with its evidence counts
Introduction the self-declared bio — boxed and labelled as self-declared
```

### 14.2 Rules
- **No duplicate models.** `ResumeEngine` takes `ProfileAggregateBundle` — the same input type the
  other engines take. If a fact isn't on the profile, it isn't on the resume.
- **Provenance survives into the PDF.** Verified and derived entries carry evidence counts; the
  self-declared block is visually distinct. A uniform-looking resume would defeat the entire point.
- **Verifiable.** Footer carries the public profile URL; each cited certificate carries its existing
  `VerifyCode`. No new verification entity, no signing infrastructure.
- **Privacy-aware.** Generated through the resolver — a section hidden from the viewer is absent
  from their copy. The owner's own download includes everything they can see.
- **Empty sections omitted**, exactly as in §12. A one-event resume is one page and honest.
- **Delivery:** `GET /v1/public/users/{username}/resume`, rendered on demand, cached on the existing
  summary TTL. Synchronous; if p95 exceeds ~2s under real data it moves behind the existing
  presign/storage pattern rather than growing a job queue.

---

## 15. UI architecture (shared)

One component contract, two implementations, mirroring the `UserCard` / `KurxUserTile` precedent
(D-209) that already works.

| Component | Purpose | Web base | Flutter base |
|---|---|---|---|
| `ProvenanceBadge` | verified / self-declared / derived | new, small | new, small |
| `EvidenceList` | expandable "show me the proof" | `chip` + `motion-panel` | `kurx_chip` + `ExpansionTile` |
| `TrustSignalRow` | the 8 positive signals | `badge` ✅ | `kurx_badge` ✅ |
| `JourneyRail` | the flagship journey | **new** | **new** |
| `RelationshipCard` | connection + derived relationship + mutual context | `user-card` ✅ | `kurx_user_tile` ✅ |
| `StatTile` | highlights | `stat-card` ✅ | `kurx_card` ✅ |
| `DnaChart` | Event DNA | `sparkline` ✅ | **new** (CustomPainter) |
| `Heatmap` | contributions | **new** | **new** |
| `PassportBadge` | role/venue badges | `badge` ✅ | `kurx_badge` ✅ |
| `SectionCard` | section frame + privacy chip | `card` ✅ | `kurx_card` ✅ |

Genuinely new primitives: **three**. Not a new design system — Kurx branding, large hero, cards,
graphs, built from the current token set.

## 16. Flutter architecture

- Extend `features/public_profile/`; new DTO per sub-resource, each requiring `build_runner`
  regeneration (freezed + json_serializable).
- **Build `features/profile/` editing, which does not exist** (defect F): edit page, privacy page,
  avatar picker. Delete the dead Hive-backed `ProfileController.update({bio})` — replace, don't layer.
- Riverpod provider per sub-resource so sections load independently; a slow section never blocks the
  hero. `AsyncValueView` + `shimmer` already exist for that pattern.
- **Wire-format discipline:** both camelCase-vs-snake_case bugs (D-208, D-210) came from hand-written
  DTOs drifting from hand-built JSON. Every new sub-resource gets a DTO-parsing test against a
  captured real payload, per `public_profile_dto_test.dart`.

## 17. Web architecture

- `/u/[username]` stays an RSC; each section is its own async component with independent `Suspense`.
- Extend `@kurx/ui` for the three new primitives — in the package, not page-local, so Flutter parity
  and any admin reuse work from one source.
- Fix defect C properly: `/settings` prefills from the extended `GET /v1/me`, never from
  `getPublicProfile`. Privacy UI uses the existing `switch` plus a tier selector.
- Render `avatar_key` / `cover_key` / `links` — all three already in the response and schema.

---

## 18. Testing strategy

Per the repo standard: **integration tests against real Postgres `kurx_test` via
`WebApplicationFactory<Program>`, never mocked persistence.** Cross-class parallelization stays
disabled. Test count reported before → after each phase. Run in the container runner.

| Area | Test |
|---|---|
| **Privacy resolver (critical)** | Exhaustiveness: every section-enum member has a resolver entry — fails on any new key. Matrix: each section × 4 tiers × (owner / ally / co-participant / stranger / anonymous) |
| **Connection privacy** | The three-way AND (§4.9): section tier × per-row `Visibility` × both-`ProfilePublic`. Mutual detail stays Accepted-gated (D-211 regression guard) |
| **Privacy migration** | Every existing flag combination round-trips to identical visibility |
| Defect C | Save with a failed prefill must not wipe `headline` / `bio` / `skills` |
| Provenance | Every field carries a `source`; no `derived` field ships empty `evidence` |
| Journey | Goldens: zero-activity (empty) · single ticket (one node) · multi-tier (chronological, not ladder order) · tier whose evidence is privacy-hidden (omitted) |
| Connections | Each of the 10 relationship types derived from a seeded shared context; a pair with no shared context yields a connection with no relationship label |
| Identity Graph | Every derived output's `evidence` refs resolve to real rows |
| Derivation engines | Golden fixtures per engine — pure functions, no DB beyond the bundle |
| Query budget | Per-request query ceiling asserted on the root and each sub-resource (R2) |
| Resume | Renders for zero-activity, single-event, and full-activity users; hidden sections absent for a stranger |
| Flutter | DTO-parsing test per new sub-resource against a captured real payload |
| Trust signals | Each of the 8 true only when its backing row exists; no negative signal ever emitted |

---

## 19. Migration strategy

1. **Additive only.** New nullable column, new table, or new route. Nothing dropped in the release
   that replaces it.
2. **Privacy dual-write.** The 4 booleans stay authoritative for one release while
   `section_visibility` is populated and read-verified; a follow-up migration drops them. Mapping
   preserves today's behaviour exactly: `ProfilePublic=false` → all sections `only_me`;
   `ShowAttended=false` (the default) → `attended: only_me`; `ShowCertificates=true` →
   `certificates: public`; `ShowAllies=true` → `network: public`.
3. **Root response frozen** (§6.4). Extension by sub-resource only.
4. **Provenance opt-in** — `_meta` is additive.
5. **No backfill anywhere.** Every Phase 2 source is queried live; the Identity Graph is
   reproducible from fact nodes by construction (§5).
6. **Per-phase rollback.** The resolver falls back to the boolean columns when `section_visibility`
   is empty, so Phase 1 is revertible without data loss.

---

## 20. Risks

| # | Risk | Severity | Mitigation |
|---|---|---|---|
| R1 | A section bypasses the resolver → **data leak** | **Critical** | `SectionView<T>` constructible only by the resolver (compile-time) + exhaustiveness test. Fails closed |
| R2 | Query explosion — 12 sub-resources on top of an already query-heavy profile | High | Sub-resources, not root fields; root stays as cheap as today. Per-request query ceiling in tests |
| R3 | Client contract breakage (strict Zod / freezed) | High | Root frozen; additive `_meta`; `build_runner` gated per phase |
| R4 | Wrong derived data is worse than absent on a trust product | High | Empty `evidence` → not rendered. "Building" over fabricated tiers (D-212). Golden tests per engine |
| R5 | Privacy migration silently flips a profile public | High | Exact boolean mapping, dual-write, round-trip test |
| R6 | **Relationship-type derivation is O(pairs × contexts)** and could be expensive for a heavy organizer | Medium | Derive only for the connections actually rendered (a page), reusing the set-based pattern `GetSuggestionsAsync` already proves; never per-pair queries. Cache on the summary TTL |
| R7 | Journey misread as a prescriptive career ladder | Medium | Chronological ordering only; no "next step"; documented in §11.4 and in UI copy |
| R8 | Scope drift into a social network | Medium | Every Phase 5 item gated on a verified interaction pair. No follows, no feed, no 1:1 chat (D-209) |
| R9 | Web/Flutter divergence | Medium | Per the cross-platform mandate, a phase is not done until both render it |
| R10 | Stale derived data after an event completes (10-min TTL) | Low | Already the accepted design (D-201 verified this for `ally_count`). Surface "updated hourly" where it matters |
| R11 | Resume latency under real data | Low | Synchronous first; behind the existing presign pattern if p95 > ~2s. No job queue |

---

## 21. Architecture validation

The six pre-implementation checks, run against this specification.

**✅ 1. Every profile section has a verified data source.** Every section in §6.1 traces to a table
in §2. The three that do not — Projects, Testimonials, Hours — are excluded from Phases 0–4 and
listed as approval-gated new domains (§3.2).

**✅ 2. Every derived value has evidence.** Enforced structurally, not by review: a derived value
with an empty `evidence` array is not emitted (§5 invariant 1, §9.2), and a test asserts it (§18).

**✅ 3. Every section uses the centralized visibility resolver.** `SectionView<T>` is constructible
only by the resolver, making a bypass a compile error; the exhaustiveness test fails when a new
section key appears without a resolver entry. The 9 existing scattered checks are migrated in, not
duplicated (§8.3).

**✅ 4. No duplicate models introduced.** Phases 0–4 add **one column and zero tables**. Professional
Connections extends `AllyService` rather than adding a second connection system (§4.10). The Resume
consumes the same `ProfileAggregateBundle` as every other engine (§14.2). Achievements gain one
method, not a new merge (§10).

**✅ 5. No existing backend capability overlooked.** This revision's sweep added five sources missed
in earlier drafts: `PointsLedger` (activity ledger), `Leaderboard.Scope=college` (cohort rank),
`EventSeries` (continuity), `TeamMembership` (leadership + teammates), `Ticket.CheckedInAt` /
`GateEntry.CreatedAt` (attendance timestamps). Sources deliberately excluded, with reasons, are
listed in §13. Two hazards recorded: `Registration`/`Credential` double-counting, and follower
tables not being professional relationships.

**⚠️ 6. Web, Flutter, Backend, API aligned — with two defects to close first.**
Defect H (stale `IAllyService` XML contradicting the D-211 gate) and defects C/E/F (client
divergence from the API contract) are all fixed in Phase 0, before any new surface is built on top.
Beyond that, alignment is enforced per phase by the cross-platform gate (§22) — a phase is not done
until backend, web, and Flutter all ship it.

---

## 22. Final phased roadmap

| Phase | Scope | DB | New tables | Ships |
|---|---|---|---|---|
| **0 · Defect closure** ✅ **SHIPPED 2026-08-01 (D-219, D-220)** | Privacy write APIs (A, B) · silent-wipe fix via extended `GET /v1/me` (C) · avatar/cover presign + render (D) · links (E) · **Flutter profile editor** (F) · education decision (G, D-220) · fix stale ally doc (H) | none | none | A profile that is correct and editable. No redesign |
| **1 · Foundations** ✅ **SHIPPED 2026-08-01 (D-221)** | Provenance envelope + `_meta` · `section_visibility` + `ProfileVisibilityResolver` + migration of the 9 scattered checks · the 8 trust signals · four-tier privacy UI on both clients | 1 column | none | The architectural spine. **Highest-risk phase** |
| **2 · Connect existing sources** ✅ **SHIPPED 2026-08-01 (D-222)** | Competition results · assignments (14 roles + `CompletedAt`) · speaker sessions · 3 new timeline lanes · 3rd achievement source. **Event media excluded** — it has no person-level owner (§4.2 of this doc; reasoning in D-222). **Professional Connections (§4) NOT started.** | none | none | The largest jump in verified content, at zero schema cost |
| **3 · Derivation engines** 🟡 **PARTIAL** | ✅ **Professional Journey** (D-223, the flagship) — engine, endpoint, web + Flutter rails. ⬜ Remaining: Skills · `IHeadlineGenerator` · Experience · Connection · metrics · contributions heatmap · passport · profile adaptation (§12) | none | none | Flagship shipped; the rest of the derived surfaces outstanding |
| **4 · UI redesign + Resume** 🟡 **PARTIAL** | ✅ Journey rail + competition/speaking sections on web, Journey rail on Flutter. ⬜ Remaining: `ResumeEngine` (§14), heatmap/DNA/passport primitives, the full visual redesign | none | none | Sections render; the redesign and resume are outstanding |
| **5 · New domains — approval-gated** | Testimonials (four verified pairs) · assignment hours · **Projects as an independent domain** | tbd | tbd | Only after 0–4 land |

Every phase ends with the cross-platform gate: backend + web + Flutter + contracts + docs + tests,
and a Cross-Platform Impact Report.

---

## 23. Decisions — resolved

**23.1 Derivation engines — RESOLVED as recommended.** Eight `internal sealed` engines behind one
`IProfileDerivationService` facade. **As actually built (D-229) there is no interface at all** and no
facade — every engine is a static pure function over the fact-set. Rationale in
§10. Override by saying so; nothing else in the plan changes if you do.

**23.2 Defect G, `EducationJson` — RESOLVED and recorded as D-220: retain as self-declared, supersede D-041's promise.**
D-041 committed to migrating it into `membership_claims` in M6; M6 (D-045) shipped without it, and
the reason is now clear — a `MembershipClaim` requires an existing org, a `ClaimedRole` enum value,
evidence documents, and reviewer approval. A free-text education string supplies none of these.
Auto-migrating would either fabricate unreviewed claims (breaking the "reviewed only from the claim
and its documents" invariant) or silently drop data. **Decision:** keep the column, label it
self-declared in the provenance envelope (`source: self_declared`, no `evidence`), and let users
upgrade it themselves by filing a real membership claim — which is exactly the evidence-backed path
D-045 built. Recorded as a `D-NNN` in Phase 0 superseding D-041's bullet.

**23.3 Phase 0 — APPROVED AND SHIPPED (2026-08-01).** Recorded as D-219 (write surface) and D-220
(education). Zero schema change, as designed. Verification: backend 899 passed / 0 failed / 1 skipped
/ 900 total against real Postgres (883 before, +17 regression tests); web `tsc --noEmit` clean and
`next build` green; Flutter `analyze` 0 issues and `test` 250 passed / 0 failed (248 before, +2).

Defects A–H are closed. Phase 1 is **not** started and remains gated on approval of this document's
foundations (§8 privacy engine, §9 provenance). The four boolean flags stay the source of truth until
Phase 1 migrates them into `section_visibility` with a dual-write release.
