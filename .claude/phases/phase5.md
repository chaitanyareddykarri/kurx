# Phase 5: Groups, Tickets & Check-in

**Status: not started.**

## Objective

Issue QR-coded tickets from paid orders and support at-venue scanning/check-in.

## Deliverables

Group/ticket issuance from completed orders · QR generation and validation · check-in scanning flow (likely via `ScanHub` SignalR, already wired up in Phase 1) · check-in state and duplicate-scan handling.

## Dependencies

Phase 4 (orders must be paid before a ticket is issued).

## Completion criteria

A paid order yields a scannable ticket, and a scan flips check-in state exactly once (duplicate scans rejected), verified live via `ScanHub`.

## Verification

`.claude/checklists/backend.md`, `.claude/checklists/security.md` (ticket forgery/replay concerns).

## Loop OS integration

- **Acceptance criteria (testable):** a paid order yields a scannable QR ticket; a scan flips check-in state exactly once and a duplicate scan is rejected, verified live via `ScanHub`; a forged/tampered QR is rejected.
- **Exit criteria:** deliverables shipped + live-verified through the hub, forgery/replay defended, required reviews pass, roadmap + this file updated.
- **Required reviews:** **security-review (forgery/replay/QR signing)**, backend-review, api-review (scan endpoints/hub), database-review (ticket/check-in schema).
- **Required documentation:** `D-NNN` for QR signing + duplicate-scan handling; `docs/api/README.md`; `docs/architecture/overview.md` (ScanHub flow); `docs/roadmap/README.md`.
