# Backend Conventions

.NET 10, `backend/Kurx.{Api,Application,Infrastructure,Domain}`. Extends `.claude/memory/coding-standards.md` with backend-specific rules. For the request/response contract see `.claude/memory/api-conventions.md`; for errors `.claude/memory/error-handling.md`; for data `.claude/memory/database-conventions.md`.

## One event authorization service — never write your own (D-269/D-272)

**`IEventAuthority` is the single source of truth for event authorization** — management *and* audience.
Resolve once, then ask:

```csharp
var access = await authority.ResolveAsync(userId, eventId, isAdmin, ct);
if (!access.Can(EventPermission.ManageContent)) return Fail("forbidden");
```

Levels are ordered `None < Participant < Staff < Manager < Admin`; `Manager` is reached by the event's
**creator** (no membership needed), a `Representative`, or an `Owner`/`Manager` seat; `Participant` by an
accepted programme participation **or a live ticket** (D-272). `Finance` is `None`. Permission → minimum
level lives in `EventAuthority.Requirements` — **a new event capability adds an enum value and one row
there, never authorization logic.**

Two permission families sit on the one ladder, and mixing them is a bug:

| Family | Permissions | Question |
|---|---|---|
| Management | `ViewAttendees`, `ViewAnalytics`, `ManageContent`, `ManageLifecycle`, `Delete` | may this caller **run** the event? |
| Audience (D-272) | `Participate` (feed read, post attachment, chat room), `ModerateAudience` (chat `Host`) | does this caller **belong to** the event? |

Gating an audience surface on a management permission is a silent lockout of every ticket holder.

`ResolveOrgAsync` is for what an *organization* owns rather than an event — speaker/sponsor/venue
libraries, series, representation authority. Same interface on purpose: one role → authority mapping.

