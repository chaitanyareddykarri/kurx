# Professional Identity V2 — Phase 3–5 Definitive Design

> **Status: DESIGN — no code, no migrations, no APIs. Awaiting approval.**
> Companion to `PROFESSIONAL_IDENTITY_V2_REVIEW.md` (which owns the Phase 0–2 record) and
> `PROFESSIONAL_IDENTITY_SPEC.md` (which owns the shipped D-201 pillars). This document owns the
> design of everything still outstanding. Nothing already shipped is redesigned here.

---

## 1. Executive summary

The remaining work was framed as ten features. **It is not ten features.** Read against the code, it
resolves into:

| Framing | Reality | Verdict |
|---|---|---|
| Event DNA engine | `BuildDnaAsync` already computes it over the curated `EventKind` registry | **Already built.** Needs a chart, not an engine |
| Headline engine | `BuildIdentityLabels` already emits the exact tokens ("Verified Organizer", "Public Speaker") in priority order, capped at 6 | **A formatter**, not an engine. Merge |
| Passport | Achievements + Journey rendered as badges. No new data, no new rule | **Cut as a subsystem.** Keep as a view |
| Adaptive Profile | The visibility resolver already performs the only principled viewer adaptation; the rest is a sort | **A sort function**, not a subsystem |
| Metrics engine | Mostly existing counts; the real work is *removing* vanity metrics | **A subtraction pass** |
| Contributions heatmap | A bucketed scan of timestamps the profile already loads | **Small**, once the fact-set exists |
| Experience engine | A ranking over the same facts as everything else | **Small**, once the fact-set exists |
| **Skills engine** | Blocked: Kurx has **no curated skill vocabulary** (§4.1) | **Genuinely hard.** Needs a prerequisite |
| **Connections** | Allies + derived relationship types | **Genuinely new**, no new tables |
| **Resume** | Document composition + export | **Genuinely new**, no new tables |

**The single most important architectural decision in this document** is §6: every one of these reads
the same rows. Today each sub-resource re-queries independently, which is fine at 4 sections and
untenable at 10 — and impossible for the Resume, which needs all of them at once. The answer is one
`ProfileFactSet` loaded once per request, with every engine as a pure function over it.

**The single most important product decision** is §4.1: Skills as specified cannot ship honestly.
Role-derived skills duplicate identity labels, and topic-derived skills sit on an uncurated free-text
tag vocabulary that will produce `Flutter` / `flutter dev` / `FlutterDev` as three separate skills.
Skills must move behind a vocabulary prerequisite.

Net effect: **the outstanding scope is roughly half what the feature list implies**, and what remains
is better shaped.

---

## 2. Product vision (restated, to test every decision against)

Kurx answers one question: **"Can I trust this person based on what they have actually done?"**

Three tests every remaining feature must pass:

1. **Evidence test.** Does a real row prove it? If a value cannot name the row behind it, it does not
   render. (Already structural: D-221's provenance, D-223's `evidence` object.)
2. **Event-first test.** Does it derive from event activity? Kurx is not a CV host. Employment
   history, endorsements, and self-rated skills all fail this test.
3. **Subtraction test.** If this feature were deleted, would a user be unable to answer the trust
   question? If not, it is decoration. Vanity metrics and Passport fail this test.

---

## 3. Architecture review of what exists (do not redesign)

| Shipped | Where | Consequence for this design |
|---|---|---|
| Visibility resolver, 4 tiers, fails closed | `ProfileVisibilityResolver` | **Every new section gets a `ProfileSection` member and a gate.** No new privacy mechanism |
| Provenance `_meta` | `PublicProfileEndpoints` | Every new field declares verified / self_declared / derived |
| Trust signals (8, positive only) | `VerificationBadges` | Closed set. New signals need the same "an act by someone else proves it" bar |
| Journey (11 tiers, first-attainment) | `JourneyEngine` | The template for every future engine: pure, gated, evidence-bearing |
| Timeline (10 lanes) | `PublicProfileService` | Journey ≠ Timeline is settled (D-223). Do not blur |
| Allies (state machine, suggestions, mutual detail, batch status) | `AllyService` | **Connections extends this. No second connection system** |
| Achievements (3 sources, merge) | `GetAllAchievementsAsync` | Adding a source is one method (D-203) |
| Event DNA | `BuildDnaAsync` over `EventKind` | Already done |
| Identity labels (6, priority-ordered) | `BuildIdentityLabels` | **The headline is a rendering of this** |
| Derived summary | `BuildSummary` | Prose form of the same facts. Headline is the short form |

