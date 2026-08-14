# Phase 9: Admin App

**Status: not started** (`admin/` is an empty Next.js scaffold).

## Objective

Build the platform-admin application: cross-org visibility and moderation tools gated by the `KurxAdmin` claim.

## Deliverables

Admin auth flow reusing existing JWT/`KurxAdmin` policy · org/event moderation views · taxonomy (category/template) management UI (currently admin-managed via API only) · platform-wide reporting.

## Dependencies

Phases 2–3 minimum (needs event/org data to administer); more useful after Phase 4+ for payment/ledger oversight.

## Completion criteria

An admin user can authenticate and perform at least one moderation action (e.g. category management) through the `admin/` UI against the real API.

## Verification

`.claude/checklists/frontend.md`, `.claude/checklists/security.md` (admin privilege boundary).

## Loop OS integration

- **Acceptance criteria (testable):** an admin authenticates and performs ≥1 moderation action (e.g. category management) through the `admin/` UI against the real API; a non-admin (`kurx_admin` absent) is denied every admin surface, verified live.
- **Exit criteria:** deliverables shipped + browser-verified against real admin auth, privilege boundary proven for non-admins, required reviews pass, roadmap + this file updated.
- **Required reviews:** **security-review (admin privilege boundary)**, frontend-review + accessibility-review, api-review (any new admin endpoints).
- **Required documentation:** `D-NNN` for admin auth/session decisions; `docs/api/README.md` (admin endpoints); `docs/architecture/overview.md` (admin app); `docs/roadmap/README.md`.
