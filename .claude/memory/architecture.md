# Architecture

Full detail lives in `docs/architecture/overview.md` — this file is the short-context pointer, don't duplicate it here.

## ⚠️ Event system — read V3 before touching it

The event system **was** rebuilt to **Event Architecture V3** (`docs/architecture/EVENT_ARCHITECTURE_V3.md`, adopted D-131, **frozen — never redesign it, there is no V4**), delivered by the 18-phase `docs/architecture/V3_IMPLEMENTATION_ROADMAP.md`. **All 18 phases (0–17) are complete as of 2026-07-29** (Phase 17 / D-192 was the last). Live phase status stays `docs/roadmap/README.md` — read it rather than this line.

"Complete" means the *backend program* is done end-to-end, not that every client consumes it: the §12 Client Integration Backlog in `V3_IMPLEMENTATION_ROADMAP.md` is scoped, named future work. What this changes for you is that the new model is **authoritative**, not a shadow — `InventoryPool` owns oversell via conditional decrement, the `Registration → Admission → Credential` chain is produced inside the money transaction, and `Order`/`Ticket`/`TicketType.Sold` are retained **legacy mirrors**. Never read availability from `TicketType.Sold`.

Before adding anything to events, taxonomy, registration, capacity, or event money: **check V3 for where that concept already has a home.** The whole program exists because the previous taxonomy (145 types) was read by no filter, sort, permission check, pricing rule, notification, or analytics grouping — building more of the same is the exact failure being corrected. An implementation blocker is reported and recorded as a new `D-NNN`, never by amending V3 in place. The old D-130…D-180 program reservation is **spent** — decisions now continue in the shared range, and the **next free number is D-329** (D-328 is the latest; `grep -c "^## D-" docs/DECISIONS.md` is the check, and a concurrent session may have taken one since — verify before claiming it).

## One-line map

`backend/Kurx.{Api,Application,Infrastructure,Domain}` (.NET 10, Clean-ish layers, Postgres 17, Hangfire 1.8 recurring jobs) · `web/` (Next.js, public site + the signed-in app: Workspace is a person's own event list, D-267 — there is no organizer dashboard; Server Components/Actions) · `admin/` (Next.js console — **27 pages**, of which 24 sit inside the console shell, over the `/v1/admin` surface: verification, events + pending/review queues, users, orgs, staff/roles, audit, blacklist, risk, reports, analytics, competitions, certificates, speakers, sponsors, broadcast, security, health, event-taxonomy; **no messaging surface by product decision**. The old "14 pages over 24 admin endpoints" here was measured before half the console existed — count it, don't quote it) · `mobile/` (Flutter — feature-first Clean Architecture, D-019: auth incl. trusted devices/passkeys/recovery, events, orders, chat, certificates, gamification, organizer screens).

Three social/account modules were added on top of that base and exist on **backend + web + Flutter**:

- **Posts** (D-262) — `Kurx.Infrastructure/Posts/`, a social feed alongside Events: text/images/video/documents/polls, event posts, mentions, hashtags, like/comment/reply/share/save/report, and full-text search (D-297). Visibility is one predicate (`public` / `connections` / `event_participants` / `only_me`) composed by every read path — **including search**, which composes `VisibleTo` first rather than filtering after; the feed is an explicit graph, not "everything public". Free-text search uses `websearch_to_tsquery` + a GIN index on `to_tsvector('english', "Body")`, the same idiom as chat message search (D-295) — there is one search idiom here, not two.
- **Account settings** (D-263) — `Kurx.Infrastructure/Users/AccountService.cs`: notification preferences (enforced inside the single `NotifyAsync` dispatch point), symmetric user blocks, language, username history, email change, scheduled+reversible account deletion. **No TOTP** — a decision, not a gap.
- **Direct messages** (D-264) — `Kurx.Infrastructure/Chat/DmService.cs`. A DM *is* a `ChatRoom` with a null `EventId`, so it reuses event chat wholesale. One room per pair by unique index; non-ally DMs land as silent requests.