**Load-bearing constraint:** the public profile root response is frozen. Everything new is a
sub-resource or an additive `_meta` key.

---

## 4. Remaining feature definitions

### 4.1 Skills — the hardest problem, and it is a vocabulary problem

**What a skill is.** A *topic* a person has demonstrable involvement with. Not a capability rating,
not a self-assessment, not an endorsement.

**The finding that reshapes this.** What the brief calls "Skills" is two different things:

- **Capability** ("Organizer", "Public Speaking", "Judging") — derived from **role slugs**, a closed
  29-value registry. High confidence. **But this is exactly what `BuildIdentityLabels` already
  produces.** Building a Skills engine over roles creates a second system saying the same thing in
  different words.
- **Topic** ("Flutter", "AI", "Design") — derived from `EventTag` → `Tag`. This is the genuinely new
  information a profile does not yet carry.

**So: Skills = topics only.** Capability stays with identity labels. This removes the largest overlap
in the remaining scope.

**The blocker.** `Tag` rows are created ad-hoc by organizers at event-edit time
(`EventService.cs:1153`), matched only case-insensitively by name, with no curation, no synonym map,
and no admin surface. `Flutter`, `flutter dev`, and `Flutter Development` become three tags. A skills
list built on that is noisy, unrankable, and embarrassing on a trust product — and worse, it is
*organizer-authored*, so a person's skills would be decided by other people's tagging habits.

**Prerequisite (new, must precede Skills): tag vocabulary curation.** Options, in preference order:

1. **Curate `Tag` as a first-class platform vocabulary** — admin CRUD, merge/alias (the shape D-188
   already built for Audience/Category/Type), a `NormalizedName` column, and organizer tag entry
   becomes *select-or-request* rather than free-create. Reuses an existing admin pattern.
2. Separate `Skill` vocabulary + `Tag → Skill` mapping. Rejected: two topic vocabularies is exactly
   the duplication this document exists to prevent.
3. Ship Skills on raw tags. Rejected: fails the evidence test in spirit — the label would be
   arbitrary even when the underlying involvement is real.

**Recommendation: option 1, and it is its own deliverable with its own `D-NNN`.**

**Once unblocked — the model.**

- **Earning:** a skill is earned by *involvement in events carrying that tag*. Weighted by role:
  organizing or speaking at an event tagged `flutter` is stronger evidence than attending it.
- **Verification:** a skill is never "verified" in the KYC sense. It is **evidenced**. Do not use the
  word "verified" on a skill chip; that word is reserved for identity/membership/certificate facts.
- **Confidence** = f(evidence count, **source diversity**, role weight, recency). Diversity matters
  most: five events for one organizer is weaker than five events across five organizers. Rendered as
  a small band (Emerging / Established / Deep), **never a percentage** — a false precision.
- **Editable?** No. Derived values are never editable (D-221 provenance rule).
- **Pin / hide?** **Yes to both** — display preferences, not claims. Pinning does not change evidence
  or confidence; hiding removes it from the public list only. Stored in the existing per-section
  visibility jsonb pattern, not a new table.
- **Expiry?** **Decay, never deletion.** A skill with no recent evidence ranks lower and may fall
  below the display cut, but the history stays true. Hard expiry would delete a fact.
- **Org-verified skills?** A genuinely new capability (an org asserting "this person can do X").
  It is a new claim type with a review workflow — **Phase 5, its own decision**, not part of Skills v1.
- **Certificates create skills?** Only via their event's tags. A certificate's *kind* is a recognition,
  not a topic.
- **Grouping:** by `EventKind.GroupSlug` (the curated 7-group V3 registry), not by an invented skill
  taxonomy.
- **Search:** skills become a filter on the existing people-search (`/v1/public/users?q=`) once the
  vocabulary is curated — the pg_trgm indexes from D-211 already cover name/username; a skill filter
  needs a join, not a new search engine.

### 4.2 Professional Headline — a formatter, not an engine

`BuildIdentityLabels` already produces priority-ordered tokens; `BuildSummary` already produces the
prose form. The headline is the **short form of the same derivation**.

**Design:** `DeterministicHeadlineGenerator` composes **2–3 tokens** joined by ` • `, drawn from one
ordered pool:

