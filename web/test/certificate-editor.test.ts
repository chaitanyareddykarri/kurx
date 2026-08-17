import { describe, expect, it } from "vitest";

import {
  HISTORY_LIMIT, addField, canRedo, canUndo, commit, duplicateField, initHistory, maskingField,
  moveField, newField, normaliseOrder, normaliseRotation, redo, removeField, reorderField, resizeField,
  same, squareOnPage, toInput, undo, updateField,
  type DraftField,
  nextFreeSlot,
  coverArtwork,
  uncoverArtwork, startDrawing } from "@/lib/certificate-editor";

/**
 * The certificate editor's arithmetic and history (D-355, Phase 3).
 *
 * Everything here is pure, which is the point: the canvas component owns pointer capture and CSS, and
 * neither is assertable in jsdom. What IS assertable — that a field cannot be dragged off its page, that
 * undo returns exactly the previous state, that depth never ties, that a masking field carries the ground
 * it needs to actually hide anything — lives here.
 */

const field = (over: Partial<DraftField> = {}): DraftField => ({
  ...newField("dynamicfield", { fieldKey: "participant_name", label: "Participant name" }),
  id: "f1",
  ...over
});

describe("new fields", () => {
  it("places a field where it can be seen, not at the origin", () => {
    const f = newField("text");
    expect(f.x).toBeGreaterThan(0);
    expect(f.y).toBeGreaterThan(0);
  });

  it("gives a dynamic field the key it was asked for", () => {
    expect(newField("dynamicfield", { fieldKey: "employee_grade" }).field_key).toBe("employee_grade");
  });

  it("generates distinct ids, so two added fields are not one field", () => {
    expect(newField("text").id).not.toBe(newField("text").id);
  });

  it("adds on top, so a new field is never hidden beneath an existing one", () => {
    const fields = [field({ id: "a", z_order: 1 }), field({ id: "b", z_order: 2 })];
    const added = addField(fields, field({ id: "c" }));
    expect(added[2].z_order).toBeGreaterThan(2);
  });
});

describe("square boxes", () => {
  /** A QR that is not square is unreadable. Width and height are percentages of DIFFERENT edges, so
   *  equal percentages are only square on a square page. */
  it("compensates for the page aspect", () => {
    const { width, height } = squareOnPage(10, "a4-landscape");
    expect(height).toBeGreaterThan(width);
    expect(height / width).toBeCloseTo(297 / 210, 5);
  });
});

describe("moving and resizing", () => {
  it("keeps a field on the page", () => {
    const fields = [field({ x: 95, y: 95, width: 10, height: 10 })];
    const moved = moveField(fields, "f1", 50, 50);
    expect(moved[0].x).toBeLessThanOrEqual(90);
    expect(moved[0].y).toBeLessThanOrEqual(90);
  });

  it("does not let a field escape the top-left either", () => {
    const moved = moveField([field({ x: 5, y: 5 })], "f1", -50, -50);
    expect(moved[0].x).toBe(0);
    expect(moved[0].y).toBe(0);
  });

  /** A full-bleed band is legitimately wider than the page; clamping it to 0 would make it
   *  unpositionable. */
  it("inverts the clamp for a field wider than the page", () => {
    const moved = moveField([field({ x: -10, y: 0, width: 120, height: 10 })], "f1", -5, 0);
    expect(moved[0].x).toBeLessThan(0);
  });

  it("resizes from the corner without moving the origin", () => {
    const resized = resizeField([field({ x: 10, y: 20, width: 30, height: 10 })], "f1", 5, 5);
    expect(resized[0].x).toBe(10);
    expect(resized[0].y).toBe(20);
    expect(resized[0].width).toBe(35);
  });

  it("never resizes to nothing", () => {
    const resized = resizeField([field({ width: 5, height: 5 })], "f1", -100, -100);
    expect(resized[0].width).toBeGreaterThan(0);
    expect(resized[0].height).toBeGreaterThan(0);
  });

  it("rounds, so a save is not a list of floating-point noise", () => {
    const moved = moveField([field({ x: 10, y: 10 })], "f1", 1 / 3, 0);
    expect(moved[0].x.toString().split(".")[1]?.length ?? 0).toBeLessThanOrEqual(2);
  });
});

describe("rotation", () => {
  it("normalises to 0–359", () => {
    expect(normaliseRotation(370)).toBe(10);
    expect(normaliseRotation(-90)).toBe(270);
    expect(normaliseRotation(360)).toBe(0);
  });
});

