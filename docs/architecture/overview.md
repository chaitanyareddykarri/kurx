# Architecture overview

> **Event system: being rebuilt to Event Architecture V3 ([D-131](../DECISIONS.md)).** This overview
> describes the **current shipped** system. The frozen target design for the event system — Kinds,
> capabilities, OrgUnit tree, audience rules, inventory pools, registration/admission/pass — is
> [`EVENT_ARCHITECTURE_V3.md`](EVENT_ARCHITECTURE_V3.md), delivered by
> [`V3_IMPLEMENTATION_ROADMAP.md`](V3_IMPLEMENTATION_ROADMAP.md) (18 phases; live status in
> [`../roadmap/README.md`](../roadmap/README.md)). Where this file and V3 disagree, **this file is what
> exists and V3 is what is coming** — neither is wrong. Each phase updates this file as it lands.
>
> **Landed so far:** Phase 0 (guardrails); **Phase 1 — Kind registry** (2026-07-22): the closed 20-Kind
> catalog (`event_kinds`) + 145 data-driven aliases (`kind_aliases`) + a derived `events.kind_slug` +
> `GET /v1/kinds`; and **Phase 2 — Capability registry** (2026-07-22): the ~45 capabilities
> (`capabilities`) + the §19 Kind×Capability matrix (`kind_capability_defaults`) + a per-event resolved set
> (`event_capabilities`, gated by mode and the depends_on DAG) + `GET /v1/capabilities`. All additive over
> the still-present 3/13/145 taxonomy; trust-capabilities and the D-116 workspace-capabilities are untouched.
> **Phase 3 — Money** (2026-07-22): an additive `currency` column (ISO-4217, default INR) on every
> money-bearing table + `settlement_currency` on events/organizations, the `Money` value type, and
> `settlement_currency` on event detail. Existing `*_paise` amounts (D-004) unchanged; multi-currency
> settlement is out of scope (V3 §9.1).
> **Phase 4 — OrgUnit tree** (2026-07-22): the recursive `org_units` hierarchy (materialised `path`, one
> DB-enforced root per org) + a nullable `events.org_unit_id` auto-bound to the org's root unit + a permission
> chain-walk seam. Backend-only, additive; existing per-org authz is unchanged (a one-node tree resolves to the
> same org-level grant). Unit-scoped grants + sub-unit pickers are later phases (V3 §4.1).
> **Phase 5 — Audience rules** (2026-07-22): a per-event `audience_rules` table + `memberships.attributes`
> (cohort_year, …) and `source`. Server-side eligibility — DENY BY DEFAULT when a rule exists, open when none —
> enforced at registration (`OrderService` → 403 `not_eligible`) and re-checked at admission (`GateEntryService`
> flags, never voids). Organiser RBAC is unchanged; eligibility is a separate registration gate (V3 §4.4).
> **Phase 6 — Participants** (2026-07-22): a platform `participant_roles` registry (7 classes) + `event_participants`
> (Person/OrgUnit subject; Team is Phase 10), unifying V2's EventAssignment / capability people-lists / org RBAC.
> The §5.4 permission union adds participant grants as a fourth, **event-scoped** source (an event ORGANISER
> participant manages the event without an org membership), resolved live by `IEventPermissionService`. V2
> `EventAssignment` is kept + backfilled from; COI (§5.5) is **now enforced in Phase-11 judge scoring** (a judge
> cannot score their own team or self); policy-driven conflict rules (`ScoringPolicy.conflict_rules`) remain for a
> later pass (V3 §5).
> **Phase 7 — Inventory pools** (2026-07-23): `inventory_pools` (§8) as an additive **dual-write shadow** of the
> scalar Quantity/Sold — one general pool per TicketType, `consumed` synced to `Sold` at every sale/refund/
> hold-expiry; the waitlist re-points onto pools; a reconciliation job alerts on drift (§17.1). The scalar was the
> oversell authority at this phase; **pools became authoritative in Phase 9** (below). Inventory authz reuses the
> Phase-6 `event:manage` union (V3 §8).
> **Phase 8 — Registration layer** (2026-07-23): the `registrations` → `admissions` → `credentials` chain +
> five-axis `registration_policies` (§7), an additive **dual-write shadow** of Order/Ticket. Each order projects
> the chain **post-commit** (§17.1 "never inline") on **its own DbContext** — a Registration, an Admission per
> ticket (linked to the Phase-7 pool), one Credential per person per event **tree** (keyed on the tree root, §9.3);
> a daily reconciliation job proves the model matches and **self-heals** drift. Order/Ticket were authoritative at
> this phase; **the cut-over landed in Phase 9** (below), where the chain became authoritative and is produced in
> the money transaction. Subject is Person (a party booking is one Registration with N Admissions, §6.1); TEAM and
> the later-subsystem gates are stored-but-deferred. Authz reuses `event:manage`.
>
> **Phase 9 — Authority cut-over: Passes, VAR, §17.1 concurrency** (2026-07-23, Option A): the new model becomes
> **authoritative**. Inventory pools own oversell via a **conditional decrement** (`consumed + held + n <= total +
> oversell`, under the pool row lock) — never `TicketType.Sold += 1`; the registration → admission → credential
> chain is now produced **in the money transaction** (§6.1), not post-commit. Each order writes a `Pass` +
> `AdmissionRight(SINGLE)` (§9.2) and an immutable **VAR** (§9.5, the sole refund basis). §17.1 in full: reserve-
> first holds, ascending pool-id lock order, **per-caller** client idempotency (`Idempotency-Key`, scoped to the
> user or guest phone), duplicate-callback + full-refund idempotency (atomic status claims), a per-person credential
> advisory lock, and outbox-delivered side effects; the reconciliation job self-heals inventory drift to
> `Consumed == count(active admissions)`. **Every availability read resolves from the authoritative pool**, never
> the legacy `TicketType.Sold`. Order / Ticket / `TicketType.Sold` are retained as written **legacy mirrors**
> (removal is a later phase). Option A defers multi-scope Passes (Subtree/Set/Query), window_policy, cross-event VAR,
> and the later-wave subsystems.
>
> **Phase 10 — Teams (retires `Group`)** (2026-07-24, *approved / complete*): the V3 §6 Team subsystem —
> the only group entity, and only where competition exists — added **additively**. `Team` (identity + audited
> lifecycle FORMING→COMPLETE→LOCKED→COMPETING→terminal), `TeamMembership` (roles/states; substitution is an edge via
> `replaced_by_membership_id`, never a delete), `TeamInvite`, `TeamJoinRequest`, `TeamPolicy` (§6.3, on the `teams`
> capability); invites/join-requests, organiser lifecycle, and **merge/split** as guarded transactions blocked once
> a team is Competing (§6.4). The purchase `Group`/`GroupMember`/`RegistrationMode.Group` stay as **legacy mirrors**
> and the Phase-9 money path is untouched; existing competition Groups are back-filled into Teams. **Formation only**
> — team-slot inventory + the team-registration purchase flow (§6.5) are deferred to the competitive-purchase phase.

