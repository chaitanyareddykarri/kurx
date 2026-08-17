import { describe, expect, it } from "vitest";

import {
  ACCEPTED_IMAGE_TYPES, DEFAULT_EDITS, MAX_IMAGE_BYTES, MIN_IMAGE_DIMENSION,
  clamp, clampOffsets, coverScale, filterString, formatBytes, isDefaultEdits,
  normaliseRotation, outputSize, panBounds, validateImageFile, validateImageSize
} from "@/lib/image-edit";

/**
 * The rules and the geometry behind the profile-image editor.
 *
 * These are the assertions the component tests cannot make: jsdom has no 2D canvas context and no
 * image decoder, so nothing that renders can prove the crop actually covers its frame. Keeping the
 * maths pure is what makes it checkable at all.
 */

const file = (type: string, size: number) => ({ type, size });

describe("file validation", () => {
  it("accepts the three editable formats", () => {
    for (const type of ACCEPTED_IMAGE_TYPES) {
      expect(validateImageFile(file(type, 1024))).toBeNull();
    }
  });

  it("rejects an unsupported type by name, not by code", () => {
    const msg = validateImageFile(file("application/pdf", 1024));
    expect(msg).toContain("JPG, PNG or WebP");
  });

  // GIF and AVIF are image types a user reasonably expects to work, so their refusal has to be
  // deliberate rather than incidental — re-encoding a GIF through a canvas drops the animation.
  it("rejects GIF and AVIF, which the editor would silently degrade", () => {
    expect(validateImageFile(file("image/gif", 1024))).not.toBeNull();
    expect(validateImageFile(file("image/avif", 1024))).not.toBeNull();
  });

  it("rejects an oversized file and says how big it was", () => {
    const msg = validateImageFile(file("image/png", MAX_IMAGE_BYTES + 1));
    expect(msg).toContain("5 MB");
    expect(msg).toContain("MB");
  });

  it("accepts a file exactly at the limit — the boundary is inclusive", () => {
    expect(validateImageFile(file("image/png", MAX_IMAGE_BYTES))).toBeNull();
  });

  it("rejects an empty file", () => {
    expect(validateImageFile(file("image/png", 0))).toContain("empty");
  });
});

describe("dimension validation", () => {
  it("rejects an image too small to crop usefully, quoting both sides", () => {
    const msg = validateImageSize(64, 64);
    expect(msg).toContain("64×64");
    expect(msg).toContain(String(MIN_IMAGE_DIMENSION));
  });

  it("rejects when only one side is too small", () => {
    expect(validateImageSize(2000, 40)).not.toBeNull();
    expect(validateImageSize(40, 2000)).not.toBeNull();
  });

  it("accepts exactly the minimum", () => {
    expect(validateImageSize(MIN_IMAGE_DIMENSION, MIN_IMAGE_DIMENSION)).toBeNull();
  });
});

describe("coverScale", () => {
  it("scales up so a small image still fills the frame", () => {
    expect(coverScale(256, 256, 512, 512)).toBe(2);
  });

  it("covers rather than contains — a wide source is scaled by its short side", () => {
    // 1000×500 into a 500×500 square: containing would scale by 0.5 and leave bars top and bottom.
    expect(coverScale(1000, 500, 500, 500)).toBe(1);
  });

  // The bug this guards: at a quarter turn the source's extent swaps, so scaling against the
  // unrotated dimensions leaves two uncovered bars inside the saved crop.
  it("accounts for the swapped extent at 90 and 270 degrees", () => {
    expect(coverScale(1000, 500, 500, 500, 90)).toBe(1);
    expect(coverScale(500, 1000, 500, 500, 90)).toBe(1);
    expect(coverScale(1000, 500, 500, 500, 270)).toBe(1);
    // A half turn does not swap anything, so it matches the unrotated scale.
    expect(coverScale(1000, 500, 500, 500, 180)).toBe(coverScale(1000, 500, 500, 500, 0));
  });
});