1. Verified affiliation role ("Verified Organizer") — strongest, someone else vouched
2. Highest-priority participation identity ("Public Speaker", "Judge", "Mentor")
3. Top achievement class ("Hackathon Winner") — only from a published competition result or an
   achievement-kind certificate
4. Top topic skill ("Flutter") — **only once §4.1 lands**; until then the headline is 2 tokens

- **Tie-breaking:** evidence count desc → most recent evidence desc → alphabetical. Deterministic and
  stable, so a headline does not flicker between page loads.
- **Max length:** 60 characters total; a token that would overflow is dropped, never truncated
  mid-word.
- **Localization:** tokens come from label dictionaries already in the codebase; the *joiner* and
  ordering are locale-independent. Kurx is English-first today — the design keeps token text in one
  dictionary per label family so a future locale swap is a data change.
- **Manual override?** **No.** A self-written headline is exactly the self-declared claim this system
  exists to replace — and the `bio` field already exists for anything a user wants to say in their own
  words. This is the single clearest place to hold the line.
- **Pinned identities?** **Yes** — a user may pin *which of their earned* labels leads. Pinning
  reorders evidence-backed tokens; it cannot invent one. Same mechanism as skill pinning.
- **Extensibility (corrected in D-229):** the headline is a static function on `IdentityEngine`, not
  an interface. The seam was speculative and was never built; a future model-backed generator replaces
  that one method. Documenting an abstraction that does not exist is the defect this correction fixes.

**Merge decision:** the headline generator and `BuildIdentityLabels` become one engine
(`IdentityEngine`) with two outputs — `labels[]` and `headline`. Two consumers, one derivation.

### 4.3 Experience — a ranking, not a résumé section

**Definition:** *depth and breadth of verified event involvement over time.* Not employment history.

**Inputs (all existing):** distinct events by lane, role seniority (organiser class > evaluation >
content > operations > participant), distinct organizations, years active, completion signals
(`EventAssignment.CompletedAt`, `ParticipantState.Completed`), speaking sessions, published
competition results.

**Explicitly excluded:**
- **Employment.** No entity, no evidence, no event linkage. Adding it makes Kurx a CV host and fails
  the event-first test. **Do not build.**
- **Education.** Stays exactly as D-220 settled it: self-declared, marked, excluded from any derived
  score. It must never contribute to Experience, or a typed string would inflate a derived value.

**Output:** a band (Emerging / Active / Established / Distinguished) **plus the counts that produced
it**, always shown together. Below the first threshold it renders "Building" — the D-212 precedent
(the fictional tier concept was deleted, not quietly patched). **Never a numeric score**: a single
number invites comparison it cannot support.

### 4.4 Professional Connections — the largest genuinely-new piece

Extends `AllyService`. **No new tables, no second state machine.**

- **Relationship types (derived, directional):** worked together · organized together · volunteered
  together · competed together · judge↔participant · speaker↔organizer · mentor↔mentee · teammates ·
  team lead↔member · shared organization · shared community (verified orgs only). Derived by
  intersecting two users' role sets over a shared context. Directionality matters — "A judged B" is
  not symmetric.
- **Connection strength — deliberately not a score.** Emit the **reasons**, ranked, in plain language
  ("3 shared events, same organization"), which `GetSuggestionsAsync` already does. A numeric strength
  invites a leaderboard of friendships and is not defensible from the data.
- **Shared achievements:** a new intersection (same event, both hold achievement-kind recognition) —
  cheap, and the most interesting shared context Kurx can show.
- **Suggested allies:** already shipped (D-209, two signals). Extend with the relationship type as the
  reason, not with more signals.
- **Connection graph:** **not** a graph traversal feature. Second-degree suggestions need traversal
  Kurx does not need yet, and would change the product into a social network. Depth stays 1.
- **Privacy:** the three-way AND already established (§4.9 of the review) — section tier ×
  per-connection `Visibility` × both-`ProfilePublic`. Relationship types inherit the visibility of the
  context that produced them: a relationship derived from a hidden event is omitted, never summarised.
- **Performance:** the real risk. Derivation is O(connections × contexts). Mitigation: derive **only
  for the page of connections actually rendered**, reusing the set-based pattern `GetSuggestionsAsync`
  already proves (a handful of queries, never one per candidate). Cache on the profile TTL.
- **`LastInteractionAt` stays unwritten** (D-201 reserved it; the relationship timeline is derivable).

### 4.5 Metrics — a subtraction pass

