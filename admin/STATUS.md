> **Event-first architecture ([D-074](../docs/DECISIONS.md)).** Kurx has no organizer/owner accounts — every person is a User who may *represent* verified organizations; a not-yet-verified org is a staged **verification request** an admin approves. Admin-console specifics below remain valid; any "org owner" phrasing reflects the legacy D-055 model, pending migration.

# Kurx Admin Console — Build Status

_Last narrative update: 2026-07-29 (Final polish pass — Audit Log/User Detail RSC fix, [D-200](../docs/DECISIONS.md)) · Counts re-measured 2026-08-12 · App root: `admin/`_

> ⚠️ **Read the counts, not the prose.** The module/page figures in §1 and §3 were re-measured against
> `nav-config.ts` and `admin/app` on 2026-08-12 and were wrong in both directions before that ("16 of 20"
> in one place, "12 modules" in another; the truth is **20 of 20, none disabled**). Six modules — Event
> Review, Competitions, Speakers, Sponsors, Event Taxonomy, Security — shipped after this file's last
> narrative pass and are **not** described in the §3/§4/§6 prose below. Treat the dated narrative sections
> as history and `components/layout/nav-config.ts` as the source of truth.

This is the **internal staff console** (`@kurx/admin`), a separate Next.js 14 app from the
public/organizer site (`web/`). It runs on **port 3001** and shares the same backend auth
and the `@kurx/ui` design system (`packages/ui/`).

**2026-07-29 — Final polish: Audit Log / User Detail RSC fix (D-200).** Both pages threw "Functions
cannot be passed directly to Client Components" on every load (a Server Component passing a `columns`
array with `render` closures straight into `DataTable`, a Client Component) — extracted into
`components/admin/{audit-log-table,user-audit-table}.tsx`, matching every other `DataTable` consumer
in this codebase. Zero visual/behavioral change; verified live (0 console errors, real data renders).

**2026-07-29 — Refund backend surface shipped (D-199).** `IRefundService` (D-103) now has an HTTP
surface: `POST`/`GET /v1/orders/{id}/refund`, `GET /v1/refunds`, `GET /v1/admin/refunds`. The admin
`/refunds` page itself is still unbuilt — only the backend gap this file's §4 table named is closed.

**2026-07-29 — Admin-scoped organization list & detail (D-194).** Closes the standing "no admin
org read" gap named by §5.4 below and independently by D-186 point 8. `/organizations` is now a
searchable, paginated org list opening into a detail workspace (wallet/risk/events, reusing D-186's
existing admin reads) instead of requiring a pre-known GUID; GUID-driven moderation still works.
No new permission model — extends the same `isAdmin`-bypass pattern D-186 used for events.

