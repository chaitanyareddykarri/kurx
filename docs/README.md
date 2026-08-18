# Kurx Documentation Index

Start here. Kurx has **Users**, **Events** and **Representations** — and no organization accounts,
organizer accounts, or personal organizations.

- **Users own events** (`events.created_by`) — **[D-268](DECISIONS.md)**. An organization never owns one.
- **User-first, event-first navigation** — **[D-267](DECISIONS.md)**. Workspace lists *your* events;
  Create Event is reachable directly; choosing who you represent is a step *inside* the form.
- **One event authorization service** (`IEventAuthority`) — **[D-269](DECISIONS.md)**. It decides *who may
  act*; the Capability Engine decides *what an event supports*; the two never consult each other.
- **Organizations are institutions a user may represent** — **[D-074](DECISIONS.md)** /
  **[D-075](DECISIONS.md)**. A not-yet-verified institution is a staged verification request an admin
  approves before any `Organization` exists.

Narrative: [`architecture/event-creation.md`](architecture/event-creation.md).

## Source of truth per topic

| Topic | Authoritative document |
|---|---|
| Decisions (the spec) | [`DECISIONS.md`](DECISIONS.md) — append-only from D-001; superseded entries carry ⚠️ markers. Read the highest number from the file (`grep -oE '^## D-[0-9]+' docs/DECISIONS.md \| tail -1`) rather than trusting a ceiling quoted here — this line said "D-269" long after the log had passed D-300. |
| **Event system design (frozen contract)** | [`architecture/EVENT_ARCHITECTURE_V3.md`](architecture/EVENT_ARCHITECTURE_V3.md) — adopted [D-131](DECISIONS.md); **not to be redesigned** |
| **Event system delivery plan** | [`architecture/V3_IMPLEMENTATION_ROADMAP.md`](architecture/V3_IMPLEMENTATION_ROADMAP.md) — 18 phases, authoritative sequencing |
| Event system — design philosophy / *why* (companion to V3) | [`architecture/EVENT_OS_PHILOSOPHY.md`](architecture/EVENT_OS_PHILOSOPHY.md) — non-normative rationale + the 20 Laws; **defers to V3**, never overrides it |
| Architecture (overview) | [`architecture/overview.md`](architecture/overview.md) |
| Architecture diagrams | [`architecture/diagrams.md`](architecture/diagrams.md) |
| **End-to-end flow across app · web · admin** | [`architecture/PLATFORM_FLOW_MAP.md`](architecture/PLATFORM_FLOW_MAP.md) — surface ownership, auth/creation/booking journeys, where a journey cannot complete, API↔client coverage. Page lists stay in `ui-ux/inventory-*.md` |
| **Canonical terminology (one vocabulary)** | [`architecture/TERMINOLOGY.md`](architecture/TERMINOLOGY.md) — legacy→current table, naming debt; **wins over any conflicting word elsewhere** |
| Event creation · ownership · representation · event authority | [`architecture/event-creation.md`](architecture/event-creation.md) |
| Event lifecycle | [`architecture/overview.md`](architecture/overview.md) + DECISIONS D-047 |
| Authentication | [`auth/AUTHENTICATION_ARCHITECTURE.md`](auth/AUTHENTICATION_ARCHITECTURE.md) — master doc, with `auth/AUTHENTICATION_{API,DATABASE,SECURITY,TESTING,UI,MIGRATION}.md` beside it. Status/phases: [`auth/AUTHENTICATION_ROADMAP.md`](auth/AUTHENTICATION_ROADMAP.md). [`architecture/overview.md`](architecture/overview.md) §Authentication is the one-paragraph summary. |
| Trust / verification / fraud | [`../.claude/memory/trust-verification.md`](../.claude/memory/trust-verification.md) |
| API reference & error model | [`api/README.md`](api/README.md) |
| Database (tables) | [`database/DATABASE_TABLES.md`](database/DATABASE_TABLES.md) |
| **Running the tests by hand** | [`TESTING.md`](TESTING.md) — the commands for all four surfaces, prerequisites, hygiene and troubleshooting. Rules and current baselines stay in [`../.claude/memory/testing-standards.md`](../.claude/memory/testing-standards.md) |
| Build status (built vs not) | [`roadmap/README.md`](roadmap/README.md) |
| Payments | [`architecture/overview.md`](architecture/overview.md) §Payments + DECISIONS D-049 |
| Certificates | DECISIONS D-035 |
| Notifications & external providers | [`EXTERNAL_SERVICES_AND_PROVIDERS.md`](EXTERNAL_SERVICES_AND_PROVIDERS.md) |
| Security posture | [`security/overview.md`](security/overview.md) |
| Secret management | [`security/secret-management.md`](security/secret-management.md) |
| Deployment | [`deployment/README.md`](deployment/README.md) |
| Product handbook (narrative) | [`PROJECT_HANDBOOK.md`](PROJECT_HANDBOOK.md) |
| Admin console | [`../admin/ADMIN_AUDIT_AND_ROADMAP.md`](../admin/ADMIN_AUDIT_AND_ROADMAP.md) + [`../admin/STATUS.md`](../admin/STATUS.md) |
| Web app | [`../web/README.md`](../web/README.md) + [`../.claude/memory/frontend-conventions.md`](../.claude/memory/frontend-conventions.md) |
| Mobile app | [`../mobile/README.md`](../mobile/README.md) + [`../.claude/memory/mobile-conventions.md`](../.claude/memory/mobile-conventions.md) |
| Backend conventions | [`../.claude/memory/backend-conventions.md`](../.claude/memory/backend-conventions.md) |
| Database conventions | [`../.claude/memory/database-conventions.md`](../.claude/memory/database-conventions.md) |