> **Phase 11 — Stages, Fixtures, Scoring** (2026-07-24, *approved / complete*): the V3 §10 competition
> engine, added **additively** (money path / InventoryPool authority / Registration→Admission / Pass / VAR / §17.1
> untouched). `Stage` (§10.1 — **not registerable**; competitors advance in, spectators are admitted; audited
> Draft→Live→Closed), `StageParticipant` (roster), `Fixture`/`FixtureParticipant`/`FixtureOfficial` (§10.2 — **manual
> scheduling with conflict detection**; the auto-scheduler is deferred by V3), `ScoringPolicy`, `JudgeScore`,
> `PublicVote`, `StageResult`, `ResultCorrection`. A **deterministic scoring engine** (per-judge z-score, sum/mean/
> trimmed-mean/median/Borda-rank aggregation, capped public-vote blend, explicit tie-breaks + subject-id fallback —
> no tie left unresolved) with **enforceable transactional fraud controls** (judge eligibility + **COI §5.5**, one
> live score per judge/subject, **one immutable vote per identity**, rate limit, Live-window). Results are append-only
> once **Published** (corrections snapshot the prior value; certificates read `Published`/`Corrected` only, gated by
> `results_visibility`); **advancement** (TopN/TopPercent/ScoreGte/Manual) seeds the next stage. **Spectators (§10.4)
> are config-only** — `Stage.spectator_pool_id` links an `InventoryPool`; the purchase flow is deferred (no money-path
> change). Statistical anomaly detection is deferred to a later analytics phase.

> **Phase 12 — Structure & series** (2026-07-24, *approved / complete*): the V3 §3 structural model + §13.2
> EventSeries, added **additively** (money path / InventoryPool / Registration→Admission / Pass / VAR / §17.1 /
> Stage·Fixture / Team untouched). **AgendaItem (§3.1 CONTAINMENT)** — the existing `EventSession` reclassified and
> extended with an optional `inventory_pool_id` (§3.4 rule 3: may hold a pool for a seat limit; still no Pass/
> Registration/Credential). **Composition depth ≤ 3** (§3.4 rule 1) enforced in the API on sub-event creation.
> **Structural discovery** (§3.4 rule 2) — `Event.ListedStandalone` (roots/editions discoverable by default, a
> sub-event only on opt-in) filters the public discovery feeds; direct access unaffected. **EventSeries (§13.2)** —
> `event_series` (RECURRING/EDITIONS; RFC-5545 rrule + exception dates; brand assets) + `event_series_followers`;
> an event links to ≤1 series (§3.4 rule 5, series never nest) via `SeriesId` + `EditionOrdinal`/`EditionLabel`;
> per-occurrence timezone reuses `Event.Timezone`. Deferred to later phases: push-to-children inheritance (§3.5 →
> Phase 15), cancellation cascade + material change (§14 → Phase 14), the outbox-fed search index + RECURRING
> one-listing collapse (§15 → Phase 16), series analytics (§17), and RRULE auto-expansion.

