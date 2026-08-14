# Kurx — Platform Flow Map

The end-to-end journey across **app, web and admin as one system**: how a person arrives, how an
event comes into being, how it gets sold, run and settled, and which surface owns each step.

**Scope boundary — this file does not list pages.** Per-surface page inventories are owned by
[`docs/ui-ux/inventory-web.md`](../ui-ux/inventory-web.md) (88 routes),
[`inventory-admin.md`](../ui-ux/inventory-admin.md) (27 routes) and
[`inventory-mobile.md`](../ui-ux/inventory-mobile.md). Structural grouping and reachability are owned
by [`information-architecture.md`](../ui-ux/information-architecture.md). This file owns only what
none of those do: the **flow between surfaces**, and where a journey cannot complete.

Measured 2026-08-07 on `feat/messages-settings`. Method: every literal `/v1/…` call site in `web/`,
`admin/` and `mobile/` diffed against the 414 unique paths in [`docs/api/openapi.json`](../api/openapi.json).

---

## 1. Surface ownership

| Journey stage | App | Web | Admin |
|---|:--:|:--:|:--:|
| Register / sign in | ✅ | ✅ | ✅ |
| New-device approval | ✅ | — | — |
| Step-up challenge | ✅ | detect only | — |
| Discover an event | ✅ | ✅ | — |
| Book / pay | ✅ | ✅ | — |
| Create an event | ✅ 7 steps | ✅ 11 steps | — |
| Manage an event | ✅ 16 tabs | ✅ 17 tabs | oversight only |
| Review / approve an event | — | reviewer copies | ✅ owner |
| Check in at the gate | ✅ scanner | ✅ | — |
| Moderate content | — | — | ✅ |
| Verify an organization | — | reviewer copies | ✅ owner |
| Platform taxonomy | — | — | ✅ |
| Finance / payouts | wallet only | wallet only | ❌ not built |

Web carries four reviewer screens (`/host/admin/*`) that duplicate admin-console functions. D-195
points web's nav at the console instead; the copies remain addressable.

---

## 2. Authentication

```
                    ┌──────────────────────────────────────┐
                    │  POST /v1/auth/otp/request           │  rate limit: 20/min/IP
                    │  → OtpChannelPolicy → SMS | WhatsApp │
                    └──────────────────┬───────────────────┘
                                       ▼
                    ┌──────────────────────────────────────┐
                    │  POST /v1/auth/otp/verify            │
                    │  → RiskEngine (device, velocity)     │
                    └──────────────────┬───────────────────┘
                    ┌──────────────────┼──────────────────┐
                    ▼                  ▼                  ▼
              step-up required    new user          signed in
                    │                  │                  │
                    ▼                  ▼                  ▼
             /step-up start      onboarding          app shell
             + verify            (name, username)
             ⚠ app only
```

**Token handling differs by surface and the difference is load-bearing.** Web and admin hold
`kurx_access` + `kurx_refresh` as httpOnly cookies and refresh inside `middleware.ts` — the only
place in the App Router where cookies are mutable. Mobile holds both in `flutter_secure_storage` and
refreshes from a Dio 401 interceptor.

**Refresh rotation** is single-use and claimed atomically by a conditional `UPDATE … WHERE
RevokedAt IS NULL`. Presenting an already-rotated token revokes the entire session family and writes
an `auth.token_reuse_detected` audit row. Losing the CAS race is deliberately **not** treated as
reuse — two parallel refreshes from one client are a race, and revoking the family would sign the
user out everywhere (D-240).

**New-device approval** is app-only. The waiting device subscribes to `/hubs/login`, which is
anonymous by design: no session exists yet, so the poll token authorises the subscription inside the
hub (AM9). An already-signed-in device calls pending → approve/reject.

---

## 3. Event creation → publication

```
ORGANISER                        PLATFORM (admin)                ATTENDEE

Create Event
  app  7 steps
  web  11 steps
      │
      ▼
   DRAFT
      │
Add Ticket Types
Add Inventory Pool
      │
      ▼
Submit for Review ──────────►  PENDING REVIEW
                                    │
                               Reviewer claims
                               ⚠ organiser edits now 409
                                    │
                    ┌───────────────┼───────────────┐
                    ▼               ▼               ▼
            Request Changes      Approve         Reject
                    │               │               │
            CHANGES REQUESTED       │           REJECTED
                    │               │               │
                    └── resubmit ───┴─── resubmit ──┘
                                    │
                                 APPROVED
                                    │
                    ┌───────────────┴───────────────┐
                 schedule                    publish_approved
                    ▼                               ▼
               SCHEDULED ──open_registration──► PUBLISHED ──────► visible in Discover
                                                    │
                                                 go_live
                                                    │
                                        ⚠ GATE: requires an ACCEPTED
                                          EventAssignment. No client on
                                          any surface calls
                                          /v1/assignments/{id}/accept.
                                                    │
                                                  LIVE ─────────► Check-in (QR scan)
                                                    │
                                                complete
                                                    ▼
                                               COMPLETED ───────► Results · Certificate · Review
                                                    │
                                                 archive
                                                    ▼
                                                ARCHIVED (terminal)
```

Publish gates enforced server-side before each forward transition:

| Transition | Requirement | Failure code |
|---|---|---|
| `open_registration` | ≥1 TicketType **and** ≥1 InventoryPool | `no_pass` / `no_inventory_pool` |
| `go_live` | an accepted `EventAssignment` | `no_staff_assigned` |
| `complete` | published `StageResults` if any Stage exists | — |

`Cancelled` is terminal — there is deliberately no path back to Published; clone instead (D-101).

### Creation payload parity