**Do not add a private `CanManage`/`RoleAsync` to a service, and never authorize on a bare
`db.Memberships.Any(m => m.OrgId == ev.OrgId && m.UserId == x)`.** There were eleven private copies; seven
byte-identical, the rest drifted, and the drift hid two live bugs (`Representative` could publish an event
but not add a ticket type; after D-268 a creator held nothing on their own event's sub-resources). The bare
membership query is the same defect in cheaper clothing: it grants `Finance`, and grants the creator
*nothing* unless a self-representation row happens to exist — which is exactly how three audience surfaces
survived D-269 (D-272).

Where an EF predicate genuinely cannot resolve one caller per row (`PostService.VisibleTo` filters many
events at once), use `EventAuthority.AudienceRoles` / `ModeratorRoles`. They are **derived from
`EventAuthority.LevelFor` at load** via `RolesAtLeast`, so the query-side mirror cannot drift from the
resolver. Never hand-write the role list beside it.

Exceptions are documented in D-269/D-272 and are not precedent: `ApprovalService` needs the *exact* role an
approval step names; the `Orgs/*` services are organization RBAC with Owner-only rules; and the
`Owner`-or-`Finance` financial bar (`AnalyticsService`, `RefundService`, `WalletService`) is the ladder's
inverse by design.

**`IEventAuthority` decides WHO may act; `ICapabilityService` decides WHAT an event supports.** They never
consult each other. A capability is not a permission.

## The representation field is `RepresentingOrgId` (D-273a)

`Event.RepresentingOrgId` is the organization an event **represents**; `Event.CreatedBy` is its **owner**.
The old name was `OrgId` and it read like ownership — if you see `Event.OrgId` anywhere, it is stale.

**The physical column is still `"OrgId"`**, pinned with `HasColumnName`, and its indexes and FK keep their
original names. That is deliberate, not an oversight: it kept the rename schema-neutral so the app can roll
back independently of the database. Do not "fix" it into a migration without a decision entry.

On the wire, event payloads emit `representing_org_id` **and** a deprecated `org_id` alias with the same
value (expand-and-contract). Read the new one; never add a new consumer of `org_id`.

`RepresentingOrgId` is **non-null**, and making it nullable is D-273b — not a rename. `LedgerEntry`,
`OrganizationWallet` and `PayoutSchedule` are all keyed non-nullably on an org, so a null representation
means a captured payment with nowhere to settle.

When renaming a field this widely, delete it and let the compiler enumerate the call sites, then rewrite at
the reported line/column. A textual find-replace on `OrgId` would also hit `Membership.OrgId`,
`LedgerEntry.OrgId`, `EventAssignment.OrgId` and six other entities that legitimately keep the name.

## The User owns the Event; representation is an attribute (D-268)

Kurx has **Users**, **Events** and **Representations** — no organization accounts, no organizer
accounts, and **no personal organizations**. `Event.CreatedBy` is the owner and every event
authorization checks it **first** (`EventService.CanManageEventAsync` = admin OR creator OR
org-manage-role; `EventPermissionService` grant **(0)**). An organization role is an *additional* grant —
how staff of a represented institution collaborate — never the source of the owner's authority.

Do not write a new event authorization as `RoleAsync(userId, ev.RepresentingOrgId)` alone. That was the
whole defect: the right to manage an event derived entirely from membership of the organization it
represents, so a person hosting under their own name needed a synthetic organization to be a member of.

**`Event.RepresentingOrgId` stores the *represented* organization, not the owner** — renamed from `OrgId`
in D-273a, which closed the naming debt D-268 opened. It is still a non-null FK (nullability is D-273b),
so a self-represented event points at an internal row resolved privately by
`EventService.ResolveSelfRepresentationAsync`. That row is persistence, never domain: it must not appear
in any API field, DTO, service name, route, label or document, and `ListRepresentableAsync` filters it
out server-side so no client needs an `is_personal` flag. If you find yourself adding one, you are
re-creating the concept D-268 deleted.

Representation on the wire is `{ kind: "personal" | "organization", organization_id, organization_name,
verified }` — `personal` carries **no** organization identity at all.

## Layering (hard rule)

`Api → Infrastructure → Application (interfaces) ← implemented by Infrastructure`; everything depends on `Domain`; `Domain` has **no framework dependencies**.

- `Kurx.Api/Endpoints` — minimal-API endpoint maps, DTOs, FluentValidation validators. No business logic, no EF.
- `Kurx.Application/Abstractions` — service interfaces + provider interfaces (`IEmailSender`, `IPaymentGateway`, `IStorage`, `IKycProvider`, …). No implementations.
- `Kurx.Infrastructure/<Domain>` — service implementations (`OrgService`, `EventService`, …), EF `DbContext`, provider adapters. Folder-per-domain.
- `Kurx.Domain/Entities|Enums` — POCOs + enums + workflow rules (`EventStatusWorkflow`). Pure C#.

An `Api` handler that reaches for an EF type or writes a raw `DbContext` query for business logic is a layering violation — route it through an `Application` interface.

## Endpoints (minimal API)

- One endpoint = interface method + Infrastructure implementation + `Kurx.Api/Endpoints` map + DTO + validator + integration test (the five steps in `.claude/memory/api-conventions.md`).
- Request DTOs validated with FluentValidation via the generic `WithValidation<T>()` `IEndpointFilter` — never manual `if` guards in the handler, never `FluentValidation.AspNetCore` (MVC-only, D-017).
- Handlers stay thin: validate (filter) → call service → map result → return. No orchestration logic in the endpoint.

## Services

- Expose an interface in `Kurx.Application.Abstractions`, implement in `Kurx.Infrastructure/<Domain>`. Register in DI.
- Authorization is passed **in**: `Task X(Guid userId, bool isAdmin, …)`. Never thread `ClaimsPrincipal`/`HttpContext` into a service (D-018). Resource-role checks (Owner/Manager/Staff/Finance) are queried live from Postgres inside the service (`OrgService.RoleAsync` pattern, D-015) — never read from a token claim. Platform roles (`IPlatformRoleService`, D-040) and trust capability flags (`ITrustService`, D-046) are likewise read live per request, never from the token.
- The trust/verification/admin/fraud subsystems (`Identity/`, `Trust/`, `Admin/`, and the `Orgs/…VerificationService` services) follow the same folder-per-domain + `Kurx.Application.Abstractions` interface pattern. Their conventions live in [`.claude/memory/trust-verification.md`](trust-verification.md) — read it before touching identity, org/membership verification, capabilities, event approval, payments, admin, or fraud.
- Throw domain exceptions for expected failures (`not_found`, `forbidden`, `conflict`); `GlobalExceptionHandler` maps them to RFC7807. Don't build error responses in the service.

## Money, IDs, time

- Money: `long` paise, column suffix `_paise` (D-004). Never `decimal`/`float` for currency.
- IDs: `Guid` v4 generated app-side (D-006).
- Store UTC; convert at the edge. Phone numbers normalized with `91` prefix for 10-digit inputs (D-012).

## Providers & background work

- Real third-party providers mostly do not exist yet — only `console`/`mock`/`localdisk` adapters (`QrCodeGenerator` uses real QRCoder; `CertificateRenderer` uses real QuestPDF, D-035; `DocumentRasterizerStub` still returns a placeholder — only needed for custom uploaded-PDF templates). Adding a real payment/notification provider (Razorpay, SES, WhatsApp Cloud, FCM, S3) is a phased change requiring architect + `D-NNN`.
- Hangfire is wired with PostgreSQL storage (D-029). **17 recurring jobs** are registered in `Program.cs` today — the "three jobs" this line used to claim went stale a long time ago; count them there rather than trusting a doc. Recent additions: `post-media-cleanup` (hourly, D-262) and `account-deletion` (daily, D-263). Dashboard at `/hangfire` (dev-only). New jobs require a `D-NNN` and phase approval.
- **A scheduled feature needs its scheduler in the same change.** D-263 shipped a 30-day account-deletion grace whose sweep existed but was registered nowhere; caught before merge. If a column names a future moment, grep `AddOrUpdate` and make sure something acts on it.

## Package pinning (do not bump without a `D-NNN`)

- EF Core + Npgsql pinned to **10.0.2** (Npgsql) / **10.x** (EF Core), targeting `net10.0`.
- SixLabors.ImageSharp pinned **3.1.x** (4.0 requires a paid license, D-011).
- Hangfire **1.8** + `Hangfire.PostgreSql`.

## Do / don't

- Do: match the existing per-domain folder + service pattern. Do: keep `Domain` framework-free.
- Don't: reference EF from `Api`; don't hand-roll error JSON; don't cache authorization in a claim; don't add a real external provider or scheduled job without a phase + decision.

## Provider boundary

Every external dependency sits behind an interface in `Kurx.Application.Abstractions`; implementations
live in `Kurx.Infrastructure`. Business logic never touches an SDK. Selection is one environment
variable per provider and **fails fast** — an unimplemented value throws at startup rather than
silently falling back to the development implementation.

Canonical reference, including how to add one: [`docs/architecture/providers.md`](../../docs/architecture/providers.md).

## Derived lifecycle state

Chat room state is **derived, not read**. `ChatLifecycle.EffectiveStatus` (in `Kurx.Domain`) is the one
place that decides whether a room is `Active`, `Locked` or `Archived`; every gate calls it, and the
only code that reads the stored status is the code that writes it. The pattern generalises: when state
changes because a timestamp passed rather than because somebody acted, derive it at the gate and let a
background sweep persist it — never make enforcement wait for the sweep.

Lifecycle transitions are compare-and-set (`UPDATE … WHERE id = @x AND status = @from`), so concurrent
callers cannot double-apply one. Detail: [`docs/EVENT_CHAT_ARCHITECTURE.md` §6a](../../docs/EVENT_CHAT_ARCHITECTURE.md).


## Wide request contracts: group, do not extend (D-265)

`CreateEventInput`/`UpdateEventInput` were already 25-parameter positional records when the
create-event wizard needed ~25 more fields. **Do not keep adding positions.** Two adjacent `string?`
parameters transposed compile perfectly and corrupt data silently.

The shape that works, and the one to copy next time a contract gets wide:

- New fields go into small **named groups** (`EventContentInput`, `EventLegalInput`, …), added as
  **optional trailing parameters**. Every request body that predates the change binds unchanged — no
  versioned endpoint, no client migration.
- **One applier, shared by create and update.** `EventService.ApplyFieldGroups` is called by both.
  The alternative is the same twenty assignments written twice, which drift the first time one side
  gains a validation the other does not.
- **Null field = leave alone. Empty string = clear.** A wizard PATCHes one step at a time, so a body
  naming one field must not null its neighbours. Both clients strip blanks before sending; the rule
  only holds if all three agree.

## A write path is only half a field (D-265)

Adding columns and a write path leaves fields **write-only**: persisted, but absent from the response
DTO, so no edit form can prefill and no page can render them. A round-trip then looks like data loss.
When adding a field, add it to the **read projection in the same change**.

**And check who reads that projection first.** `ToEventJson` serves the organiser reads *and* the
public `GET /v1/events/{slug}`. Adding the location group wholesale would have published
`MeetingPassword` to anonymous visitors. Secrets get left out of the shared projection entirely
(`EventLocationDetailView` has no password field, so it *cannot* leak) rather than filtered at the
call site, where the next caller forgets.

## Copying an entity: name what must NOT travel, never what must (D-344)

`EventService.CloneAsync` built its copy from a hand-written property list — 47 of `Event`'s 110 columns.
The other 56 were never mentioned, so they landed on their C# defaults. Every column D-265 and D-266 added
after that list was written updated the entity and `ApplyUpdateAsync` and left the clone alone: the legal
terms, the consent gate, the age and gender limits, the tax treatment, `RegistrationPolicy`. Measured
against the running API, cloning silently dropped **30 columns**, including `RequiresConsent` true→false
and `TaxInclusive` false→true.

**An allowlist rots because nothing forces you to extend it.** Copy the whole row (`ShallowCopy()`), then
reset the named exceptions — identity, runtime state, moderation, review outcomes, lineage. A column added
later is then inherited by default, and the failure mode inverts from silent config loss to a visible
wrong value someone reports.

The same shape applies to any "build a new X from an existing X" path. If you write `new Thing { A = src.A,
B = src.B, … }`, you have written a list that will be wrong within two sprints.

**Prove it with reflection, not with a list.** `EventCloneTests` enumerates the entity's properties, probes
every inheritable one with a non-default value, clones over real HTTP and compares. Two name-sets encode
the intent (re-derived, blanked); anything in neither must survive the copy. A second hand-maintained list
in the test would rot exactly like the first.

## Capabilities decide behaviour, never authorization (D-266 M2)

`CapabilityResolver` answers *"what does this event support?"*. It takes an archetype slug, an
`EventProduct`, a mode and the persisted matrix — **no user id, role or permission ever reaches it**.
"Who may do this?" is D-269 (`IEventAuthority`) and the two must never meet: a capability check is not an
access check, and an access check is not a capability check.

Pipeline, fixed order, no special case for any slug:
**Product → Archetype → Matrix → Dependency DAG → Defaults**.
Unsupported wins, Required wins, defaults never override the matrix.

**A slug belongs in the capability catalog only if two archetypes could reasonably differ on it.** The
catalog was 57 and is now 30 because the other 27 were Registration / Ticketing / Invitation / Scheduling /
Eligibility / Finance / Infrastructure concerns carrying a capability flag — each with a duplicate source of
truth in its own subsystem. Before adding a slug, check the owning subsystem does not already model it.

**Absence in the matrix means Unsupported, not "off".** `Off` = available and unchosen; `Locked` =
forbidden. Never collapse them: `Off` tells a client to render a toggle, `Locked` tells it not to.

**Organiser workspace tabs are composed, not derived from capabilities.** Each layer implements
`IWorkspaceContributor`; `WorkspaceComposer` merges them. Every surface declares a **unique `Order`** and a
collision fails startup — deliberately no alphabetical fallback, so tab position never depends on naming.
Deriving tabs from the capability catalog is what made `registration` leaving the engine delete the
Registrations tab.

Full reference: `docs/architecture/CAPABILITY_ENGINE.md`.


## Review lifecycle: states are the engine, authority is not (D-266 M4)

`EventStatusWorkflow` owns which transitions exist and which actions are reviewer-scoped
(`ReviewerActions`, `ActionsRequiringReason`, `ActionsRequiringNotes`). It does NOT own who may invoke
them — that is `IEventAuthority.ResolveAsync` (D-269). An organiser who can manage an event still cannot
decide their own submission.

**Never spell out reviewer actions at a call site.** Authorization reads `ReviewerActions` from the
workflow, so adding an action cannot forget to authorize it.

**A decision and its status change share one save.** `VerificationReview` rows are added in the same
`SaveChangesAsync` as the transition; two writes could leave a status with no recorded decision behind it.

**Only verdicts are recorded.** Claim and release are queue mechanics — writing rows for them turns
"what did the reviewer decide" into a filtered query instead of a read.

**`IsEditLocked` is the only definition of the edit lock**, and `ChangesRequested` is deliberately NOT
locked: editing is the purpose of that state.

**Retiring an enum member means auditing strings too — across the whole repo, not just the backend.** The
Stage 4 sweep for `EventStatus.InReview` reported zero while a test still asserted the lowercased
`"inreview"` from an API response, and web/admin/Flutter each still hard-coded it in status filters, badge
maps and action tables. An enum-symbol grep cannot see serialized forms; grep the lowercased member name
across every surface. Nothing here fails at build time — an empty tab looks like an empty tab.

**A status filter the backend cannot parse is silently dropped, so a stale value inverts the filter.**
`ListForAdminAsync` does `Enum.TryParse` and skips the `Where` on failure: the admin "review" tab kept
sending the retired `inreview` and went from listing the queue to listing **every event on the platform**.
When you retire a member, the client filters are part of the change.

**Retarget legacy actions, do not delete them.** `submit_review` and `reject` predate M4 and clients still
post them; both were pointed at the new states rather than removed. Deleting them would have broken the
existing admin queue for no gain — and retargeting `publish` without also widening its readiness-gate
guard silently skipped the gate, which is how it failed first time.

Full reference: `docs/architecture/REVIEW_LIFECYCLE.md`.


## Institutional authorization ≠ event authority (D-266 M5)

Two different things share one English word, and the codebase says which it means at every definition site.

| | `IEventAuthority` (D-269) | `EventAuthorization` (M5) |
|---|---|---|
| Question | may **this caller** act on this event | did **the institution** consent to this event |
| Lifetime | resolved per request, stored nowhere | reviewed evidence, persisted |
| Failure | 403 / 404 | `event_authorization_required` publish blocker |

The integration suites are `EventAuthorizationTests` (D-269) and `EventAuthorizationDocumentTests` (M5) —
never merge them; a shared name makes a failing run ambiguous about which concept broke.

**The authorization belongs to the event, never to the organization.** Hanging it off the org turns one
signed letter into a standing licence with no moment at which it expires. It does not replace
`Organization.VerificationStatus` (D-044): org verification says the institution is real, authorization says
it agreed to *this*.

**A publish rule is written in `PolicyResolver` and nowhere else.** The chain is
`PolicyResolver` → `EventPolicyService` → `PublishBlockers`/`ReviewerChecklist` → `TransitionGateAsync`.
The service reads stored facts and passes them in as `RepresentationFacts`; the resolver decides. Adding a
publish rule means adding it to the resolver — writing one into the gate directly recreates the drift M4
removed, where the checklist a reviewer read and the rule that refused the publish were different lists.

**`TransitionGateAsync` must consume `PolicyBlockerAsync` on EVERY leg that reaches `Published`.** M4 wired
it into the `Scheduled` case only, so policy rules were reported and then ignored by the publish that
mattered — for a full milestone, undetected, because the test only exercised `schedule`. When adding a
lifecycle target, check the gate consumes the policy engine on it.

**A retired/legacy transition still needs the new gates.** `publish` (Draft → Published) survives from
before the review lifecycle; it is the path most existing fixtures use, so a new publish rule shows up
there first and loudest.


## Invitations: one policy, two methods (D-266 M6 / D9)

`Invite Only` is a single `EventRegistrationPolicy`. Username invitations (`EventInvitation.InvitedUserId`)
and invite links (`EventInviteLink`) are **delivery methods** inside it, never separate policies. A policy
answers *who may register*; a method answers *how were they told*.

**A per-invitee token is not a shareable link.** `EventInvitation.InviteToken` names one person;
`EventInviteLink` carries seats, expiry, single-use and a passcode. Overloading the first would make "how
many seats are left" unanswerable and let a forwarded token be redeemed by the wrong person.

**Shared counters are claimed in SQL, and every refusal rides in the same WHERE.** `UsedCount` uses a
conditional `ExecuteUpdateAsync` carrying cap + expiry + revocation + single-use, so the database picks the
winner. Read-modify-write is the coupon over-redemption bug (D-240/D-261) wearing a new table name. Prove it
at 100 concurrent claimants, not 2 — a non-atomic increment can pass at low N.

**Idempotence is a unique index, never a read-then-write** — two concurrent redemptions both pass a read.
When the index rejects a racing duplicate, hand the claimed seat back or the count ends up overstated.

**Verify a secret before consuming a resource.** A wrong passcode is refused before the seat claim;
otherwise guessing drains the link without ever getting in.

## The eligibility engine is one function, and placement inside it matters

`AudienceService.EvaluateAsync` is the ONLY eligibility engine — order creation, both group-join tails,
ticket transfer, gate admission and discovery all route through it. A new registration rule goes there once,
never into each caller.

**Put the check ABOVE the `rule is null` early return** when it does not depend on an `AudienceRule`. An
`Invite Only` event rarely also carries one, so a check placed after that return would leave every
invite-only event wide open — a policy that is a label with no behaviour, which is the exact failure D-266
exists to remove.

**Ask the owning service, don't query its tables.** `EvaluateAsync` calls
`IInvitationService.HasAccessAsync` rather than reading the invitation tables itself, so "invited" has one
definition instead of two that drift the first time a method changes.


## A reviewer's checklist is derived, never stored (D-266 M7)

`EventReviewChecklistItem` records **ticks only**. The item list is a projection of
`PolicyResolver.ReviewerChecklist` — the same array `PublishBlockers` comes from — recomputed on every read.

**Why it is not stored.** A persisted list freezes at the moment the event entered review. A rule added
afterwards, or an event edited into needing one, becomes invisible to the person approving it: they tick a
complete-looking list while the publish still refuses. That is the M3 drift the policy engine exists to
prevent, reappearing one layer up.

**Ticks are per (event, reviewer, item).** Release-and-reclaim must not let one reviewer inherit another's
sign-off — the point of a checklist is that the person approving did the reading.

**Refuse a tick against an item not on the current list** (`unknown_checklist_item`). Otherwise
completeness can be reached without any real requirement having been read — the gate bypassed, not met.

**Unticking re-blocks, and clears the timestamp.** "Complete" describes the current state, not a latch a
reviewer once passed through.

## Confidential fields are excluded by construction, not filtered per call site

`PendingEventView` has no `MeetingPassword` field at all — the DTO cannot carry it, so no projection of it
can leak one. Filtering at each call site works until the next call site, which is the one that forgets.
Apply the same shape to any field that is secret everywhere (D-266 §5).