**2026-07-29 — Web → Admin trust/safety migration completed (D-195).** `web/app/(app)/host/admin/*`
(org verification, membership claims, fraud/blacklist, org moderation, the doc viewer) was always the
temporary D-055 prototype this console's Verification Queue/Blacklist/Organizations pages were ported
from (D-051, 07-13; the porting commit's own message, 07-17) — never finished. Web's copies are now
`redirect()` shims to this console; the duplicate frontend contract (`web/lib/api.ts`'s 12 functions,
3 components, 6 server actions) is removed. This is exactly the state the 2026-08 platform audit
(07-19) flagged as "worth consolidating" — now resolved. This console is the sole owner going forward.

**2026-07-29 — Admin Event Management production completion (D-191).** Closes every gap a
production-readiness audit found in D-186/D-187: `/events` now uses the real pagination the
backend has returned since D-187 (page/total/page-size, filters preserved across pages — the
frontend simply never adopted it before); the table's column-sort is wired to the same `sort`
state the dropdown already drove (D-186's `sortable: true` had no `onSortChange`, a dead
control); row selection + a bulk-action toolbar (Approve/Reject/Suspend/Unsuspend/Hide/Unhide/
Archive) call one new backend endpoint that loops the exact single-event methods the row-level
actions already call — never a second moderation implementation; CSV export (current filters,
or just the selected rows); the audit Timeline tab now queries server-side by `entityId` instead
of fetching every `events`-entity row and filtering client-side; a `VerificationReviewer` without
org membership can now view event detail (previously 404'd — only the `kurx_admin` claim was
recognized). **Policy clarified, not just patched**: Platform Admins do not edit organizer-owned
event content as normal moderation — the `kurx_admin` bypass on the organizer PATCH is removed.
A separate **Super Admin Emergency Edit** (`ActionBar`'s red "Emergency edit" button, SuperAdmin-
only) exists for compromised accounts / legal takedowns / critical corrections: mandatory reason,
full before/after audit snapshot, and — architecturally — it calls the *same* update core the
organizer PATCH does (`ApplyUpdateAsync`), so there is exactly one place "what an event update
does" is implemented. Full rationale and verification evidence in D-191.

**2026-07-28 — Platform Taxonomy Management (D-188).** The Audience/Category/Type taxonomy is no
longer developer-seeded-only. `/platform/event-taxonomy` ("Event Taxonomy" in the nav, under a new
"Platform" group) replaces the old flat `/categories` screen with a tabbed workspace: full
lifecycle (Active/Disabled/Archived — orthogonal to the existing Visible flag), rich metadata
(description/icon key/color/badge/search keywords), reorder (Move Up/Down, real API support for
drag-and-drop later), Type-level duplicate and reparent, a Type-level capability-defaults bridge
(reuses the existing V3 Capability registry — not yet wired into live event behavior), a full
audit trail with replay-capable snapshots, and merge-only import/export (never a destructive
replace). `EventTaxonomySeeder` is now a one-time bootstrap for a brand-new database only — the
Admin Console is the source of truth from first boot onward. Full rationale, and the honest list
of what's designed-for-but-not-built (capability inheritance, live capability resolution, a real
drag-and-drop UI, effective-dating, whole-tree version history), in D-188.

**2026-07-28 — Admin Event Management (D-186).** `/events` ("Event Management" in the nav) is now
a full operational workspace, replacing the old thin search+status-dropdown list: 8 status tabs
(Upcoming/Live/Completed/Draft/Under Review/Cancelled/Archived/All) over one shared `DataTable`
with the full requested column/filter/sort set, and a 9-tab detail workspace per event (Overview,
Registrations, Tickets, Attendees, Finance, Organizer, Moderation, Media, Timeline) opened via a
wide `Sheet`. New moderation actions: Suspend/Unsuspend, Hide/Unhide (new `IsSuspended`/`IsHidden`
booleans, D-186), Message organizer/Issue warning (composition over the existing notification +
audit pipeline). Approve/Reject/Archive/Cancel/Unpublish/Feature reuse the exact endpoints they
always did. Live check-in/sale ticks via the now-wired `ScanHub`/`SalesHub` (SuperAdmin only, same
`kurx_admin` reason as Certificates/Categories/Competitions below). `/events/pending` (Event
Approval) is unchanged and still works standalone — "Under Review" in the new workspace covers the
same queue with the same actions, not a replacement. Full rationale, and the honest list of fields
this pass could NOT populate from any existing endpoint (attendee email/payment/certificate
status, platform fee, org devices/logins), in D-186.

**2026-07-27 — visual redesign, no functional change.** Every module below still does exactly
what its row says (same routes, role gates, server actions, API calls). What changed is the UI
layer: list screens that predated `DataTable` (Users, Events, Reports, Blacklist, Staff, Audit)
now use it like the newer modules already did; the sidebar/topbar gained collapsible groups, an
active indicator, a `Cmd/Ctrl+K` nav palette, and breadcrumbs; row actions now surface a toast;
and `@kurx/ui` gained the primitives `web` had built locally but never shared (`Avatar`, `Chip`,
`Switch`, `Tabs`, `SectionHeader`, `Sheet`, `Toast`, `Field`/`Input`/`Textarea`/`Select`) plus a
new dependency-free `Sparkline`/`MiniBars` chart pair. Full rationale in D-185.

---

## 0. Tooling — Serena & Context7

Optional MCP servers used when working on this repo with an agentic IDE. To (re)connect:

```bash
claude mcp add serena -- uvx --from git+https://github.com/oraios/serena serena start-mcp-server --context claude-code --project /Users/naralanaveen/kurx
claude mcp add context7 -- npx -y @upstash/context7-mcp
```

---

## 1. TL;DR