> **Phase 13 — Delegated & walk-in** (2026-07-24, *approved / complete*): the V3 §7.5 SeatBlock + §7.6
> walk-in, added **additively** (money path / InventoryPool authority / Registration→Admission→Credential / Pass / VAR
> / §17.1 / Stage·Team·Series untouched). **The existing Order→Ticket projection is the ONLY authoritative minting
> path** — both reuse it. **Walk-in:** a staff participant registers at the gate against the `WalkIn`-segment pool,
> minting the chain in one transaction; **offline replay is DB-enforced** via a staff-scoped Phase-9 idempotency key
> plus a partial-unique index covering the NONE-identity case, so a concurrent replay returns the original — never a
> duplicate registration or double consume. **SeatBlock:** an org unit reserves N **unassigned** admissions (PersonId
> null); the **delegate console** (`SeatBlockSeat`) binds people, governed by assignment deadline + reassign limit
> (serialised by a per-seat advisory lock) + a per-assignment audit (§7.5). **Credential lifecycle:** assignment
> re-runs the projection to bind the one-per-tree credential; reassignment/unassignment revoke the previous assignee's
> now-orphaned credential (never leaving a stale Active one, history preserved). Delegated payment (§9.7) is
> **data/authz only** —
> `payer_id` + FREE|DEFERRED; no live collection this phase. New tables `seat_blocks`/`seat_block_seats`; no Phase-8/9
> entity changed. Deferred: prerequisites (§7.3), lottery (§7.4), gateway settlement (§9.7), and the client surfaces
> (Web delegate console, Flutter gate/offline mode).

