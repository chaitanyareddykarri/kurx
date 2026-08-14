import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { describe, expect, it } from "vitest";
import { parsePhoneNumberFromString } from "libphonenumber-js";
import { toE164Identifier, isValidPhone } from "@kurx/ui";

/**
 * The web half of the shared corpus in `docs/api/phone-conformance.json` (D-288).
 *
 * The backend and Flutter assert the same file. All three use independent libphonenumber ports whose
 * version numbers cannot be compared to one another, so there is no version to pin — behaviour is the only
 * thing that can be held stable. Without this, a metadata update on one platform silently makes it reject a
 * number the others accept, and because the submit button is gated on client-side validity, the failure
 * lands on a user who cannot register rather than on a red build.
 */

type ConformanceCase = {
  input: string;
  region: string | null;
  e164: string | null;
  valid: boolean;
  note: string;
};

const fixture = JSON.parse(
  readFileSync(resolve(__dirname, "../../docs/api/phone-conformance.json"), "utf8")
) as { cases: ConformanceCase[] };

describe("cross-platform phone conformance", () => {
  it("has a corpus to check", () => {
    expect(fixture.cases.length).toBeGreaterThan(0);
  });

  it.each(fixture.cases)("$input — $note", (testCase) => {
    const parsed = parsePhoneNumberFromString(testCase.input);
    const valid = parsed ? parsed.isValid() : false;

    // If this platform is the outlier, bump its libphonenumber metadata — never edit the fixture to match.
    expect(valid).toBe(testCase.valid);
    if (!testCase.valid) return;

    expect(parsed!.number).toBe(testCase.e164);
    expect(parsed!.country).toBe(testCase.region);
  });

  // The shared helpers are what the app actually calls, so pin them to the same corpus rather than only
  // the library underneath them.
  it.each(fixture.cases)("$input — shared helpers agree", (testCase) => {
    expect(isValidPhone(testCase.input)).toBe(testCase.valid);
    // A valid number canonicalises; an invalid one is returned untouched rather than rewritten into
    // E.164 shape, which is what stops a bad value being sent on as though it were real.
    expect(toE164Identifier(testCase.input)).toBe(testCase.valid ? testCase.e164 : testCase.input);
  });
});