**20 of 20 sidebar modules are live** — `grep -c "ready: true" components/layout/nav-config.ts` returns
20 and `ready: false` returns **0**, so nothing renders disabled any more (measured 2026-08-12):
Dashboard, Verification Queue, Event Approval, Event Review, Reports, Blacklist, Risk Flags, Users,
Organizations, Event Management, Certificates, Competitions, Speakers, Sponsors, Event Taxonomy,
Staff & Roles, Audit Log, Analytics, Broadcast, Health — plus auth, the role-based frame, the
`/users/[id]` and `/competitions/[stageId]` detail routes, `/security`, `/account`, and all
error/redirect surfaces. **24 pages sit inside the console shell; 27 exist in `admin/app` overall**
(login, reset and forbidden live outside it).

The Finance group (Payments/Payouts/Refunds) named as "disabled with a soon badge" here is **no longer
in the sidebar at all** — it waits on real payments, not on this console. Count the file rather than
quoting this paragraph; that is how it came to be wrong in two directions at once.

The two original backend blockers (`platform_roles[]` on `/v1/me`, dashboard summary
endpoint) are both resolved — details in §5.

**Phase 1 (2026-07-25)** added Categories, Certificates, Broadcast, Health, and User Detail.
Three requested screens were **not** built at the time because the backend could not support
them — **Templates**, **Organization List**, **Organization Details**. **Organization List/Details
shipped 2026-07-29 as the admin org workspace (D-194)** — see §5.4. **Templates** remains blocked
on a backend gap; see §5.3.

---

## 2. How to run & view every page

```bash
# from repo root
cd admin
npm install          # or `npm ci` at repo root (workspaces)
npm run dev          # → http://localhost:3001
```

Backend must be reachable (defaults to `http://localhost:5080`, override with `API_URL` — see `lib/site.ts`).

**To sign in during development**, use `/login` — the same flow operators use in production, because it
is the only one that exists (D-274). Request a one-time code and read it from the API log
(`docker compose logs backend | grep 'sms→console'`), or sign in with a password once the account
has one. Reaching the console then requires a real platform role: the first `SuperAdmin` is granted by
`SUPERADMIN_BOOTSTRAP_PHONE` at API startup, every one after it via **Staff & Roles → Grant**. There is
no developer login, no seeded identity, and no fake-role hatch in any build.

### Every page that currently exists

