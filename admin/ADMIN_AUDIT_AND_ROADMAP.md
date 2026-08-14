> **Event-first architecture ([D-074](../docs/DECISIONS.md)).** No organizer/owner accounts — every person is a User who may *represent* verified organizations. The admin's org-verification queue now also gates the creation of the org itself: approving an **Organization Verification Request** is what creates the `Organization` and links the requester as a **Verified Representative**. Capability specifics below remain valid; "org owner" references reflect the legacy D-055 model, pending migration.

# Kurx Admin Console — Product Audit & Production Roadmap

_Audit date: 2026-07-15 · Branch: `feat/production-rearchitecture` · Method: backend treated as source of truth (`Kurx.Api/Endpoints`, `Kurx.Domain/Entities`, `Kurx.Application/Abstractions`, `docs/api/README.md`, `docs/DECISIONS.md`)._

> Companion to `admin/STATUS.md` (what's built in the shell). This document is the **full-scope product audit**: every operational capability a platform admin needs, measured against what the backend actually exposes today.

> **Build progress (2026-07-15):** ✅ Module 1 — **Staff & Roles** (D-056, Tier B). ✅ **Tier-A harvest** — Verification queue (org + membership claims), Blacklist, Risk-signal recording, Organization tools (suspend/blacklist/merge). ✅ **Event Approval queue** (D-057, Tier B) — `GET /v1/admin/events/pending` + approve/reject via existing transition. ✅ **Dashboard summary** (D-058, Tier B) — `GET /v1/admin/dashboard/summary` live counts; landing page now real. ✅ **Reports & moderation** (D-059, Tier B) — `POST /v1/reports` + Moderation-gated triage (`GET /v1/admin/reports`, resolve/dismiss, audit-logged). Backend + `next build` green; backend tests CI-verification-required. **Tier-B done** (Audit viewer remains, low value — sparse data). ✅ **Tier-C started — User Management** (D-060): additive `User.SuspendedAt/BannedAt` + auth-enforced suspend/ban/unban (blocks login + refresh); `GET/POST /v1/admin/users*` (Moderation-gated). Backend + `next build` green; ⚠️ hand-written migration + auth change → **CI apply + security review owed**. ✅ **Global Event Management** (D-061, no schema — search all events, feature/unfeature, force publish/unpublish). ✅ **Audit-log viewer** (D-062, read-only over audit_log). **Remaining are genuinely blocked or greenfield:** Finance/Ledger/Payouts (blocked — no real money movement yet), Communication (blocked — provider stubs), Support (greenfield), Settings/Flags (greenfield), Monitoring/Analytics (prefer external tooling), Catalog (deferred — low value). See `admin/STATUS.md` §"Still pending".

---

## 0. Executive summary — the one thing to understand

There are **three tiers** of gap, and conflating them is the biggest planning risk:

| Tier | Meaning | Admin work | Backend work | Count |
|------|---------|-----------|--------------|-------|
| **A — API exists, UI missing** | Endpoint is live & tested; the admin app just doesn't call it | Build page + wire | none | ~7 modules |
| **B — Service/table exists, no admin API** | Domain entity + service are built, but there is no HTTP endpoint | Build page | **thin endpoint over existing service** | ~4 modules |
| **C — Neither UI nor real backend** | No endpoint and the backend logic is a stub / doesn't exist | Build page | **full backend build** | ~7 modules |

**The whole admin API surface today is 3 files** (`AdminOrgEndpoints`, `AdminFraudEndpoints`, admin group in `MembershipClaimEndpoints`) — 11 endpoints, all `VerificationReviewer`-gated. Everything the user's audit describes as "Finance Dashboard", "Global Ledger", "Payout Ops", "User Management", "Support Center", "Platform Settings", "Monitoring", "Analytics", "Communication Center" is **Tier C — the backend does not expose it yet.**

The single highest-leverage fact: **Staff Management (the user's #1 priority) is Tier B.** The `PlatformRoleAssignment` table and `IPlatformRoleService` already exist — there is simply no endpoint to grant/revoke. It's a small backend addition, not a new subsystem.

---

## 1. Complete feature gap report (18 modules)

Legend — **Tier**: A/B/C above. **API**: ✅ exists · 🟡 partial · ❌ none. **UI**: ❌ none (admin app has no operational pages yet).

### 1. Staff Management — **Tier B** · Priority **P0**
- **Backend today:** `PlatformRoleAssignment` entity (`UserId, Role, GrantedBy, GrantedAt, ExpiresAt`), `IPlatformRoleService` (read/write), live per-request role read (M2/D-040). Auth is phone-OTP (shared with users).
- **API:** ❌ **No endpoint grants/revokes roles or manages staff.** This is the biggest "so close" gap — the data layer is done.
- **Missing endpoints:** `GET /v1/admin/staff` (search/filter), `POST /v1/admin/staff/{userId}/roles`, `DELETE /v1/admin/staff/{userId}/roles/{role}`, `POST /v1/admin/staff/{userId}/{disable|enable}`, `POST /v1/admin/staff/{userId}/force-logout`, `GET /v1/admin/staff/{userId}/activity`.
- **Requested roles vs. reality:** backend enum has **5** (`SuperAdmin, VerificationReviewer, FinanceOps, Support, ReadOnlyAuditor`). The requested **Risk Team** and **Moderator** roles require a `PlatformRole` enum extension + migration (additive) — a `D-NNN` decision.
- **Requested-but-not-modelled:** "department" filter (no department concept), "reset staff login"/"lock/unlock" (no account-lock field on `User`), "delete staff" (prefer soft-disable via role revocation + a `disabled` flag). Each needs a small schema decision.
- **Force-logout:** feasible today — reuse the refresh-token reuse-revokes-all mechanism (D-009/D-014).

### 2. Event Approval Queue — **Tier B** · Priority **P0**
- **Backend today:** event approval **logic** exists (M8/D-047) — a paid event goes `Draft → submit_review → InReview`, and a reviewer publishes. `EventStatus` = `{Draft, InReview, Published, Closed, Archived}`. The org-scoped `POST /v1/orgs/{o}/events/{e}/transition` performs the publish.
- **API:** ❌ **No admin queue.** There is no `GET /v1/admin/events/pending` and no admin-side review action; a reviewer today would have to know each event id.
- **Missing endpoints:** `GET /v1/admin/events/pending`, `GET /v1/admin/events/{id}` (full review payload: organizer + org + verification + venue + tickets + forms + media + pricing + payout account + prior review history), `POST /v1/admin/events/{id}/review {approve|reject|request-changes, notes}`.
- **Note:** "internal notes" + "previous review history" ride on the M0 `verification_reviews` substrate (subject=`Event`) — the audit spine already exists.

### 3. Global Event Management — **Tier C** · Priority **P1**
- **Backend today:** events are org-scoped only. No platform-wide event admin.
- **API:** ❌ none. **Missing model:** no `Suspended`/`Hidden`/`Cancelled` event status, no `Featured`/`Pinned` flags, no admin edit/transfer-ownership path.
- **Missing endpoints:** `GET /v1/admin/events` (search/filter all), `POST /v1/admin/events/{id}/{suspend|hide|archive|restore|force-publish|force-unpublish|feature|pin}`, `POST /v1/admin/events/{id}/transfer-owner`, `PATCH /v1/admin/events/{id}` (admin edit), `POST /v1/admin/events/{id}/category`.
- **Schema work:** `events.IsFeatured`, `events.PinnedAt`, `events.SuspendedAt/Reason` (additive columns); event **cancellation** with refund is P0 in the backend report (E-1) but the *admin surface* for it is P1.

### 4. Finance Dashboard — **Tier C** · Priority **P1**
- **Backend today:** wallet + ledger exist but are **org-scoped** (`/v1/orgs/{id}/wallet`, `/wallet/ledger`). There is **no platform-global aggregation**, no revenue rollups, no per-category/city/college breakdowns.
- **API:** ❌ none. Every tile the audit lists (Platform Revenue, Gateway Fees, Pending/Completed Payouts, Refunds, Chargebacks, Taxes, Settlement Status, Revenue by *) requires new admin aggregation endpoints reading `ledger_entries`/`payments`/`refunds`.
- **Blocked by:** real money doesn't move yet (Razorpay is `MockPaymentGateway`, P-1/P0), and **there is no refund endpoint at all** (P-4/P0) and **payout execution is a no-op** (P-5/P0). Finance numbers will be simulated until those land.
- **Missing endpoints:** `GET /v1/admin/finance/summary`, `GET /v1/admin/finance/revenue?groupBy=day|month|event|organizer|category|city`, etc.

### 5. Ledger — **Tier C (partial reuse)** · Priority **P1**
- **Backend today:** `ledger_entries` table + per-org ledger view exist. The full trace (participant → gateway → platform fee → organizer share → wallet → payout → bank) is **modelled in the ledger rows** but not exposed as an admin cross-org view.
- **API:** ❌ no global/admin ledger. **Missing:** `GET /v1/admin/ledger?scope=global|org|event|payment|refund|payout|wallet&...`, `GET /v1/admin/transactions/{id}/trace`.

### 6. Payout Operations — **Tier C** · Priority **P0 (backend) / P1 (admin UI)**
- **Backend today:** `withdrawals`, `payout_schedules` (T1/T2/T3), `IRouteClient`/`MockRouteClient`, `CollectedToAvailableLedgerJob`. **`WalletService.InitiateWithdrawalAsync` creates a row but makes no external call** — payouts are a no-op (P-5/P0).
- **API:** ❌ no admin payout ops. **Missing:** `GET /v1/admin/payouts?status=`, `POST /v1/admin/payouts/{id}/{approve|reject|hold|release|retry}`, `GET /v1/admin/payouts/{id}` (bank details, KYC, failures).
- **Blocked by:** P-5 (real Route execution) + KYC gate. Approve/hold/release is meaningless until execution is real.

### 7. User Management — **Tier C** · Priority **P1**
- **Backend today:** `User` entity, profile endpoints. **No admin user endpoints, no suspend/ban/merge/delete.**
- **API:** ❌ none. **Missing:** `GET /v1/admin/users` (search), `GET /v1/admin/users/{id}` (tickets/events/payments/reports/wallet/verification/login-history), `POST /v1/admin/users/{id}/{suspend|ban|unban|reset}`, `POST /v1/admin/users/merge`, `DELETE /v1/admin/users/{id}`.
- **Schema work:** no `User.SuspendedAt/BannedAt` today (additive); "login history" needs a store (auth events aren't persisted for display). User merge is non-trivial (FK repoint) — needs a `D-NNN`.

### 8. Organization Management — **Tier A (verification) + C (rest)** · Priority **P0 (verification) / P1 (rest)**
- **Backend today ✅:** `GET /v1/admin/orgs/pending`, `POST .../verification/{review|suspend|blacklist}`, `POST /v1/admin/orgs/merge` all **exist, tested, and already have a web client** (`web/lib/api.ts`). Change org type / verify / suspend / delete beyond these are not exposed.
- **API:** 🟡 approve/reject/merge/suspend/blacklist ✅; **transfer ownership, change type, remove members, delete org** ❌.
- **This is the most ready module** — port the existing web client into the admin app.

### 9. Trust & Safety — **Tier A (fraud/blacklist) + B (reports)** · Priority **P0**
- **Backend today ✅:** `/v1/admin/blacklist` (POST/GET/DELETE), `/v1/admin/fraud-signals` (POST), `/v1/admin/verifications/{type}/{id}/history`, `/v1/admin/verification-documents/{id}/view` — **all exist, tested, web client already written** (`web/components/host/blacklist-manager.tsx`, `org-admin-tools.tsx`).
- **Reports/moderation — Tier B:** `reports` table exists (polymorphic, Open/Resolved/Dismissed) + `audit_log`. **No service/endpoints** — specced in backend report as C-5/P1: `POST /v1/reports`, `GET /v1/admin/reports`, `POST /v1/admin/reports/{id}/{resolve|dismiss}`.
- **Risk score / watchlist / appeals — Tier B/C:** per-signal scores exist; consolidated risk rollup (T-2) and appeals queue (T-3) are specced, not built.

### 10. Catalog Management — **Tier A** · Priority **P1**
- **Backend today ✅:** category writes require `KurxAdmin`/platform authority (`POST /v1/categories`), 3-tier taxonomy seeded (D-023); templates (`/v1/templates`, system + custom) exist.
- **API:** 🟡 categories ✅, templates ✅ (read; custom-template write partial). **Missing:** subcategory/tag admin CRUD as first-class, certificate-template management, "default forms / default ticket types" (no global-default concept — needs design).
- **Mostly a UI over existing endpoints.**

### 11. Communication Center — **Tier C** · Priority **P2**
- **Backend today:** announcements exist but are **org/event-scoped** (`AnnouncementEndpoints`), fanned out via push/email/WhatsApp. Providers are **console stubs** (no real SES/WhatsApp/FCM — P-2/P-9/P-7 all P0).
- **API:** ❌ no platform-wide broadcast, no maintenance banner, no emergency alert, no targeting by role/city/category. **Missing:** `POST /v1/admin/broadcasts`, `GET /v1/admin/broadcasts`, plus a maintenance-banner setting (ties to Platform Settings).
- **Blocked by:** real notification providers.

### 12. Support Center — **Tier C (greenfield)** · Priority **P2**
- **Backend today:** **nothing** — no ticket/conversation/escalation entity exists.
- **API:** ❌ none. Full greenfield: `support_tickets`, `support_messages` tables + endpoints. Largest net-new backend build in the list.

### 13. Audit System — **Tier B** · Priority **P1**
- **Backend today:** `audit_log` table exists; the `verification_reviews` substrate is the audit spine for every trust decision and is **readable** via `/v1/admin/verifications/{type}/{id}/history`. General write-side audit (who/when/what/old/new/IP/reason) is **not uniformly recorded** for non-trust actions.
- **API:** 🟡 trust history ✅; **general admin audit-log query** ❌. **Missing:** `GET /v1/admin/audit?actor=&entity=&from=&to=`, and an audit-write interceptor on every new admin mutation.
- **Cross-cutting requirement:** every Tier-B/C endpoint above must write `audit_log`.

### 14. Dashboard — **Tier B/C** · Priority **P1**
- **Backend today:** the shell renders 6 tiles as `—` (no data). Some counts are cheaply derivable (pending orgs/claims via existing queues); most (revenue, refunds, failed payments, fraud alerts) need the Tier-C aggregations.
- **API:** ❌ **no `GET /v1/admin/dashboard/summary`** (called out in `STATUS.md` §5). **Missing:** one summary endpoint + a recent-activity feed off `audit_log`.

### 15. Platform Settings — **Tier C (greenfield)** · Priority **P1**
- **Backend today:** fees/tiers are code/seed constants (D-007); **no settings store, no feature-flag table.**
- **API:** ❌ none. Greenfield: `platform_settings` (fees, taxes, commission, currencies, countries, limits), `feature_flags`, maintenance-mode toggle. `GET/PATCH /v1/admin/settings`, `GET/PATCH /v1/admin/feature-flags`.
- **Payment/notification/API-key settings** overlap with the provider secrets — those stay in the secret store, **not** an admin-editable table (security).

### 16. Monitoring — **Tier C** · Priority **P2**
- **Backend today:** only `GET /health`. No queue/job/Redis/DB/storage/provider health surface, no error-log API, no perf metrics.
- **API:** 🟡 `/health` ✅; everything else ❌. Prefer **external tooling** (Grafana/Sentry/uptime) over building this in-app — a monitoring UI in the admin console is largely reinventing observability platforms. Recommend: link out, expose only `GET /v1/admin/health/detail` (queues + job last-run) in-app.

### 17. Analytics — **Tier C** · Priority **P1/P2**
- **Backend today:** `Event.ViewCount` only; org-scoped analytics (O-3) is **also not implemented**. No platform-wide analytics.
- **API:** ❌ none. **Missing:** `GET /v1/admin/analytics/{growth|users|organizers|events|revenue|retention|top-organizers|top-events}`. Heavy aggregation; build after finance/ledger (shares the same read models).

### 18. Current architecture review — **done (this document)**
- APIs that exist: §3 below. Missing APIs: §3. Immediately buildable pages: Tier A modules. Backend-blocked: Tier B/C.

---

## 2. Missing page list (admin routes to create)

All under `admin/app/(console)/`. **✅ = backend ready now (Tier A)** · 🟡 = needs thin endpoint (Tier B) · 🔴 = needs backend build (Tier C).

| # | Route | Module | Ready |
|---|-------|--------|-------|
| 1 | `verification/` | Verification queue (org + membership + identity) | ✅ |
| 2 | `verification/[type]/[id]/` | Review detail + cross-subject history + doc viewer | ✅ |
| 3 | `blacklist/` | Blacklist manager | ✅ |
| 4 | `risk/` | Fraud signals list/create | ✅ |
| 5 | `organizations/` | Org search + verify/suspend/blacklist/merge | ✅ |
| 6 | `categories/` | Category/taxonomy admin | ✅ |
| 7 | `templates/` | Template admin | ✅ |
| 8 | `staff/` + `staff/[id]/` | **Staff & role management** | 🟡 |
| 9 | `events/pending/` | Event approval queue | 🟡 |
| 10 | `reports/` | Moderation / reported content | 🟡 |
| 11 | `audit/` | Audit log viewer | 🟡 |
| 12 | `` (dashboard rewrite) | Real operational dashboard | 🟡 |
| 13 | `events/` | Global event management | 🔴 |
| 14 | `users/` + `users/[id]/` | User management | 🔴 |
| 15 | `finance/` | Finance dashboard | 🔴 |
| 16 | `ledger/` | Global ledger + trace | 🔴 |
| 17 | `payouts/` | Payout operations | 🔴 |
| 18 | `communications/` | Broadcast center | 🔴 |
| 19 | `support/` | Support ticket queue | 🔴 |
| 20 | `settings/` | Platform settings + feature flags | 🔴 |
| 21 | `monitoring/` | System health (mostly link-out) | 🔴 |
| 22 | `analytics/` | Platform analytics | 🔴 |

---

## 3. API inventory — exists vs. missing

### Admin endpoints that EXIST (11 total, all `VerificationReviewer`)
```
GET    /v1/admin/orgs/pending
POST   /v1/admin/orgs/{id}/verification/review
POST   /v1/admin/orgs/{id}/verification/suspend
POST   /v1/admin/orgs/{id}/verification/blacklist
POST   /v1/admin/orgs/merge
GET    /v1/admin/verifications/{subjectType}/{subjectId}/history
GET    /v1/admin/verification-documents/{documentId}/view
GET    /v1/admin/membership-claims/pending
POST   /v1/admin/membership-claims/{id}/review
GET/POST/DELETE  /v1/admin/blacklist[/{id}]
POST   /v1/admin/fraud-signals
```
> A **web client for all of these already exists** in `web/lib/api.ts` + `web/app/(app)/host/admin/*` + `web/components/host/{blacklist-manager,org-admin-tools}.tsx` — port it to `admin/`, don't rewrite.

### Admin endpoints that are MISSING (grouped by tier)

**Tier B — thin endpoint over an existing service/table:**
```
GET  /v1/admin/staff                         POST /v1/admin/staff/{id}/roles      DELETE .../roles/{role}
POST /v1/admin/staff/{id}/{disable|enable|force-logout}    GET /v1/admin/staff/{id}/activity
GET  /v1/admin/events/pending                GET /v1/admin/events/{id}            POST /v1/admin/events/{id}/review
GET  /v1/admin/reports                        POST /v1/admin/reports/{id}/{resolve|dismiss}    POST /v1/reports
GET  /v1/admin/audit                          GET  /v1/admin/dashboard/summary
```
**Tier C — needs backend build (schema and/or real provider):**
```
GET  /v1/admin/users …                        POST /v1/admin/users/{id}/{suspend|ban|unban}    POST /v1/admin/users/merge
GET  /v1/admin/events …                        POST /v1/admin/events/{id}/{suspend|feature|pin|transfer-owner}
GET  /v1/admin/finance/*                        GET  /v1/admin/ledger …           GET /v1/admin/transactions/{id}/trace
GET  /v1/admin/payouts …                        POST /v1/admin/payouts/{id}/{approve|hold|release|retry}
POST /v1/admin/broadcasts                       GET/PATCH /v1/admin/settings       GET/PATCH /v1/admin/feature-flags
GET  /v1/admin/analytics/*                       support_tickets/* (greenfield)
```

---

## 4. Recommended navigation (IA)

Extends the existing `admin/components/layout/nav-config.ts`. Marked `[A]`/`[B]`/`[C]` by tier.

```
Overview
  Dashboard                     [B]
Trust & Safety
  Verification Queue            [A]
  Event Approval                [B]
  Reports & Moderation          [B]
  Blacklist                     [A]
  Risk / Fraud Signals          [A]
  Appeals                       [C]   (later)
Finance
  Finance Dashboard             [C]
  Ledger                        [C]
  Payouts                       [C]
  Refunds & Chargebacks         [C]
People & Orgs
  Users                         [C]
  Organizations                 [A]
Events
  All Events                    [C]
Catalog
  Categories                    [A]
  Templates                     [A]
Communication
  Broadcasts                    [C]
Support
  Ticket Queue                  [C]
System
  Staff & Roles                 [B]   ← promote to top for SuperAdmin
  Audit Log                     [B]
  Platform Settings             [C]
  Feature Flags                 [C]
  Monitoring                    [C]
  Analytics                     [C]
```

---

## 5 & 6. Recommended implementation order + priority

Ordered by **(value ÷ effort)** — start where the backend is already done.

| Phase | Modules | Tier | Priority | Rationale |
|-------|---------|------|----------|-----------|
| **1 — Harvest Tier A** | Verification queue, Organizations, Blacklist, Risk, Categories, Templates | A | **P0** | Endpoints live + web client to port. Fastest real value; makes the console operational for Trust & Safety. |
| **2 — Cheap Tier B** | **Staff & Roles**, Event Approval, Dashboard summary, Audit viewer | B | **P0** | Small backend endpoints over existing services/tables. Staff mgmt is the user's #1 ask and unblocks RBAC. |
| **3 — Moderation** | Reports & Moderation | B | **P1** | `reports` table exists; unblocks content safety. |
| **4 — Money (backend-first)** | Refunds (P-4), real Razorpay (P-1), payout execution (P-5) → then Finance dashboard, Ledger, Payout ops | C | **P0 backend / P1 UI** | Admin finance UI is meaningless until money actually moves. Backend money integrity gates this. |
| **5 — People & events ops** | User management, Global event management | C | **P1** | Needs additive schema (suspend/ban/feature flags) + merge decisions. |
| **6 — Platform config** | Platform Settings, Feature Flags, Analytics | C | **P1/P2** | Settings greenfield; analytics reuses finance read models. |
| **7 — Enhancements** | Communication center, Support center, Monitoring, Appeals | C | **P2/P3** | Support is greenfield; monitoring should mostly link to external tooling (don't rebuild Grafana/Sentry). |

**Priority key:** P0 = console can't do trust/safety ops without it · P1 = needed shortly after · P2 = enhancement · P3 = defer / evaluate build-vs-buy.

---

## 7. Database dependencies

| Need | Exists today | New schema required |
|------|--------------|---------------------|
| Staff/roles | `platform_role_assignments`, `IPlatformRoleService` | `PlatformRole` enum += `RiskTeam`,`Moderator`; optional `User.DisabledAt`, department concept |
| Event approval | `events.Status(InReview)`, `verification_reviews` | none |
| Global events | `events` | additive `IsFeatured`, `PinnedAt`, `SuspendedAt/Reason`; possibly `EventStatus.Cancelled` |
| Reports/moderation | `reports`, `audit_log` | none |
| Audit | `audit_log`, `verification_reviews` | none (needs write-side interceptor) |
| Users admin | `users` | additive `SuspendedAt`/`BannedAt`; login-event store; merge FK strategy (D-NNN) |
| Finance/Ledger | `ledger_entries`, `payments`, `refunds`, `organization_wallet`, `withdrawals`, `transfers`, `payout_schedules` | none for reads; **refunds need a service+endpoint** (entity exists) |
| Payouts | `withdrawals`, `IRouteClient` | none (needs real Route execution, not schema) |
| Settings/flags | — | greenfield `platform_settings`, `feature_flags` |
| Support | — | greenfield `support_tickets`, `support_messages` |
| Communications | `announcements`, `whatsapp_messages`, `notifications` | broadcast target model; maintenance-banner setting |
| Analytics | read models above | optional rollup tables only if slow |

**Principle (from the backend completion report):** every addition is **additive** (new nullable columns / new tables / new endpoints) — no existing contract breaks.

---

## 8. Security considerations

1. **RBAC is live-read, never from the token (D-040)** — every new admin endpoint must `RequireAuthorization(<role>)` with the correct least-privilege role, not a blanket `SuperAdmin`. Preserve this.
2. **PII exposure** — user management, attendee rosters, and verification docs return names/phones/emails/IDs. Mandatory **security review** per CLAUDE.md §7/§10 for each. Consider masked vs. full views by role; verification docs use **per-document presigned URLs** (already the pattern), never bulk key dumps.
3. **Financial endpoints** (payouts, refunds, ledger writes) → `FinanceOps`/`SuperAdmin` only; every mutation writes `audit_log` with actor + reason; approvals should be idempotent and double-entry consistent.
4. **Audit everything** — no admin mutation ships without a `who/when/what/old/new/IP/reason` audit row. This is a hard requirement (module 13), not optional.
5. **Hidden ≠ 403** — keep the 404-not-403 invariant (D-018) for resources an admin role can't see.
6. **Settings ≠ secrets** — API keys / provider secrets stay in the secret store with fail-closed validation; the settings table holds only non-secret config. Never make secrets admin-editable.
7. **Destructive actions** (delete user/org/event, force-logout, ban) → confirm-dialog + audit + ideally soft-delete/restore over hard delete.
8. **Force-logout / session revocation** reuses the reuse-revokes-all refresh mechanism (D-009/D-014) — don't invent a parallel path.
9. **Impersonation** (if ever added for support) is high-risk — not in scope here; would need its own decision + heavy audit.

---

## 9. RBAC matrix

Backend enum = `SuperAdmin, VerificationReviewer, FinanceOps, Support, ReadOnlyAuditor` (SuperAdmin implies all). Requested `RiskTeam`, `Moderator` require an enum extension (marked ⨁).

| Capability | SuperAdmin | VerificationReviewer | FinanceOps | Support | ReadOnlyAuditor | Moderator ⨁ | RiskTeam ⨁ |
|------------|:--:|:--:|:--:|:--:|:--:|:--:|:--:|
| Staff & role management | ✅ | — | — | — | — | — | — |
| Platform settings / flags | ✅ | — | — | — | — | — | — |
| Verification queue (org/membership/identity) | ✅ | ✅ | — | — | 👁 | — | — |
| Event approval | ✅ | ✅ | — | — | 👁 | — | — |
| Blacklist / fraud signals | ✅ | ✅ | — | — | 👁 | — | ✅ |
| Risk score / watchlist | ✅ | ✅ | — | — | 👁 | — | ✅ |
| Reports / moderation | ✅ | ✅ | — | ✅ | 👁 | ✅ | — |
| Finance dashboard / ledger | ✅ | — | ✅ | — | 👁 | — | — |
| Payout operations | ✅ | — | ✅ | — | 👁 | — | — |
| Refunds / chargebacks | ✅ | — | ✅ | — | 👁 | — | — |
| User management | ✅ | 🟡 view | — | ✅ | 👁 | 🟡 | 🟡 |
| Organization management | ✅ | ✅ | 🟡 finance-only fields | 🟡 view | 👁 | — | — |
| Global event management | ✅ | 🟡 | — | 🟡 view | 👁 | ✅ suspend/hide | — |
| Catalog (categories/templates) | ✅ | — | — | — | 👁 | — | — |
| Communication / broadcast | ✅ | — | — | 🟡 | — | — | — |
| Support tickets | ✅ | — | — | ✅ | — | — | — |
| Audit log | ✅ | — | — | — | ✅ | — | — |
| Analytics | ✅ | 🟡 | 🟡 finance | 🟡 | ✅ | — | — |

✅ full · 🟡 scoped/partial · 👁 read-only · — none. (`ReadOnlyAuditor` = read-only across the board.)

---

## 10. Final production-ready admin roadmap

**Milestone A — Operational Trust Console (P0, backend mostly done)**
Port the existing web admin client into `admin/`: Verification queue, Organizations, Blacklist, Risk, Categories, Templates. Add Tier-B endpoints: **Staff & Roles**, Event Approval queue, `dashboard/summary`, Audit viewer. → The console becomes usable by Trust & Safety + SuperAdmin.

**Milestone B — Safety & Moderation (P1)**
Reports/moderation endpoints + UI; risk-score aggregation surfaced to reviewers; appeals queue (P2).

**Milestone C — Money (P0 backend → P1 admin UI)**
Backend first: real Razorpay + webhook HMAC (P-1), refunds (P-4), real payout execution + reconciliation (P-5), chargebacks (P-6). Then admin UI: Finance dashboard, global Ledger + trace, Payout ops, Refunds. *No finance UI before money is real.*

**Milestone D — People & Event Ops (P1)**
User management (+ additive suspend/ban schema, merge decision), Global event management (+ feature/pin/suspend flags).

**Milestone E — Platform Control (P1/P2)**
Platform settings + feature flags (greenfield), platform analytics (reuses finance read models).

**Milestone F — Growth & Support (P2/P3)**
Communication/broadcast center (needs real providers), Support ticket system (greenfield), Monitoring (prefer external tooling + a thin health-detail endpoint).

**Cross-cutting, every milestone:** live-read RBAC on each endpoint · `audit_log` write on every mutation · security review for every PII/financial surface · each ambiguous product call recorded as a `D-NNN` before build (CLAUDE.md §3).

---

### Appendix — what the audit assumed vs. what the backend actually is

- The audit prompt treats Finance/Ledger/Payouts/Users/Support/Settings/Monitoring/Analytics as "just missing UI." **They are missing the backend**, and three of the money pieces (real gateway, refunds, payout execution) are P0 backend work the admin UI depends on.
- Conversely, **Staff Management, Event Approval, Reports, and Audit are much closer than they look** — the tables/services exist; they need a thin endpoint, not a subsystem.
- The genuinely-ready-now surface (Tier A) is **Trust & Safety + Organizations + Catalog**, and a **reference web client already exists** to port — that's where to start for immediate, low-risk value.
