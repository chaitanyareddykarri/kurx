# Workflow: Continuous Learning (Memory & Doc Update Triggers)

Run at the **Documentation + Decision Logging** stages of every loop, before STOP. This is the decision procedure that keeps `.claude/memory/` and `docs/` from drifting away from the code. Owner: `.claude/agents/documentation-engineer.md`.

## The self-check (run every time)

After finishing work, answer each question. A "yes" fires the linked update. Don't skip a "yes" because it "feels minor" — a drifted convention file is worse than none.

| Did the change… | If yes, update | Also |
|---|---|---|
| **Change architecture / layering / a structural pattern?** | `docs/architecture/overview.md`; fix `.claude/memory/architecture.md` pointer | `D-NNN` if a call was made; `.claude/reviews/architecture-review.md` |
| **Change the database schema?** | add EF migration; `.claude/memory/database-conventions.md` only if a *convention* changed | never edit an applied migration |
| **Change the public API contract?** | `docs/api/README.md` | `.claude/memory/api-conventions.md` only if a *rule* changed; `versioning.md` if breaking |
| **Change a business rule / product behavior?** | `docs/DECISIONS.md` (`D-NNN`) | `docs/PROJECT_HANDBOOK.md` if a user-facing rule; roadmap if scope shifted |
| **Change a coding/backend/frontend/test convention?** | the **one** owning `.claude/memory/*.md` | don't copy the rule elsewhere |
| **Make an ambiguous architecture/product call?** | `docs/DECISIONS.md` (`D-NNN`, `.claude/templates/decision.md`) | `.claude/memory/decision-log.md` if load-bearing |
| **Close/advance a phase deliverable?** | `.claude/phases/phaseN.md` **and** `docs/roadmap/README.md` | phase status table in `.claude/phases/README.md` |
| **Change setup / run / deploy / env?** | `README.md` / `docs/deployment/` / `.env.example` | `.claude/memory/deployment.md` if a rule changed |
| **Add a new dependency or change a pin?** | `docs/DECISIONS.md` (`D-NNN`) | `.claude/memory/versioning.md` known-pins list |
| **Add/alter a health probe or observability surface?** | `docs/deployment/` | `.claude/memory/observability.md` |
| **Change error/logging behavior?** | `.claude/memory/error-handling.md` / `logging.md` if a *rule* changed | — |
| **Discover a recurring mistake / anti-pattern?** | the relevant memory file's "common mistakes" + `.claude/FUTURE-IMPROVEMENTS.md` | consider a `D-NNN` |

## Rules

- **`docs/DECISIONS.md` is authoritative and append-only.** Never renumber/delete; a superseding decision references the one it reverses (architect flags reversals to the user first).
- **One source of truth per fact.** If a rule already lives in a memory file, update *that* file — don't restate it in a second place (that's how drift starts). Memory files are pointers/summaries, not copies of `docs/`.
- **Docs follow verified behavior**, never lead it. Don't document intent as if shipped.
- **Update the auto-memory too** (`/Users/naralanaveen/.claude/.../memory/`) when a *non-obvious* project fact changes (what's built, environment gotchas) — that's the cross-session index, distinct from repo `.claude/memory/`.

## When nothing needs updating

Legitimate — a pure internal refactor of already-documented behavior may trigger nothing. State that explicitly in STOP ("no docs triggered: internal-only change") rather than silently skipping the check.

## Automation opportunities

- A CI doc-freshness gate: fail if a `/v1` endpoint exists with no `docs/api/README.md` entry, or a `D-NNN` reference points to a missing entry.
- `.claude/reviews/documentation-review.md` as the pre-merge verification of this matrix.
