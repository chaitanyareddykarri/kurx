import { describe, expect, it } from "vitest";
import { autoLayout, foldDetection } from "@/lib/certificate-autolayout";
import { newField } from "@/lib/certificate-editor";
import type { DraftField } from "@/lib/certificate-editor";
import type { TextDetection } from "@/lib/certificate-detection";

/**
 * Turning detected text into clickable elements (D-355).
 *
 * The step that makes an uploaded raster editable. What matters most is that every element it creates
 * **covers** what it sits on — without that the creator sees their new text and the printed characters
 * underneath, which is the duplication detection exists to remove.
 */

const region = (over: Partial<TextDetection["regions"][number]> = {}) => ({
  text: "Certificate of Completion",
  x: 10, y: 20, width: 60, height: 8, confidence: 0.95, ground: "#FDFDFD", lines: 1,
  ...over,
});

const scan = (regions: TextDetection["regions"]): TextDetection =>
  ({ available: true, regions, reason: null });

describe("what gets created", () => {
  it("makes one element per detected line", () => {
    const result = autoLayout(scan([region(), region({ text: "has successfully completed", y: 40 })]));

    expect(result.fields).toHaveLength(2);
  });

  /** A field that REPLACES printed text must hide it, using the paper colour sampled from the artwork —
   *  not an assumed white, which smears on cream stock. */
  it("covers the printed text underneath, with the sampled colour", () => {
    const [field] = autoLayout(scan([region({ text: "[Recipient's Full Name]", ground: "#FDF6E3" })])).fields;

    expect(field.kind).toBe("dynamicfield");
    expect(field.is_masking).toBe(true);
    expect(field.mirrors_artwork).toBe(false);
    expect(field.background_color).toBe("#FDF6E3");
  });

  /**
   * And text nobody is changing covers nothing.
   *
   * Painting a flat rectangle over words the design already prints erases its watermark and texture,
   * leaving a smooth hard-edged patch — the "this was edited" look, applied to every line on the page for
   * no gain at all. The element still exists so the words stay clickable; it just draws nothing.
   */
  it("leaves text it is not changing completely untouched", () => {
    const [field] = autoLayout(scan([region({ text: "has successfully completed", ground: "#FDF6E3" })])).fields;

    expect(field.kind).toBe("text");
    expect(field.mirrors_artwork).toBe(true);
    expect(field.is_masking).toBe(false);
    // The colour is still carried, ready for the edit that may never come.
    expect(field.background_color).toBe("#FDF6E3");
  });

  it("places the element exactly where the text was", () => {
    const [field] = autoLayout(scan([region({ x: 12.5, y: 33, width: 55, height: 9 })])).fields;

    expect(field.x).toBeCloseTo(12.5, 3);
    expect(field.y).toBeCloseTo(33, 3);
    expect(field.width).toBeCloseTo(55, 3);
    expect(field.height).toBeCloseTo(9, 3);
  });

  /** A replacement at a default size reads as obviously wrong; sizing from the measured box keeps it
   *  close to the original. */
  it("sizes the type to the box the engine measured", () => {
    const small = autoLayout(scan([region({ height: 3 })])).fields[0];
    const large = autoLayout(scan([region({ height: 12 })])).fields[0];

    expect(large.font_size_pt!).toBeGreaterThan(small.font_size_pt!);
    expect(small.font_size_pt!).toBeGreaterThanOrEqual(6);
  });

  /** A stylised "Your Company Name" logo came back as "© 'our" at 0.76. Covering artwork with that
   *  replaces the creator's own mark with three wrong characters — worse than doing nothing. */
  it("leaves artwork alone rather than covering it with a shaky read", () => {
    const misread = autoLayout(scan([region({ text: "© 'our", confidence: 0.76 })]));
    expect(misread.fields).toHaveLength(0);

    // And still covers text it is confident about, which is the whole point of the pass.
    const solid = autoLayout(scan([region({ text: "© 'our", confidence: 0.95 })]));
    expect(solid.fields).toHaveLength(1);
  });

  /** Merging lines into a paragraph made the block three times as tall as its type. Sizing from the
   *  block rendered "Ensuring expertise in" at heading size across a body paragraph — the type has to be
   *  sized per line. */
  it("sizes a merged paragraph by its line, not by the whole block", () => {
    const oneLine = autoLayout(scan([region({ height: 4, lines: 1 })])).fields[0];
    const threeLines = autoLayout(scan([region({ height: 12, lines: 3 })])).fields[0];

    expect(threeLines.font_size_pt).toBe(oneLine.font_size_pt);
    // And a wrapped block needs leading, or its lines land on top of each other.
    expect(threeLines.line_height).toBeGreaterThan(1);
    expect(oneLine.line_height).toBeNull();
  });

  it("keeps page order, so the panel reads like the certificate", () => {
    const result = autoLayout(scan([
      region({ text: "Title", y: 10 }),
      region({ text: "Body text here", y: 50 }),
    ]));

    expect(result.fields.map((f) => f.z_order)).toEqual([0, 1]);
  });
});

