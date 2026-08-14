# The Kurx Event Operating System — Design Philosophy & Principles

> **Status:** Companion document · **Version:** 1.0 · **Type:** Philosophy & rationale (non-normative)
>
> **Relationship to V3.** The single source of truth for the event system is
> [`EVENT_ARCHITECTURE_V3.md`](EVENT_ARCHITECTURE_V3.md) — the adopted, frozen implementation contract
> ([D-131](../DECISIONS.md)). **This document does not define, redefine, or override any mechanism.** It
> explains *why* the architecture is shaped the way it is, states the principles that keep it true over
> a decade, and uses V3's vocabulary throughout. **Where this document and V3 ever disagree, V3 wins**,
> and the disagreement is recorded in §7 for deliberate resolution via a new `D-NNN` — never by silently
> editing V3.
>
> **Who should read this, and when.** Read this *first*, before V3. This is the map of the territory and
> the reasons; V3 is the territory. A new engineer who reads this understands what Kurx is trying to be
> and why V3 makes the choices it makes; they then read V3 to learn exactly how.

---

## 0. How this document relates to V3

V3 answers **"what is the event system, precisely?"** — 22 sections of mechanics, verified against 16 blocking findings and 16 edge cases, scored 8.7 and approved for implementation.

This document answers **"why does an event system need to be shaped like that at all?"** — the product beliefs, the failure modes being avoided, and the small set of laws that, if ever broken, quietly turn the Event Operating System back into ordinary event-management software.

Everything below is either (a) philosophy that motivates V3, (b) reasoning that explains a V3 choice, or (c) a clearly-marked tension where this author's thinking diverged from V3 and the divergence is offered for decision, not enacted.

---

# PART I — WHY KURX EXISTS

## 1. What Kurx is, conceptually

Kurx is the **operating layer between a human's intention to gather people and everything that intention requires to become real** — from the first idea to long after the event ends.

Not "a ticketing platform." Not "an event website builder." Not "software for conferences." Those are applications with a fixed idea of an event baked in. Kurx is an *operating system for gatherings*: a small set of primitives and a runtime from which every kind of event is **composed**, so the platform can serve a hackathon, a wedding, a blood-donation camp, and a placement drive **without the engine knowing any of those words exist.**

V3 states the same belief as its governing tenet (§1): *a "type" earns its existence only by changing what the system does.* The 145 hardcoded event types in the old system were read by no filter, permission, price rule, or report — so they were fiction. Kurx replaces fiction with structure.

## 2. The problem Kurx solves

Every existing tool forces the organizer to translate their event into *the tool's* worldview, and that produces one of two deaths:

- **The catalog trap** — "support" 100 event types by building 100 flows. Every new type is a project; nothing is consistent; the tenth engineer cannot hold it in their head. It collapses under its own weight.
- **The lowest-common-denominator trap** — stay generic by supporting nothing well. A form builder that calls itself an event platform. It never *understands* the event, so it never helps.

Kurx's problem statement: **support every real-world event through one engine that adapts itself to the organizer, while the platform's internal complexity grows with its *capabilities* (which grow slowly and deliberately), never with the number of events it recognizes (which grows without limit).**

The organizer must feel the platform *understands* their event. The engine must not know their event exists. Holding both at once is the entire craft.

## 3. Event Operating System vs. Event Management software

| Event Management software | Event Operating System (Kurx) |
|---|---|
| Has features | Has **primitives** that compose into features |
| Knows event types | Knows **nothing** about event types (V3 §1 tenet) |
| Grows by adding flows | Grows by adding **capabilities** (V3 §11) |
| Its worldview; you adopt it | **Your** worldview; it adopts you (V3 §20 aliases) |
| Complexity ∝ number of event types | Complexity ∝ number of **capabilities** (~40, V3 §11) |
| An application | A **platform** built from primitives |

This table is the north star. Any proposed change that moves Kurx toward the left column is wrong on its face, regardless of how convenient it is — and V3's structure (Kind/Template/Capability, §2) is precisely the right column made concrete.

## 4. How humans think about events vs. how Kurx thinks

