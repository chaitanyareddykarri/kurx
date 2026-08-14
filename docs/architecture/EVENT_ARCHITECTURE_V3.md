# Kurx Event Architecture V3 — Authoritative Design

> **ADOPTED — this is the implementation contract ([D-131](../DECISIONS.md)).**
>
> **Status:** Frozen. V3 supersedes V2 in full; V2 and its adversarial review are historical
> (deleted 2026-08-08; in git history). This document is the single source of truth for
> the event system and **must not be redesigned**. There is no V4. An implementation blocker is reported
> and resolved as a new `D-NNN`, never by amending this design.
> **Amendment rule:** §0–§22 below are preserved as approved on 2026-07-20 and are **never edited inline**.
> Post-adoption amendments are ratified as dated `D-NNN` entries in [`../DECISIONS.md`](../DECISIONS.md) and
> surfaced in the **Constitutional Amendments** section appended after §22; the banner and closing provenance
> note aside, §0–§22 are unchanged.
>
> **Delivery:** sequenced by [`V3_IMPLEMENTATION_ROADMAP.md`](V3_IMPLEMENTATION_ROADMAP.md) — 18 phases,
> 5 waves. §21.3 below is the original coarse phasing; the roadmap's 18-phase breakdown is the
> operative plan and takes precedence on sequencing.
> **Provenance:** authored 2026-07-20 against `feat/dev-workspace-auth`; relocated into the repository
> from a session scratchpad on 2026-07-20 so that no design artifact lives outside version control.

**Scope:** Design only — the document itself specifies no code. Implementation is tracked by the roadmap.
**Baseline:** As-built analysis of `feat/dev-workspace-auth`, 2026-07-20.

---

## 0. What V3 changes and why

V2 was reviewed adversarially and scored 6.5/10 with eight blocking findings. V3 resolves all eight, plus the eight GA-blockers and the four undefined edge cases. It also **removes six contradictions** V2 carried.

| Blocking item | Resolution | Section |
|---|---|---|
| **B1** Team subsystem; polymorphic registration subject | Full Team model; `Registration.subject ∈ {Person, Team, OrgUnit}`; purchase `Group` **deleted** as a concept | §6, §7 |
| **B2** Stage/Fixture/Scoring as a fourth structure | Structural model rebuilt: **3 relation classes, 5 relations** | §3, §10 |
| **B3** InventoryPool replacing scalar capacity | Segmented pools with holds and release policy | §8 |
| **B4** Concurrency contract | Hold→Allocate→Consume, total lock order, conditional decrement, idempotency, reconciliation | §17 |
| **B5** Refund allocation rule | **Value Allocation Record** — snapshotted at purchase, one rule for revenue *and* refunds | §9.5 |
| **B6** Five-axis RegistrationPolicy | Replaces the four-value enum | §7 |
| **B7** DelegatedRegistration | `SeatBlock` + a third management persona | §7.5 |
| **B8** Unified ParticipantRole | One model; capabilities reference roles, never own people-lists | §5 |
| **B9** Walk-in registration | On-spot registration against a walk-in pool | §7.6 |
| **B10** Editions | `EventSeries` with two modes | §13.2 |
| **B11** Pass channel dimension | `AdmissionRight.channel` + channel-scoped pools | §9.3 |
| **B12** Internal approval chains | `ApprovalChain` on OrgUnit, inherited | §14.3 |
| **B13** Capability `LOCKED` state | Capabilities with dependent data lock, never disable | §11.4 |
| **B14** Five validation gates | Explicit gate table | §14.2 |
| **B15** Team vs member eligibility | `AudienceRule.applies_to` | §4.4 |
| **B16** Money as (amount, currency) | `Money` value type platform-wide | §9.1 |

**Contradictions removed from V2:**

| V2 said | V3 says | Why |
|---|---|---|
| "AgendaItem is never an Event" — yet allowed seat caps on it | AgendaItem may hold an InventoryPool, but never a Pass, Registration, or Credential | The boundary is *commerce and identity*, not capacity |
| "Template is never a grouping key" | Template is a **secondary** analytics dimension, never replacing Kind | Cross-year template performance is a legitimate question |
| `registration_model` is a 4-value enum | Five orthogonal axes | The enum could not express real combinations |
| Occurrences cover annual fests | Occurrence ≠ Edition; one `EventSeries` entity, two modes | Different content, different listings, different analytics |
| Purchase `Group` and competitive `Team` coexist | `Group` deleted; party bookings are one Registration with N Admissions | Two group models is the D-018 failure mode |
| "Fixtures: R" as a Tournament capability | Stages capability with declared formats | A boolean cannot express Swiss vs double-elimination |

---

## 1. First principles

> **An event is a bounded commitment of people to a time and a place (physical or virtual), organised by an accountable party, with a defined way in and a defined thing that happens.**

| Invariant | Always | Never |
|---|---|---|
| Time-bounded | has a start | open-ended (that is a service) |
| Convening | ≥2 people in one context | a solo activity |
| Accountable | exactly one owning party | ownerless |
| Gated | a defined way to be present | unbounded (that is broadcast) |
| Programmed | something is intended to happen | a bare room booking |

**Governing tenet, unchanged from V2 and still the reason this design exists:**

> The as-built system's 145 event types were read by no filter, sort, permission check, pricing rule, notification, or analytics grouping. **A type earns its existence only by changing what the system does.** In V3, a Kind is a named, stable, analysable bundle of capabilities — nothing else.

**Admission test for a Kind** — a candidate qualifies only if it changes at least one of: registrant classes · required intake · admission allocation · in-event operation · produced artifact. Otherwise it is an attribute.

---

## 2. The Universal Event Catalog — 20 Kinds

Unchanged from V2. The catalog survived review; only its *bindings* changed.

| Group | Kinds |
|---|---|
| **Competitive** | Hackathon · Competition · Tournament · Race |
| **Learning** | Conference · Workshop · Talk · Training |
| **Community** | Meetup · Exhibition · Selection Drive |
| **Experience** | Performance · Trip |
| **Ceremonial** | Ceremony · Celebration · Wedding |
| **Purpose** | Fundraiser · Camp |
| **Structural** | Festival · Meeting |

**Six variation rules** — a candidate that differs only by these is an attribute, not a Kind:

| Rule | Rejected | Modelled as |
|---|---|---|
| R1 Audience is not type | Student/Corporate Hackathon, College Fest | `AudienceRule` + owning `OrgUnit` |
| R2 Topic is not type | Coding/Dance/Robotics Competition | `topics[]` |
| R3 Mode is not type | Webinar, Virtual Conference | `mode` |
| R4 Scale is not type | Mega Fest, Mini Hackathon | capacity + `topics` |
| R5 Occasion is not type | Diwali Celebration, New Year Bash | `topics[]` + date |
| R6 Formality is not type | Gala Dinner vs Dinner Party | `tags[]` |

The 145 retired names survive as **aliases** on the Kind registry — searchable, never stored on an event.

**Kind / Template / Capability — three layers:**

| Layer | Vocabulary | Owner | Analysable |
|---|---|---|---|
| **Kind** | closed, 20 | platform | ✅ primary dimension |
| **Template** | open, unlimited | platform / org / unit / person | ✅ secondary dimension |
| **Capability** | closed, ~40 | platform | ✅ |

---

## 3. The Structural Model  [B2]

V2 proposed three structures and the review proved the set incomplete. V3 defines **three relation classes containing five relations**, and asserts exhaustiveness by construction.

### 3.1 The classification

```
CONTAINMENT   — lives inside one Event, has no independent identity
   ├─ Stage        a competitive phase with its own participants, results, schedule
   └─ AgendaItem   a scheduled item with no participant set of its own

COMPOSITION   — Event contains Event
   └─ SubEvent     independently bookable, own capacity, own registration

LINEAGE       — Event relates to Event without containment
   ├─ Occurrence   the same content, run again        (EventSeries, mode=RECURRING)
   └─ Edition      the same brand, different content  (EventSeries, mode=EDITIONS)
```

### 3.2 The decision matrix — exhaustive by the four questions that matter

| | Own participant set? | Separately bookable? | Own results? | Own listing? |
|---|:-:|:-:|:-:|:-:|
| **SubEvent** | ✅ | ✅ | optional | ✅ |
| **Stage** | ✅ (by advancement) | ❌ for competitors · optional for spectators | ✅ | ❌ |
| **AgendaItem** | ❌ (attendance only) | ❌ | ❌ | ❌ |
| **Occurrence** | ✅ | ✅ | optional | one listing, many dates |
| **Edition** | ✅ | ✅ | optional | ✅ separate, one brand page |

Four binary questions yield sixteen cells; the five relations occupy every reachable one. A structure with **no** participant set, **not** bookable, **no** results and **no** listing is not an event structure at all — it is metadata. This is the exhaustiveness argument V2 lacked.

### 3.3 The organiser-facing decision tree

```
Is it the same content, happening again?          → OCCURRENCE
Is it the same brand, different content?          → EDITION
Can a person attend this and nothing else?        → SUB-EVENT
Does it have its own competitors and results?     → STAGE
Otherwise                                         → AGENDA ITEM
```

Applied to the classic confusions:

| Case | Structure | Reason |
|---|---|---|
| Fest → Hackathon | Sub-Event | separately attendable |
| Conference → Keynote | Agenda Item | included in the conference admission |
| Conference → paid Workshop | Sub-Event | separate fee, own capacity |
| Hackathon → Qualifier / Semi / Final | **Stages** | own competitors, own results, not separately registerable |
| Wedding → Haldi / Mehendi / Reception | Sub-Events | different guest lists, days, venues |
| Workshop batches A/B/C | Occurrences | identical content |
| Kurx Fest 2025 / 2026 | Editions | different content, same brand |
| Sports Meet → Cricket, Football | Sub-Events; the meet is a Festival | umbrella over independently attendable events |