describe("what becomes a field", () => {
  /** The whole promise of detection: a placeholder printed on the design becomes something that fills
   *  itself per recipient, without the creator configuring anything. */
  it.each([
    ["[Recipient's Full Name]", "participant_name"],
    ["Date of Issue", "issue_date"],
    ["Valid Until", "expiry_date"],
    ["Your Company Name", "organization"],
    ["John Doe", "participant_name"],
  ])("turns %s into the %s field", (text, key) => {
    const [field] = autoLayout(scan([region({ text })])).fields;

    expect(field.kind).toBe("dynamicfield");
    expect(field.field_key).toBe(key);
    // A dynamic field carries no fixed words — its text comes from the participant list.
    expect(field.static_text).toBeNull();
  });

  /** Prose stays prose. Guessing wrong here means every certificate says one person's name. */
  it.each([
    "Certificate of Completion",
    "has successfully completed",
    "This 8-hour training awarded 8 CPD points",
  ])("leaves %s as text the creator can retype", (text) => {
    const [field] = autoLayout(scan([region({ text })])).fields;

    expect(field.kind).toBe("text");
    expect(field.static_text).toBe(text);
    expect(field.field_key).toBeNull();
  });

  it("reports which fields it recognised, without duplicates", () => {
    const result = autoLayout(scan([
      region({ text: "Recipient's Full Name", y: 30 }),
      region({ text: "Participant Name", y: 45 }),
      region({ text: "Date of Issue", y: 60 }),
    ]));

    expect(result.dynamicCount).toBe(3);
    expect(result.dynamicKeys).toEqual(["participant_name", "issue_date"]);
  });
});

describe("what gets refused", () => {
  /** A wrong box costs a deletion AND the creator's trust in every other box on the page. */
  it("drops low-confidence guesses", () => {
    expect(autoLayout(scan([region({ confidence: 0.2 })])).fields).toHaveLength(0);
  });

  it("drops stray marks and punctuation", () => {
    expect(autoLayout(scan([region({ text: "|" }), region({ text: "." })])).fields).toHaveLength(0);
  });

  it("drops regions too small to click", () => {
    expect(autoLayout(scan([region({ width: 0.4, height: 0.2 })])).fields).toHaveLength(0);
  });

  /** An unavailable scan is not an empty design — it must produce nothing rather than wiping a layout. */
  it("creates nothing when detection did not run", () => {
    const result = autoLayout({ available: false, regions: [], reason: "not enabled" });

    expect(result.fields).toHaveLength(0);
    expect(result.dynamicCount).toBe(0);
  });

  it("never places an element off the page", () => {
    const [field] = autoLayout(scan([region({ x: 95, y: 97, width: 40, height: 20 })])).fields;

    expect(field.x + field.width).toBeLessThanOrEqual(100);
    expect(field.y + field.height).toBeLessThanOrEqual(100);
  });
});


/**
 * Re-reading a design that has already been read (D-356).
 *
 * The defect this exists to prevent is the one that made every detection improvement look like it had
 * done nothing: scanning twice appended a second element to every line, and the OLDER one — built before
 * the fix, still covering the artwork with its own fill — sat on top. The new reading was invisible
 * underneath it, and the page rendered exactly as it had before.
 */