| Route | File | What renders |
|-------|------|--------------|
| `/login` | `app/login/page.tsx` | Phone → OTP two-step sign-in (client) |
| `/forbidden` | `app/forbidden/page.tsx` | "No admin access" for signed-in non-staff |
| `/` (Dashboard) | `app/(console)/page.tsx` | **✅ LIVE (D-058)** — real counts from `dashboard/summary` (pending queues, blacklist, staff, users/orgs/events), tiles link to their queues. |
| `/staff` (**Staff & Roles**) | `app/(console)/staff/page.tsx` | **✅ BUILT (D-056)** — list staff, grant a role by phone, revoke. SuperAdmin only. |
| `/verification` (**Verification Queue**) | `app/(console)/verification/page.tsx` | **✅ BUILT (Tier-A)** — org verifications + membership claims, approve/reject/request-changes, evidence-doc viewer. Reviewer only. |
| `/events/pending` (**Event Approval**) | `app/(console)/events/pending/page.tsx` | **✅ BUILT (D-057)** — queue of paid events awaiting review (`PendingReview`/`UnderReview`, D-266 M4); approve (publish) / reject. Reviewer only. The full review console (claim, reason codes, notes, history) is `/events/review`. Shows **who holds each item** — "You're reviewing this" or "Held by …" plus the claim time — because one reviewer owns an item at a time and a rule discovered only by being refused at the decision is a worse rule. |
| `/events/review` (**Event review**) | `app/(console)/events/review/page.tsx` | **✅ BUILT (D-266 M4, extended M7)** — the review console: queue tabs + counts, claim/release, approve/request-changes/reject with reason codes and notes, decision history. **M7 added the reviewer checklist (Approve is blocked until it is complete), the institutional-authorization panel, and the FinanceOps financial-review panel (rendered only when the policy engine reports `financial_review_required`)** (letterhead + signature + supporting docs as short-lived presigned URLs; approve / reject / request changes). Both panels render only while the reviewer holds the item. Reviewer only. |
| `/reports` (**Reports & moderation**) | `app/(console)/reports/page.tsx` | **✅ BUILT (D-059)** — open-report triage; resolve / dismiss (audit-logged). Moderation staff (Reviewer/Support). |
| `/users` (**Users**) | `app/(console)/users/page.tsx` | **✅ BUILT (D-060)** — search users; suspend / ban / reinstate (blocks login, audit-logged). Moderation staff. ⚠️ security review owed. |
| `/events` (**All Events**) | `app/(console)/events/page.tsx` | **✅ BUILT (D-061)** — search all events; feature/unfeature; force publish/unpublish. Reviewer. |
| `/audit` (**Audit Log**) | `app/(console)/audit/page.tsx` | **✅ BUILT (D-062)** — read-only audit_log viewer w/ filters. SuperAdmin / Auditor. |
| `/analytics` (**Analytics**) | `app/(console)/analytics/page.tsx` | **✅ BUILT (D-063)** — growth aggregates (signups/events over time, top orgs/events, by-status). Any staff. No revenue (Finance-gated). |
| `/blacklist` | `app/(console)/blacklist/page.tsx` | **✅ BUILT (Tier-A)** — hard blocklist add/list/remove. Reviewer only. |
| `/risk` (**Risk Flags**) | `app/(console)/risk/page.tsx` | **✅ BUILT (Tier-A)** — record a fraud/risk signal (record-only; no list endpoint yet). |
| `/organizations` | `app/(console)/organizations/page.tsx` | **✅ BUILT (D-194)** — searchable, paginated org list + detail workspace (wallet/risk/events), plus suspend / blacklist / merge. Reviewer only. |
| `/users/[id]` (**User detail**) | `app/(console)/users/[id]/page.tsx` | **✅ BUILT (Phase 1)** — one account plus its audit trail (`admin/audit?actor=`). There is no `GET /v1/admin/users/{id}`, so the row's search term travels in the URL and the page re-resolves the account from the list. Devices/sessions/passkeys are absent: those endpoints are self-scoped only. |
| `/platform/event-taxonomy` (**Event Taxonomy**) | `app/(console)/platform/event-taxonomy/page.tsx` | **✅ BUILT (D-188)** — replaces the old flat `/categories` screen (retired). Full lifecycle (Active/Disabled/Archived, orthogonal to Visible), rich metadata, reorder, duplicate, Type-level capability defaults, audit trail, merge-only import/export, real usage counts. SuperAdmin. §5.5's old "cannot manage hidden rows" gap is resolved — the admin list shows every status. |
| `/certificates` (**Certificates**) | `app/(console)/certificates/page.tsx` | **✅ BUILT (Phase 1)** — pick an event, then bulk-generate / revoke its roster. Event-scoped because no platform-wide roster endpoint exists; cross-org access rides the `kurx_admin` claim, so **SuperAdmin only**. |
| `/broadcast` (**Broadcast**) | `app/(console)/broadcast/page.tsx` | **✅ BUILT (Phase 1)** — platform-wide in-app notification. Confirmation-gated: the backend fans out to every user synchronously and there is no recall or audience filter. SuperAdmin. |
| `/health` (**Health**) | `app/(console)/health/page.tsx` | **✅ BUILT (Phase 1)** — live ASP.NET health report (postgres / redis / storage). Rendered per request; a 503 report is displayed, not thrown. SuperAdmin. |
| Console frame | `app/(console)/layout.tsx` + `components/layout/app-shell.tsx` | Sidebar + topbar wrapper, auth-gated |
| Loading state | `app/(console)/loading.tsx` | Skeleton grid |
| Route error | `app/(console)/error.tsx` | Retryable `ErrorState` |
| App error | `app/error.tsx`, `app/global-error.tsx` | Top-level boundaries |

Sidebar entries without `ready: true` in `components/layout/nav-config.ts` (Finance, Catalog,
Background Jobs, Health) render **disabled with a "soon" badge** — designed, not built. See
`admin/ADMIN_AUDIT_AND_ROADMAP.md` for the full module plan and build order.

---

## 3. What's COMPLETE (with locations)

