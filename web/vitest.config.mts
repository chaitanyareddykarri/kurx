import react from "@vitejs/plugin-react";
import { cpus, freemem } from "node:os";
import { fileURLToPath } from "node:url";
import { defineConfig } from "vitest/config";

/*
 * How many test processes may run at once — bounded by FREE MEMORY as well as by cores.
 *
 * The default is core-derived alone, and on a loaded machine that oversubscribes badly: each fork
 * holds a jsdom instance, and when they collectively exhaust RAM the main thread cannot answer a
 * worker's module request within birpc's 60-second ceiling. The worker then dies with
 * `[vitest-pool]: Timeout calling "fetch"` — a whole FILE reported as failed, several per run,
 * moving between files each time, while every assertion that did execute passed. Observed
 * 2026-08-18 with 0.7 GB free of 15.7 GB; the same suite is green serially and green again once the
 * box is idle, which is what identifies it as starvation rather than a defect.
 *
 * birpc's timeout is not configurable from here — Vitest constructs it without one — so the only
 * lever is not to start more forks than the machine can feed. ~700 MB per fork is measured from a
 * jsdom worker's peak RSS in this suite.
 *
 * On CI this changes nothing: a runner with several GB free lands on the core-derived number, which
 * is exactly what it used before.
 */
const maxForks = Math.min(
  Math.max(2, cpus().length - 1),
  Math.max(2, Math.floor(freemem() / 700_000_000))
);

// Minimal test setup (D-109): Vitest + React Testing Library only. Deliberately no Jest, Cypress,
// Playwright or Storybook — this exists to cover logic that is impractical to verify by hand
// (capabilities, optimistic reconciliation, cursor paging, reconnect, deep links, a11y), not to
// replace browser verification.
export default defineConfig({
  plugins: [react()],
  test: {
    environment: "jsdom",
    globals: true,
    setupFiles: ["./test/setup.ts"],
    include: ["test/**/*.test.{ts,tsx}"],
    /*
     * Vitest's default is 5s, which the wizard tests outgrew: a step that renders a dozen controls and
     * asserts between each one is genuinely slow in jsdom, and `userEvent` types character by
     * character. Tests were failing on the CLOCK while passing in isolation, and the failures moved
     * between files on each run — a timeout that varies with machine load is a flake, not a signal.
     * Raised here rather than per-test so no future test inherits the same trap.
     */
    testTimeout: 20_000,
    // `minForks` must be set alongside it: Vitest defaults the minimum to the CORE count independently,
    // so capping only the maximum makes the two conflict and Tinypool refuses to start at all
    // ("options.minThreads and options.maxThreads must not conflict") — the whole run collects no tests.
    poolOptions: { forks: { minForks: 1, maxForks } }
  },
  resolve: {
    alias: {
      "@": fileURLToPath(new URL("./", import.meta.url)),
      "@kurx/ui": fileURLToPath(new URL("../packages/ui/src/index.ts", import.meta.url))
    }
  }
});
