import { describe, expect, it, vi } from "vitest";

// `lib/api` calls React's `cache()` at module scope, and `cache` only exists under the react-server
// export condition — in jsdom it is undefined and the import throws, which vitest reports as a suite
// that collected 0 tests rather than as a failure. Same shim as test/api-contract-shape.test.ts.
// Hoisted by vitest, so it applies before the imports below.
vi.mock("react", async () => {
  const actual = await vi.importActual<typeof import("react")>("react");
  return { ...actual, cache: (fn: unknown) => fn };
});

import { formatJoinedMonth, formatDateTime } from "@/lib/formatters";
import { publicProfileSchema, meSchema } from "@/lib/api";

/**
 * Account creation time, at the two precisions the API serves it (D-312):
 * `joined_at` (month) on the public profile, `created_at` (exact) on `/v1/me`.
 */
describe("joined month, on the public profile", () => {
  it("renders an ISO year-month as a readable month and year", () => {
    expect(formatJoinedMonth("2026-08")).toBe("August 2026");
    expect(formatJoinedMonth("2026-01")).toBe("January 2026");
    expect(formatJoinedMonth("2026-12")).toBe("December 2026");
  });

  /**
   * The bug this exists for. `new Date("2026-01")` is midnight UTC on the 1st; formatting it in the
   * viewer's zone renders "December 2025" anywhere west of Greenwich — the same profile telling two
   * viewers different things, and in January getting the YEAR wrong too. Pinning the formatter to UTC
   * is what makes a year-month a label rather than an instant.
   */
  it("does not slip a month or a year in a western timezone", () => {
    expect(formatJoinedMonth("2026-01")).not.toContain("2025");
    expect(formatJoinedMonth("2026-01")).not.toContain("December");
    expect(formatJoinedMonth("2026-12")).not.toContain("2027");
  });

  it("renders nothing for a value that is not a year-month", () => {
    expect(formatJoinedMonth("not-a-date")).toBe("");
  });
});

describe("the API contract carries both precisions", () => {
  it("accepts a month-only joined_at on the public profile", () => {
    const parsed = publicProfileSchema.partial().parse({ joined_at: "2026-08" });
    expect(parsed.joined_at).toBe("2026-08");
  });

  /**
   * The public projection must never carry the exact instant — the schema tolerating one would let a
   * future backend leak it without a single test going red.
   */
  it("has no exact-timestamp field on the public profile", () => {
    expect(Object.keys(publicProfileSchema.shape)).not.toContain("created_at");
  });

  it("carries the exact created_at on /v1/me, and it survives parsing intact", () => {
    // A full parse, not a partial one: `meSchema` carries a transform (it is a ZodEffects), so the
    // only honest way to prove the field round-trips is to run a real payload through it.
    const iso = "2026-08-09T09:12:30Z";
    const parsed = meSchema.parse({
      id: "u1",
      phone: "+919000000001",
      name: "Asha",
      needs_onboarding: false,
      created_at: iso
    });
    expect(parsed.created_at).toBe(iso);
  });
});

describe("exact creation time in settings", () => {
  it("formats a UTC instant into a full local date and time", () => {
    // Rendered through the same helper the settings component uses; Intl applies the runtime's zone,
    // so no offset is ever computed by hand.
    const rendered = formatDateTime("2026-08-09T09:12:30Z");
    expect(rendered).toContain("2026");
    expect(rendered).toMatch(/August/);
    // Not the raw ISO string — a settings page showing "2026-08-09T09:12:30Z" is the failure mode.
    expect(rendered).not.toContain("T09:12:30Z");
  });
});
