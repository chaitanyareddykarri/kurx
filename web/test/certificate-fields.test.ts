import { describe, expect, it } from "vitest";
import {
  CANONICAL_FIELDS, canonicalField, fieldLabel, fieldSample, matchCanonicalField, normalise
} from "@/lib/certificate-fields";

/**
 * The vocabulary of things that change between certificates (D-355).
 *
 * A wrong match here prints the wrong words on every certificate in a run, so matching is exact and the
 * tests care most about what it refuses. Missing a synonym costs the creator one menu click; inventing a
 * match costs them a reprint.
 */

describe("the catalogue", () => {
  it("has no duplicate keys", () => {
    const keys = CANONICAL_FIELDS.map((f) => f.key);
    expect(new Set(keys).size).toBe(keys.length);
  });

  it("gives every field a label and a sample, so nothing renders as a raw key", () => {
    for (const field of CANONICAL_FIELDS) {
      expect(field.label.length).toBeGreaterThan(0);
      expect(field.sample.length).toBeGreaterThan(0);
    }
  });

  /** Two fields claiming one synonym would make matching order-dependent. */
  it("never lets two fields claim the same synonym", () => {
    const seen = new Map<string, string>();
    for (const field of CANONICAL_FIELDS) {
      for (const synonym of field.synonyms) {
        const key = normalise(synonym);
        expect(seen.has(key), `"${synonym}" claimed by ${seen.get(key)} and ${field.key}`).toBe(false);
        seen.set(key, field.key);
      }
    }
  });

  it("covers everything the spec asks the Add Field menu to offer", () => {
    const keys = CANONICAL_FIELDS.map((f) => f.key);
    for (const required of [
      "participant_name", "event_name", "organization", "issue_date", "expiry_date",
      "certificate_id", "instructor_name", "location",
    ]) {
      expect(keys).toContain(required);
    }
  });
});

describe("matching text to a field", () => {
  /** The whole point: one concept, many spellings on real certificates. */
  it.each([
    ["Recipient's Full Name", "participant_name"],
    ["Participant Name", "participant_name"],
    ["Student Name", "participant_name"],
    ["Learner", "participant_name"],
    ["John Doe", "participant_name"],
    ["Course", "event_name"],
    ["Training", "event_name"],
    ["Workshop", "event_name"],
    ["Your Company Name", "organization"],
    ["Institution", "organization"],
    ["Date of Issue", "issue_date"],
    ["Completion Date", "issue_date"],
    ["Date of validity", "expiry_date"],
    ["Valid Until", "expiry_date"],
    ["Expires", "expiry_date"],
    ["Certificate No", "certificate_id"],
    ["Instructor", "instructor_name"],
  ])("maps %s to %s", (text, expected) => {
    expect(matchCanonicalField(text)).toBe(expected);
  });

  /** The exact string Tesseract returned from a real uploaded certificate. Note the CURLY apostrophe —
   *  designers type one and OCR faithfully reports it, while the vocabulary was written with a straight
   *  one. Normalising away punctuation is what makes those the same word rather than a silent miss. */
  it("matches the placeholder as OCR actually reports it", () => {
    expect(matchCanonicalField("[Recipient\u2019s Full Name]")).toBe("participant_name");
  });

  /** Punctuation, case and spacing are noise. */
  it("ignores case, spacing and punctuation", () => {
    for (const spelling of ["PARTICIPANT NAME", "participant_name", "Participant-Name", " participant  name "]) {
      expect(matchCanonicalField(spelling)).toBe("participant_name");
    }
  });

  /** Static certificate prose must never be mistaken for a field — this is the expensive mistake. */
  it.each([
    "Certificate of Completion",
    "has successfully completed",
    "Congratulations",
    "Authorized Signature",
    "This 8-hour training awarded 8 CPD points",
    "National Institute for Occupational Safety and Health",
  ])("refuses to match static prose: %s", (prose) => {
    expect(matchCanonicalField(prose)).toBeNull();
  });

  it("returns null for empty or meaningless input", () => {
    for (const value of ["", "   ", "!!!", "—"]) expect(matchCanonicalField(value)).toBeNull();
  });
});

describe("naming and sampling", () => {
  it("names a known field the way a creator would say it", () => {
    expect(fieldLabel("participant_name")).toBe("Participant name");
    expect(fieldLabel("expiry_date")).toBe("Expiry date");
  });

  /** A custom field keeps its own name rather than being forced into the catalogue. */
  it("makes a readable name out of an uncatalogued key", () => {
    expect(fieldLabel("cohort_number")).toBe("Cohort number");
    expect(fieldLabel(null, "Signature line")).toBe("Signature line");
  });

  /** Samples exist so the canvas reads like a finished certificate, not a form full of {tokens}. */
  it("gives realistic sample text, never a bare key", () => {
    expect(fieldSample("participant_name")).toBe("Ananya Rao");
    expect(fieldSample("issue_date")).toMatch(/\d{4}/);
    expect(fieldSample("unknown_key")).toBe("Unknown key");
    expect(fieldSample("participant_name")).not.toContain("_");
  });

  it("resolves a field object by key, and nothing for an unknown one", () => {
    expect(canonicalField("event_name")?.label).toBe("Event name");
    expect(canonicalField("nope")).toBeNull();
    expect(canonicalField(null)).toBeNull();
  });
});


/**
 * A decorative word in a badge is not a data field (D-356).
 *
 * A CPD seal reading "8 CPD / POINTS" had its "POINTS" mapped to the canonical score field: the badge was
 * covered in its own purple and a score printed into it. Missing a genuine "Points:" label costs the
 * creator one click in Add field; a confident mistake costs every certificate in the run.
 */
describe("words that must not be mistaken for data", () => {
  it("does not turn a badge's POINTS into a score field", () => {
    expect(matchCanonicalField("POINTS")).toBeNull();
  });

  it("still recognises the ways a score is actually labelled", () => {
    expect(matchCanonicalField("Score")).toBe("score");
    expect(matchCanonicalField("percentage")).toBe("score");
  });
});