Both wizards deliberately cover only the fields needed for a publishable Draft; the workspace
finishes the rest. Web collects six fields mobile does not: `description`, `venueAddress`,
`capacity`, `schedule.resultDate`, `schedule.certificateReleaseAt`, `legal.cancellationPolicy`.

Web's **Pricing** step (Free/Paid) gates the stepper on `can_organize_paid` but its value is never
included in the submitted payload — product class is settled later by ticket types.

---

## 4. Registration → attendance → settlement

```
Discover ──► Event Detail ──► Checkout ──► Pay ──► Ticket ──► Gate ──► Certificate
                                 │
                                 ├── ticket type · quantity · custom form fields
                                 ├── SeatHold / InventoryPool reserve
                                 │     (ExpireSeatHoldsJob sweeps minutely)
                                 ├── RegistrationPolicy check
                                 ├── AudienceRule evaluation
                                 └── ⚠ no coupon field on any surface
                                 │
                                 ▼
                    POST /v1/events/{eventId}/orders
                                 │
                                 ▼
                          IPaymentGateway
                    ⚠ MockPaymentGateway is the only
                      implementation in the codebase
                                 │
                    POST /v1/webhooks/razorpay   (server-to-server)
                                 │
                    OrderService.ConfirmPaymentAsync
                    ├── Ticket issued, code HMAC-signed
                    │   with a dedicated TICKET_HMAC_SECRET
                    ├── Registration row created
                    ├── LedgerEntry(collected)
                    └── OutboxMessage → dispatched minutely
                                 │
                                 ▼
                    CollectedToAvailableLedgerJob (daily)
                                 ▼
                    OrganizationWallet.AvailablePaise
                                 ▼
                    InitiateWithdrawalAsync → Withdrawal
```

Alternate entry paths and their client coverage:

| Path | App | Web |
|---|:--:|:--:|
| Waitlist | ✅ | ✅ |
| Group booking | ✅ | partial |
| Ticket transfer / claim | ✅ | ✅ |
| Refund request | ✅ | ✅ |
| Guest checkout (no account) | ❌ | ❌ |
| Invitation RSVP (public link) | ❌ | ❌ |
| Delegated registration | ❌ | ❌ |

---

## 5. Where journeys cannot complete

> **Six breaks in the Home/Discover flow were closed on 2026-08-08 ([D-297](../DECISIONS.md))** and are
> deliberately not listed below: web's Trending/Upcoming/Latest rails (written, called by nothing),
> Featured being signed-out-only, the missing `/categories/:id` route, post search (absent at every
> layer), and Home offering one of the three searches it named. The table below is the remaining set.

| # | Break | Consequence |
|---|---|---|
| 1 | `/v1/assignments/{id}/accept` has no caller on any surface | `go_live` requires an accepted assignment, so an event may be unable to reach **Live** through the UI at all. Highest-impact item here — verify by hand. |
| 2 | Web calls `step-up/status` but neither `/start` nor `/verify` | Web detects that step-up is required and cannot satisfy it. Any web flow behind it dead-ends. |
| 3 | Web has no `login/pending` · `approve` · `reject` | A browser-only user cannot approve a new-device sign-in. |
| 4 | Coupons complete server-side, no UI anywhere | `/v1/events/{id}/coupons` and `/coupons/quote` are unreachable. |
| 5 | `MockPaymentGateway` is the only `IPaymentGateway` | The entire money spine is unexercised against a real PSP. |
| 6 | `MockKycProvider` is the only `IKycProvider` | Identity and organization verification are mocked end to end. |
| 7 | Admin Finance section unbuilt | `GET /v1/admin/refunds` exists and nothing calls it. |
| 8 | `/workspace` → Templates card → `/host/templates` | 404 — only the three child pages exist. Tracked as **S2** in `information-architecture.md` §1.1. |

---

## 6. API coverage

| | |
|---|---:|
| Spec paths (unique, normalised) | **414** |
| Called by web | 189 |
| Called by mobile | 205 |
| Called by admin | 112 |
| Union of all clients | **323** |
| **No client caller** | **96** → 83 after webhooks, JWKS, `<img>` sources and regex artifacts |
| **Client → non-existent path** | **0** |

**Drift runs one way: the backend is consistently ahead of the interface, never behind it.**

Subsystems complete server-side with no screen on any surface: coupons, referrals, seat blocks
(6 endpoints), templates (6), scoring policies, walk-ins, approval chains, result correction and
dispute, team split/substitute/transition, guest orders, public invitation RSVP, and the registries
(`/v1/capabilities`, `/v1/participant-roles`, `/v1/tags`).

Correctly uncalled and not gaps: `/v1/webhooks/razorpay`, `/v1/webhooks/whatsapp` (server-to-server),
`/.well-known/jwks.json` (token validators), `/v1/storage/{key}` (LocalDisk presign receiver,
self-disabling), `/v1/tickets/{code}/qr.png` (mobile renders it as an `<img>` source).

The CI contract gate proves **backend ↔ committed spec**. Nothing proves **spec ↔ client**: web,
admin and mobile each hand-maintain their own request/response models, so the bug class that
motivated the gate — a client schema disagreeing with the wire format — remains reachable.

---

## 7. Not verified

The measurements above are static. Nothing in this document was executed: no test was run, no page
rendered, no flow clicked. Endpoint coverage matched **literal** path strings, so an endpoint whose
URL is assembled from variables could appear as uncalled when it is not — confirm individually before
acting on §6. Three flagged as suspect for exactly this reason: leaderboards, organization KYC and
invitation RSVP each have a mobile route whose matching endpoint reads as uncalled.