**Humans think in names.** Nobody thinks "a competitive gathering with team registration, a submission phase, and panel judging." They think **"hackathon."** The name is the unit of human thought: concrete, recognizable, emotionally loaded, specific to their world.

**Kurx thinks in structure.** Kurx must never branch on "hackathon." It thinks: people, forming teams, producing submissions, judged against criteria, ranked, awarded — a **Kind** with a capability preset, owned by an **OrgUnit**, gated by an **AudienceRule**. The name is a label on a configuration; the configuration is the truth.

The whole art of the product is the **translation between these two views** — and V3 builds the bridge deliberately: the organizer types "ideathon," V3's alias search (§20) resolves it to the *Hackathon* Kind, the preset reshapes the form, and the organizer only ever sees "teams," "submissions," "judges." The words *Kind*, *Capability*, *Template* are ours; **they must never leak into the organizer's experience.**

## 5. What an event is

V3 §1 gives the definition this document endorses without amendment:

> *An event is a bounded commitment of people to a time and a place (physical or virtual), organised by an accountable party, with a defined way in and a defined thing that happens.*

Everything else — tickets, teams, judging, RSVPs, certificates, sponsors — is *one of the operations*, present or absent depending on the purpose. An event is therefore not a "type"; it is a **selection of capabilities bound to a Kind, a time, an owning OrgUnit, and an audience.** Change the selection and you have a different event, without the engine ever needing a new kind of code.

---

# PART II — THE MENTAL MODEL (in V3's vocabulary)

This part is *reasoning*, not definition. Every mechanism named here is specified authoritatively in V3; the value added is the "why."

## 6. Kind, Template, Capability — the three layers, and why there are exactly three

V3 §2 defines three layers. The reason there are three — not one, not five — is that three different questions are being answered:

- **Kind** (closed, 20, platform-governed) answers *"what shape of event is this?"* It is a **capability preset** and the primary analytics dimension. It is closed on purpose: a closed vocabulary is analyzable, comparable across years, and impossible to pollute. Kind is the answer to the governing tenet — a Kind exists only because it changes the default capability set (V3's admission test, §2).
- **Template** (open, unlimited, scoped platform/org/unit/personal — V3 §13) answers *"what named, reusable starting point do I recognize?"* This is what a human calls their event. "Diwali Hackathon 2026," "Annual Placement Drive," "Sangeet Night" are Templates over Kinds. Templates are open because human vocabulary is infinite; they are snapshot-applied so changing a Template never mutates events already made from it.
- **Capability** (closed, ~40 — V3 §11) answers *"what can this event actually do?"* Capabilities are the implementation. They carry their config schema and declare what they provide (fields, workspace tab, endpoints, permissions, analytics).

> **The single most important reason the platform scales:** an *unbounded* number of Templates over a *closed* set of ~20 Kinds and ~40 Capabilities. Ten thousand Templates, forty Capabilities. That ratio **is** the architecture (V3 §11.1). Adding the ten-thousandth Template adds a data row — no screen, no code path, no engineer-hours.

*(Terminology note: an earlier draft of the Kurx philosophy called the recognizable named recipe a "Blueprint." That word maps to V3's **Template**; V3's vocabulary is canonical. See §7.1.)*

## 7-of-Part-II. Why capabilities are the implementation, and why they must stay independent

Capabilities are where 100% of behavior lives. A Kind is a preset (config). A Template is a recipe (config). Neither contains logic. The Judging capability contains the one implementation of scoring that every Kind reuses; the Kind merely *selects* it and sets defaults.

V3 §11.4 makes capability independence a **law**: `depends_on` is the *only* permitted coupling, and it must form a shallow DAG. The reasoning is arithmetic — forty independent capabilities are 2⁴⁰ untestable combinations; forty capabilities with declared edges are forty tests plus a handful of edges. An undeclared cross-capability branch is rejected at review. This is the discipline that keeps the closed capability set from rotting into the catalog trap by the back door.

## 8. Representation — why it modifies, and never forks

Kurx is **event-first** (D-074/D-075/D-101). This is not a detail; it is constitutional, and it is stated here so no future engineer reintroduces its opposite:

> **There are no organization login accounts. Users own accounts; users create events; during creation a user declares which OrgUnit they represent; Kurx verifies the right to represent. Organizations never authenticate — they are *represented by verified people*.**

V3 realizes this with the recursive **OrgUnit** tree and **Membership** (§4): universities and companies are the *same* tree with different `kind` labels, and a solo organizer is a one-node tree that costs nothing to render. Representation then acts as a **modifier** on the resolved event — it changes defaults, branding, audience defaults, approval chains (§14.3), and trust gates (§14.4) — **without ever forking the engine or the builder.** A college hackathon and a company hackathon are the *same* Kind and the *same* generated builder (§20), pre-seasoned by the owning OrgUnit.

The prohibition matters because the failure is seductive: the day someone decides "organizations should just log in," Kurx grows a second identity model, a second permission story, and a second everything — forever. Representation exists precisely so that never happens.

## 9. Trust and Verification — the fourth force that shapes an event without changing the engine

An event's effective configuration is resolved from more than Kind, owner, and access. **Trust and verification silently shape it too** — and V3 already threads this through:

- **Capabilities** — a paid event's commerce capabilities are gated on the organizer being paid-verified and the org verified (§14.4); an unverified org simply cannot reach the paid path.
- **Workflows** — verification state and OrgUnit approval chains (§14.3) decide whether publishing is one click or a four-step institutional review.
- **Moderation & compliance** — minors, `owner_unit.kind = school`, and consequential AudienceRules pull in guardian consent, restricted PII visibility, and shortened retention (§18).
- **Payouts** — settlement is gated on org verification and representation being non-vacant (§14.4, D-101).
- **Operational permissions** — AudienceRules that gate anything consequential require *verified* memberships and reject `SELF_DECLARED` (§4.3–§4.4); effective permission is evaluated live per request, never trusted from a token (§5.4, D-015).

The reason all of this changes behavior **without changing the engine**: trust is an *input to resolution*, exactly like Kind and access. It turns capabilities on or off, tightens gates, and adjusts defaults — it never adds an event-specific code path. Conceptually:

> **Kind × Owning-OrgUnit (Representation) × Visibility/Audience (Access) × Trust/Verification → the resolved event.** Change any input, re-resolve, and every surface — builder, workspace, gates, payouts — updates. Nothing downstream is bespoke.

## 10. Access — why "public/private/invite/internal" is not one dial

An earlier Kurx draft tried to collapse openness into a single "Access spectrum." **V3 is more correct, and this document defers to it.** V3 separates three genuinely different questions (§12.1, §4.4, §4.5):

- **`visibility`** (Public / Unlisted / Internal / Private) — *who can see the event exists.*
- **`AudienceRule`** — *who may register* (deny-by-default, server-side, logged).
- **`IdentityRequirement`** (None / Contact / Account / Verified) — *what proof of identity joining requires.*

"Friends / family / community / internal" are not peers and not Kinds; they resolve onto these axes plus vocabulary. The lesson for future engineers: **discoverability, join-ability, and identity are three separate authorization facts** — V3 keeps them separate, and any attempt to re-merge them into one flag will fail the first time an event is publicly discoverable but invite-only to join.

## 11. Composition — why "big" and "recurring" are relationships, not types

Most platforms drown here by inventing a "festival type" and a "recurring type." V3 refuses (§3): a Festival is a *containment* shell over independently-attendable Sub-Events; annual editions are *lineage* (`EventSeries`, EDITIONS mode); a weekly series is lineage (RECURRING mode). Crucially — and this matches the one self-correction this author made earlier — **multi-day is *not* composition:** a three-day conference is one event with a longer schedule, not three events (V3 §3, depth counts Sub-Events only). The payoff is that fests, series, traditions, and multi-track programs exist with **no new type and no new builder.**

## 12. The lifecycle — why status is a journey, not three words

`DRAFT → IN_REVIEW → SCHEDULED → REGISTRATION_OPEN → LIVE → COMPLETED → ARCHIVED` (V3 §14.1) is not bureaucracy; each transition exists because it gates something real, and V3's **five validation gates** (§14.2) enforce *only what each transition needs* — title to save a draft, a resolvable audience to open registration, staff and check-in to go live. The philosophy: **an event is never asked for everything up front, and never surprised at publish** — readiness is a continuously computed checklist derived from the enabled capabilities.

*(This author believes the journey does not end at ARCHIVED — see §6 of Part V, the one place this document argues for extending V3's emphasis.)*

## 13. Why the builder and workspace are generated, never written per Kind

V3 §20 reaches a valid Draft in four screens and generates the workspace from each capability's declared `workspace_tab`. The principle behind it: **there is one builder and one workspace; both are projections of the resolved event.** A Hackathon workspace shows Teams/Stages/Scoring; a Wedding workspace shows Functions/Guest Lists/Registry — *the same workspace* rendering different capabilities, in the organizer's vocabulary. The invariant that makes 100,000 Templates costless: **adding a Template adds zero screens and zero code.**

---

# PART III — THE CONSTITUTIONAL PRINCIPLES

These are the laws that keep V3 true for a decade. Each is grounded in a V3 section; this document only distills and names them so they are memorable and teachable. A change that violates any law is wrong by definition.

## The 20 Laws of the Kurx Event Operating System

1. **A type earns its existence only by changing what the system does.** The Kind vocabulary is closed at 20; a candidate that changes no capability, intake, allocation, operation, or artifact is an attribute, not a Kind. *(V3 §1–§2)*
2. **Capabilities contain implementation; Kinds and Templates contain only configuration.** Never invert this.
3. **The engine never knows an event type exists.** No code branches on "hackathon" or "wedding." Kind selects a preset; it never gates behavior (mode is the only hard availability constraint). *(V3 §11.3)*
4. **`depends_on` is the only permitted coupling between capabilities**, and it must form a shallow DAG. Any undeclared cross-capability branch is rejected at review. *(V3 §11.4)*
5. **A capability with dependent data LOCKS, never disables.** No toggle silently destroys data. *(V3 §11.2)*
6. **There is one generated builder and one generated workspace.** Both render from the resolved event; adding a Template or Kind adds no builder and no screen. *(V3 §20)*
7. **Representation modifies defaults; it never forks the engine.** One builder, seasoned by the owning OrgUnit — never many builders. *(V3 §4, §14.3)*
8. **Organizations never authenticate. Users own accounts and represent verified OrgUnits.** No organization login accounts, ever. *(D-074/075/101)*
9. **Events are composed, not duplicated.** Festivals are containment, editions are lineage, multi-day is a schedule — never new types. *(V3 §3)*
10. **The engine grows by adding capabilities, never by adding event types.** Complexity tracks the ~40 capabilities, not the unbounded Templates. *(V3 §11.1)*
11. **Templates are seeds, not cages.** Snapshot-applied at creation; afterward the event owns its own capability set and may diverge. *(V3 §13.1)*
12. **Every fact has exactly one home; if it can be derived, it is never stored.** Owner→Org, who-may-attend→AudienceRule, module behavior→capability config, subject→topics. *(V3 §12.2)*
13. **Any JSONB key used in a filter, sort, or aggregation becomes a typed column** — enforced by lint, not discipline. Config that is queried is not config. *(V3 §12.3)*
14. **Configuration stays declarative; it never becomes an embedded rule language.** Templates state values and selections; conditional *behavior* belongs only in capabilities, never in Template config — Templates never become a rule engine. *(Ratified as a V3 constitutional law: [`EVENT_ARCHITECTURE_V3.md`](EVENT_ARCHITECTURE_V3.md) CA-1, [D-132](../DECISIONS.md); grounded in §12.3.)*
15. **The user is never shown a capability that does not apply**, and the platform speaks the organizer's vocabulary via aliases. Relevance-gating and naming are mandatory, not polish. *(V3 §2 aliases, §20)*
16. **`visibility`, `AudienceRule`, and `IdentityRequirement` are three separate facts.** Discoverability, join-ability, and identity are never merged into one flag. *(V3 §4.4–§4.5, §12.1)*
17. **Money is always `(amount, currency)`; one event settles in one currency, immutable after first sale.** No bare integers anywhere in prices, refunds, ledger, or analytics. *(V3 §9.1)*
18. **Inventory is a pool with a hold→allocate→consume contract, never a scalar.** Reserve before payment; conditional decrement; total lock order; idempotency; a reconciliation job that detects drift before a customer does. *(V3 §8, §17.1)*
19. **Trust, permission, and compliance are resolved live from representation, verification, and context** — never trusted from a stored, bypassable flag. *(V3 §14.4, §18; D-015)*
20. **Results, refunds, ledgers, and audit are append-only.** Corrections are recorded, never silent; refunds reverse the ledger, never delete; one snapshotted Value Allocation Record serves both revenue and refunds so they cannot diverge. *(V3 §9.5, §10.5, §16, §18)*

**Corollaries (equally binding):**

- **C1.** Defaults are inferred and confirmable — the engine guesses so the human doesn't have to. *(V3 §20)*
- **C2.** The classification vocabulary that *drives behavior* is closed; the vocabulary that *aids discovery* (topics, tags, aliases) is open. Never confuse them. *(V3 §12.1)*
- **C3.** Hot paths do no synchronous work they can defer — no write on a public read, no capability joins in discovery, side effects through the outbox. *(V3 §15–§17)*

---

# PART IV — THE EXTENSION MODEL: how the Event OS evolves without touching the engine

This is the question every future engineer will ask: *"How do I add something without editing the core?"* V3 already answers it; this section explains the answer so it is used correctly.

**How a new Kind enters.** Rarely, and deliberately. A Kind is added only when it passes the admission test (§2) — it must change at least one of: registrant classes, required intake, admission allocation, in-event operation, or produced artifact. Adding a Kind is a platform decision with a `D-NNN`, because the vocabulary is closed and analyzable. Most "new event types" are **not** new Kinds — they are new Templates or new topics/aliases over an existing Kind.

**How a new Template enters.** Freely, by anyone with scope (platform / org / unit / personal — §13.1). A Template is data: a selection of capabilities and their config, form fields, a default AudienceRule, branding. It is validated against the capability registry — an unknown capability is rejected, not created (this is what makes AI-assisted Templates a generator rather than a hallucination engine, §13.1). Adding a Template requires **no engine change and no deploy.**

**How a new Capability enters — the only path that touches code.** A capability is added to the registry (§11.1) by declaring its contract:

- its **config schema** (validated; anything queried must be promoted to a column, §12.3);
- what it **provides** — form `fields`, a `workspace_tab`, `endpoints`, `permissions`, `analytics` dimensions;
- its **`depends_on`** edges (the only permitted coupling, a shallow DAG, §11.4);
- its **mode availability** (§11.3).

A capability contributes to every surface **through this single declaration**, and this is the key to extension-without-engine-change:

- **Builder** — the capability's declared `fields` appear as the relevant step reshapes (§20); no wizard is hand-edited.
- **Workspace** — the capability's `workspace_tab` renders automatically when the capability is on; the workspace is generated, never written per Kind (§20).
- **Lifecycle** — the capability declares which validation gate its minimum config satisfies (§14.2); it lights the stages it participates in.
- **Vocabulary** — user-facing terms come from Kind aliases and the capability's labels, so a new capability speaks the event's language without hardcoding.
- **Operations** — the capability's runtime actions surface in the workspace during LIVE.
- **Permissions** — the capability declares the permissions it needs; effective permission is the live union along the OrgUnit → Event ancestry plus participant grants (§5.4).

**How compatibility and versioning are maintained.** Kind presets and Templates are **versioned**, and applying them is a **snapshot** — changing a preset or Template never mutates events already created from it (§13.1, §22.5). Capabilities that gain dependent data LOCK rather than disable (§11.2), so evolving the registry never destroys live data. New surfaces are additive against a stable schema (V3 §22.4 lists the deferred capabilities that all slot in without reshaping a table).

**The one-sentence extension rule:** *A new Template is data; a new Capability is a declared contract added to the registry; a new Kind is a rare, decision-logged widening of a closed vocabulary — and none of the three requires editing the engine's core logic.*

---

# PART V — THE LONG GAME (where this author argues to extend, not redefine)

## Post-event community as a first-class direction

V3's lifecycle ends at `ARCHIVED`, and continuity is carried by `EventSeries` — followers, brand, canonical SEO across editions (§13.2). That is real and valuable. This author's product conviction is that Kurx's durable, ten-year advantage lives **past the event date**: the community that persists between editions, the alumni of a hackathon, the room that stays alive, the "next year" that is announced to last year's attendees. Most competitors treat "the event is over" as the end; an Event *Operating System* should treat it as the beginning of the community.

This is **not a contradiction of V3** and is **not enacted.** Per the ratified decision (§7.4), it is **deferred to a future capability**: when the platform reaches that stage, post-event engagement / community continuity is proposed as a new `D-NNN` ADR over the existing EventSeries follower graph and chat. V3 is **not** changed for it now.

---

# PART VI — HOSTILE REVIEW AT EXTREME SCALE

Assume 100,000 Templates, thousands of OrgUnits per institution, millions of events, ~40–hundreds of capabilities, and multiple engineering teams. Does the philosophy hold? V3 §17 and §22.5 already carry the *mechanical* scale review; this is the *philosophy-level* stress test.

- **100,000 Templates** — holds, *because* Templates are validated data over a closed capability registry (§13.1). The residual risk is **governance, not architecture**: an open Template catalog can accumulate near-duplicate, low-quality, or drifting entries. Control: scoping (platform/org/unit/personal), provenance display, and registry validation — but curation policy at six figures is a real, unbudgeted product surface. *Flagged, not solved.*
- **Millions of events** — holds on V3's terms: fact-at-leaf + aggregate-on-read (§16), partitioned fact tables (§17.3), read models for hot paths (§17.2), inventory as pools with a reconciliation job (§17.1). The philosophy adds nothing V3 lacks here.
- **Hundreds of capabilities** — this is the **most fragile point at scale.** V3's `depends_on` DAG (§11.4) keeps forty capabilities testable; at *hundreds*, the DAG's depth and the combinatorics of LOCKED states grow, and the "one test per capability plus declared edges" promise strains. The discipline holds only if Law 4 is enforced without exception. *This is where a lazy team will quietly cheat first.*
- **The config-vs-logic boundary (Law 14)** — the single most likely long-term failure across many teams. V3 §12.3 stops *EAV* (queried JSONB → columns), but it does not, by itself, stop a capability's config schema from slowly admitting conditionals until config is a bad embedded programming language. At multi-team scale this erodes by a thousand small, reasonable-looking pull requests. *Now ratified as an explicit V3 constitutional law (CA-1, [D-132](../DECISIONS.md)); the remaining work is enforcement, not design.*
- **Multiple engineering teams** — the architecture is well-suited (capabilities are independently ownable units with declared contracts), **but** the laws only bind if enforced at review across teams. The risk is organizational, not technical: Law 4 (no undeclared coupling) and Law 14 (config stays declarative) are exactly the ones a team under deadline will bypass, and a bypass in one capability is invisible to the others until it isn't. Control: the laws must be lint/review gates, not tribal knowledge.
- **The vocabulary/i18n tax** — making every organizer-facing term alias-driven and translatable (Law 15, V3 §20 + Phase 6 i18n) is a genuine, ongoing tax on every capability's velocity. Worth it, but adopt it clear-eyed.

**Verdict:** the philosophy holds at extreme scale on V3's mechanics. The three things that fail first are all *discipline*, not *design* — Law 4 (capability coupling), Law 14 (config-vs-logic), and Template-catalog governance. None require reshaping V3; all require enforcement and, for two of them, an explicit written rule.

---

# PART VII — CONTRADICTIONS & TENSIONS WITH V3 (for deliberate resolution, not silent change)

As instructed, where this author's philosophy diverged from V3, it is listed — **not enacted.** V3 remains authoritative until a `D-NNN` says otherwise.

### 7.1 Vocabulary: "Blueprint" (earlier Kurx draft) vs V3 "Template"/"Kind"
- **Divergence:** the earlier Kurx "constitution" used *Blueprint* for the recognizable named recipe and blurred it with the capability preset.
- **V3:** splits cleanly into **Kind** (closed preset) and **Template** (open recipe).
- **Recommendation:** **Adopt V3.** No amendment. This document uses Kind/Template throughout. (Resolution: closed.)

### 7.2 A separate "Module" concept vs V3's Capability contract
- **Divergence:** the earlier draft proposed a *Module* (implementation) distinct from a *Capability* (contract), to allow swapping implementations without touching Templates.
- **V3:** folds implementation into the Capability registry (`provides: …`, §11.1); there is no separate Module.
- **Recommendation:** **Defer to V3.** The swap-ability concern is real but is served by the capability's stable slug + versioned preset; a distinct "provider" concept is a *possible future clarification*, not a needed one. No amendment now. (Resolution: closed, revisit only if implementation-swapping becomes a live need.)

### 7.3 "Access as one spectrum" vs V3's visibility + AudienceRule + IdentityRequirement
- **Divergence:** the earlier draft collapsed openness into a single Access dial.
- **V3:** keeps three separate facts (§4.4–§4.5, §12.1) — which is **more correct**; the earlier draft's own self-critique had flagged the collapse as unresolved.
- **Recommendation:** **Adopt V3; the earlier simplification was wrong.** No amendment. (Resolution: closed in V3's favor.)

### 7.4 Post-event Community/Legacy as a first-class stage/capability
- **Divergence:** this author elevates post-event community continuity to a strategic, first-class concern; V3 ends the lifecycle at ARCHIVED and carries continuity via EventSeries followers (§13.2).
- **Nature:** an **extension**, not a contradiction — V3 does not forbid it.
- **Resolution — DECIDED (do not implement now).** Post-event Community/Legacy is deferred to a **future capability**. When the platform reaches that stage, it is proposed as a new `D-NNN` ADR over the existing EventSeries follower graph + chat. V3 is **not** amended for it now.

### 7.5 The two "blocking RFCs" (config-vs-logic, inter-capability contract)
- **Divergence:** the earlier draft declared these *unsolved prerequisites that must be ratified before implementation begins.*
- **V3:** already addresses both — inter-capability coupling via the `depends_on` independence rule (§11.4) and side effects via the outbox (§17.1); config integrity via anti-EAV promotion + schema validation (§12.3) — and is **approved for implementation.** So they are **not** blockers, and framing them as blockers contradicts D-131.
- **Resolution — ENACTED.** The config-vs-logic boundary is now an explicit V3 constitutional law (CA-1, [D-132](../DECISIONS.md)): Templates are declarative, never a rule engine; behavior lives only in capabilities. It is **not** a prerequisite to implementation — V3 remains approved for implementation. The inter-capability contract needs nothing further (V3 §11.4 `depends_on` DAG + §17.1 outbox).

### 7.6 Archetype count (~6) vs V3's 20 Kinds
- **Divergence:** the earlier draft reasoned about ~6 "archetypes"; V3 defines 20 Kinds (grouped into 7 families: Competitive/Learning/Community/Experience/Ceremonial/Purpose/Structural).
- **Recommendation:** **Adopt V3's 20 Kinds.** The ~6 archetypes survive only as an informal teaching lens ≈ V3's Kind groups. No amendment. (Resolution: closed.)

**Summary of resolutions:** 7.1, 7.2, 7.3, 7.6 → defer to V3 (closed). 7.4 → deferred to a future capability, proposed via a new `D-NNN` when the platform reaches that stage (V3 unchanged). 7.5 → ratified into V3 as constitutional law CA-1 ([D-132](../DECISIONS.md)). **This philosophy document changes nothing in V3 itself; the CA-1 law was enacted separately through the D-132 decision and V3's own amendment mechanism.**

---

## Closing

V3 is *how* the Kurx Event Operating System works. This document is *why* it must work that way: one engine for every gathering, growing by capabilities and never by types, resolving each event from Kind × Representation × Access × Trust, generating its own builder and workspace, speaking the organizer's language, and never — under any deadline — sliding back toward the catalog of a hundred bespoke flows. The twenty Laws are how we keep that true. Where this author sees further than V3, it is written as a question for a future decision (§7.4), never as a silent edit. V3 remains the single source of truth.