**Keep** (real, evidence-backed, non-comparative): events organized / participated / attended ·
completion rate · assignments completed · competitions entered/won · speaker sessions · certificates ·
organizations + tenure · years active · geographic reach (from `Event.City`) · Event DNA distribution.

**Remove or never build:**

| Metric | Why |
|---|---|
| Profile views | Vanity, and surveillance-adjacent. Never build |
| Points / leaderboard rank on the profile | Engagement, not accomplishment. D-212 already deleted the fictional tier concept; keep gamification in its own surface |
| Ally count as a headline stat | Network size is not achievement. Keep the list, drop the number from the stat row |
| **No-show rate** | Real, but a **negative** signal — it violates D-221's positive-signals-only rule. **Owner-only**, as self-knowledge |
| Any percentile / rank-against-others | Turns identity into a ladder |

**Caching:** everything lands in the `ProfileFactSet` (§6) on the existing 10-minute TTL.

### 4.6 Contributions heatmap

- **Resolution:** daily buckets, 12 months rolling. Weekly is too coarse to show rhythm; hourly is
  noise.
- **Inputs:** timestamps the fact-set already loads — check-ins, certificate issuance, assignment
  completion, published results, participation start, membership verification. **Not** logins,
  **not** page views, **not** points.
- **Intensity:** count of distinct contribution events per day, banded into 4 levels + empty.
- **Colour:** Kurx accent ramp, not GitHub green. Must satisfy contrast in both themes, and must not
  be the only encoding — the cell tooltip carries the count and the day.
- **Privacy:** its own `ProfileSection` (`contributions`), through the resolver. Each contributing
  fact is *also* gated: a day whose only contribution came from a hidden section must not raise that
  day's intensity, or the heatmap becomes an oracle for hidden activity. **This is the subtlest
  privacy trap in the remaining work.**
- **Performance:** one aggregate over the fact-set, no per-day queries.
- **Mobile:** a 12-month grid does not fit a phone. Flutter renders a **26-week** window with
  horizontal scroll to the full year; web renders 52 weeks.

### 4.7 Passport — recommend cutting as a subsystem

**Why it was proposed:** a shareable visual summary of what someone has done.

**What it would contain:** hackathon/conference/speaker/organizer/volunteer/judge badges — every one
of which is already a Journey tier or an Achievement.

**Verdict: cut it as a concept.** It introduces a third name for data already called Achievements and
Journey, and a third mental model for users. Its *shareable* aspect is better served by the Resume
(§4.9) and the existing public profile URL. Its *visual badge* aspect is a rendering choice inside
Achievements.

**What survives:** a "badge wall" presentation option for the Achievements section, and QR/share
handled once by the Resume. **No Passport entity, no Passport endpoint, no Passport engine.**

### 4.8 Profile DNA — merge into Metrics

Already computed (`BuildDnaAsync`) over the curated `EventKind` registry with its 7 groups. It is one
metric family — a distribution — and needs a chart, not an engine.

**Verdict: DNA is a visualization of the Metrics distribution output.** Keep the name as a *label in
the UI* (it is good product language), delete it as an architectural concept. Future model-driven
analysis, if ever wanted, consumes the same distribution.

### 4.9 Resume — the second genuinely-new piece

**A projection of the fact-set, not a template engine.**

- **Sections (fixed order):** header (name, headline, trust signals, member since) → Professional
  Journey → Experience summary → organizations → competitions → speaking → assignments → certificates
  → achievements → skills (when §4.1 lands) → self-declared introduction, **visually distinct and
  labelled**.
- **Templates:** **one.** A template picker is a personalization feature with no trust value and
  multiplies the surface to maintain. Revisit only on real demand.
- **Custom sections:** **no.** A free-text section is a self-declared claim in a document whose whole
  value is that it is not self-declared.
- **Export:** PDF via QuestPDF (already a dependency, already used by the certificate path). A4 and
  Letter from the same composition.
- **Verification:** footer QR → the public profile URL; each cited certificate keeps its existing
  `VerifyCode`. **No new verification entity, no signing infrastructure.**
- **Privacy:** generated **through the resolver** — a section hidden from the viewer is absent from
  their copy. The owner's own download includes everything they can see.
- **Versioning:** **do not store generated resumes.** A stored copy is stale the moment the next event
  completes, and creates a "which version is real" problem on a trust document. Always regenerate;
  the document carries its generation date.