### Auth & session — done
- `middleware.ts` — cookie gate + optional `ADMIN_IP_ALLOWLIST` network isolation. Public paths: `/login`, `/forbidden`.
- `lib/session.ts` — `currentSession()` (with one-shot token refresh), `requireStaffSession()` gate, httpOnly cookie handling (shared `kurx_access`/`kurx_refresh` cookies with web).
- `lib/auth-actions.ts` — server actions: `requestOtpAction`, `verifyOtpAction` (confirms staff up front), `logoutAction`.
- `lib/api.ts` — axios client + zod schemas for OTP/refresh/me, RFC7807 error mapping.
- `lib/roles.ts` — the 5 platform roles (D-040), `deriveRoles`, `hasRole` (SuperAdmin implies all), `isStaff`.

### Shell & navigation — done
- `components/layout/app-shell.tsx` — fixed desktop sidebar, off-canvas mobile drawer, sticky topbar, skip-to-content.
- `components/layout/sidebar.tsx` — role-filtered nav; disabled items render with a "soon" badge, never link to placeholders.
- `components/layout/nav-config.ts` — **the full information architecture** (see §6). All 20 entries are `ready: true`; none render disabled (measured 2026-08-12 — this line previously said 12).
- `components/layout/topbar.tsx`, `user-menu.tsx`, `theme-toggle.tsx`, `brand/logo.tsx` — chrome, sign-out, dark/light toggle.
- `components/providers.tsx` + `lib/query-client.tsx` — next-themes (class strategy, dark default) + React Query provider.

### Shared UI foundation — done (`packages/ui/`)
Admin-ready primitives already exist and are exported from `packages/ui/src/index.ts`:
`DataTable` (sortable), `SearchBar`, `FilterBar`/`FilterSelect`, `Pagination`, `ConfirmDialog`,
`StatCard`, plus the base set (`Button`, `Card`, `Badge`, `Spinner`, `Skeleton`, `EmptyState`,
`ErrorState`, `Dialog`). **Phase-2 pages have their table/filter/dialog building blocks ready.**

### Config — done
`next.config.mjs`, `tailwind.config.cjs`, `postcss.config.js`, `tsconfig.json`, `.eslintrc.json`,
`app/layout.tsx` (noindex, shared tokens). Builds clean.

---

## 4. What's PENDING (with where it will live)

Every item below is a nav entry without `ready: true` in `components/layout/nav-config.ts` today.
Each needs a route folder created under `admin/app/(console)/<name>/page.tsx` plus its data wiring.
Grouped as the nav groups them:

| Module | Planned route → new file | Backend it consumes | Roles | Blocked on |
|--------|--------------------------|---------------------|-------|-----------|
| **Payments** | `app/(console)/payments/page.tsx` | M10 payments | FinanceOps | No admin payments endpoint |
| **Payout Approvals** | `app/(console)/payouts/page.tsx` | payouts/ledger | FinanceOps | Only org self-service `wallet/withdraw` |
| **Refunds** | `app/(console)/refunds/page.tsx` | refunds | FinanceOps | Backend HTTP surface shipped (D-199: `GET /v1/admin/refunds`, `POST`/`GET /v1/orders/{id}/refund`, `GET /v1/refunds`) — only the admin UI page itself remains unbuilt |
| **Templates** | `app/(console)/templates/page.tsx` | catalog | SuperAdmin | **Published-only list — see §5.3** |
| **Background Jobs** | `app/(console)/jobs/page.tsx` | jobs infra | SuperAdmin | No endpoints; Hangfire dashboard is dev-only |

**To activate one:** create the route file, add data loaders in `lib/`, then set `ready: true`
on that item in `nav-config.ts`. See §"Still pending" for why each is blocked or deferred.

> ⚠️ Existence of an endpoint is **not** sufficient to call a module unblocked. Templates was
> planned from `GET /v1/templates` existing, and only failed acceptance testing once a created
> template proved invisible. **Check the service's filter predicate, not just the route table.**

---

## 5. Blocking backend gaps

