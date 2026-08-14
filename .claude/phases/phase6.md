# Phase 6: Ledger & Payouts

**Status: partial** — Hangfire wired with PostgreSQL storage (D-029); 3 recurring jobs active (`ExpireSeatHoldsJob`, `ExpireWaitlistOffersJob`, `CollectedToAvailableLedgerJob`); `WalletService` + ledger read endpoints live; `organization_wallet` cache wired (D-028). Full payout automation (ledger write on payment capture, Transfer row creation, advance/reserve entries, Razorpay Route execution) not yet implemented — blocked on Phase 4 (real payment gateway).

## Objective

Reconcile order revenue into an organizer ledger and automate payouts per the org's tier schedule (T1/T2/T3, D-007/D-016).

## Deliverables

Ledger entries per order/refund · Hangfire-scheduled payout jobs respecting each org's advance percentage/schedule · Razorpay Route payout execution · reconciliation reporting.

## Dependencies

Phase 4 (real payment gateway and order state must exist).

## Completion criteria

A completed order generates a correct ledger entry, and a scheduled job executes a payout matching the org's tier schedule, verified against a sandbox payout.

## Verification

`.claude/checklists/backend.md`, `.claude/checklists/security.md` (financial data handling).

## Loop OS integration

- **Acceptance criteria (testable):** a completed order produces a correct ledger entry (paise-exact, D-004); a Hangfire-scheduled job executes a sandbox payout matching the org's tier schedule (T1/T2/T3, D-007/D-016); a refund reconciles correctly; the job is idempotent on retry.
- **Exit criteria:** deliverables shipped + sandbox-verified, ledger balances reconcile, jobs idempotent, required reviews pass, roadmap + this file updated.
- **Required reviews:** **security-review (money movement)**, performance-review (batch/scheduled jobs, no hot-path blocking), backend-review, database-review (ledger schema + indexes).
- **Required documentation:** `D-NNN` for ledger model + payout scheduling + idempotency; `docs/architecture/overview.md` (background-job trigger contract, `event-driven-design.md`); `docs/roadmap/README.md`.