- **Branding:** Kurx-marked, because the verification claim is Kurx's. Not white-label.
- **Performance:** synchronous first (it is one fact-set + one render). If p95 exceeds ~2s under real
  data, move behind the existing presign/storage pattern — **not** a job queue.

### 4.10 Adaptive profile — mostly reject

**What genuinely adapts, and should:**
- **Entitlement** — already handled by the resolver. This is the only viewer-based adaptation with a
  principled basis.
- **Section order by evidence weight** — a pure sort, self-maintaining, no stored type (§12 of the
  review). Keep.
- **Empty sections omitted.** Keep.

**What should not adapt:** viewer *persona* (recruiter / student / organizer). Kurx cannot know a
viewer's intent without asking, guessing would be wrong often, and a profile that shows different
facts to different people undermines the one thing it is for. **Reject persona adaptation.**

**Verdict:** "Adaptive Profile" as a subsystem does not exist. It is one ordering function plus the
resolver that already ships.

---

## 5. Cross-feature overlap resolution (the merge table)

| Overlap | Resolution |
|---|---|
| Skills vs Experience | **Different axes, one fact-set.** Skills = *what topics* (breadth of subject). Experience = *how much and how senior* (depth of involvement). Both project from `ProfileFactSet`; neither queries independently |
| Headline vs Identity Labels | **Merged.** One `IdentityEngine`, two outputs (`labels[]`, `headline`). The headline is the short form |
| Journey vs Timeline | **Settled (D-223).** Journey = first attainment (growth). Timeline = every milestone (history). Do not blur |
| Passport vs Resume | **Passport cut.** Resume owns portable/shareable proof; Achievements owns badge display |
| DNA vs Metrics | **DNA merged into Metrics** as the distribution output. Name survives in UI only |
| Heatmap vs Journey | **Distinct and both justified.** Heatmap = density (rhythm); Journey = milestones (progression). They share timestamps, not logic |
| Connections vs Allies | **Connections = Allies + relationship derivation.** No second system |
| Skills vs Identity Labels | **Split by vocabulary.** Roles → labels (closed vocabulary). Topics → skills (curated tags). No role ever becomes a skill |

**Net: ten proposed subsystems collapse to four engines plus two views.**

---

## 6. Data flow — the `ProfileFactSet` (the core architectural change)

Today every sub-resource loads its own rows. That is correct at 4 sections; at 10 it means a profile
view can fan out past 30 queries, and the Resume — which needs all sections at once — would be the
worst offender.

**Design.** One loader materialises a person's verified facts once per request:

```
ProfileFactSet (loaded once, cached on the existing 10-min TTL)
├── events[]         eventId, kindSlug, groupSlug, city, startsAt, orgId, orgName, visibility
├── roles[]          eventId → roleSlug[]        (participation + assignment, deduped)
├── attendance[]     eventId, checkedInAt
├── sessions[]       eventId, sessionTitle, startsAt, endsAt
├── results[]        eventId, stageName, rank, publishedAt
├── certificates[]   eventId, kind, issuedAt, verifyCode
├── memberships[]    orgId, roles, isVerified, joinedAt, verifiedAt
├── teams[]          teamId, role, joinedAt
├── badges[]         name, earnedAt
└── tags[]           eventId → tagSlug[]         (only once §4.1 lands)
```

Every engine is then a **pure function** `ProfileFactSet × SectionAccess → output`:

```
IdentityEngine   → labels[], headline
ExperienceEngine → band, counts
SkillsEngine     → skills[] with evidence        (blocked on §4.1)
MetricsEngine    → counts, rates, DNA distribution
HeatmapEngine    → daily buckets
JourneyEngine    → nodes[]                       (SHIPPED — refactor to consume the fact-set)
ConnectionEngine → relationship types            (needs the *other* party's fact-set too)
ResumeEngine     → PDF                           (consumes all of the above)
```

**Properties this buys:** one query budget to reason about and assert in tests; engines testable as
pure functions with no DB; the Resume becomes trivial; and privacy is applied at **emission**, once,
so a hidden fact shrinks counts rather than leaking through an aggregate.

**Migration path:** `JourneyEngine` already has the right shape and is refactored to take the fact-set
instead of querying. That refactor is the first deliverable of Phase 3B and de-risks everything after.

---

## 7. Entity relationships

**New tables: zero**, for every feature in §4 except the two prerequisites below.

