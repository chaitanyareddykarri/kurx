import react from "@vitejs/plugin-react";
import { fileURLToPath } from "node:url";
import { defineConfig } from "vitest/config";

/**
 * Admin's first test harness (Phase 27).
 *
 * Until now `npm run typecheck && lint && build` plus manual browser checks were the *entire* guard
 * on 27 screens — the largest untested surface in the product, and the one where a mistake is an
 * operator suspending the wrong account rather than a misaligned card.
 *
 * Deliberately the same shape as `web/vitest.config.mts`, and deliberately nothing more: vitest and
 * React Testing Library only. D-109 excludes Playwright, Cypress and Storybook and that still holds —
 * this adds no new dependency class, only the runner web has had since Phase 0, so admin's screens
 * can be asserted on rather than only looked at.
 */
export default defineConfig({
  plugins: [react()],
  test: {
    environment: "jsdom",
    globals: true,
    setupFiles: ["./test/setup.ts"],
    include: ["test/**/*.test.{ts,tsx}"]
  },
  resolve: {
    alias: {
      "@": fileURLToPath(new URL("./", import.meta.url)),
      "@kurx/ui": fileURLToPath(new URL("../packages/ui/src/index.ts", import.meta.url))
    }
  }
});
