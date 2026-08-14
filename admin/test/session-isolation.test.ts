import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { describe, expect, it } from "vitest";

import { ACCESS_COOKIE, REFRESH_COOKIE } from "@/lib/auth-cookies";

/**
 * One identity, two doors. The same phone number can legitimately be both a user and a platform
 * admin — that is the model — but signing in on web must open the user app only, and signing in here
 * must open the console only.
 *
 * That failed for a reason invisible in either app's code: **cookies are scoped by host, not by
 * port.** Web on `localhost:3000` and the console on `localhost:3001` share one jar, so while both
 * used `kurx_access`/`kurx_refresh`, a web sign-in silently authenticated the console. On split
 * production domains host-only cookies already kept them apart — which is exactly why this needs a
 * test rather than a deployment convention.
 */
const WEB_COOKIES = ["kurx_access", "kurx_refresh"];

describe("the console's session is its own", () => {
  it("does not reuse web's cookie names", () => {
    expect(WEB_COOKIES).not.toContain(ACCESS_COOKIE);
    expect(WEB_COOKIES).not.toContain(REFRESH_COOKIE);
  });

  it("names them distinctly from each other too", () => {
    expect(ACCESS_COOKIE).not.toBe(REFRESH_COOKIE);
  });

  it("is what web still writes — the other half of the pair", () => {
    // If web ever renames to match, the isolation is gone and nothing else would notice.
    const webSession = readFileSync(
      resolve(__dirname, "..", "..", "web/lib/session.ts"),
      "utf8"
    );
    expect(webSession).toContain('"kurx_access"');
    expect(webSession).toContain('"kurx_refresh"');
    expect(webSession).not.toContain(ACCESS_COOKIE);
    expect(webSession).not.toContain(REFRESH_COOKIE);
  });

  it("defines the names once, so middleware and session cannot drift", () => {
    // middleware.ts rotates the cookie; session.ts reads it. Two literals is one rename away from a
    // console that writes a session it cannot read.
    for (const rel of ["lib/session.ts", "middleware.ts"]) {
      const src = readFileSync(resolve(__dirname, "..", rel), "utf8");
      expect(src).toContain('from "@/lib/auth-cookies"');
      expect(src).not.toMatch(/const ACCESS_COOKIE\s*=\s*"/);
    }
  });

  it("refuses a signed-in account with no platform role instead of leaving it half-authenticated", () => {
    // completeSession is the single mint point: save → check isStaff → clear on failure.
    const actions = readFileSync(resolve(__dirname, "..", "lib/auth-actions.ts"), "utf8");
    const fn = actions.slice(actions.indexOf("async function completeSession"));
    const body = fn.slice(0, fn.indexOf("\n}"));
    expect(body).toContain("isStaff");
    expect(body).toContain("clearSession");
  });
});
