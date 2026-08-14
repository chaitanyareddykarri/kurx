# Kurx — Project Handbook

This is the entry point to the Kurx project. It explains what Kurx is, why it exists, how it's built, and where to look for deeper detail. It does not replace any existing document — `docs/DECISIONS.md` remains the single source of truth for product/architecture decisions, and every section below points to the file that carries the full detail.

If something here conflicts with `docs/DECISIONS.md`, the decision log wins; file an update here.

> ⚠️ **Architecture direction (updated 2026-08-04) — read first.** Kurx has **Users**, **Events** and **Representations**. It does **not** have organization accounts, organizer accounts, or **personal organizations**. **Users own events** — the owner is `Event.CreatedBy` and every event authorization checks it first ([D-268](DECISIONS.md)); an organization role is an *additional* grant for staff of a represented institution, never the source of the owner's authority. A **representation** is an attribute of an event affecting only **branding, verification, trust, permissions and payout destination** — never a container events live inside, and never a step on the way to hosting one. The primary relation is always **User → Event**, never Organization → Event and never User → Personal Org → Event.
>
> Canonical flow ([D-305](DECISIONS.md)): **Login → Profile → Create Event → eligibility gate (existing verification reused; then Public or Private) → the creation form → (inside the form) "Representing: Personal / Verified Organization / Request Organization Verification" → Draft → review/approval → User Workspace ▸ Created → Event Host Workspace → publish.** The gate runs *before* the form; Public/Private is a capability decision, not a form field, and maps to `EventProduct`, which the chosen Type still derives (D-266 M1). Choosing an organization is a *field* in the creation flow; it is never required to reach it, and **Personal** — representing yourself — is always available and creates nothing. Registering a not-yet-verified institution is a separate, optional act — a representation request an admin approves ([D-074](DECISIONS.md)/[D-075](DECISIONS.md)) — which never blocks creating an event.
>
> This is **[D-267](DECISIONS.md)**, the current source of truth. The org-first navigation it removed (`Workspace → Organizations → Organization → Events → Create Event`, `Profile → My Organizations`, the routes `/orgs`, `/org/create`, `/org/:id/events/create`, and the "current organization" cookie) no longer exists on any surface. Any section below that says "Create Organization", "Owner/Organizer", or describes an "org → verify → create" journey is **retired history**, not the target.
>
> ⚠️ **Documentation status (numbers re-measured 2026-08-12).** The backend completed a 13-module production re-architecture (**M0–M13 / D-039–D-052**), then the full 18-phase Event Architecture V3 program (all phases complete 2026-07-29), and has run past both since. Every figure below is a measured run, not a documented one — quoting a documented number is what produced each of the errors this banner has had to correct twice now.
>
> | Fact | Measured 2026-08-12 | What this file used to say |
> |---|---|---|
> | Test suite | **1743 total / 1737 passed / 1 skipped / 5 failed** (1 h 21 m, real Postgres) | "1,572 executed / 1,571 passed" |
> | Decisions in `DECISIONS.md` | **277** | "all 38 to date" (§15) |
> | Database tables | **158** | "66+-table schema" (§3) |
> | Admin console modules | **20 of 20 live, none disabled**; 24 console pages, 27 total | "12 live modules" (§8/§9/§10) |
> | API surface | **427 paths / 518 operations** | — |
> | Provider boundaries with real adapters | **6** (SES, SNS, Firebase, ClamAV, KMS, AWS Secrets Manager) + Redis presence | "all mocked" (§3/§9/§13) |
>
> The 5 failing tests are environmental, not defects: 4 `ClamAvUploadPathTests` need a clamd that the compose file only starts under `--profile scanning`, and 1 `NoContentDeclarationTests` is an artifact of the `-p:ArtifactsPath` container test recipe.
>
> **§13 and §15 are current**; the product-narrative prose in earlier sections predates all of this and may lag. Authoritative current sources: [`docs/DECISIONS.md`](DECISIONS.md), [`CHANGELOG.md`](../CHANGELOG.md), [`docs/database/DATABASE_TABLES.md`](database/DATABASE_TABLES.md), [`.claude/memory/trust-verification.md`](../.claude/memory/trust-verification.md), [`docs/architecture/overview.md`](architecture/overview.md), [`docs/api/README.md`](api/README.md), [`docs/roadmap/README.md`](roadmap/README.md).
>
> ⚠️ **Broken-link warning.** This handbook references many `docs/product/*.md` and `docs/planning/*.md` files that **were planned but never created** — `docs/product/` and `docs/planning/` do not exist in this repository. Treat those pointers, and any Access-Model / Educational-Profile / Judging narrative that appears *only* here, as **aspirational product intent**, not shipped behavior, unless it also carries a `D-NNN` in `DECISIONS.md`. Superseded audits and the original re-architecture plan were deleted on 2026-08-08 and live only in git history.

---

## 1. Executive Summary

**What Kurx is.** Kurx is an India-focused, all-purpose event-ticketing platform. One system spans three fundamentally different kinds of gatherings — a college hackathon, a public music festival, and a private wedding — on one underlying Event model, one Organizer model, and one Ticketing/Registration engine. What makes them feel different to organizers and attendees is category, templates, and modules, not separate codebases. (`docs/product/overview.md`)

**Why it exists.** Campus fests, public ticketed events, and private family functions share far more infrastructure — venues, schedules, guest/attendee management, payments, notifications, check-in — than they differ. Building three bespoke systems for these would triple the engineering surface for a shared set of primitives. Kurx's bet is that the differences can be expressed as configuration on top of shared primitives instead. (`docs/product/overview.md`)

