import { describe, expect, it } from "vitest";
import {
  DETECTION_UNAVAILABLE, offersAssistance, textDetectionSchema, type TextDetection
} from "@/lib/certificate-detection";

/**
 * The client half of the OCR boundary (D-344, Phase 12).
 *
 * OCR is deferred, so what is asserted here is that the *absence* of an engine is a shape the editor can
 * render rather than an error it has to survive — and that "unavailable" is never mistaken for "this
 * design has no text on it".
 */

const UNAVAILABLE: TextDetection = { available: false, regions: [], reason: "Not enabled here." };

describe("the detection contract", () => {
  it("accepts the answer every deployment gives today", () => {
    const parsed = textDetectionSchema.parse(UNAVAILABLE);

    expect(parsed.available).toBe(false);
    expect(parsed.regions).toEqual([]);
  });

  it("accepts a reason of null, since a completed detection has none", () => {
    expect(textDetectionSchema.parse({ available: true, regions: [], reason: null }).available).toBe(true);
  });

  /** Percentages of the page, 0–100, so a detected region maps straight onto a field without the editor
   *  learning anything about image dimensions. */
  it("carries regions in the editor's own coordinate space", () => {
    const parsed = textDetectionSchema.parse({
      available: true,
      reason: null,
      regions: [{ text: "Certificate of Participation", x: 10, y: 20, width: 80, height: 8, confidence: 0.94 }],
    });

    const region = parsed.regions[0];
    expect(region.x).toBeGreaterThanOrEqual(0);
    expect(region.width).toBeLessThanOrEqual(100);
    expect(region.confidence).toBeLessThanOrEqual(1);
    expect(region.text).toBe("Certificate of Participation");
  });

  /** The distinction the whole phase turns on: one is a statement about the deployment, the other about
   *  the design. Both have an empty region list, so the list alone can never be the signal. */
  it("distinguishes nothing-looked from nothing-found", () => {
    const nothingLooked = textDetectionSchema.parse(UNAVAILABLE);
    const nothingFound = textDetectionSchema.parse({ available: true, regions: [], reason: null });

    expect(nothingLooked.regions).toEqual(nothingFound.regions);
    expect(nothingLooked.available).not.toBe(nothingFound.available);
  });

  it("rejects a malformed answer rather than passing it to the canvas", () => {
    expect(() => textDetectionSchema.parse({ available: "maybe", regions: [] })).toThrow();
    expect(() => textDetectionSchema.parse({ available: true, regions: [{ text: "x" }], reason: null }))
      .toThrow();
  });
});

describe("what the editor does with it", () => {
  it("offers nothing when no engine is configured", () => {
    expect(offersAssistance(UNAVAILABLE)).toBe(false);
  });

  it("offers assistance only once an engine exists", () => {
    expect(offersAssistance({ available: true, regions: [], reason: null })).toBe(true);
  });

  /** A detector that is missing, off, or throwing must cost the editor nothing — which is why the action
   *  swallows every failure into this exact shape. */
  it("treats a failed probe as simply unavailable", () => {
    expect(offersAssistance(DETECTION_UNAVAILABLE)).toBe(false);
    expect(() => textDetectionSchema.parse(DETECTION_UNAVAILABLE)).not.toThrow();
  });
});