> **Phase 14 — Lifecycle, gates & approvals** (2026-07-24, *approved / complete*): the V3 §14 lifecycle +
> five validation gates + internal approval chains + material change, added **additively** (money path / InventoryPool
> authority / Registration→Admission→Credential / Pass / VAR / §17.1 / Stage·Team·Series·SeatBlock untouched).
> **§14.1:** `Published` stays the **authoritative registration-open state** (its 30 gate sites unchanged); `Scheduled`/
> `Live`/`Completed` + `schedule`/`open_registration`/`go_live`/`complete` are added around it, the existing `publish`
> preserved. **§14.2:** each transition enforces only its own gate (venue-or-URL + owner unit + approval on schedule;
> pass + pool + currency on open-registration; staff on go-live; results on complete). **§14.3:** `ApprovalChain` on an
> OrgUnit, **inherited down the tree** (an event resolves the chain on its owning unit or the nearest ancestor); steps
> (role|user approver, condition, SLA/escalation metadata), SEQUENTIAL|PARALLEL, approve/reject/**bypass** (bypass =
> Owner/admin, always audited); order internal chain → platform review (§14.4, preserved) → published; the escalation
> timer is deferred. Approval completeness is **re-evaluated on every gate check** so a condition that becomes true after
> materialisation (e.g. a free event becoming paid) re-opens the request; a rejected request can be **resubmitted** into
> a fresh cycle (`POST /events/{id}/approval/resubmit`) with the prior decisions preserved in the audit spine. **§14.5:**
> a material change (date/venue/mode, or a **sub-event cancellation**, after a registration exists) records before/after
> in the audit spine, notifies registrants, and opens a refund window (`Event.RefundWindowEndsAt`, never shortening an
> already-open one) — no auto-refund. **Correction (D-199):** "registrant-initiated" here was always aspirational, not
> built — `IRefundService.RefundOrderAsync` has no self-service path; D-199 gave it its first HTTP surface faithful to
> what the service actually does — Owner/Finance of the event's org, or platform FinanceOps/SuperAdmin, refund on the
> registrant's behalf. A true self-service registrant-initiated refund remains unbuilt.
> New tables `approval_chains`/`approval_steps`/`approval_requests`/`approval_step_decisions` + one nullable column; no
> approved entity changed.

> **Entry model: event-first ([D-074](../DECISIONS.md)).** No organizer/owner accounts. The org-role matrix (Owner/Manager/Staff/Finance) in *Authorization model* below is the org's **internal RBAC** and remains valid; the **creation/entry** path is event-first — a not-yet-verified org is a staged verification request an admin approves, which materializes the org and links the submitter as a **Verified Representative**. See [`event-creation.md`](event-creation.md).

## Stack

| Layer | Tech |
|---|---|
| Runtime | .NET 10 (`net10.0`), SDK pinned at `10.0.301` via `global.json` |
| ORM | EF Core 10 + Npgsql 10.0.2 |
| Database | Postgres 17 |
| Real-time | ASP.NET Core SignalR (5 hubs); Redis backplane via `REDIS_CONNECTION` — optional in dev, **required in Production** ([D-217](../DECISIONS.md)) |
| Background jobs | Hangfire 1.8 + PostgreSQL storage (schema `hangfire`, 12 tables, inside the `kurx` database); **18 recurring jobs** (counted live from `hangfire.set`, 2026-08-14) — seat-hold and waitlist expiry, ledger settlement, inventory / registration / **wallet** reconciliation, database health probe, leaderboard and search-index refresh, notification cleanup, event reminders, signing-key maintenance, outbox dispatch, chat-room locking, chat-attachment and post-media cleanup, account deletion, and the convergence backfill moved off the boot path ([D-250](../DECISIONS.md)). Two further jobs (`ChatNotificationJob`, `PhoneE164BackfillJob`) are enqueue-driven and carry no schedule. It is the **only** background-work mechanism — the unused n8n service was deleted in [D-337](../DECISIONS.md). A Hangfire server runs on **every replica**, so each recurring job carries `[DisableConcurrentExecution]` + a cadence-matched `[AutomaticRetry]` ([D-243](../DECISIONS.md)); per-message jobs deliberately do not. Dashboard at `/hangfire` (dev-only) |
| API contract | OpenAPI generated from the running API, committed at `docs/api/openapi.json`, and **gated in CI** — a surface change that skips `scripts/generate-openapi.sh` fails the build ([D-259](../DECISIONS.md)). Responses are snake_case, requests camelCase. Response bodies are declared on **496 of 535 operations** (461 JSON + 32 `204` + 3 binary downloads as `format: binary`; re-measured 2026-08-14 with `scripts/openapi-response-check.mjs`), ratcheted upward ([D-246](../DECISIONS.md), [D-313](../DECISIONS.md)) and enforced by `scripts/openapi-response-check.mjs`, which fails CI on a new undeclared response. A response DTO **must** be declared in `Kurx.Application.Abstractions` or `SnakeCaseResponseConverter` skips it and it silently emits camelCase. `required` is derived by `RequiredFromNonNullableSchemaFilter` because the pinned Swashbuckle 6.6.2 predates the switch for it |
| Frontend | Next.js 14 (`web/`), next-intl 3.26.3 for i18n |
| Mobile | Flutter 3.44.6 (`mobile/`, pinned in CI) — **not attendee-only**, which this row claimed: 95 screens across 17 feature areas, 24 of them organizer screens (check-in scanner, attendees, announcements, certificates, team assignment). Per-screen wiring status: [`../roadmap/README.md`](../roadmap/README.md) |
| Admin | Next.js (`admin/`) — internal staff console, 20+ modules live (verification, event approval, users/orgs, blacklist/fraud, staff & roles, audit, analytics, categories, certificates, broadcast, health); premium UI redesign D-185. Current status: `admin/STATUS.md` |
| CI/CD | GitHub Actions — `ci.yml` (build/test) + `cd.yml` (GHCR + SSH deploy) |

## Layering

Kurx's backend (`backend/`) follows a Clean-ish layered architecture:

```
Kurx.Api            → HTTP endpoints, SignalR hubs, middleware, composition root (Program.cs)
Kurx.Application    → Interfaces only (IAuthService, IOrgService, IStorage, IRealtimeBroadcaster, ...)
Kurx.Infrastructure → Implements Application interfaces: EF Core, auth, org/event services, dev providers
Kurx.Domain         → Entities and enums, no framework dependencies
```

Dependency direction: `Api → Infrastructure → Application ← (implemented by) Infrastructure`, and everything depends on `Domain`. `Api` never talks to EF Core directly for business logic — it calls into `Kurx.Application` interfaces, resolved via DI to their `Kurx.Infrastructure` implementations.

`Kurx.Tests` boots the real `Kurx.Api` app via `WebApplicationFactory<Program>` against a real `kurx_test` Postgres database (no mocked persistence layer) — see `KurxApiFactory`.

## Backend directory layout

```
backend/
  Kurx.Api/
    Endpoints/          72 files (`ls Kurx.Api/Endpoints/*.cs | wc -l`, 2026-08-15) carrying 534
                        route registrations. This line said "25", then "70"; re-derive it rather
                        than trusting it. Recent: Post (D-262), Account + Dm (D-263/D-264).
                        Older list, still indicative: (Auth, Org, Event, Category, Venue, Speaker,
                        Sponsor, Schedule, Media, Template, TicketType, TicketTransfer,
                        Gate, PublicProfile, Webhook, Invitation, Announcement, Chat,
                        Order, Wallet, Certificate, Identity, MembershipClaim,
                        AdminOrg, AdminFraud) — no KycEndpoints (dead file deleted, M9/D-048)
    Hubs/               ChatHub, SalesHub, ScanHub, NotificationHub, LoginHub
    Middleware/         CorrelationIdMiddleware
    Auth/               PlatformRoleClaimsTransformation (live platform-role claims, M2/D-040)
    ExceptionHandling/  GlobalExceptionHandler, ProblemResults
    HealthChecks/       LocalDiskStorageHealthCheck, HealthReportWriter
    Realtime/           SignalRBroadcaster (IRealtimeBroadcaster implementation)
    Validation/         FluentValidation validators
  Kurx.Application/
    Abstractions/       Service + provider interfaces in Providers.cs (IEmailSender,
                        IWhatsAppSender, IPaymentGateway, IRouteClient, IStorage,
                        IKycProvider, IPushSender, INotificationService, IWalletService,
                        ICertificateRenderer, IQrCodeGenerator, IDocumentRasterizer,
                        IPlatformRoleService, ITrustService, IFraudService,
                        IIdentityVerificationService, IOrganizationRegistryService,
                        IOrgVerificationService, IMembershipVerificationService,
                        IAdminVerificationService, …). No ISmsSender (never existed).
  Kurx.Infrastructure/
    Auth/               AuthService, TokenService, PlatformRoleService (M2/D-040)
    Chat/               ChatService (architecture frozen — docs/EVENT_CHAT_ARCHITECTURE.md),
                        DmService (direct messages, D-264 — a DM is a ChatRoom with a null EventId)
    Posts/              PostService (+.Writes/.Engagement), PostMediaPolicy,
                        PostTextParser — the social feed (D-262)
    Configuration/      SecretValidation
    Identity/           IdentityVerificationService (person KYC, M3/D-042)
    Trust/              TrustService (capability matrix, M7/D-046),
                        FraudService (blacklist + signals, M13/D-052)
    Admin/              AdminVerificationService (admin console, M12/D-051)
    Events/             EventService, CategoryService, VenueService, SpeakerService,
                        SponsorService, ScheduleService, MediaService, TemplateService,
                        TicketTypeService, InvitationService, AnnouncementService,
                        CertificateService, EventStatusWorkflow, seeders
    Orders/             OrderService (free + paid checkout + ledger write-path, M10/D-049)
    Orgs/               OrgService, WalletService, OrganizationRegistryService (M4/D-043),
                        OrgVerificationService (M5/D-044),
                        MembershipVerificationService (M6/D-045)
    Jobs/               20 job files; 18 of them recurring (`grep -c 'jobs.AddOrUpdate'
                        Kurx.Api/Program.cs`, 2026-08-15) — ExpireSeatHoldsJob,
                        ExpireWaitlistOffersJob, CollectedToAvailableLedgerJob,
                        InventoryReconciliationJob, RegistrationReconciliationJob,
                        WalletReconciliationJob (D-240), DatabaseHealthProbeJob (DB-9),
                        DataBackfillJob (D-250), LeaderboardRefreshJob, SearchIndexRefreshJob,
                        NotificationCleanupJob, EventReminderJob, SigningKeyMaintenanceJob,
                        OutboxDispatchJob, LockExpiredChatRoomsJob, ChatAttachmentCleanupJob,
                        PostMediaCleanupJob (D-262), AccountDeletionJob (D-263).
                        Plus ChatNotificationJob (enqueued per message, NOT recurring — a
                        class-level lock there would serialise all chat fan-out) and
                        PhoneE164BackfillJob (admin-invoked, not a Hangfire job at all).
                        This list said 15 and omitted the health probe, post-media cleanup and
                        account deletion; the count above the table (§Stack) is the same 18.
    Localization/       SharedResources.cs + SharedResources.resx + SharedResources.hi.resx
    Messaging/          WhatsAppLogService
    Migrations/         102 EF Core migrations (`ls Kurx.Infrastructure/Migrations/*.cs |
                        grep -v Designer | grep -v ModelSnapshot | wc -l`, 2026-08-18). The
                        live dev database is one behind until the API restarts and applies
                        AddStaffGateEntries — migrations run at boot (`Program.cs`).
                        Initial → EventManagement → … → the V3 program's ~22 → AddIdCards,
                        AddEntitlements, AddIdCardMealDisplay, … → AddStaffGateEntries (D-385).
                        This line said "19", which was the M0–M13 figure and predates the entire
                        V3 program; it then said "91" (2026-08-15).
    Notifications/      NotificationService — the ONE dispatch point; the D-263 notification-preference
                        gate lives inside NotifyAsync so all callers inherit it
    Persistence/        KurxDbContext — 175 DbSets (`grep -c 'public DbSet<'
                        Kurx.Infrastructure/Persistence/KurxDbContext.cs`, 2026-08-18; matches
                        the 175 `ToTable` calls in the model snapshot). This line said "70",
                        then "162" (2026-08-15). Includes verification_documents /
                        verification_reviews trust substrate M0/D-039, platform_roles,
                        user_identity_verifications, membership_claims,
                        org_bank_verifications, organization_aliases,
                        blacklist_entries, fraud_signals. Per-table reference:
                        docs/database/DATABASE_TABLES.md
    Providers/          ConsoleProviders, MockProviders, LocalDiskStorage, DocumentProviders
                        (real QuestPDF CertificateRenderer + real QRCoder QR +
                        DocumentRasterizerStub)
    Ticketing/          GateEntryService, TicketTransferService
    Users/              PublicProfileService, AllyService, ReservedUsernames,
                        AccountService (settings, blocks, deletion — D-263)
  Kurx.Domain/
    Entities/           42 files (`ls Kurx.Domain/Entities/*.cs | wc -l`, 2026-08-15), one per
                        bounded area rather than one per entity — Users, Identity, AuthIdentity,
                        Orgs, PlatformRoles, Verification, Events, EventManagement,
                        EventArchetypes, EventAuthorization, EventExposure, Capabilities, Kinds,
                        Orders, EventCommerce, Money, Registration, Participants, Teams,
                        Competition, Series, Inventory, Entitlements, Passes, IdCards, Chat,
                        ChatLifecycle, Posts, Allies, Gamification, Design, Operational,
                        Invitations, Approvals, Audience, Analytics, Search, Fraud, and the rest.
                        This line listed 14 of them.
    Enums/              All enum definitions
  Kurx.Tests/           Integration test suite (WebApplicationFactory<Program>). 172 .cs files
                        carrying ~1,700 [Fact]/[Theory] attributes, which expand to more executed
                        cases via [InlineData]. **Quote a measured full run, never a number from
                        this file** — the baseline lives in `.claude/CLAUDE.md` §9 and
                        `.claude/memory/testing-standards.md`, which are updated per run. This
                        line has been stale three times; it no longer carries a run figure at all.
```

## Request pipeline (`Kurx.Api/Program.cs`)

In order:

1. **Startup gate**: `db.Database.MigrateAsync()` runs once at boot. A connection failure or a migration that can't apply aborts the host instead of serving traffic. The **nine reference-data seeders** that follow it run inside one transaction holding `pg_advisory_xact_lock` ([D-344](../DECISIONS.md)): EF Core's own migration lock ends with the migration, and the seeders are read-then-insert, so two replicas of a rolling deploy would both find a catalog slug missing and the loser would take a `23505` on `event_kinds."Slug"` — inside this gate, i.e. a boot crash. The lock also makes seeding atomic; a failure now rolls back rather than leaving the next replica a half-seeded database.
2. `CorrelationIdMiddleware` — generates/echoes `X-Correlation-Id`, pushes it into Serilog's `LogContext`.
3. `UseExceptionHandler()` — `GlobalExceptionHandler` catches anything unhandled and returns an RFC7807 `ProblemDetails` response.
4. `UseSerilogRequestLogging` — one structured log line per request, enriched with `CorrelationId`/`UserId`/`OrgId`.
5. `UseCors()`. **Correctly after the exception handler**, not before: `CorsMiddleware` registers a `Response.OnStarting` callback on the way in, and `ExceptionHandlerMiddleware.ClearHttpContext()` clears headers but not those callbacks — so a 500 still carries `Access-Control-Allow-Origin`. Verified against a minimal app reproducing this exact order ([D-344](../DECISIONS.md)); this is also the framework's documented ordering, so do not "fix" it by moving CORS earlier.
6. `UseRequestLocalization` — detects locale from `Accept-Language` header or `NEXT_LOCALE` cookie; supports `en` (default) and `hi`.
7. `UseRateLimiter()` — global per-user/per-IP sliding window + named policies (`"otp"`, `"heavy"`).
8. `UseAuthentication()` → `UseAuthorization()`.
9. Endpoint execution — FluentValidation `IEndpointFilter` (`WithValidation<T>()`) runs before the handler.

Every error response — validation failures, expected business errors (403/404/409/429/...), and unhandled exceptions — shares the same `ProblemDetails` shape with `error` + `correlationId` extensions. See `docs/api/README.md`.

## Rate limiting

Three layers applied in `Program.cs`:

| Layer | Policy | Limit | Applies to |
|---|---|---|---|
| Global | `PartitionedRateLimiter` (fixed window, Redis-backed when configured — D-255) | 300 req/min per authenticated user; 60 req/min per IP (anonymous) | Every request |
| Named: `"otp"` | Fixed window per IP | Configurable via `RATE_LIMIT_OTP_PER_MIN` (default 20) | OTP endpoints, email/phone change |
| Named: `"resume"` | Fixed window per IP | 10/min | Resume PDF (D-228) |
| Named: `"posts"` | Fixed window **per user** | `RATE_LIMIT_POSTS_PER_MIN` (default 30) | Post + comment creation, post media presign/confirm (D-262), opening a DM (D-264) |
| Named: `"heavy"` | Concurrency limiter per user | 5 concurrent, queue 2 | CSV import, bulk send, announcement create |

`"posts"` is partitioned **per user**, not per IP: what it bounds is how fast one account can publish,
and a shared IP (campus wifi, an office) is exactly where a per-IP cap punishes everyone for one
spammer. There is deliberately no elevated admin tier in the global throttle (D-252) — the limiter runs
before `PlatformRoleClaimsTransformation`, so `kurx_admin` cannot be present at that point; the row
this table used to claim was unreachable.

Rejections return 429 with RFC7807 ProblemDetails carrying `error: "rate_limited"` and a `Retry-After`
header (D-251).

## Authorization model

Two authorization mechanisms coexist:

- **Platform roles, read live per request** (M2, D-040): `SuperAdmin`/`VerificationReviewer`/`FinanceOps`/`Support`/`ReadOnlyAuditor` are stored in `platform_roles` and resolved on every authenticated request by `PlatformRoleClaimsTransformation`, which **strips any token-supplied platform claim** (anti-forgery) and re-adds the user's current roles from the DB. Authority is never carried in the JWT, so a grant/revoke is effective on the next request. ASP.NET policies (`KurxAdmin`→SuperAdmin, `VerificationReviewer`, `FinanceOps`, `Support`) and the per-endpoint `IsAdmin` helpers both read these live claims.
- **Event authorization is one service** (`IEventAuthority`, D-269/D-272) for anything that depends on *which event* a request targets — **management and audience alike**. It resolves a caller to one ordered level — `None < Participant < Staff < Manager < Admin` — and every permission is a threshold on it, held in a single table. `Manager` is reached by the event's **creator** (`Event.CreatedBy` — the owner, D-268; needs no membership at all), by a `Representative` (D-075), or by an `Owner`/`Manager` seat in the organization the event represents (`Event.RepresentingOrgId`, D-273a — representation, never ownership); `Participant` by an accepted programme participation or a **live ticket** (D-272); `Finance` holds nothing on events. Two permission families sit on the ladder: management (`ManageContent`, `ManageLifecycle`, `ViewAttendees`, `ViewAnalytics`, `Delete`) and audience (`Participate`, `ModerateAudience` — the event feed, post attachment and chat). Eleven services used to carry their own copy of this rule — seven byte-identical, the rest drifted; all are deleted. **An organization membership alone is never event access**: three audience checks still read one directly until D-272, which is why an event's own creator silently depended on a synthetic self-representation row to reach their own feed and chat.
- **Organization-scoped role checks** (`OrgService.RoleAsync`, and `IEventAuthority.ResolveOrgAsync` for org-owned assets like the speaker/sponsor/venue libraries) for anything that depends on *which organization* a request targets — Owner/Manager/Staff/Finance per org (D-015). Queried fresh from Postgres on every request; never trusted from a token claim.

SignalR hubs require authentication (JWT via `?access_token=` query string) and re-run the same org-membership check before allowing a group join (D-017).

## i18n

- **Backend**: `IStringLocalizer<SharedResources>` with `.resx` files in `Kurx.Infrastructure/Localization/`. Used by `InvitationService` for bilingual email/WhatsApp message strings.
- **Frontend**: `next-intl` 3.26.3 with `localePrefix: "never"` (cookie-based, no URL changes). Locale detected via `NEXT_LOCALE` cookie or `Accept-Language`. Message files at `web/messages/{en,hi}.json`. Formatters (`web/lib/formatters.ts`) use native `Intl` APIs for currency/date/relative-time.

## SignalR hubs

| Hub | Path | Purpose |
|---|---|---|
| `ScanHub` | `/hubs/scan` | Gate check-in live feed |
| `SalesHub` | `/hubs/sales` | Ticket sales live dashboard |
| `ChatHub` | `/hubs/chat` | Event chat (per-room sliding-window rate limit) — [`docs/EVENT_CHAT_ARCHITECTURE.md`](../EVENT_CHAT_ARCHITECTURE.md) |
| `NotificationHub` | `/hubs/notifications` | Per-user notification pipeline; auto-joins `user:{userId}` on connect |
| `LoginHub` | `/hubs/login` | Waiting sign-in screen (AM9); anonymous by design |

Redis backplane (`StackExchangeRedis`) added when `REDIS_CONNECTION` env var is set — transparent fan-out for multi-instance deploys. ChatHub sliding-window rate limit is Redis-backed when configured; falls back to always-allow in single-instance dev mode.

**Production requires `REDIS_CONNECTION` ([D-217](../DECISIONS.md), implementing ADR-AM14).** `AddKurxInfrastructure` throws at startup if it is unset in Production, because the fallback path is silently wrong rather than merely degraded: the backplane loses cross-instance fan-out, `IPresenceService` resolves to `PresenceDisabledService`, and both the ChatHub limiter and the distributed cache become per-process (N instances ⇒ N× the configured limit) — and none of it surfaces in a health check, since `/health` only probes Redis when the variable is set. Dev and test behaviour is unchanged: the in-memory fallback is retained outside Production.

## Data/provider boundary

`Kurx.Application.Abstractions/Providers.cs` defines interfaces (`IEmailSender`, `IWhatsAppSender`, `IPaymentGateway`, `IRouteClient`, `IStorage`, `IKycProvider`, `IPushSender`, `ICertificateRenderer`, `IQrCodeGenerator`, `IDocumentRasterizer`, ...) that `Kurx.Infrastructure/Providers` implements. (There is no `ISmsSender`; the SMS boundary is `ISmsProvider`, added in AM1.) Certificate rendering (QuestPDF) and QR generation (QRCoder) are **real, in-process** implementations. Push (`FirebasePushSender`) and SMS (`SnsSmsProvider`) are **real implementations**, dormant until credentialed. Every other network provider — email, WhatsApp, payments + Route payouts, storage and person-KYC — still resolves to a `console`/`mock`/`localdisk` dev stand-in; `DependencyInjection.AddProvider` throws `NotSupportedException` if a `*_PROVIDER` env var requests a real provider that isn't built. The paid-checkout **write-path** (order → capture webhook → ledger, M10/D-049) is real and live-gated, but drives `MockPaymentGateway` until the Razorpay adapter ships. Provider architecture: [`providers.md`](providers.md). Per-vendor credentials and webhook detail: [`docs/EXTERNAL_SERVICES_AND_PROVIDERS.md`](../EXTERNAL_SERVICES_AND_PROVIDERS.md).

One boundary's mock status now has a documented consequence in the authorization layer. Because `IKycProvider` is `MockKycProvider` — `DigilockerAsync` always approves, penny-drop and PAN pass for anything not ending `0000` — the trust gates built on it are real logic over simulated evidence. `IDENTITY_VERIFICATION_BYPASS=true` ([D-323](../DECISIONS.md)) therefore skips the government-ID, PAN and bank proofs in `TrustService`, letting a plain account publish a public event, organize paid, and receive a payout in dev and test. It is scoped tightly and fails closed: `fraudClear` (blacklist + risk) stays enforced because it is real code; `IdentityVerified`/`BankVerified` keep reporting the true state so nothing forges a verification; every unset or unrecognised value means enforced; and `AddKurxInfrastructure` **throws at startup** if it is set in Production, the same shape as `SIGNING_KEY_PROTECTION=none` and a missing `REDIS_CONNECTION`. Default off — the suite runs enforced. It is scaffolding for the gap above and should be deleted when a real KYC adapter lands, not carried forward.

## Health checks

`/health` runs real dependency checks — Postgres always, Redis only if `REDIS_CONNECTION` is configured, and local-disk storage writability only when `STORAGE_PROVIDER=localdisk`. Email/SMS aren't checked (console/mock dev senders only).

## CI/CD

`.github/workflows/ci.yml` runs **four jobs**, not two — this list omitted the test steps and the
whole mobile job:
- **Backend**: `dotnet restore` → `dotnet build -warnaserror` → `dotnet test` against a Postgres 17
  service container (`kurx_test` created; artifacts uploaded) → generate the OpenAPI spec from the
  running API → **fail on contract drift** against the committed `docs/api/openapi.json` (D-259,
  compared after `jq -S` so key ordering is not reported as drift) → validate the three client model
  sets against the spec (`scripts/contract-check.mjs`) → **fail on new undeclared responses**
  (`scripts/openapi-response-check.mjs`) → verify declared response types match the handler.
- **Web**: `npm ci` → `tsc --noEmit` → `next lint` → **`npm test`** (vitest; carries the web half of
  the D-288 cross-platform phone corpus) → `next build`.
- **Admin**: `npm ci` → `tsc --noEmit` → `next lint` → **`npm test`** → `next build`.
- **Mobile**: Flutter pinned to **3.44.6** → `flutter pub get` → `flutter analyze --no-fatal-infos`
  (errors + warnings gate; ~62 pre-existing style infos tracked but not enforced) → `flutter test`.
  `build_runner` deliberately does **not** run — generated Freezed/json_serializable output is
  committed, so a stale generated file surfaces as an analyze failure.

Two things run nowhere: `node --test scripts/contract-check.test.mjs` (CI runs the contract *tool*,
never its own test), and mobile's `integration_test/` + `test_live/`, which need real hardware or a
live backend.

`.github/workflows/cd.yml`:
- Job 1: Docker build + push to GHCR (`ghcr.io/{owner}/kurx-{api,web,admin}:sha-{sha}`).
- Job 2: Staging auto-deploy on push to `main` — SSH + `docker compose up`.
- Job 3: Production deploy on GitHub Release — manual approval via GitHub Environment, auto-rollback if health check fails post-deploy.


## Capability engine (D-266 M2)

Event behaviour is resolved by one deterministic pipeline -
**Product -> Archetype -> Matrix -> Dependency DAG -> Defaults** - over 30 capabilities and 14 archetypes.
The engine **performs no authorization**: it answers "what does this event support?", never "who may do
this?". Authority is D-269's concern and the two never meet.

The organiser workspace is composed from every platform layer through `IWorkspaceContributor`, not
generated from the capability catalog: Registration, Ticketing, Invitations, Scheduling, Finance and
Infrastructure each contribute their own surfaces, with a unique `Order` enforced at startup.

Full reference: `docs/architecture/CAPABILITY_ENGINE.md`.