### 3.4 Rules

1. **Composition depth ≤ 3.** Enforced in the API. Deeper is always a modelling error.
2. **Only roots and Editions are discoverable by default.** A Festival is one card; sub-events surface inside it, standalone only on opt-in.
3. **AgendaItem has no Pass, Registration, or Credential** — it may hold an InventoryPool for seat limits, and attendance is derived from the parent Credential plus a scan. *The boundary is commerce and identity, not capacity.* [contradiction removed]
4. **Stage is not addressable for registration.** Competitors arrive by advancement. Spectators, if permitted, arrive through a Stage-scoped InventoryPool (§10.4).
5. **Lineage never nests.** A Series contains Events; an Event belongs to at most one Series. Editions are siblings, never parents. Using `parent_id` for years is prohibited — it breaks the depth cap by design.

### 3.5 Inheritance across Composition

| Tier | Fields | Behaviour |
|---|---|---|
| **Bound** — always the parent's | `org_id`, permission root, verification status, cancellation cascade, financial account, currency | divergence is unsafe or nonsensical |
| **Seeded** — copied at creation, then independent | timezone, venue, branding, contact, capability preset, topics, language, default visibility | organiser may diverge; parent offers explicit **push-to-children** with a diff preview and per-child audit entry |
| **Never inherited** | dates, inventory, passes, status, slug, short code, description | always child-owned |

Cancellation is the only mandatory cascade — the refund obligation is joint (§9.6).

---

## 4. Identity & Context

### 4.1 One recursive tree for every organisation type

```
OrgUnit
  id, org_id, parent_id, kind, name, path, state
  kind ∈ { university, campus, school, college, department, program, centre,
           club, chapter, placement_cell, laboratory,
           company, division, team, project, committee, office, region,
           ngo_chapter, family_side, ... }              ← open list
  UNBOUNDED depth · every level OPTIONAL · a solo organiser has one node
```

Universities and companies are the **same tree** with different `kind` labels. Building two hierarchies is prohibited — every downstream system (permissions, rollup, discovery scoping, budget, audience rules) would need two implementations forever.

**Matrix organisations** are solved by *multiple memberships*, never by a DAG or multi-parent nodes. A person in Engineering **and** Bangalore **and** the Diversity Committee holds three memberships. The tree stays a tree; ancestor queries stay linear.

**A one-node tree must cost nothing.** If an org has exactly one unit, the unit picker never renders and `org_unit_id` auto-sets.

### 4.2 OrgUnit lifecycle  [edge 14 resolved]

A unit that owns events is **never deleted**.

| Operation | Semantics |
|---|---|
| `archive` | unit stops accepting new events and new memberships; existing events unaffected; historical analytics resolve |
| `merge_into(target)` | memberships reparent; event ownership re-points; the source persists as a **tombstone** so historical rollups still resolve; one audit record per moved entity |
| `reparent(new_parent)` | permission grants re-evaluate; a warning lists everyone gaining or losing access before confirmation |
| `delete` | permitted only when the unit has zero events, zero memberships, zero children |

University department restructuring is an annual event. This must be a first-class operation, not a support ticket.

### 4.3 Membership

```
Membership
  user_id, org_unit_id, role, state, verified_at, valid_from, valid_until
  attributes: { cohort_year: 2026,    ← IMMUTABLE. NEVER an ordinal year.
                section, roll_no, employee_id, grade, shift, cgpa, ... }
  source: SELF_DECLARED | INVITED | IMPORTED | PROVISIONED(idp)
```

**Cohort year, never ordinal year.** "Final Year" means the 2026 batch today and 2027 next June. Any rule anchored on "Year 4" is silently wrong after every promotion cycle. Ordinal year is *derived* from cohort year plus the academic calendar. This is non-negotiable.

**`source` matters:** audience rules that gate anything consequential require `verified_at` and reject `SELF_DECLARED`. Provisioned memberships (SCIM/HRIS) are trusted on arrival.

### 4.4 AudienceRule — who may register  [B15]

```
AudienceRule
  event_id
  predicate:
    unit_subtree_in[]      role_in[]        cohort_year_in[]
    attribute_matches{}    require_verified: bool
    external_orgs_allowed: bool
  applies_to: EVERY_MEMBER | CAPTAIN_ONLY | AT_LEAST_N(n) | TEAM_ATTRIBUTE   ← [B15]
  guests: { allowed, per_registrant_cap, aggregate_pool_id, requires_approval }
  DENY BY DEFAULT · evaluated SERVER-SIDE · every decision logged
```

**`applies_to` is the ruling on team eligibility.** Default `EVERY_MEMBER`. A cross-college hackathon that wants mixed teams sets `CAPTAIN_ONLY` or `AT_LEAST_N(1)`. A tournament requiring a whole team from one college sets `TEAM_ATTRIBUTE` with the team's declared unit. Making it explicit prevents the decision being made accidentally in code.

**Re-evaluation policy.** Eligibility is checked at registration *and* re-checked at admission. If a membership lapses between the two, the admission is flagged, not silently voided — an organiser decides. Silent revocation at the gate is unacceptable; silent admission of the ineligible is equally so.

Six canonical requirements, one mechanism, zero new entities:

| Requirement | Owner unit | Rule |
|---|---|---|
| Hackathon for CSE only | CSE dept | `unit_subtree_in:[CSE]` |
| Placement Drive, final year | Placement Cell | `cohort_year_in:[2026]`, `role:student`, `require_verified` |
| Workshop for ECE | ECE dept | `unit_subtree_in:[ECE]` |
| Orientation for freshers | College | `cohort_year_in:[2030]` |
| Department Fest | CSE dept | open, or `unit_subtree_in:[CSE]` |
| Annual College Fest | College | none — public |

**Ownership (`org_unit_id`) and audience (`AudienceRule`) are always separate.** CSE may own an event open to everyone; the Placement Cell may own one restricted to CSE.

### 4.5 Person identity levels  [edge 7 resolved, reconciles D-036]

Not every participant needs an account. Weddings, camps, and door sales are majority account-less.

```
IdentityRequirement (set per RegistrationPolicy.INTAKE)
  NONE      — name only. Wedding guests, walk-in camp beneficiaries.
  CONTACT   — verified phone or email, no account. Guest checkout (D-036).
  ACCOUNT   — a Kurx account.
  VERIFIED  — account + verified membership. Competitions, placement drives.
```

A `Person` record may exist at any level and be upgraded in place — a walk-in who later creates an account keeps their attendance history. **This is the reconciliation with D-036** ("guest checkout for Events, mandatory accounts for Competitions"): that decision becomes a per-policy field rather than a per-Kind hardcode.

---

## 5. Participants  [B8]

### 5.1 One model, replacing three

V2 left three overlapping systems: `EventAssignment.Role` (free text), capability-owned people-lists, and org RBAC. V3 unifies them. **Capabilities reference participant roles; they never own their own people tables.**

```
ParticipantRole  (registry)
  slug, name, class, default_permissions[], default_access_zones[],
  is_public, counts_toward_capacity, inventory_segment

EventParticipant
  event_id, subject (person | team | org_unit), role_slug, custom_label
  state: INVITED | ACCEPTED | DECLINED | ACTIVE | REMOVED | COMPLETED
  scope: whole_event | sub_events[] | stages[] | agenda_items[]
  visibility: public | internal
```

### 5.2 Three orthogonal facts, never conflated

```
PARTICIPATION  "I am here, in this capacity"   → EventParticipant
PERMISSION     "I may do X"                    → derived from role, overridable per participant
CREDENTIAL     "this gets me through that gate"→ Credential + access zones
```

A Judge participates as a judge, permits `scoring:submit` on assigned subjects, and credentials into the judging room and green room but not backstage.

### 5.3 Governance — hardcoded where it must be, open where it should be

| Layer | Governance | Rationale |
|---|---|---|
| `class` | **hardcoded, 7 values** | capacity and permission logic branch on it; must be closed |
| `slug` | platform registry, **org-extensible** | organisers need "Anchor", "Rangoli Judge", "Bouncer" |
| `custom_label` | free text | last resort; never a permission or capacity input |

```
ORGANISER    Owner · Manager · Coordinator · Staff
OPERATIONS   Volunteer · Security · Medical · Technical · Registration Desk
CONTENT      Speaker · Artist · Performer · Panelist · Trainer
EVALUATION   Judge · Examiner · Referee · Scrutineer · Mentor
PARTICIPANT  Attendee · Competitor · Team Member · Delegate
COMMERCIAL   Sponsor · Exhibitor · Recruiter · Vendor
OBSERVER     VIP · Media · Guest · Chaperone
```

**`counts_toward_capacity` and `inventory_segment` are properties of the role.** Volunteers and Media do not consume attendee inventory. Competitors do. VIPs consume the VIP segment. Every one of these is a manual spreadsheet correction at real events today.

### 5.4 Permission rules

1. **A participant role never grants org-level permission.** Participant grants are a fourth grant source, scoped strictly to the event subtree.
2. Effective permission = union of grants along Org → OrgUnit → Event ancestry, plus participant grants, **evaluated live per request** (never from a token claim — preserving the existing D-015 invariant).
3. Grants flow down only. A child grant never elevates the parent.

### 5.5 Conflict of interest  [edge 15 resolved]