1. ~~**`platform_roles[]` on `/v1/me`**~~ — ✅ **RESOLVED (D-056).** `GET /v1/me` now returns the
   caller's live `platform_roles: string[]`, so `deriveRoles` resolves full RBAC (Finance/Support/Auditor),
   not just `VerificationReviewer`. The `ADMIN_DEV_ALLOW_ALL` hatch was removed in D-125 and the
   `/dev-login` that replaced it in D-274 — authority is only ever a real `platform_roles` grant,
   bootstrapped once from `SUPERADMIN_BOOTSTRAP_PHONE` and managed thereafter from Staff & Roles.
2. ~~**`GET /v1/admin/dashboard/summary`**~~ — ✅ **RESOLVED (D-058).** The endpoint ships with live counts
   (pending verifications / claims / events, blacklist, staff, new users 24h, totals) and the dashboard now
   renders real numbers linking to their queues. Revenue/payouts/refunds tiles await the Finance module (Tier-C).

**Both original §5 gaps are now closed.** The gaps below were found during Phase 1 acceptance
testing (2026-07-25) and are **open**.

3. **Admin Templates list — BLOCKED (frontend cannot fix).**
   `GET /v1/templates` is the *published catalog*, not an admin inventory.
   `TemplateService.ListForContextAsync` (`backend/Kurx.Infrastructure/Events/TemplateService.cs:208`)
   hardcodes `.Where(t => t.State == TemplateState.Published …)`, and it is the **only** list method on
   `ITemplateService`. Drafts and Archived versions are therefore unlistable.
   **Consequence:** Create, New Version and Clone all produce a **Draft**, so a template created from a
   UI immediately disappears and can never be edited, published or deleted — the entire write half of a
   Templates screen is unreachable. This was proven by browser acceptance testing, not inferred.
   **Required before a UI:** an admin inventory read — e.g. `GET /v1/templates?scope=Platform&includeAllStates=true`,
   or an `ITemplateService.ListForAdminAsync(scope, states…)`. Filtering client-side is not an option:
   the rows never reach the client.
   *Status: documented only. No backend change has been made.*

4. ~~**Organization list & detail — BLOCKED (frontend cannot fix).**~~ — ✅ **RESOLVED (D-194,
   2026-07-29).** `IOrgService.GetAsync` gained an `isAdmin` bypass of the D-018 membership check;
   `IOrgService.ListForAdminAsync` added a paginated, searchable (name/slug/domain), status/type-
   filterable read. `GET /v1/admin/orgs` and `GET /v1/admin/orgs/{orgId}` ship in the existing
   `VerificationReviewer`-gated route group. `/organizations` now lists and searches every org and
   opens a detail workspace (wallet/risk/events) without requiring a pre-known GUID; the by-GUID
   moderation tools still work unchanged.

5. **Certificate generate/revoke — UNVERIFIED (needs test data, not backend work).**
   The endpoints exist and honour the `kurx_admin` bypass, and the screen is wired to them, but the dev
   database contains **0 events**, so acceptance testing could only reach the empty state and the picker.
   **Required to close:** seed an event with eligible tickets, then execute generate + revoke end to end.
   *Not a code defect — a verification gap.*

6. ~~**Categories cannot manage hidden rows.**~~ **RESOLVED (D-188).** `GET /v1/admin/categories`
   returns every status/visibility combination; the new Event Taxonomy screen lists and can
   re-enable/unhide anything.

7. **Speaker / sponsor images are not renderable in the console.** `photo_key` and `logo_key` are storage
   keys, not URLs. `GET /v1/storage/{*key}` requires a signed `sig` (`VerifyGetSignature`) and always
   responds `application/octet-stream` — deliberate, so a stored object can never be sniffed into
   `text/html` as stored XSS. Nothing mints a **GET** signature for a speaker photo or sponsor logo (the
   media-presign endpoints are for PUT). The console therefore reports whether an image *is set* and
   never fakes a preview. Fix: a presigned-GET endpoint returning a real content type.

8. **`SpeakerView` / `SponsorView` carry no timestamps** — no `created_at`/`updated_at`, so no "added on"
   column and no recency sort.

