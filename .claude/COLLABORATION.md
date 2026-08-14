# Collaboration & Scale

How the Loop Engineering OS works across **multiple developers, multiple AI tools (Claude Code / Codex / GitHub Copilot), parallel git worktrees, Docker, and a codebase heading toward thousands of files**. Referenced from `CLAUDE.md` §12.

## Multiple AI tools, one contract

Every tool reads the same source of truth so their output is consistent:

- **`.claude/CLAUDE.md`** is the operating contract. Claude Code loads it automatically. For **Codex** and **Copilot**, point their instruction file at it (a repo-root `AGENTS.md` / `.github/copilot-instructions.md` should say: *"Follow `.claude/CLAUDE.md` and `.claude/index.md`; `docs/DECISIONS.md` is the spec."*). Don't fork the rules per tool — one contract, many readers.
- **`docs/DECISIONS.md`** is the shared spec regardless of which tool wrote the code. Any tool making an ambiguous call appends a `D-NNN`.
- **Reviews are tool-agnostic.** A human, Claude, or `/code-review` all run the same `.claude/reviews/` criteria. Code authored by one tool is reviewed against the same gates.

## Multiple developers

- **One decision log, append-only.** `D-NNN` numbering is the coordination point — two developers adding decisions coordinate on the next number (last-writer resolves the conflict by renumbering their own new entry, never an existing one).
- **Memory files are shared and singular.** A convention change is a change everyone inherits; it goes through review like code.
- **Phases are the work-allocation unit.** Different developers/agents can own different phases in parallel where dependencies allow (see the phase dependency graph in each `phaseN.md`).

## Parallel git worktrees

The repo already uses worktrees (`.claude/worktrees/ai-workspace`, `phase1-foundation`; `git worktree list`).

- **One worktree per parallel task/agent** so branches don't collide in a single working tree. Assign at the Task Breakdown stage.
- Each worktree still runs the full loop and its own verification against `kurx_test`. **Heads-up:** all worktrees on one machine share the **same** local Postgres cluster and `kurx_test` DB — don't run two integration suites simultaneously against it (cross-class parallelization is already disabled for the same reason). Serialize test runs, or give a worktree its own DB before parallelizing.
- Merge order follows phase dependencies; a worktree finishing an earlier-phase slice merges first.
- A worktree left unchanged is auto-cleaned; don't leave half-loops open in one.

## Docker & CI

- `docker compose up --build` is the **integration truth** — the full stack (postgres, redis, backend, web, admin) as it runs together. Use it for cross-service verification before a release, not just per-service dev ports (`.claude/memory/deployment.md`).
- CI (`.github/workflows/ci.yml`) is the shared regression net: backend build+test against a real Postgres container, web/admin typecheck/lint/build. Keep CI in parity with local dev; never weaken a check without sign-off (`.claude/agents/devops.md`).
- **Future CI/CD:** gates map cleanly onto pipeline stages — build+test (exists) → `.claude/reviews/` checks → security-review on sensitive paths → doc-freshness → release. Add them as jobs, not as replacements for the human/agent gates.

## Scaling to thousands of files

The system is designed to stay navigable as the codebase grows:

- **Load on demand, never speculatively.** `.claude/index.md` is the router; read only the file the current task needs. `CLAUDE.md` stays tight and points outward precisely so context cost doesn't grow with the repo.
- **Folder-per-domain** (`Infrastructure/Orgs`, `Infrastructure/Events`, …) keeps grep-before-build (non-negotiable #2) O(one folder), not O(repo). New domains get their own folder.
- **One source of truth per fact** means N files don't each carry a copy to keep in sync — the memory layer scales because it doesn't duplicate.
- **Pointers over copies.** Memory files point at `docs/`; the index points at everything. Depth lives in leaf files, loaded only when relevant.
- **Phases bound the working set.** At any time, active work is one or two phases — you never need the whole roadmap in context.

## Golden rules for any collaborator (human or AI)

1. Read `CLAUDE.md` + `index.md` first; `docs/DECISIONS.md` is the spec.
2. Grep before you build; finish scaffolding, don't duplicate (D-018).
3. One worktree per parallel task; serialize `kurx_test` runs.
4. Record every ambiguous call as a `D-NNN`.
5. Run the continuous-learning matrix before STOP.
6. Never self-commit/push/PR or touch secrets without the user's ask.