```
ScoringPolicy.conflict_rules[]
  relation: SAME_ORG_UNIT | MENTOR_OF | MEMBER_OF | DECLARED_BY_EVALUATOR
  action:   BLOCK | WARN | REQUIRE_DISCLOSURE
```
Evaluated when assignments are made and again at score submission. A judge who mentors a team is blocked from scoring it by default. Cheap now, impossible to retrofit cleanly once results exist.

---

## 6. Teams  [B1]

### 6.1 The purchase Group is deleted

V2 left an existing `Group` / `GroupNumber` / `RegistrationMode.Group` model in place alongside a new Teams capability. Two group models is the D-018 failure mode. V3 removes one:

> **A party booking is one Registration (subject = Person) producing N Admissions.** No group entity. Per-participant form answers attach to the Admission. A family buying four concert tickets creates no `Group` row.
>
> **`Team` is the only group entity, and it exists only where competition does.**

| | Party booking | Team |
|---|---|---|
| Entity | none — N Admissions on one Registration | `Team` |
| Membership mutable | no | yes, until lock |
| Identity (name, logo, captain) | no | yes |
| Survives the event | no | yes — it is a result record |
| Disqualifiable, mergeable, splittable | meaningless | yes |

### 6.2 The model

```
Team
  event_id, name, slug, logo, tagline, declared_org_unit_id (nullable)
  state: FORMING → COMPLETE → LOCKED → COMPETING
                 → { ELIMINATED | DISQUALIFIED | WITHDRAWN | FINALIST }
  registration_id            ← the Team's own Registration when it registers as a unit
  every transition audited (disqualification requires reason + actor)

TeamMembership
  team_id, person_id
  role:  CAPTAIN | CO_CAPTAIN | MEMBER | SUBSTITUTE | MENTOR
  state: INVITED | REQUESTED | ACTIVE | REPLACED | REMOVED | LEFT
  joined_at, left_at, replaced_by_membership_id     ← substitution is an edge, not a delete

TeamInvite       team_id, invitee(person|email|phone), token, expires_at, state
TeamJoinRequest  team_id, person_id, message, state, decided_by, decided_at
```

Substitution never deletes history. `replaced_by_membership_id` preserves who competed in which Stage — required for result integrity and for certificates.

### 6.3 TeamPolicy — configuration of the Teams capability

```
min_size, max_size                                  required
lock_at                                             after: no join / leave / rename
formation_mode:  OPEN | INVITE_ONLY | ORGANISER_ASSIGNED | RANDOM_ALLOCATION
join_approval:   NONE | CAPTAIN | ORGANISER
edit_window:     { name_until, roster_until, mentor_until }
max_teams_per_person_in_event:        1
max_teams_per_person_across_tree:     1 | unlimited      ← Festival-level constraint
allow_solo_as_team:      bool                       "Individual + Team together"
allow_cross_org_members: bool                       gates §4.4 applies_to
substitutes_allowed: n · substitution_deadline
incomplete_team_policy: BLOCK_AT_LOCK | AUTO_MERGE | ALLOW_UNDERSIZED | WAITLIST
team_waitlist: { enabled, ordering: fcfs | score | random }
```

`incomplete_team_policy` answers the most common operational failure at student hackathons — forty two-person teams at lock when the minimum is three. Without it, organisers export a CSV and email people, which is what they do today *without* a platform.

### 6.4 Merge and split — composite transactions, not UI conveniences

| Operation | Preconditions | Effects |
|---|---|---|
| `merge(A, B) → C` | both captains consent (or organiser override); `\|A\|+\|B\| ≤ max_size`; **neither has been scored** | one captain retained, the other demoted; submissions explicitly assigned to C or discarded, never silently merged; A and B become tombstones referencing C |
| `split(A) → A', B'` | organiser-only; both results satisfy `min_size`; **A has not been scored** | submissions stay with exactly one side, chosen explicitly; A becomes a tombstone |

Both are organiser-only after `lock_at` and **prohibited once any Stage scoring has begun.** A design permitting a merge mid-judging has created an integrity bug it cannot detect.

### 6.5 Team as a registration subject

A Team registration consumes **one team-slot and N person-slots** from the relevant inventory pools (§8). Each active member receives an Admission and a Credential. Substitution transfers the Admission; it does not mint a new one, so person-inventory stays balanced.

---

## 7. Registration  [B6]

### 7.1 Five orthogonal axes replace the four-value enum

`registration_model` is deleted. Registration is a composable policy:

```
RegistrationPolicy
  SUBJECT     PERSON | TEAM | ORG_UNIT | EXTERNAL_ORG
  GATE        OPEN | APPROVAL | INVITE | LOTTERY | PREREQUISITE | REFERRAL
              (composable — multiple gates evaluate in declared order)
  INTAKE      identity_requirement: NONE | CONTACT | ACCOUNT | VERIFIED
              form_id, documents_required[], application_id
  ALLOCATION  FCFS | LOTTERY | QUOTA | RANKED | ASSIGNED
  PAYMENT     FREE | SELF | DELEGATED | SPONSORED | DEFERRED
  windows     { opens_at, closes_at, late_window, edit_until, cancel_until }
```

Every registration type in the original requirement list is a point in this space:

| Requirement | Expression |
|---|---|
| Individual | SUBJECT=PERSON, GATE=OPEN |
| Team | SUBJECT=TEAM |
| Organization / Department / Company | SUBJECT=ORG_UNIT, PAYMENT=DELEGATED |
| Invite only | GATE=INVITE |
| Approval | GATE=APPROVAL |
| Lottery | GATE=LOTTERY, ALLOCATION=LOTTERY |
| Qualification based | GATE=PREREQUISITE |
| Paid / Free | PAYMENT=SELF / FREE |
| Referral | GATE=REFERRAL |
| Application | INTAKE.application_id + GATE=APPROVAL |
| Waitlist | ALLOCATION overflow → §8.5 |
| Section / Branch | SUBJECT=PERSON + AudienceRule attribute match |

A single event may offer several policies, one per Pass — an Individual open pass and a Team application pass on the same Competition.

### 7.2 The registration → admission chain

Four distinct concepts, each with one job:

```
Pass          the product   — what is bought or claimed
Registration  the act       — a subject secured participation; holds state, form answers, payment
Admission     the right     — one person's right of entry; consumes person-inventory
Credential    the artifact  — the scannable identity, ONE per person per event tree
```

A Team registration of four produces one Registration, four Admissions, four Credentials. A family buying four concert tickets produces one Registration, four Admissions, four Credentials. **The same shape, no group entity.**

### 7.3 Cross-event prerequisites

```
RegistrationPrerequisite
  event_id
  source: { event_id | stage_id | series_id }
  condition: PLACED_TOP_N | SCORE_GTE | COMPLETED | CERTIFIED | ATTENDED
  params, evaluated_at: ON_REGISTER | ON_RESULTS_PUBLISHED
```

Covers "only the top 20 from the Qualifier may enter the Final" and "Advanced Workshop requires Beginner Workshop" with one mechanism. This is the graph edge V2 entirely lacked.

### 7.4 Lottery — designed, not named

```
Lottery
  entry_window (distinct from the registration window)
  draw_at
  seed              recorded BEFORE the draw, published after   ← reproducible, auditable
  weighting         uniform | by_attribute | by_loyalty
  winners_pool_id, claim_ttl
  rollover: on claim expiry, offer to the next ranked entry, repeat until pool exhausted
  fairness_record   immutable: seed, entry count, ordered result
```

**The draw must be a seeded deterministic function, never a runtime RNG.** The as-built analytics job fabricating figures with `Random.Shared` is the cautionary precedent; a lottery has legal exposure that analytics does not.

### 7.5 Delegated registration  [B7]

```
SeatBlock
  event_id, pass_id
  registrant_org_unit_id        ← who registers (the college / company)
  payer_id                      ← may differ from registrant
  quantity, assignment_deadline
  seats[]: { admission_id, assigned_to_person | UNASSIGNED,
             form_state, reassignable_until, reassign_count }
  reassign_limit, audit trail per assignment
```

Three consequences V3 accepts explicitly:

1. **Seats are bought before people are known.** A Pass may mint *unassigned* Admissions. Person-inventory is consumed at purchase; identity binds later. This is a deliberate change to the Admission lifecycle.
2. **A third management persona exists** — the *delegate console*: assign, reassign, chase incomplete forms, view aggregate status. Neither the attendee nor the organiser surface. It must be built, not implied.
3. **Reassignment is governed** — deadline, count limit, audit — or it becomes an unmonitored resale channel.

This is how college contingents, corporate delegations, and school trips work. It is not an enterprise nicety.

### 7.6 Walk-in registration  [B9]

```
WalkInRegistration
  created_by: staff EventParticipant
  draws from: InventoryPool(segment = walk_in)
  identity_requirement: usually NONE or CONTACT
  payment: at-gate or free
  produces a Registration + Admission + Credential in one transaction
  offline_capable: queued locally, reconciled on reconnect with idempotency keys
```

Camps, Exhibitions, Performances, and any door-sale event need this. Offline capability is required, not optional — Indian venues lose connectivity routinely, and a check-in desk that stops working is worse than no platform.

---

## 8. Inventory & Capacity  [B3]

### 8.1 Capacity is an inventory, not a number

```
"200 seats: 120 reserved for CSE, 40 other departments, 20 VIP, 20 guests;
 unclaimed departmental quota returns to general at T-48h; VIP never returns;
 10 held for walk-ins."
```

Scalar capacity cannot express one clause of that. V3 replaces it with pools.