describe("depth", () => {
  /** Two fields claiming the same depth is a coin toss over which is on top, so order is renumbered
   *  dense and gap-free after every change. */
  it("never ties or leaves gaps", () => {
    const messy = [field({ id: "a", z_order: 5 }), field({ id: "b", z_order: 5 }), field({ id: "c", z_order: 99 })];
    const orders = normaliseOrder(messy).map((f) => f.z_order);
    expect(orders).toEqual([1, 2, 3]);
  });

  it("brings a field forward by exactly one place", () => {
    const fields = normaliseOrder([field({ id: "a" }), field({ id: "b" }), field({ id: "c" })]);
    const after = reorderField(fields, "a", "forward");
    expect(after.find((f) => f.id === "a")!.z_order).toBe(2);
  });

  it("sends to front and back", () => {
    const fields = normaliseOrder([field({ id: "a" }), field({ id: "b" }), field({ id: "c" })]);
    expect(reorderField(fields, "a", "front").find((f) => f.id === "a")!.z_order).toBe(3);
    expect(reorderField(fields, "c", "back").find((f) => f.id === "c")!.z_order).toBe(1);
  });
});

describe("duplicating", () => {
  it("copies the styling but never the identity", () => {
    const result = duplicateField([field({ id: "a", font_size_pt: 42 })], "a");
    expect(result).not.toBeNull();
    const copy = result!.fields.find((f) => f.id === result!.id)!;
    expect(copy.id).not.toBe("a");
    expect(copy.font_size_pt).toBe(42);
  });

  it("offsets the copy so it is visibly a second thing", () => {
    const source = field({ id: "a", x: 10, y: 10 });
    const copy = duplicateField([source], "a")!.fields.find((f) => f.id !== "a")!;
    expect(copy.x).not.toBe(source.x);
    expect(copy.y).not.toBe(source.y);
  });

  it("returns null for a field that is not there, rather than a corrupted list", () => {
    expect(duplicateField([field()], "missing")).toBeNull();
  });
});

describe("cover and replace", () => {
  /** Text baked into a JPG is pixels. A masking field paints over it — so the ground colour is the
   *  whole mechanism, and without it the patch is transparent and the original shows through. */
  it("carries the ground it needs to hide anything", () => {
    const mask = maskingField({ x: 10, y: 20, width: 40, height: 8 }, "#FFFFFF", "Replacement");
    expect(mask.is_masking).toBe(true);
    expect(mask.background_color).toBe("#FFFFFF");
    expect(mask.static_text).toBe("Replacement");
  });

  /** Descenders and antialiasing spill past a tight bounding box, so a patch sized exactly to the
   *  detected region leaves a visible fringe of the old text. */
  it("pads beyond the region it covers", () => {
    const region = { x: 10, y: 20, width: 40, height: 8 };
    const mask = maskingField(region, "#FFFFFF", "x");
    expect(mask.x).toBeLessThan(region.x);
    expect(mask.y).toBeLessThan(region.y);
    expect(mask.width).toBeGreaterThan(region.width);
    expect(mask.height).toBeGreaterThan(region.height);
  });

  it("keeps the patch on the page even for a region at the edge", () => {
    const mask = maskingField({ x: 0, y: 0, width: 10, height: 5 }, "#FFF", "x");
    expect(mask.x).toBeGreaterThanOrEqual(0);
    expect(mask.y).toBeGreaterThanOrEqual(0);
  });
});

describe("history", () => {
  const a = [field({ id: "a" })];
  const b = [field({ id: "b" })];

  it("undo returns exactly the previous state", () => {
    const h = commit(initHistory(a), b);
    expect(undo(h).present).toEqual(a);
  });

  it("redo returns the state undone", () => {
    const h = redo(undo(commit(initHistory(a), b)));
    expect(h.present).toEqual(b);
  });

  /** Dragging one pixel and back would otherwise cost two undos to escape, which reads as undo being
   *  broken. */
  it("drops a commit that changes nothing", () => {
    const h = initHistory(a);
    expect(commit(h, [...a])).toBe(h);
  });

  it("editing after undoing forks the timeline and clears redo", () => {
    const h = undo(commit(initHistory(a), b));
    expect(canRedo(h)).toBe(true);
    expect(canRedo(commit(h, [field({ id: "c" })]))).toBe(false);
  });

  it("is a no-op at both ends", () => {
    const h = initHistory(a);
    expect(undo(h)).toBe(h);
    expect(redo(h)).toBe(h);
    expect(canUndo(h)).toBe(false);
  });

  it("bounds the past so a long session cannot grow without limit", () => {
    let h = initHistory(a);
    for (let i = 0; i < HISTORY_LIMIT + 20; i++) h = commit(h, [field({ id: `f${i}` })]);
    expect(h.past.length).toBeLessThanOrEqual(HISTORY_LIMIT);
  });

  it("recognises an unchanged list, which is what drives the Save button", () => {
    expect(same(a, [field({ id: "a" })])).toBe(true);
    expect(same(a, b)).toBe(false);
  });
});