| Need | Storage | New table? |
|---|---|---|
| Skill pins / hides, headline pinned label | Extend the existing `users.SectionVisibilityJson` sibling pattern — one `users.ProfilePreferencesJson` jsonb | **No new table** (one column) |
| Curated tag vocabulary (§4.1) | `Tag` gains `NormalizedName` + `IsCurated` + an alias/merge table mirroring `OrganizationAlias` | **One small table** (aliases) |
| Org-verified skills (Phase 5) | New claim type + review workflow | **Yes — deferred, own decision** |
| Everything else | Projection over existing rows | **No** |

Existing relationships this design leans on (unchanged): `Event → EventTag → Tag`, `Event.KindSlug →
EventKind`, `EventParticipant`, `EventAssignment`, `Speaker → EventSessionSpeaker → EventSession`,
`StageResult → Stage → Event`, `Certificate`, `Membership → Organization`, `TeamMembership → Team`,
`AllyConnection`.

---

## 8. API requirements

Root response stays frozen. All additions are sub-resources under `/v1/public/users/{username}`:

| Route | Section gate | Notes |
|---|---|---|
| `/skills` | `skills` (new) | Blocked on §4.1 |
| `/metrics` | `metrics` (new) | Includes the DNA distribution |
| `/contributions` | `contributions` (new) | `?months=12`, daily buckets |
| `/experience` | `events` | Band + the counts that produced it |
| `/connections` | `network` | Allies + derived relationship types, paginated |
| `/resume` | resolver-wide | `Accept: application/pdf` |

Authenticated: `PATCH /v1/me/profile-preferences` (pins/hides — one endpoint, not one per feature).
`GET /v1/me/metrics` returns the owner-only additions (no-show rate).

`GET /v1/me/allies/mutual/{id}` gains `relationships[]` and `shared_achievements[]` — additive, the
D-211 accepted-connection gate unchanged.

**Every new sub-resource adds a `ProfileSection` enum member** and therefore a resolver entry — the
exhaustiveness test enforces it.

---

## 9. Database requirements

1. `users.ProfilePreferencesJson` (jsonb, nullable) — pins/hides. One column, mirrors the
   `SectionVisibilityJson` precedent.
2. **Tag curation (prerequisite for Skills):** `tags.NormalizedName`, `tags.IsCurated`, and a
   `tag_aliases` table mirroring `OrganizationAlias`. Plus an admin surface — the only admin work in
   this entire design.
3. Nothing else. No metrics table, no skills table, no heatmap table, no resume table. Every one of
   those would be a cache pretending to be a source of truth.

---

## 10–12. Client architecture

**Shared component contract** (web + Flutter, mirroring the `UserCard`/`KurxUserTile` precedent):

| Component | New? | Notes |
|---|---|---|
| `SkillChip` | new | Topic + evidence count + confidence band. Never a percentage |
| `EvidencePopover` | new | The "show me the proof" affordance, shared by skills/metrics/journey |
| `Heatmap` | new | 52w web / 26w Flutter + scroll |
| `DnaChart` | web reuses `sparkline`; Flutter `CustomPainter` | Distribution, not a timeline |
| `MetricTile` | reuse `stat-card` / `kurx_card` | — |
| `RelationshipCard` | reuse `user-card` / `kurx_user_tile` | + relationship label |
| `ExperienceBand` | reuse `badge` | Band + counts, always together |
| `JourneyRail` | **shipped** | — |

**Web:** each section stays an independent RSC with its own `Suspense`; a slow section never blocks
the hero. Sections render in evidence-weight order (§4.10) computed server-side.

**Flutter:** one Riverpod provider per sub-resource, `AsyncValueView` + `shimmer` for each, so a
forbidden or slow section renders nothing rather than an error. Every new DTO gets a parsing test
against a captured real payload — both wire-format bugs in this codebase's history (D-208, D-210)
came from hand-written DTOs drifting from hand-built JSON.

**Backend:** `ProfileFactSet` loader + pure engines + one facade (`IProfileDerivationService`).
No interface: every engine is a static pure function over the fact-set (corrected in D-229 — the
previously-documented `IHeadlineGenerator` was never implemented).

---

## 13. Performance

| Risk | Mitigation |
|---|---|
| Query fan-out across 10 sections | The fact-set: one load, all engines. Assert a per-request query ceiling in tests |
| Connection relationship derivation is O(connections × contexts) | Derive only for the rendered page, set-based (the `GetSuggestionsAsync` pattern). Never per-pair queries |
| Heatmap over 12 months | One bucketed aggregate over the fact-set; no per-day queries |
| Resume needs everything at once | Exactly what the fact-set is for. One load, one render |
| Cache stampede on a popular profile | Existing 10-min TTL; the fact-set makes the miss cost one load instead of ten |
| Skills over uncurated tags | Does not ship until §4.1 — a correctness gate that is also a performance one |