```
InventoryPool
  scope:   event | sub_event | stage | agenda_item | venue | zone
  segment: general | vip | quota(org_unit) | quota(role) | guest
           | accessible | press | staff | walk_in
  channel: IN_PERSON | VIRTUAL                     ← [B11]
  unit:    person_slot | team_slot                 ← [B1]
  total, held, allocated, consumed
  release_policy:     { at: T-Xh | on_sellout | never, to: pool_id }
  oversell_allowance: n (default 0 — explicit, never accidental)
  waitlist:           { enabled, ordering, offer_ttl, auto_promote }
```

- **Venue capacity** is a constraint on the *Venue* entity. Any event scheduled there validates against it. One venue hosts sequential events; conflating venue and event capacity is a category error.
- **Department / branch / org quotas** are `segment = quota(org_unit)`, composing directly with `AudienceRule` — the composition is the proof the abstraction is right.
- **Reserved seats** are `held` inventory with a named holder and a release schedule.
- **Guest quota** is consumed by plus-ones, capped per registrant *and* in aggregate.
- **Accessibility inventory** is its own segment. In several jurisdictions wheelchair and companion positions are legally distinct inventory.
- **`unit = team_slot`** lets a Competition cap "50 teams" and "200 people" independently.

### 8.2 The lifecycle

```
   ┌──────┐   reserve    ┌──────┐   confirm   ┌───────────┐   scan   ┌──────────┐
   │ free │ ───────────▶ │ held │ ──────────▶ │ allocated │ ───────▶ │ consumed │
   └──────┘              └──────┘             └───────────┘          └──────────┘
       ▲                    │ TTL expiry            │ cancel / refund
       └────────────────────┴───────────────────────┘
```

**Hold before payment, always.** A hold is TTL-bounded and idempotent. `ExpireSeatHoldsJob` already exists in the codebase and is the sweeper for this.

### 8.3 Multi-pool draws

An AdmissionRight declares which pools it draws from and when:

```
AdmissionRight.draws[]:  { pool_selector, at: AT_PURCHASE | AT_CLAIM | AT_ENTRY }
```

A Festival all-access pass draws from the festival pool `AT_PURCHASE` and from each sub-event pool `AT_CLAIM` (if reservation is enabled) or `AT_ENTRY` (if not). **Two counters, both authoritative.** Entry to a full sub-event on a valid pass is refused with a distinct, explainable outcome — `capacity_full`, never `invalid_credential`.

### 8.4 No-show and release

```
no_show_policy: NONE | RELEASE_AFTER(minutes) | DEPOSIT | STRIKE
```
Free events run 30–50% no-show. Without release-on-no-show or a deposit, capacity is fiction. This is the single loudest complaint about free student events, and it is a policy field, not a feature request.

### 8.5 Waitlist

Attached to a pool, not an event — a VIP waitlist and a general waitlist are different queues. On release or promotion, the next entry receives a time-boxed offer; expiry rolls forward automatically. The existing `ExpireWaitlistOffersJob` is the sweeper.

---

## 9. Money, Passes & Admission

### 9.1 Money is never a bare integer  [B16]

```
Money = { amount_minor: int64, currency: ISO-4217 }
```

Every price, fee, refund, ledger entry, payout, and analytics aggregate. An event declares one `settlement_currency`, bound from its Org (§3.5) and immutable once any Pass is sold. Multi-currency display is a presentation concern; multi-currency *settlement* on one event is deliberately out of scope.

**Retrofitting a currency dimension across orders, tickets, ledger, payouts, refunds, and analytics after five years of data is a multi-quarter project. It costs a week now.**

### 9.2 Pass and AdmissionRight

```
Pass
  event_id, name, Money price, registration_policy_id
  quantity, sale_window, per_subject_limit
  visibility: public | unlisted | code_only | invite_only
  grants → AdmissionRight[]

AdmissionRight
  scope:   SINGLE(event) | SUBTREE(event) | SET([events]) | QUERY(predicate)
  channel: IN_PERSON | VIRTUAL | EITHER                    ← [B11]
  uses:    n (default 1)
  window:  optional time restriction
  window_policy: STARTS_WITHIN | FULLY_CONTAINED | OVERLAPS  ← [edge 2]
  draws[]: pool selectors with draw timing (§8.3)
```

`window_policy` resolves the day-pass problem: does a Day-2 pass admit to a sub-event running Days 1–3? Default `STARTS_WITHIN` (no). An organiser may choose `OVERLAPS` (yes). Previously undefined.

`channel` resolves hybrid: a conference sells 500 in-person and 5,000 virtual as two Passes drawing on two channel-scoped pools at different prices.

| Product | Expression |
|---|---|
| Single event ticket | `SINGLE(hackathon)` |
| Fest all-access | `SUBTREE(fest)` |
| Day pass | `SUBTREE(fest)` + window + `STARTS_WITHIN` |
| Combo, pick 3 of 7 | `SET([a,b,c])` selected at checkout |
| Virtual conference seat | `SINGLE(conf)`, `channel=VIRTUAL` |
| Season / membership | `QUERY(org=X AND kind=meetup)` + recurring term |
| Wedding family pass | `SET([haldi, mehendi, reception])` |

### 9.3 Credential

**One Credential per person per event tree.** The QR resolves to a person; the scanner resolves person → admissions → rights → this gate. Never one QR per sub-event — that produces attendees holding seven screenshots.

### 9.4 Forms

Shared fields are collected once at the tree root. A sub-event collects only fields it alone needs. **A field already answered at an ancestor is never re-asked.** Form answers attach to the Admission (per-person) or the Registration (per-party), matching the existing `FormFieldScope` semantics.

### 9.5 The Value Allocation Record — one rule for revenue and refunds  [B5]

The highest-stakes gap in V2. V3 resolves it with a snapshot.

> **At purchase, a multi-scope Pass's price is allocated across its constituent events and written immutably to the order line as a Value Allocation Record (VAR).**
>
> **The VAR is the sole basis for revenue reporting AND for refunds.** They cannot diverge because they read the same rows.

```
allocation basis, in order of precedence:
  1. the event's own standalone list price at time of purchase
  2. the event's declared allocation_weight
  3. equal share across in-scope events

VAR line: { order_line_id, event_id, Money allocated, basis, computed_at }
Σ(allocated) == price paid, exactly. Rounding remainder assigned to the
highest-allocated line, deterministically.
```

Why snapshot rather than derive: list prices change, sub-events are added and removed, and events are cancelled. A derived allocation gives different answers in March and June for the same order. **A snapshot is the only design where the books balance.**

### 9.6 Cancellation and refunds

| Event | Refund |
|---|---|
| Sub-event cancelled, buyer holds `SINGLE` | full price |
| Sub-event cancelled, buyer holds `SUBTREE` | **exactly the VAR line for that sub-event** |
| Parent cancelled | all VAR lines, i.e. the full price; cascades to sub-events (§3.5) |
| Buyer cancels within `cancel_until` | per the event's cancellation policy |
| Material change (§14.5) | full refund window opens; buyer's choice |

Inventory returns to its originating pool on refund. Refund writes reverse the ledger — never a deletion.

### 9.7 Delegated and sponsored payment

`PAYMENT=DELEGATED` — the payer is the `SeatBlock.payer_id`; the invoice is to the org, refunds return to the org, not the seat holder.
`PAYMENT=SPONSORED` — a sponsor covers N admissions from a named pool; attendees pay nothing and see no price.

---

## 10. Competition  [B2]

### 10.1 Stage

```
Stage
  event_id, sequence, name
  format: SINGLE_SUBMISSION | JURY_REVIEW | KNOCKOUT | DOUBLE_ELIM
        | ROUND_ROBIN | SWISS | LEAGUE | TIME_TRIAL | PUBLIC_VOTE
  participant_source: ALL_REGISTERED | ADVANCED_FROM(stage) | SEEDED | WILDCARD
  advancement_rule:   TOP_N | TOP_PERCENT | SCORE_GTE | MANUAL
  scoring_policy_id
  schedule: own window, own venue, OWN MODE      ← a Final may be offline when Quals were online
  results_visibility: LIVE | ON_STAGE_CLOSE | ON_EVENT_CLOSE
  spectator_pool_id (nullable)                   ← §10.4
```

A Stage has its own participants (by advancement), results, schedule, venue and mode, but is **not registerable**. That combination is why it is a fourth structure and not a Sub-Event or an Agenda Item.

### 10.2 Fixture

```
Fixture
  stage_id, round_no, participants[] (person | team), venue, slot, officials[]
  result, state: SCHEDULED | LIVE | COMPLETE | WALKOVER | ABANDONED | DISPUTED
```

`WALKOVER` and `ABANDONED` are first-class. Without them organisers falsify results to make the bracket advance.

**Scheduling.** Fixture assignment across venues, slots, officials and rest periods is a constraint-satisfaction problem. V3 requires only that Fixtures be **manually schedulable with conflict detection** (double-booked venue, official, or participant). An automated solver is an optimisation over the same schema and is explicitly deferred — the data model does not change when it arrives.

### 10.3 Scoring

```
ScoringPolicy
  sources[]:      { JUDGE(weight, rubric_id) | PUBLIC_VOTE(weight, rules) | AUTOMATED(weight) }
  aggregation:    SUM | WEIGHTED_MEAN | TRIMMED_MEAN | MEDIAN | RANK_AGGREGATION
  normalisation:  per_judge_zscore | none
  tie_break[]:    ordered, EXPLICIT — an unresolved tie is a defect, not an outcome
  conflict_rules: §5.5
  reveal:         per Stage.results_visibility

PublicVoteRules
  identity_binding: ACCOUNT | VERIFIED_CONTACT      ← never IP
  one_vote_per: identity per stage
  rate_limit, weight_cap                            ← bounds influence against judge score
  anomaly_detection, immutable public tally audit
```

