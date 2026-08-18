import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { describe, expect, it } from "vitest";

/**
 * Signing in with an account whose setup is unfinished lands on `/register`, because `/login`
 * redirects on `needs_onboarding`. That page then told the person to "Create your Kurx account"
 * while ticking "Phone verified" above it, and offered no sign-out — so pressing "Log in" appeared
 * to create a second account, and could never produce a login form for anyone in that state.
 *
 * Asserted at source level rather than by rendering: the page is an async Server Component that
 * calls `cookies()` and the API, neither of which exists under vitest. What regressed here was the
 * copy and the presence of an escape route, and both are visible in the source.
 *
 * The page moved to `app/(public)/register/` in D-384 so signup inherits the public shell — the URL
 * is unchanged, route groups being invisible to routing. These assertions moved with it, and the
 * heading check no longer pins the `h1`'s class string: it guarded which ARM the copy sits in, and
 * spelling out `text-2xl font-semibold` made restyling the page look like breaking it.
 */
const page = readFileSync(resolve(__dirname, "..", "app/(public)/register/page.tsx"), "utf8");
const login = readFileSync(resolve(__dirname, "..", "app/(public)/login/page.tsx"), "utf8");

describe("arriving at /register already signed in", () => {
  it("does not tell an existing account to create one", () => {
    // Both sentences exist; what matters is that the "Create" one is not unconditional.
    expect(page).toContain("Finish setting up your account");
    expect(page).toContain("Create your Kurx account");
    // Anchor on the ternary that CHOOSES the copy, then search forward from it. Searching the whole
    // file finds the doc comment above the code, which quotes the same sentence while explaining why
    // it must be conditional — the trap this test's own comment warned about, which the move to
    // `resuming` re-sprung by removing the class string that used to disambiguate it.
    const branch = page.indexOf("resuming ?");
    expect(branch).toBeGreaterThan(-1);
    const finish = page.indexOf('"Finish setting up your account"', branch);
    const create = page.indexOf('"Create your Kurx account"', branch);
    expect(finish).toBeGreaterThan(-1); // the resuming arm
    expect(create).toBeGreaterThan(finish); // and the create copy after it, in the else arm
  });

  it("offers a way out, so Log in can reach a login form", () => {
    expect(page).toContain("SignOutLink");
    expect(page).toMatch(/Not you\?/);
  });

  it("only offers it on the resume path — a signed-out visitor has nothing to sign out of", () => {
    const escape = page.indexOf("Not you?");
    expect(escape).toBeGreaterThan(-1);
    const guard = page.lastIndexOf("resuming ? (", escape);
    expect(guard).toBeGreaterThan(-1);
  });

  it("points back at sign-in, so the two screens are not a one-way door", () => {
    // `OtpPanel` has always offered "New to Kurx? Create an account"; nothing here pointed back
    // until D-384. Only on the signed-out arm — a resuming visitor gets the sign-out escape instead.
    expect(page).toMatch(/Already have an account\?/);
    expect(page).toMatch(/href="\/login"/);
  });

  it("is reached because /login redirects on needs_onboarding, not by accident", () => {
    // Pins the cause. If this redirect is ever removed the copy above is merely unused; if it is
    // kept, the escape route is mandatory.
    expect(login).toMatch(/needs_onboarding\s*\?\s*"\/register"/);
  });
});
