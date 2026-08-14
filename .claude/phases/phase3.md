# Phase 3: Ticketing & Registration

**Status: in progress** — the ticket-type / registration-form slice shipped (commit 6b5a2e1, D-020); the registration/capture flow, organizer UI, and the end-to-end registration test are still open.

## Progress

**Done (shipped, verified with 11 integration tests — see D-020):**
- `TicketType` CRUD scoped to an event (Owner/Manager/`KurxAdmin`): pricing (`price_paise`), pricing unit (per-ticket/per-group), registration mode (individual/group + min–max), quantity, sale window, per-user limit, all-access.
- Inventory guards on the `Sold` counter: no delete when `sold > 0`; no quantity below `sold`.
- `FormField` custom registration forms nested under a ticket type (key/label/type/scope/required/options/sort; snake_case unique key; PerRegistration vs PerParticipant scope).
- Public listing (`GET /v1/events/{eventId}/ticket-types`) scoped to `Published` events + active sale window; org-facing list returns all.

**Still open against the original deliverables:**
- **Registration flow implementation** — `OrderService` implementing `IOrderService` (form answer capture, `Sold` increment, `Group`/`Ticket` issuance). Stub Order endpoints scaffolded and mapped; implementation pending D-021 resolution.
- **Organizer UI** (web `admin/` or `web/` host dashboard) for configuring ticket types + forms.
- **End-to-end "test registration captured" completion criterion** — blocked on the `OrderService` implementation above.
- Required reviews for the open slices: api-review (registration endpoints), backend-review, frontend-review + accessibility-review (config UI). (database-review already passed for the shipped `TicketType`/`FormField` schema.)

## Objective

Let organizers define how attendees register for an event — ticket types and custom registration forms — decoupled from payment collection (Phase 4).

## Deliverables

`TicketType` model (pricing tiers, quantity limits, sale windows) · `FormField`-based custom registration forms · registration flow endpoints · organizer UI for configuring ticket types/forms.

## Dependencies

Phase 2 (Event content/status model) must exist; ticket types attach to a published or draft event.

## Completion criteria

Organizer can define ticket types + a registration form for an event, and a test registration can be captured end-to-end (no payment yet — see Phase 4).

## Verification

`.claude/checklists/backend.md`, `.claude/checklists/database.md`, `.claude/checklists/frontend.md`. New `D-NNN` entries for any ambiguous pricing/inventory rule.

## Loop OS integration

- **Acceptance criteria (testable):** an organizer defines ≥1 ticket type + a registration form on an event; a test registration is captured and read back; inventory/quantity limits enforce correctly at the boundary.
- **Exit criteria:** deliverables shipped + verified live, test count grows, required reviews below pass, roadmap + this file updated.
- **Required reviews:** database-review (new `TicketType`/`FormField` schema), backend-review, api-review (registration endpoints), frontend-review + accessibility-review (config UI).
- **Required documentation:** `D-NNN` for each pricing/inventory/sale-window rule; `docs/api/README.md` for registration endpoints; `docs/roadmap/README.md`.