Vote brigading is guaranteed at student events. Naive public voting turns the first contested result into a reputation incident.

### 10.4 Spectators  [edge 1 resolved]

A Stage may declare a `spectator_pool_id`. Spectators buy an AdmissionRight scoped to the Stage's parent event with a window matching the Stage, drawing on that pool, entering as `class = OBSERVER`. **Competitors arrive by advancement; observers by admission.** The Stage remains non-registerable for competitors, so the structural rule holds without a fifth concept.

### 10.5 Results integrity

```
Result
  stage_id, subject, rank, score_breakdown, published_at
  state: PROVISIONAL | PUBLISHED | DISPUTED | CORRECTED
  corrections[]: { by, at, reason, previous_value }   ← append-only, never a silent edit
```

Disputes have a window, a reviewer, and an outcome. A published result is immutable except through a recorded correction. Certificates and prerequisites (§7.3) read `PUBLISHED` results only.

---

## 11. Capabilities

### 11.1 The model

```
CapabilityRegistry   (~40, platform-governed)
  slug, name, config_schema,
  provides: { fields[], workspace_tab, endpoints[], permissions[], analytics[] }
  depends_on[]        ← the ONLY permitted coupling; must form a shallow DAG
  mode_availability   ← §11.3

KindDefaults      Kind × Capability → { REQUIRED | ON | OFF }
EventCapability   event_id, capability_slug, state, config_json, locked_reason
```

Subsystems are reached through capabilities: `teams` carries `TeamPolicy`, `stages` carries Stage definitions, `scoring` carries `ScoringPolicy`, `quotas` carries pool segmentation. **The capability contract pattern absorbing three new subsystems without modification is evidence the abstraction is sound.**

### 11.2 States

| State | Meaning |
|---|---|
| **REQUIRED** | on, cannot be disabled |
| **ON** | on by default, may be disabled |
| **OFF** | available, off by default |
| **LOCKED** | [B13] has dependent data; may be hidden from navigation, but data and endpoints persist and remain exportable |

> **A capability with dependent data becomes LOCKED, never disabled.** Turning off Teams with forty teams registered, or Judging mid-scoring, must not destroy data. Silent, irreversible loss on a toggle is unacceptable.

### 11.3 Mode-gated availability — the only hard constraint

| Mode | Unavailable | Required |
|---|---|---|
| Online | Seat Map · Route & Timing · Venue · Travel & Stay · physical Check-in | Streaming URL · virtual check-in |
| Offline | Streaming URL | Venue |
| Hybrid | — | Venue **and** URL; channel-scoped pools (§8.1) |

Kind never restricts availability. Predicting every legitimate oddity over ten years is impossible; mode constraints are small, hard, and will still be true in 2036.

### 11.4 The independence rule

`depends_on` is the sole permitted coupling. Without it, forty capabilities are 2⁴⁰ untestable combinations. With it, testing is forty capabilities plus a handful of declared edges. **Any change introducing an undeclared cross-capability branch is rejected at review.** This rule is load-bearing for the whole design.

---

## 12. Classification & Metadata

### 12.1 Seven fields, not thirteen axes

**Structural — closed, drives behaviour**

| Field | Values | Responsibility |
|---|---|---|
| `kind` | 20 | capability preset; primary analytics dimension |
| `mode` | Offline / Online / Hybrid | capability availability; venue-vs-URL |
| `visibility` | Public / Unlisted / Internal / Private | who can see it |
| *(registration)* | — | now `RegistrationPolicy`, §7 |

**Descriptive — open, drives discovery**

| Field | Responsibility |
|---|---|
| `topics[]` | subject matter — replaces both Category and Industry |
| `tags[]` | long-tail search, organiser vocabulary |
| `languages[]` | delivery language(s) |

**Contextual — never stored**

Category derived from `kind` · organiser type from `org` · "student event" from `owner_unit.kind` and audience rules · difficulty, age band, prerequisites from capability config.

Eliminated as duplicates: Category (derive), Audience (split into rules/visibility/owner), Industry (merge into topics), Organizer Type (belongs to Org), Purpose (is Kind), Features (is Capabilities), Difficulty and Age Group (capability config and eligibility).

### 12.2 The one-home rule

> Every fact has exactly one home. Organiser → Org. Unit → OrgUnit. Who may attend → AudienceRule. Module behaviour → capability config. Subject → topics. **If it can be derived, it is never stored.**

### 12.3 Anti-EAV promotion rule

| Attribute is… | Lives in |
|---|---|
| filtered, sorted, or aggregated | a **real typed column** — no exceptions |
| capability configuration | `config_json`, validated against `config_schema` |
| descriptive, display-only | `attributes` JSONB, never queried |
| organiser-defined | a Template's form definition |

**Any JSONB key appearing in a `WHERE`, `ORDER BY`, or `GROUP BY` becomes a column within one release.** Enforced by lint, not discipline.

---

## 13. Templates & Series

### 13.1 Templates

```
Template
  scope: PLATFORM | ORG | UNIT | PERSONAL      ← most specific wins; picker shows provenance
  kind_slug, version, state
  carries: capabilities + config, form fields, agenda skeleton, stage definitions,
           pass structure, participant roles, branding, default audience rule,
           publish-checklist overrides
  never carries: dates, inventory, slug, status, anything financial
```

**Snapshot-applied at creation.** Changing a template never mutates events made from it — consistent with §3.5. Events record `created_from_template_version`, so "why do two events from the same template differ" is answerable.

**Template is a secondary analytics dimension** — never replacing Kind, always available alongside it. [contradiction removed]

**AI-assisted templates** are permitted under three constraints: output is a **draft Template**, never a published event; it must validate against the capability registry (an unknown capability is rejected, not created); the organiser reviews and edits before creation. The closed registry is what makes this a generator rather than a hallucination engine. The risk is taxonomy drift, and strict validation is the control.

### 13.2 EventSeries — one entity, two modes  [B10]

```
EventSeries
  org_id, name, slug, brand assets, follower list
  mode: RECURRING | EDITIONS
  RECURRING:  rrule (RFC 5545), exception_dates[], per-occurrence timezone
  EDITIONS:   ordinal / label per member event
```

| | RECURRING | EDITIONS |
|---|---|---|
| Content | identical | different |
| Listing | one, pick a date | separate, one brand page |
| URL | series slug + date | own slug, canonical to the series |
| Analytics | rolled up as one | compared period over period |
| Test | *"Would you mind attending the other one instead?"* No | Yes |

| Example | Mode |
|---|---|
| Weekly Meetup, Monthly Workshop batches | RECURRING |
| Annual Fest, Seasonal Festival, Conference Series | EDITIONS |

**Followers, brand assets, and canonical SEO authority live on the Series**, carrying across editions. Occurrences carry independent inventory and cancellation; every edit asks "this one, or all future". **Per-occurrence timezone** — a weekly series crossing DST shifts local time, so timezone is an occurrence property, not an event property.

---

## 14. Lifecycle, Gates & Approvals

### 14.1 Status

```
DRAFT → IN_REVIEW → SCHEDULED → REGISTRATION_OPEN → LIVE → COMPLETED → ARCHIVED
                 ↘ CANCELLED (terminal) ↗
```

V2's model preserved and refined: `SCHEDULED` (published, registration not yet open) and `LIVE` are now distinct, because check-in configuration and staff assignment are gated on going live, not on publishing.

### 14.2 Five validation gates  [B14]

One publish checklist was wrong. Each transition enforces only what it needs:

| Gate | Enforces |
|---|---|
| **Save Draft** | title only. Nothing else. Ever. |
| **Add capability** | that capability's minimum config; `depends_on` satisfied |
| **Publish → SCHEDULED** | description; venue-or-URL per mode; all REQUIRED capabilities configured; owner unit set; approval chain complete |
| **Open registration** | ≥1 Pass; ≥1 InventoryPool; audience rule resolves to ≥1 person; currency set; refund policy set |
| **Go live** | staff assigned; check-in configured; walk-in policy set if enabled |
| **Complete** | results published if any Stage exists; certificates queued if enabled |

Publish-readiness is a continuously computed checklist derived from enabled capabilities — never a wall of required fields at creation, and never a surprise at publish.

### 14.3 Internal approval chains  [B12]

```
ApprovalChain  (attached to an OrgUnit, inherited down the tree)
  steps[]: { approver (role | user), condition (always | if_paid | if_external
             | if_budget_gt | if_minors), sla, escalation_after }
  mode: SEQUENTIAL | PARALLEL
  bypass: who may — and it writes an audit entry
```

A student club cannot simply publish: faculty advisor → HOD → Dean → accounts (if funded) → admin (if external guests). A company event needs cost-centre approval. V2 had one approval flow — the platform reviewer for paid events. **Publishing without institutional approval is a governance violation, not an inconvenience**; without this, institutions do not adopt.

The platform review for paid events remains and is *additional*, not alternative. Order: internal chain → platform review → published.

### 14.4 Paid-event trust gates

Preserved from the as-built system. A paid event may not self-publish: the organiser must be paid-verified, the owning org verified (D-075), representation non-vacant (D-101), and a platform reviewer approves. All evaluated live.

### 14.5 Material change  [edge 16 resolved]

```
MaterialChange = change to { date | venue | mode | cancellation of a sub-event }
                 after any Registration exists

Effect: notify every registrant · open a refund window (default 7 days or until
        event start, whichever is sooner) · record the before/after in the audit spine
```

One mechanism covers date shifts, venue moves, offline→online conversion, and partial cancellation. An Offline→Online change additionally invalidates venue, seat map and route configuration, which lock rather than delete (§11.2).

---

## 15. Discovery & Search

