# Phase 4: Orders & Payments

**Status: not started** (mock gateway abstraction exists, unimplemented).

## Objective

Turn a registration into a paid order using a real payment gateway, with webhook-driven state updates.

## Deliverables

Order model + lifecycle · `IPaymentGateway` real implementation (Razorpay per D-007/D-016 org payout context) replacing mock · webhook endpoint(s) with signature verification · idempotent payment state transitions.

## Dependencies

Phase 3 (ticket types/registration must exist to attach an order to).

## Completion criteria

A real (sandbox) payment can be initiated, completed via webhook, and reflected in order state, with retries/idempotency verified.

## Verification

`.claude/checklists/security.md` is mandatory here (payment surface) in addition to backend/database checklists.

## Loop OS integration

- **Acceptance criteria (testable):** a sandbox payment is initiated, completed via a signature-verified webhook, and reflected in order state; a replayed/duplicate webhook is idempotent (no double transition); a failed payment leaves a coherent state.
- **Exit criteria:** deliverables shipped + sandbox-verified, idempotency proven by test, required reviews pass, roadmap + this file updated. No real secret committed.
- **Required reviews:** **security-review (mandatory — payment surface)**, api-review (webhook contract + signature), database-review (order/lifecycle schema), performance-review (no blocking work on the pay path).
- **Required documentation:** `D-NNN` for gateway choice, webhook verification, and idempotency strategy; `docs/api/README.md`; `docs/security/secret-management.md` for the new provider credentials; `docs/roadmap/README.md`.