describe("panBounds and clamping", () => {
  it("allows no pan when the image exactly covers the frame", () => {
    expect(panBounds(500, 500, 500, 500, { zoom: 1, rotation: 0 })).toEqual({ maxX: 0, maxY: 0 });
  });

  it("allows pan along the overflowing axis only", () => {
    const b = panBounds(1000, 500, 500, 500, { zoom: 1, rotation: 0 });
    expect(b.maxX).toBe(250);
    expect(b.maxY).toBe(0);
  });

  it("grows the pan range with zoom", () => {
    const at1 = panBounds(500, 500, 500, 500, { zoom: 1, rotation: 0 });
    const at2 = panBounds(500, 500, 500, 500, { zoom: 2, rotation: 0 });
    expect(at2.maxX).toBeGreaterThan(at1.maxX);
    expect(at2.maxX).toBe(250);
  });

  it("pulls an out-of-range offset back inside the frame", () => {
    const { offsetX, offsetY } = clampOffsets(1000, 500, 500, 500, {
      ...DEFAULT_EDITS, offsetX: 9999, offsetY: 9999
    });
    expect(offsetX).toBe(250);
    expect(offsetY).toBe(0);
  });

  // Zooming back out strands an offset that was legal a moment earlier; without a re-clamp the saved
  // crop shows a transparent wedge where the image no longer reaches.
  it("re-clamps a formerly legal offset after zooming out", () => {
    const zoomedIn = clampOffsets(500, 500, 500, 500, { ...DEFAULT_EDITS, zoom: 2, offsetX: 240 });
    expect(zoomedIn.offsetX).toBe(240);
    const zoomedOut = clampOffsets(500, 500, 500, 500, { ...DEFAULT_EDITS, zoom: 1, offsetX: 240 });
    expect(zoomedOut.offsetX).toBe(0);
  });
});

describe("rotation", () => {
  it("normalises into 0–359, including negatives from Rotate left", () => {
    expect(normaliseRotation(-90)).toBe(270);
    expect(normaliseRotation(360)).toBe(0);
    expect(normaliseRotation(450)).toBe(90);
  });

  it("returns to zero after four left turns", () => {
    let r = 0;
    for (let i = 0; i < 4; i++) r = normaliseRotation(r - 90);
    expect(r).toBe(0);
  });
});

describe("filters and reset", () => {
  it("emits none at defaults rather than a no-op filter", () => {
    expect(filterString({ brightness: 1, contrast: 1 })).toBe("none");
  });

  it("emits both functions once either is adjusted", () => {
    expect(filterString({ brightness: 1.2, contrast: 1 })).toBe("brightness(1.2) contrast(1)");
  });

  it("recognises the pristine state, so Reset can be disabled when it would do nothing", () => {
    expect(isDefaultEdits(DEFAULT_EDITS)).toBe(true);
    expect(isDefaultEdits({ ...DEFAULT_EDITS, zoom: 1.5 })).toBe(false);
    expect(isDefaultEdits({ ...DEFAULT_EDITS, rotation: 90 })).toBe(false);
    expect(isDefaultEdits({ ...DEFAULT_EDITS, brightness: 1.1 })).toBe(false);
    expect(isDefaultEdits({ ...DEFAULT_EDITS, offsetX: 3 })).toBe(false);
  });
});

describe("output", () => {
  it("saves a square avatar and a 3:1 cover", () => {
    expect(outputSize("avatar")).toEqual({ width: 512, height: 512 });
    expect(outputSize("cover")).toEqual({ width: 1200, height: 400 });
  });
});

describe("helpers", () => {
  it("clamps", () => {
    expect(clamp(5, 0, 10)).toBe(5);
    expect(clamp(-1, 0, 10)).toBe(0);
    expect(clamp(11, 0, 10)).toBe(10);
  });

  it("formats sizes the way the error message reads them", () => {
    expect(formatBytes(6 * 1024 * 1024)).toBe("6.0 MB");
    expect(formatBytes(2048)).toBe("2 KB");
    expect(formatBytes(12)).toBe("12 B");
  });
});
