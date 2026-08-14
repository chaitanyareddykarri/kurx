# Phase 8: Real Notification Providers

**Status: not started** (console/mock dev senders only today).

## Objective

Replace dev-only notification providers with real delivery: WhatsApp Cloud API (OTP is already built on this abstraction), AWS SES (email), FCM (push).

## Deliverables

Real `IWhatsAppSender`, `IEmailSender`, push-notification implementations behind existing `Kurx.Application.Abstractions` interfaces · provider config/secrets wired per `docs/security/secret-management.md` · health checks extended to probe real providers where feasible.

## Dependencies

None beyond Phase 1's provider-abstraction pattern; can proceed in parallel with Phases 3–7.

## Completion criteria

A real OTP/notification is delivered end-to-end in a staging environment (not just console/mock), with secrets validated fail-closed in production per D-017's pattern.

## Verification

`.claude/checklists/security.md` (new external credentials), `.claude/checklists/release.md`.

## Loop OS integration

- **Acceptance criteria (testable):** a real OTP/notification is delivered end-to-end in staging (WhatsApp Cloud / SES / FCM), not console/mock; production secret validation fails closed on a missing/weak provider credential (D-017 pattern); `/health` probes the new provider(s) where feasible.
- **Exit criteria:** deliverables shipped + staging-verified delivery, secrets validated fail-closed, health probes added, required reviews pass, roadmap + this file updated.
- **Required reviews:** **security-review (new external credentials + PII in payloads)**, release checklist, backend-review.
- **Required documentation:** `D-NNN` per provider choice; `docs/security/secret-management.md` (new secrets); `docs/deployment/` + `docs/architecture/overview.md` (`observability.md` health probes); `docs/roadmap/README.md`.
