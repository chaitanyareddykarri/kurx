# Future Improvements

Where the Loop Engineering OS and the Kurx codebase should grow next. Not a backlog of features (that's `docs/roadmap/README.md`) — this is about the *engineering system itself* and the gaps it currently tolerates. Promote an item to a `D-NNN` / phase when it's picked up.

## Verification & testing gaps (highest value)

- **End-to-end browser tests.** *(Superseded in part — `web` and `admin` now have vitest suites, 625 and 40 tests. What is still missing is a real browser path.)* A Playwright smoke test for OTP-login → create event → publish would close the remaining verification gap; today that path is only walked by hand or by an ad-hoc HTTP script.
- ~~**Contract tests.**~~ *Shipped:* `scripts/contract-check.mjs` diffs 36 client models against 440 OpenAPI schemas and runs in CI. Its own test (`contract-check.test.mjs`) still runs nowhere — CI runs the tool, never the tool's tests.
- **Per-worktree test DBs.** All worktrees share one `kurx_test`, forcing serialized integration runs (`COLLABORATION.md`). Per-worktree ephemeral DBs would unlock true parallel verification.

## Automation the OS is ready for

- **CI gates from `.claude/reviews/`.** The review criteria are already measurable; wire the mechanizable ones (pagination on list endpoints, `ProblemDetails` on error paths, migration-in-place check, doc-freshness) as CI jobs.
- **Doc-freshness gate.** Fail CI if a shipped `/v1` endpoint has no `docs/api/README.md` entry, or a `D-NNN` reference is dangling (`continuous-learning.md`).
- **Decision-number linter.** Prevent duplicate/gapped `D-NNN` numbers on merge (`COLLABORATION.md` coordination point).
- **Auto-attach the right review.** Route a diff to the correct `.claude/reviews/*` based on touched paths (auth/payment/PII → security-review mandatory).

## Observability maturity (arrives with real providers)

- **Metrics backend** (Prometheus/OTLP) — counters for auth/order/payout outcomes, latency histograms. Design at the phase that needs it, not speculatively (`observability.md`).
- **Distributed tracing** (OpenTelemetry) beyond the current `X-Correlation-Id`.
- **Real-provider health probes** — email/SMS/push probes land with Phase 8; the health-check design already anticipates them.

## Codebase-scale ergonomics

- **AGENTS.md / copilot-instructions.md** at repo root pointing Codex/Copilot at `.claude/CLAUDE.md`, so the one-contract-many-tools model in `COLLABORATION.md` is enforced by each tool's own loader.
- **A `verify` project skill** scripting the common live-exercise flows (OTP login, org create, event publish) so verification is one command, not a manual recipe.
- **Phase dependency graph** as a single diagram (currently prose in each `phaseN.md`) to make parallel-work allocation obvious.

## Production readiness (Phase 10 territory)

- Semantic release tagging (`versioning.md` notes this is deferred).
- A production deploy target + secret store (nothing wired yet — `deployment.md`).
- Load/security hardening pass across the full stack once real payment/notification providers exist.

## How to use this file

When a loop surfaces a recurring pain or a "we should really automate this," add it here (the continuous-learning matrix routes anti-patterns to this file). When someone picks an item up, it graduates to a `D-NNN` and/or a phase, and the entry is removed.
