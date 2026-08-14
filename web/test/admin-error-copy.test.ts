import { readFileSync, readdirSync, statSync } from "node:fs";
import { resolve } from "node:path";
import { describe, it, expect } from "vitest";
import { PROBLEM_COPY } from "@kurx/ui";

/**
 * The admin console's refusals must be sentences, not codes.
 *
 * An admin panel that answers every refusal with "Something went wrong. Please try again." is
 * indistinguishable from one that is broken — which is exactly how it was reported: several unrelated
 * moderation actions "failing" turned out to be several unrelated refusals, each correct, each
 * unreadable. 40 of the 71 codes reachable from the /v1/admin endpoints had no copy.
 *
 * This test reads the backend rather than a hand-kept list, so a new `Fail("...")` in an admin-reachable
 * service fails here instead of reaching a moderator as a generic sentence months later. It is the
 * mechanical half of D-315's rule; the shared table is the other half.
 */

const BACKEND = resolve(__dirname, "..", "..", "backend");

/** Services the /v1/admin endpoints delegate to. Adding one here is how the net widens. */
const ADMIN_SERVICES = [
  "UserAdminService", "OrgVerificationService", "PlatformRoleService", "FraudService",
  "AdminVerificationService", "EventReviewChecklistService", "EventAuthorizationService",
  "EventService", "OrgService",
];

function walk(dir: string): string[] {
  return readdirSync(dir).flatMap((e) => {
    const full = resolve(dir, e);
    if (statSync(full).isDirectory()) return e === "Migrations" || e === "obj" || e === "bin" ? [] : walk(full);
    return full.endsWith(".cs") ? [full] : [];
  });
}

function codesIn(files: string[]): Set<string> {
  const codes = new Set<string>();
  for (const f of files) {
    for (const m of readFileSync(f, "utf8").matchAll(/(?:Fail|Problem)\("([a-z_]+)"/g)) {
      codes.add(m[1]);
    }
  }
  return codes;
}

describe("admin refusals are readable", () => {
  it("every error code reachable from the admin surface has copy", () => {
    const all = walk(resolve(BACKEND, "Kurx.Infrastructure"))
      .concat(walk(resolve(BACKEND, "Kurx.Api", "Endpoints")));

    const adminFiles = all.filter((f) => {
      const base = f.split("/").pop()!.replace(".cs", "");
      return base.includes("Admin") || ADMIN_SERVICES.includes(base);
    });

    // Guard against the filter silently matching nothing — a passing test over an empty set is worse
    // than a failing one, and this exact mistake (a shell glob that expanded to nothing) hid the real
    // count during the audit.
    expect(adminFiles.length).toBeGreaterThan(5);

    const missing = [...codesIn(adminFiles)].filter((c) => !(c in PROBLEM_COPY)).sort();
    expect(missing).toEqual([]);
  });
});