**Problems it solves.** Today an organizer running a hackathon, a wedding, and a marathon needs three different tools (or three ad-hoc processes: spreadsheets, WhatsApp groups, a generic ticketing SaaS that doesn't understand Indian payment rails or campus/institution structures). Kurx unifies registration, ticketing, payments (India-first via Razorpay), check-in, and communication (WhatsApp + email) under one identity and one organization model.

**Target users.** Three organizer archetypes (student clubs/colleges running Student Events, public organizers running ticketed/open Public Events, individuals/families running Private Events) and their attendees, who carry a single reusable identity across all three categories rather than a throwaway per-event registration. (`docs/product/attendee-workflow.md`)

**Long-term vision.** A platform where adding a new event type is a data-authoring exercise (a new Template row), not a new code path — supporting eventually cross-institution verification, self-service judge/speaker accounts, org-custom template sharing, a portable "verified student" credential, and mature networking/matchmaking. (`docs/product/event-templates.md`, `docs/product/future-roadmap.md`)

**MVP scope.** The smallest production-ready MVP targets **Public and Private Events end-to-end** — Student Events and the Educational Profile Gate are deliberately deferred, though the `Root Category` field exists on every Event from day one. In scope for MVP: Access Models (Open / Approval Gate / Invitation & Guest List / Access Code), full ticketing (ticket types, forms, free + paid registration, orders, Razorpay, refunds), Team/Group Registration, online check-in + QR, WhatsApp + email notifications, automated payouts, a first admin-panel slice, and basic organizer analytics. Judging/leaderboards/streaming/seating/volunteer & vendor management/merch/donations/most templates are explicitly post-MVP. (`docs/planning/10-mvp-roadmap-risks.md` — not re-verified in this pass; note that certificates and the mobile app have since been built ahead of this original MVP sequencing, per §13 below, so this line may no longer reflect current planning intent.)

---

## 2. Project Goals

| Dimension | Goal |
|---|---|
| **Business** | One platform instead of bespoke per-event-type systems; differences expressed as configuration, not new codebases (`docs/product/overview.md`). |
| **Product** | A category-agnostic hosting workflow — the same 8-step journey (create → represent → publish → registrations → tickets → check-in → certificates → reports) regardless of whether the event is a hackathon, concert, or wedding; only the *modules* attached differ (`docs/product/organizer-workflow.md`). Representation is a step *inside* creation, not a gate in front of it (D-267). |
| **Technical** | Extend the existing layered architecture (Api → Application → Infrastructure → Domain) rather than introduce a second architecture or split into microservices pre-MVP (`docs/planning/01-system-architecture.md`, D-018). Templates are data, not code (`docs/product/event-templates.md`). |
| **User experience** | Attendees get a single reusable identity across categories, not a per-event throwaway account (`docs/product/attendee-workflow.md`). Accessibility target: WCAG 2.1 AA for attendee-facing flows (`docs/planning/09-security-and-nfr.md`). |

---

## 3. Project Scope

**What Kurx does today (built):**
- Auth (WhatsApp OTP + JWT + refresh rotation), mandatory unique-username onboarding, phone-number change with full session revocation, case-insensitive unique email (D-037, D-038). Organizations with a role matrix, org KYC + Razorpay Route + payout schedule seeding (all mocked providers — see below).
- Full event content management: Event CRUD + status workflow, categories/tags, venues, speakers, sponsors, schedule, media, 11 seeded event templates.
- Ticket types + custom registration forms (`TicketType`/`FormField` CRUD, org auth, sold-count inventory guards, public on-sale listing — D-020).
- `OrderService` implements **free/guest and paid checkout** (`backend/Kurx.Infrastructure/Orders/OrderService.cs`): free/guest for individual non-competition tickets (D-036), authenticated group/team registration, competition team invitations, ticket issuance, auto event-chat membership; and **paid checkout (M10/D-049)** — a priced ticket type creates a `Pending` order + gateway order and issues on capture (`POST /v1/webhooks/razorpay` → `ConfirmPaymentAsync` → ticket + Collected ledger + wallet). It drives `MockPaymentGateway`, so no real money moves until the Razorpay adapter ships.
- Check-in: `GateEndpoints.cs`/`IGateEntryService`/`GateEntryService` exist with a real scan/entry service, alongside the `ScanHub` SignalR live feed.
- Public discovery API and organizer dashboard (web).
- Invitations & announcements, event chat (SignalR-backed `ChatHub`).
- Certificate generation: real PDF/PNG rendering via QuestPDF + QRCoder, manual per-event trigger, revocation support (D-035, D-036).
- Flutter mobile attendee app: OTP auth, mandatory username onboarding, event browse/search/detail/related, ticket-type listing (D-019, D-037) — **not an empty scaffold**.
- **158-table schema** (measured 2026-08-12). The "66+" that stood here was the Addendum-02 figure (D-022–D-026) and predates the V3 program, the social/chat/gamification modules and everything after D-038. Full reference: [`docs/database/DATABASE_TABLES.md`](database/DATABASE_TABLES.md).
- Hangfire wired with **18 recurring jobs** (counted live from `hangfire.set` on 2026-08-14; this line said 15) — the original three (seat-hold expiry, waitlist expiry, ledger settlement, D-029) plus inventory / registration / wallet reconciliation, database health probe, leaderboard and search-index refresh, notification cleanup, event reminders, signing-key maintenance, outbox dispatch, chat-room locking, chat-attachment and post-media cleanup, account deletion, and convergence backfill. A Hangfire server runs on every replica, so each carries `[DisableConcurrentExecution]` and a cadence-matched retry (D-243). Hangfire is the only background-work mechanism; the unused n8n container was deleted in D-337.
- `OrganizationWallet` with `WalletService` (balance view, paginated ledger, withdrawal initiation) and org **bank verification** (`org_bank_verifications`, penny-drop / PAN — the old `KycService` was split and renamed in M9/D-048) — endpoints live (D-028).
- `ICertificateRenderer` (real, QuestPDF) / `IQrCodeGenerator` (real, QRCoder) / `IDocumentRasterizer` (stub) provider interfaces.

**What Kurx does not do yet** (current frontier — see [`docs/roadmap/README.md`](roadmap/README.md)):
- **Five provider boundaries are still dev-only** (corrected 2026-08-12 — this line used to say *no* real providers existed): **payments** (`MockPaymentGateway`), **payouts** (`MockRouteClient`), **KYC/DigiLocker** (`MockKycProvider`), **storage** (`LocalDiskStorage`) and the document rasterizer. Real adapters **do** ship for email (SES), SMS (SNS), push (Firebase), malware scanning (ClamAV), signing-key protection (KMS) and secrets (AWS Secrets Manager), all dormant until credentialed. So **no real money moves and no WhatsApp message is delivered**, and uploads are not durable — but the messaging and security seams are real. The boot log names every dev implementation still in play; read that rather than this line. Detail: [`docs/EXTERNAL_SERVICES_AND_PROVIDERS.md`](EXTERNAL_SERVICES_AND_PROVIDERS.md).
- **Partial client parity**: the web organizer dashboard and mobile don't yet wire the new trust/identity/verification/capability/payment endpoints. The `admin/` staff console is live for verification, event approval, users/orgs, blacklist/risk, reports, staff & roles, audit, and analytics (`admin/STATUS.md`); its finance modules wait on real payments. Mobile's per-screen wiring status: `docs/roadmap/README.md`.
- **No automated fraud-signal producers** (the fraud gate + admin endpoints exist; producers feeding them are a follow-up). No paid *group* tickets, no device-token-registration endpoint (push unreachable even once FCM is real). Refunds shipped an HTTP surface in D-199 (full-refund only — see §11).
- **Retired, not pending**: the 4-table `registration_forms/*` system was **dropped in M11/D-050** — the one form system is ticket-scoped `FormField`. The dead `KycEndpoints.cs` was deleted in M9/D-048. Do not re-add either.
- No Maps, OAuth (Google/Apple), analytics, crash reporting, or dedicated full-text search (search is `ILIKE` substring matching today).

**Current MVP** (see §1 and `docs/planning/10-mvp-roadmap-risks.md` for the prioritized build order A→I): payment capture (Razorpay), the Access Model layer, and a first admin-panel slice are the largest remaining gaps — all without Student Events or Educational Profile. (Refunds shipped D-199 — full-refund only, HTTP-reachable.)

**Future roadmap** (`docs/product/future-roadmap.md`, `docs/roadmap/README.md`):
- *Near-term*: finish what's already specified — ticketing, orders, payments, access models, Educational Profile (Student Events), check-in, certificates, seating/volunteer management, and the remaining ~140 event templates.
- *Medium-term*: cross-institution verification partnerships, judge/speaker self-service accounts, org-custom template sharing, a portable "verified student" credential, matured networking/matchmaking.
- *Longer-term, unscoped*: mobile app, admin app maturity, real (non-mock) third-party integrations.

---

## 4. Product Overview

Modules are drawn from the canonical **Module Catalog** in `docs/product/event-system.md`, which is the fixed vocabulary every other product doc must use. Grouped by purpose:

| Module group | Examples | Status |
|---|---|---|
| Core content | Event Profile, Venue, Schedule, Media Gallery, Speakers, Sponsors, Categories & Tags | Built |
| Discovery & access | Public Discovery, Institution-Scoped Discovery, Invitation & Guest List, Access Code, Approval Gate, Educational Profile Gate, Waitlist | Public Discovery built; rest unbuilt/scaffolded |
| Registration & ticketing | Registration Form, Ticketing, Team/Group Registration, Capacity Management, Payments & Orders | Ticketing/Orders/Team Registration built for free/guest tickets (D-020, D-021, D-036); Payments (Razorpay) not built; Registration Form service/endpoints not built despite the table existing (D-024) |
| Engagement & competition | Judging & Scoring, Leaderboard, Live Streaming, Q&A/Polls, Networking | Unbuilt |
| Commerce & sponsorship | Sponsorship Packages, Merchandise, Donations/Fundraising | Unbuilt |
| Operations | Check-in & QR, Seating, Volunteer Management, Vendor Management, Certificates, Feedback, Reports & Analytics, Notifications | Check-in & QR built (`GateEndpoints`/`GateEntryService` + `ScanHub`); Certificates built (real PDF/PNG, D-035); Notifications wired but all senders are console/mock; Seating, Volunteer/Vendor Management, Feedback, Reports & Analytics unbuilt |

Each module's users and dependencies follow from the Event it's attached to via a Template; see §5 for how modules, categories, and templates relate, and `docs/planning/02-feature-inventory.md` for the authoritative build-status table (Built / Scaffolded / Unbuilt, with priority tags).

---

## 5. Event System

> ⚠️ **This section describes the *current shipped* event model, which is being replaced.**
> The frozen target design is [`architecture/EVENT_ARCHITECTURE_V3.md`](architecture/EVENT_ARCHITECTURE_V3.md)
> (adopted, [D-131](DECISIONS.md)), delivered by the 18-phase
> [`architecture/V3_IMPLEMENTATION_ROADMAP.md`](architecture/V3_IMPLEMENTATION_ROADMAP.md).
> Live phase status: [`roadmap/README.md`](roadmap/README.md).
>
> **What changes:** the 145-name type taxonomy (read by no filter, sort, permission check, pricing rule,
> notification, or analytics grouping) is replaced by **20 capability-bearing Kinds** plus ~40 capabilities
> and a 145-entry alias map. Root Category becomes *derived*. Modules become the capability registry.
> The Access model becomes a five-axis `RegistrationPolicy` plus server-side `AudienceRule`. Scalar
> capacity becomes `InventoryPool`. `Order`/`Ticket` gain a
> `Registration` → `Admission` → `Credential` layer, and money gains an explicit currency.
> As of 2026-07-23, **Phases 0–9 have landed** (Waves A–C: guardrails; Kind + capability registries; money/
> currency; OrgUnit tree; audience rules; participants; inventory pools; the registration → admission → credential
> layer; and the **Phase 9 Passes/VAR/§17.1 authority cut-over** — `InventoryPool` and the registration chain are
> now authoritative, with `Order`/`Ticket`/`TicketType.Sold` retained as legacy mirrors). Phase 10 (Teams) is next,
> not started. Live phase status: [`roadmap/README.md`](roadmap/README.md).

Full detail: `docs/product/event-system.md` (canonical), `docs/product/event-templates.md`, `docs/product/business-rules.md`.

**The Event model:**
```
Event
 ├─ Root Category   (Student | Public | Private — fixed at creation, never changes)
 ├─ Template        (defaults a module set)
 ├─ Category/Tags
 ├─ Content         (venue, schedule, media, speakers, sponsors...)
 ├─ Modules         (from the Module Catalog)
 ├─ Access model    (Open / Approval Gate / Invitation & Guest List / Educational Profile Gate)
 └─ Status          (Draft → review states → Published → Closed → Archived)
```
An "event type" (hackathon, wedding, marathon) is not a different data model — it's a Root Category + default Template + default Module set + Access model, all expressed as configuration. This avoids an ever-growing if/else per event type.

**The three root categories:**

| | Student | Public | Private |
|---|---|---|---|
| Purpose | Competitions / campus life | Open general audience | Personal / family functions |
| Discoverability | Student discovery surface, may be institution-scoped | Public by default | Never |
| Educational Profile required | Yes | No | No |
| Typical access | Open / team / institution-restricted | Open / ticketed / approval | Invitation / access-code |
| Monetization | Free / low-fee, occasional paid workshops | Free / ticketed / sponsorship | Rarely ticketed, host-borne |
| Event-type count | 66 across 8 subcategories | 50 across 4 subcategories | 29 across 3 subcategories |

Subcategory detail docs live under `docs/product/{student,public,private}-events/*.md` (e.g., `technology-innovation.md`, `professional-business.md`, `family-celebrations.md`) — read these when working on a specific event type; they are not duplicated here.

**Templates** (`docs/product/event-templates.md`): a Template = Name + exactly one Root Category + Default/Recommended/Optional Modules + Default Access Model + Owner (system-seeded or org-custom). Default Modules can never be removed, only added to. 11 system templates ship today (Conference, Workshop, Hackathon, Wedding, Birthday, Sports Tournament, Concert, Seminar, Meetup, Webinar, Festival); org-custom templates must derive from a system template and cannot change its root category.

**Lifecycle** (category-agnostic, implemented as `EventStatusWorkflow`):
```
Draft --submit_for_review--> PendingReview --claim_review--> UnderReview --approve_review--> Approved
  |            |                    |                             |                              |
  |            |                    --release_review-------------->                              |
  |            --withdraw---------->|         --request_changes--> ChangesRequested --------------|
  |                                           --reject_review----> Rejected                      |
  |                                                                                              v
  --------------publish (Private products only, no review)---------------------------> Published --close--> Closed --archive--> Archived
  unpublish: only allowed pre-registration/sales, returns to a Draft-like state
```

**One reviewer holds an item at a time.** `claim_review` records the holder (`events.review_claimed_by`);
release and every decision clear it, and anyone else is refused with `claimed_by_another_reviewer` (an admin
may override so an offline reviewer cannot strand an item). Each transition is **claimed in SQL** against
the status the request read, so two simultaneous decisions cannot both land — the loser gets
`transition_conflict`. Detail: [`architecture/REVIEW_LIFECYCLE.md`](architecture/REVIEW_LIFECYCLE.md).
`ChangesRequested`/`Rejected` re-enter via `submit_for_review`. Full detail, including the retained legacy
`submit_review`/`reject` aliases: `docs/architecture/REVIEW_LIFECYCLE.md` (D-266 M4).

**Key business rules** (full numbered list in `docs/product/business-rules.md`): Root Category is fixed at creation; Student Events always require an Educational Profile; Private Events are never publicly discoverable regardless of status; Public Events are discoverable by default once Published; Private Events require a guest-management mechanism before publish; certain templates (minors, alcohol, public safety, marathons) require review; team/group events need a size rule before registration opens; waitlist promotion is automatic in registration order; refund policy is set per ticket type and immutable after first sale.

---

## 6. User Types

Full detail: `docs/product/permissions.md`, `docs/planning/03-roles-and-permissions.md`.

**Organization roles (built, D-015)** — identical permission set regardless of event category; only which *actions* are available differs by category:

| Role | Responsibilities / capabilities | Restrictions |
|---|---|---|
| **Owner** | Everything: org KYC/bank, add/remove Managers, delete org | Exactly one Owner required at all times; last Owner can't leave or be demoted |
| **Manager** | Create/edit/publish/archive events of any category; manage modules, templates, registrations, tickets; add/remove Staff/Finance; view all reports | Cannot touch org KYC/bank or delete the org |
| **Staff** | Day-to-day ops on published events: registrations, check-in, certificates; edit assigned event content | Cannot publish new events or change org settings; today scoped org-wide (spec wants event-scoped — a tracked gap) |
| **Finance** | View/export payment, order, and payout reports; manage refunds | No event-content or registration access |

**Roles required by spec but not yet implemented:** Volunteer (single-event, not an org member, scoped check-in access), Ticket Scanner (device/session-scoped Volunteer variant), Team Lead (attendee-side, manages teammates pre-deadline), Judge (no self-service login yet — Staff/Manager enter data on their behalf), Guest/Invitee (Private Events, RSVP not registration, never publicly listed).

**Platform-level:** `KurxAdmin` — a static policy/claim that bypasses org-role checks for moderation and support; already implemented. The full `PlatformRole` set (`SuperAdmin`, `VerificationReviewer`, `FinanceOps`, `Support`, `ReadOnlyAuditor`) is ratified by D-040 and read live per request, never trusted from the token.

**Attendee-side capabilities** (computed per-event from relationship to that event, not a fixed role): Unregistered visitor → Registered/invited attendee → Team lead → Volunteer.

---

## 7. System Workflow

**Event-first user journey (D-074)** — there is no "organizer" role and no create-org step; every person is a User:

```
Login
      ↓
Create Event → fill event details
      ↓
"Who are you representing?"
      ↓
Search the verified organization registry
   ├─ Org exists  → select it → submit proof of authority (membership claim, admin-reviewed)
   └─ Org missing → submit an Organization Verification Request (details + documents)
                    → NOT an Organization, NOT searchable → admin approves →
                      org created + submitter linked as Verified Representative
      ↓
Publish  (free: direct;  paid or pending-org: review/approval gate — D-047)
      ↓
Manage Registrations → Tickets → Check-in → Certificates → Reports  (→ Archived)
```

*(Personal events skip representation entirely — only the creator's own identity is needed.)*
*Legacy note — **superseded 2026-08-12**: the shipped code no longer runs the D-055 path. `EventService.CreateAsync` establishes ownership with `CreatedBy` (D-268) and resolves a self-representation row only to satisfy the non-null `events.OrgId` FK — a persistence detail, never an organization the user owns. Authorization goes through `IEventAuthority` (D-269). Read [`architecture/TERMINOLOGY.md`](architecture/TERMINOLOGY.md), which is canonical when this file disagrees (D-271).*

**Attendee journey** (`docs/product/attendee-workflow.md`):

```
Discover → Register (gate checks) → Join Team → Purchase Ticket → Check-in → Receive Certificate → Profile & History
```

Edge cases and recovery paths for both journeys — duplicate check-in scans, payment timeouts, sold-out races, lost tickets — are catalogued in `docs/planning/04-user-journeys-and-lifecycle.md`; that document is the one to consult when implementing or testing a specific step.

---

## 8. Feature Overview

The authoritative, always-current table is `docs/planning/02-feature-inventory.md` (Built / Scaffolded / Unbuilt, with priority tags P0–P4). Summary by module group:

| Module group | Status | Notable remaining work |
|---|---|---|
| Identity, Trust & Verification | **Built** (M0–M13): auth, platform roles, person identity KYC, org registry + verification, membership claims, capability matrix, fraud | Real KYC/DigiLocker adapter; client UIs |
| Organization | Built (CRUD/roles, org bank verification, wallet) | Event-level Staff scoping |
| Event Content | Built (Phase 2) | ~140 remaining templates (data-authoring, not code) |
| Root Category & Access Model | Partial — Open implicit; event-approval + paid gate built (M8) | Approval Gate, Invitation & Guest List, Access Code, Educational Profile Gate, Waitlist |
| Educational Profile | Unbuilt (deferred) | Tiered verification, institution registry, per-event minimum tier |
| Registration & Ticketing | **Built**: ticket types, free + paid checkout (M10), orders, groups, competition invites, refunds (D-199, full-refund only) | Paid group tickets, capacity edge cases |
| Check-in & On-site | Built (`GateEntryService` + `ScanHub`) | Offline scanning, seating, volunteer/vendor mgmt |
| Engagement & Competition | Unbuilt | Judging, leaderboards, streaming, Q&A, networking |
| Post-event | Partial — certificates built (D-035) | Feedback/surveys, analytics |
| Admin & Platform | Backend built (M12/M13) + admin console live (**20 of 20 modules, none disabled** — `admin/STATUS.md`) | Admin finance modules (wait on real payments), payout automation, remaining real providers, web/mobile parity |

---

## 9. Technical Architecture

Full detail: `.claude/memory/architecture.md`, `docs/architecture/overview.md`, `docs/planning/01-system-architecture.md`, `docs/deployment/README.md`.

**Monorepo layout:**
```
kurx/
├─ backend/   Kurx.Api / Kurx.Application / Kurx.Infrastructure / Kurx.Domain / Kurx.Tests
├─ web/       Next.js — public site + organizer dashboard
├─ admin/     Next.js — staff console (verification, users/orgs, events, blacklist/risk, staff, audit, analytics live; finance pending — admin/STATUS.md)
├─ mobile/    Flutter — attendee app (D-019); broad screen surface, per-screen wiring in docs/roadmap/README.md
└─ infra/     docker-compose for local Postgres + Redis
```

**Backend layering** (strict dependency direction, `Api → Infrastructure → Application ← implemented by Infrastructure`, everything depends on `Domain`):
- `Kurx.Api` — HTTP endpoints, SignalR hubs, middleware, `Program.cs` composition root.
- `Kurx.Application` — interfaces only (`IAuthService`, `IOrgService`, `IStorage`, `IRealtimeBroadcaster`, ...).
- `Kurx.Infrastructure` — EF Core, auth, org/event services, dev providers.
- `Kurx.Domain` — entities/enums, no framework dependencies.

**Frontend**: `web/` is Next.js serving both the public site and the signed-in app (91 routes); `admin/` is a separate Next.js staff console on `:3001` — **20 of 20 sidebar modules live, none disabled**, 24 pages inside the console shell (`admin/STATUS.md`). Note the terminology: web has **no organizer dashboard** (D-267) — `/workspace` is a user's activity hub and event management lives in the Event Host Workspace.

**Database**: Postgres is the system of record; all IDs are `uuid`; money is stored as `long`/`bigint` paise in columns suffixed `_paise` (D-004, D-006). EF Core migrations auto-apply on boot and abort startup on failure.

**Storage**: `IStorage` abstraction; only a local-disk dev implementation exists today; S3 is the intended production provider.

**Authentication**: WhatsApp OTP → JWT (HMAC-SHA256, access token 1h, refresh token 30d, rotated on use; reuse revokes all sessions — D-009, D-014). Web stores tokens in httpOnly `SameSite=Lax` cookies, never localStorage.

**Notifications**: `INotificationService`/`IEmailSender`/`IWhatsAppSender` abstractions exist; only console/mock dev senders are wired.

**Payments**: the paid-checkout write-path is built (M10/D-049) — order → `POST /v1/webhooks/razorpay` capture → ticket + Collected ledger + wallet, gated live on payment-readiness (M8) — but drives `MockPaymentGateway`; a real Razorpay adapter (+ Route payouts) is a gated later phase. Org bank verification uses penny-drop with masked last-4 only (D-016). Hangfire runs the seat-hold / waitlist / ledger-settlement jobs (D-029).

**Deployment**: root `docker-compose.yml` runs Postgres/Redis/API/web/admin with healthcheck-gated startup order; CI (`.github/workflows/ci.yml`) runs backend tests against a real Postgres instance plus web/admin typecheck/lint/build. `.github/workflows/cd.yml` defines a real deploy pipeline (GHCR image push, SSH auto-deploy to staging on push to `main`, manual-approval-gated production deploy on GitHub Release, post-deploy health check + rollback capture) — but no actual staging/production host is provisioned and the required GitHub secrets aren't set, so it cannot deploy anywhere yet.

**Integrations**: **real adapters shipped** — AWS SES (email), AWS SNS (SMS), Firebase (push), ClamAV (malware scanning), AWS KMS (signing-key protection), AWS Secrets Manager, Redis (presence/backplane). **Still mock/dev-only** — Razorpay (payments), Razorpay Route (payouts), a KYC/DigiLocker adapter, S3 (storage is local disk), WhatsApp Cloud API (outbound). Selection is by env flag and **fails closed**: an unrecognised value throws at startup rather than silently falling back.

**Scaling posture**: vertical scaling is sufficient pre-MVP; the API is stateless (JWT) so horizontal scaling is a later, low-effort step; search starts on Postgres full-text search, not a dedicated search engine; SignalR uses an in-memory backplane today with a one-line path to Redis later. No microservice split is recommended pre-MVP (D-018).

---

## 10. Folder Structure

| Path | Purpose | Contents | Notes |
|---|---|---|---|
| `backend/Kurx.Api` | HTTP surface | Controllers, Endpoints, ExceptionHandling, HealthChecks, Hubs, Middleware, `Program.cs` | Composition root; owns the request pipeline |
| `backend/Kurx.Application` | Contracts | `Abstractions/`, `Common/` | Interfaces only — no implementations |
| `backend/Kurx.Infrastructure` | Implementations | Auth, Orgs, Events, Notifications, Orders, Persistence, Migrations, Providers, Ticketing, Users | `Orders/OrderService.cs` is implemented (free/guest checkout, group registration, D-021/D-036); `Providers/` is still all dev/mock/console (D-NNN pending real providers) |
| `backend/Kurx.Domain` | Core model | Entities, Enums | No framework dependencies |
| `backend/Kurx.Tests` | Test suite | Runs against real Postgres `kurx_test`, never mocked persistence | See `.claude/memory/database-conventions.md` |
| `web/` | Public site + organizer dashboard | `app/(app)`, `app/(public)`, `app/onboarding`, `components`, `lib` | Next.js |
| `admin/` | Staff console | `app/(console)`, `app/login`, `components`, `lib` | **20 of 20 modules live**, 24 console pages / 27 total; finance modules wait on real payments — see `admin/STATUS.md` |
| `mobile/` | Mobile app | `lib/core`, `lib/common`, `lib/features/{auth,events,orders,organizer,social,...}` | Working Flutter app — OTP auth plus trusted devices/passkeys/recovery, event browse, orders, event chat with attachments and presence, certificates, gamification, 24 organizer screens. Paid checkout and multi-step event creation are the known gaps (see [`roadmap/README.md`](roadmap/README.md)) |
| `docs/` | All documentation | `DECISIONS.md`, `architecture/`, `api/`, `auth/`, `database/`, `deployment/`, `security/`, `roadmap/`, `ui-ux/`, this handbook | `DECISIONS.md` is the spec (`planning/`/`product/` were never created — see banner) |
| `.claude/` | Claude Code configuration | `index.md`, `memory/`, `agents/`, `commands/`, `workflows/`, `checklists/` | Session rules and persistent working memory, not product spec |
| `infra/` | Local infra | Lighter alternate `docker-compose.yml` (Postgres + Redis only) | Compare against root `docker-compose.yml` |

---

## 11. Business Rules

Canonical list: `docs/product/business-rules.md` (19 numbered rules) and `docs/product/educational-profile.md`. Highlights by area:

- **Registration**: Approval Gate registrations are pending and don't count against capacity until approved. Educational Profile Gate blocks confirmation for Student Events.
- **Capacity**: hard caps per event/session/ticket type, enforced as a check-and-increment inside the same transaction as order creation.
- **Tickets**: default per-user limit of 5 across all orders for a ticket type; refund policy is set per ticket type at creation and is immutable after the first sale; ticketing of any kind requires org KYC, free registration does not.
- **Groups**: one Order → one Group → many GroupMember rows, each optionally linked to a Ticket; a team-size rule (min/max) is required before publish whenever Team Registration is enabled.
- **Refunds**: full-refund only today — `RefundOrderAsync` derives the amount from the immutable VAR (D-103), not a caller-chosen partial amount; `Refund.AmountPaise` records what was refunded. HTTP-reachable since D-199 (`POST`/`GET /v1/orders/{id}/refund`, `GET /v1/refunds`, `GET /v1/admin/refunds`). A Paid Order is never hard-deleted, preserving the original sale record the refund policy depends on.
- **Certificates**: sourced from the Educational Profile (Student) or the registration record (Public); if attendance/participation criteria aren't met, the certificate is absent-with-reason, never silently missing.
- **Permissions/visibility**: Private Events are never publicly discoverable regardless of status; Public Events are discoverable by default; Draft/Unlisted/Private events return 404 (never 403) to non-members, so their existence isn't leaked.
- **Access models**: Educational Profile fields like DOB and student ID are never shown raw to organizers — only computed eligibility flags.

---

## 12. User Journeys

Full step-by-step detail with edge cases and recovery paths: `docs/planning/04-user-journeys-and-lifecycle.md`.

- **Guest** — an unregistered visitor browsing public discovery; for Private Events, never sees the event at all.
- **User/Attendee** — registers (subject to the event's access model), optionally joins a team, purchases a ticket, checks in, receives a certificate, and retains history against one reusable identity.
- **Organizer** (Owner/Manager) — creates an org, completes KYC, builds an event from a Template, publishes, manages registrations/tickets, runs check-in, issues certificates, and reviews reports.
- **Staff** — operates day-to-day on already-published events (registrations, check-in, certificates) without publish or org-settings access.
- **Admin (KurxAdmin)** — platform-level moderation and support, bypassing org-role checks; not a substitute for Owner/Manager in normal operation.
- **Platform Admin** — same as KurxAdmin; the term used in this catalogue for the static admin policy already implemented in the backend.

---

## 13. Current Development Status

Source of truth: [`docs/roadmap/README.md`](roadmap/README.md), [`CHANGELOG.md`](../CHANGELOG.md), [`.claude/memory/trust-verification.md`](../.claude/memory/trust-verification.md), [`docs/DECISIONS.md`](DECISIONS.md).

**Completed — foundation & content:**
- Auth & Organizations (WhatsApp OTP, JWT + rotating refresh with reuse-revocation, Org CRUD/roles, org bank verification + Route seeding — providers still mocked).
- Identity hardening: mandatory unique-username onboarding, phone-number change with full session revocation, case-insensitive unique email, 30-day reclaim-hold (D-037, D-038).
- Phase 1 foundation hardening (secret validation, RFC7807 errors, FluentValidation, JWT claim safety, EF auto-migrate fail-closed, health checks, structured logging, SignalR, CI).
- Phase 2 Event Management (CRUD + status workflow, categories/tags/venues/speakers/sponsors/schedule/media, 11 templates, public discovery, organizer dashboard).
- Ticketing (`TicketType`/`FormField` CRUD, inventory guards, on-sale listing, D-020); check-in (`GateEntryService` + `ScanHub`); certificates (real QuestPDF/QRCoder, revocation, D-035).
- Two-product registration: account-optional Events vs. account-mandatory Competitions on one Order/Ticket/Group engine (D-036).
- Flutter attendee app: OTP auth, username onboarding, event browse/search/detail (D-019, D-037).

**Completed — production re-architecture (M0–M13 / D-039–D-052; 166/166 tests *at that time* — see the status note at the top for the current figure):**
- **Trust & verification (M0/M3/M4/M5/M6/M7)**: polymorphic verification substrate; person identity KYC (masked last-4 only, `/v1/me/identity`); typed/canonical org registry + fuzzy search; org verification lifecycle; evidence-backed membership claims; a **live capability matrix** (`ITrustService`) gating paid organizing / payouts / representation.
- **Platform authority (M1/M2)**: platform roles read live per request; the old `IsKurxAdmin` boolean dropped; profile is authority-zero.
- **Event approval + payments (M8/M10)**: paid events can't self-publish (reviewer-gated); **paid checkout write-path is built** — order → `POST /v1/webhooks/razorpay` capture → ticket + Collected ledger + wallet, live-gated. Drives `MockPaymentGateway`.
- **KYC term split (M9)**, **forms convergence (M11, `registration_forms/*` dropped)**, **admin verification console backend (M12)**, **fraud prevention (M13)**: `blacklist_entries` + `fraud_signals` cascading into the paid gates.

**Not wired yet (the frontier):**
- **The remaining mock providers** — Razorpay (+ Route), DigiLocker/KYC, S3 and WhatsApp Cloud (outbound). **SES, SNS, Firebase, ClamAV, KMS and Secrets Manager are real** as of 2026-08-12; this line previously listed SES and FCM among the mocks and was wrong. No real money moves and uploads are not durable. The paid *write-path* exists; the payment *adapter* does not. See [`docs/EXTERNAL_SERVICES_AND_PROVIDERS.md`](EXTERNAL_SERVICES_AND_PROVIDERS.md).
- **Client parity** — partial, and **no longer as this line once claimed**. `admin/` is **not a placeholder**: all 20 console modules are live (that statement contradicted §8 of this same file). Mobile is **not attendee-read-only**: it carries **95 screens, 24 of them organizer** screens. What genuinely remains is per-screen wiring of some newer identity/verification/capability/payment endpoints — tracked per screen in [`roadmap/README.md`](roadmap/README.md).
- **Automated fraud-signal producers**, **paid group tickets**, **device-token registration endpoint** — all follow-ups. (Refunds shipped D-199.)

**Deferred beyond MVP:** Student Events / Educational Profile, judging/leaderboard/streaming/Q&A/networking, seating/volunteer/vendor management, merch/donations/sponsorship, the remaining ~140 event templates, coupons/tax, dedicated full-text search (search is `ILIKE` substring today).

---

## 14. Development Philosophy

From `.claude/CLAUDE.md` and reinforced throughout the planning docs:

- **`docs/DECISIONS.md` is the spec.** No other document overrides it; every ambiguous product/architecture call gets a new `D-NNN` entry before code is built on top of it.
- **Read before you build.** This repo has a documented history of unfinished scaffolding (see D-018, and the `IOrderService`/`Orders/` gap in §13). Finish what exists; don't start a parallel implementation.
- **Real persistence in tests.** Tests run against a genuine Postgres `kurx_test` database, never mocked persistence.
- **Close the loop.** Every change goes plan → implement → build → test → fix → verify → document; nothing ships on "should work."
- **No drive-by scope.** No unrequested refactors, speculative abstractions, or dependency changes.
- **Templates over code branches.** A new event type should mean authoring a Template row, not shipping a new code path (`docs/product/event-templates.md`).
- **One architecture, not two.** Extend the existing layered backend; no new top-level service or microservice split pre-MVP (D-018, `docs/planning/01-system-architecture.md`).
- **Security by default.** OWASP Top 10 applies to every change; auth/payment/PII changes always run a security review before merge.

---

## 15. Important Decisions

Full text: `docs/DECISIONS.md`, which now holds **277 decisions** (measured 2026-08-12). The index below covers only **D-001–D-052** — it was written when those were all that existed and has never been extended; it is kept as an orientation aid for the foundational decisions, **not** as a complete list. For anything after D-052, read `DECISIONS.md` directly. Each entry describes what was true *at the time it was written*; later decisions may supersede earlier ones without rewriting them, per the append-only convention, and `docs/DECISIONS.md` always wins if this index and that file disagree:

| ID | Summary |
|---|---|
| D-001 | Sudo-free local toolchain — no Docker sudo access; native Postgres/Redis for dev. |
| D-002 | Repo directory renamed to `~/kurx`. |
| D-003 | Local Postgres cluster config: port 5432, user/pass `kurx`, databases `kurx`/`kurx_test`. |
| D-004 | Money stored as `long` paise in C#, `bigint` in Postgres, columns suffixed `_paise`. |
| D-005 | .NET built-in rate limiter; OTP limits also enforced against Postgres. |
| D-006 | All entity IDs are `uuid` (Guid v4). |
| D-007 | Payout tier defaults: T1 (₹50k cap, 5-day delay, 12%/7-day reserve), T2 (75% advance, 8% reserve), T3 (100%, instant, 2% reserve). |
| D-008 | OTP/refresh tokens hashed SHA-256; dev console sender prints raw OTP. |
| D-009 | JWT HMAC-SHA256; access token 1h, refresh token 30d, rotated on use. |
| D-010 | Slugs auto-generated, uniquified, immutable after publish. |
| D-011 | ImageSharp pinned to 3.1.12 (4.0 requires a commercial license). |
| D-012 | First-login provisioning via empty Name + `needs_onboarding`; OTP policy; 10-digit phones get `91` prefix. Superseded by D-037 (Username is now also mandatory) and by D-089 (phone identity is E.164; length no longer implies India). |
| D-013 | EF Core pinned to 8.0.11 to match the Npgsql provider at the time — the solution now runs EF Core 10 / Npgsql 10.0.2; no `D-NNN` was logged for that upgrade. |
| D-014 | Refresh-token reuse revokes all active refresh tokens for that user. |
| D-015 | Org role matrix (Owner/Manager/Staff/Finance) defined; last-Owner protection; org slugs immutable. |
| D-016 | KYC via penny-drop approval activates Razorpay Route; masked last4 only persisted; payout schedule seeded at org creation. |
| D-017 | Phase 1 hardening details (secret checks, RFC7807, FluentValidation, `KurxAdmin` policy, SignalR auth, health checks). |
| D-018 | Phase 2 completes the pre-existing `EventService` in place rather than duplicating it; new status enum; Owner/Manager + `KurxAdmin` authorization; draft visibility returns 404 not 403; trending uses real view counts. |
| D-019 | Phase 10 Flutter mobile app — scope (attendee-only), stack (Riverpod/GoRouter/Dio/Freezed/Hive), and the verified backend contract it consumes. |
| D-020 | Phase 3 ticket-type pricing, inventory, group, and form-field rules. |
| D-021 | Disposition of the unwired Order/Group/Payment scaffolding — finish `OrderService` in place rather than build a parallel system (later implemented under D-036). |
| D-022 | Username audit trail — `username_history` (30-day reclaim hold) kept separate from `username_change_log` (full change history). |
| D-023 | `design_templates.EventId` for event-scoped certificate templates. |
| D-024 | Registration forms as a 4-table system decoupled from ticket-type form fields. |
| D-025 | Soft deletes via `DeletedAt` nullable timestamp + partial indexes. |
| D-026 | Location columns on `events` (`Country`/`State`/`District`/`PostalCode`) for India-scale discovery and GST. |
| D-027 | Org invitations — GitHub-style flow with a time-limited token. |
| D-028 | Organization wallet — cached balance, never `SUM` on the hot path. |
| D-029 | Hangfire wired with 3 recurring jobs (seat-hold expiry, waitlist expiry, ledger settlement). |
| D-030 | Org soft-deletion policy. |
| D-031 | Wallet withdrawal concurrency — `SELECT FOR UPDATE` + pending deduction. |
| D-032 | Removal of the Flutter mobile mock data layer. |
| D-033 | Event taxonomy seeding + slug uniqueness strategy. |
| D-034 | Rate limiter ran before authentication — every request was bucketed as anonymous; fixed by reordering middleware. |
| D-035 | Real certificate rendering — QuestPDF-only, system templates, manual per-event trigger. |
| D-036 | Two-product registration model — guest checkout for Events, mandatory accounts for Competitions, sharing one Order/Ticket/Group engine. |
| D-037 | Mandatory username onboarding (supersedes D-012's Name-only check) + authenticated phone number change. |
| D-038 | Case-insensitive email uniqueness (functional index) + phone-change now revokes all other active sessions. |
| D-039 | Trust & verification substrate — polymorphic `verification_documents` / `verification_reviews` (re-architecture M0). |
| D-040 | Platform roles read live per request via `platform_roles` + `PlatformRoleClaimsTransformation` (M2). |
| D-041 | Identity/account authority boundary — dropped `IsKurxAdmin`; profile is authority-zero (M1). |
| D-042 | Person identity verification — `user_identity_verifications`, masked last-4 only (M3). |
| D-043 | Organization registry — typed, canonical, alias/domain-aware, fuzzy dedup (M4). |
| D-044 | Organization verification lifecycle (Unverified→PendingReview→Verified/Rejected/Suspended/Blacklisted) (M5). |
| D-045 | Membership-affiliation verification — `membership_claims` (M6). |
| D-046 | Trust capability matrix — L0–L5 labels over live capability flags (M7). |
| D-047 | Event approval + live payment gate — paid events can't self-publish (M8). |
| D-048 | KYC term split — `org_bank_verifications` vs person identity; dead `KycEndpoints` deleted (M9). |
| D-049 | Paid checkout + ledger write-path — capture webhook → ticket + Collected ledger + wallet (M10). |
| D-050 | Forms convergence — one `FormField` system; `registration_forms/*` dropped; supersedes D-024 (M11). |
| D-051 | Admin verification console (backend) — merge/blacklist/history, VerificationReviewer-gated (M12). |
| D-052 | Fraud prevention — `blacklist_entries` + `fraud_signals`, live fraud-clear gate (M13). |

---

## 16. Terminology

| Term | Definition |
|---|---|
| **Organization** | A **verified institution** in the registry that an event can be run on behalf of. Not an account and has no "owner" — users *represent* it. Holds payout/KYC details and an internal role matrix (see *Verified Representative*). |
| **Organization Verification Request** | A user's *pending* submission to register a not-yet-verified institution (details + documents). It is **not** an `Organization`, never appears in the registry or search, and becomes an `Organization` only on admin approval (D-074). |
| **Verified Representative** | A User whose authority to act for an `Organization` was granted by **admin approval** of an evidence-backed membership claim. Replaces the retired "Owner"/"Organizer" account concept (D-074). |
| **Organizer / Owner** *(retired)* | Legacy terms from the D-055 org-first model. No "organizer" or "owner" account exists in the D-074 target — every person is a **User** who may *represent* organizations. |
| **Access Model** | One of **four** values: Open, Approval Gate, Invitation & Guest List, or Educational Profile Gate. (This row said "six" while listing four; corrected 2026-08-12.) Superseded in the V3 model by the five-axis `RegistrationPolicy` plus server-side `AudienceRule` — see [`architecture/EVENT_ARCHITECTURE_V3.md`](architecture/EVENT_ARCHITECTURE_V3.md) §7. |
| **Registration Form** | Custom field collection at signup, implemented as `FormField`. |
| **Ticket Type** | The entity carrying price, pricing unit, registration mode, group min/max, quantity, sold count, sale window, and per-user limit. |
| **Educational Profile** | The profile every Student Event attendee must hold — institution, program, year, student ID, DOB, plus optional email/skills/clubs/alumni flag. |
| **Invitation** | The named-guest invite mechanism for Private Events — the private-event analog of public registration. |
| **Group Registration** | Multiple attendees registering as one unit (a hackathon team, a family invite group). |
| **Check-in** | On-site or virtual verification via a scannable ticket/QR code. |
| **Verification** (Educational Profile) | Tiered: Unverified → Domain-verified → Document-verified. |
| **Settlement / Payout** | Org payout tiers T1/T2/T3, each with a cap, advance percentage, and reserve percentage, stored in `payout_schedules`. |
| **Certificate** | Auto-generated participation/winner certificate issued post-event. |
| **Template** | A named, reusable configuration of the Module Catalog plus sensible defaults, scoped to exactly one Root Category. |
| **Event Lifecycle** | `Draft → PendingReview → UnderReview → Approved → Published → Closed → Archived`, category-agnostic (D-266 M4). |
| **Root Category** | Student, Public, or Private — chosen once at event creation, never changeable afterward. |
| **Module Catalog** | The fixed vocabulary of features every product-spec document must use when describing an event type. |
| **KurxAdmin** | The platform-level claim/policy that bypasses org-role checks for moderation. |

---

## 17. Reading Order

**Rewritten 2026-08-12.** The previous list sent a newcomer to eleven documents, **eight of which do not
exist** — every `docs/product/*` and `docs/planning/*` entry. Every file below was verified present on that
date.

1. **[`architecture/TERMINOLOGY.md`](architecture/TERMINOLOGY.md)** — read this *first*, before any prose in
   this handbook. It is the canonical vocabulary (D-271) and it **wins over this file** wherever they
   disagree. It is also the shortest thing here.
2. [`DECISIONS.md`](DECISIONS.md) — the spec, and the only one. 277 entries; skim the recent ones, then
   search rather than read end to end.
3. [`architecture/overview.md`](architecture/overview.md) + [`.claude/memory/architecture.md`](../.claude/memory/architecture.md)
   — how the backend is actually laid out.
4. **[`architecture/EVENT_ARCHITECTURE_V3.md`](architecture/EVENT_ARCHITECTURE_V3.md)** — **required before
   any event-system work.** The frozen design (D-131); all 18 phases complete 2026-07-29. Read §0 for what
   it replaced, because §4–§7 of *this* handbook still describe the older model.
   [`V3_IMPLEMENTATION_ROADMAP.md`](architecture/V3_IMPLEMENTATION_ROADMAP.md) beside it for the sequence and
   the §12 Client Integration Backlog.
5. [`roadmap/README.md`](roadmap/README.md) — **the canonical build-status authority.** What is built and
   what is not.
6. [`architecture/event-creation.md`](architecture/event-creation.md),
   [`architecture/CAPABILITY_ENGINE.md`](architecture/CAPABILITY_ENGINE.md) and
   [`architecture/REVIEW_LIFECYCLE.md`](architecture/REVIEW_LIFECYCLE.md) — the three flows most likely to be
   your first task.
7. [`api/README.md`](api/README.md) — the contract (427 paths / 518 operations), plus the camelCase-in /
   snake_case-out rule that has bitten more than one client.
8. [`database/DATABASE_TABLES.md`](database/DATABASE_TABLES.md) — all 158 tables.
9. [`security/overview.md`](security/overview.md) — security posture and the invariants that must not be
   broken.
10. [`EXTERNAL_SERVICES_AND_PROVIDERS.md`](EXTERNAL_SERVICES_AND_PROVIDERS.md) — which provider boundaries
    are real and which are still mocks. Read before assuming anything is delivered or paid for.
11. [`CHANGELOG.md`](../CHANGELOG.md) — implementation history; `DECISIONS.md` carries the *why*.

For day-to-day engineering work also read [`.claude/index.md`](../.claude/index.md), which maps agent
personas, slash commands, and workflows to the task at hand, and
[`deployment/README.md`](deployment/README.md) before touching infrastructure.

Sections 4, 5, 7, 11 and 12 of this handbook describe **product intent that predates V3** and cite files
that were never created. Where they conflict with items 1, 4 or 5 above, those win.
