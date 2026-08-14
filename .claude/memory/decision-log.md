# Decision Log

This is a pointer, not a copy. **`docs/DECISIONS.md` is the single authoritative decision log** (`D-001`…`D-NNN`) — it is the spec for this project since no separate spec document exists. Do not duplicate its entries here; that would create a second source of truth that drifts.

## Rule

Any ambiguous architecture or product call gets a new entry there, using `.claude/templates/decision.md`, before code is built on top of the assumption.

## Known load-bearing decisions (names only — read `docs/DECISIONS.md` for rationale)

- D-005: OTP/auth rate limits
- D-011: ImageSharp pinned to 3.1.x
- D-015: Org role matrix (Owner/Manager/Staff/Finance)
- D-016: Bank/PAN KYC + Route linked-account; full numbers never persisted — last-4 only
- D-017: Phase 1 foundation hardening scope
- D-018: Event Management — complete existing unfinished scaffolding in place rather than build parallel system
- D-020: Phase 3 ticket-type pricing/inventory/group + form-field rules (shipped)
- D-021: Order/Group/Payment scaffolding — finish in place (not supersede) when registration ships; **PROPOSED**
- D-022: Username audit trail — `username_change_log` separate from `username_history`
- D-023: `design_templates.EventId` for event-scoped templates
- D-024: Registration forms 4-table system decoupled from ticket-type form fields
- D-025: Soft deletes via `deleted_at` on events/orgs/venues/speakers/sponsors/ticket_types
- D-026: Location columns on events (`Country`/`State`/`District`/`PostalCode`) for discovery + GST
- D-027: Org invitations — GitHub-style flow, time-limited token, explicit accept/decline
- D-028: `organization_wallet` — cached balance; never SUM ledger on hot path; atomic update with ledger insert
- D-029: Hangfire wired — 3 recurring jobs (seat-hold expiry, waitlist expiry, ledger settlement)
- D-030: Org soft-deletion policy
- D-031: Wallet withdrawal concurrency — SELECT FOR UPDATE + pending deduction
- D-032: Removal of the Flutter mobile mock data layer
- D-033: Event taxonomy seeding + slug uniqueness strategy
- D-034: Rate limiter ran before authentication — every request bucketed as anonymous; reordered `UseAuthentication()` → `UseRateLimiter()` → `UseAuthorization()`
- D-035: Real certificate rendering (Phase 7) — QuestPDF-only, system templates, manual trigger
- D-036: Two-product registration model — guest checkout for Events, mandatory accounts for Competitions
- D-037: Mandatory username onboarding + phone number change
- D-038: Case-insensitive email uniqueness + phone-change revokes all other sessions
- D-039: Trust & verification substrate — polymorphic `verification_documents` / `verification_reviews` (re-architecture M0)
- D-040: Platform roles read live per request — `platform_roles` + `PlatformRoleClaimsTransformation` (M2)
- D-041: Identity & account authority boundary — dropped `IsKurxAdmin`; profile is authority-zero (M1)
- D-042: Person identity verification — `user_identity_verifications`, masked last-4 only (M3)
- D-043: Organization registry — typed, canonical, alias/domain-aware, fuzzy dedup (M4)
- D-044: Organization verification lifecycle — Unverified→PendingReview→Verified/Rejected/Suspended/Blacklisted (M5)
- D-045: Membership-affiliation verification — `membership_claims` (M6)
- D-046: Trust capability matrix — L0–L5 labels over live capability flags (M7)
- D-047: Event approval + live payment gate — paid events can't self-publish (M8)
- D-048: KYC term split — `org_bank_verifications` (org bank) vs person identity; dead `KycEndpoints` deleted (M9)
- D-049: Paid checkout + ledger write-path — capture webhook → ticket + Collected ledger + wallet (M10)
- D-050: Forms convergence — one `FormField` system; dropped `registration_forms/*`; supersedes D-024 (M11)
- D-051: Admin verification console (backend) — merge/blacklist/history, VerificationReviewer-gated (M12)
- D-052: Fraud prevention — `blacklist_entries` + `fraud_signals`, live fraud-clear gate (M13)

If any of the above looks wrong against the actual file, `docs/DECISIONS.md` wins — update this list, don't trust it blindly.
