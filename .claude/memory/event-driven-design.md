# Event-Driven & Workflow Design

Kurx is not a message-bus system today — it's a request/response API with **state-machine-driven domain workflows** and a real-time push layer (SignalR). This file records how stateful flows are modeled so new ones stay consistent.

## State machines are explicit, in `Domain`

- The event lifecycle is `Draft → PendingReview → UnderReview → Approved → Published → Closed → Archived` (D-266 M4; `ChangesRequested`/`Rejected` are the other review outcomes), and `EventStatusWorkflow` is the **single source of truth for valid transitions** (D-018). Publish is decoupled from ticket types entirely.
- Rule: model a stateful entity's transitions as an explicit workflow type in `Domain`, not as scattered `if (status == …)` checks across services. New stateful flows (orders, payouts, tickets) get their own workflow type when they arrive (Phases 4–6).
- Invalid transitions throw a domain `conflict` (`error-handling.md`), never silently no-op.

## Real-time push (SignalR)

- Two hubs: `SalesHub` (org sales feed) and `ScanHub` (event check-in). Both `[Authorize]`, both take the JWT via `?access_token=` query string (browsers can't set WS headers), both re-verify membership on **every** group join — `SalesHub.JoinOrg` checks org membership, `ScanHub.JoinEvent` resolves the event's `OrgId` first (D-017).
- Rule: a hub group is a security boundary. Any new hub/group join re-checks authorization live against Postgres on join — a connected socket must never guess into another org's feed (`security-rules`).

## Asynchronous / background work

Hangfire is wired and **14 recurring jobs are live** (registered in `Program.cs`, implemented in
`Kurx.Infrastructure/Jobs/`) — seat-hold expiry, waitlist offers, ledger maturation, inventory and
registration reconciliation, leaderboard and search-index refresh, notification cleanup, event
reminders, signing-key maintenance, outbox dispatch, chat-room locking and attachment cleanup.

Job work is triggered from a committed state transition, idempotent (safe to retry — payment webhooks
especially), and observable (`observability.md`).

**`AddHangfireServer()` runs on every API replica**, so a job is cluster-wide, not per-instance.
Recurring jobs are enqueued once per tick by whichever server wins the scheduler lock, but a run whose
invisibility timeout lapses is **re-dispatched while the first is still executing**. Conventions
(D-243):

- Every *recurring* job carries `[DisableConcurrentExecution]` + `[AutomaticRetry]`. Retry count
  follows cadence: `Attempts = 0` for minutely→hourly jobs (the next tick is soon; replaying a failed
  sweep buys nothing), `Attempts = 3` for daily ones (a lost run costs 24 hours). Lock wait is 10s /
  60s respectively.
- **Do not put `[DisableConcurrentExecution]` on a per-message/per-entity job.** `ChatNotificationJob`
  is enqueued once per chat message; a class-level lock would funnel every notification on the
  platform through one worker. The attribute is right for sweeps, wrong for fan-out.
- The lock narrows the window; it does not close it. **Idempotency is the actual guarantee** — claim
  the state transition conditionally (`WHERE … AND state = @expected`, D-240) so a duplicate run is a
  no-op rather than a double-apply.
- Not every class named `*Job` is a Hangfire job: `PhoneE164BackfillJob` is resolved directly by an
  admin endpoint, so scheduling attributes on it would be inert.

## Idempotency & external events (future)

- Payment webhooks (Phase 4) are the first true inbound external events. They must be idempotent (dedupe on provider event id) and verified (signature) before mutating state. This is a security + correctness gate, not a convenience — record the approach as a `D-NNN` when built.

## Rule of thumb

Prefer an explicit state machine in `Domain` + a synchronous service call over an event bus, until a concrete requirement (fan-out, ret/decoupling, cross-service) forces otherwise. The current system's flows are all expressible as guarded transitions — keep them there.
