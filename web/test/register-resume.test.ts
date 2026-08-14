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
 */
const page = readFileSync(resolve(__dirname, "..", "app/register/page.tsx"), "utf8");
const login = readFileSync(resolve(__dirname, "..", "app/(public)/login/page.tsx"), "utf8");

describe("arriving at /register already signed in", () => {
  it("does not tell an existing account to create one", () => {
    // Both sentences exist; what matters is that the "Create" one is not unconditional.
    expect(page).toContain("Finish setting up your account");
    expect(page).toContain("Create your Kurx account");
    // The heading element, not the prose in the comment above it that names the same string.
    const create = page.indexOf("<h1 className=\"text-2xl font-semibold\">Create your Kurx account");
    const branch = page.indexOf("initialStatus ? (");
    expect(create).toBeGreaterThan(-1);
    expect(branch).toBeGreaterThan(-1);
    expect(create).toBeGreaterThan(branch); // the create copy sits inside the else arm
  });

  it("offers a way out, so Log in can reach a login form", () => {
    expect(page).toContain("SignOutLink");
    expect(page).toMatch(/Not you\?/);
  });

  it("only offers it on the resume path — a signed-out visitor has nothing to sign out of", () => {
    const escape = page.indexOf("Not you?");
    const guard = page.lastIndexOf("initialStatus ? (", escape);
    expect(guard).toBeGreaterThan(-1);
  });

  it("is reached because /login redirects on needs_onboarding, not by accident", () => {
    // Pins the cause. If this redirect is ever removed the copy above is merely unused; if it is
    // kept, the escape route is mandatory.
    expect(login).toMatch(/needs_onboarding\s*\?\s*"\/register"/);
  });
});
