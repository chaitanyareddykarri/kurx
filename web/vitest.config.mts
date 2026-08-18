import react from "@vitejs/plugin-react";
import { fileURLToPath } from "node:url";
import { defineConfig } from "vitest/config";

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
    testTimeout: 20_000
  },
  resolve: {
    alias: {
      "@": fileURLToPath(new URL("./", import.meta.url)),
      "@kurx/ui": fileURLToPath(new URL("../packages/ui/src/index.ts", import.meta.url))
    }
  }
});