describe("saving", () => {
  it("drops client-only ids and sends dense depth", () => {
    const input = toInput([field({ id: "a", z_order: 9 }), field({ id: "b", z_order: 3 })]);
    expect(input.map((f) => f.zOrder)).toEqual([1, 2]);
    expect(Object.keys(input[0])).not.toContain("id");
  });

  it("carries the masking intent and its ground through to the server shape", () => {
    const input = toInput([maskingField({ x: 5, y: 5, width: 20, height: 5 }, "#EEEEEE", "New")]);
    expect(input[0].isMasking).toBe(true);
    expect(input[0].backgroundColor).toBe("#EEEEEE");
  });

  it("preserves an arbitrary field key", () => {
    expect(toInput([field({ field_key: "employee_grade" })])[0].fieldKey).toBe("employee_grade");
  });
});

describe("mutations are immutable", () => {
  it("never touches the list it was given", () => {
    const original = [field({ id: "a", x: 10 })];
    const snapshot = JSON.stringify(original);

    moveField(original, "a", 5, 5);
    resizeField(original, "a", 5, 5);
    updateField(original, "a", { label: "changed" });
    removeField(original, "a");
    addField(original, field({ id: "z" }));

    expect(JSON.stringify(original)).toBe(snapshot);
  });
});

describe("nextFreeSlot", () => {
  /**
   * The bug this exists for: every new text or dynamic field landed at the same fixed coordinates, so
   * adding three produced three boxes stacked exactly on top of one another. On the canvas that looks
   * like one field that will not move; on the finished certificate it prints as illegible overlapping
   * text, which is how three placeholders ended up on top of each other on a real design.
   */
  const box = { x: 15, y: 45, width: 70, height: 10 };

  const at = (x: number, y: number): DraftField =>
    ({ ...newField("dynamicfield", { fieldKey: "k" }), x, y, width: 70, height: 10 });

  it("uses the default position on an empty design", () => {
    expect(nextFreeSlot([], box)).toEqual({ x: 15, y: 45 });
  });

  it("moves clear of a field already sitting there", () => {
    const slot = nextFreeSlot([at(15, 45)], box);

    expect(slot).not.toEqual({ x: 15, y: 45 });
    // And clear of it, not merely different.
    expect(slot.y).toBeGreaterThanOrEqual(55);
  });

  /** Three added in a row must occupy three distinct places — the exact case that was reported. */
  it("gives three consecutively added fields three distinct slots", () => {
    const fields: DraftField[] = [];
    const slots = [0, 1, 2].map(() => {
      const slot = nextFreeSlot(fields, box);
      fields.push({ ...at(slot.x, slot.y) });
      return `${slot.x},${slot.y}`;
    });

    expect(new Set(slots).size).toBe(3);
  });

  it("never places a field off the page", () => {
    const crowded = Array.from({ length: 12 }, (_, i) => at(15, i * 8));

    const slot = nextFreeSlot(crowded, box);

    expect(slot.y).toBeGreaterThanOrEqual(0);
    expect(slot.y + box.height).toBeLessThanOrEqual(100);
    expect(slot.x).toBeGreaterThanOrEqual(0);
    expect(slot.x + box.width).toBeLessThanOrEqual(100);
  });

  /** The caller can also refuse slots that sit over the design's own artwork, so a new field does not
   *  start life on top of the printed title. */
  it("avoids regions the caller reports as busy", () => {
    const busyTop = (c: { y: number; height: number }) => c.y < 60;

    const slot = nextFreeSlot([], box, busyTop);

    expect(slot.y).toBeGreaterThanOrEqual(60);
  });

  /** The bug this guards: a finished certificate is printed almost edge to edge, so treating artwork as
   *  a hard constraint rejected every candidate and every new field cascaded onto the last one. Clear of
   *  other fields is required; clear of the design is only preferred. */
  it("still separates fields when the whole design is covered in artwork", () => {
    const fields: DraftField[] = [];
    const slots = [0, 1, 2, 3].map(() => {
      const slot = nextFreeSlot(fields, box, () => true);
      fields.push({ ...at(slot.x, slot.y) });
      return `${slot.x},${slot.y}`;
    });

    expect(new Set(slots).size).toBe(4);
  });

  it("still returns a usable slot when everywhere is busy", () => {
    const slot = nextFreeSlot([], box, () => true);

    expect(slot.y + box.height).toBeLessThanOrEqual(100);
    expect(Number.isFinite(slot.x)).toBe(true);
  });
});