---

## 14. Privacy model

No new mechanism. Every new section is a `ProfileSection` with a resolver entry and a tier.

**Three traps specific to this phase:**

1. **Heatmap as an oracle.** A day whose only contribution came from a hidden section must not raise
   intensity. Gate each contributing fact, then bucket — never bucket then gate.
2. **Aggregates leaking hidden facts.** Metrics and Experience must be computed from the
   *viewer-visible* fact subset, so counts shrink for a restricted viewer rather than revealing what
   is hidden. Same rule the Journey already follows.
3. **Resume as a privacy bypass.** It must be generated through the resolver for the requesting
   viewer, not from the owner's full view.

**Skill pins/hides are display preferences, not access control** — a hidden skill is still evidenced
by public events, so hiding it is cosmetic and must not be described to users as privacy.

---

## 15. Security

- Derived values are **never writable**. The preferences endpoint accepts only pin/hide of values the
  user has actually earned — a pin naming an unearned label is rejected, not silently ignored.
- The Resume endpoint is a document generator taking a username: it must be **rate-limited** (PDF
  render is the most expensive public operation on the platform) and must not accept a template or
  section list from the caller (no injection surface into the document).
- Tag curation gives admins a vocabulary that appears on user profiles — an audit-logged surface,
  same as the D-188 taxonomy admin.
- No new PII. No new secret. No change to the auth surface.

---

## 16. Future extensibility

- **New fact source** → add a loader to the fact-set; every engine sees it. This is the payoff of §6.
- **New achievement source** → one method (D-203, proven twice).
- **New journey tier** → one entry + a gate.
- **Model-backed headline** → replace `IdentityEngine.BuildHeadline`; introduce an interface only
  when a second implementation actually exists.
- **Org-verified skills** → a new claim type reusing the D-045 review workflow.
- **Second-degree connections** → deliberately *not* designed for. If ever wanted, it is a product
  decision about becoming a network, not a technical extension.

---

## 17. Implementation dependencies

```
ProfileFactSet ──┬──> IdentityEngine (labels + headline)
                 ├──> ExperienceEngine
                 ├──> MetricsEngine (incl. DNA distribution)
                 ├──> HeatmapEngine
                 ├──> JourneyEngine (refactor to consume it)
                 └──> ResumeEngine ── depends on ALL of the above

ConnectionEngine ── depends on AllyService (shipped) + two fact-sets

Tag curation ────> SkillsEngine ────> headline token #4, /skills, skill search
ProfilePreferences column ──> pins/hides (skills + headline)
```

**Critical path:** `ProfileFactSet` first. Everything except Connections depends on it, and the
Journey refactor proves it before anything new is built on top.
**Longest lead item:** tag curation — it needs an admin surface, so it is the only item requiring
work in a fourth codebase.

---

## 18. Recommended phase order

| Phase | Contents | Why here |
|---|---|---|
| **3B** | `ProfileFactSet` + refactor `JourneyEngine` onto it. No user-visible change | De-risks everything after; proves the shape against shipped code |
| **3C** | `IdentityEngine` (labels + **headline**), `ExperienceEngine`, `MetricsEngine` (+DNA distribution) | Pure functions over 3B. Highest value per unit of risk |
| **3D** | `ConnectionEngine` — relationship types, shared achievements, mutual context | Independent of 3B/3C; the largest new user-facing capability |
| **4A** | `ResumeEngine` + export | Needs 3C complete; nothing needs it |
| **4B** | Heatmap + DNA chart + section-order adaptation + the visual pass | UI-weighted; safe last |
| **5A** | **Tag curation** (admin + normalization + aliases) | Prerequisite, own decision, fourth codebase |
| **5B** | `SkillsEngine` + headline token #4 + skill search | Blocked on 5A |
| **5C** | Approval-gated new domains: testimonials, assignment hours, Projects, org-verified skills | Unchanged from the review |

**Note the reordering:** Skills was Phase 3 in the original roadmap. It moves to Phase 5 behind tag
curation. That is the single biggest sequencing change in this document, and the reason is §4.1.

---

## 19. Risks

