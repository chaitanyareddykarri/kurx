# Phase 1: Foundation Hardening

**Status: complete** (D-017). Kept here for historical completion-criteria reference — verify against `docs/roadmap/README.md` before assuming still accurate.

## Objective

Harden the base platform (auth, errors, validation, health, logging, CI) before building further business features on top of it.

## Deliverables

Fail-closed production secret validation · RFC7807 global exception handling with correlation IDs · FluentValidation on every request DTO · safe JWT claim parsing + `KurxAdmin` policy · real session enforcement in `web/` · EF migrations auto-apply with startup abort on failure · real health checks · structured logging enrichment · SignalR hubs authenticated and membership-checked · CI pipeline.

## Dependencies

None (foundation phase).

## Completion criteria

All items above shipped, test count grew (19→35), CI green, full docker-compose smoke test passed.

## Verification

`.claude/checklists/release.md` + `.claude/checklists/security.md`.

## Loop OS integration (historical)

- **Acceptance criteria (met):** every foundation item exercised live; a full docker-compose smoke test (OTP login through a protected route) passed; `/health` reflected real probes.
- **Exit criteria (met):** all deliverables shipped, test count 19→35, CI green, roadmap + this file updated.
- **Required reviews (run):** security-review (secret/auth surface), release checklist.
- **Required documentation (done):** D-017 in `docs/DECISIONS.md`; `docs/architecture/overview.md` (auth/error/health patterns); `docs/security/`; `docs/roadmap/README.md`.