- **Chat roles are independent of event roles** (D-300/D-301) — `ChatMemberRole` is `{ Member, Moderator, Host }`, ordinal order **is** rank order. Automatic Host comes from exactly four sources: the event's creator, and an organization Owner, Manager or Representative. **Staff, Speaker, Judge, Mentor, Volunteer, Participant and ticket holders are all Members**; Moderator is only ever an explicit promotion by a Host. Two predicates carry the whole model — `CanModerate(role) => role >= Moderator` and `OutRanks(actor, target) => actor > target`, **strictly** greater, which is what stops a Moderator acting on a peer or a Host. Promotion is Host-only (`canManageModerators`), and `canManageRoom` (lock, post policy) is deliberately *not* implied by `canModerate`: a Moderator moderates people, never the room. `ChatHostRoles` and `ModeratorRoles` are separate arrays on purpose — chat borrowed the latter before D-300, which is how an operational seat came to confer ban powers.

- **Authority changes reach published rooms through the outbox** (D-304) — a membership/role/removal change stages `org.authority_changed` on the *same* transaction; `OutboxDispatchJob` then calls `ChatService.SyncOrgAuthorityAsync`. The payload carries `{orgId, userId}` and **never the resulting role**: the handler re-resolves live authority per event, which is what makes redelivery a no-op and concurrent changes converge on committed state rather than on message order. Never call `IChatService` inline after commit — that was the D-299 failure, and D-304 is the same rule applied to its last three producers.

- **Filing must be reversible** (D-306) — `ChatMember.ArchivedAt` is personal and per-member. `GET /v1/me/chats?archived=` and `MyChatView.Archived` mirror the DM shape; before them, archiving an event room removed it from the only list that could return it. Not to be confused with `ChatRoom.Status == Archived`, which is the room's own lifecycle (D-122).

## Layering rule

`Api → Infrastructure → Application (interfaces) ← implemented by Infrastructure`, everything depends on `Domain`. `Api` never calls EF directly for business logic. Full detail: `docs/architecture/overview.md#layering`.

## Authorization

Four live, DB-read mechanisms (never trusted from the JWT):

1. **Platform roles** in `platform_roles` (SuperAdmin/VerificationReviewer/FinanceOps/Support/ReadOnlyAuditor, resolved per request by `PlatformRoleClaimsTransformation`, M2/D-040 — the old `IsKurxAdmin` boolean was dropped, D-041).
2. **Event authority** — `IEventAuthority` (D-269) is the **single** source for "may this caller act on this event". One ordered level (`None < Participant < Staff < Manager < Admin`), every permission a threshold on it. The event's **creator** reaches `Manager` with no membership (D-268); so does a `Representative` (D-075) or an `Owner`/`Manager` seat. `Finance` holds nothing on events. Never re-implement this per service — eleven copies used to exist and their drift hid two live defects.
3. **Per-org resource roles** (`OrgService.RoleAsync`; `IEventAuthority.ResolveOrgAsync` for org-owned assets — speaker/sponsor/venue libraries, series, representation authority), Owner/Manager/Staff/Finance, D-015.
4. **Trust capability matrix** (`ITrustService`, M7/D-046) composing person identity, org verification, membership, and fraud into live flags that gate paid organizing / payouts / event approval.

**Event authority decides WHO may act; the Capability Engine (`ICapabilityService`) decides WHAT an event supports.** They never consult each other — a capability is not a permission. The full trust subsystem (M0–M13) is documented in [`.claude/memory/trust-verification.md`](trust-verification.md). See `docs/architecture/overview.md#authorization-model`.

## Provider boundary

`Kurx.Application.Abstractions` interfaces (`IEmailSender`, `IPaymentGateway`, `IRouteClient`, `IStorage`, `IKycProvider`, `IPushSender`, `ICertificateRenderer`, `IQrCodeGenerator`, `IDocumentRasterizer`, ...) implemented by dev providers (console/mock/localdisk). Certificate rendering (QuestPDF) and QR (QRCoder) are **real**; every **network** provider (Razorpay + Route, SES, WhatsApp Cloud, FCM, S3, DigiLocker KYC) is still a mock/console/localdisk stand-in and a gated later phase — full status in `docs/EXTERNAL_SERVICES_AND_PROVIDERS.md`. (No `ISmsSender` exists.)

## When this file is stale

If `docs/architecture/overview.md` says something different, that file wins — it's maintained alongside the code by the `architect` agent. Fix this pointer, don't trust it blindly.
