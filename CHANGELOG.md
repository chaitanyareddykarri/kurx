# Changelog — Kurx Production Re-Architecture

Running log of the module-by-module production re-architecture. Its design docs
(`KURX_REARCHITECTURE_MASTER_PLAN.md`, `KURX_MODULE_DESIGN_DOSSIER.md`) were deleted
on 2026-08-08 once every module shipped; recover them from git history if needed.
Entries below are dated and append-only, so older ones still cite `docs/history/`
paths — that is provenance, not a live pointer.

**Architecture Decision Log (ADR):** formal decisions are recorded as `D-NNN`
entries in `docs/DECISIONS.md` (the repo's canonical decision log). This file
tracks the *implementation* history; `docs/DECISIONS.md` tracks the *why*.

Format: newest first. Each module entry mirrors the 8-part completion report
(Implementation Summary · Files Changed · Database · API · Docs · Breaking
Changes · Verification · Remaining Work).

---

## [Unreleased]

### A team ticket is not sellable at the gate or in a seat block (2026-08-17) - D-369

**Implementation Summary.** D-366 made `TicketType.PricePaise` a derived value — the cheapest band — so
that `IsPaidEventAsync`, the paid/free filter and "from ₹250" keep working without learning what a band
is. Every charged price resolves in `OrderService.AmountFor` via `TierPriceAsync`. **Two services never
went through it**: `WalkInService.CreateAsync` and `SeatBlockService.CreateAsync` each build an
`OrderItem` straight off the column, so a banded team ticket was admitted at the *cheapest* band —
₹250 at the gate for a team of five whose band says ₹400, and the same shortfall invoiced to the
organisation under a `DEFERRED` seat block.

The money was the symptom. Neither channel can express a team at all: a walk-in mints one ticket with no
`GroupSize`, no display name and no roster; a seat block mints N unassigned admissions a delegate binds to
N individual people. Both were producing a team of nobody, correctly priced or not.

**Both channels now refuse a `RegistrationMode.Group` ticket type** (`group_ticket_not_supported`),
checked before the pool draw and before any mutation. Refused rather than repriced, on the reasoning
D-366 already settled when it chose `ambiguous_price_rule` over "take the first band" — charging an
amount nobody decided is worse than refusing. The guard is on `Group` rather than on "has bands" so the
unbanded sibling (a phantom team with an empty roster) closes with it.

**Files Changed.** Backend: `Kurx.Infrastructure/Events/{WalkInService,SeatBlockService}.cs` (one guard
each), `docs/DECISIONS.md` (D-369). Tests: `Kurx.Tests/DelegatedRegistrationTests.cs` (+1 —
`Neither_a_walk_in_nor_a_seat_block_can_sell_a_team_ticket`, asserting the refusal *and* that no order,
no seat block and no pool consumption survives it).

**Database.** None. No schema change, no migration, no data change — a read-side guard only.

**API.** `POST /v1/events/{eventId}/walk-ins` and `POST /v1/events/{eventId}/seat-blocks` gain one
refusal code, `group_ticket_not_supported`.

**Breaking Changes.** None shipped. Neither endpoint has a caller in `web/`, `admin/`, `mobile/` or
`packages/ui` — they are backend-only Phase 13 surfaces, which is also why no error copy is added
(`invalid_ticket_type` and `no_walkin_pool` have none either). Copy arrives with the delegate console.

**Verification.** `dotnet build` clean. Full-suite run pending the compose stack.

**Remaining Work.** Nothing already sold is revisited — this stops a wrong charge being created, it does
not find the ones that were. No banded team ticket has been sold through either channel in the dev data.

### `teams` is enforced by the domain, not only described (2026-08-16) - D-367

**Implementation Summary.** D-266 M2 drew a hard line — the Capability Engine *describes* what an event
supports and never decides anything — and `teams` turned out to be the one capability that cannot live on
the descriptive side of it. A `RegistrationMode.Group` ticket creates a `TeamPolicy`, a roster, a join
code, a `PerGroup` inventory unit and a D-366 price band set; on an archetype whose `teams` capability is
Unsupported, all of that is real, persisted, chargeable state describing something the event cannot run.

Both clients already refused to offer it — web reverts `participation` in an effect, Flutter on rebuild —
which is **two implementations of one rule, neither of which is the one that matters**. `POST
.../ticket-types` with `registrationMode: "Group"` was accepted whatever the archetype said, so the
invariant held only while every client kept its half of the bargain.

**Three enforcement points, all reading the existing engine:**

| Boundary | Rule | Refusal |
|---|---|---|
| `TicketTypeService.CreateAsync` | may not create a `Group` ticket | `teams_not_supported` (400) |
| `TicketTypeService.UpdateAsync` | may not *become* `Group` | `teams_not_supported` (400) |
| `EventService.ApplyUpdateAsync` | Type may not change to a non-team archetype while a `Group` ticket exists | `type_conflicts_with_team_ticket` (409) |

No second capability system: an existing event is read through `ICapabilityService.GetForEventAsync`, an
incoming Type through `GetForArchetypeAsync`, and both interpret the result through one
`CapabilitySet.Supports` — because "`Locked` means unsupported" is a two-line rule and two copies of it
drift. The persisted `archetype_capability_defaults` matrix stays the authority, so an admin enabling
`teams` for an archetype makes team events legal there **with no code change** — asserted by a test that
flips the cell and watches the answer change.

**Two decisions inside it that are not obvious.** An event with **no archetype** cannot create a team
ticket: `CapabilityResolver.StateOf` returns Unsupported for a null archetype deliberately, and
enforcement inherits that rather than special-casing it open. And an **existing** `Group` ticket stays
editable even where the matrix would refuse it today — enforcing on every update would strand rows that
were legal when written, leaving an organiser unable to fix or reprice a ticket that may already have
sold. The invariant is "no NEW invalid state", not "punish old state".

**The Type change is refused, never converted.** Converting would delete a `TeamPolicy`, its roster rules
and its D-366 price bands as a side effect of a dropdown. The check runs **before a single field is
assigned**, so a refusal leaves nothing staged — not even the new archetype.

**Files Changed.** `Kurx.Infrastructure/Events/TicketTypeService.cs` (both points + `SupportsTeamsAsync`),
`Kurx.Infrastructure/Events/EventService.cs` (Type-change guard + `ArchetypeSupportsTeamsAsync`), new
`Kurx.Infrastructure/Events/CapabilitySet.cs`, `Kurx.Api/Endpoints/EventEndpoints.cs` (409 mapping),
`packages/ui/src/problem-copy.ts`, `mobile/lib/core/network/api_error.dart`, `docs/DECISIONS.md` (D-367),
`docs/api/README.md`, `.claude/memory/backend-conventions.md`. Tests: new
`TeamCapabilityEnforcementTests.cs` (14); fixtures corrected in `TeamSizePricingTests.cs` and
`OrderTests.cs`.

**Test fixtures that were creating an illegal shape.** Both suites built **archetype-less** events and
gave them team registrations — exactly what D-367 refuses. They now carry a `competitive` Type, which is
what a real team event has; the rule was not weakened to accommodate them. That is the invariant doing its
job on first contact with existing code.


### A team's price depends on its size (2026-08-16) - D-366

**Implementation Summary.** D-357 settled what a team price MEANS — charged once for the whole team,
one inventory slot, never multiplied by the roster. What it could not express is the shape organisers
actually use: `2 → ₹250, 3 → ₹300, 4–5 → ₹400`. A `TicketType` carries one `PricePaise` for its whole
`GroupMin..GroupMax` range, and a min/max range describes *eligibility* — overloading it to mean a price
curve would be a lie in the schema.

**The model: a child table, `ticket_price_tiers`** (`TicketTypeId`, `MinSize`, `MaxSize`, `PricePaise`,
both ends inclusive). One ticket type, one inventory pool — which is exactly why a child table beats one
ticket type per size: `Quantity` stays "how many teams may enter" for the event as a whole, where four
ticket types would have been four independent pools with nowhere for "maximum 100 teams" to live.

Resolution happens in **one function** — `OrderService.AmountFor`, already the single authority on what a
registration costs. No band covering the team size refuses the sale (`no_price_for_team_size`); two
covering it is a configuration error (`ambiguous_price_rule`, 409) and never "pick the first", because
charging one of two prices the organiser wrote means the attendee pays an amount nobody decided.

**Overlap is impossible in the database, not just checked in code:**
`EXCLUDE USING gist ("TicketTypeId" WITH =, int4range("MinSize","MaxSize",'[]') WITH &&)`. Two concurrent
updates can each read a clean set and each pass an application check; they cannot both commit past this.

**The headline price is derived, never trusted.** With bands, `TicketType.PricePaise` becomes the cheapest
band, so `IsPaidEventAsync`, the paid/free discovery filter, price sorting and "from ₹250" all keep
working without learning what a band is. A client sending ₹9,999 alongside bands starting at ₹250 gets
₹250 — verified live.

**Backward compatible by construction.** No bands → priced by `PricePaise`, exactly as before. No data
migration, no backfill, no reinterpretation of an existing paid event. Bands are refused on anything but
`RegistrationMode.Group`, and a free event has no bands at all rather than ₹0 rules.

**Mobile can now author a team registration, not just display one.** Until this change the Flutter
wizard sent `'pricingUnit': 'PerTicket', 'registrationMode': 'Individual'` as **literals**, so an
organiser on a phone produced an individual-entry event whatever the archetype allowed — bands were
visible on mobile and impossible to create there. It now has the same Registration step as web, in the
same place (after Type, because the Type carries the archetype and the archetype's `teams` capability is
the only thing that may decide whether team entry is offered): participation, team size, the band editor,
and capacity labelled in the unit it counts. `_Step.pricing` is gone from the Flutter wizard with it, so
neither client has a Pricing step any more (D-365).

Two supporting gaps closed on the way: the taxonomy DTO dropped `archetype_slug`, so the app had no way
to ask the capability engine anything; and there was no capabilities call at all —
`archetypeSupportsTeamsProvider` asks `GET /v1/archetypes/{slug}/capabilities` and **fails closed**, so an
unreadable answer offers individual entry rather than a team option the server may refuse.

**Files Changed.** Backend: new `Kurx.Domain/Entities` `TicketPriceTier`,
`Kurx.Infrastructure/Persistence/KurxDbContext.cs`, `Kurx.Infrastructure/Events/TicketTypeService.cs`
(validation as a SET: coverage, overlap, range, price > 0), `Kurx.Infrastructure/Orders/OrderService.cs`
(`TierPriceAsync`, `AmountFor`, `OrderItem.UnitPricePaise`), `Kurx.Application/Abstractions/ITicketTypeService.cs`,
`Kurx.Api/Endpoints/{TicketTypeEndpoints,OrderEndpoints}.cs`, migration `AddTicketPriceTiers`.
Clients: `web/lib/{event-wizard,event-actions,api}.ts`, `web/components/host/create-event-wizard.tsx`
(band editor), `web/components/events/{event-detail-sections,booking-form}.tsx`, `web/app/e/[slug]/page.tsx`,
`admin/{lib/api.ts,components/admin/review-dossier.tsx}`, `packages/ui/src/problem-copy.ts`,
`mobile/lib/features/events/{domain/entities/ticket_type,data/models/ticket_type_dto}.dart`,
`mobile/lib/features/events/presentation/widgets/ticket_type_tile.dart`,
`mobile/lib/core/network/api_error.dart`,
`mobile/lib/features/organizer/presentation/pages/create_event_page.dart` (Registration step),
`mobile/lib/features/organizer/domain/event_wizard_payload.dart`,
`mobile/lib/features/organizer/presentation/providers/organizer_providers.dart`,
`mobile/lib/features/events/{domain/entities/event_category,data/models/event_category_dto}.dart`.
Tests: new `TeamSizePricingTests.cs` (15); updated `web/test/{event-wizard,event-detail-sections,event-creation}`,
`mobile/test/features/events/ticket_pricing_unit_test.dart` and
`mobile/test/features/organizer/event_wizard_validation_test.dart` (the same band cases as web, case for case).

**Database.** One migration, `20260816082701_AddTicketPriceTiers` — a new table, an FK cascading from
`ticket_types`, two check constraints, and the exclusion constraint above (requires `btree_gist`, a
standard contrib module, because the constraint mixes uuid equality with range overlap). **No existing row
is touched.**

**Verification.** Full backend suite **1912: 1911 pass, 1 skip, 0 fail** (46m41s, SDK container) — up from
1897. Web 646 pass/1 skip, admin 40, Flutter **498** + analyze clean, contract-check 0 errors, `openapi.json`
regenerated (442 schemas).

**Proved live** against the running stack over HTTP, one ticket type, three teams:

```
team of 2  groupSize=2  charged Rs.250   |  stored: 2|25000|qty 1|unit 25000
team of 3  groupSize=3  charged Rs.300   |  stored: 3|30000|qty 1|unit 30000
team of 5  groupSize=5  charged Rs.400   |  stored: 5|40000|qty 1|unit 40000
```

A team of 3 paid ₹300, not ₹900. The headline sent as ₹9,999 stored as ₹250. Artifacts removed afterwards
under a guard that refuses to delete an event carrying a captured order.


### Free/Paid is asked once, not twice (2026-08-16) - D-365

**Implementation Summary.** The organiser answered "is this event free or paid?" at the create-event
gate, and then again at step 3 of the wizard the gate opens. D-343 needs the gate's answer — the
product+pricing pair selects the verification tier the caller must clear before the form exists — and
D-357 pinned the wizard step at index 2 to sit near that dependency. Neither noticed that a second
control does not merely repeat the question, it can **change the answer after the decision was made on
it**: the gate refuses Paid unless the caller is `canHostPaid` **and** representing a verified
organization, while the wizard's `canChoosePaid` checked only the first. A host the gate had refused
could re-select Paid inside the form and spend eleven steps on an event the server rejects at submit.

The Pricing step is deleted (web: 12 steps → 11, plus Authorization), `pricing` is now a read-only value
carried from the gate — a `const` on web, written only in `initState` on mobile, with no setter anywhere
— and Registration *states* the inherited mode ("Paid event — chosen during setup") instead of asking
for it. The two paid-eligibility checks moved to Registration, the first step where money is typed and
the step after Representing is chosen. Mobile's step outlived web's by one change — its ticket fields
still lived there — and went with them when the Registration step landed (D-366); its Free/Paid selector
is gone either way.

No new state was introduced: `EventService.IsPaidEventAsync` still derives paid-ness from
`AnyAsync(t => t.PricePaise > 0)`, so the ticket price remains the single source of truth.

**Files Changed.** `web/components/host/create-event-wizard.tsx` (step table, read-only pricing, step
body removed), `web/test/event-creation.test.tsx` (renumbered; the "asks only free or paid" case
replaced by one asserting it is never asked again),
`mobile/lib/features/organizer/presentation/pages/create_event_page.dart`,
`docs/DECISIONS.md` (D-365), `docs/architecture/event-creation.md`.

**Verification.** Web 631 pass/1 skip, Flutter 479 + analyze clean, typecheck clean. No backend file
touched — the 1897 backend suite result stands.


### Event lifecycle — approval separated from publication, and deletion made non-destructive (2026-08-16) - D-362, D-363, D-364

**Implementation Summary.** Three defects on one axis: what an event's status *permits*, and what it
*destroys*.

*D-362* — the reviewer gate on `publish` sat inside `if (isPaid)`. That was wrong in both directions: a
**free** public event could be submitted for review and then published by its own creator, live and
unapproved, out of the queue while a reviewer might be holding it (reproduced over HTTP:
`submit_for_review` → 200, `publish` as creator → 200 `published`); and a **paid** event reached the same
gate from `Approved`, so a creator whose event a reviewer had already approved still could not publish
it. One gate keyed on the *target* state replaces both. Separately, `EventExposure` — the file whose own
docstring calls it "the one rule for public exposure" — tested Product + Visibility + not-deleted and
said nothing about status; nothing leaked only because its single public-surface consumer wrote its own
status check beside the call.

*D-363* — `Published (with orders) → unpublish → Draft → delete` was open. `DeleteDraftAsync` checked
only `Status == Draft`, `unpublish` returns a Published event to Draft, and delete was a hard
`db.Events.Remove` with **46 tables cascading from `events`**. Found live: the dev database held a Draft
event with an order already on it. `PROJECT_HANDBOOK.md` had documented the correct rule ("unpublish:
only allowed pre-registration/sales") for as long as the hole existed.

*D-363 §1/§2/§4* — `Approved` had no exit but forward, and approval bound to nothing. `withdraw` now
also runs `Approved → Draft` and `cancel` accepts `Approved`, so an organiser who changes their mind can
rework the event or abandon it instead of holding a state with one door. And `IsEditLocked` covered the
review states only, which meant an **approved event was fully editable with no re-review** — approved,
then re-titled, re-dated, re-venued, and published as something no reviewer had seen. A material edit
now returns it to `PendingReview`, clears the reviewer's claim and audits the reopen; cosmetic edits stay
free. The edit is applied rather than refused: refusing would leave "cancel and start again" as the only
way to correct an approved event.

Three of the fields §4 names are not on the event row: **ticket price** lives on `ticket_types`,
**eligibility** on `audience_rules` and the **authorization letter** on `event_authorizations`, each
behind its own endpoint and service, so the trigger on the event's PATCH could see none of them. An
organiser approved on a free event could raise the ticket to ₹5,000, change who may attend, or swap the
letter, and publish it themselves. All four services now call one shared reopen (`EventReviewReopen`),
and the ticket-type and audience services also gained the `event_under_review` (409) freeze they lacked —
a reviewer could otherwise approve a price that had already been replaced underneath them.

*D-364* — D-025 ratified soft deletes for `events`. The column existed, `EventExposure` filtered on it,
and **nothing ever wrote it**. The admin console's dialog even read "Soft delete — removed from every
list but not purged", the decision's own words attached to a button doing the opposite.

**Client sweep against the workflow table (same day).** Reading every client's status/action map against
`EventStatusWorkflow` and `EventStatus` — rather than against the docs — turned up six more defects, all
client-side:

1. **Mobile printed the raw enum.** `event.status.toUpperCase()` on the status page, `Opens after
   approval · ${event.status}` on the workspace hub — the *exact* sentence web's `event-status.ts` was
   written to kill — and the raw value again in the manage-screen badge. Mobile had no status label map
   at all. Added `core/utils/event_status.dart` with web's labels word for word, used in all three.
2. **Two phantom error codes on mobile.** The status screen mapped `approval_required` and
   `no_ticket_types`; **no backend file emits either**. The codes that do arrive (`approval_pending`,
   `no_pass`) fell through to "Something went wrong."
3. **Five V3 §14.2 gate refusals had copy nowhere** — `no_pass`, `no_inventory_pool`, `no_currency`,
   `no_staff_assigned`, `results_not_published`. Every one is fixable in a minute by the organiser, and
   all three surfaces showed "Something went wrong. Please try again." Added to `PROBLEM_COPY` and
   mobile's map.
4. **`Scheduled` was unreachable from every UI.** Both clients render a full action list for that status,
   and neither offered `schedule`, the only action that produces it. Now offered from `Approved`.
5. **Mobile omitted `cancel` from Published/Scheduled/Live and `complete` from Published** — the action
   that obliges refunds (D-101) was on web but not on the phone.
6. **The admin console had no way back from `Approved`.** D-363 §1 exists because "a reviewer who
   approves in error cannot take it back", and the console still offered only Publish. Added `withdraw`.

`web/test/error-copy.test.ts` gained three cases that pin **both** label maps against the backend
`EventStatus` enum, so a status added there now fails a test instead of reaching a user as
`CHANGESREQUESTED`.

**Three follow-ups from that sweep, fixed after it.**

*The rail drew terminal states as "Draft".* `statusIndex` sends anything unrecognised to step 0 — correct
for an *unknown* status, since guessing forward would tell someone their event is published — but
`rejected`, `cancelled` and `archived` are known statuses that were never given an arm and inherited it.
A cancelled event was drawn as if it were back at the beginning. Those three now replace the rail with
`EventStatusPage.offRailNote`, a sentence saying what actually happened and what remains; every status
that belongs on the rail still gets it, and the unknown fallback is unchanged. Three test cases pin the
split.

*`archive` from Draft existed in the workflow table and in no host UI* — only the admin console offered
it. Added to web and mobile, beside (not instead of) Delete: archive files a draft away, delete removes
it.

*Mobile could create a draft and never get rid of it.* It had delete calls for speakers, sponsors,
sessions, ticket types and media, and none for the event — so a draft started on a phone stayed in that
person's list forever, while web and the console could both delete it. Added
`EventManageRemoteDataSource.deleteEvent` and a confirmed Delete draft action that names what goes with
it. Its refusals (`event_has_history`, `not_draft`) already had copy on that screen.

Web's delete surfaced none of that: `deleteDraftEventAction` threw, and a thrown server action reaches
the client as an opaque digest in production, so the component could only say "That draft could not be
deleted." It now returns the refusal — which is the point of `event_has_history` naming cancel and close
as the doors that are open.

**Files Changed.** Backend: `Kurx.Infrastructure/Events/{EventService,EventStatusWorkflow,TicketTypeService,SeriesService}.cs`,
new `Kurx.Infrastructure/Events/EventReviewReopen.cs`, `Kurx.Infrastructure/Events/EventAuthorizationService.cs`,
`Kurx.Infrastructure/Audience/AudienceService.cs`,
`Kurx.Api/Endpoints/{TicketTypeEndpoints,AudienceEndpoints}.cs` (409 for `event_under_review`),
`Kurx.Infrastructure/{Search/SearchIndexService,Posts/PostService}.cs`,
`Kurx.Infrastructure/Persistence/KurxDbContext.cs`, `Kurx.Domain/Entities/EventExposure.cs`. Clients:
`web/lib/workspace.ts`, `packages/ui/src/problem-copy.ts`,
`mobile/lib/features/{organizer/presentation/pages/event_status_page,workspace/presentation/pages/workspace_hub_page}.dart`,
`admin/components/admin/events-workspace/event-workspace-sheet.tsx`,
`web/components/host/{event-status-actions,edit-event-form}.tsx`,
`web/app/(app)/host/events/[id]/tickets/page.tsx`, `mobile/lib/core/network/api_error.dart`,
new `mobile/lib/core/utils/event_status.dart`,
`mobile/lib/features/{organizer/presentation/pages/event_manage_detail_page,workspace/presentation/pages/workspace_hub_page}.dart`,
`mobile/lib/features/organizer/data/datasources/org_remote_data_source.dart`,
`admin/components/admin/review-actions.tsx`, `packages/ui/src/problem-copy.ts`,
`web/lib/event-actions.ts`. Tests: new
`LifecycleVisibilityTests.cs` (13), `LifecycleDeletionGuardTests.cs` (9),
`ApprovedEventLifecycleTests.cs` (7), `mobile/test/features/workspace/lifecycle_visibility_test.dart`
(6); updated `EventExposureTests.cs`, `AdminEventManagementTests.cs`, `web/test/workspace-rules.test.ts`,
`web/test/event-creation.test.tsx`, `web/test/error-copy.test.ts`.

**Database.** One migration, `20260815203711_EventSoftDeleteFilter` — index-only, no data change. The
unique indexes on `events.Slug` and `events.ShortCode` became partial (`WHERE "DeletedAt" IS NULL`),
because a retained row keeps its slug and a plain unique index would make recreating a deleted event's
title collide on a row nobody can see. Reads are filtered by an EF global query filter on `Event` rather
than by 169 hand-written clauses.

**API.** No new endpoints. New refusal `event_has_history` (`unpublish`, `DELETE` event);
`tickets_already_sold` extended to any `order_item` reference; `reviewer_required` now reachable on
`publish` from `PendingReview`/`UnderReview`; `paid_event_requires_review` narrowed to `Draft` only
(it previously listed reviewed states and omitted `Scheduled`, dead-ending
`Approved → schedule → open_registration`). `DELETE` of an event is now soft. Contract check: 0 errors,
no drift.

**Docs.** `docs/DECISIONS.md` (D-362, D-363, D-364); `docs/api/README.md`;
`docs/architecture/{REVIEW_LIFECYCLE,diagrams}.md`; `docs/PROJECT_HANDBOOK.md` (lifecycle diagram +
rules); `.claude/memory/{database-conventions,testing-standards}.md`; `.claude/CLAUDE.md` §9 baseline;
`.claude/FUTURE-IMPROVEMENTS.md` (two stale claims — web/admin now have suites; contract tests shipped).

**Breaking Changes.** None on existing data: nothing had ever set `DeletedAt`, so the new query filter
hides no existing row. Behavioural changes are all refusals of paths that should never have worked.

**Verification.** Full backend suite **1897: 1896 pass, 1 skip, 0 fail** (35m56s, SDK container, clamd
up) — up from 1831, then 1880 at the end of the §3/D-364 work. Web 630 pass/1 skip, admin 40, Flutter 479
+ analyze clean, contract-check 0 errors, and the committed `openapi.json` verified byte-identical to
what the running API serves (no DTO changed). `web/test/error-copy.test.ts` caught the mobile copy for
`event_under_review` drifting from `PROBLEM_COPY` by a clause — which is exactly the drift that test
exists to catch, and it was found before the words reached anyone.
Live acceptance against the dev stack over real HTTP, every "not public" assertion made anonymously: all
six lifecycle tests green, including `Approved` invisible on the slug route and all six discovery feeds,
and the creator publishing their own approved event. Artifacts cleaned up under a guard that refuses if
any order is attached.

**Remaining Work.** D-363 is now implemented in full. Three known edges, none a hole: the re-review
predicate tests *presence in the payload* rather than a changed value, so a client posting the whole
record re-reviews on every save (web's `EditEventForm` does, and now warns before you press Save); the
client action tables that offer withdraw/cancel are UI lists with no test of their own — the server's
transition table is the authority and refuses anything they get wrong with `invalid_transition`; and
`EmergencyUpdateAsync` (Super Admin, D-191) still bypasses both the edit lock and the reopen, which is
what an emergency edit is for and is audited as one.

### Documentation accuracy pass — the contract regenerated, and every count made re-derivable (2026-08-15) - D-361

**Implementation Summary.** A full read of `docs/` (53 files, 35.5k lines) against the running system.
The findings clustered on one kind of claim: **rules and vocabulary had survived; counts had rotted**.
Fifteen stale figures were corrected and each now carries the command that re-derives it or defers to
the single live authority (D-361). One finding was CI-breaking: `docs/api/openapi.json` was missing
`requires_representation` on `TrustCapabilities` (added by D-353), so the D-259 drift gate would have
failed on `main`. Two findings were user-facing traps rather than cosmetics —
`EXTERNAL_SERVICES_AND_PROVIDERS.md` documented `PUSH_PROVIDER=fcm`, a value that makes the API refuse
to boot (the switch takes `firebase`), and it plus `PRODUCTION_PROVIDERS_CHECKLIST.md` both still said
`EMAIL_PROVIDER=ses` throws at startup, untrue since D-284 shipped `SesEmailSender`.

**Files Changed.** `docs/api/openapi.json` (regenerated); `docs/architecture/{overview,diagrams,
PLATFORM_FLOW_MAP}.md`; `docs/roadmap/README.md`; `docs/EXTERNAL_SERVICES_AND_PROVIDERS.md`;
`docs/deployment/PRODUCTION_PROVIDERS_CHECKLIST.md`; `docs/PROJECT_HANDBOOK.md`;
`docs/auth/AUTHENTICATION_TESTING.md`; `docs/ui-ux/{inventory-web,inventory-mobile,regression-criteria,
HANDOFF,do-not-change}.md`; `docs/DECISIONS.md` (collision note + D-361); `mobile/README.md`;
`UI_REDESIGN_PROGRESS.{md,json}`; `CHANGELOG.md`. One code file, comment-only:
`backend/Kurx.Infrastructure/DependencyInjection.cs` — a provider comment that read "only the dev
implementations exist so far" sat directly above the `case "ses"` disproving it.

**Database.** No change.

**API.** No behaviour change. `docs/api/openapi.json` regenerated from the running API: **441 paths /
535 operations / 440 schemas**, one real diff (`TrustCapabilities.requires_representation`).

**Docs.** The substantive corrections: DbSets 70→**162** and migrations 19→**91** (both asserted stale
in two files); recurring jobs 15→**18**; endpoint files 70→**72**; entity files 14→**42**; web routes
88/91→**93**; Flutter pages 91→**95**; response-schema coverage "only 15 of ~424"→**496 of 535** (that
project had shipped under D-246/D-313); real provider adapters "two"→**six**. `inventory-web.md` had
listed its own 88 pages *twice* — once as the inventory, once under "Non-visual route handlers (no UI —
excluded from redesign scope)" — so every web page was simultaneously in scope and excluded from it;
the duplicate is removed and the table rebuilt from the filesystem (9 dead rows dropped,
`/chats/[eventId]`→`/chats/[roomId]`, 13 untracked pages added with **no fabricated status**).
`diagrams.md`'s event-lifecycle state diagram predated Phase 14 and was missing four states
(`Scheduled`, `Live`, `Completed`, `Cancelled`) and five transitions. A three-collision note
(`D-105`/`D-114`/`D-299`) was added to the `DECISIONS.md` header.

**Breaking Changes.** None.

**Verification.** `dotnet build Kurx.sln -c Debug -warnaserror` in the SDK container: **Build succeeded,
0 Warning(s), 0 Error(s)** (5 m 21 s). `scripts/openapi-response-check.mjs`: 496/535 declared, 0 new,
0 stale. `scripts/contract-check.mjs`: 0 errors, no contract drift. Inventories diffed against the
filesystem: web **93 = 93**, mobile **95 = 95**, admin **27 = 27**, zero missing and zero dead rows.
Relative links resolve across all edited files, and every source path cited in the two rebuilt
inventories exists. Suites were not re-run — no product code changed.

**Remaining Work.** `D-350`–`D-360` are a concurrent workstream's (create-event wizard, team pricing)
and their CHANGELOG entries are owed by that session, not invented here. The three genuine `D-NNN`
collisions are documented rather than renumbered — the log is append-only and 100+ citations point at
them. `architecture/diagrams.md` was audited only for the lifecycle diagram and the ownership ER
(both now correct); its remaining sequence diagrams were not line-checked against code.

### A dormant crypto leak, nine seeders outside the migration lock, and 52 phantom IDE errors (2026-08-15) - D-344, D-345

**Implementation Summary.** Four findings from an external read of `Program.cs` were checked against the
running system; **two were real, two were not**, and the severity of one real finding was reported wrongly.

**The ECDsa leak (D-344) is real but dormant.** The ES256 `IssuerSigningKeyResolver` called
`ECDsa.Create()` inline and never disposed it - the only un-disposed `ECDsa.Create()` in the repository,
where `JwksEndpoints`, `DeviceSignatures`, `SigningKeyService` and `TokenService` all use `using`. But it
was reported as firing on every authenticated request, and it fires on none: all six issuance sites call
the synchronous HS256 `CreateAccessToken`, and `CreateAccessTokenAsync` has zero callers. Verified against
the running API rather than by reading - a live token's header is `{"alg":"HS256","typ":"JWT"}` with no
`kid`. Fixed anyway: the defect would go live with the ES256 cut-over, on the hottest path in the app, in a
change whose reviewers will be reading issuance rather than validation.

**Nine seeders ran outside any lock (D-344).** EF Core 9 locks `MigrateAsync`; that lock ends with the
migration and the read-then-insert seeders sit outside it, so two replicas of a rolling deploy both find a
catalog slug missing, both insert, and the loser takes a `23505` inside the startup gate - a boot crash.
One `pg_advisory_xact_lock` around the whole block, chosen over making nine seeders individually
conflict-tolerant so later seeders are covered too.

**52 phantom IDE errors (D-345).** VS Code showed 52 "type or namespace not found" errors in `Program.cs`
- Serilog, Hangfire, even `IServiceCollection` - while the container build was green. NuGet writes absolute
paths into `obj/project.assets.json`, and the host and SDK container share this tree over a bind mount, so
a container restore stamped `/root/.nuget/packages/` onto the host. Whoever built last won; two repairs
were undone within nine and thirty-four minutes respectively.

**Files Changed.** `Kurx.Api/Program.cs` (resolver + seed lock), new `Kurx.Api/Auth/ValidationKeyCache.cs`,
`Kurx.Tests/SigningKeyTests.cs` (+1 test), `backend/Directory.Build.props`, `.gitignore`.

**Database.** None. **API.** None - no route, DTO or `openapi.json` change.

**Docs.** `docs/DECISIONS.md` D-344/D-345; `architecture/overview.md` (startup gate, CORS ordering);
`auth/AUTHENTICATION_ARCHITECTURE.md` (the ES256 cut-over is six call sites, not four);
`.claude/memory/{backend-conventions,security-rules,database-conventions,deployment,testing-standards}.md`.

**Breaking Changes.** None.

**Verification.** Full suite in the SDK container. The new signing-key test mints ES256 directly, because
no login path produces such a token - it is currently the only coverage the `kid` branch has, and it closes
a real gap: the existing compromise test asserts at the service layer and would pass even if the resolver
served a stale cached key. The lock's safety rests on advisory locks being per-database, which was measured
rather than assumed. The bare `dotnet restore` that caused the IDE recurrence now writes outside the mount
and leaves the host's assets byte-identical.

**Rejected, with reasons recorded.** `UseCors()` after `UseExceptionHandler()` is correct - a minimal app
reproducing Kurx's exact order returns CORS headers on a 500, because `CorsMiddleware` registers a
`Response.OnStarting` callback that `Response.Clear()` does not remove. `jobs.Trigger("data-backfill")` on
boot is deliberate and guarded. `UseArtifactsOutput=true` was implemented and measured for D-345, then
reverted: it moves intermediates to `backend/artifacts/`, still inside the bind mount.

### Event cloning kept 47 of 110 columns; coupons and the phone migration are recorded, not fixed (2026-08-14) — D-340, D-341, D-342

**Implementation Summary.** `EventService.CloneAsync` built the copy from a hand-written property list
naming 47 of `Event`'s 110 columns. The other 56 were never mentioned, so they landed on their C#
defaults — every column D-265 and D-266 added after that list was written. Verified against the running
API, not inferred: an event created through `POST /v1/events` with all six D-265 input groups populated,
cloned over HTTP, then diffed in Postgres — **36 columns differed, 6 legitimately, 30 silently lost.**
Including `RequiresConsent` true→false, `GenderRestriction` Female→Any and `TaxInclusive` false→true.

The fix inverts the default: the clone is a `ShallowCopy()` of the source with 27 named resets (fresh
identity, runtime state, admin moderation per D-186, review outcomes per D-266 M4/M7, series lineage per
V3 §3.4). A column added later is inherited unless someone deliberately excludes it.

**Files Changed.** `Kurx.Domain/Entities/Events.cs` (+`Event.ShallowCopy()`),
`Kurx.Infrastructure/Events/EventService.cs` (`CloneAsync` inverted),
`Kurx.Tests/EventCloneTests.cs` (new, 3 tests).

**Database.** None. **API.** None — same DTO, correct values; `openapi.json` unaffected.

**Docs.** `docs/DECISIONS.md` D-340/D-341/D-342. Alongside it, a documentation-drift sweep corrected
figures that had gone stale against the code: API surface 427/518 → **441 paths / 535 operations** and
response coverage 480/518 → **496/535** (`docs/api/README.md`, `docs/architecture/overview.md`); tables
158 → **162**, with the seven undocumented ones named (`docs/database/DATABASE_TABLES.md`); the test
baseline 1572/1743/1297 → **1820** across `README.md`, `docs/architecture/overview.md`,
`docs/PROJECT_HANDBOOK.md` and `docs/roadmap/README.md`; the removed `--profile scanning` recipe
(D-339) in `.claude/CLAUDE.md`, `.claude/memory/{testing-standards,security-rules}.md`; the root
README's claim that SES and FCM were still mocked (six adapters are real); `web/README.md`'s reference
to `/host/organizations/*`, which no longer exists; `docs/architecture/providers.md`'s scanner coverage
(2 paths → 8, D-338); and the repo's one broken internal link.

**Breaking Changes.** None in shape. A clone now inherits its source's registration policy, fee/tax
configuration and eligibility limits where it previously reset them to defaults — the intended fix, but a
real behavioural change for anyone who had adapted to the old copies.

**Verification.** SDK container, `-warnaserror` clean. `EventCloneTests` 3/3 green, and confirmed
**red-on-purpose**: reintroducing three drops failed the sweep with the exact columns named
(`RefundPolicy`, `MinAge`, `TaxInclusive`). Full suite re-run recorded below.

**Remaining Work.** D-341 — coupons ship CRUD + `/quote` with no order-path consumer and no client field;
left in place and recorded rather than wired or deleted. D-342 — nine phone lookups still read the legacy
`users."Phone"` column while `AuthIdentifiers` reads both; no live symptom, deliberately not widened.

### Database integrity + observability: a state the schema refuses, and drift that pages (2026-08-12) — D-328

**Implementation Summary.** Two phases kept deliberately apart. **DB-8** puts the *vocabulary* of four
high-value state columns into the schema — `orders."Status"`, `tickets."State"`, `events."Status"`,
`ledger_entries."State"` — derived from their enums so the two cannot drift. **DB-9** makes three things
observable that were not: PostgreSQL deadlocks and bloat (neither of which RDS publishes for PostgreSQL —
the CloudWatch `Deadlocks` metric is *Aurora*-only), and reconciliation drift, which until now produced a
log line at `Error` that nothing consumed. `WalletReconciliationJob`'s own comment said drift *"should page
rather than sit in a dashboard nobody reads"*; nothing paged. That is the D-240 shape — ledger correct,
cached balance wrong, no request failing.

**Files Changed.** New: `Telemetry/DatabaseTelemetry.cs` (two meters — `Kurx.Database`,
`Kurx.Reconciliation`); `Jobs/DatabaseHealthProbeJob.cs`; `infra/terraform/monitoring_database.tf`;
`docs/deployment/DATABASE_RUNBOOKS.md`; tests `DatabaseHealthProbeTests.cs`,
`ReconciliationTelemetryTests.cs`. Modified: the three reconciliation jobs (outcome recording +
`try`/`catch`), `TelemetryRegistration.cs` (two meters registered), `DependencyInjection.cs` + `Program.cs`
(probe job, 5-min recurring), `infra/terraform/{data_stores,variables}.tf`.

**Database.** One migration, `AddStateVocabularyCheckConstraints` — four `CHECK` constraints, no other
change. Existing values were queried on the live database first: all four tables hold **zero rows**, and
`events."Status"`'s only retired member (`InReview`) had already been rewritten by
`MigrateInReviewToPendingReview` on 2026-08-04. Pre-existing constraint count 23 → 27. ~80 other status
columns were deliberately **not** constrained.

**API.** No change. No endpoint, DTO, contract or authorization change in either phase; `openapi.json`
unaffected.

**Breaking Changes.** None. The constraints admit exactly the values the enums can express, so no code path
that compiles can violate one.

**Docs.** `D-328`; `docs/roadmap/README.md`; new `DATABASE_RUNBOOKS.md` (six runbooks + a deployment
checklist); `docs/deployment/README.md` pointer; `.claude/memory/database-conventions.md` (the derived-CHECK
convention) and `.claude/memory/observability.md` — the latter corrected as well as extended: it claimed
OpenTelemetry was "not wired up yet" and that there was "no metrics backend", both untrue since D-100.

**Verification.** Backend Release `-warnaserror` build clean (0 warnings / 0 errors). Full suite in the SDK
container against real Postgres 17.10: **1743 total / 1737 passed / 1 skipped / 5 failed**, 1 h 21 m —
against the 2026-08-11 baseline of 1709 / 1697 / 1 / **11**. Failures are down from 11 to 5 and every one
that remains is environmental: 4 × `ClamAvUploadPathTests` (`scan_unavailable` — no clamd; the compose
service is behind the opt-in `--profile scanning`, confirmed absent from `docker ps`) and 1 ×
`NoContentDeclarationTests` (`Could not locate the endpoints directory from /tmp/artifacts/…` — the
documented `-p:ArtifactsPath` runner artifact). **The 3 `EventAudienceAuthorizationTests` that the baseline
recorded as failing on committed `HEAD` now pass.** DB-8/DB-9 contributed **33 tests across 3 classes**
(`StateVocabularyConstraintTests` 13, `ReconciliationTelemetryTests` 11, `DatabaseHealthProbeTests` 9), all
discovered and all passing; none appears in the failure list.

Infrastructure: `terraform fmt` clean and `terraform validate` **Success** against the real AWS provider
schema (hashicorp/aws v5.100.0) — the same bar D-104a set, and the *only* bar available without an account.
Both probe statements were additionally executed by hand against the live PostgreSQL 17.10, which is how two
defects were caught that compiled and reviewed cleanly: `EXTRACT(EPOCH …)/3600.0` returns `numeric` on PG
14+, so `GetDouble` would have thrown `InvalidCastException` on every probe of a vacuumed table; and a bare
`count(*)` over `pg_stat_activity` counts the checkpointer, walwriter, background writer and both launchers
(measured 15 rows, 10 of them client backends), none of which are charged against `max_connections`.
`DatabaseHealthProbeTests` now executes the job end to end so neither can return silently.

**Remaining Work.** Everything in `infra/terraform/` is **CONFIGURED ONLY** — this Terraform has never been
applied, so no alarm has fired and `pg_stat_statements` is enabled nowhere. Enabling it additionally needs
an instance **reboot** (`shared_preload_libraries` is a static parameter) and a `CREATE EXTENSION` Terraform
cannot issue. **Neither SNS topic has a subscriber**, which makes every alarm currently equivalent to no
alarm. Replication-lag monitoring is **DEFERRED** — there is no read replica, and an alarm on an empty
dimension would read as coverage while providing none. Autovacuum tuning for the three high-churn tables is
explicitly *not* done: this phase measures, and tuning needs its own decision once there is data.

### Auth hardening: unplaceable phones refused, disposable email measured (2026-08-10) — D-317

**Implementation Summary.** Two signup-hardening phases. **Phase 1:** `NormalizePhone` validated with
libphonenumber but did not *reject* — on failure it returned the input's bare digits, which then satisfied
the 8–16 length gate, so `+911111111111` was issued a one-time code for a destination that cannot exist.
Nothing was insecure (the code went nowhere; no account exists until verify consumes one) but it was a dead
end presented as a sent message. `RequestOtpAsync` now refuses with the existing `invalid_phone` before
minting anything. **Phase 2:** `FraudSignalKind.DisposableContact` had no producer; a verified address on a
configured throwaway domain now records exactly one signal at score 10 against a threshold of 100 — it
blocks nothing, and feeds the gate that already exists.

The premise that libphonenumber had been "deliberately avoided" was wrong: it is a dependency and is called
on both branches. The comment that reads like a refusal is on `ToOtpDestination`, and is D-290's guard
against re-normalizing a stored bare-digit phone. It is untouched.

**Files Changed.** `AuthService.cs` (+19/−0, the guard); new `DisposableEmailPolicy.cs`;
`RegistrationService.cs` (signal hook after commit); `DependencyInjection.cs`; new
`DisposableEmailTests.cs` (20 tests); `InternationalPhoneTests.cs` (+4 methods / 11 cases);
`problem-copy.ts` + `api_error.dart` (`invalid_phone` copy, identical wording); `.env.example`
(`DISPOSABLE_EMAIL_DOMAINS`); and **27 phone generators across 26 test files**.

**The 27 generators are the more useful finding.** The fix took the suite to 283 failures — not a
regression, but the discovery that the fixtures were registering users with impossible numbers
(`$"9199{seq:D7}"` is an eleven-digit Indian number). They passed only because of the fallback being
removed. A suite whose fixtures are impossible numbers can never catch a bug about impossible numbers.

**Database.** No migration. No stored phone data rewritten; all 8 accounts carry valid E.164 with a resolved
region, verified against libphonenumber before any change.

**API.** No route added, removed or renamed. Error codes are not in the contract, so `openapi.json` needs no
regeneration.

**Docs.** New D-317. `.env.example` documents `DISPOSABLE_EMAIL_DOMAINS` and why it is empty by default.

**Breaking Changes.** A number libphonenumber cannot place is now refused instead of silently proceeding to
an OTP nobody receives. Legacy bare-national-number login is unaffected and pinned by a test.

**Verification.** Build 0/0 · `InternationalPhoneTests` 49/49 · auth suite 116/116 ·
`DisposableEmailTests` 20/20 · full regression **1614 passed / 8 failed / 1 skipped of 1623** (from
1339/283 before the fixture repair).

**Remaining Work.** The 8 failures belong to a concurrent workstream, not to this change: three
`EventAudienceAuthorizationTests` and one `PostTests` answering `NotFound` where an event is expected
(`EventEndpoints.cs`, +53/−112, untouched here), one `NoContentDeclarationTests` that did not exist that
morning, and three `InviteLinkConcurrencyTests` already failing beforehand. Raising the disposable score to
a blocking weight needs its own decision against observed volume.

### Fix: "Log in" led to a create-account page with no way back (2026-08-09) — D-316

**Implementation Summary.** `/login` correctly refuses to show a form to someone already signed in, and
routes them on `needs_onboarding` — which D-311 widened to include a password and a date of birth. An
account created under that contract with either outstanding therefore landed on `/register`, which was
titled "Create your Kurx account" unconditionally, directly above a checklist ticking "Phone verified".
An account that plainly exists was being told to create itself.

The worse half: `/register` carried **no sign-out and no account switcher**, so that state could not
reach a login form by any route. Pressing "Log in" returned the user to the same page indefinitely. A
dead end reachable from the primary nav, not a wording problem.

`/register` now branches on `initialStatus` — "Finish setting up your account", with the signed-in phone
number shown — and the resume path renders "Not you? Sign out" through the existing `logoutAction`.

**Files Changed.** `web/app/register/page.tsx`; new `web/components/auth/sign-out-link.tsx` (settings'
`LogoutButton` is a block `Button` in its own form and cannot sit inside a sentence — same action, same
server side); new `web/test/register-resume.test.ts`.

**Database.** No schema change. **API.** No change — `needs_onboarding`, `Onboarding.IsIncomplete` and
D-311's grandfathering are all untouched. Client copy and one new component.

**Docs.** New D-316.

**Breaking Changes.** None.

**Verification.** web typecheck clean · 499 passed / 1 skipped (was 495). The new test pins all four
properties, including that `/login`'s redirect is *why* the escape route is required — removing that
redirect fails the test rather than quietly orphaning the copy.

**Remaining Work.** None for this defect.

### Fix: a refused password said nothing, and mobile skipped email verification (2026-08-09) — D-315

**Implementation Summary.** Reported as "it could not set password" on the phone. The API was correct
throughout — verified live: a valid password returned 200, an invalid one returned the right code. What
failed was turning that code into a sentence. Mobile switched on `weak_password`, **a code this backend
has never emitted**, so every real refusal fell to "Could not set your password. Try again."; web's
registration step resolved through `problemMessage`, which had no password entries. Same outcome by two
different routes: the server explained itself and the client discarded it.

The trap is that `PasswordPolicy.Breached` is dominated by *exactly twelve-character* strings
(`password1234`, `qwerty123456`, `welcome12345`), because it exists to catch what a "12 characters"
instruction produces. Told to use 12, the user complies, is refused, and is told nothing.

The second bug sat in the same function: `_savePassword` set `_step = _Step.success` instead of
re-reading the checklist, so **on mobile the email-verification step was built, listed in the checklist,
and unreachable in one sitting**. Three copies of the step ladder existed in that file; two ended at
`success`. All three now route through `_firstStep`.

**Files Changed.** `mobile/lib/features/auth/presentation/pages/registration_flow_page.dart` (route via
`_refreshAndAdvance`, error via `e.userMessage`, both other ladder copies consolidated);
`mobile/lib/core/network/api_error.dart` and `packages/ui/src/problem-copy.ts` (+8 `password_*` codes,
identical wording — an existing test compares the two tables); `web/lib/password.ts` (`passwordErrorCopy`
keeps only positional codes, delegates the rest); tests in
`mobile/test/features/auth/registration_flow_test.dart` and `web/test/error-copy.test.ts`.

**Database.** No schema change. **API.** No route, contract or response change — this is client copy only.

**Docs.** New D-315. `.claude/memory/{mobile,frontend}-conventions.md` carry the rule.

**Breaking Changes.** None.

**Verification.** `flutter analyze` clean · mobile 413 tests (was 395) · web typecheck clean · 495 passed
/ 1 skipped (was 478) · admin 27. Both new mobile tests were **confirmed to fail against the unfixed
code** before being kept. The live browser walk of the fixed web flow was blocked by CORS on the scratch
port it ran on (`ALLOWED_ORIGINS` is 3000/3001/8081) — a property of the test setup, not the fix.

**Remaining Work.** `PasswordPolicy.DerivedFromIdentifier` checks the stored E.164 string and its digits
but not the **national** number, so `9876501234xyz` is accepted for `+919876501234` — the form an Indian
user actually writes. Verified live, recorded in D-315, not fixed here: widening it needs its own pass
over the false-positive rate.

### Fix: Create Event was unreachable on mobile (2026-08-09)

**Implementation Summary.** `/events/create` was declared 370 lines below `/events/:slug` in
`mobile/lib/core/router/app_router.dart`. GoRouter matches in declaration order, not by specificity, so
the wildcard swallowed it: tapping Create Event opened `EventDetailPage(slug: 'create')`, which fetched
`GET /v1/events/create`, took a 404 and rendered "We couldn't find that." The D-305 eligibility gate had
never been reachable on mobile. Web was unaffected — Next.js resolves static segments before dynamic
ones, so `host/events/new` always beat `host/events/[id]`. Also added the create-account affordance the
sign-in screen was missing (`password_login_page.dart`); web has carried "New to Kurx? Create an account"
since Phase 25 and mobile's own guest gate has "Create account", but the sign-in page itself offered only
"Use a one-time code instead", which no new user reads as sign-up.

**Files Changed.** `mobile/lib/core/router/app_router.dart` (route moved above the wildcard),
`mobile/lib/features/auth/presentation/pages/password_login_page.dart` (+8),
`mobile/test/core/guest_browse_test.dart` (+2 tests), `.claude/memory/mobile-conventions.md`.

**Database.** None. **API.** None — the 404 was a client-side routing defect, not a missing endpoint.

**Verification.** `flutter test test/core/guest_browse_test.dart` — 9/9. `flutter analyze` clean on both
touched files. A scan of all 60 absolute routes in the router confirms zero remaining shadowed literals;
`/posts/*` was already ordered correctly. The `/v1/auth/otp/request` 500 seen in the same backend log was
an `OperationCanceledException` at 23.8s — the client aborting, not a fault.

### UI/UX redesign merged, with four refusals (2026-08-09) — D-314

**Implementation Summary.** Merged `origin/naveen` — the UI/UX redesign program, phases 39–50 — into
`feat/messages-settings`: 299 files, 26 conflicts resolved by hand. Taken whole except four changes that
would have regressed shipped behaviour, each refused with a reason recorded in D-314. The pattern worth
carrying forward: **a branch that forked before a fix will re-delete it.** `origin/naveen` predates D-302
and D-305, correctly identified two defects, and fixed them by removal — which now reads as a regression
against work that had since fixed the same defects properly.

**Files Changed.** 293 staged. Notable: `web/components/host/select-card-group.tsx` (new, from the
redesign) now backs the create-event wizard's five single-select steps; `backend/Kurx.Api/Endpoints/
OrgEndpoints.cs` (+2 lines) grants `opsEdit` on a personal representation; our duplicate
`web/components/events/ticket-qr.tsx` and `web/app/api/tickets/[code]/qr/` deleted in favour of the
tested equivalent; five unreachable `/host` routes deleted; `docs/architecture/CAPABILITY_ENGINE.md`
corrected to drop the `Route` field D-310 removed.

**Database.** No schema change.

**API.** No route added, removed or renamed — `OrgEndpoints` holds 20 routes before and after, and
`docs/api/openapi.json` is untouched. Only the *response values* of
`GET /v1/orgs/{orgId}/workspace-capabilities` change: a caller representing themselves now sees
create/update/delete on tickets, forms, announcements, attendees and report export. Publish, wallet,
settings and `can_host_paid_events` are unchanged, pinned by a test.

**Docs.** New D-314. `CAPABILITY_ENGINE.md` no longer documents a `Route` on `WorkspaceSurface`.

**Breaking Changes.** None on the wire. `docs/DECISIONS.md` was resolved to ours, so the incoming
D-288…D-291 are **not** in the log — they collide with four decisions of ours dated two days earlier and
need renumbering above the current maximum before they can land.

**Verification.** web typecheck + 478 passed / 1 skipped · admin typecheck + 27 passed ·
`flutter analyze` clean + 395 passed · backend build 0 warnings / 0 errors · backend suite
**1576 passed / 1 failed / 1 skipped of 1578** in the SDK container.

Four defects in our own code were found by the incoming discipline tests: a `text-fg` colour class naming
no token (six chat sites), four decorative images with `alt=""` and no `aria-hidden`, two loading shells
still importing the pre-redesign shim, and two profile widgets animating durations nothing could clamp
under reduced motion. Two of those tests were themselves broken on Windows — both compared `\`-separated
paths against `/`-keyed allow-lists, so every allowance silently missed. Also fixed:
`WorkspaceCompositionTests` still passed the `Route` argument D-310 removed, so the test project did not
compile (pre-existing on this branch, not from the merge).

**Remaining Work.** `InviteLinkConcurrencyTests` (expected 25 seats, saw 24) is recorded as
**unconfirmed**, not as a pass: it under-filled rather than oversold, so the invariant held, and it failed
only while a second full suite and a live `kurx-backend` shared the same Postgres. It needs an isolated
re-run per §9 before being called a defect or a flake. Not pushed.

### API response contract hardened; a ratchet now guards it (2026-08-09) — D-313

**Implementation Summary.** The reported defect — "~382 operations expose anonymous response objects" —
was wrong in kind. **Zero** operations carried an anonymous *object schema*; 396 of 518 carried **no
`content` block at all**, Swashbuckle's output for a minimal-API delegate returning `IResult` without
`.Produces<T>()`. Two further gaps the count had hidden: **no operation declared `204`** though 32 return
it, and the whole API declared **5** error responses.

Declared responses were raised by picking the cheapest honest rung per endpoint rather than minting a DTO
each time: reuse an existing `Abstractions` type · delete a 1:1 mapper and return its View · name a record
only where the mapper *translates* · one shared contract for a repeated shape · declare `204`/empty
`200`/binary as what they are.

**Files Changed.** New: `Abstractions/OperationAck.cs`, `Abstractions/EndpointResponses.cs`,
`Api/OpenApi/BinaryResponseSchemaFilter.cs`, `Tests/ResponseContractTests.cs`,
`scripts/openapi-response-check.mjs`, `docs/api/undeclared-allowlist.json`. Modified: 57 files under
`Api/Endpoints`, `Program.cs`, `.github/workflows/ci.yml`, `docs/api/openapi.json`.

**Database.** None.

**API.** No wire change. 358 operations gained a declaration (**480 of 518 now declared**); 22 hand-written `To*Json` mappers were
deleted as proven 1:1 projections and 26 translating mappers were converted to named records carrying
their translations verbatim. 80 endpoints across 34 files collapsed onto one `OperationAck`. Binary
downloads now publish `format: binary` rather than `format: byte` (base64).

**Docs.** `docs/DECISIONS.md` (D-313), `docs/api/README.md`, `docs/architecture/overview.md`,
`docs/roadmap/README.md`, `.claude/memory/api-conventions.md`, and a generated
`docs/api/UNDECLARED_TRACKER.md` listing every operation still undeclared.

**Breaking Changes.** None. The two token shapes stay split (5-field vs 6-field with `is_new_user`);
webhook acks stay empty `200`, not `204`; error responses remain undeclared — all deliberate.

**Verification.** `dotnet build -warnaserror` clean; full suite **1605 passed / 0 failed / 1 skipped**.
Wire equivalence proved key-for-key against the
pre-change mappers — `EventDetailResponse` publishes **44 keys against the original 44, none lost, none
gained**. `contract-check.mjs` 0 errors; its 13-case self-test passes; the new ratchet reports 0 new /
0 stale and was proved to fire by deliberately breaking the allow-list in both directions.

**Remaining Work.** 34 pending operations, all listed in `docs/api/UNDECLARED_TRACKER.md` (admin 8, auth 7,
events 6, me 3, orgs 2, 8 singletons). Two need a product decision rather than code — `POST
/v1/auth/login/password` and `/login/status` each return several shapes by outcome, the same polymorphism
as `/v1/gate/scan`. Two more are legitimately dynamic (passkey `*/options` return raw WebAuthn options).
Comprehensive `ProblemDetails` documentation is its own phase.

**Two traps for whoever picks this up.** 26 of 48 mappers *translate*, mostly **57 `.ToLowerInvariant()`
calls** — the wire carries `"published"`, the View carries `"Published"`, so "just return the View" would
silently rewrite every status value on the platform. And a conditional `value is null ? NoContent() :
Ok(value)` must declare BOTH outcomes; declaring only the 200 describes the misleading half.

### Developer Mode deleted platform-wide (2026-08-05) — D-274

**Implementation Summary.** Developer Mode is removed in full, superseding D-125. There is now exactly
one way to obtain a session on any client: the real authentication flow (OTP, password, passkey, trusted
device). No developer login, seeded identity, fixed OTP, impersonation endpoint, compile flag or
environment gate exists in any build or environment.

**Files Changed.** 23 deleted: `backend/Kurx.Api/Endpoints/Dev/**`, `backend/Kurx.Infrastructure/Dev/**`,
`backend/Kurx.Tests/DevAuthTests.cs`; web's `app/dev-login`, `app/api/dev-preview`, `components/dev`,
`components/dev-banner.tsx`, `lib/dev-actions.ts`, `lib/dev-session-config.ts`, `app/gallery`; admin's
`app/dev-login`, `components/dev-banner.tsx`, `lib/dev-actions.ts`; all of `mobile/lib/features/dev/` and
`mobile/test/features/dev/`; `docs/dev-workspace/`. Added: `SuperAdminBootstrap.cs`,
`SuperAdminBootstrapTests.cs`. `IAuthService.IssueDevSessionAsync` — a token-issuance path with no
credential check — is gone from the interface and the implementation.

**Database.** No schema change.

**API.** 9 routes removed: `/v1/dev/ping` and `/v1/dev/auth/{login,impersonate,preview,users,options,
quick-presets,presets,scenarios}`. The committed OpenAPI contract is unchanged — these were compiled out
of Release builds and never appeared in it.

**Docs.** New D-274; D-125 carries a superseded banner. `docs/dev-workspace/` deleted. Real-flow local
sign-in documented in `docs/auth/AUTHENTICATION_TESTING.md`; `SUPERADMIN_BOOTSTRAP_PHONE` documented in
`.env.example`. Memory updated: `security-rules`, `testing-standards`, `mobile-conventions`.

**Breaking Changes.** Anyone who signed in via `/dev-login` must now use the real flow; locally the OTP is
readable from the API log (`docker compose logs backend | grep 'sms→console'`). A fresh database gets its
first `SuperAdmin` from `SUPERADMIN_BOOTSTRAP_PHONE` — config-driven, runs only while zero SuperAdmins
exist, never creates a user, fully audited; every grant after that is `POST /v1/admin/staff/grant`.
Removed config: `DEV_AUTH`/`EnableDevAuth`, `NEXT_PUBLIC_DEV_LOGIN`, and CI's second dev-auth test job.

**Verification.** Backend 1296 passed / 1 skipped / 0 failed of 1297 (isolated worktree at `HEAD`+D-274,
16 m 13 s), build 0 warnings / 0 errors. Flutter 275/275, `analyze` clean. Web + admin typecheck, lint and
build green, no dev routes in either manifest. Repo-wide grep for every dev-mode symbol: zero matches.

**Remaining Work.** None.

### Terminology audit + OpenAPI contract regenerated (2026-08-04) — D-271

**Implementation Summary.** Final pass over D-267…D-270. Added `docs/architecture/TERMINOLOGY.md` as the
canonical vocabulary — legacy→current table, a "looks legacy but is correct" list so nobody 'fixes'
`OrgRole` or `/v1/kinds`, the authority ladder, and a naming-debt register — given precedence in
`.claude/CLAUDE.md`.

**Six stale architecture claims fixed in code.** The worst: `Event.OrgId` and `Event.CreatedBy`, the two
most-misread fields in the domain, carried **no documentation at all**, and `Event.OrgUnitId` said the
unit "OWNS this event". Also corrected the `EventService`/`IEventService` summaries and the
`EventPermissionService` grants comment, which still described per-service authorization after D-269.

**Generated artifacts.** `docs/api/openapi.json` regenerated — it had advertised
`POST /v1/orgs/{orgId}/events` (returns 404) and the retired `GET /v1/kinds/{slug}/capabilities`, while
missing `/v1/me/events`, `/v1/me/representations`, `/v1/events/{eventId}` and
`/v1/archetypes/{slug}/capabilities`. Produced from a throwaway API container on a spare port rather than
by restarting the shared compose backend, which another session was using.

**Breaking Changes.** None — documentation, comments and a regenerated artifact only.

### One event authorization service; eleven copies deleted (2026-08-04) — D-269

**Implementation Summary.** Eleven services each carried their own `CanManage`/`RoleAsync` pair or an
inline membership query — seven byte-identical, the rest drifted. `IEventAuthority` is now the single
source of truth; **17 services** delegate to it and every private copy is deleted.

**Architecture.** A caller resolves to one ordered level — `None < Participant < Staff < Manager < Admin`
— and every permission is a threshold on it, held in one table (`EventAuthority.Requirements`). `Manager`
is reached by the event's **creator** (no membership needed, D-268), a `Representative` (D-075), or an
`Owner`/`Manager` seat. `Finance` maps to `None`. `ResolveOrgAsync` on the same interface covers what an
*organization* owns rather than an event — speaker/sponsor/venue libraries, series, representation
authority — so there is still exactly one role → authority mapping.

**Two permission bugs the duplication was hiding, both fixed:** `Representative` — the platform's primary
organizer role — could create and publish an event but **not add a ticket type to it**, because nine
sub-resources hardcoded `Owner or Manager`; and after D-268 an event's **creator** held no authority over
its sub-resources. Both widenings are architecture-supported and stay inside the organization boundary.

**Preserved exactly:** the three surfaces with no admin bypass (attendees, announcements, invitations),
`Staff`-only attendee access, `Finance` holding nothing on events, D-191's exclusion of admins from
content edit, and every 404-vs-403 boundary.

**Extension point.** A future event capability (polls, maps, live Q&A, networking, resources) adds one
`EventPermission` value and one row — never authorization logic.

**Independent of M2.** `IEventAuthority` decides WHO may act; `ICapabilityService` decides WHAT an event
supports. They never consult each other, and no M2 file was modified.

### Domain model: the User owns the Event; no personal organizations (2026-08-04) — D-268

**Implementation Summary.** D-267 removed organization-first navigation but left the implementation
expressing **Organization owns Event**. Corrected: `Event.CreatedBy` is the owner and every event
authorization checks it first; an organization role is an *additional* grant, not the source of
authority. Kurx has Users, Events and Representations — no organization accounts, organizer accounts,
or **personal organizations**.

**API.** `GET /v1/orgs` (the "my orgs" list) → **`GET /v1/me/representations`**, named for its real
responsibility and excluding the self-representation row server-side. Event rows and the
workspace-capability contract replaced `org_id` / `org_name` / `org_is_personal` and the `organization`
block with a `representation` object (`kind: "personal" | "organization"`, `organization_id`,
`organization_name`, `verified`); `kind: "personal"` carries no organization identity at all.
`IEventService.CreateAsync` takes `Guid? representingOrgId` (null = Personal), and its authorization is
explicitly *representation authority* rather than ownership.

**Removed.** `IOrgService.GetOrCreatePersonalOrgAsync` — a persistence detail D-267 promoted into a
public abstraction — plus `OrgSummary.IsPersonal`, `is_personal` on every user-facing contract, web's
dead `createOrg` and `getMyOrganizations`, and mobile's dead `createOrg`. The FK-satisfying row is now
resolved by a private `EventService.ResolveSelfRepresentationAsync` and named nowhere public.

**Database.** No schema change. `Event.OrgId` still stores the *represented* organization; the name is
recorded as technical debt in D-268 with a full leak inventory (408 dereference sites across ~30
services, which is why it was not renamed in this pass).

**Breaking Changes.** `GET /v1/orgs` is removed — clients use `GET /v1/me/representations` with
`organization_id` / `authority`. Event-row and capability payloads changed shape.

### User-first architecture: organization-centric navigation removed everywhere (2026-08-04) — D-267

**Implementation Summary.** Kurx is user-first: **users own events**, and organizations are optional
metadata used only for representation, verification, permissions, payouts and collaboration. The
`Workspace → Organizations → Organization → Events → Create Event` workflow and `Profile → My
Organizations` are gone from every client, with no compatibility layer.

**API.** Added `GET /v1/me/events` (all the caller's events across every org they represent, one flat
list, `org_*` fields as row metadata), `POST /v1/events` (`representingOrgId` is a body field; null =
Personal, resolved to the caller's own `IsPersonal` org), and `GET /v1/events/{eventId:guid}`
(authenticated read by id, authorized against the event's own org). Removed
`POST /v1/orgs/{orgId}/events`. `GET /v1/orgs` rows gained `is_personal`; the workspace-capabilities
`organization` block gained `is_personal`. The `/v1/orgs/{orgId}/events/{eventId}/…` management
sub-resources and the per-org event **list** are retained — the former is reached only after an event is
open (org derived from the event), the latter serves the admin console.

**Database.** No schema change. `Event.OrgId` stays a non-null FK, as D-055/D-075 decided.

**Clients.** Web: `/workspace` rebuilt as the caller's own event list with status views and a direct
Create Event; `/host` redirects there; `current-org.ts`, the `kurx_org` cookie, `OrgSwitcher`,
`WorkspaceNav`, `HostDashboard`, `EventsTable`, `RepresentOrgPicker` and `/host/organizations` deleted;
`/host/revenue` + `/host/team` moved under `/host/representing/[orgId]/`. Flutter: `/orgs`,
`/org/create`, `/org/:orgId`, `/org/:orgId/events` and four pages deleted; create is `/events/create`,
management is `/events/:eventId/manage/…` via a new `EventManageScope`, org surfaces are
`/representing/:orgId/…`, and Profile's "My organizations" is now "Representing".

**Breaking Changes.** `POST /v1/orgs/{orgId}/events` is removed — clients must use `POST /v1/events`.
The retired client routes have no redirects.

**Bug fixed on the way.** Flutter's `EventManageDto` required five field names no projection emits and
its data source read a bare array from an endpoint returning `{ items, total }`, so every organizer
event-list parse threw. Pinned against the real projections.

### Zero-trust production-readiness audit + remediation (2026-08-03) — D-240…D-246, D-250…D-259

**Implementation Summary.** A full-repository audit of every layer, then remediation in two parallel
workstreams. Two P0 defects were **reproduced with failing tests before being fixed**, and both were
silent in production: the durable record (the append-only ledger, the token row) stayed correct while a
cache or a guard did not, so no request ever errored.

- **Wallet lost updates** — `OrderService.ConfirmPaymentAsync`, `RefundService.RefundOrderAsync` and
  `CollectedToAvailableLedgerJob` each loaded the wallet and assigned `balance += x`, which EF compiles
  to `SET "CollectedPaise" = <literal>`. Postgres READ COMMITTED does not prevent a lost update there.
  Measured: six concurrent captures produced six correct ledger rows totalling 60000 paise and a wallet
  crediting **10000**; six concurrent refunds netted the ledger to 0 and left **50000 paise** of phantom
  withdrawable funds. All three sites now use in-SQL increments with any precondition in the `WHERE`,
  matching `InventoryService.TryConsumeManyAsync` — the contract the wallet had simply never adopted,
  while `WalletService.InitiateWithdrawalAsync` had independently reached for `SELECT … FOR UPDATE`.
  (**D-240**)
- **Refresh rotation was not single-use** — the `RevokedAt` test was a read and the write was a tracked
  change flushed later. Two parallel `POST /v1/auth/refresh` calls with one token **both returned 200**,
  each minting a chain, so a stolen token could be forked into a live session and reuse detection never
  fired. Now an atomic conditional claim. The race loser is refused but its family is **not** revoked —
  concurrent refreshes are ordinary client behaviour, not proof of theft — with single-flight added to
  web and admin middleware, which previously answered a failed refresh by deleting the auth cookies.
  (**D-241**)

Also in this pass: `PerUserLimit` and group-capacity checks moved inside the order transaction behind
advisory locks (**D-242**); all 15 recurring Hangfire jobs given `[DisableConcurrentExecution]` and a
cadence-matched retry, with per-message jobs deliberately excluded (**D-243**); every required secret
strength-checked in Production, closing a hole where the committed `OTP_PEPPER` placeholder booted
clean (**D-244**); own-list endpoints bounded and their N+1s removed (**D-245**); and response-schema
coverage raised behind a ratchet (**D-246**). In parallel: boot-path backfills, the 429 error model,
Redis lifecycle and distributed rate limiting, signing-key I/O, unprivileged containers, client currency
rendering, and a generated + CI-gated API contract (**D-250…D-259**).

**A live bug found while tracing consumers.** `web/lib/api.ts` declared `myTicketSchema` with a required
`order_id` that `ToOrderJson` has never emitted, so `z.array(...).parse()` threw on every non-empty
response and the page's `.catch(() => [])` rendered "No tickets yet." **Every buyer with tickets saw an
empty My Tickets page, and nothing was logged.** Both ends fixed: the schema now mirrors the payload, and
the list endpoint denormalises `event_title` / `event_slug` / `ticket_type`, which the client had always
expected and the server had never sent.

**Files Changed.** Backend: `Orders/OrderService.cs`, `Orders/RefundService.cs`, `Auth/AuthService.cs`,
`Orgs/WalletService.cs`, `Jobs/*.cs` (15 annotated, `WalletReconciliationJob.cs` new),
`Secrets/RequiredSecrets.cs`, `Configuration/SecretValidation.cs`,
`Abstractions/{IOrderService,IWalletService}.cs`, `Api/Endpoints/*.cs` (26 `.Produces<T>()`).
Web/admin: `middleware.ts` (both), `lib/api.ts`, `app/(app)/tickets/page.tsx`. Mobile: `order_dto.dart`,
`org_dto.dart`, `my_orders_page.dart`, and the last three hardcoded `₹` sites.

**Database.** No schema change. The fixes are statement-shape and locking changes only — no migration.

**API.** Additive: `currency`, `event_title`, `event_slug`, `ticket_type` on the order payload; optional
`page`/`pageSize` (max 200, also the default) on `GET /v1/orders` and `GET /v1/groups`.

**Docs.** `DECISIONS.md` (17 entries), `docs/api/README.md`, `docs/architecture/overview.md`,
`docs/roadmap/README.md`, and three `.claude/memory/` convention files —
`database-conventions.md` (shared counters), `event-driven-design.md` (the "Hangfire schedules nothing"
claim was stale by 14 jobs), `security-rules.md` (secret strength, rotation).

**Breaking Changes.** None on the wire for existing clients. Internally, `IOrderService.MyTicketsAsync` /
`MyGroupsAsync` gained optional paging parameters, and `IWalletService` gained `ReconcileAsync` /
`RepairAsync`.

**Verification.** Backend **1017 passed / 0 failed / 1 skipped (1018 total)**, up from 1005 — 13 tests
added, none weakened or skipped. Build clean under `-warnaserror`. Web and admin: `tsc` 0 errors,
`next build` exit 0. Mobile: `flutter analyze` no issues, **266 tests pass**. Five new test classes each
reproduce their original defect and pass on the fix.

**Remaining Work.** `TeamService` carries the same count-then-act race D-242 fixed in the Group path,
and Team is the *authoritative* model — Group is its legacy mirror. `TicketType.Sold` is still a
read-modify-write mirror that can undercount organizer-facing figures (it cannot oversell; the inventory
pool guards that). Public-vote rate limiting is bypassable under concurrency. 382 of 426 operations lack
a response schema — they return hand-written anonymous objects and need named DTOs first, since
declaring a schema that disagrees with the wire is worse than declaring none. Two mobile analytics charts
and iOS passkeys remain genuine feature work. Not verified in this pass: load/soak behaviour, browser and
device runtime, and the migration history.

### Professional Identity V2 — Phase 4A + 4B: the Professional Resume and the contributions heatmap (2026-08-02) — D-228

The two surfaces that aggregate across *every* section — and therefore the two where a privacy
mistake does the most damage. **No schema change.**

- **The Resume is a projection, not a template engine.** Header headline from `IdentityEngine`, spine
  from `JourneyEngine`, counts from `ExperienceEngine`, the rest from the fact-set. If a fact isn't on
  the profile it isn't on the resume. First surface needing every section at once — exactly the case
  D-224's fact-set was built for, so it's one load and one render.
- **Composed for the requesting viewer, through the resolver.** A section hidden from that viewer is
  absent from their copy; otherwise the PDF would be a trivial bypass of the whole privacy model.
  Tested: a hidden profile 404s for a stranger while its owner still downloads, and hiding a section
  measurably shrinks a stranger's document but not the owner's.
- **One template, no custom sections, never stored.** A stored copy is stale the moment the next event
  completes; the footer carries the generation date so a stale printout is self-evident. The
  self-declared bio sits last, labelled "Written by this person — not verified by Kurx."
- **Rate-limited** (10/min per IP) — the most expensive public operation on the platform. The caller
  controls nothing about composition, so there's no injection surface into the document.
- **Contributions heatmap: gate, then bucket — never bucket, then gate.** The subtlest privacy trap in
  the system: counting a day first and suppressing hidden entries after would leave the day's
  *intensity* as an oracle for hidden activity. A test asserts a day whose only activity is hidden
  **disappears entirely**, not merely reduces.
- Contributions are *events*, not engagement — no logins, page views or points. 52-week grid on web,
  26-week on Flutter (a year doesn't fit a phone at a legible cell size); both scroll inside their own
  viewport so the page never does.

**Verification:** backend **950 passed / 0 failed / 1 skipped / 951 total** (+8); web `tsc`/`eslint`/
`next build` clean; Flutter `analyze` 0 issues, `test` 255 passed.

### Professional Identity V2 — Phase 4B (client): the derivation engines become visible (2026-08-02) — D-227

Closes the cross-platform gap left by D-225/D-226, whose engines shipped with nothing rendering them.
**No backend change.**

- **`null` renders as an em dash, never as `0`.** The metrics contract distinguishes *hidden from this
  viewer* from *genuinely none*; showing a hidden section as "0" would turn the owner's privacy choice
  into a factual claim about them. A DTO test pins `null` surviving as `null`.
- **The derived headline leads, the self-declared one follows, both labelled.** New shared
  `ProvenanceBadge` driven by the server's `_meta` map rather than by field name — a client physically
  cannot mislabel a field, and an unrecognised category renders nothing rather than guessing.
- **Event DNA is proportional bars, not a sparkline** — a correction to the design doc. A sparkline
  reads as a time series; this is a categorical distribution, and implying a trend would be wrong.
- **Relationships lead the "Shared history" sheet**, above the raw shared events/orgs that evidence
  them. Labels render verbatim — re-wording them client-side risks inverting a directional
  relationship ("Judged their entry" vs "Judged by them").
- New trust badges on Flutter: Speaker Verified, Community Verified, Email Verified.
- Each section fetches independently on both platforms: a 403 is the normal outcome for a restricted
  section, so it renders nothing and never takes the profile down with it.

**Verification:** web `tsc`/`eslint`/`next build` all clean; Flutter `analyze` 0 issues,
`test` 255 passed / 0 failed (+5); backend unchanged at 942/943.

### Professional Identity V2 — Phases 3C & 3D: derivation engines and professional relationships (2026-08-01) — D-225 · D-226

**No schema change.** Four pure engines over the D-224 fact-set.

- **The headline is not its own engine.** It is the short form of the identity labels, so both come
  from one `IdentityEngine` and can never disagree — the old `BuildIdentityLabels` path is deleted
  rather than left as a second derivation. A test asserts every headline token comes from the earned
  label pool. No manual override; pinning an *earned* label is allowed. Capped at 3 tokens / 60 chars,
  overflow dropped rather than truncated.
- **`derived_headline` is a new root key**, not a replacement for the self-declared `headline` —
  `_meta` marks one `derived` and the other `self_declared`.
- **Experience is a band, never a score**, and always renders with the counts that produced it.
  Below the first threshold it reads "Building". Employment and education are permanently excluded.
- **Metrics gate before counting.** A hidden section shrinks the number rather than being counted and
  relabelled; `null` means hidden and clients render "—", never `0`. Event DNA is one of these
  metrics — a distribution, not a subsystem.
- **Vanity metrics stay out**: no profile views, no points or leaderboard rank, no percentiles, no
  ally count as a headline stat. A test asserts none of them appear in the payload. No-show rate was
  approved as owner-only but is **not faked** — the data to compute it honestly isn't loaded yet, so
  it is deferred rather than stubbed.
- **Professional relationships (D-226).** `ConnectionEngine` explains *why* two people know each
  other by intersecting two fact-sets: 16 directional types (`judged` / `judged_by`,
  `spoke_at_their_event` / `hosted_their_talk`, …), ranked reasons in plain language, **no strength
  score**. Co-attendance is explicitly not collaboration. A shared *verified* org reports as a
  community, separately from a shared org. Surfaced on `GET /v1/me/allies/mutual/{id}`, additive,
  D-211's accepted-connection gate unchanged.

**Not yet done:** the clients do not render metrics, experience, the derived headline or
relationships; the Resume and contributions heatmap are not started.

### Professional Identity V2 — Phase 3B: the profile fact-set (2026-08-01) — D-224

**A refactor with no user-visible change**, shipped first on purpose: it proves the shape against a
feature that already works before six more are built on top. No schema, API, DTO or client change.

- **One `ProfileFactSet`** materialises a person's verified facts (events, participations,
  assignments, attendance, sessions, published results, memberships, teams) once per request. Every
  derivation engine becomes a pure function over it — `JourneyEngine` is now a `static` class that
  opens no connection and issues no query.
- **Viewer-independent by design.** It holds the person's facts, not one viewer's view of them;
  privacy stays with the resolver at emission. Filtering at load would split the privacy decision
  across two places, which is the exact situation D-221 was created to end. A test asserts the loader
  takes no viewer parameter so the property can't be quietly lost.
- **The private-event invariant now lives in one place.** Previously repeated in every query touching
  an event; a private event's facts can no longer enter the fact-set at all.
- **Query cost is fixed at 8 per load** regardless of how many sections a request projects, instead of
  growing per section — and the Resume becomes a single load rather than an impossible assembly.
- Regression proof: the existing Journey and visibility suites pass **unchanged**.

### Professional Identity V2 — Phases 1–3: central privacy resolver, dormant sources wired, Professional Journey (2026-08-01) — D-221 · D-222 · D-223

**One new nullable column across all three phases.** Everything else is projection over rows that
already existed.

- **One central visibility resolver (D-221).** The same authorization question was answered in nine
  scattered places; all nine now call `IProfileVisibilityResolver`. `SectionAccess` has a private
  constructor and one factory, and every gated read takes it as a required parameter — "forgot to
  check" is now a compile error, not a leak. Fails closed, with a test asserting every
  `ProfileSection` has a decision.
- **Four visibility tiers per section** — public / connections / event participants / only me —
  stored in one jsonb column with a **stored-override → legacy-boolean → default** fallback chain, so
  a user who never touches the new settings resolves exactly as before. Writes dual-write the old
  booleans; a tier they can't express maps to `false`, so a rollback over-hides rather than
  over-shares.
- **Public profile endpoints are now optionally authenticated** — a bearer token, when present, only
  widens what the caller may see. Anonymous behaviour is unchanged.
- **Three new trust signals**, all previously computed and never exposed: Email Verified, Speaker
  Verified (an organizer linked a `Speaker` row — D-208), Community Verified (`IsOrgVerifiedRep`).
  Positive signals only: risk, fraud, reports, moderation state and trusted-device details are
  **never** public, and a test enforces their absence.
- **Provenance `_meta`** classifies every root field as `verified` / `self_declared` / `derived`, so
  a client can't render a claim as proof. Additive — the root shape is frozen.
- **Four dormant verified sources wired (D-222):** competition results (`StageResult`, **published
  only** — provisional and disputed never surface), assignments with `CompletedAt` and the 14-role
  vocabulary, and speaker sessions from the organizer-made link. Plus three timeline lanes and a
  third achievement source — the latter took *one method*, exactly as D-203 was designed for. Event
  media is deliberately **not** exposed: it has no person-level owner, and presenting an event's
  gallery as someone's portfolio would invent a relationship.
- **Professional Journey (D-223)** — the flagship. A first-attainment ladder across 11 tiers, ordered
  **chronologically, never by a canonical career ladder** (people organize before they volunteer). Every
  node links to the row that proved it; a tier whose gating section is hidden is dropped entirely, so
  the Journey can't become a side channel around the resolver. No "next step" suggestions. Rendered on
  web and Flutter.

### Professional Identity V2 — Phase 0: the profile write surface (2026-08-01) — D-219 · D-220

Pure defect closure from the profile audit in
[`docs/architecture/PROFESSIONAL_IDENTITY_V2_REVIEW.md`](docs/architecture/PROFESSIONAL_IDENTITY_V2_REVIEW.md).
**No schema change.** Every flag involved was already enforced on the public read paths and settable
by nothing.

- **The four visibility flags got a writer (D-219).** `PATCH /v1/me/privacy`, partial. `ShowAttended`
  defaults to `false`, so until now the attended-events lane D-201 specified was hidden for *every*
  user with no way to turn it on, and `/events?type=attended` was an unclearable 403.
- **Fixed a silent data-loss bug.** `GET /v1/me` now returns the editable display fields. The web
  settings form previously prefilled from the *public* profile — which returns nothing for an
  unclaimed username or a private profile — and because it submits every field, a failed prefill
  overwrote real headline/bio/skills with blanks. Fixing the prefill source removes the failure mode
  rather than guarding the write.
- **Per-connection ally visibility (D-219).** `PATCH /v1/allies/{id}/visibility`. The shared-flag
  semantics are now explicit: `AllyConnection` is one row per pair, so either party may hide it and
  it disappears from both profiles — the more private choice wins. Non-party → 404, not 403.
- **Profile image upload.** `POST /v1/me/profile-image/presign` for avatar/cover, reusing the existing
  presign pattern with a closed `slot` set (it forms part of the storage key) and an image-only
  content-type allowlist (the key renders in an `img` tag on a public page).
- **Flutter got profile editing**, which did not exist at all beyond the username claim — edit page,
  privacy page, avatar upload. The dead Hive-backed `bio` cache no screen ever read was deleted.
- **`links_json` is finally rendered and editable** on both clients, after being accepted, stored and
  returned by the API since D-201 with no UI anywhere.
- **`EducationJson` retained permanently as self-declared data (D-220)**, superseding D-041's promise
  to migrate it into `membership_claims` — that migration cannot run without fabricating unreviewed
  claims or dropping user data, which is why it never did.
- Fixed `IAllyService.GetMutualDetailAsync`'s XML doc, which still claimed the call was "not gated on
  being allies" after D-211 made it require an accepted connection.

**Verification.** Backend 899 passed / 0 failed / 1 skipped / 900 total against real Postgres (883
before; +17 regression tests). Web `tsc --noEmit` clean, `next build` green. Flutter `analyze` 0
issues, `test` 250 passed / 0 failed (248 before; +2).

---

## [Unreleased] — branch `feat/dev-workspace-auth`

### Security & correctness pass — OTP storage, secret validation, Redis fail-closed, fixture contract (2026-08-01) — D-215 · D-216 · D-217 · D-218

Found by a full-repository read-through. No feature work; the login API contract is unchanged.

- **Login OTP moved off unsalted SHA-256 (D-215).** `AuthService` delegated the OTP code lifecycle to
  `IOtpService`, so login codes are now stored in `otp_codes` as `HMACSHA256(OTP_PEPPER)` instead of a plain
  SHA-256 in `otp_requests`. A 6-digit code has a 10⁶ keyspace, so the old digest was fully precomputable —
  `code_hash` was effectively plaintext to anyone able to read the table. Recorded as finding C5 in the archived
  auth dossier; the hardened replacement had shipped for password reset/recovery but the login cut-over never
  happened. **Deliberately unchanged:** accepted phone formats, rate limits (3/destination/10min + the per-IP
  hourly shield), every error code, and the HTTP contract — no client on any platform changed. `IOtpService`
  gained `destinationIsCanonical` and `enforceResendCooldown` opt-outs (both defaulting to strict) so the legacy
  login contract is preserved without relaxing anything for other callers. `otp_requests` is now dead storage.
- **`TICKET_HMAC_SECRET` mandatory in Production (D-216).** Previously read as
  `config["TICKET_HMAC_SECRET"] ?? secret` — an unvalidated read that silently fell back to the JWT signing key,
  reusing one key across two security domains, and permitting the repo-published placeholder in Production.
  Now resolved through `ISecretProvider` (so `SECRETS_PROVIDER=aws` works) and validated: Production refuses to
  start if it is missing, <32 chars, or the placeholder. Dev keeps the fallback.
- **Production Redis fail-closed (D-217).** Implements ADR-AM14, previously specified but never built. Without
  `REDIS_CONNECTION` the app fell back to an in-process cache in *every* environment, leaving the SignalR
  backplane, presence, and the rate limiters silently wrong while reporting healthy. Production now refuses to
  start; dev/test behaviour unchanged.
- **`FixtureView.Participants` output contract (D-218).** New `FixtureSubjectView(SubjectType, SubjectId,
  SubjectName, Seed)` replaces the reused *request* record, adding a server-resolved display name so fixture UI
  no longer renders raw GUIDs or issues per-participant lookups. Additive to the response; request bodies
  unchanged. The 15 value-returning competition routes also gained `.Produces<T>()`, putting those five view
  types into `swagger.json`.

**Also fixed (no ADR — no architecture introduced):** 44 web tests that never ran (`lib/api.ts` calls React's
`cache()` at module scope, which is undefined under jsdom, so two chat suites threw at import and reported as
0-test failures — the mock moved to `test/setup.ts`); `.env.example` documenting `NEXT_PUBLIC_API_BASE` where
admin reads `NEXT_PUBLIC_API_URL`; and n8n published on `:5678` with no authentication while holding the
Postgres credentials (`N8N_BASIC_AUTH_*` now set).

**Verification.** Backend **883/883 passing** in a container against real Postgres (up from 880 — three new
`SecretValidationTests` cover D-216, and the existing acceptance test now asserts `Secret != TicketHmacSecret`).
Web **123/123** (was 79 passing with 44 unrunnable). `flutter analyze` 0 issues; web + admin typecheck clean.

### Backend Hardening Sprint (post backend-audit) (2026-07-25) — ⏳ AWAITING REVIEW

Production-readiness hardening only — no new features, no architecture change, no API contract change.

- **Dead code removed.** `IScanService` + `ScanDigest`/`ScanRosterMember`/`ScanResult` — an orphaned check-in
  abstraction with no implementation, DI registration, endpoint, consumer, doc, or test (verified). The live check-in
  is `GateEntryService` (`POST /v1/gate/{eventId}/scan` + `ScanHub`), which the Flutter scanner actually calls.
- **Webhook hardening** (`WebhookEndpoints`). **Fail closed in production:** the WhatsApp POST + verification-handshake
  now reject (503) when `WHATSAPP_APP_SECRET` / `WHATSAPP_VERIFY_TOKEN` is unset in Production (Development keeps the
  dev fallback so tests/local runs are unblocked). **No silent swallow:** a processing exception now logs and returns a
  **retryable 500** instead of a 200 — safe because both handlers are idempotent (`ConfirmPaymentAsync` via the §17.1
  atomic Pending→Paid claim; `HandleWebhookAsync` via `wamid` upsert), so a provider retry re-confirms without duplicate
  processing. Signature-rejection and skipped-verification paths are now logged. Razorpay signature verification was
  already fail-closed (via the gateway).
- **Dead FCM-token cleanup** (`OutboxDispatchJob`). The security-notification push path now catches
  `InvalidFcmTokenException` per token, **deactivates the dead device, and continues** to the user's other devices —
  mirroring `NotificationService`. Previously a single dead token aborted the send loop and churned outbox retries
  without ever cleaning the token.
- **Provider abstraction + Firebase audited — no change needed.** The env-flag provider selector fails fast on an
  unknown value; startup logs the resolved providers; health checks cover Postgres/Redis/storage; real adapters already
  exist for Push (Firebase), SMS (AWS SNS), and Secrets (AWS Secrets Manager). `FirebasePushSender` handles init,
  console-fallback, and dead-token signalling correctly.
- **Verification.** Release build `-warnaserror` **0/0**; **full backend suite 768 passed / 1 skipped / 0 failed**
  (761 → 768; +7 `WebhookHardeningTests`: signature valid/invalid/missing, malformed-body retryable-500, replay/duplicate
  idempotency, unknown-wamid graceful, provider fail-fast). No regressions; no API contract changed.
- **Intentionally deferred** (real provider implementations behind the existing abstraction, unchanged by this sprint):
  payment gateway, storage, email, SMS/WhatsApp sender, KYC.

### Event Architecture V3 — Phase 16: Discovery & Search (§15) (2026-07-25) — ⏳ AWAITING REVIEW

> **Status: IMPLEMENTED — awaiting engineering review.** Not committed, not approved. See `D-184`.

- **Implementation Summary.** Implemented V3 §15 Discovery & Search — **backend + API only** (client discovery UI is
  the §12 backlog), confirmed via four Option-1 scope forks. Replaced the `ILIKE`/`ViewCount DESC` discovery with an
  **outbox-fed** Postgres FTS + trigram index, deterministic ranking, alias search, eligibility-aware feeds, and the
  Festival/RECURRING listing collapse — **additively**, preserving every Phase 0–15 foundation (money path, lifecycle,
  templates, workspace, registration, inventory, passes, teams, competition, approval chains, snapshot semantics).
- **§15 index (outbox-fed, never dual-write).** New `event_search_documents` (one row per Published + Public event) is
  the only discovery source. An event write enqueues a `search.reindex` message in its own transaction; `OutboxDispatchJob`
  → `SearchIndexService.ProjectAsync` rebuilds/removes the document idempotently. The projector writes plain text; the DB
  derives a **stored generated `tsvector`** (title=A, body=B; GIN) + a `gin_trgm_ops` index on `FuzzyText`. Discovery is
  eventually consistent. A startup backfill projects existing events.
- **One canonical discovery implementation.** `SearchService` (raw SQL for the ranked FTS/trigram `q` path; LINQ over the
  index for feeds) backs the **existing** `/v1/events`, `/upcoming`, `/trending`, `/featured`, `/latest`, `/{slug}/related`
  — routes + response DTOs unchanged, new query params (`kind`, `language`, `lat`, `lng`, `radiusKm`) additive. `EventService`
  discovery methods are thin delegations. No `/v1/search` or `/v1/discover`. Alias search (retired type names via
  `kind_aliases`) + trigram typo tolerance.
- **Deterministic ranking replaces `ViewCount DESC`.** Recency + velocity (recent-view stream, windowed) + conversion
  (registrations) + proximity (haversine, only when a location is supplied). Signals refreshed by the new
  `search-index-refresh` job (15-min cron). **Affinity/personalisation deferred** (no user-model/ML).
- **Listing collapse** (reusing Phase-12 `ParentEventId`/`SeriesId`/`ListedStandalone`): a Festival is one card
  (sub-events hidden from standalone discovery unless opted in); a RECURRING series is one listing (next-upcoming
  occurrence via `IsSeriesPrimary`). Direct slug/id access unchanged; EDITIONS members stay individually discoverable.
- **Eligibility-aware feed.** New authenticated `GET /v1/events/for-you` — Published + Public events the caller may
  register for (`IAudienceService.EvaluateAsync`; open events pass via a `HasAudienceRule` fast path). Internal events
  are never indexed → their existence is never leaked (404-not-403 preserved).
- **Files Added.** `Domain/Entities/Search.cs` (`EventSearchDocument`), `Application/Abstractions/ISearchService.cs` +
  `ISearchIndexService.cs`, `Infrastructure/Search/SearchService.cs` + `SearchIndexService.cs`,
  `Infrastructure/Jobs/SearchIndexRefreshJob.cs`, migration `20260725055959_AddSearchIndex`, `Tests/SearchDiscoveryTests.cs`.
- **Files Modified.** `EventService.cs` (outbox reindex enqueue on create/update/transition/feature; discovery methods
  delegate to `ISearchService`; removed the old ILIKE/`PublicBaseQuery`/`ToSummaryExpr`), `IEventService.cs`
  (`EventListFilter` gains additive optional filters), `OutboxDispatchJob.cs` (`search.reindex` handler),
  `KurxDbContext.cs` (index config + DbSet), `DependencyInjection.cs`, `Api/Program.cs` (DI, startup backfill, cron),
  `Api/Endpoints/EventEndpoints.cs` (additive search params + `/for-you`). 4 pre-existing discovery tests updated to
  drain the outbox (sync→async). No approved entity/table altered.
- **Database.** Additive, reversible migration `AddSearchIndex`: `event_search_documents` + btree indexes; a generated
  `tsvector` column + GIN + `gin_trgm` index via raw SQL (`Down` drops the table). `pg_trgm` already enabled.
- **API.** Existing discovery routes + DTOs unchanged (new optional query params only); one new authed route
  `GET /v1/events/for-you`. No existing response shape changed.
- **Breaking Changes.** None. Discovery becomes eventually consistent (index-backed) — a functional change, not a
  contract change.
- **Engineering-review fixes (Option B → applied → awaiting re-verification).** No contract change: **H1** — audience-rule
  create/update/delete enqueues a reindex (eligibility feed stays correct); **H2** — the FTS query is now index-eligible
  (the `<%` word-similarity operator replaces the non-indexable functional predicate → BitmapOr of the GIN indexes;
  `SET LOCAL` threshold, `EXPLAIN`-verified); **M1** — ticket-type mutations enqueue a reindex (paid/free filter);
  **M2** — series attach/detach/mode/delete enqueue reindex for affected events (collapse); **M3** — `RefreshSignalsAsync`
  is set-based (no N+1); **M4** — one canonical ranked query for search/trending/eligible so proximity ranks consistently
  (a no-`q` location browse now orders by distance). A shared `SearchReindex.Message` factory keeps every enqueue site
  identical.
- **Pre-engineering self-review fixes.** A mutation/projection audit found two more stale-projection paths, fixed with
  the same outbox pattern: **speaker attach/detach** (`SpeakerService`) and **org rename** (`OrgService.UpdateAsync`,
  reindexing the org's Published+Public events on a real rename). Referenced-entity name renames (org unit — no path
  today; the Speaker entity's own name; kind/alias/tag static data) refresh on the next event edit — a documented
  eventual-consistency boundary. The admin/org event-management `ILIKE` lists intentionally stay off the index (they
  must show drafts).
- **Verification (post-fix).** Release build `-warnaserror` **0 warnings / 0 errors**; **full backend suite 761 passed /
  1 skipped / 0 failed** (container, real Postgres — 744 → 753 → 759 → **761**; +17 `SearchDiscoveryTests` incl. 6
  API-path review-fix regressions + 2 self-review regressions (speaker/org-rename searchable); the 1 skip is the
  pre-existing D-103 concurrent-refund skip). Web/Admin/Flutter untouched (backend-only).
- **Deferred (outside Phase 16 scope — not defects).** `topics`/`channel` classification facets (require a §12 field —
  a future classification phase; the index accommodates them without change); per-user **affinity/personalisation** (a
  dedicated future phase); speaker/sponsor/media edits reindex on the next event write rather than instantly;
  eligibility evaluated per candidate over a bounded window (SQL-pushdown is a future scale optimisation); all client
  discovery UI (Web/Admin/Flutter, §12 backlog).

### Event Architecture V3 — Phase 15: Templates & generated workspace (§13.1/§20) (2026-07-24) — ✅ APPROVED

> **Status: APPROVED / COMPLETE.** Implemented → engineering-reviewed (Option B) → review-fixed (H1/H2/M1/M2/M3 + M5
> tests) → focused re-verification (Option A) → approved. See the **Engineering review fixes** bullet below and `D-183`.

- **Implementation Summary.** Activated the inert `EventTemplate` scaffold into the authoritative **scoped +
  versioned** Template system of §13.1 and added the §20 **generated** organiser workspace — **backend only, no
  wizard/UI, no AI** (confirmed Option 1 ×3). Additive/reversible; every Phase 0–14 foundation (Order→Ticket,
  Registration→Admission→Credential, InventoryPool, Pass, AdmissionRight, VAR, lifecycle/gates/approvals, material
  change, SeatBlock, Walk-in, Delegate, Structure, Series, Teams, Competition, the money path, `Published` semantics)
  is untouched.
- **§13.1 templates.** A template is `Scope` (PLATFORM|ORG|UNIT|PERSONAL, most-specific-wins) + a version family
  (versions share `RootTemplateId` = the v1 id events reference; a Published version is immutable). **Declarative
  config only** ([D-132](docs/DECISIONS.md)) — `config_json` carries a capability preset + defaults; it never carries
  dates/slug/status/inventory/pools/financials. CRUD: create (v1 Draft) · update (Draft only) · publish · new version
  (v+1, same family) · archive · clone (new family) · delete (unused Draft family). Scope authority: Platform=admin ·
  Org/Unit=org manager · Personal=any user.
- **Closed-registry validation — one pipeline.** Every write runs `ValidateConfigAsync` against the capability
  registry: unknown slug / referenced-as-disabled / bad state / template-declared `workspace_tab` / malformed JSON are
  **rejected, never repaired** — the exact door a future AI generator would submit a draft through (no AI built here).
- **Snapshot-at-creation.** Creating an event with `templateId` (the family root; usable only if it has a Published
  version) overlays that version's capability preset onto the just-materialised `event_capabilities`, applies
  declarative defaults (timezone), and records `events.created_from_template_version`. No inventory/financial/
  registration rows are auto-created; a later template edit never mutates the event.
- **§20 generated workspace.** `GET /v1/orgs/{orgId}/events/{eventId}/workspace` groups the event's non-Off
  capabilities by `workspace_tab` (via `ICapabilityService.GetForEventAsync` — generated, never hand-written per Kind)
  and attaches a **live publish checklist** that projects the Phase-14 §14.2 gates by calling the same
  `TransitionGateAsync` the real transition runs — never a second source of truth.
- **Files Modified.** `Domain/Entities/EventManagement.cs` (`EventTemplate` activation fields), `Domain/Entities/Events.cs`
  (`CreatedFromTemplateVersion`), `Domain/Enums/Enums.cs` (`TemplateScope`/`TemplateState`),
  `Application/Abstractions/ITemplateService.cs` + `IEventService.cs` (workspace DTOs + method),
  `Infrastructure/Events/TemplateService.cs` (activated), `EventService.cs` (template-apply in `CreateAsync` +
  `GetWorkspaceAsync`; ctor gains `ITemplateService`), `EventStatusWorkflow.cs` (`ForwardActions`),
  `SystemTemplateSeeder.cs` (Platform/Published/root), `Persistence/KurxDbContext.cs` (template config + indexes),
  `Api/Endpoints/TemplateEndpoints.cs` (activated CRUD) + `EventEndpoints.cs` (workspace route),
  migration `20260724184540_AddTemplateActivation`, `Tests/TemplateActivationTests.cs`. No approved entity/table altered.
- **Database.** Additive migration `AddTemplateActivation`: new `event_templates` columns (scope/version/state/
  root/config_json/…) + unique `(RootTemplateId, Version)` + scope/slug indexes (old unique-Slug relaxed to non-unique,
  a family shares its slug); `events.created_from_template_version` (nullable). Backfills pre-existing rows to
  `root=self, v1, Published`; fully reversible.
- **API.** `/v1/templates` list gains scope/version fields (keeps `name`/`slug`); new authed writes on `/v1/templates`
  (`POST /`, `PATCH /{id}`, `POST /{id}/{publish,versions,archive,clone}`, `DELETE /{id}`); new
  `GET /v1/orgs/{orgId}/events/{eventId}/workspace`. The removed `/v1/orgs/{orgId}/templates` write routes had no
  test/client references. No existing event route or response shape changed.
- **Breaking Changes.** None to shipped surfaces. Additive.
- **Engineering review fixes (Option B → applied → re-verified Option A → approved).** No schema change, no new
  features: **H1** — `NewVersionAsync` catches the `(RootTemplateId, Version)` unique-index race and returns
  `draft_exists` (409), never a 500 (the DB constraint stays authoritative). **H2** — the workspace publish checklist
  is a **pure read**: a new `IApprovalService.IsCompleteAsync` evaluates the SAME completeness rule via a shared
  read-only `EvaluateCompletenessAsync`, and `TransitionGateAsync` gained a `readOnly` flag the checklist passes, so a
  `GET /workspace` no longer materialises `approval_requests`/`step_decisions` or calls `SaveChanges`. **M1** —
  `ApplyToEventAsync` skips any capability not available in the event's `EventMode` (§11.3; `CapabilityService`
  authoritative), so a mode-forbidden state is never persisted. **M2** — `CloneAsync` enforces a view check
  (Platform=all · Personal=owner · Org/Unit=member), returning `not_found` otherwise (no cross-tenant/personal
  disclosure). **M3** — event insert + capability materialisation + template snapshot run in one
  `BeginTransactionAsync`, so a failed apply rolls the whole event back (no orphan). **M4 deliberately NOT changed** —
  declarative-default materialisation stays timezone-only, awaiting architectural confirmation.
- **Final delivered capabilities.** Scoped templates (PLATFORM|ORG|UNIT|PERSONAL, most-specific-wins) · versioned
  templates (immutable Published versions sharing `RootTemplateId`) · snapshot-at-creation · `created_from_template_version`
  provenance · backend-generated organiser workspace · read-only publish-checklist projection of the §14.2 gates ·
  manual template CRUD (create/update/publish/version/archive/clone/delete) · closed capability-registry validation
  (reject-never-repair) · read-only workspace projection (no writes) · atomic event creation · concurrency-safe
  versioning · clone/personal authorization. **No AI generation** (the validation pipeline is AI-ready).
- **Verification (final, approved).** Release build `-warnaserror` **0 warnings / 0 errors**; **full backend suite 744
  passed / 1 skipped / 0 failed** (container, real Postgres — 724 → 737 initial → **744** after the fix round; +20
  `TemplateActivationTests` incl. system-template scope/version, create→publish→list, immutable-Published + new-version,
  unknown/disabled/malformed config rejected, scope authority, snapshot preset+timezone+version, workspace tabs +
  checklist, workspace 404, clone new family, clone-hidden, **parallel version race**, **parallel publish**, **scope
  precedence**, **snapshot independence**, **deletion**, **archival**). The 1 skip is the pre-existing D-103
  concurrent-refund skip. Web/Admin/Flutter untouched (backend-only).
- **Deferred (outside Phase 15 scope — not defects).** The 4-screen creation **wizard** and all client workspace UI
  (Web/Admin/Flutter, §12 backlog → Phase 15/16 client work); wiring template branding/audience/form-field/agenda/stage
  declarations into their own subsystems (carried + versioned + snapshot-recorded now); the per-event
  `event_capabilities` override **merge** in `CapabilityService.GetForEventAsync` (a documented later-phase step — a
  template's overlaid capability is persisted now but surfaces in the resolver/workspace only once that merge lands);
  AI-assisted template generation (validation pipeline is AI-ready).

### Event Architecture V3 — Phase 14: Lifecycle, gates & approvals (§14) (2026-07-24) — ✅ APPROVED

> **Status: APPROVED / COMPLETE.** Implemented → engineering-reviewed (Option B) → review-fixed (H1/H2/M1/M2/M4 +
> N1/N2) → final verification (Option A) → approved. See the **Review fixes** bullet below for the corrections applied
> before approval.

- **Implementation Summary.** The V3 §14 lifecycle refinement + five validation gates + internal approval chains +
  material change, added **additively** (confirmed Option A ×3) — the money path, InventoryPool authority,
  Registration→Admission→Credential, Pass, VAR, §17.1 concurrency, and the Stage/Team/Series/SeatBlock subsystems are
  untouched.
- **§14.1 lifecycle.** `EventStatus.Published` stays the **authoritative registration-open state** — the 30
  `Published`-gate sites (OrderService money path, discovery, chat, reminders, gamification, analytics) are unchanged.
  Added `Scheduled`/`Live`/`Completed` states + `schedule`/`open_registration`/`go_live`/`complete` actions to
  `EventStatusWorkflow`; the existing `publish` direct path is preserved for backward compatibility.
- **§14.2 five validation gates.** Per-transition gates in `TransitionAsync`: Publish→SCHEDULED (description, venue-or-
  URL per mode, owner unit, approval-chain complete), Open-registration (≥1 Pass, ≥1 InventoryPool, currency), Go-live
  (staff assigned), Complete (results published if any Stage). The existing `publish` keeps its own readiness/org/paid
  checks and gains only the no-op-without-chain approval gate.
- **§14.3 approval chains.** `ApprovalChain` on an OrgUnit, **inherited down the tree** (Phase-4 materialised path);
  `ApprovalStep` (approver role|user, condition [always/if_paid/if_external/if_budget_gt/if_minors], SLA/escalation
  metadata); SEQUENTIAL|PARALLEL; per-event `ApprovalRequest`/`ApprovalStepDecision`; approve/reject/**bypass** (bypass
  is org-Owner/admin-only, always audited); order internal chain → platform review (§14.4, preserved) → published. The
  auto-escalation timer is deferred (metadata stored).
- **§14.5 material change.** A change to date/venue/mode after any Registration exists records the before/after in the
  audit spine, notifies every registrant (existing notification infra), and opens a refund window
  (`Event.RefundWindowEndsAt`, default 7 days or event start whichever sooner). Refunds within it stay
  **registrant-initiated via the existing RefundService** — no auto-refund, no money-path change.
- **Files Added.** `Domain/Entities/Approvals.cs` (ApprovalChain/Step/Request/StepDecision),
  `Application/Abstractions/IApprovalService.cs` (+ DTOs), `Infrastructure/Events/ApprovalService.cs`,
  `Api/Endpoints/ApprovalEndpoints.cs`, migration `AddApprovalChainsAndMaterialChange`, `Tests/LifecycleApprovalTests.cs`.
- **Files Modified.** `Domain/Enums/Enums.cs` (+3 EventStatus values, +4 approval enums), `Domain/Entities/Events.cs`
  (`RefundWindowEndsAt`), `Events/EventStatusWorkflow.cs` (+4 additive actions/transitions), `Events/EventService.cs`
  (transition gates + approval integration + material change; constructor gains `IApprovalService`),
  `Persistence/KurxDbContext.cs` (4 configs + DbSets), `DependencyInjection.cs`, `Api/Program.cs` (endpoint mapping).
  No approved entity/table was altered.
- **Database.** Additive migration `AddApprovalChainsAndMaterialChange`: `approval_chains`, `approval_steps`,
  `approval_requests` (unique `(event_id, chain_id)`), `approval_step_decisions` (unique `(request_id, step_id)`);
  column `events.refund_window_ends_at` (nullable). No column altered, no existing table touched; fully reversible.
- **API.** Additive `/v1/…` surface: `POST/GET /org-units/{id}/approval-chains`, `PATCH/DELETE /approval-chains/{id}`,
  `GET /events/{id}/approval`, `POST /approvals/decisions/{decisionId}`; plus the new lifecycle actions
  (`schedule`/`open_registration`/`go_live`/`complete`) on the existing `/events/{id}/transition`. No existing route or
  response shape changed.
- **Security.** Chain config is org Owner/Manager; a step decision requires the step's approver (role or user) or admin;
  bypass requires an org Owner or admin and is always audited. Every approval action + material change is written to the
  audit spine. The paid-event platform-review gate (§14.4) is preserved unchanged.
- **Breaking Changes.** None. Additive; `Published` semantics and all existing transitions preserved.
- **Review fixes (engineering review Option B → final verification Option A → approved).** Four required + three
  low-risk optional corrections before approval, no schema change: **H1** — approval completeness is re-evaluated on
  every gate check, adding a `Pending` decision for any step whose condition (`if_paid`/`if_external`/`if_budget_gt`/
  `if_minors`) becomes true after the request was materialised (closing the conditional-approval bypass); **H2** — a
  rejected request can be **resubmitted** into a fresh cycle (`POST /v1/events/{id}/approval/resubmit`, org Owner/
  Manager), the prior decisions preserved in the append-only audit spine; **M1** — completeness derives from the current
  decision set (not just the stored flag), so a concurrent final approval can never strand the request; **M2** — the
  concurrent request-insert race is caught and the winning request returned (no 500); **M4** — cancelling a **sub-event**
  now triggers the material-change flow (refund window + notify + audit; top-level cancellation still uses the D-101
  full-refund path); **N1** — deterministic SEQUENTIAL ordering on `(Sort, StepId)`; **N2** — a material change never
  shortens an already-open refund window. All code-only; regression tests added.
- **Verification.** `dotnet build` 0 warnings/0 errors (`-warnaserror`, Release); **full backend suite 724 passed /
  1 skipped / 0 failed** (container, real Postgres — up from 716). 8 `LifecycleApprovalTests` (5 initial + 3 review-fix):
  approval chain gates scheduling until approved (+ non-approver forbidden), bypass requires Owner + is recorded, go-live
  gate requires staff then complete progresses, open-registration gate requires a pass/pool, material change opens the
  refund window + notifies + audits, **H1 condition-becomes-true adds the step**, **H2 reject → resubmit → publish**,
  **M4 sub-event cancellation**. Backend-only — web/admin/flutter untouched.
- **Deferred as future work (outside approved Phase 14 scope — not defects).** The SLA/escalation background-timer job
  (metadata stored, future-attachable); the remaining §14.2 gate sub-checks (M3 — audience-resolves / check-in /
  walk-in-policy / certificates-queued, which lack clear data representations today); outboxing the material-change
  notifications (M5); additional approval concurrency stress tests; SCHEDULED public discovery (kept as a pre-Published
  internal state per the additive model); and the client surfaces — the organiser **lifecycle/approval console** (Web).

### Event Architecture V3 — Phase 13: Delegated & walk-in (§7.5, §7.6) (2026-07-24) — ✅ APPROVED

> **Status: APPROVED / COMPLETE.** Implemented → engineering-reviewed (Option B) → review-fixed (H1/M1/M3/M4) →
> final verification → approved. See the **Review fixes** bullet below for the corrections applied before approval.

- **Implementation Summary.** The V3 §7.5 SeatBlock (delegated registration) + §7.6 walk-in, added **additively** — the
  money path, InventoryPool authority, Registration→Admission→Credential, Pass, VAR, §17.1 concurrency, and the Stage/
  Team/Series subsystems are untouched. Per the confirmed scope (Option A ×3), **the existing `Order→Ticket` projection
  (`ProjectOrderInTransactionAsync`) remains the ONLY authoritative minting path**: both subsystems create an
  authoritative Order + Ticket(s) against a segment pool and reuse that projection — no second minting path, no nullable
  `OrderId`/`TicketId`, no change to any Phase-8/9 entity.
- **Walk-in (§7.6).** A staff `EventParticipant` (Operations class, e.g. `registration_desk`) or organiser registers an
  attendee at the gate against the `WalkIn`-segment pool via `InventoryService.TryConsumeManyAsync` (§17.1), producing
  Registration+Admission+Credential in one transaction; the admission's pool is corrected to the WalkIn pool it consumed
  so §17.1 reconciliation stays exact. Offline-safe via a **staff-scoped idempotency key** reusing the Phase-9 per-caller
  idempotency — a replayed queued walk-in returns the original order, never a duplicate.
- **SeatBlock (§7.5).** An organiser reserves `Quantity` seats for a `registrant_org_unit_id`; the funding order mints
  **N unassigned admissions** (PersonId null). The **delegate console** (`SeatBlockSeat`) binds people — assign /
  reassign / unassign / aggregate-status / incomplete-list — **governed by `AssignmentDeadline` + `ReassignLimit` + a
  per-(re)assignment audit** (§7.5 rule 3). Assignment sets the ticket holder + the admission's person and re-runs the
  projection to bind the one-per-tree Credential.
- **Delegated payment (§9.7).** Data/authz only: `PayerId` + org context on the block, FREE | DEFERRED modes; **no live
  card collection, gateway, or refund-to-org** this phase — the model is future-gateway-attachable without redesign.
- **Files Added.** `Domain/Entities/DelegatedRegistration.cs` (SeatBlock/SeatBlockSeat),
  `Application/Abstractions/IDelegatedRegistrationService.cs` (IWalkInService/ISeatBlockService + DTOs),
  `Infrastructure/Events/WalkInService.cs`, `Infrastructure/Events/SeatBlockService.cs`,
  `Api/Endpoints/DelegatedRegistrationEndpoints.cs`, migration `AddDelegatedRegistration`,
  `Tests/DelegatedRegistrationTests.cs`.
- **Files Modified.** `Domain/Enums/Enums.cs` (+`SeatBlockState`, `DelegatedPaymentMode`),
  `Persistence/KurxDbContext.cs` (2 configs + DbSets), `DependencyInjection.cs`, `Api/Program.cs` (endpoint mapping).
  **No approved Phase-8/9 entity was modified.**
- **Database.** Additive migration `AddDelegatedRegistration`: `seat_blocks` (unique `order_id`; CHECK
  `quantity >= 1 AND reassign_limit >= 0`; FKs to events/ticket_types/org_units/orders/users) and `seat_block_seats`
  (unique `admission_id`; FK to seat_blocks/admissions cascade). No column altered, no existing table touched; fully
  reversible `Down()`.
- **API.** Additive `/v1/…` surface: `POST /events/{id}/walk-ins` (staff); `POST/GET /events/{id}/seat-blocks`,
  `GET /seat-blocks/{id}`, `GET /seat-blocks/{id}/seats`, `GET /seat-blocks/{id}/status`,
  `POST /seat-blocks/seats/{seatId}/assign|unassign`. No existing route or response shape changed.
- **Security.** Walk-in requires staff (Operations participant or `event:manage`); SeatBlock create requires organiser;
  seat assignment requires the block's delegate, organiser, or admin. Reassignment is deadline- and limit-bounded;
  every (re)assignment + walk-in is audited. Idempotency prevents replayed duplicates.
- **Breaking Changes.** None. Additive.
- **Review fixes (engineering review Option B → verified → approved).** Four corrections before approval, all with
  regression tests: **H1** — a NONE-identity walk-in (both `UserId` and `GuestPhone` null) was not DB-deduped by the
  guest idempotency index (Postgres NULLs distinct), so a concurrent replay could double-register + double-consume;
  closed with an additive partial-unique index `ix_orders_walkin_idempotency` on `orders(EventId, IdempotencyKey)`
  (migration `WalkInIdempotencyIndex`) — the existing `DbUpdateException` catch returns the winner and the loser's
  consume rolls back. **M1** — reassign/unassign now revoke the previous assignee's tree credential when no non-void
  admission still references it (no orphaned Active credential; one-per-tree preserved; row kept as history). **M3** —
  seat (re)assignment takes a per-seat `pg_advisory_xact_lock` and re-reads under it, so the reassign-limit check +
  increment are atomic under concurrency. **M4** — the delegate block list computes assigned-seat counts in one grouped
  query (no N+1). No schema change beyond the additive H1 index; no API-shape change.
- **Verification.** `dotnet build` 0 warnings/0 errors (`-warnaserror`, Release); **full backend suite 716 passed /
  1 skipped / 0 failed** (container, real Postgres — up from 709). 7 `DelegatedRegistrationTests` (4 initial + 3
  review-fix): walk-in staff/pool authz + chain minting + correct pool, walk-in idempotent replay, SeatBlock mints
  unassigned admissions, delegate assign + governed reassignment + one-per-tree credential, **H1 concurrent NONE replay
  → one registration + one consume**, **M1 reassign/unassign revoke orphaned credential**, **M3 concurrent reassign
  respects the limit**. Backend-only — web/admin/flutter untouched.
- **Deferred (unchanged roadmap items).** Cross-event prerequisites (§7.3), lottery (§7.4), SPONSORED payment and live
  DEFERRED collection/gateway/refund-to-org (§9.7), and the client surfaces — the **Web delegate console** and
  **Flutter gate/offline mode** are the documented follow-on.

### Event Architecture V3 — Phase 12: Structure & series (§3, §13.2) (2026-07-24) — ✅ APPROVED

> **Status: APPROVED / COMPLETE.** Implemented → engineering-reviewed (Option B) → review-fixed (H1) → verified →
> approved. See the **Review fix** bullet below for the required correction applied before approval.

- **Implementation Summary.** The V3 §3 structural model + §13.2 EventSeries, added **additively** — the money path,
  InventoryPool authority, Registration→Admission→Credential, Pass, VAR, §17.1 concurrency, and the Stage/Fixture/Team
  subsystems are untouched. Four deliverables: (1) **AgendaItem** — the existing `EventSession` is reclassified and
  **extended** (no destructive rename) with an optional `inventory_pool_id` so it may hold a pool for a seat limit
  (§3.4 rule 3; still no Pass/Registration/Credential — config-only, seat enforcement at scan is later); (2)
  **composition depth ≤ 3** (§3.4 rule 1) enforced in the API on sub-event creation via a `ParentEventId`-chain walk;
  (3) **structural discovery** (§3.4 rule 2) — new `Event.ListedStandalone` (roots/editions discoverable by default, a
  sub-event only on opt-in) filters the shared public discovery query; (4) **EventSeries** (§13.2, RECURRING/EDITIONS)
  with RFC-5545 rrule + exception dates (validated/stored), followers and brand assets on the series, and members
  linked by `Event.SeriesId` (≤1 series per event, §3.4 rule 5) + `EditionOrdinal`/`EditionLabel`. Per-occurrence
  timezone reuses the existing `Event.Timezone`.
- **Authority note.** InventoryPool authority / Registration→Admission→Credential / Pass / AdmissionRight / VAR /
  §17.1 / deterministic scoring engine / Stage · Fixture / Team / Group + legacy mirrors are **unchanged**.
- **Files Added.** `Domain/Entities/Series.cs` (EventSeries/EventSeriesFollower),
  `Application/Abstractions/ISeriesService.cs`, `Infrastructure/Events/SeriesService.cs`,
  `Api/Endpoints/SeriesEndpoints.cs`, migration `AddStructureAndSeries`, `Tests/SeriesStructureTests.cs`.
- **Files Modified.** `Domain/Enums/Enums.cs` (+`SeriesMode`), `Domain/Entities/Events.cs` (`SeriesId`,
  `EditionOrdinal`, `EditionLabel`, `ListedStandalone`), `Domain/Entities/EventManagement.cs`
  (`EventSession.InventoryPoolId` + AgendaItem doc), `Persistence/KurxDbContext.cs` (2 series configs + Event/Session
  FKs + DbSets), `Events/EventService.cs` (depth cap, `ListedStandalone` default, discovery filter),
  `Events/ScheduleService.cs` + `IScheduleService.cs` (pool wiring), `IEventService.cs` (`ListedStandalone` opt-in),
  `DependencyInjection.cs`, `Api/Program.cs` (endpoint mapping).
- **Database.** Additive migration `AddStructureAndSeries`: `event_series` (unique `(org_id, slug)`),
  `event_series_followers` (unique `(series_id, user_id)`); columns `events.series_id`/`edition_ordinal`/
  `edition_label`/`listed_standalone`, `event_sessions.inventory_pool_id`; FKs `events → event_series` (SetNull),
  `event_sessions → inventory_pools` (Restrict). **`listed_standalone` backfills existing events to `true`** so no
  current event drops out of discovery. No column altered, no table removed; fully reversible `Down()`.
- **API.** Additive `/v1/…/series` surface (org series CRUD, public series/member reads, attach/detach member,
  follow/unfollow) + `parentEventId` depth enforcement on event create + `listedStandalone` on event update +
  `inventoryPoolId` on schedule create/update. No existing route or response shape changed (fields added, not removed).
- **Security.** Series/structure mutations reuse the org role check (Owner/Manager/Representative); series reads are
  public; every series mutation audited; depth + one-series + cross-org guards validated at the boundary.
- **Breaking Changes.** None. Additive.
- **Review fix (engineering review Option B → approved).** **H1** — the public series member list
  (`GET /v1/series/{id}/events`) previously returned every member event to anonymous callers. `ListMembersAsync` now
  threads the caller identity and reuses the org-role check (`CanManageAsync`): a manager of the series' org sees all
  members, everyone else sees only the Event subsystem's `Published && Public` set — so Draft/Private/Unlisted
  editions never leak. No schema/API-shape change; regression test added.
- **Verification.** `dotnet build` 0 warnings/0 errors (`-warnaserror`, Release); **full backend suite 709 passed /
  1 skipped / 0 failed** (container, real Postgres — up from 702). 7 `SeriesStructureTests` (6 initial + 1 review-fix):
  composition depth cap, sub-event discovery opt-in, series create authz + mode/rrule validation, one-series-per-event
  (attach/detach), follow/unfollow idempotency, AgendaItem pool link + invalid-pool, and member-list visibility
  (H1 — anonymous cannot see Draft/Private/Unlisted; managers/admins see all). Backend-only — web/admin/flutter
  untouched (structure/series client is the follow-on, §12).
- **Deferred (unchanged roadmap items).** Push-to-children inheritance (§3.5 → Phase 15), cancellation cascade +
  material change (§3.5/§14.5 → Phase 14), outbox-fed search index + RECURRING one-listing collapse (§15 → Phase 16),
  series analytics rollup (§17), RRULE auto-expansion into occurrence events, and AgendaItem seat-limit enforcement at
  scan (attendance/gate phase).

### Event Architecture V3 — Phase 11: Stages, Fixtures, Scoring (§10) (2026-07-24) — ✅ APPROVED

> **Status: APPROVED / COMPLETE.** Implemented → engineering-reviewed (Option B) → review-fixed (C1/C2/H1/H2) →
> verified → approved. See the **Review fixes** entry below for the four required corrections applied before approval.

- **Implementation Summary.** The V3 §10 **competition engine**, added **additively** (Option A both forks) over the
  approved foundations. Delivers `Stage` (§10.1 — **not registerable**: competitors arrive by advancement, spectators
  by admission; own sequence/format/mode/venue/results-visibility; audited Draft→Live→Closed), `StageParticipant`
  (the stage roster), `Fixture`/`FixtureParticipant`/`FixtureOfficial` (§10.2 — **manual scheduling with conflict
  detection**: an overlapping slot double-booking a venue, official or participant is rejected; `Walkover`/`Abandoned`
  are first-class so results are never falsified), `ScoringPolicy` (§10.3), `JudgeScore`, `PublicVote`, `StageResult`,
  `ResultCorrection`. The **deterministic scoring engine** (`ScoreAggregator`) is a pure function of stored
  scores/votes/policy: optional per-judge z-score, aggregation (sum/weighted-mean/trimmed-mean/median/Borda rank),
  min-max blend with a **capped** public-vote share, ranked with explicit tie-breaks then a deterministic subject-id
  fallback (**no tie unresolved**). **Advancement** seeds the next stage's roster from the published set (TopN /
  TopPercent / ScoreGte / Manual). Backend + API only (competitive attendee/organiser client is the follow-on, §12).
- **Scope (Option A both forks).** **Spectators (§10.4) are config-only** — `Stage.spectator_pool_id` links an
  existing `InventoryPool` for schema/validation/API; the spectator purchase flow (OBSERVER `AdmissionRight`,
  checkout, inventory consumption, payment) is **deferred** — **no money-path change**. **Statistical anomaly
  detection** (ML/behavioural/cross-event vote-fraud analytics) is **deferred** to a later analytics phase; the audit
  trail it will read is stored.
- **Authority note.** InventoryPool authority / Registration→Admission→Credential / Pass / AdmissionRight / VAR /
  §17.1 concurrency / per-caller idempotency / self-healing reconciliation / Team subsystem / Group + legacy mirrors
  are **unchanged** — Phase 11 does not touch them.
- **Fraud controls (enforceable, transactional).** Judge eligibility (Evaluation-class participant) + conflict-of-
  interest (§5.5 — no scoring your own team/self); fixture-official assignment; **one live score per (stage, judge,
  subject)** (upsert); **one immutable vote per (stage, identity)** (unique index + `DbUpdateException` race guard);
  authenticated/verified-contact identity binding; per-hour vote rate limit; scoring/voting window (stage must be
  Live). Results are **append-only once Published** — a `ResultCorrection` snapshots the prior value before any change;
  certificates and public reads see `Published`/`Corrected` only, gated by `results_visibility`.
- **Files Added.** `Domain/Entities/Competition.cs` (Stage/StageParticipant/Fixture/FixtureParticipant/
  FixtureOfficial/ScoringPolicy/JudgeScore/PublicVote/StageResult/ResultCorrection),
  `Application/Abstractions/ICompetitionService.cs`, `Infrastructure/Events/CompetitionService.cs`,
  `Infrastructure/Events/ScoreAggregator.cs`, `Api/Endpoints/CompetitionEndpoints.cs`, migration `AddCompetition`,
  `Tests/CompetitionTests.cs`.
- **Files Modified.** `Domain/Enums/Enums.cs` (+12 competition enums), `Persistence/KurxDbContext.cs` (10 tables +
  indexes), `DependencyInjection.cs`, `Api/Program.cs` (endpoint mapping).
- **Database.** Additive migration `AddCompetition` — 10 tables: `stages` (unique `(event_id, sequence)`),
  `stage_participants` (unique `(stage_id, subject)`), `fixtures`, `fixture_participants` (unique
  `(fixture_id, subject)`), `fixture_officials` (unique `(fixture_id, participant_id)`), `scoring_policies`
  (`vote_weight_cap` CHECK 0–100), `judge_scores` (**unique `(stage_id, judge, subject)`**), `public_votes`
  (**unique `(stage_id, voter)`** + rate-limit index), `stage_results` (unique `(stage_id, subject)`),
  `result_corrections`. Enums stored as text; JSON columns `jsonb`. No column altered, no existing table removed;
  fully reversible `Down()`.
- **API.** Additive `/v1/…` competition surface: stages CRUD + transition/delete, roster add/list/remove +
  seed-from-registered, scoring-policies create/list/update, fixtures create/list + state, `POST …/scores` (judge),
  `POST …/votes` (voter), results compute/publish/**GET (visibility-gated, anonymous-readable)**, dispute/correct,
  and `POST …/advance`. No existing route or response shape changed.
- **Security.** Organiser gates reuse the Phase-6 `event:manage` union; judge/voter identity + COI + window enforced
  in the service; every organiser mutation and result compute/publish/dispute/correct/advance audited. Hidden results
  read as empty (not 403) for the public when visibility is not yet met.
- **Breaking Changes.** None. Additive.
- **Verification.** `dotnet build` 0 warnings/0 errors (`-warnaserror`, Release); **full backend suite 702 passed /
  1 skipped / 0 failed** (container, real Postgres — up from 686). 16 `CompetitionTests` (12 initial + 4 review-fix):
  stage authz + auto-sequence, sequence collision, roster add/list/remove, seed-from-registered (competition teams
  only), scoring-policy validation, **fixture conflict detection** (venue/participant double-book + non-overlap
  allowed), judge-only + live-window + score upsert, **conflict-of-interest** (own team + self), **one-vote-per-
  identity** + window + invalid-subject, **deterministic ranking** + publish + visibility gating, **append-only
  correction** + **TopN advancement** seeds the next stage, HTTP stage endpoint, plus the four review-fix regressions
  (below). Backend-only change — web/admin/flutter untouched (competitive client is the follow-on, §12).
- **Review fixes (engineering review Option B → approved).** Four required corrections applied before approval, all
  with regression tests: **C1** — `ScoreAggregator.Weights()` applies the public-vote cap only when a judge component
  exists, so a vote-only stage with a cap ranks by votes instead of collapsing to an all-tied result; **C2** —
  `TeamService.Merge/Split` now also block once any Stage scoring has begun for a team (`ScoringHasBegunAsync`),
  enforcing V3 §6.4 (previously guarded only on `Competing` state); **H1** — `ComputeResultsAsync` loads roster/
  scores/votes with `OrderBy(Id)` so recomputation is byte-identical regardless of DB row order; **H2** — new additive
  migration `RestrictScoreVoteFks` changes `judge_scores → event_participants` and `public_votes → users` FKs from
  `Cascade` to `Restrict` (immutable scoring/vote evidence; whole-stage deletion still cascades via `stage_id`).
- **Deferred (unchanged roadmap items).** Spectator purchase flow (OBSERVER admission over `spectator_pool_id`),
  statistical/ML vote-fraud anomaly detection, automated scoring sources, the competitive attendee/judge client UI,
  and the fixture bracket **solver** (V3 defers the auto-scheduler — manual scheduling + conflict detection ships).

### Event Architecture V3 — Phase 10: Teams (retires `Group`) (2026-07-24) — ✅ APPROVED

- **Implementation Summary.** The V3 §6 **Team subsystem** — the only group entity, and only where competition
  exists — added **additively**. Per the approved scope: (1) **Group is NOT removed** — `Group`/`GroupMember`/
  `RegistrationMode.Group` stay as legacy compatibility mirrors and the party-booking flow + the **approved Phase 9
  money path are untouched**; (2) **formation only** — team-slot inventory and the team-registration purchase flow
  (§6.5) are deferred to the competitive-purchase phase. Delivers `Team` (identity + FORMING→COMPLETE→LOCKED→
  COMPETING→{ELIMINATED|DISQUALIFIED|WITHDRAWN|FINALIST}, audited transitions), `TeamMembership` (roles
  CAPTAIN/CO_CAPTAIN/MEMBER/SUBSTITUTE/MENTOR; states INVITED/REQUESTED/ACTIVE/REPLACED/REMOVED/LEFT; substitution
  is an **edge, not a delete** — `replaced_by_membership_id`), `TeamInvite`, `TeamJoinRequest`, and `TeamPolicy`
  (§6.3, on the existing `teams` capability); invites/join-requests/approvals; **merge/split** as organiser-only
  guarded composite transactions blocked once a team is Competing (§6.4); every transition audited. Existing
  competition `Group`s are backfilled into Teams at startup. Backend + API (client UI is the follow-on, as in
  Phases 7–9 — no placeholder pages added).
- **Authority note.** InventoryPool / Registration→Admission→Credential / Pass / AdmissionRight / VAR / §17.1 /
  per-caller idempotency / self-healing reconciliation are **unchanged** — Phase 10 does not touch them.
- **Files Added.** `Domain/Entities/Teams.cs` (Team/TeamMembership/TeamInvite/TeamJoinRequest/TeamPolicy),
  `Application/Abstractions/ITeamService.cs`, `Infrastructure/Events/TeamService.cs`, `Api/Endpoints/TeamEndpoints.cs`,
  migration `AddTeams`, `Tests/TeamTests.cs`.
- **Files Modified.** `Domain/Enums/Enums.cs` (+8 team enums), `Persistence/KurxDbContext.cs` (5 tables + indexes),
  `Events/TicketTypeService.cs` (sync TeamPolicy for competition types), `DependencyInjection.cs`, `Api/Program.cs`
  (endpoint mapping + Group→Team backfill).
- **Database.** Additive migration `AddTeams`: `teams` (unique `(event_id, slug)`), `team_memberships`
  (partial-unique active `(team_id, person_id)`), `team_invites` (unique token), `team_join_requests`
  (partial-unique pending `(team_id, person_id)`), `team_policies` (unique `ticket_type_id`, size CHECK). No column
  altered; no existing table removed (Group retained). Full reversible `Down()`.
- **API.** Additive `/v1/…/teams` surface (create/list/get/update, invite/accept/revoke, join-request/decide,
  leave/remove, substitute, organiser transition/merge/split, `GET /v1/me/teams`) + per-ticket-type team-policy
  read/set. No existing route or response shape changed.
- **Security.** Formation acts as the caller; captain/co-captain gates and organiser gates (the Phase-6
  `event:manage` union) live in the service; disqualification requires a reason; every lifecycle transition audited.
- **Breaking Changes.** None. Additive; Group retained.
- **Verification.** `dotnet build` 0 warnings/0 errors (`-warnaserror`); **full backend suite 686 passed / 1
  skipped / 0 failed** (container, real Postgres). 17 new `TeamTests`: TeamPolicy sync + validation/authz, create/
  captain, non-competition rejection, invite→accept→Complete, join-request→decide, team-full, one-team-per-person,
  leave/remove (history preserved), substitution-as-an-edge, organiser lock→disqualify(reason), merge (tombstone +
  members moved), split (new team), merge-forbidden-once-competing, non-captain-cannot-invite, HTTP create + `/me/
  teams`. web + admin typecheck/lint/build green; `flutter analyze` clean (0 errors) + `flutter test` 216 passed
  (backend-only change — clients unaffected; team UI is the follow-on). A first run had 1 failure (team auto-
  `Complete` state miscounted a still-tracked new member); fixed by recomputing the derived state after the roster
  save.
- **Deferred (unchanged roadmap items).** Team-slot inventory (§8.1 `unit=team_slot`) and the team-registration
  purchase flow (§6.5, "1 team-slot + N person-slots"), Group table/flow **removal** (a later strangler phase),
  Stages/fixtures/scoring (Phase 11), and the client team-formation UI.

### Event Architecture V3 — Phase 9: Authority cut-over — Passes, VAR, §17.1 concurrency (2026-07-23) — ✅ APPROVED

> **Status: APPROVED / COMPLETE.** Implemented → engineering-reviewed → review-fixed → verified → approved. This is
> the current authority for the money path. (Older Phase 3/7/8 entries below are point-in-time records: where they
> say "the cut-over is Phase 9", that cut-over is this entry, now done.) Next: Phase 10 (Teams) — not started.

- **Implementation Summary.** The Wave-C **authority cut-over** (Option A, immediate flip — dev data only, roadmap
  B-4). The new model becomes authoritative and the money path is rebuilt on the V3 §17.1 contract: **inventory
  pools** are the oversell authority via a **conditional decrement** (`consumed = consumed + n WHERE
  consumed + held + n <= total + oversell_allowance`), never the naive `TicketType.Sold += 1`; the **registration
  → admission → credential** chain is produced **in the money transaction** (§6.1) so the admission never lags the
  seat; and a **Pass + AdmissionRight(SINGLE)** (the §9.2 product) plus an immutable **VAR** (§9.5) per order line
  are written at purchase — the VAR being the sole basis for refunds. Order / Ticket / `TicketType.Sold` remain as
  **legacy mirrors** (still written, no longer authoritative); nothing is removed.
- **Authority changes.** Oversell authority `TicketType.Sold` → `inventory_pools.Consumed` (conditional decrement);
  registration/admission/credential shadow → authoritative, created in-transaction; `Pass`/`AdmissionRight`/`VAR`
  introduced as the authoritative product + revenue records; reconciliation flips from `Consumed == Sold` to
  `Consumed == count(active admissions)`.
- **Concurrency guarantees (§17.1).** No oversell (conditional decrement under the pool row lock); **reserve-first**
  holds (paid orders hold; capture converts held→consumed; expiry/refund release); **deterministic lock ordering**
  (multi-pool draws sorted ascending by pool id); **client idempotency** (`Idempotency-Key` header → one order per
  **caller** per event — scoped to the authenticated user, or a guest's phone); **duplicate-callback safety**
  (atomic Pending→Paid claim); **refund idempotency** (atomic Paid→Refunded claim); a **per-person credential
  advisory lock** so two concurrent same-person orders both commit and share the one credential; the DB enforces
  pool non-negativity. Non-money side effects (chat membership) publish through the **transactional outbox**, never
  inline.
- **Files Changed.** New: `Domain/Entities/Passes.cs` (Pass/AdmissionRight/VAR), migration
  `AddPassesVarConcurrency`. Modified: `Enums.cs` (+3 enums), `Domain/Entities/{Orders,Operational}.cs`
  (Order.IdempotencyKey, SeatHold.PoolId), `KurxDbContext.cs` (3 tables + idempotency/refund/CHECK indexes),
  `IInventoryService`/`InventoryService` (conditional decrement/hold/release + reconcile flip),
  `IEventRegistrationService`/`EventRegistrationService` (in-transaction projection, VAR, Pass sync, advisory lock,
  backfill), `Orders/OrderService.cs` (transactional money path), `Orders/RefundService.cs` (VAR-based + atomic
  claim), `Events/TicketTypeService.cs` (Pass sync), `Jobs/{OutboxDispatchJob,ExpireSeatHoldsJob,InventoryReconciliationJob}.cs`,
  `Api/Endpoints/OrderEndpoints.cs` (idempotency header), `Api/Program.cs`, `IOrderService.cs`.
- **Database.** Additive migration `AddPassesVarConcurrency`: `passes`, `admission_rights`, `var_lines` tables;
  `orders.IdempotencyKey`; `seat_holds.PoolId` (+FK); `inventory_pools` non-negativity CHECK. Migration
  `ScopeOrderIdempotencyAndRefundIndex` (review-fix): per-caller partial-unique idempotency indexes
  (`(event, user, key)` and `(event, guest_phone, key)`) and `refunds.OrderId` reverted to non-unique. No column
  dropped; full reversible `Down()`.
- **Review-fixes (post engineering review).** Additive, no architecture redesign: **(P1/H1)** client idempotency is
  now **scoped to the caller** (authenticated user, or guest phone) in both the lookup and the unique index, so one
  caller's key can never retrieve another's order or leak a `GuestAccessToken`. **(P2/H2)** the authority cut-over is
  completed on the read side — availability now resolves from the pool everywhere (`WaitlistService` sold-out gate,
  `TicketTypeService` public/org availability + `quantity_below_sold`/delete guards, `AnalyticsService` cap/sold/
  available, the order sold-out pre-check) via `InventoryService.AvailableAsync`/`PoolCountsAsync`, never the legacy
  `TicketType.Sold` mirror. **(P3/M1)** `refunds.OrderId` reverted to non-unique — the atomic Paid→Refunded claim is
  the real full-refund idempotency, and its absence keeps V3 §9.6 partial refunds open. **(P4/M2)** the inventory
  reconciliation job now **self-heals** (`RepairAsync` sets a drifting pool's `Consumed` to its active-admission
  ground truth). **(P5/M3)** a late capture after hold expiry now **consumes unconditionally** (honours the paid
  ticket, §9) so `Consumed == active admissions` always holds.
- **API.** Additive: `POST /v1/events/{id}/orders` honours a standard `Idempotency-Key` header (and a body field),
  scoped per caller. No existing route or response shape changed.
- **Authorization.** Unchanged — Pass sync/reads ride the existing ticket-type management authz; the money path is
  the existing order flow. No parallel authorization model.
- **Docs.** `DATABASE_TABLES.md`, `api/README.md`, `architecture/overview.md`, `V3_IMPLEMENTATION_ROADMAP.md`
  (Phase 9 → COMPLETE), `roadmap/README.md`.
- **Breaking Changes.** None. Additive; Order/Ticket/TicketType retained and still written as legacy mirrors.
- **Verification.** `dotnet build` 0 warnings/0 errors (`-warnaserror`); **full backend suite 664 passed / 1
  skipped / 0 failed** (container, real Postgres). New Phase 9 tests: oversell-under-concurrency, oversell-
  allowance, client-idempotency, duplicate-capture, concurrent-captures, deterministic multi-pool lock ordering
  (InventoryTests); Pass+AdmissionRight creation, in-transaction admission, VAR = price, VAR-derived refund,
  pass/VAR backfill (EventRegistrationTests); the Phase-7 reconciliation tests re-targeted to the `Consumed ==
  active admissions` invariant and the hold semantics. A first run surfaced 7 failures, all one root cause —
  tests that seed a `TicketType` directly (bypassing `TicketTypeService`) had no pool, which the new money path
  requires; fixed by making the path **self-heal a missing pool** (`EnsureGeneralPoolAsync`), also more robust in
  production. web + admin typecheck/lint/build green; `flutter analyze` clean (0 errors) + `flutter test` 216
  passed. Flutter/web/admin are outside this backend-only change's surface.
- **Assumptions / Deferred.** Option A — **SINGLE scope only**. Multi-scope Passes (Subtree/Set/Query), day-pass
  `window_policy`, cross-event VAR allocation (weight/equal-share), TEAM/ORG_UNIT subjects, delegated/walk-in,
  lottery/prerequisite, gate validation, and no-show release automation all remain deferred (enum vocabulary
  stored, never created). Legacy-mirror **removal** is a later phase, not Phase 9. The legacy `TicketType.Sold`
  mirror may undercount under high concurrency (non-atomic increment); it is non-authoritative and the pool is
  race-free, so the pre-checks it feeds stay loose-but-safe (the conditional decrement is the real guard).

### Event Architecture V3 — Phase 3: Money (2026-07-22)

- **Implementation Summary.** Landed the money **currency dimension** (V3 §9.1 / §21.1) additively — the
  expensive-to-retrofit schema shape, INR-only for now. Existing `*_paise` amounts (D-004) untouched; no
  existing API/behaviour/client changed. Multi-currency settlement on one event is deliberately out of scope.
- **Files Changed.** New: `Domain/Money.cs` (the `Money` value type), migration `AddMoneyCurrency`,
  `Tests/MoneyCurrencyTests.cs`. Modified entities (`+Currency`): `Orders.cs` (Order/OrderItem/Refund),
  `Money.cs` (Transfer/LedgerEntry/Withdrawal), `Events.cs` (TicketType), `Orgs.cs` (OrganizationWallet,
  PayoutSchedule), `Analytics.cs` (EventAnalyticsDaily, OrganizationAnalytics); `+SettlementCurrency`:
  `Events.cs` (Event), `Orgs.cs` (Organization). Also `KurxDbContext.cs` (one uniform currency-default rule),
  `EventService.cs` (bind settlement currency from org on create; inherit on clone), `IEventService.cs` +
  `EventEndpoints.cs` (expose `settlement_currency` on event detail).
- **Database.** Additive migration `AddMoneyCurrency`: `currency` (`varchar(3)`, default `INR`) on orders,
  order_items, ticket_types, refunds, transfers, ledger_entries, withdrawals, organization_wallet,
  payout_schedules, event_analytics_daily, organization_analytics; `settlement_currency` (same) on events
  and organizations. Existing rows default to INR; no column altered.
- **API.** Additive: event detail now returns `settlement_currency`. No existing field changed or removed.
- **Docs.** `DATABASE_TABLES.md`, `api/README.md`, `architecture/overview.md`, `V3_IMPLEMENTATION_ROADMAP.md`
  (§0 + §11 → Phase 3 COMPLETE), `roadmap/README.md`.
- **Breaking Changes.** None. Additive.
- **Review hardening (2026-07-22).** Closed the currency-coherence gap: every money row created through an
  event flow (order, order_item, ticket_type, refund, ledger entry incl. the capture ledger, plus event
  analytics and org wallet/payout) is now stamped with the owning event/org currency, not an independent INR
  default. Added `currency` to the money-showing responses (org events revenue, event analytics gross, wallet,
  ledger entries). Added `Money.IsValidCurrency` (ISO-4217 format) and validate at the org→event bind (an
  invalid code falls back to INR). Collapsed the repeated `"INR"` literals to `Money.DefaultCurrency`.
- **Verification.** `dotnet build` 0 errors; **8 money tests** (incl. an end-to-end
  Org→Event→TicketType→Order→OrderItem→Refund→Ledger USD-coherence chain, a default-INR order, and the
  invalid-currency fallback) + event/capability + money-path (orders/refunds/ledger/wallet) regression green;
  web + admin typecheck/lint/build green; `flutter analyze`/`test` unchanged (the pre-existing
  `guest_browse_test.dart` failure is unrelated). Full local suite B-5-flaky — CI is the authority.
- **Assumptions.** INR-only today (the degenerate case per §21.1); the `Money` type and currency columns are
  the foundation Wave C (Passes/VAR) consumes. New money rows default to INR (correct for India); threading a
  non-INR value end-to-end is future work when multi-currency events exist.
- **Remaining Work.** Phase 4 (OrgUnit tree) — **complete** (below).

### Event Architecture V3 — Phase 8: Registration layer (2026-07-23)

- **Implementation Summary.** Added the registration → admission → credential chain + the five-axis
  `RegistrationPolicy` (V3 §7) as an **additive dual-write shadow** of the authoritative Order/Ticket — the
  second Wave-C step. One policy per ticket type (the Pass analog), derived from current config and organiser-
  settable; each order projects a `Registration` (the act), an `Admission` per ticket (the right, linked to the
  Phase-7 pool), and one `Credential` **per person per event tree** (the artifact, keyed on the tree root —
  V3 §9.3). The projection runs **post-commit, never inline** (§17.1) on **its own DbContext** (a fresh scope,
  isolated from the request), so a projection failure can never break — or 500 — the committed order; a daily
  reconciliation job proves the shadow matches the authority and **self-heals** drift by re-projecting the
  affected orders. **Order/Ticket stay authoritative** — the cut-over is Phase 9. Backend + API only.
- **Review-fixes (post engineering review).** Additive, no architecture redesign: **(P1)** the post-commit
  projection now resolves its own `KurxDbContext` from a fresh `IServiceScopeFactory` scope — never the request's
  scoped context — so a projection failure cannot dirty the request context or turn a committed order into a 500.
  **(P2)** the `Credential` is keyed on the **event-tree root** (`Event.ParentEventId ?? Id`) rather than the raw
  event, so a person spanning a fest and its sub-events holds exactly one credential (V3 §9.3); no schema change —
  the existing `(event_id, person_id)` partial-unique index now enforces per-**tree**. **(P3)** reconciliation
  gained a credential-consistency check and a `RepairAsync` that re-projects drifting/missing orders, so the
  daily job **heals** without waiting for a restart backfill. **(P4)** the credential-revocation pass is now
  **set-based** (3 queries, was N+1); per-order projection contexts keep backfill change-tracking bounded.
  **(P5)** 5 adversarial tests: concurrent same-person orders + credential race, duplicate/retry projection,
  reconciliation repair of a missing projection, concurrent (replay-safe) refunds, one credential across a
  fest + sub-event. Every one asserts Order/Ticket stay authoritative and the shadow re-synchronises.
- **Files Changed.** New: `Domain/Entities/Registration.cs` (4 entities), `Application/Abstractions/IEventRegistrationService.cs`,
  `Infrastructure/Orders/EventRegistrationService.cs`, `Infrastructure/Jobs/RegistrationReconciliationJob.cs`,
  `Api/Endpoints/EventRegistrationEndpoints.cs`, migration `AddRegistrationLayer`, `Tests/EventRegistrationTests.cs`.
  Modified: `Enums.cs` (+8 enums), `KurxDbContext.cs`, `DependencyInjection.cs`, `Program.cs` (backfill + job);
  the policy sync hooks into `TicketTypeService` (create/update) and the shadow projection into `OrderService`
  (4 flows) + `RefundService`, all post-commit and best-effort.
- **Database.** Additive migration `AddRegistrationLayer`: `registration_policies` (unique `ticket_type_id`),
  `registrations` (unique `order_id`), `admissions` (unique `ticket_id`, FKs → registration/ticket/pool/
  credential), `credentials` (**partial-unique** `(event_id, person_id)` — one per person per event tree). No
  column altered. The whole chain is backfilled from existing orders at startup (idempotent).
- **API.** Additive: `GET/PATCH /v1/orgs/{orgId}/events/{eventId}/ticket-types/{ttId}/registration-policy`
  (the five axes) and `GET /v1/orgs/{orgId}/events/{eventId}/registrations`. No existing route or response
  changed — the order/refund/ticket flows are byte-for-byte identical (the shadow is post-commit).
- **Authorization.** Policy read/write + registration reads reuse the **Phase-6 event-permission union**
  (`event:manage`) — no parallel model.
- **Docs.** `DATABASE_TABLES.md`, `api/README.md`, `architecture/overview.md`, `V3_IMPLEMENTATION_ROADMAP.md`
  (Phase 8 → COMPLETE; Next → Phase 9), `roadmap/README.md`.
- **Breaking Changes.** None. Additive; Order/Ticket unchanged and authoritative.
- **Verification.** `dotnet build` 0 warnings/0 errors (`-warnaserror`); **full backend suite 653 passed / 1
  skipped / 0 failed** (container-run — the only honest verifier here per B-5; 17 `EventRegistrationTests`: the
  12 chain/policy/reconciliation/backfill tests plus, from the review-fix, 5 adversarial ones — concurrent
  same-person orders + credential race, duplicate/retry projection, reconciliation repair of a missing
  projection, concurrent replay-safe refunds, one credential across a fest + sub-event — each asserting
  Order/Ticket stay authoritative and the shadow re-synchronises); web + admin typecheck/lint/build green;
  `flutter analyze` clean (0 errors) and `flutter test` 216 passed. Flutter/web/admin are outside this backend-
  only change's surface.
- **Assumptions.** (1) **Dual-write shadow, Order/Ticket authoritative** — the §17.1 cut-over that makes the
  registration chain authoritative is **Phase 9**, not pulled forward. (2) **Post-commit projection** (best-
  effort, its own transaction) rather than inline — §17.1 requires admission side-effects "never inline";
  reconciliation + startup backfill close any gap. (3) **Subject is Person** — a party/group booking is one
  Registration with N Admissions (§6.1); `TEAM`/`ORG_UNIT`/`EXTERNAL_ORG` are stored axis values, activated in
  Phase 10+. (4) **Gate/allocation values needing later subsystems** (LOTTERY draw §7.4, PREREQUISITE eval
  §7.3, DELEGATED §7.5, walk-in §7.6) are stored-but-not-enforced; the authoritative gate stays the order path
  + Phase-5 audience rules until cut-over. (5) `Pass`/`AdmissionRight`/VAR (§9) are Phase 9. (6) The derived
  policy axes reflect creation-time config; the organiser owns them thereafter (the order path reads
  `TicketType` directly, so staleness is inert this phase). No new D-NNN — implements V3 §7.
- **Remaining Work.** Phase 9 (Passes, VAR, §17.1 concurrency contract) makes both the inventory pools and the
  registration chain authoritative and adds the Pass/AdmissionRight product layer.

### Event Architecture V3 — Phase 7: Inventory pools (2026-07-23)

- **Implementation Summary.** Added `InventoryPool` (V3 §8) as an **additive dual-write shadow** of the scalar
  `TicketType.Quantity/Sold` — the first, lowest-risk step of Wave C. One `general` / `InPerson` / `PersonSlot` /
  `Event` pool is minted per TicketType; its `Total`/`Consumed` are kept in lock-step with the scalar at every
  sale, refund, and hold-expiry, committed atomically with the change they mirror. The waitlist is re-pointed
  onto pools (§8.5); a reconciliation job proves `consumed == count(active admissions)` and alerts on drift
  (§17.1). **The scalar remains the oversell authority this phase** — the §17.1 conditional-decrement contract
  that makes pools authoritative is Phase 9. Backend + API only.
- **Files Changed.** New: `Domain/Entities/Inventory.cs` (`InventoryPool`), `Application/Abstractions/IInventoryService.cs`,
  `Infrastructure/Ticketing/InventoryService.cs`, `Infrastructure/Jobs/InventoryReconciliationJob.cs`,
  `Api/Endpoints/InventoryEndpoints.cs`, migration `AddInventoryPools`, `Tests/InventoryTests.cs`. Modified:
  `Enums.cs` (+`InventoryScope`/`Segment`/`Channel`/`Unit`, `NoShowPolicy`), `Events.cs` (`TicketWaitlist.PoolId`),
  `KurxDbContext.cs`, `DependencyInjection.cs`, `Program.cs` (backfill + reconciliation job); dual-write wired
  into `TicketTypeService` (create/update), `OrderService` (4 sale sites), `RefundService`, `ExpireSeatHoldsJob`,
  and `WaitlistService` (pool pointer).
- **Database.** Additive migration `AddInventoryPools`: `inventory_pools` (FK → events/ticket_types CASCADE;
  partial-unique `(ticket_type_id, segment)` for the general pool; jsonb release/waitlist config) + a nullable
  `ticket_waitlist.pool_id` FK. No column altered. Pools + waitlist re-pointing are backfilled at startup.
- **API.** Additive: `GET /v1/orgs/{orgId}/events/{eventId}/inventory` and
  `PATCH …/ticket-types/{ttId}/inventory` (set oversell / no-show / release / waitlist policy). No existing
  route or response changed.
- **Authorization.** Inventory read/policy reuse the **Phase-6 event-permission union** (`event:manage`) — no
  parallel model. This is the first production consumer of `IEventPermissionService`.
- **Docs.** `DATABASE_TABLES.md`, `api/README.md`, `architecture/overview.md`, `V3_IMPLEMENTATION_ROADMAP.md`
  (Phase 7 → COMPLETE; Next → Phase 8), `roadmap/README.md`.
- **Breaking Changes.** None. Additive; the scalar path is unchanged and remains authoritative.
- **Review fixes (2026-07-23).** (1) **Reconciliation corrected** — it now proves the Phase-7 shadow invariant
  `pool.Consumed == TicketType.Sold` (the authority it mirrors), not `== count(active admissions)` which
  false-alarmed on every held-but-uncaptured paid order (Consumed mirrors Sold = held + issued). Single query,
  no false positives. (2) **Money-path tests added** (see Verification). (3) **Semantics clarified** in the
  entity/service/docs: Phase-7 `Consumed` mirrors `Sold` and is not yet V3's scanned-`Consumed`; Held/Allocated/
  Consumed become independent in the Phase-9 cut-over. (4) **Self-heal path documented** as architecturally
  unreachable (a new type mints its pool atomically; a failed startup backfill re-throws before the app serves,
  so no sale can reach a pool-less type — the concurrent-first-sale unique race cannot occur); behaviour unchanged.
- **Verification.** `dotnet build` 0 errors; **full backend suite 636 passed / 1 skipped / 0 failed**
  (container-run — the only honest verifier here per B-5; **16 `InventoryTests`** incl. 7 review-fix tests:
  paid-held-no-drift, concurrent purchase, concurrent refund, duplicate refund, duplicate hold-expiry,
  failed-order-touches-neither, backfill idempotency — all asserting `pool.Consumed == TicketType.Sold`);
  web + admin typecheck/lint/build green. `flutter analyze`/`test` unchanged by this phase (Flutter is not in
  Phase 7's surface; the `guest_browse_test.dart` failure is the pre-existing one).
- **Assumptions.** (1) **Dual-write shadow, scalar authoritative.** Pools mirror `Sold`; oversell is still
  prevented by the scalar. The §17.1 lock-order + conditional-decrement + idempotency contract that makes pools
  authoritative is **Phase 9** — not pulled forward. (2) **`Held`/`Allocated`** stay 0 (the hold lifecycle
  re-points onto pools in Phase 9); Phase 7 tracks `Total`/`Consumed`. (3) **release_policy / no_show_policy /
  oversell / waitlist config** are **stored** on the pool and settable, but the release/no-show **automation**
  (which moves inventory between pools) is Phase 9. (4) **Only the `general` segment** is minted; VIP / quota /
  guest / accessible segments, `team_slot`, `Virtual` channel, and non-event scopes are the affordances later
  phases fill in. (5) The dual-write mirrors the scalar's read-modify-write concurrency (Phase 9's conditional
  decrement fixes both). No new D-NNN — implements V3 §8.
- **Remaining Work.** Phase 8 (Registration layer) is next; Phase 9 makes pools authoritative (§17.1 contract).

### Event Architecture V3 — Phase 6: Participants (2026-07-22)

- **Implementation Summary.** Added the participant model (V3 §5) that unifies V2's three overlapping systems
  (EventAssignment free-text roles, capability people-lists, org RBAC). A platform `ParticipantRole` registry
  (7 hardcoded classes, ~30 slugs) carries the role properties §5.2 needs (`counts_toward_capacity`,
  `inventory_segment`, `is_public`, `default_permissions`); `EventParticipant` records a Person/OrgUnit
  participating in a capacity (Team subject is stored-for-Phase-10), with state / scope / visibility. The §5.4
  permission union adds participant grants as a **fourth, event-scoped source** — an event ORGANISER
  participant can manage the event even without an org membership. V2 `EventAssignment` is untouched and
  backfilled from. Backend + API only.
- **Files Changed.** New: `Domain/Entities/Participants.cs` (`ParticipantRole`, `EventParticipant`),
  `Infrastructure/Events/{ParticipantRoleCatalog,ParticipantRoleSeeder,ParticipantService,EventPermissionService}.cs`,
  `Application/Abstractions/{IParticipantService,IEventPermissionService}.cs`, `Api/Endpoints/ParticipantEndpoints.cs`,
  migration `AddParticipants`, `Tests/ParticipantTests.cs`. Modified: `Enums.cs` (+`ParticipantClass`,
  `ParticipantState`, `ParticipantSubjectType`, `ParticipantVisibility`), `KurxDbContext.cs`,
  `DependencyInjection.cs`, `Program.cs` (seed platform roles + backfill from assignments + map endpoints).
- **Database.** Additive migration `AddParticipants`: `participant_roles` (partial-unique platform `slug`,
  jsonb `default_permissions`/`default_access_zones`) and `event_participants` (FK → events CASCADE, unique
  `(event_id, subject_type, subject_id, role_slug)`, jsonb `scope`). No column altered. Backfill runs at
  startup (idempotent), not in the migration.
- **API.** Additive: `GET /v1/participant-roles`; `POST/GET /v1/orgs/{orgId}/events/{eventId}/participants`,
  `DELETE …/participants/{id}`; `POST /v1/participants/{id}/respond`; `GET /v1/me/participations`. The legacy
  `event_assignments` endpoints are unchanged.
- **Authorization.** New §5.4 fourth grant source (participant grants), **event-scoped, evaluated live** —
  resolved by `IEventPermissionService` and consulted by the participant surface. Existing per-service org
  authz is **not** rewired; a participant grant never leaks to org level.
- **Docs.** `DATABASE_TABLES.md`, `api/README.md`, `architecture/overview.md`, `V3_IMPLEMENTATION_ROADMAP.md`
  (Phase 6 → COMPLETE; Next → Phase 7), `roadmap/README.md`.
- **Breaking Changes.** None. Additive; `EventAssignment` and its clients keep working.
- **Review fixes (2026-07-22).** (1) **Anti-amplification (§5.4)** — `AssignAsync` now refuses to confer an
  *authority* permission (`participants:manage`, `event:manage`) the actor doesn't already hold, so an event
  ORGANISER participant with only `participants:manage` can't mint owners/managers and self-escalate; functional
  grants (e.g. a judge's `scoring:submit`) remain freely appointable. (2) **Backfill** made set-based (one key
  query, in-memory dedup) instead of per-row N+1 — same idempotent, additive behaviour. (3) **Coexistence
  (Priority 2):** V3 §5.1/§21.2 intends `EventAssignment` to be *replaced and retired* (strangler), not
  runtime-synchronised — `EventParticipant` is authoritative for the V3 model + §5.4 permissions, the legacy
  surface conferred no permissions and is backfilled once at startup; the divergence during the window is
  benign and left unchanged, now documented. (4) **Scope (Priority 3):** the roadmap Phase 6 line does not
  require subtree permission propagation; exact-event matching satisfies every §5.4 rule (never org-level,
  never flows up), so `scope` (and `visibility`) are documented as **stored-for-later** — their sub-event/
  stage/agenda consumption lands with those phases (11/12).
- **Verification.** `dotnet build` 0 errors; **full backend suite 620 passed / 1 skipped / 0 failed**
  (container-run — the only honest verifier here per B-5; 16 `ParticipantTests` incl. 6 review-fix tests:
  coordinator-can't-assign-manager/owner, can't self-escalate, owner-can-assign-organiser, removed-coordinator-
  loses-grant, pending/declined-no-grant, multiple-roles); web + admin typecheck/lint/build green.
  `flutter analyze`/`test` unchanged by this phase (Flutter is not in Phase 6's surface; the
  `guest_browse_test.dart` failure is the pre-existing one).
- **Assumptions.** (1) **COI (§5.5) is deferred to Phase 11** — its home is `ScoringPolicy.conflict_rules`,
  and the relations (SAME_ORG_UNIT/MENTOR_OF/…) evaluate evaluator-vs-subject, but subjects (teams being
  scored) don't exist until Teams (Phase 10) / Stages (Phase 11). The participant substrate it will use is now
  in place. (2) **EventAssignment is kept and backfilled**, not deleted — strangler parallel-run; the two
  coexist, no dual-write. (3) **Team subjects** are stored-for-Phase-10 (assignment accepts Person/OrgUnit
  only today). (4) **Org-extensible custom slugs** (§5.3) — the schema affords it (`ParticipantRole.OrgId`),
  but the write path is deferred; `custom_label` covers ad-hoc naming now. (5) **`counts_toward_capacity` /
  `inventory_segment`** are exposed as role properties; wiring them into capacity accounting is Phase 7
  (inventory pools). (6) The §5.4 resolver is consulted by the participant surface but the ~15 legacy
  per-service `RoleAsync` checks are **not** rewired (out of scope; no behavioural change to existing authz).
  No new D-NNN — implements V3 §5.
- **Remaining Work.** The Web surface for participant management (roadmap lists Web for Phase 6) is the
  remaining Phase 6 client work over the now-stable API. Phase 7 (Inventory pools) is next.

### Event Architecture V3 — Phase 5: Audience rules & membership attributes (2026-07-22)

- **Implementation Summary.** Added the audience / eligibility subsystem (V3 §4.4) — who may REGISTER for an
  event, evaluated server-side, **DENY BY DEFAULT** when a rule exists and **open (backward compatible)** when
  none does. A per-event `AudienceRule` carries the predicate (unit subtree, role, cohort year, attribute
  matches, require-verified, external-orgs, guests, applies_to); memberships gain `attributes` (cohort_year, …)
  and `source`. Eligibility is enforced at **registration** (order creation) and **re-checked at admission**
  (the gate flags an ineligible holder for the organiser, never silently voids or admits). Backend + API only.
- **Files Changed.** New: `Domain/Entities/Audience.cs` (`AudienceRule`), `Application/Abstractions/IAudienceService.cs`,
  `Infrastructure/Audience/AudienceService.cs`, `Api/Endpoints/AudienceEndpoints.cs`, migration `AddAudienceRules`,
  `Tests/AudienceRuleTests.cs`. Modified: `Enums.cs` (+`MembershipSource`, +`AudienceAppliesTo`), `Orgs.cs`
  (`Membership.AttributesJson` + `Source`), `KurxDbContext.cs` (map both + `memberships.attributes` jsonb),
  `DependencyInjection.cs`, `Program.cs`, `OrderService.cs` (registration gate), `GateEntryService.cs` +
  `IGateEntryService.cs` (admission re-check + `CheckInResult.EligibilityFlag`), `OrderEndpoints.cs` +
  `GateEndpoints.cs` (surface `not_eligible` 403 / `eligibility_flag`).
- **Database.** Additive migration `AddAudienceRules`: new `audience_rules` table (one per event, FK → events
  CASCADE, unique `event_id`, jsonb predicate columns); `memberships.attributes` (jsonb) + `memberships.source`
  (text, existing rows backfilled to `SelfDeclared`). No column altered.
- **API.** Additive: `GET/PUT/DELETE /v1/orgs/{orgId}/events/{eventId}/audience`,
  `PATCH /v1/orgs/{orgId}/members/{membershipId}/attributes`, `GET /v1/events/{eventId}/eligibility`. Existing
  order creation now returns **403 `not_eligible`** when an audience rule denies the caller; the gate scan
  response gains `eligibility_flag`. No existing field removed.
- **Authorization.** Rule management + member-attribute writes reuse the same org-level Owner/Manager/
  Representative check every event service uses — the existing organiser RBAC is **not** rewired. Eligibility
  is a separate registration gate (deny-by-default), not a change to who may manage.
- **Docs.** `DATABASE_TABLES.md`, `api/README.md`, `architecture/overview.md`, `V3_IMPLEMENTATION_ROADMAP.md`
  (Phase 5 → COMPLETE; Next → Phase 6), `roadmap/README.md`.
- **Breaking Changes.** None. Additive; events without a rule behave exactly as before.
- **Review fixes (2026-07-22).** (1) **Closed every registration bypass** — the audience gate was only on
  `CreateOrderAsync`; it now runs through one shared `AudienceDenialReasonAsync` → `EvaluateAsync` on every
  ticket-issuing path: `AddMemberToGroupAsync` (covers both `JoinGroupAsync` and `AcceptGroupInvitationAsync`)
  and `TicketTransferService.ClaimAsync` (the claimant is re-evaluated). Identical decision everywhere, no
  duplicate logic. (2) **`require_verified` now rejects `SelfDeclared`** (V3 §4.3) using the existing `Source`
  field. (3) **Docs** now mark `applies_to`, `guest_per_registrant_cap`, `guests_require_approval` as stored-
  but-not-yet-enforced. No schema change; no Membership redesign (per the architecture decision).
- **Verification.** `dotnet build` 0 errors; **full backend suite 604 passed / 1 skipped / 0 failed**
  (container-run — the only honest verifier here per B-5; 17 `AudienceRuleTests` incl. 5 review-fix tests: the
  three bypass paths, `require_verified` rejecting `SelfDeclared`, and an eligible-member group-join regression);
  web + admin typecheck/lint/build green. `flutter analyze`/`test` unchanged by this phase (no `.dart` touched;
  the `guest_browse_test.dart` failure is the pre-existing/concurrent-workstream one).
- **Assumptions.** (1) Rules evaluate against **org memberships** — staff RBAC roles today; the rich affiliate
  (student/faculty) membership population arrives with a later IMPORTED/PROVISIONED import, at which point the
  same mechanism gains that reach. (2) `role_in` matches `Membership.Role` (RBAC), not the M6 affiliation
  claim. (3) `unit_subtree_in` resolves a member's effective unit as the org root (no unit-scoped memberships
  yet), so a rule naming a deeper sub-unit matches no one until those exist. (4) Team `applies_to` variants are
  stored but only `EveryMember` is enforced for individual registration (teams are Phase 10). (5) `guests`
  `aggregate_pool_id` is deferred to Phase 7 (inventory pools). No new D-NNN — implements V3 §4.4.
- **Remaining Work.** The Web/Admin/Flutter surfaces for configuring rules + eligibility-aware register UI are
  the remaining Phase 5 client work (the backend mechanism they consume is complete and API-stable). Phase 6
  (Participants) is next.

### Event Architecture V3 — Phase 4: OrgUnit tree (2026-07-22)

- **Implementation Summary.** Added the OrgUnit structural tree (V3 §4.1) — the recursive, materialised-path
  hierarchy that universities and companies share. Backend-only and strictly additive: every org gets a root
  unit (materialised on first need), an event auto-binds its owning unit, and a permission chain-walk resolves
  a descendant unit's effective org role. No behavioural change to existing per-org authz — with a one-node
  tree the walk resolves to the same org-level membership (§4.1 "a one-node tree costs nothing"). Sub-unit
  pickers, unit-scoped grants, and the archive/merge/reparent lifecycle are later phases.
- **Files Changed.** New: `Domain/Entities/Orgs.cs` → `OrgUnit`; `Enums.cs` → `OrgUnitState`;
  `Application/Abstractions/IOrgUnitService.cs`; `Infrastructure/Orgs/OrgUnitService.cs`; migration
  `AddOrgUnitTree`; `Tests/OrgUnitTests.cs`. Modified: `Events.cs` (`Event.OrgUnitId`), `KurxDbContext.cs`
  (map `org_units` + one-root-per-org partial unique index + `events.OrgUnitId` FK), `DependencyInjection.cs`
  (register `IOrgUnitService`), `EventService.cs` (bind the owning unit on create; inherit on clone).
- **Database.** Additive migration `AddOrgUnitTree`: new `org_units` table (id, org_id, parent_id, kind, name,
  path, state) with a **partial unique index** `ix_org_units_one_root_per_org` (one parentless unit per org);
  new nullable `events.org_unit_id` FK. Backfill: one root unit per existing organisation + every existing
  event repointed to its org's root **in COMMIT-per-batch chunks** (a temp procedure run with
  `suppressTransaction`) so a large `events` table is never rewritten under one long lock; NOT EXISTS / IS NULL
  guards make it idempotent. No `path` index this phase (see Review fixes).
- **API.** None. Phase 4 is backend structural only (the roadmap change-summary lists no API/Web/Admin/Flutter
  for Phase 4). Existing `/v1/events/*` responses are byte-for-byte unchanged.
- **Docs.** `DATABASE_TABLES.md` (org_units + events.org_unit_id), `architecture/overview.md`,
  `V3_IMPLEMENTATION_ROADMAP.md` (Phase 4 → COMPLETE; Next → Phase 5), `roadmap/README.md`.
- **Breaking Changes.** None. Additive; `org_unit_id` is nullable.
- **Review fixes (2026-07-22).** (1) **Migration safety** — the event repoint is now a COMMIT-per-batch temp
  procedure so it never holds one long table lock; still idempotent. (2) **Path index** — removed; nothing
  queries `path` this phase and a plain btree can't serve a UTF-8 prefix `LIKE` anyway (the `text_pattern_ops`
  index lands with Phase 5's subtree query). (3) **Concurrent root race** — `EnsureRootAsync` now does an
  atomic `INSERT … ON CONFLICT ("OrgId") WHERE "ParentId" IS NULL DO NOTHING` then reloads, so a concurrent
  first-use can't surface as a spurious `slug_conflict`. (4) Added tests for backfill, concurrent EnsureRoot,
  deep (3-level) hierarchy, and the clone null-fallback.
- **Verification.** `dotnet build` 0 errors; **full backend suite 587 passed / 1 skipped / 0 failed**
  (container-run — the only honest verifier here per B-5; 9 `OrgUnitTests` incl. the 4 new review-fix tests);
  web + admin typecheck/lint/build green. `flutter analyze`/`test` show a new `auth_screens_test.dart`
  (SecurityPage) failure + the pre-existing `guest_browse_test.dart` — **both from the concurrent auth
  workstream's mobile changes, not Phase 4** (Phase 4 touched no `.dart` file).
- **Assumptions.** (1) Root units are materialised **lazily on first need** (idempotent `EnsureRootAsync`) +
  a one-time backfill, rather than eagerly at org creation — matching V3's "org_unit_id auto-sets". (2) Root
  `kind` defaults to the string `"organization"` (V3's kind list is open); org-type-specific labels are a
  later refinement. (3) The permission chain-walk is a **seam** (`ResolveOrgRoleAsync`), not yet wired into the
  existing per-service authz — that ancestor-union rewire is Phase 5 (🔴 authz), not pulled forward here.
- **Remaining Work.** Phase 5 (Audience rules · Membership attributes) — depends on this (ordering 5→4).

### Event Architecture V3 — Phase 2: Capability registry (2026-07-22)

- **Implementation Summary.** Made event capabilities real (V3 §11). Added the ~45-capability registry,
  the §19 Kind×Capability default matrix, and a per-event resolved set materialized from Kind × mode ×
  dependencies. Strictly additive — no existing table/API/client changed. Distinct from (and leaves
  untouched) trust capabilities (M7) and the D-116 workspace-capabilities.
- **Files Changed.** New: `Domain/Entities/Capabilities.cs`, `Infrastructure/Events/{CapabilityCatalog,CapabilityRegistrySeeder,CapabilityService}.cs`,
  `Application/Abstractions/ICapabilityService.cs`, `Api/Endpoints/CapabilityEndpoints.cs`, migration
  `AddCapabilityRegistry`, `Tests/CapabilityRegistryTests.cs`. Modified: `Enums.cs` (+`CapabilityState`),
  `KurxDbContext.cs`, `DependencyInjection.cs`, `Program.cs` (seed + backfill + map), `EventService.cs`
  (materialize on create/clone/update), `EventEndpoints.cs` (event-capabilities read).
- **Database.** Additive migration `AddCapabilityRegistry`: `capabilities` (~45), `kind_capability_defaults`
  (§19 matrix), `event_capabilities` (per-event, FK-cascade from events). No existing table altered.
- **API.** Added `GET /v1/capabilities`, `GET /v1/kinds/{slug}/capabilities`,
  `GET /v1/orgs/{orgId}/events/{eventId}/capabilities`. No existing endpoint changed.
- **Docs.** `DATABASE_TABLES.md`, `api/README.md`, `architecture/overview.md`, `V3_IMPLEMENTATION_ROADMAP.md`
  (§0 + §11 → Phase 2 COMPLETE), `roadmap/README.md`.
- **Breaking Changes.** None. Additive; clients unchanged.
- **Review hardening (2026-07-22).** Fixed `MaterializeForEventAsync` re-materialization — an EF
  identity-tracking conflict (a Deleted row + a re-Added row sharing `(event_id, capability_slug)` in one
  `SaveChanges`) that would have 500'd any post-create Kind/mode change; removal now uses `ExecuteDeleteAsync`
  (no tracking). Materialization is intentional per V3 §11 (the per-event substrate later phases read/write)
  and is left as-is. Registry caching is documented as a future optimization (no current hot path).
- **Verification.** `dotnet build` 0 errors; **11 capability tests** (6 registry + 5 review-fix: Kind change,
  Mode change, Clone, registry-slug validation, untyped event) + event/kind/workflow regression green; web +
  admin typecheck/lint/build green; `flutter analyze`/`test` unchanged (63 infos; the pre-existing
  `guest_browse_test.dart` failure is unrelated). Full local suite remains B-5-flaky — CI is the authority.
- **Remaining Work.** Phase 3 (Money) — not started; awaits approval. Capability config schemas + the full
  `provides` contract + per-event toggle/LOCKED transitions land as each subsystem is built (Phases 10–15).
  Future optimizations (documented, not premature): cache the static registry, index/one-time the per-boot
  backfill, and make re-materialization override-preserving when per-event overrides arrive.

### Event Architecture V3 — Phase 1: Kind registry (2026-07-22)

- **Implementation Summary.** Made the event Kind analysable (V3 §2). Added the closed 20-Kind catalog and
  mapped the 145 legacy taxonomy Type names onto it as data-driven aliases; every event now carries a
  derived `kind_slug`. Strictly additive — the 3/13/145 taxonomy, all `/v1/events` shapes, and every client
  are untouched (no behaviour change). §21.1 schema shapes are **not** pre-landed here (deferred to their §6
  phases; B-4 — no production data).
- **Files Changed.** New: `Domain/Entities/Kinds.cs`, `Infrastructure/Events/{KindCatalog,KindRegistrySeeder,KindService}.cs`,
  `Application/Abstractions/IKindService.cs`, `Api/Endpoints/KindEndpoints.cs`, migration `AddKindRegistry`,
  `Tests/KindRegistryTests.cs`. Modified: `Events.cs` (+`KindSlug`), `KurxDbContext.cs`, `DependencyInjection.cs`,
  `Program.cs` (seed + backfill + map), `EventService.cs` (derive on create/clone/update).
- **Database.** Additive migration `AddKindRegistry`: `event_kinds` (20, seeded), `kind_aliases` (145, seeded,
  unique `normalized_alias`), `events.kind_slug` (nullable, backfilled). No existing column/table altered.
- **API.** Added `GET /v1/kinds` (public). No existing endpoint changed or removed.
- **Docs.** `docs/DATABASE_TABLES.md`, `docs/api/README.md`, `docs/architecture/overview.md`,
  `docs/architecture/V3_IMPLEMENTATION_ROADMAP.md` (§0 + §11, Phase 1 → COMPLETE), `docs/roadmap/README.md`.
- **Breaking Changes.** None. Additive; clients unchanged.
- **Verification.** `dotnet build` 0 errors. New tests 5/5; `EventTaxonomyTests` + `EventTests` regression 14/14;
  all directly-affected classes green in the full run. Web + admin typecheck/lint/build green; `flutter analyze`
  unchanged (63 pre-existing infos). Full local suite is B-5-flaky (208→31 on identical code, login-heavy
  classes that pass in isolation) — **CI is the authority**. Pre-existing `guest_browse_test.dart` mobile
  failure is unrelated (no Phase-1 mobile change).
- **Remaining Work.** Phase 2 (Capability registry) — not started; awaits approval. `kind_slug` is not yet on
  the event response JSON (deferred to the kind-aware client work, Phase 15).

### Event Architecture V3 — Phase 0: Guardrails — 2026-07-20 (D-130, D-131) — *wave A of 5*

First phase of the 18-phase [Event Architecture V3](docs/architecture/V3_IMPLEMENTATION_ROADMAP.md)
program. Phase 0 carries no event-model change by design — it removes three as-built analytics defects
and raises CI to the standard the remaining seventeen phases are verified against.

- **Implementation summary:**
  - **Fabricated analytics deleted (D-130).** `AnalyticsService.AggregateDailyStatsAsync` populated
    `Views` and `UniqueVisitors` with `Random.Shared` — every traffic figure the platform reported
    (dashboards, CSV exports, org rollups, the `trending` feed) was invented. Both metrics now derive
    from an append-only `event_views` stream.
  - **Synchronous `ViewCount` write removed from the public read path.** `GET /v1/events/{slug}` appended
    to the view stream instead of `UPDATE events SET ViewCount = ViewCount + 1`. `events.ViewCount` is
    retained but **derived** by the daily aggregation job; replacing `ORDER BY ViewCount` with real
    ranking is Phase 16, deliberately not folded in here.
  - **Divergent second analytics path retired** — one authoritative `AnalyticsService` remains.
  - **CI raised to the per-phase verification bar:** new `Mobile — analyze & test` job
    (`flutter analyze` + `flutter test`, previously no mobile job at all); `npm run lint` added to the
    admin job (the script existed; CI never called it).
  - **Design artifacts moved into version control (D-131).** V3 (1,357 lines), the 18-phase roadmap, and
    the V2/review/as-built history (4,259 lines total) existed **only in a session scratchpad under
    `%TEMP%`** — approved architecture on a self-deleting path, readable by no one but the authoring
    session. Now `docs/architecture/` (current) and `docs/history/architecture/` (superseded, bannered).
- **Files changed:** `AnalyticsService.cs`, `EventService.cs`, `KurxDbContext.cs`, `Analytics.cs`,
  `.github/workflows/ci.yml`; new `EventViewStreamTests.cs`; docs as below.
- **Database:** migration `20260720055102_AddEventViewStream` — `event_views` (append-only:
  `EventId`, `VisitorKey`, `ViewedAt`). Additive; no destructive change. `events.ViewCount` retained,
  now derived rather than incremented.
- **API:** no contract change. `GET /v1/events/{slug}` keeps its response shape; only its write
  behaviour changed (stream append, not counter update).
- **Docs:** `docs/architecture/EVENT_ARCHITECTURE_V3.md` + `V3_IMPLEMENTATION_ROADMAP.md` (new, adopted);
  `docs/history/architecture/` ×3 (archived with supersession banners); `docs/DECISIONS.md` (D-130,
  D-131); `docs/README.md`; `docs/roadmap/README.md` (live 18-phase status table);
  `docs/architecture/overview.md`; `docs/history/README.md`; `docs/PROJECT_HANDBOOK.md`; this file.
- **Breaking changes:** none.
- **Verification:** `dotnet build` 0 warnings / 0 errors. Full backend suite, web typecheck/lint/build/test,
  admin typecheck/lint/build, `flutter analyze` + `flutter test` — see the phase completion report.
- **Remaining work:** Phase 1 — Kind registry (`event_kinds` 20 + `kind_aliases` 145, `Event.KindSlug`
  backfilled, category derived, `GET /v1/kinds`), which must also land the V3 §21.1 schema shapes that
  cannot be retrofitted later.
- **Standing risk:** the unmerged `feat/template-engine` worktree (D-114) holds a parallel event
  implementation colliding with Phases 1, 2 and 15. V3 is scoped around it; re-raise before Phase 15.

---

## [Unreleased] — branch `feat/production-rearchitecture`

### M13 — Fraud prevention — 2026-07-13 (D-052) — *final module*
- **Implementation summary:** `blacklist_entries` (hard blocks, normalized) + `fraud_signals`
  (polymorphic risk). Made M7's `fraudClear` live (`IFraudService.IsUserClearAsync`) — a blacklisted
  or high-risk (score ≥ 100) user loses CanOrganizePaid, cascading into the M8/M10 gates. Blacklisted
  org names blocked at creation.
- **Files changed:** new `Domain/Entities/Fraud.cs`, `Application/Abstractions/IFraudService.cs`,
  `Infrastructure/Trust/FraudService.cs`, `Api/Endpoints/AdminFraudEndpoints.cs`,
  `Migrations/…_AddFraudTables.cs`, `Kurx.Tests/FraudTests.cs`; modified `Enums.cs` (+BlacklistKind/
  FraudSignalKind), `KurxDbContext.cs`, `Trust/TrustService.cs` (live fraudClear), `Orgs/OrgService.cs`
  (blacklist org name), `Program.cs`, `DependencyInjection.cs`, docs.
- **Database:** +2 tables `blacklist_entries` (unique kind+value) + `fraud_signals` (polymorphic).
  Additive. 68 → 70 tables.
- **API:** `POST/GET/DELETE /v1/admin/blacklist`, `POST /v1/admin/fraud-signals` (VerificationReviewer);
  org creation returns `org_blacklisted` (403) for blocked names.
- **Docs:** DECISIONS (D-052), DATABASE_TABLES, trust-verification, CHANGELOG.
- **Breaking changes:** none (additive). `fraudClear` now real — a flagged user loses paid capability.
- **Deferred:** automated signal producers (device/velocity/dup-account) feeding RecordSignalAsync.
- **Verification:** `dotnet build` clean; `dotnet test` **166/166** (+5; blacklisted phone / high risk →
  no paid organizing, blacklisted org name blocked, admin blacklist CRUD, non-reviewer 403 — real HTTP).
- **ALL 13 MODULES COMPLETE.**

### M12 — Admin verification console (backend) — 2026-07-13 (D-051)
- **Implementation summary:** On top of the existing M5/M6 review queues+actions, added the new admin
  capabilities: org merge (fresh duplicates only), blacklist, and a cross-subject verification audit
  trail. All gated by the VerificationReviewer platform role. admin/ Next.js UI = Pending Stitch UI.
- **Files changed:** `Orgs/OrgVerificationService.cs` (+Merge/Blacklist), `IOrgVerificationService.cs`;
  new `Application/Abstractions/IAdminVerificationService.cs`, `Infrastructure/Admin/AdminVerificationService.cs`;
  `Api/Endpoints/AdminOrgEndpoints.cs` (+merge/blacklist/history), `DependencyInjection.cs`;
  new `Kurx.Tests/AdminConsoleTests.cs`; docs.
- **Database:** none (reuses verification_reviews/documents, organizations, memberships). 68 tables.
- **API:** `POST /v1/admin/orgs/merge`, `POST /v1/admin/orgs/{id}/verification/blacklist`,
  `GET /v1/admin/verifications/{subjectType}/{subjectId}/history` (VerificationReviewer).
- **Docs:** DECISIONS (D-051), trust-verification, CHANGELOG.
- **Breaking changes:** none (additive).
- **Deferred:** admin/ Next.js UI (Pending Stitch UI); first-class appeals queue (resubmission works today).
- **Verification:** `dotnet build` clean; `dotnet test` **161/161** (+5; merge+alias, merge-blocked-on-events,
  blacklist, history, non-reviewer 403 — real HTTP/kurx_test).
- **Remaining work:** M13 (fraud prevention) — final module.

### M11 — Forms convergence — 2026-07-13 (D-050) — *destructive drop, approved*
- **Implementation summary:** Two form systems existed; only the ticket-scoped `FormField` (D-020)
  was ever wired. Retired the schema-only event-scoped `registration_forms/*` 4-table builder (D-024).
  `FormField` is now the single registration-form system.
- **Files changed:** deleted `Domain/Entities/RegistrationForms.cs`; removed `RegistrationFieldType`
  from `Enums.cs`; removed 4 DbSets + config from `KurxDbContext.cs`; new `DropRegistrationForms`
  migration; docs.
- **Database:** dropped `registration_forms` / `registration_fields` / `registration_responses` /
  `registration_response_values` (dependency-ordered; reversible; 0 rows). **72 → 68 tables.**
- **API:** none. **Docs:** DECISIONS (D-050, supersedes D-024), DATABASE_TABLES, CHANGELOG.
- **Breaking changes:** the never-wired registration_forms tables/entities removed. No behavior change.
- **Deferred (not lost):** event-wide standalone surveys can be re-introduced on `FormField` if scheduled.
- **Verification:** `dotnet build` clean; `dotnet test` **156/156** (unchanged; the dropped tables were
  never exercised). Applies clean on a freshly-reset kurx_test.
- **Remaining work:** **M12 (admin console) & M13 (fraud) — PAUSE (reached the m11 boundary).**

### M10 — Paid checkout + ledger write-path — 2026-07-13 (D-049) — *money module, security-reviewed*
- **Implementation summary:** Paid ticket type → real Pending order + gateway order (mock) + seat
  hold, gated live (organizer paid-verified + org verified, M8). Razorpay webhook confirms capture →
  issues ticket + writes LedgerEntry(Collected) + updates the wallet cache atomically (D-028).
  Idempotent; amount server-authoritative. Completes the capture→Collected→Available→withdrawal pipe.
- **Files changed:** `Orders/OrderService.cs` (paid branch + ConfirmPaymentAsync +IPaymentGateway/
  ITrustService), `Application/Abstractions/IOrderService.cs` (+ConfirmPaymentAsync),
  `Api/Endpoints/WebhookEndpoints.cs` (+Razorpay webhook), `Auth/TokenService.cs` (TICKET_HMAC_SECRET
  for ticket signing), new `Kurx.Tests/PaidOrderTests.cs`, updated `Kurx.Tests/OrderTests.cs`, docs.
- **Database:** none (reuses orders/payments/seat_holds/ledger_entries/organization_wallet). 72 tables.
- **API:** paid `POST /v1/events/{id}/orders` now returns a Pending order + `razorpay_order_id` (or
  `payments_not_enabled`); new `POST /v1/webhooks/razorpay` (signature-verified, idempotent capture).
- **Docs:** DECISIONS (D-049), CHANGELOG.
- **Breaking changes:** `payment_not_supported_yet` (D-021) replaced by real paid flow /
  `payments_not_enabled`. Ticket QR signing key changed to TICKET_HMAC_SECRET (transparent).
- **Deferred (documented, not faked):** real RazorpayPaymentGateway (HMAC/envelope/route transfers),
  paid-group tickets, refunds.
- **Verification:** `dotnet build` clean; `dotnet test` **156/156** (+4; Pending order, capture→ticket+
  ledger+wallet, idempotent webhook, suspend-blocks-orders-live — real HTTP/kurx_test). Security review done.
- **Remaining work:** M11 (forms convergence — destructive drop of registration_forms/*).

### M9 — KYC term split — 2026-07-13 (D-048) — *destructive rename, approved*
- **Implementation summary:** "KYC" now means person identity (M3). Renamed the org financial
  verification `kyc_records` → `org_bank_verifications` (entity `KycRecord` → `OrgBankVerification`),
  and deleted the dead duplicate `IKycService`/`KycService`/`KycEndpoints` (AmbiguousMatchException
  landmine). Routes and response shapes unchanged.
- **Files changed:** `Domain/Entities/Orgs.cs` (rename), `KurxDbContext.cs` (DbSet + table name),
  `Orgs/OrgService.cs` (refs), `DependencyInjection.cs` (drop dead reg), `Program.cs` (drop dead
  comment), new migration; **deleted** `IKycService.cs`, `KycService.cs`, `KycEndpoints.cs`; docs.
- **Database:** `kyc_records` → `org_bank_verifications` via **data-preserving** RenameTable +
  RenameIndex + RENAME CONSTRAINT (EF's scaffolded DROP+CREATE was replaced). Reversible. Still 72 tables.
- **API:** none (routes `/v1/orgs/{orgId}/kyc/*` + shapes unchanged).
- **Docs:** DECISIONS (D-048), DATABASE_TABLES, CHANGELOG.
- **Breaking changes:** internal only (entity/table rename). Dead never-mapped endpoints removed.
- **Verification:** `dotnet build` clean; `dotnet test` **152/152** (unchanged; rename + existing
  OrgTests bank/PAN cases apply clean on a freshly-reset kurx_test).
- **Remaining work:** M10 (real payments + ledger write-path) next.

### M8 — Event approval + payment gate — 2026-07-13 (D-047)
- **Implementation summary:** Free events publish directly; a PAID event (any priced ticket
  type) can't self-publish — org must submit_review (gated on organizer CanOrganizePaid + org
  Verified, M7) → platform reviewer publishes. `payments_enabled` computed live (not stored).
  Reused existing EventStatus states → no enum change, no migration.
- **Files changed:** `Application/Abstractions/IEventService.cs` (TransitionAsync +isReviewer,
  +GetPaymentReadinessAsync, +PaymentReadiness), `Infrastructure/Events/EventService.cs`
  (+ITrustService, paid gate, readiness), `Api/Endpoints/EventEndpoints.cs` (+isReviewer,
  +payment-readiness endpoint, +IsReviewer helper), new `Kurx.Tests/EventApprovalTests.cs`, docs.
- **Database:** none (payments-enabled computed live). Still 72 tables.
- **API:** `POST /transition` now enforces the paid gate + reviewer role; new
  `GET /v1/orgs/{org}/events/{id}/payment-readiness`. New errors:
  `paid_event_requires_review` (409), `organizer_not_verified_for_paid`/`org_not_verified` (403).
- **Docs:** DECISIONS (D-047), trust-verification, CHANGELOG.
- **Breaking changes:** `IEventService.TransitionAsync` signature +isReviewer (internal). Paid
  events can no longer be self-published (intended). Free events unchanged.
- **Verification:** `dotnet build` clean; `dotnet test` **152/152** (+4; free-direct, paid-can't-
  self-publish, paid-blocked-unverified, full reviewer flow + live readiness — real HTTP/kurx_test).
- **Remaining work:** **M9 (KYC split — destructive `kyc_records` rename) — PAUSE for approval.**

### M7 — Trust capability matrix — 2026-07-13 (D-046)
- **Implementation summary:** `ITrustService` composes identity (M3), org (M5), and membership
  (M6) verification into live capability flags (CanOrganizeFree/Paid, CanReceivePayout,
  CanRepresentOrg, IsOrgVerifiedRep); L0–L5 are labels over the flags. No schema — derived
  live per request. Gates (M8, M10) read this instead of re-deriving rules.
- **Files changed:** new `Application/Abstractions/ITrustService.cs`,
  `Infrastructure/Trust/TrustService.cs`, `Kurx.Tests/TrustTests.cs`; modified
  `AuthEndpoints.cs` (`/v1/me` `trust` object), `OrgEndpoints.cs`
  (`/v1/orgs/{id}/my-capabilities`), `DependencyInjection.cs`, docs.
- **Database:** none (derived). Still 72 tables.
- **API:** `/v1/me` gains a `trust` object; new `GET /v1/orgs/{id}/my-capabilities`.
- **Docs:** DECISIONS (D-046), trust-verification, CHANGELOG.
- **Breaking changes:** none (additive).
- **Verification:** `dotnet build` clean; `dotnet test` **148/148** (+5; free-not-paid,
  identity+bank→paid, identity-only→not-paid, verified-rep, member≠rep — real HTTP/kurx_test).
- **Remaining work:** M8 (event approval + payment gate) next — reads these capabilities.

### M6 — Membership-affiliation verification — 2026-07-13 (D-045)
- **Implementation summary:** A user's claim to represent an org becomes an evidence-backed,
  reviewed record (never inferred from bio). Approval marks their operational membership
  verified (read-only Staff seat if none); organizer rights stay an org-Owner act. Email-
  domain fast-track hint.
- **Files changed:** new `Application/Abstractions/IMembershipVerificationService.cs`,
  `Infrastructure/Orgs/MembershipVerificationService.cs`,
  `Api/Endpoints/MembershipClaimEndpoints.cs`, `Migrations/…_AddMembershipClaims.cs`,
  `Kurx.Tests/MembershipVerificationTests.cs`; modified `Domain/Entities/Orgs.cs`
  (+Membership verified fields, +MembershipClaim), `Enums.cs` (+MembershipClaimRole/Status),
  `KurxDbContext.cs`, `Program.cs`, `DependencyInjection.cs`, docs.
- **Database:** new `membership_claims`; memberships +is_verified/source_claim_id/valid_until.
  71 → 72 tables.
- **API:** `POST /v1/orgs/{id}/membership-claims`, `GET /v1/me/membership-claims`;
  `/v1/admin/membership-claims/{pending, {id}/review}` (VerificationReviewer).
- **Docs:** DECISIONS (D-045), DATABASE_TABLES, trust-verification, CHANGELOG.
- **Breaking changes:** none (all additive). `AddMemberAsync` kept immediate (operational).
- **Verification:** `dotnet build` clean; `dotnet test` **143/143** (+7; submit/authz/duplicate/
  fast-track/approve→verified-Staff/reject — real HTTP/kurx_test).
- **Remaining work:** M7 (trust capability matrix) next — composes M3+M5+M6 into capabilities.

### M5 — Organization verification lifecycle — 2026-07-13 (D-044)
- **Implementation summary:** Verification state machine on top of the M4 registry. Owner
  submits evidence → PendingReview; a platform VerificationReviewer approves/rejects/
  requests-changes/suspends. Approval reserves the normalized name (hard dedup vs other
  verified orgs — the enforcement M4 deferred). Every decision is audited.
- **Files changed:** new `Application/Abstractions/IOrgVerificationService.cs`,
  `Infrastructure/Orgs/OrgVerificationService.cs`, `Api/Endpoints/AdminOrgEndpoints.cs`,
  `Migrations/…_AddOrgVerification.cs`, `Kurx.Tests/OrgVerificationTests.cs`; modified
  `Domain/Entities/Orgs.cs` (+verification fields), `Enums.cs` (+OrgVerificationStatus),
  `OrgService.cs`/`IOrgService.cs` (OrgDetail +verification_status),
  `OrganizationRegistryService.cs`/`IOrganizationRegistryService.cs` (search +status),
  `OrgEndpoints.cs` (submit/get verification), `Program.cs`, `DependencyInjection.cs`, docs.
- **Database:** organizations +verification_status (default Unverified)/reviewed_by/
  reviewed_at/notes. No new table (still 71).
- **API:** `POST /v1/orgs/{id}/verification/submit` + `GET …/verification` (Owner/member);
  `/v1/admin/orgs/{pending, {id}/verification/review, …/suspend}` (VerificationReviewer).
  `OrgDetail` + registry search now include `verification_status`.
- **Docs:** DECISIONS (D-044), DATABASE_TABLES, trust-verification, CHANGELOG.
- **Breaking changes:** `OrgDetail`/`OrgSearchResult` records gained fields (internal);
  API responses gained additive `verification_status`.
- **Verification:** `dotnet build` clean; `dotnet test` **136/136** (+9; submit/authz/approve/
  duplicate-block/request-changes/suspend/audit — all real HTTP/kurx_test).
- **Remaining work:** M6 (membership verification + teams) next.

### M4 — Organization registry — 2026-07-13 (D-043)
- **Implementation summary:** Replaced the thin `Organization` with a registry — typed,
  canonical, alias- and domain-aware, with pg_trgm fuzzy dedup search — so organizers
  find and reuse an existing org (the "NSRIT ↔ full name" rule) instead of duplicating.
- **Files changed:** new `Application/Abstractions/IOrganizationRegistryService.cs`,
  `Infrastructure/Orgs/OrganizationRegistryService.cs`, `Migrations/…_AddOrgRegistry.cs`,
  `Kurx.Tests/OrgRegistryTests.cs`; modified `Domain/Entities/Orgs.cs` (+registry fields,
  +OrganizationAlias), `Enums.cs` (+OrganizationType/AliasSource), `KurxDbContext.cs`
  (pg_trgm, GIN trigram indexes, alias table), `Orgs/OrgService.cs` (CreateAsync reshape +
  domain dedup + normalize), `Application/Abstractions/IOrgService.cs` (CreateAsync sig +
  OrgDetail), `Api/Endpoints/OrgEndpoints.cs` (+type/domain, +/search), `OrgValidators.cs`,
  `DependencyInjection.cs`, docs.
- **Database:** organizations +type/legal_name/primary_domain/canonical_org_id/
  normalized_name (unique domain among active; GIN trigram on normalized_name); new
  `organization_aliases`; pg_trgm extension. Backfill sets canonical=id + normalized_name.
  70 → 71 tables.
- **API:** `POST /v1/orgs` now accepts `type`/`legalName`/`primaryDomain` (type optional,
  default Other); hard-blocks duplicate `primary_domain` (409). New `GET /v1/orgs/search?q=`
  (fuzzy/alias/domain). `OrgDetail` gains `type`/`primary_domain`.
- **Docs:** DECISIONS (D-043), DATABASE_TABLES, trust-verification, CHANGELOG.
- **Breaking changes:** `IOrgService.CreateAsync` signature changed (internal); org create
  API is backward-compatible (new fields optional). Duplicate primary_domain now rejected.
- **Verification:** `dotnet build` clean; `dotnet test` **127/127** (+8; all search modes
  incl. fuzzy trigram + acronym→canonical via alias, via real HTTP/kurx_test). No regressions.
- **Remaining work:** M5 (org verification lifecycle) next — adds hard name-dedup at
  verification, merge, and status.

### M3 — Person identity verification (KYC) — 2026-07-13 (D-042)
- **Implementation summary:** New `user_identity_verifications` aggregate (1:1 user),
  graduated ID0–ID4, run against the existing mock `IKycProvider` (real DigiLocker/PAN/
  penny-drop adapter is a gated integration task — documented, not faked). Only masked
  last-4 is persisted; every decision writes a `verification_reviews` audit row.
- **Files changed:** new `Domain/Entities/Identity.cs`,
  `Application/Abstractions/IIdentityVerificationService.cs`,
  `Infrastructure/Identity/IdentityVerificationService.cs`,
  `Api/Endpoints/IdentityEndpoints.cs`, `Migrations/…_AddUserIdentity.cs`,
  `Kurx.Tests/IdentityVerificationTests.cs`; modified `Enums.cs` (+IdentityLevel/Status),
  `KurxDbContext.cs`, `DependencyInjection.cs`, `Program.cs`, `AuthEndpoints.cs`
  (`/v1/me` identity summary), docs.
- **Database:** +1 table `user_identity_verifications` (unique user_id; masked last-4
  columns; jsonb provider_refs; FKs to users). 69 → 70 tables. Additive.
- **API:** new `GET /v1/me/identity`, `POST /v1/me/identity/{government-id,pan,bank}`
  (caller's own identity only). `/v1/me` gains an `identity` summary object (additive).
- **Docs:** DECISIONS (D-042), DATABASE_TABLES, trust-verification, CHANGELOG.
- **Breaking changes:** none (`/v1/me` change is additive).
- **Security:** KYC/PII self-review done — only masked last-4 stored (full number never
  persisted/logged); own-identity-only authorization; automated audit rows; input
  validation; JSON-injection defense on `kind`; concurrent-create race handled; 5-attempt cap.
- **Verification:** `dotnet build` clean; `dotnet test` **119/119** (+9, all via real HTTP
  against kurx_test).
- **Remaining work:** M4 (organization registry) next.

### M1 — Identity & Account authority boundary — 2026-07-13 (D-041)
- **Implementation summary:** Removed the last authorization bit from the user row
  and made the display profile authority-zero. Dropped `users.IsKurxAdmin` (authority
  moved to `platform_roles` in M2, backfill already ran); annotated profile fields as
  self-declared, never proof. `EducationJson` retained (labelled unverified) until M6
  migrates it to evidence-backed claims.
- **Files changed:** `Domain/Entities/Users.cs` (remove IsKurxAdmin + authority-zero
  docs), `Migrations/…_DropIsKurxAdmin.cs` (+Designer +snapshot); docs.
- **Database:** dropped `users.is_kurx_admin` (reversible; ordered after the M2 backfill
  so admin grants are preserved first). Table count unchanged (69).
- **API:** none (`is_kurx_admin` was never exposed by any endpoint). `/v1/me` trust
  summary deferred to M3/M7 (no verification state to summarize yet).
- **Docs:** DECISIONS (D-041), DATABASE_TABLES (users table), trust-verification, CHANGELOG.
- **Breaking changes:** none observable to clients. `User.IsKurxAdmin` no longer exists
  in the domain model / DB.
- **Verification:** `dotnet build` clean; `dotnet test` **110/110** (full migration chain
  incl. the M2 backfill applies clean on a freshly-reset kurx_test).
- **Remaining work:** M3 (identity verification / person KYC) next.

### M2 — Authorization / RBAC & platform roles — 2026-07-13 (D-040)
- **Implementation summary:** Replaced the single `IsKurxAdmin` god-bit + static
  `kurx_admin` token claim with a `platform_roles` table read **live per request**.
  `PlatformRoleClaimsTransformation` strips any token-supplied platform claim
  (anti-forgery) and re-adds the user's DB roles each request, so grant/revoke is
  effective on the next request. Adds least-privilege roles for the admin console.
- **Files changed:** new `Domain/Entities/PlatformRoles.cs`,
  `Application/Abstractions/IPlatformRoleService.cs`,
  `Infrastructure/Auth/PlatformRoleService.cs`,
  `Api/Auth/PlatformRoleClaimsTransformation.cs`,
  `Migrations/…_AddPlatformRoles.cs` (+backfill +Designer +snapshot),
  `Kurx.Tests/PlatformRoleTests.cs`; modified `Domain/Enums/Enums.cs` (+PlatformRole),
  `Persistence/KurxDbContext.cs`, `Program.cs` (policies + transform registration),
  `DependencyInjection.cs`, `Auth/TokenService.cs` (no longer mints admin claim),
  `Kurx.Tests/EventTests.cs` (admin token → real grant), docs.
- **Database:** +1 table `platform_roles` (unique `(user_id, role)`; FKs to users);
  backfill inserts a SuperAdmin grant per existing `IsKurxAdmin=true` user. 68 → 69 tables.
- **API:** no route changes. Authorization behavior: platform authority now live-read;
  new policies `SuperAdmin`/`VerificationReviewer`/`FinanceOps`/`Support` available
  (used by M12). Admin rate-limit tier (3000/min) no longer applies pre-authorization
  (throttle-only change; M12 sets admin-console limits).
- **Docs:** DECISIONS (D-040), DATABASE_TABLES (+platform_roles), architecture overview
  (authorization model), `.claude/memory/{security-rules,trust-verification}.md`, CHANGELOG.
- **Breaking changes:** JWT no longer carries `kurx_admin`; a forged `kurx_admin`/
  `platform_role` token claim is ignored (was previously honored). Admin authority now
  requires a real `platform_roles` grant. `User.IsKurxAdmin` retained (read only by the
  backfill) until M1 drops it.
- **Verification:** `dotnet build` clean (0 warn/0 err); `dotnet test` **110/110** (+4:
  forged-claim rejected, real grant allowed, revocation effective next request on the
  same token, reviewer ≠ SuperAdmin). Existing platform-admin event test migrated to a
  real grant.
- **Remaining work:** M1 (identity boundary — drops `IsKurxAdmin`) next.

### M0 — Verification substrate — 2026-07-13 (D-039)
- **Implementation summary:** Added the two polymorphic tables every trust
  subsystem (identity/org/membership verification, event approval, fraud, admin
  console) will write to — one evidence table, one append-only review table.
  Schema only; no service/endpoint yet (those arrive in M3/M5/M6/M12).
- **Files changed:** new `Domain/Entities/Verification.cs`,
  `Migrations/…_AddVerificationSubstrate.cs` (+Designer +snapshot),
  `Kurx.Tests/VerificationSubstrateTests.cs`, `.claude/memory/trust-verification.md`;
  modified `Domain/Enums/Enums.cs` (+3 enums), `Persistence/KurxDbContext.cs`
  (+2 DbSets +config), `docs/DATABASE_TABLES.md`, `docs/DECISIONS.md` (D-039),
  `docs/architecture/overview.md`.
- **Database:** +2 tables (`verification_documents`, `verification_reviews`);
  polymorphic `(subject_type, subject_id)` with no FK on subject; `uploaded_by` /
  `reviewer_id` → users (RESTRICT, reviewer nullable); jsonb `extracted_json`;
  indexes for subject lookup, sha256 (forgery/dup), and reviewer/audit history.
  Additive, reversible. 66 → 68 tables.
- **API:** none. **Docs:** DATABASE_TABLES, DECISIONS (D-039), architecture
  overview, new memory `trust-verification.md`, this CHANGELOG.
- **Breaking changes:** none.
- **Verification:** `dotnet build` clean (0 warn/0 err); `dotnet test` **106/106**
  (was 105; +1 substrate round-trip test against real kurx_test).
- **Remaining work:** M2 (platform roles) next.

### Baseline stabilization (pre-M0) — 2026-07-13
- **Summary:** Fixed a pre-existing red test baseline (5/105 failing) so module
  work starts from green. Test-harness only; no product behavior change.
- **Files:** `Kurx.Tests/{ExceptionHandlingTests,ValidationTests,HubSecurityTests}.cs`
  (fixes); inherited WIP committed: `Kurx.Tests/KurxApiFactory.cs`,
  `Kurx.Infrastructure/Auth/AuthService.cs`, `Kurx.Api/Program.cs`.
- **Database:** none. **API:** none. **Docs:** none.
- **Breaking changes:** none.
- **Verification:** `dotnet build` clean (0 warn/0 err); `dotnet test` 105/105 green.
- **Remaining work:** begin M0 (verification substrate).

### Planning (approved 2026-07-13)
- Full system audit + target architecture: `KURX_REARCHITECTURE_MASTER_PLAN.md` (deleted 2026-08-08; in git history).
- 13-module per-module design dossier: `KURX_MODULE_DESIGN_DOSSIER.md` (deleted 2026-08-08; in git history).
- Locked decisions: capability-matrix trust (L0–L5 labels); liveness risk-gated
  only; keep `org_invitations`, defer other scaffolding; additive infra +
  clean-sheet domain where weaker.
- Approved implementation order: M0 → M2 → M1 → M3 → M4 → M5 → M6 → M7 → M8 →
  M9 → M10 → M11 → M12 → M13.