describe("folding a fresh reading into an existing design", () => {
  const at = (over: Partial<DraftField>): DraftField =>
    ({ ...newField("text"), x: 10, y: 20, width: 60, height: 8, ...over });

  it("replaces an element covering the same words", () => {
    const stale = at({ static_text: "old reading", is_masking: true, background_color: "#F8F8F8" });
    const fresh = at({ static_text: "new reading", mirrors_artwork: true });

    const result = foldDetection([stale], [fresh]);

    expect(result).toHaveLength(1);
    expect(result[0].static_text).toBe("new reading");
  });

  it("keeps everything the reading does not describe", () => {
    const signature = at({ y: 80, static_text: "Signed" });
    const fresh = at({ static_text: "Certificate of Completion" });

    const result = foldDetection([signature, at({ static_text: "stale" })], [fresh]);

    expect(result.map((f) => f.static_text)).toEqual(["Signed", "Certificate of Completion"]);
  });

  /** Certificate lines sit close together. A shared edge is a neighbour, not a duplicate — treating it as
   *  one would silently delete the line above every time a design was rescanned. */
  it("does not swallow the line above just because it is adjacent", () => {
    const above = at({ y: 12, height: 8 });
    const fresh = at({ y: 20, height: 8 });

    expect(foldDetection([above], [fresh])).toHaveLength(2);
  });

  /**
   * The case that survived the first version of this fold, and went on looking exactly like the old
   * implementation in the most visible place on the page.
   *
   * A stylised logo was read as `© 'our` at 0.76 and saved as a covering element. Re-reading the design
   * now correctly DECLINES that region — too weak to paint over artwork — so no fresh block is produced
   * there, nothing superseded the old one, and it went on covering the logo with a grey patch and a wrong
   * reading. A region the engine read and declined still means "leave the artwork alone here".
   */
  it("drops a stale element where the new reading deliberately declined", () => {
    const stale = at({ static_text: "© 'our", is_masking: true, background_color: "#F8F8F8" });
    const declined: TextDetection = {
      available: true,
      reason: null,
      regions: [{ text: "© 'our", x: 10, y: 20, width: 60, height: 8, confidence: 0.76, ground: "#FFF", lines: 1 }],
    };

    expect(foldDetection([stale], [], declined)).toHaveLength(0);
  });

  it("still keeps hand-placed content the engine never read", () => {
    const signature = at({ y: 80, static_text: "Signed" });
    const declined: TextDetection = {
      available: true,
      reason: null,
      regions: [{ text: "© 'our", x: 10, y: 20, width: 60, height: 8, confidence: 0.76, ground: "#FFF", lines: 1 }],
    };

    expect(foldDetection([signature], [], declined).map((f) => f.static_text)).toEqual(["Signed"]);
  });

  it("leaves a first scan alone, since there is nothing to replace", () => {
    const fresh = [at({ static_text: "a" }), at({ static_text: "b", y: 50 })];

    expect(foldDetection([], fresh)).toEqual(fresh);
  });
});


/**
 * A lead-in announces the recipient; it is not the recipient (D-359).
 *
 * `Presented to` was a synonym for the participant's name, which reads a certificate exactly backwards.
 * The words "Presented to" were replaced by the recipient's name, while the name printed underneath stayed
 * as fixed text — so every certificate in a run carried one person's name twice and lost its own label.
 */
describe("the line a lead-in points at", () => {
  const line = (text: string, y: number, over: Partial<TextDetection["regions"][number]> = {}) =>
    ({ text, x: 20, y, width: 60, height: 6, confidence: 0.95, ground: "#FFF", lines: 1, ...over });

  it("keeps the lead-in as words on the page", () => {
    const { fields } = autoLayout(scan([line("Presented to", 20), line("OLIVER SMITH", 30)]));
    const lead = fields.find((f) => f.static_text === "Presented to");

    expect(lead).toBeTruthy();
    expect(lead!.kind).toBe("text");
    expect(lead!.mirrors_artwork).toBe(true);
  });

  it("makes the line beneath it the participant's name", () => {
    const { fields, dynamicKeys } = autoLayout(scan([line("Presented to", 20), line("OLIVER SMITH", 30)]));
    const name = fields.find((f) => f.kind === "dynamicfield");

    expect(name?.field_key).toBe("participant_name");
    expect(name?.is_masking).toBe(true);
    expect(dynamicKeys).toContain("participant_name");
  });

  /** Promoting the wrong line prints the recipient's name over the course title on every certificate. */
  it("ignores a line too far below to be the name", () => {
    const { fields } = autoLayout(scan([line("Presented to", 20), line("OLIVER SMITH", 70)]));

    expect(fields.every((f) => f.kind === "text")).toBe(true);
  });

  it("ignores a line that does not sit under it", () => {
    const { fields } = autoLayout(scan([
      line("Presented to", 20, { x: 5, width: 20 }),
      line("Date of Issue", 30, { x: 70, width: 25 }),
    ]));

    expect(fields.some((f) => f.field_key === "participant_name")).toBe(false);
  });

  it("does not steal a line that is already something else", () => {
    const { fields } = autoLayout(scan([line("Presented to", 20), line("Date of Issue", 27)]));

    expect(fields.some((f) => f.field_key === "participant_name")).toBe(false);
    expect(fields.some((f) => f.field_key === "issue_date")).toBe(true);
  });
});
