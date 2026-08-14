import { vi } from "vitest";
import "@testing-library/jest-dom/vitest";

/**
 * React's `cache()` only exists under the react-server export condition, so in jsdom it is
 * `undefined` — and `lib/api.ts` calls it at module scope. Anything that transitively imports it
 * (the shell does, through the topbar's user menu) therefore throws "cache is not a function", which
 * vitest reports as a suite collecting zero tests rather than as a failing assertion.
 *
 * Shimmed here as identity rather than mocked per file. `cache` is a request-scoped memoiser: in a
 * test there is one request and one call, so returning the function unchanged is faithful — it
 * removes the deduplication, not the behaviour.
 */
vi.mock("react", async () => {
  const actual = await vi.importActual<typeof import("react")>("react");
  return { ...actual, cache: <T,>(fn: T) => fn };
});

/**
 * `server-only` throws by design when a client bundle imports it — that guard is the whole point of
 * the package, and `lib/session.ts` uses it correctly. jsdom is neither bundle, so the guard fires on
 * a legitimate import chain (the shell's user menu pulls in the auth actions).
 *
 * Stubbed to an empty module rather than removed from the source: the production guard stays exactly
 * as it is, and only the test environment opts out of it.
 */
vi.mock("server-only", () => ({}));
