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
    include: ["test/**/*.test.{ts,tsx}"]
  },
  resolve: {
    alias: {
      "@": fileURLToPath(new URL("./", import.meta.url)),
      "@kurx/ui": fileURLToPath(new URL("../packages/ui/src/index.ts", import.meta.url))
    }
  }
});