| Concern | Design |
|---|---|
| Index | Postgres FTS + trigram at launch; a dedicated engine when facets or volume demand. Fed by the **existing transactional outbox** (ADR-AM16) — never dual-write. |
| Document | title, description, topics, tags, speakers, org, unit, venue, **kind aliases** |
| Filters | kind · topics · mode · channel · price · date · language · city · distance |
| Ranking | recency, velocity, conversion, proximity, affinity — replacing raw `ViewCount DESC` |
| Related | shared topics + kind + org + unit + co-attendance |
| Sub-events | a Festival is one card; sub-events surface inside it, standalone on opt-in |
| Eligibility-aware feeds | "events you can attend" must never leak the *existence* of an internal event — preserve the 404-not-403 invariant (D-018) |
| View counting | an event stream aggregated asynchronously. **The synchronous `ViewCount++` write on every public read is removed.** |

Retired names remain searchable as aliases; organisers and attendees keep their vocabulary.

---

## 16. Analytics

**Facts are recorded at the leaf. Aggregation happens on read, along both trees.**

```
Registration · Admission · Payment · Check-in · Score · View
Rollup dimensions:  event tree (Sub-Event → Stage) × org-unit tree
Secondary dimensions: kind · template · series · topic · channel · academic_year
```

Definitions that must be fixed once, platform-wide, or they are argued about forever:

| Metric | Definition |
|---|---|
| Attendance at a parent | distinct people who entered ≥1 sub-event — **not** the sum of sub-event attendance |
| Revenue of a sub-event | the **VAR** (§9.5). One number, used for reporting and refunds alike. |
| Capacity utilisation | per-pool against that pool's total; a full fest with empty sub-events is a real signal |
| Academic year | declared per institution; **never** assumed equal to calendar year |

**Three as-built defects are removed as part of this work, not after it:**
1. `AggregateDailyStatsAsync` fabricating Views/UniqueVisitors with `Random.Shared` — deleted. Fabricated figures become "historical data" nobody dares correct.
2. Two divergent analytics paths returning different numbers for one event — the leaf-fact model is authoritative; the other retires.
3. Synchronous `ViewCount` writes on the public read path — replaced by the event stream.

---

## 17. Concurrency, Consistency & Scale  [B4]

### 17.1 The inventory contract — non-negotiable on a money path

| Mechanism | Rule |
|---|---|
| **Reserve first** | a hold is created before payment; TTL-bounded; idempotent |
| **Total lock order** | pools are locked in ascending `pool_id`, everywhere, without exception — this is what prevents deadlock between a parent-first and a child-first path |
| **Conditional decrement** | `UPDATE … SET consumed = consumed + n WHERE consumed + n <= total + oversell_allowance`; never read-then-write |
| **Idempotency key** | client-supplied per attempt; retries never double-consume |
| **Outbox** | admission and payment side effects publish through the existing transactional outbox, never inline |
| **Reconciliation** | a periodic job proving `consumed == count(active admissions)` per pool, alerting on drift |

Without this, the two-counter design produces oversell, deadlock, and phantom inventory with certainty at scale. It was the most likely production incident in V2.

### 17.2 Hot-path budget

| Path | Requirement |
|---|---|
| Public event read | no writes; served from a denormalised read model |
| Discovery | index-served; no capability joins |
| Registration | audience rule evaluated server-side, cached per (person, event), short-circuited when the event is public and unrestricted |
| Check-in | person → admissions → rights → pool, resolvable in one indexed read; **must work offline and reconcile idempotently** |
| Permission | materialised-path ancestor walk, cached per (person, org), invalidated on grant change |

### 17.3 Data at scale

Fact tables partition by time. Retention and archival policy declared per fact class. Read/write separation: discovery is read-dominated and index-served; inventory is write-contended and pool-partitioned.

---

## 18. Compliance, Privacy & Trust

| Concern | Resolution |
|---|---|
| **Cross-org data controllership** | The event's **owning Org is the controller**. Participating orgs receive aggregate figures plus data on their own verified members — never the full registrant list. Stated in the event's privacy terms at creation. |
| **Minors** | `owner_unit.kind = school` or a declared minors audience triggers: guardian consent capability required, public discovery disabled, restricted PII visibility for organisers, chat off by default, shortened retention |
| **Audience rules are an authorisation surface** | server-side only, deny by default, verified memberships for consequential gates, every decision logged, covered by the mandatory security review |
| **Lottery and public vote** | reproducible seeds and immutable public audit records; both carry legal and reputational exposure |
| **Results** | append-only corrections, never silent edits |
| **Retention** | per fact class; legal hold supported; certificates and results outlive registrations |
| **Payments** | RFC7807 errors leak no internals; ledger writes reverse, never delete |

---

## 19. Capability Matrix

Universal to every Kind, not tabulated: Registration · Check-in · Announcements · Reminders · Media Gallery · Feedback · Chat · Audit.

**R** required · **●** default on · **○** available, default off

### A — Structure

| Kind | Sub-Events | Stages | Agenda | Tracks | Series | Multi-Venue | Multi-Day |
|---|:-:|:-:|:-:|:-:|:-:|:-:|:-:|
| Hackathon | ○ | **R** | ● | ○ | ○ | ○ | ● |
| Competition | ○ | **R** | ● | ○ | ● | ○ | ○ |
| Tournament | ○ | **R** | ● | ○ | ● | ● | ● |
| Race | ○ | ● | ● | ○ | ● | ○ | ○ |
| Conference | ● | ○ | **R** | ● | ● | ● | ● |
| Workshop | ○ | ○ | ● | ○ | ● | ○ | ○ |
| Talk | ○ | ○ | ○ | ○ | ● | ○ | ○ |
| Training | ○ | ○ | **R** | ○ | **R** | ○ | ● |
| Meetup | ○ | ○ | ● | ○ | **R** | ○ | ○ |
| Exhibition | ○ | ○ | ● | ○ | ● | ● | ● |
| Selection Drive | ● | **R** | ● | ○ | ● | ○ | ● |
| Performance | ○ | ○ | ● | ○ | ● | ○ | ○ |
| Ceremony | ○ | ○ | **R** | ○ | ● | ○ | ○ |
| Celebration | ○ | ○ | ● | ○ | ○ | ○ | ○ |
| Wedding | **R** | ○ | ● | ○ | ○ | ● | **R** |
| Fundraiser | ○ | ○ | ● | ○ | ● | ○ | ○ |
| Camp | ○ | ○ | ● | ○ | ● | ● | ○ |
| Festival | **R** | ○ | ● | ● | ● | ● | **R** |
| Trip | ○ | ○ | **R** | ○ | ● | **R** | **R** |
| Meeting | ○ | ○ | **R** | ○ | ● | ○ | ○ |

### B — People & Evaluation

| Kind | Teams | Submissions | Scoring | Officials | Speakers | Lineup | Mentors | Exhibitors | Volunteers |
|---|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|
| Hackathon | **R** | **R** | **R** | ○ | ○ | ○ | ● | ○ | ● |
| Competition | ● | ● | **R** | ● | ○ | ○ | ○ | ○ | ● |
| Tournament | **R** | ○ | **R** | **R** | ○ | ○ | ○ | ○ | ● |
| Race | ○ | ○ | **R** | **R** | ○ | ○ | ○ | ○ | **R** |
| Conference | ○ | ● | ○ | ○ | **R** | ○ | ○ | ● | ● |
| Workshop | ○ | ○ | ○ | ○ | ● | ○ | ● | ○ | ○ |
| Talk | ○ | ○ | ○ | ○ | **R** | ○ | ○ | ○ | ○ |
| Training | ○ | ● | ● | ○ | ● | ○ | ● | ○ | ○ |
| Meetup | ○ | ○ | ○ | ○ | ● | ○ | ○ | ○ | ○ |
| Exhibition | ○ | ● | ○ | ○ | ○ | ○ | ○ | **R** | ● |
| Selection Drive | ○ | **R** | **R** | ● | ○ | ○ | ○ | ● | ● |
| Performance | ○ | ○ | ○ | ○ | ○ | **R** | ○ | ○ | ● |
| Ceremony | ○ | ○ | ○ | ○ | ● | ● | ○ | ○ | ● |
| Celebration | ○ | ○ | ○ | ○ | ○ | ● | ○ | ○ | ○ |
| Wedding | ○ | ○ | ○ | ○ | ○ | ● | ○ | ○ | ● |
| Fundraiser | ○ | ○ | ○ | ○ | ● | ● | ○ | ○ | ● |
| Camp | ○ | ○ | ○ | ○ | ○ | ○ | ○ | ○ | **R** |
| Festival | ○ | ○ | ○ | ○ | ● | ● | ○ | ● | **R** |
| Trip | ○ | ○ | ○ | ○ | ○ | ○ | ○ | ○ | ● |
| Meeting | ○ | ● | ○ | ○ | ● | ○ | ○ | ○ | ○ |

### C — Access, Inventory & Commerce