describe("coverArtwork", () => {
  /**
   * The requirement behind this: one participant name, where the placeholder was, with the placeholder
   * gone. The placeholder is pixels in the uploaded artwork, so "replace" is implemented as painting the
   * paper colour over the region and drawing the value on top — same field, same coordinates, same font.
   */
  const field: DraftField = {
    ...newField("dynamicfield", { fieldKey: "participant_name" }),
    x: 15, y: 30, width: 70, height: 10,
    font_size_pt: 32, font_family: "serif", horizontal_alignment: "center"
  };

  it("keeps the field's own styling, position and key", () => {
    const covered = coverArtwork(field, "#FDF6E3");

    expect(covered.field_key).toBe("participant_name");
    expect(covered.font_size_pt).toBe(32);
    expect(covered.font_family).toBe("serif");
    expect(covered.horizontal_alignment).toBe("center");
  });

  it("paints the sampled colour rather than assuming white", () => {
    expect(coverArtwork(field, "#FDF6E3").background_color).toBe("#FDF6E3");
    expect(coverArtwork(field, "#0B1E3C").background_color).toBe("#0B1E3C");
  });

  /** Printed text overshoots its line — descenders, commas — and a patch clipped to the nominal box
   *  leaves stray marks that read as a printing fault. */
  it("grows slightly so it covers descenders rather than clipping them", () => {
    const covered = coverArtwork(field, "#FFFFFF");

    expect(covered.y).toBeLessThan(field.y);
    expect(covered.height).toBeGreaterThan(field.height);
    expect(covered.width).toBeGreaterThan(field.width);
  });

  /** Covering neighbouring static text is a worse defect than leaving a stray pixel: it destroys
   *  something the design meant to say. An earlier, more generous pad clipped the line above on a real
   *  certificate. */
  it("grows by less than a line, so it cannot reach the text above or below", () => {
    const covered = coverArtwork(field, "#FFFFFF");

    expect(field.y - covered.y).toBeLessThanOrEqual(0.5);
    expect(covered.height - field.height).toBeLessThanOrEqual(1);
  });

  it("never grows off the page", () => {
    const edge: DraftField = { ...field, x: 0, y: 0, width: 100, height: 100 };

    const covered = coverArtwork(edge, "#FFFFFF");

    expect(covered.x).toBeGreaterThanOrEqual(0);
    expect(covered.y).toBeGreaterThanOrEqual(0);
    expect(covered.x + covered.width).toBeLessThanOrEqual(100);
    expect(covered.y + covered.height).toBeLessThanOrEqual(100);
  });

  /** Applying it twice must not keep inflating the box — a field updated repeatedly has to stay put. */
  it("is stable when applied more than once", () => {
    const once = coverArtwork(field, "#FFFFFF");
    const twice = coverArtwork(once, "#FFFFFF");

    expect(twice.width).toBeCloseTo(once.width, 5);
    expect(twice.height).toBeCloseTo(once.height, 5);
  });

  it("adds no new element — the same field comes back", () => {
    expect(coverArtwork(field, "#FFFFFF").id).toBe(field.id);
  });

  it("can be undone for a field that turned out to sit on clear space", () => {
    const undone = uncoverArtwork(coverArtwork(field, "#FFFFFF"));

    expect(undone.is_masking).toBe(false);
    expect(undone.background_color).toBeNull();
  });
});

/**
 * When a handle on the artwork becomes a mark on the page (D-356).
 *
 * A detected line draws and covers nothing while the design already says it. This decides the moment that
 * stops being true — and getting it wrong in the lenient direction puts a patch on a design the creator
 * never actually edited.
 */
describe("editing text the artwork already prints", () => {
  const mirrored = (): DraftField => ({
    ...newField("text"),
    static_text: "has successfully completed",
    mirrors_artwork: true,
    is_masking: false,
    background_color: "#FDF6E3",
  });

  it("starts covering once the words genuinely change", () => {
    const patch = startDrawing(mirrored(), "has completed with distinction");

    expect(patch.mirrors_artwork).toBe(false);
    expect(patch.is_masking).toBe(true);
    expect(patch.static_text).toBe("has completed with distinction");
  });

  /** Clicking into text and clicking straight back out must not cost the design a patch. */
  it("stays a handle when the same words are retyped", () => {
    expect(startDrawing(mirrored(), "has successfully completed")).toEqual({});
  });

  it("ignores incidental whitespace, which is not an edit either", () => {
    expect(startDrawing(mirrored(), "  has successfully completed  ")).toEqual({});
  });

  /** An ordinary element was never mirroring; its text simply changes. */
  it("leaves an ordinary element's covering exactly as it was", () => {
    const patch = startDrawing({ ...newField("text"), static_text: "Signed" }, "Countersigned");

    expect(patch).toEqual({ static_text: "Countersigned" });
    expect(patch.is_masking).toBeUndefined();
  });
});
