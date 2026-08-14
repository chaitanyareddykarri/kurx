import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { describe, expect, it } from "vitest";

import { PROBLEM_COPY, looksLikeCode, problemMessage } from "@kurx/ui";

/**
 * Phase 43 — an error message has to be words.
 *
 * `docs/api/README.md` defines `error` on every ProblemDetails as "a stable machine-readable code".
 * Both web and admin returned it straight out of `apiErrorMessage`, so the message shown to someone
 * who mistyped their OTP was the literal string `invalid_code`, and a moderator refused an action
 * was shown `forbidden`.
 *
 * `web/lib/api.ts` cannot be imported here — it pulls in React's server-only `cache()`, which is not
 * a function under vitest. The mapping is exercised directly and the wiring asserted at source
 * level, which is where the defect lived: `return data?.error ?? …` in each of the two files.
 */
function shown(status: number, data: { error?: string; detail?: string }): string {
  return problemMessage({ code: data.error, detail: data.detail, status });
}

describe("a backend error code never reaches the reader", () => {
  it("translates a known code", () => {
    expect(shown(401, { error: "invalid_code" })).toBe("That code isn't right. Check it and try again.");
  });

  it("falls back by status rather than to the code itself", () => {
    // An unrecognised code is still a machine token — showing it is the defect, not a lesser one.
    const msg = shown(409, { error: "some_new_backend_code" });
    expect(looksLikeCode(msg)).toBe(false);
    expect(msg).toBe("Something went wrong. Please try again.");
  });

  it("keeps a detail that is genuinely prose", () => {
    expect(shown(400, { detail: "Your event has already been published." }))
      .toBe("Your event has already been published.");
  });

  it("separates our fault from theirs", () => {
    expect(shown(500, {})).toBe("Something went wrong on our end. Please try again.");
  });

  it("treats an unreachable server as a connection problem, not a server fault", () => {
    expect(problemMessage({ status: undefined })).toBe("Can't reach Kurx. Check your connection and try again.");
  });

  it("routes both surfaces through the mapping", () => {
    for (const rel of ["lib/api.ts", "../admin/lib/api.ts"]) {
      const src = readFileSync(resolve(__dirname, "..", rel), "utf8");
      expect(src).toContain("problemMessage({ code: data?.error");
      expect(src).not.toMatch(/return data\?\.error \?\?/);
    }
  });

  it("shows no entry that is itself a code", () => {
    expect(Object.values(PROBLEM_COPY).filter(looksLikeCode)).toEqual([]);
  });

  it("ends every sentence", () => {
    // Copy that trails off reads as truncated output rather than as a message.
    expect(Object.entries(PROBLEM_COPY).filter(([, v]) => !/[.!?]$/.test(v))).toEqual([]);
  });
});

describe("a refused password says why", () => {
  /**
   * The registration step renders whatever `apiErrorMessage` returns, which resolves through
   * `problemMessage`. Before these entries existed every policy refusal arrived as "Something went
   * wrong. Please try again." — `looksLikeCode` suppressed the `detail`, and the status was a plain
   * 400. A user told to use 12 characters picks one of the 12-character strings the breach list
   * exists to catch, and was given no way to know that.
   */
  const REFUSALS = [
    "password_too_short",
    "password_too_long",
    "password_breached",
    "password_contains_identifier",
    "password_reused",
    "password_already_set",
    "password_not_set",
    "password_invalid"
  ];

  it.each(REFUSALS)("%s resolves to prose, not a shrug", (code) => {
    const msg = problemMessage({ code, detail: code, status: 400 });
    expect(msg).not.toBe("Something went wrong. Please try again.");
    expect(looksLikeCode(msg)).toBe(false);
  });

  it("names the breach list in words a person can act on", () => {
    expect(problemMessage({ code: "password_breached", detail: "password_breached", status: 400 }))
      .toBe("That password is too common. Choose something less guessable.");
  });

  it("covers every code PasswordService can return", () => {
    // Read from the backend rather than restated here, so a new rejection reason fails this test
    // instead of silently reaching a user as a shrug.
    const cs = readFileSync(
      resolve(__dirname, "..", "..", "backend/Kurx.Infrastructure/Auth/PasswordService.cs"),
      "utf8"
    );
    // Only what is RETURNED to a caller: a `RejectionCode` switch arm, or a failed ServiceResult.
    // A bare string match also picks up `auth.password_changed` and the `password_reset` revoke
    // reason, which are audit vocabulary and never reach a client.
    const emitted = new Set([
      ...[...cs.matchAll(/=>\s*"(password_[a-z_]+)"/g)].map((m) => m[1]),
      ...[...cs.matchAll(/false,\s*"(password_[a-z_]+)"/g)].map((m) => m[1])
    ]);
    expect(emitted.size).toBeGreaterThan(4);
    expect([...emitted].filter((c) => !PROBLEM_COPY[c])).toEqual([]);
  });
});

describe("the two error vocabularies stay one vocabulary", () => {
  /**
   * Mobile keeps its own map, because Dart cannot import a TypeScript module. Same reasoning as the
   * palette in Phase 41: two copies of one vocabulary with nothing comparing them is how they drift.
   */
  it("says the same thing for every code both surfaces know", () => {
    const dart = readFileSync(
      resolve(__dirname, "..", "..", "mobile/lib/core/network/api_error.dart"),
      "utf8"
    );
    const flutter = new Map<string, string>();
    const block = dart.slice(dart.indexOf("_messages = {"));
    for (const m of block.matchAll(/'([a-z_]+)':\s*(?:'([^']*)'|"([^"]*)")/g)) {
      flutter.set(m[1], m[2] ?? m[3]);
    }
    expect(flutter.size).toBeGreaterThan(10);

    const drift: string[] = [];
    for (const [code, web] of Object.entries(PROBLEM_COPY)) {
      const mobile = flutter.get(code);
      if (mobile && mobile !== web) drift.push(`${code}: web "${web}" vs mobile "${mobile}"`);
    }
    expect(drift).toEqual([]);
  });
});
