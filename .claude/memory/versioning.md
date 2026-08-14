# Versioning

How APIs, the database schema, dependencies, and decisions are versioned. Keeps compatibility explicit rather than accidental.

## API

- The HTTP API is versioned by URL prefix: **`/v1/…`** (`/v1/me`, `/v1/events`, …). SignalR hubs live under `/hubs/*`.
- A **breaking** change to an existing `/v1` contract (removing/renaming a field, changing a status code's meaning, tightening validation on a shipped field) requires either backwards-compat within `/v1` or a new version prefix — decided via `D-NNN`, never a silent break. Additive changes (new optional field, new endpoint) stay in `/v1`.
- Compatibility precedent: the RFC7807 migration kept the old `{error:"..."}` shape as an `error` extension specifically so existing consumers didn't break (D-017). Preserve that instinct — extend, don't break.
- `docs/api/README.md` is the contract of record; update it in the same change that alters a contract (`.claude/memory/api-conventions.md`).

## Database schema

- Versioned by **EF migrations**, applied in order, auto-applied at startup (`database-conventions`). The migration history *is* the schema version.
- Never edit an applied migration — always add a new one. Destructive changes (drop/rename) need user confirmation (database agent boundary) because they're hard to reverse on the shared dev DB.
- Additive-first: prefer adding a nullable column + backfill over an in-place breaking rename.

## Dependencies

- Load-bearing pins are recorded as decisions: EF/Npgsql **8.0.11** (D-013), ImageSharp **3.1.x** (D-011), .NET **8** SDK (`global.json`), Node 22 LTS. Don't bump a pinned dependency without checking the decision that set it and recording a new `D-NNN` if you do.
- `global.json` pins the SDK; a second system-wide 10.x SDK exists off-PATH and is irrelevant (`database-conventions`).

## Decisions

- `docs/DECISIONS.md` is an append-only, contiguously-numbered log (`D-001…D-NNN`). Superseding a prior decision is itself a new entry that references the one it reverses (architect flags reversals to the user first). Never renumber or delete an entry.

## Releases / phases

- Product progress is versioned by **phase** (`.claude/phases/`, `docs/roadmap/README.md`), not semver tags yet. A phase is "released" when its completion criteria are met and the roadmap reflects it (`.claude/workflows/release.md`). Semantic release tagging is a Phase 10 (production-readiness) concern.