9. **`ListForEventAsync(eventId)` (speakers and sponsors) takes no `userId`/`isAdmin` — this is INTENTIONAL.**
   Reviewed 2026-07-25. Both are inside `RequireAuthorization()` groups, so a caller must be signed in, but
   no org membership is checked. That is *more* restrictive than the data's real exposure: speaker names are
   joined into the **anonymous** public discovery document (`SearchIndexService.cs:37-38`), `GET /v1/events`
   is anonymous (verified live: 200 with no auth), and `EventEndpoints.cs:83` lists speakers and sponsors as
   declarative content on the public event detail. An event's line-up is public marketing copy — that is the
   point of publishing it. **No backend change required**; recorded so the next reviewer does not "fix" it.

---

## 5c. Test-suite flake under concurrent load (2026-07-25)

**Symptom.** During Speakers verification the backend suite reported `1 failed / 767 passed` once, then
`768 / 0` on re-run. The same shape appeared once during the Phase 1 git review (`743/1` then `744/0`).

**Not a test-isolation defect.** The harness is well defended: `[assembly: CollectionBehavior(DisableTestParallelization = true)]`,
one database **per test class** (`kurx_test_<guid>` cloned from a per-process template), a `ResetLock`, and an
explicit `IsDatabaseLifecycleRace` retry for `55006 / 42P04 / 23505 / 3D000`. The observed failure was a
**write** (`ReaderModificationCommandBatch.Execute`), which those retries do not cover.

**Mechanism — shared-server connection pressure.** Both failures happened while I was running the browser QA
stack against the *same Postgres server*:
- `max_connections = 100` (server-wide, not per database).
- `AddHangfireServer()` is registered in `DependencyInjection`, and the test factory removes only
  `IBackgroundJobClient` — **the Hangfire server still runs in every test host**, holding worker connections
  (its own comment acknowledges this).
- The test factory sets **no Npgsql pool cap**, so the default max pool (100) applies *per connection string*,
  and each class has its own.
- Concurrently: the production backend container, the DEV_AUTH backend container (a second full host with its
  own Hangfire server), and browser-driven request load.

**Resolution.** Not a product or test defect — an environment misuse on my part. **QA practice: never run the
browser stack against the same Postgres while the suite runs.** Both re-runs with QA torn down were green.
Optional hardening if it recurs in CI (where no QA stack exists): cap the test pool
(`Maximum Pool Size=20`) and/or remove the Hangfire *server* in `ConfigureWebHost`.

**Open:** the failing test could not be named — console output was truncated in both runs. Capture with
`dotnet test … > file 2>&1` rather than piping to `tail`/`grep` if it happens again.

---

## 5b. Phase 2 split — 2A (buildable now) / 2B (backend capability first)

Phase 2 capability verification (2026-07-25) checked every module's **service guard and list-query
filter predicate**, not just whether a route exists — the lesson from Templates. Result: 4 of 7 are
buildable today. **The other 3 are NOT dropped from the roadmap; they are Phase 2B**, to be built once
the backend capability lands.

### Phase 2A — buildable
| Module | Admin wiring | Reachability |
|---|---|---|
| Competitions / Stages | `IsOrganiserAsync = isAdmin \|\| permissions.HasAsync(…)`; 18/24 routes pass `IsAdmin(p)` (the rest are reads + participant scoring/voting) | Event-scoped → `/v1/admin/events` |
| Speakers | 6/7 routes pass `IsAdmin`; 7th is a public event read | Org-scoped → org resolved from an event |
| Sponsors | 6/7, same shape | Org-scoped → via event |
| Venues | **7/7** pass `IsAdmin`; authorization via `IEventAuthority.ResolveOrgAsync` (D-269 — the private `CanManage(isAdmin, role)` copy is gone) | Org-scoped → via event |

### Phase 2B — blocked on backend capability
| Module | Why blocked | Required backend work |
|---|---|---|
| **Identity Verification** | `IIdentityVerificationService` has 4 methods, all self-service (`GetStatusAsync(userId)` + 3 submits). **No list, no queue, no review/approve/reject.** Endpoints are `/v1/me/identity` only. | Pending queue + a decision action |
| **Chat Moderation** | mute/ban/unban/pin/delete are gated on `IsHostAsync(roomId, userId)` — room Host membership. **`isAdmin`/`kurx_admin` appears 0 times in `ChatService`**, so a platform admin is refused. No admin room/message listing exists. | Thread `bool isAdmin` through the moderation methods (the `CertificateService` pattern) + an admin room read |
| **Review Moderation** | `EventReviewEndpoints` has 3 routes: create (own), list, delete **mine**. No admin delete/hide, 0 `IsAdmin` passes. | Admin review list + delete/hide action |