## Engineering process (`.claude/`)

The operating manual is [`../.claude/CLAUDE.md`](../.claude/CLAUDE.md); the navigation map is
[`../.claude/index.md`](../.claude/index.md). Conventions live under `../.claude/memory/`, workflows under
`../.claude/workflows/`, and per-surface checklists/reviews under `../.claude/{checklists,reviews}/`.

## Superseded material lives in git, not in this tree

Every file listed here is current. Nothing in `docs/` is a snapshot kept "for reference" — a banner
saying *do not use this* never stopped anyone from reading it, and the tree carried 926 KB of documents
that described code deleted by D-267/D-268/D-269 as if it still existed.

Removed 2026-08-08 (recoverable with `git log --diff-filter=D -- docs/`):

| What it was | Where the answer is now |
|---|---|
| `REMAINING_WORK.md`, `BACKEND_ARCHITECTURE_COMPLETION.md`, `PLATFORM_AUDIT_M8.md`, `VAULT_SYNC_AUDIT.md`, `audits/SCREEN_AUDIT.md`, `audits/FLOW_COVERAGE.md` — build-status snapshots, all predating D-267/D-268/D-269 | [`roadmap/README.md`](roadmap/README.md) |
| `history/` — the re-architecture master plan, module dossier, auth dossier, Event Architecture V2 + its review, `EVENT_SYSTEM_AS_BUILT.md`, `TRUST_MODEL_VS_BUILT.md`, the 2026-07 one-off audits and task prompts | [`DECISIONS.md`](DECISIONS.md) for the decisions, [`../CHANGELOG.md`](../CHANGELOG.md) for what shipped |
| `architecture/EVENT_CREATION_V2.md` — declared "Phase 1 not started" while D-266 M1–M7 were already in `AdminEventEndpoints.cs` and `EventReviewChecklist.cs` | [`architecture/event-creation.md`](architecture/event-creation.md) + D-265/D-266/D-305 |
| `architecture/PROFESSIONAL_IDENTITY_V2_PRODUCTION_AUDIT.md`, `PROFILE_COMPLETE_AUDIT.md`, `VERIFIED_IDENTITY_KICKOFF_PROMPT.md`, `VERIFIED_IDENTITY_INTEGRATION_NOTES.md` — discharged audits and a session kickoff prompt | [`architecture/PROFESSIONAL_IDENTITY_SPEC.md`](architecture/PROFESSIONAL_IDENTITY_SPEC.md) + D-201/D-221/D-229 |
| `SESSION_RECONCILIATION.md`, `auth/PHASE_1_SUMMARY.md`, `auth/PHASE_2_SUMMARY.md` — mid-flight auth records; `auth/AUTHENTICATION_PROGRESS.md` — a second status tracker that had drifted from the roadmap; `auth/AUTHENTICATION_IMPLEMENTATION_GUIDE.md` — an auth-scoped copy of the operating manual | [`auth/AUTHENTICATION_ROADMAP.md`](auth/AUTHENTICATION_ROADMAP.md) |

`DECISIONS.md` is append-only, so decision entries still cite these paths. That is provenance — the
decision was made against the document as it read on that date — not a live pointer.
