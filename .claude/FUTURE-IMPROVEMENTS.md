# Future Improvements

Where the Loop Engineering OS and the Kurx codebase should grow next. Not a backlog of features (that's `docs/roadmap/README.md`) — this is about the *engineering system itself* and the gaps it currently tolerates. Promote an item to a `D-NNN` / phase when it's picked up.

## Verification & testing gaps (highest value)

- **End-to-end browser tests.** *(Superseded in part — `web` and `admin` now have vitest suites, 625 and 40 tests. What is still missing is a real browser path.)* A Playwright smoke test for OTP-login → create event → publish would close the remaining verification gap; today that path is only walked by hand or by an ad-hoc HTTP script.
- ~~**Contract tests.**~~ *Shipped:* `scripts/contract-check.mjs` diffs 36 client models against 470 OpenAPI schemas and runs in CI. Its own test (`contract-check.test.mjs`) still runs nowhere — CI runs the tool, never the tool's tests.
- **A rule asserted against a primitive is not asserted against its call sites.** Found twice in one
  session on the sign-in and signup screens. `web/test/auth-otp.test.tsx` asserts *"has a real label,
  not a placeholder standing in for one"* and that an SMS code field declares
  `autocomplete="one-time-code"` — but it asserts both by rendering `Field`/`Input` **directly**, so
  the suite stayed green while the actual login fields carried placeholder-only labels and the actual
  signup code field had no autofill hint at all (D-384 §3). The same shape hid an unnamed phone field
  on the admin console's own login page for as long as it has existed.

  The gap is structural, not a missing test: a primitive-level assertion proves the primitive can be
  used correctly, never that it *is*. Two cheap closers, either of which would have caught all three:
  a source-level sweep asserting no `<input>` outside `@kurx/ui` carries `placeholder` without a
  paired `<label>`, and a render-level assertion per real screen rather than per primitive.

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
- **`ToAdminJson` should use named arguments.** It builds a 32-member positional record; a member added or removed anywhere but the end silently re-points every argument after it, and the payload still looks well-formed. It has bitten once already (`org_name` vanished from the wire). A guard test covers it today, which is why D-381 appended rather than rewrote — but if you are editing that mapper anyway, converting it removes the hazard outright.
- **Admin's workspace tab strip is a fixed 5+4 split, not width-measured** (D-381). A viewport wide enough for all nine still hides four behind `More`. A `ResizeObserver` would be smoother and buys a class of layout bugs; the sheet has one width, so it was not worth it. One array (`PRIMARY_TAB_IDS`) to revisit.
- **Admin's `Tabs` call sites still have no `aria-controls`/`TabPanel` wiring** (deferred at 18A.0, still open after D-381 restructured the strip). The nine panels are sibling conditionals in one sheet, so wiring them is its own change rather than a rename. Tracked in `UI_REDESIGN_PROGRESS.md`.

## Production readiness (Phase 10 territory)

- Semantic release tagging (`versioning.md` notes this is deferred).
- A production deploy target + secret store (nothing wired yet — `deployment.md`).
- Load/security hardening pass across the full stack once real payment/notification providers exist.

## How to use this file

When a loop surfaces a recurring pain or a "we should really automate this," add it here (the continuous-learning matrix routes anti-patterns to this file). When someone picks an item up, it graduates to a `D-NNN` and/or a phase, and the entry is removed.