| Kind | Paid | Passes | Seat Map | Quotas | Delegated | Walk-in | Waitlist | Invitations | Guest List | Donations | Sponsors |
|---|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|
| Hackathon | ● | ○ | ○ | ● | ● | ○ | ● | ● | ○ | ○ | ● |
| Competition | ● | ○ | ○ | ● | ● | ○ | ● | ● | ○ | ○ | ● |
| Tournament | ● | ○ | ● | ● | ● | ○ | ● | ● | ○ | ○ | ● |
| Race | **R** | ○ | ○ | ● | ● | ● | ● | ○ | ○ | ● | ● |
| Conference | ● | ● | ● | ● | ● | ● | ● | ● | ○ | ○ | **R** |
| Workshop | ● | ○ | ○ | ● | ● | ○ | **R** | ● | ○ | ○ | ○ |
| Talk | ● | ○ | ● | ○ | ● | ● | ● | ● | ○ | ○ | ● |
| Training | ● | ○ | ○ | ● | **R** | ○ | ● | ● | ○ | ○ | ○ |
| Meetup | ● | ○ | ○ | ○ | ○ | ● | ● | ● | ● | ○ | ● |
| Exhibition | ● | ● | ○ | ● | ● | **R** | ● | ● | ○ | ○ | **R** |
| Selection Drive | ○ | ○ | ○ | **R** | **R** | ○ | ● | ● | ○ | ○ | ○ |
| Performance | **R** | ● | **R** | ● | ● | ● | **R** | ● | ○ | ○ | ● |
| Ceremony | ● | ○ | ● | ● | ● | ○ | ○ | **R** | **R** | ○ | ● |
| Celebration | ○ | ○ | ○ | ○ | ○ | ○ | ○ | **R** | **R** | ○ | ○ |
| Wedding | ○ | ● | ○ | ● | ○ | ○ | ○ | **R** | **R** | ○ | ○ |
| Fundraiser | ● | ● | ● | ● | ● | ● | ○ | ● | ● | **R** | **R** |
| Camp | ○ | ○ | ○ | ● | ● | **R** | ● | ● | ○ | ● | ● |
| Festival | ● | **R** | ○ | ● | ● | ● | ● | ● | ○ | ○ | **R** |
| Trip | ● | ○ | ○ | ● | **R** | ○ | **R** | ● | ● | ○ | ● |
| Meeting | ○ | ○ | ○ | ● | ○ | ○ | ○ | **R** | **R** | ○ | ○ |

### D — Output, Logistics & Compliance

| Kind | Certificates | Attendance | Prizes | Route & Timing | Slots | Interviews | Waiver | Medical | Age | Travel |
|---|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|
| Hackathon | ● | ● | **R** | ○ | ○ | ○ | ● | ○ | ○ | ● |
| Competition | **R** | ● | **R** | ○ | ● | ○ | ● | ○ | ● | ○ |
| Tournament | ● | ● | **R** | ○ | ● | ○ | **R** | **R** | ● | ● |
| Race | ● | ● | ● | **R** | ○ | ○ | **R** | **R** | **R** | ○ |
| Conference | ● | ● | ○ | ○ | ○ | ○ | ○ | ○ | ○ | ● |
| Workshop | **R** | **R** | ○ | ○ | ● | ○ | ○ | ○ | ○ | ○ |
| Talk | ● | ● | ○ | ○ | ○ | ○ | ○ | ○ | ○ | ○ |
| Training | **R** | **R** | ○ | ○ | ● | ○ | ○ | ○ | ○ | ○ |
| Meetup | ○ | ● | ○ | ○ | ○ | ○ | ○ | ○ | ● | ○ |
| Exhibition | ● | ● | ● | ○ | ● | ○ | ○ | ○ | ○ | ● |
| Selection Drive | ○ | ● | ○ | ○ | **R** | **R** | ○ | ○ | ○ | ○ |
| Performance | ○ | ● | ○ | ○ | ○ | ○ | ○ | ○ | **R** | ○ |
| Ceremony | **R** | **R** | **R** | ○ | ○ | ○ | ○ | ○ | ○ | ● |
| Celebration | ○ | ● | ○ | ○ | ○ | ○ | ○ | ○ | ● | ○ |
| Wedding | ○ | ● | ○ | ○ | ○ | ○ | ○ | ○ | ○ | **R** |
| Fundraiser | ● | ● | ● | ○ | ○ | ○ | ○ | ○ | ○ | ○ |
| Camp | **R** | **R** | ○ | ○ | **R** | ○ | **R** | **R** | ● | ○ |
| Festival | ● | ● | ● | ○ | ○ | ○ | ● | ● | ○ | ● |
| Trip | ● | **R** | ○ | ○ | **R** | ○ | **R** | **R** | ● | **R** |
| Meeting | ○ | **R** | ○ | ○ | ○ | ○ | ○ | ○ | ○ | ○ |

---

## 20. Event Creation UX

> **Ask the most information-dense question first. Never ask what can be derived. Reach Draft in four screens.**

```
SCREEN 1  What are you organising?
          Search box with ALIASES — "ideathon" → Hackathon, "webinar" → Talk·online
          Your templates · You often run · Browse by group
          → Kind selected: preset loads, form reshapes

SCREEN 2  Context          (skipped when the org has one unit)
          Owner unit → branding, permissions, audience defaults, rollup, approval chain

SCREEN 3  Essentials       (fields differ by Kind and Mode)
          Title · Starts · Ends · Mode
          Offline → Venue · Online → Streaming URL
          Hackathon → team size, build window, stage outline
          Race      → distance categories, route
          Wedding   → functions

SCREEN 4  Access
          Visibility · who may join (audience rule) · how they join (RegistrationPolicy)

→ DRAFT + workspace: capability preset shown as editable toggles,
  continuously computed publish checklist
```

| Kind | Tabs that appear | Steps that vanish |
|---|---|---|
| Hackathon | Teams · Stages · Submissions · Scoring · Mentors · Prizes | seat map, lineup |
| Conference | Agenda · Tracks · Speakers · Sponsors · CFP | teams, stages |
| Performance | Lineup · Seating · Merch | submissions, scoring, teams |
| Wedding | Functions · Guest Lists · Travel & Stay · Registry | public discovery, sponsors, paid passes |
| Race | Route · Waves · Timing · Medical | teams, seating, submissions |
| Selection Drive | Eligibility · Applications · Stages · Slots · Offers | passes, sponsors, seating |
| Camp | Slots · Volunteers · Beneficiaries · Walk-in | passes, scoring, seating |
| Festival | Sub-Events · Passes · Schedule Grid · Venues | single-event fields |

The wizard's job is a valid Draft, not a complete event. The workspace renders from each capability's declared `workspace_tab` — it is generated, never hand-written per Kind.

---

## 21. Migration & Phasing

### 21.1 Schema shapes that must exist from the first migration

These cannot be retrofitted into live tables without a multi-quarter project. They must be in the schema even where only the degenerate case is implemented:

1. **`Registration.subject` polymorphic** (Person | Team | OrgUnit) — [B1]
2. **`InventoryPool`** replacing scalar capacity — [B3]
3. **`RegistrationPolicy`** five axes — [B6]
4. **`Money` (amount, currency)** — [B16]
5. **Structural discriminator** admitting Stage and AgendaItem alongside Sub-Event — [B2]
6. **`VAR` lines** on order lines — [B5]

### 21.2 Strangler migration — no big bang

```
1. ADD    kind_slug + capability rows alongside CategoryId/TypeId/AudienceLevelId.
          Backfill kind from the 145→20 alias map.
2. ADD    InventoryPool rows derived from existing scalar capacity (one general pool each).
          Migrate existing tickets to Registration + Admission + Credential.
3. READ   from new fields; keep writing both. Old columns become read-only shadows.
4. VERIFY every existing event renders and behaves identically for one full release.
5. STOP   writing old columns. Retain as legacy_* indefinitely — the only audit trail
          of what an event was originally filed as.
```

### 21.3 Delivery order

| Phase | Contents | Rationale |
|---|---|---|
| **0** | Alias map; freeze new type additions; delete `Random.Shared` analytics fabrication | costs nothing, unblocks everything |
| **1** | Kind + Capability registries; **schema shapes §21.1**; derive category | type stops being inert — the finding that motivated this work |
| **2** | OrgUnit tree · Membership attributes · AudienceRule · ApprovalChain | highest value for the college market; eliminates the most fake types |
| **3** | InventoryPool · concurrency contract · Passes · VAR · refunds | the money path, done once and correctly |
| **4** | Teams · Stages · Fixtures · Scoring | unlocks the competitive Kinds |
| **5** | Sub-Events · Series · Delegated registration · Walk-in | unlocks Festivals, Weddings, contingents |
| **6** | Generated builder · search index · i18n | needs 1–5 to generate from |

Phases 1–3 are independently shippable and independently valuable. Phase 4 must not begin before Phase 3 — Stages consume inventory and produce results that feed prerequisites.

---

## 22. Self-Review

Re-attacking V3 with the same method used against V2.

### 22.1 Blocking items — verification

| Item | Resolved | Where | Verified by |
|---|---|---|---|
| B1 Teams + polymorphic subject | ✅ | §6, §7.2 | party bookings need no group entity; team registration consumes both slot units |
| B2 Stages as a fourth structure | ✅ | §3, §10 | exhaustiveness argument in §3.2; spectator case closed in §10.4 |
| B3 InventoryPool | ✅ | §8 | quota example in §8.1 fully expressible |
| B4 Concurrency contract | ✅ | §17.1 | lock order + conditional decrement + idempotency + reconciliation all specified |
| B5 Refund allocation | ✅ | §9.5–9.6 | one VAR serves reporting and refunds; cannot diverge |
| B6 Five-axis policy | ✅ | §7.1 | all 17 original registration types expressed |
| B7 Delegated registration | ✅ | §7.5 | unassigned admissions + delegate console named as a build item |
| B8 Unified participant model | ✅ | §5 | capabilities reference roles; three models collapsed to one |
| B9 Walk-in | ✅ | §7.6 | offline-capable, idempotent |
| B10 Editions | ✅ | §13.2 | one entity, two modes |
| B11 Channel | ✅ | §8.1, §9.2 | channel-scoped pools + channel on AdmissionRight |
| B12 Approval chains | ✅ | §14.3 | internal chain precedes platform review |
| B13 Capability LOCKED | ✅ | §11.2 | no silent data loss on toggle |
| B14 Five gates | ✅ | §14.2 | each transition enforces only its own preconditions |
| B15 Team vs member eligibility | ✅ | §4.4 | `applies_to` with a stated default |
| B16 Money + currency | ✅ | §9.1 | value type platform-wide; settlement currency immutable after first sale |