**Already covered, needs no new work:** reported chat messages *and* reported reviews both land in the
platform moderation queue (`ReportService` whitelist = `event, review, chat_message, user, org`), so they
already appear in the shipped `/reports` screen. What is missing there is content *removal* — resolving a
report marks the report, never the underlying message or review.

---

## 6. Nav / IA map (source of truth: `components/layout/nav-config.ts`)

```
Dashboard                    [LIVE]
Trust & Safety
  Verification Queue         [LIVE]   ← Tier-A
  Event Approval             [LIVE]   ← D-057
  Reports                    [LIVE]   ← D-059
  Blacklist                  [LIVE]   ← Tier-A
  Risk Flags                 [LIVE]   ← Tier-A
Finance
  Payments                   soon
  Payout Approvals           soon
  Refunds                    soon
People & Orgs
  Users                      [LIVE]   ← D-060 (Tier-C); + /users/[id] detail (Phase 1)
  Organizations              [LIVE]   ← D-194 (searchable list + detail workspace)
Events
  All Events                 [LIVE]   ← D-061 (Tier-C)
  Certificates               [LIVE]   ← Phase 1 (event-scoped, SuperAdmin)
Platform
  Event Taxonomy             [LIVE]   ← D-188 (full lifecycle, metadata, capabilities, import/export)
Catalog
  Templates                  soon     (BLOCKED — list returns Published only, §5.3)
System
  Staff & Roles              [LIVE]   ← D-056
  Audit Log                  [LIVE]   ← D-062
  Analytics                  [LIVE]   ← D-063
  Broadcast                  [LIVE]   ← Phase 1 (confirm-gated, all users)
  Health                     [LIVE]   ← Phase 1 (live dependency report)
  Background Jobs            soon
```

### Still pending (and why)
- **Finance / Ledger / Payouts** — blocked: real money doesn't move yet (Razorpay is `MockPaymentGateway`, no refund endpoint, payout execution is a no-op). Building a finance console over zero real transactions is premature; these unblock when the P0 payment backend lands.
- **Communication center** — blocked: SES / WhatsApp / FCM are console stubs (no real delivery).
- **Support center** — greenfield (no `support_tickets` entity); needs a product design + `D-NNN`.
- **Platform settings / feature flags** — greenfield (no settings/flags table); needs a product decision on what's configurable.
- ~~**Analytics**~~ — ✅ **BUILT (D-063)**, growth aggregates (no revenue until Finance). **Monitoring** — recommend external tooling (Grafana/Sentry) over rebuilding in-app; a thin `health/detail` (queue/job last-run) is the eventual in-app slice.
- ~~**Platform (Event Taxonomy)**~~ — ✅ **BUILT (D-188)**: replaces the old flat Categories screen. Full lifecycle (Active/Disabled/Archived, independent of Visible), rich metadata (description/icon/color/badge/search keywords), reorder, Type-level duplicate, Type-level capability defaults (reuses the existing V3 Capability registry), full audit trail, merge-only import/export, real usage counts, and the backend's 409 refusals (`category_in_use` / `category_has_children`) surfaced to the operator.
- **Catalog (Templates)** — **BLOCKED, backend.** Not a UI decision and no longer a "deferred, low value" item: the only list method returns **Published versions only**, so every Draft the write actions create is invisible and unmanageable. Needs an admin inventory endpoint first — full detail in §5.3.
- ~~**Organization list / detail**~~ — ✅ **BUILT (D-194)**: admin-scoped `GET /v1/admin/orgs` (+ `/{orgId}`), searchable/paginated, composes the existing wallet/risk/events admin reads into a detail workspace. See §5.4.

---

## 7. Where this fits the roadmap

- Re-architecture module **M12 — Admin verification console** (`docs/DECISIONS.md` D-051) and
  **M13 — Fraud** (D-052) are the backend that these pages will surface.
- Broader context: `docs/roadmap/README.md`, and the "client-parity pass" called out as the current
  frontier in the re-architecture plan (web dashboard, **admin app**, mobile all need the new
  endpoints wired — this doc tracks the admin slice of that).