| # | Risk | Severity | Mitigation |
|---|---|---|---|
| R1 | Skills ship on uncurated tags to hit a date | **High** | The vocabulary prerequisite is a gate, not a preference. A noisy skill list damages the trust claim more than a missing section |
| R2 | Heatmap leaks hidden activity through intensity | **High** | Gate-then-bucket (§14.1); a dedicated test seeding a hidden-only day |
| R3 | Fact-set becomes a god object | Medium | It is a **data record with no behaviour**; all logic lives in pure engines. Enforce in review |
| R4 | Connection derivation degrades a heavy organizer's profile | Medium | Page-scoped, set-based, cached; assert a query ceiling |
| R5 | Resume PDF becomes a DoS surface | Medium | Rate-limit; no caller-controlled composition |
| R6 | Passport/DNA cuts get re-litigated later as "missing features" | Medium | The reasoning is recorded here and in the `D-NNN`s; the UI keeps both *names* where they are good product language |
| R7 | Derived bands read as scores | Medium | Bands always render with their underlying counts; never a bare number or percentile |
| R8 | Client divergence as sections multiply | Medium | Shared component contract; a phase is not done until web and Flutter both render it |

---

## 20. Final Professional Identity V2 roadmap

| Phase | Status |
|---|---|
| 0 · Write surface | ✅ Shipped (D-219, D-220) |
| 1 · Resolver, tiers, trust, provenance | ✅ Shipped (D-221) |
| 2 · Dormant verified sources | ✅ Shipped (D-222) |
| 3A · Professional Journey | ✅ Shipped (D-223) |
| **3B · ProfileFactSet + Journey refactor** | ✅ **Shipped 2026-08-01 (D-224)** — 8 fact families, scoped memoised loader, `JourneyEngine` now a static pure function. Zero schema/API/client change; 933 tests green |
| **3C · Identity (labels + headline) · Experience · Metrics + DNA** | ✅ **Shipped 2026-08-01 (D-225)**, rendered on both clients 2026-08-02 (D-227) |
| **3D · Connections** | ✅ **Shipped 2026-08-01 (D-226)** — `ConnectionEngine`, 16 directional relationship types on `GET /v1/me/allies/mutual/{id}`, rendered on both clients (D-227). The public `/connections` sub-resource is **not** built |
| **4A · Resume** | ✅ **Shipped 2026-08-02 (D-228)** — `ResumeEngine` over QuestPDF, composed through the resolver for the requesting viewer, rate-limited, never stored |
| **4B · client rendering** | ✅ **Shipped 2026-08-02 (D-227)** — provenance badge, metrics panel, experience band, Event DNA bars, relationships in Shared history, new trust badges |
| **4B · heatmap** | ✅ **Shipped 2026-08-02 (D-228)** — `ContributionsEngine` (gate-then-bucket), `/contributions`, 52-week web grid + 26-week Flutter grid |
| **4B · adaptive section ordering + visual pass** | ⬜ Not started — evidence-weighted ordering (§4.10) and the broader visual redesign |
| **5A · Tag curation (prerequisite)** | ⬜ Own decision |
| **5B · Skills** | ⬜ Blocked on 5A |
| 5C · Testimonials · hours · Projects · org-verified skills | ⬜ Approval-gated |

**Cut for good, with reasons recorded:** Passport as a subsystem · DNA as a separate engine · manual
headline override · custom resume sections · resume templates · stored resume versions · employment
history · profile-view counts · points on the professional profile · public no-show rate · numeric
connection strength · persona-based adaptive profiles · second-degree connection graph.

---

## Decisions — all approved 2026-08-01

1. **§4.1 — Skills moves behind tag curation (Phase 5A/5B).** ✅ Approved.
2. **§4.7 / §4.8 — Passport and DNA cut as subsystems**, retained as UI language. ✅ Approved.
3. **§4.2 — no manual headline override.** ✅ Approved. Pinning an *earned* label remains allowed.
4. **§4.3 — employment history permanently out of scope.** ✅ Approved.
5. **§4.5 — metric removals confirmed**, including points and ally-count as headline stats, and
   no-show rate becoming owner-only. ✅ Approved.
6. **§18 — phase order approved**, 3B first. ✅ Approved and **shipped** (D-224).

**Next: 3C** — `IdentityEngine` (labels + headline), `ExperienceEngine`, `MetricsEngine` (including
the DNA distribution), all as pure functions over the fact-set D-224 established.
