# Workflow: Hotfix

An expedited `.claude/workflows/bug-fix.md` for an urgent, production-impacting (or release-blocking) defect. Compresses the lifecycle **without** dropping the safety gates. Owner: `.claude/agents/release-manager.md` coordinating the relevant domain agent.

## Objective

Stop the bleeding with the smallest correct change, verified, without introducing a second bug or a silent security regression.

## Preconditions

- The impact is real and urgent (breaking a live flow, blocking a release), not merely "annoying" — otherwise use `bug-fix.md`.
- The defect is reproducible (or made reproducible) before any fix — same rule as `bug-fix.md`.

## Step-by-step

1. **Reproduce** — write/run the failing case first. No fix on an unreproduced symptom.
2. **Root-cause** — fix the source of truth, not a symptom (the D-017 env-var fix pattern: fix the var name, don't stack another fallback).
3. **Smallest change** — no refactors, no bundled cleanups. File those separately.
4. **Inner loop** — build + the relevant test suite green (`loop-engineering.md`).
5. **Verify live** — re-exercise the exact broken flow against real deps (curl/browser/compose), not just the new test.
6. **Security fast-check** — if the fix touches auth/payment/PII/KYC, `security-review` is **still mandatory** — urgency never waives it.
7. **Regression test** — add the reproduction as a permanent test (`testing.md`) so it can't recur.

## What's compressed vs. skipped

- **Compressed:** Discovery/Planning/Architecture collapse to one sentence each; Performance review is a quick "did I add a hot-path call" check.
- **Never skipped:** reproduce-first, root-cause, live verification, security review for sensitive surfaces, a regression test, and the `.env`/secret/force-push stop rules.

## Verification

The reproduction now passes; full relevant suite green (no regression); the live symptom is gone when re-exercised; `security-review` clean if applicable.

## Exit criteria

Root cause fixed, regression test added, live flow confirmed working, no security/perf regression, change is minimal.

## Common mistakes

- Skipping the security review "because it's urgent" — the highest-risk moment to skip it.
- Papering a symptom to ship fast, then the real cause resurfaces.
- Bundling a refactor into the hotfix and enlarging the blast radius.
- Not adding the regression test — the same fire twice.

## Automation opportunities

- CI regression net catches recurrence once the reproduction test lands.
- A post-hotfix note in `docs/DECISIONS.md` **only** if the fix revealed an undocumented architectural assumption (`bug-fix.md` rule).
