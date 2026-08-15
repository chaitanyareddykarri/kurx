import { describe, expect, it } from "vitest";
import {
  coverage, overlapsArtwork, OVERLAP_THRESHOLD, type ArtworkMap
} from "@/lib/certificate-artwork-map";

/**
 * Warning a creator before they place a field over their own design's text (D-355).
 *
 * The direction of the errors matters more than the precision. A missed overlap costs a warning nobody
 * sees; a false one fires on every design and teaches people to ignore the warning entirely — which is
 * worse than not having it, because the one time it matters looks like all the times it did not.
 */

/** A 10×10 map with a printed band across rows 3–4 (y 30–50%). */
function bandedMap(): ArtworkMap {
  const cells = Array.from({ length: 100 }, (_, i) => {
    const row = Math.floor(i / 10);
    return row === 3 || row === 4 ? "1" : "0";
  }).join("");
  return { columns: 10, rows: 10, cells, analysed: true };
}

const blank: ArtworkMap = { columns: 10, rows: 10, cells: "0".repeat(100), analysed: true };

describe("coverage", () => {
  it("is high for a field sitting on the printed band", () => {
    expect(coverage(bandedMap(), { x: 10, y: 30, width: 80, height: 20 })).toBeGreaterThan(0.9);
  });

  it("is zero for a field on clear space", () => {
    expect(coverage(bandedMap(), { x: 10, y: 60, width: 80, height: 20 })).toBe(0);
  });

  it("is partial for a field that half covers the band", () => {
    const value = coverage(bandedMap(), { x: 10, y: 40, width: 80, height: 20 });

    expect(value).toBeGreaterThan(0.2);
    expect(value).toBeLessThan(0.8);
  });

  it("is zero on a design with nothing printed", () => {
    expect(coverage(blank, { x: 0, y: 0, width: 100, height: 100 })).toBe(0);
  });

  /** An absent analysis must never read as "your design is clear here". */
  it("reports nothing when the artwork could not be analysed", () => {
    const unanalysed: ArtworkMap = { ...bandedMap(), analysed: false };

    expect(coverage(unanalysed, { x: 10, y: 30, width: 80, height: 20 })).toBe(0);
    expect(overlapsArtwork(unanalysed, { x: 10, y: 30, width: 80, height: 20 })).toBe(false);
  });

  it("survives a zero-sized box rather than dividing by nothing", () => {
    expect(coverage(bandedMap(), { x: 50, y: 50, width: 0, height: 0 })).toBe(0);
  });
});

describe("overlapsArtwork", () => {
  it("warns when a field is dropped onto printed text", () => {
    expect(overlapsArtwork(bandedMap(), { x: 10, y: 30, width: 80, height: 20 })).toBe(true);
  });

  it("stays quiet on clear space", () => {
    expect(overlapsArtwork(bandedMap(), { x: 10, y: 60, width: 80, height: 20 })).toBe(false);
  });

  /** Clipping a border by a cell or two is normal and must not warn. */
  it("stays quiet when a field merely grazes something", () => {
    const grazing = coverage(bandedMap(), { x: 10, y: 48, width: 80, height: 40 });

    expect(grazing).toBeLessThan(OVERLAP_THRESHOLD);
    expect(overlapsArtwork(bandedMap(), { x: 10, y: 48, width: 80, height: 40 })).toBe(false);
  });

  it("stays quiet when there is no map at all", () => {
    expect(overlapsArtwork(null, { x: 10, y: 30, width: 80, height: 20 })).toBe(false);
  });
});