**16 of 16 resolved.**

### 22.2 The sixteen edge cases, re-run

| # | Scenario | V2 | V3 |
|---|---|:-:|---|
| 1 | Fest → Hackathon → Final with spectator tickets | BREAK | ✅ §10.4 spectator pool |
| 2 | Day-2 pass, sub-event spanning days 1–3 | BREAK | ✅ §9.2 `window_policy` |
| 3 | Hybrid: 500 in-person + 5,000 virtual | BREAK | ✅ §8.1/§9.2 channel |
| 4 | Cross-college team from 3 colleges | BREAK | ✅ §4.4 `applies_to`; §18 controllership |
| 5 | Inter-company, employees register, company pays | BREAK | ✅ §7.5 SeatBlock |
| 6 | Camp, 80% walk-ins | BREAK | ✅ §7.6 |
| 7 | Wedding guests without accounts | BREAK | ✅ §4.5 identity levels, reconciles D-036 |
| 8 | Sub-event cancelled under an all-access pass | BREAK | ✅ §9.5 VAR |
| 9 | Two buyers, last seat, simultaneous | BREAK | ✅ §17.1 |
| 10 | 64 teams, 8 courts, 3 days | BREAK | ✅ §10.2 Fixture + conflict detection (solver deferred, schema stable) |
| 11 | International: 3 currencies, 3 timezones | DEGRADED | ✅ §9.1 money; §13.2 per-occurrence timezone. Visa letters = a capability, deferred. |
| 12 | Virtual, 50,000 concurrent | DEGRADED | ⚠️ §8.1 virtual channel and pools modelled; **streaming provider integration is deferred** |
| 13 | Government protocol seating | DEGRADED | ✅ §8.1 segments cover priority/protocol |
| 14 | Department owning 200 events deleted | UNDEFINED | ✅ §4.2 archive / merge / tombstone |
| 15 | Judge is also a mentor | UNDEFINED | ✅ §5.5 conflict rules |
| 16 | Offline → Online three days out | UNDEFINED | ✅ §14.5 material change |

**15 of 16 closed. One (12) partially closed** — see §22.4.

### 22.3 New attacks on V3 specifically

Six attempts to break what V3 added:

| Attack | Outcome |
|---|---|
| **Stage in a Sub-Event in a Festival** — does depth ≤ 3 still hold? | ✅ Stages are containment, not composition. Depth counts Sub-Events only. Festival → Hackathon → Stage is composition depth 2. |
| **A team registers for a Festival all-access pass** | ✅ Team registration draws `team_slot` from the fest pool and `person_slot` per member; §8.3 draw timing applies unchanged. |
| **A team member is substituted after the VAR is written** | ✅ Substitution transfers the Admission (§6.5). The VAR is per order line, not per person, so no financial recomputation occurs. |
| **A Stage's advancement rule references a Stage in another event** | ❌ **Prohibited.** Stages are event-local. Cross-event advancement is `RegistrationPrerequisite` (§7.3) into a separate event. Stated as a rule to prevent an accidental cross-event graph. |
| **A Series in EDITIONS mode where one edition is a Festival and another is a Talk** | ✅ Legal. A Series does not constrain Kind. Analytics group by Series *and* Kind; mixed-kind series report per-kind. |
| **Delegated registration into a team event** — a college books 5 team-slots before teams exist | ✅ SeatBlock issues unassigned team-slots; teams form later and bind. Consistent with §7.5 seat semantics. |

### 22.4 Remaining gaps, and why none are blocking

| Gap | Status | Justification |
|---|---|---|
| **Streaming provider integration** | Deferred | Virtual channel, pools, and check-in are modelled. The provider adapter is a capability behind the existing provider-boundary pattern (`Kurx.Application.Abstractions`). No schema change when it lands. |
| **Fixture scheduling solver** | Deferred | Manual scheduling with conflict detection is sufficient to run a tournament. The solver optimises the same schema. |
| **SCIM / HRIS provisioning** | Deferred | `Membership.source = PROVISIONED` exists. Blocks the *enterprise* segment only, not launch. Must be flagged in enterprise sales, not silently omitted. |
| **Academic calendar & accreditation reporting** | Deferred | `academic_year` is a declared analytics dimension (§16), so reports are constructible. The calendar entity is additive. |
| **Subscription billing** for `QUERY`-scoped passes | Deferred | The pass structure supports membership scope; recurring billing is a commerce workstream. Do not sell memberships until it exists. |
| **Visa letters, protocol invitations** | Deferred | Document-generation capabilities over existing data. |
| **Multi-currency settlement on one event** | Out of scope | Deliberate. One event, one settlement currency. Multi-currency *display* is a presentation concern. |

Each is additive against a stable schema, each has a named home in the architecture, and none require reshaping a table that will hold production rows.

### 22.5 Residual risks and their controls

| Risk | Severity | Control |
|---|---|---|
| Capability combinatorics (~40) | High | §11.4 independence rule; `depends_on` shallow DAG; one test per capability plus declared edges; review rejects undeclared coupling |
| Inventory drift | High | §17.1 reconciliation job with alerting; drift is detected, not discovered by a customer |
| Audience rules as an authz surface | High | §18: server-side, deny-default, verified memberships, logged, mandatory security review |
| Public-vote manipulation | Medium | §10.3 identity binding, weight cap, anomaly detection, public audit |
| Over-nesting | Medium | depth ≤ 3 enforced; the builder steers toward Agenda Items and Stages |
| Org-unit trees left empty | Medium | one-node trees cost nothing; seed on institutional onboarding |
| Delegate console scope creep | Medium | a named, bounded third persona — assign, reassign, chase, view. Nothing else. |
| Kind registry versioning | Medium | presets are versioned; changing a preset never mutates existing events |
| EAV rot | Medium | §12.3 promotion rule enforced by lint |

### 22.6 Score

| Area | V2 | V3 |
|---|:-:|:-:|
| Kind & Capability | 9 | 9 |
| Classification & metadata | 9 | 9 |
| OrgUnit & audience rules | 8 | 9 |
| Structural model | 6 | 9 |
| Registration | 4 | 9 |
| Inventory & capacity | 3 | 9 |
| Money & refunds | — | 9 |
| Teams | 3 | 9 |
| Competition | 2 | 8 |
| Participants | 4 | 9 |
| Series & templates | 5 | 8 |
| Lifecycle & approvals | 7 | 9 |
| Concurrency & scale | 2 | 8 |
| Education & company fit | 6.5 | 8 |
| Creation UX | 7 | 8 |
| **Overall** | **6.5** | **8.7** |

Not 10, and the missing 1.3 is honest: streaming integration, SCIM, and subscription billing are real product surfaces that are designed-for but unbuilt, and the fixture solver is a genuine problem deliberately deferred. None of them reshape the schema.

### 22.7 Verdict

> ### "Is this architecture production-ready?"
>
> # **Yes — with three release conditions.**

All sixteen blocking items are resolved. Fifteen of sixteen edge cases close cleanly and the sixteenth is partially closed with a designed-for extension point. Six fresh attacks on V3's own additions produced one new rule (no cross-event Stage advancement) and no structural defects.

**Three conditions, none architectural:**

1. **The §21.1 schema shapes must land in the first migration.** Polymorphic subject, InventoryPool, five-axis policy, Money, structural discriminator, VAR. Everything else is additive; these six are not.
2. **The §17.1 inventory contract must be implemented and its reconciliation job running before the first paid event.** This is the one place where a design defect becomes a financial incident rather than a bug report.
3. **Three as-built defects must be removed in Phase 0, not later** — fabricated analytics, the divergent second analytics path, and the synchronous view-count write. Fabricated numbers become "historical data" nobody will later dare to correct.

**Do not sell** memberships (subscription billing absent), **do not promise** enterprise SSO (SCIM absent), or **large-scale virtual events** (streaming integration absent), until those workstreams ship. They are commitments the architecture supports and the implementation does not yet have.

**Approved for implementation.**

---

## Constitutional Amendments (post-adoption)

§0–§22 above are frozen as adopted on 2026-07-20. Amendments below are ratified as dated `D-NNN` decisions and appended here; they never edit §0–§22 inline.

### CA-1 — Templates are declarative configuration, never a rule engine ([D-132](../DECISIONS.md), 2026-07-22)

Reinforces §2, §11 and §12.3. A constitutional law of this architecture:

1. **Templates remain declarative** — a selection of capabilities and their configuration (values, references, form fields, defaults); nothing more.
2. **Templates never contain imperative logic** — no conditionals, computation, or control flow that evaluates at runtime.
3. **Templates never become a rule engine** — a config schema may not grow constructs that encode branching behaviour.
4. **Business behaviour belongs only inside capabilities/modules** — the ~40 capabilities (§11) are the sole home of logic; a Template configures them, never re-implements them.
5. **Configuration is data, not code** — a value that must decide behaviour conditionally is a capability, not a Template field.

Enforced at review, and by lint where mechanizable (the posture of §12.3).

---

*Design document, adopted as the implementation contract (D-131). The design above specifies no code; it
is delivered by [`V3_IMPLEMENTATION_ROADMAP.md`](V3_IMPLEMENTATION_ROADMAP.md), phase by phase, with each
phase verified and documented before the next begins.*
