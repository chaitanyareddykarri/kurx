import type { CertificateField, CertificateFieldInput, CertificateFieldKind } from "@/lib/certificate-api";

/**
 * The certificate editor's rules and arithmetic, with no DOM in sight (D-344, Phase 3).
 *
 * Everything here is a pure function over an immutable field list: a mutation returns a new list and
 * never touches the one it was given. That is what makes undo a stack of whole documents rather than a
 * stack of inverse operations — the second kind has to invert every operation exactly, and the first is
 * correct by construction.
 *
 * Coordinates are percentages of the page, matching what the server stores, so nothing here needs to know
 * the editor's pixel size. Pixel→percent conversion happens once, at the drag handler.
 */

export const PAGE_ASPECT: Record<string, number> = {
  // A4 is 297×210mm landscape; portrait is the reverse.
  "a4-landscape": 297 / 210,
  "a4-portrait": 210 / 297
};

export const FONT_FAMILIES = ["sans", "serif", "mono"] as const;
export const FONT_WEIGHTS = ["normal", "bold"] as const;
export const HORIZONTAL_ALIGNMENTS = ["left", "center", "right"] as const;
export const VERTICAL_ALIGNMENTS = ["top", "middle", "bottom"] as const;
export const ZOOM_STEPS = [0.5, 0.75, 1, 1.25, 1.5, 2] as const;

/** Deep enough to cover a session's fiddling, bounded so a long edit cannot grow memory without limit. */
export const HISTORY_LIMIT = 60;

/** A working field: the server's shape plus a client-only id, because a newly added field has no server
 *  id until it is saved and the canvas still needs to address it. */
export type DraftField = Omit<CertificateField, "id"> & { id: string };

export function toDraft(field: CertificateField): DraftField {
  return { ...field };
}

/** Type-appropriate defaults, placed near the middle so a new field is visible the moment it appears —
 *  one dropped at 0,0 reads as "nothing happened". */
export function newField(kind: CertificateFieldKind, opts: { fieldKey?: string; label?: string } = {}): DraftField {
  const base = {
    id: `f-${Math.random().toString(36).slice(2, 10)}`,
    kind,
    field_key: opts.fieldKey ?? null,
    label: opts.label ?? null,
    static_text: null,
    rotation: 0,
    z_order: 0,
    is_required: false,
    is_masking: false,
    background_color: null,
    font_family: "sans",
    font_size_pt: 24,
    font_weight: "normal",
    color: "#0F172A",
    horizontal_alignment: "center",
    vertical_alignment: "middle"
  };

  if (kind === "qrcode") {
    // A QR that is not square is unreadable. Width and height are percentages of DIFFERENT edges, so
    // equal percentages are only square on a square page — the caller corrects for aspect.
    return { ...base, x: 80, y: 68, width: 13, height: 18, font_size_pt: null, color: null };
  }
  if (kind === "image") {
    return { ...base, x: 40, y: 60, width: 20, height: 15, font_size_pt: null, color: null };
  }
  if (kind === "text") {
    return { ...base, x: 15, y: 45, width: 70, height: 10, static_text: "New text" };
  }
  return { ...base, x: 15, y: 45, width: 70, height: 10 };
}

/**
 * Where a newly added field should land (D-344).
 *
 * Every new text or dynamic field used to appear at the same fixed spot, so adding three produced three
 * boxes stacked exactly on top of one another — which renders as illegible overlapping text on the
 * finished certificate and looks, on the canvas, like a single field that will not move.
 *
 * The search walks down the page from the default position and returns the first slot that collides with
 * nothing. `isBusy` lets the caller also refuse slots that sit over the design's own artwork, so a field
 * starts somewhere usable rather than somewhere that has to be dragged off the title.
 */
export function nextFreeSlot(
  existing: DraftField[],
  box: { x: number; y: number; width: number; height: number },
  isBusy: (candidate: { x: number; y: number; width: number; height: number }) => boolean = () => false
): { x: number; y: number } {
  /** A gap under each attempt, so a field that just fits is not flush against its neighbour. */
  const step = box.height + 2;

  const collides = (candidate: { x: number; y: number }) =>
    existing.some((f) =>
      // Rectangle intersection. Touching edges are fine; only real overlap is a collision.
      candidate.x < f.x + f.width &&
      candidate.x + box.width > f.x &&
      candidate.y < f.y + f.height &&
      candidate.y + box.height > f.y);

  // Two columns of candidates: straight down from the default, then nudged right, which keeps a design
  // with many fields from marching off the bottom of the page.
  for (const x of [box.x, Math.min(box.x + 8, 100 - box.width)]) {
    for (let y = box.y; y + box.height <= 100; y += step) {
      const candidate = { x, y, width: box.width, height: box.height };
      if (!collides(candidate) && !isBusy(candidate)) return { x, y };
    }
  }

  // Everywhere is taken or busy. Cascade off the default rather than returning it unchanged, so the new
  // field is at least visibly its own box and can be dragged.
  const cascade = Math.min(existing.length * 2, 100 - box.height - box.y);
  return { x: box.x, y: box.y + Math.max(0, cascade) };
}

/** A box that is square on the page, given the page it sits on. */
export function squareOnPage(widthPercent: number, pageSize: string): { width: number; height: number } {
  const aspect = PAGE_ASPECT[pageSize] ?? 1;
  return { width: widthPercent, height: widthPercent * aspect };
}

/**
 * A field that covers text printed into the uploaded artwork (D-344).
 *
 * **This is not editing.** Text baked into a JPG is pixels; nothing can modify it. A masking field paints
 * `ground` over the region and draws new text on top, which is why the ground colour matters more than
 * anything else here: get it wrong and the patch is a visible smear exactly where it was meant to be
 * invisible. Padded slightly beyond the region because descenders and antialiasing spill past a tight
 * box and would leave a fringe of the old text showing.
 */
export function maskingField(
  region: { x: number; y: number; width: number; height: number },
  ground: string,
  text: string
): DraftField {
  const padX = Math.min(1, region.width * 0.04);
  const padY = Math.min(1.5, region.height * 0.18);
  return {
    ...newField("text"),
    static_text: text,
    is_masking: true,
    background_color: ground,
    x: clamp(region.x - padX, 0, 100),
    y: clamp(region.y - padY, 0, 100),
    width: clamp(region.width + padX * 2, 1, 100),
    height: clamp(region.height + padY * 2, 1, 100)
  };
}

/**
 * Turns an existing field into one that covers the artwork behind it (D-344).
 *
 * **This is replacement, not layering — but not editing either.** Text baked into a JPG is pixels, and
 * nothing can modify pixels. What this does is paint the artwork's own colour across the field's box and
 * draw the value on top, at the same coordinates, alignment, font and size the field already had. The
 * result on the page is one value where the placeholder was, which is what "replace the placeholder" can
 * mean when the placeholder is a raster.
 *
 * The box grows slightly, because printed text has overshoot — descenders, a comma, the tail of a `y` —
 * and a patch clipped to the nominal line leaves a row of stray marks that reads as a printing fault.
 *
 * `ground` must be sampled from the artwork rather than assumed white: a white patch on cream stock is a
 * visible smear exactly where it was meant to be invisible.
 */
export function coverArtwork<T extends DraftField>(field: T, ground: string): T {
  // Idempotent. A field that is already covering has already been padded, so re-applying only refreshes
  // the colour — otherwise updating a field twice inflates its box a little each time, and a field
  // adjusted a few times creeps across the page.
  if (field.is_masking) return { ...field, background_color: ground };

  // Small, and capped hard. Enough to catch a descender or a comma hanging below the line; not enough
  // to reach the static text above or below it. An earlier, more generous pad clipped "This certificate
  // acknowledges that" on a real design — covering neighbouring static content is a worse defect than
  // leaving a stray pixel of the placeholder, because it destroys something the design meant to say.
  const padX = Math.min(0.6, field.width * 0.02);
  const padY = Math.min(0.4, field.height * 0.05);

  return {
    ...field,
    is_masking: true,
    background_color: ground,
    x: clamp(field.x - padX, 0, 100),
    y: clamp(field.y - padY, 0, 100),
    width: clamp(field.width + padX * 2, 1, 100 - clamp(field.x - padX, 0, 100)),
    height: clamp(field.height + padY * 2, 1, 100 - clamp(field.y - padY, 0, 100))
  };
}

/** Undoes {@link coverArtwork}, for a field placed over clear space after all. */
export function uncoverArtwork<T extends DraftField>(field: T): T {
  return { ...field, is_masking: false, background_color: null };
}

export function clamp(v: number, min: number, max: number): number {
  return Math.min(max, Math.max(min, v));
}

function round(v: number): number {
  // Two decimals. A percentage carried to fifteen digits makes every save look like a change and turns
  // the undo stack into a list of rounding noise.
  return Math.round(v * 100) / 100;
}

export function addField(fields: DraftField[], field: DraftField): DraftField[] {
  // Appended on top: a newly added field must not appear beneath something already placed.
  const top = fields.reduce((max, f) => Math.max(max, f.z_order), 0);
  return [...fields, { ...field, z_order: top + 1 }];
}

export function removeField(fields: DraftField[], id: string): DraftField[] {
  return fields.filter((f) => f.id !== id);
}

export function updateField(fields: DraftField[], id: string, patch: Partial<DraftField>): DraftField[] {
  return fields.map((f) => (f.id === id ? { ...f, ...patch } : f));
}

/** Move by a percentage delta, kept on the page. A field wider or taller than the page inverts the clamp
 *  so an oversized element (a full-bleed band) can still be positioned. */
export function moveField(fields: DraftField[], id: string, dx: number, dy: number): DraftField[] {
  const field = fields.find((f) => f.id === id);
  if (!field) return fields;
  const [minX, maxX] = field.width <= 100 ? [0, 100 - field.width] : [100 - field.width, 0];
  const [minY, maxY] = field.height <= 100 ? [0, 100 - field.height] : [100 - field.height, 0];
  return updateField(fields, id, {
    x: round(clamp(field.x + dx, minX, maxX)),
    y: round(clamp(field.y + dy, minY, maxY))
  });
}

/** Resize from the bottom-right corner. The origin does not move, so a resize never also looks like a
 *  drag. */
export function resizeField(fields: DraftField[], id: string, dw: number, dh: number): DraftField[] {
  const field = fields.find((f) => f.id === id);
  if (!field) return fields;
  return updateField(fields, id, {
    width: round(clamp(field.width + dw, 2, 200)),
    height: round(clamp(field.height + dh, 2, 200))
  });
}

export function rotateField(fields: DraftField[], id: string, delta: number): DraftField[] {
  const field = fields.find((f) => f.id === id);
  if (!field) return fields;
  return updateField(fields, id, { rotation: normaliseRotation(field.rotation + delta) });
}

export function normaliseRotation(deg: number): number {
  return Math.round(((deg % 360) + 360) % 360);
}

/** Copy a field, offset slightly so it is visibly a second thing, placed directly above its original in
 *  paint order — a duplicate is almost always "this again, next to it". */
export function duplicateField(fields: DraftField[], id: string): { fields: DraftField[]; id: string } | null {
  const index = fields.findIndex((f) => f.id === id);
  if (index < 0) return null;
  const source = fields[index];
  const copy: DraftField = {
    ...source,
    id: `f-${Math.random().toString(36).slice(2, 10)}`,
    x: round(clamp(source.x + 2, 0, Math.max(0, 100 - source.width))),
    y: round(clamp(source.y + 2, 0, Math.max(0, 100 - source.height))),
    z_order: source.z_order + 1
  };
  const next = [...fields].sort((a, b) => a.z_order - b.z_order);
  const at = next.findIndex((f) => f.id === id);
  next.splice(at + 1, 0, copy);
  return { fields: renumber(next), id: copy.id };
}

/** Move a field through the paint order. Depth is the z_order value, and this keeps it dense and
 *  gap-free so "forward" is always a swap with an actual neighbour. */
export function reorderField(
  fields: DraftField[], id: string, direction: "front" | "forward" | "backward" | "back"
): DraftField[] {
  const ordered = [...fields].sort((a, b) => a.z_order - b.z_order);
  const from = ordered.findIndex((f) => f.id === id);
  if (from < 0) return fields;
  const to =
    direction === "front" ? ordered.length - 1
    : direction === "back" ? 0
    : direction === "forward" ? Math.min(ordered.length - 1, from + 1)
    : Math.max(0, from - 1);
  if (to === from) return fields;

  const [moved] = ordered.splice(from, 1);
  ordered.splice(to, 0, moved);
  return renumber(ordered);
}

/** Renumbers z_order to 1..n by ARRAY POSITION, leaving the order alone.
 *
 *  Separate from `normaliseOrder` because the two are needed at different moments and conflating them is
 *  a real bug: a reorder rearranges the array and must then be numbered as it now stands. Re-sorting by
 *  the old z_order at that point silently undoes the move that was just made. */
function renumber(fields: DraftField[]): DraftField[] {
  return fields.map((f, i) => ({ ...f, z_order: i + 1 }));
}

/** Sorts by depth, then renumbers 1..n, so depth never has gaps or ties. Two fields claiming the same
 *  depth is a coin toss over which one is on top. */
export function normaliseOrder(fields: DraftField[]): DraftField[] {
  return renumber([...fields].sort((a, b) => a.z_order - b.z_order));
}

/** The shape the server accepts. Client-only ids are dropped — the canvas is the truth and the server
 *  replaces the whole set. */
export function toInput(fields: DraftField[]): CertificateFieldInput[] {
  return normaliseOrder(fields).map((f) => ({
    kind: f.kind,
    fieldKey: f.field_key ?? null,
    label: f.label ?? null,
    staticText: f.static_text ?? null,
    x: f.x,
    y: f.y,
    width: f.width,
    height: f.height,
    rotation: f.rotation,
    zOrder: f.z_order,
    isRequired: f.is_required,
    isMasking: f.is_masking,
    backgroundColor: f.background_color ?? null,
    fontFamily: f.font_family ?? null,
    fontSizePt: f.font_size_pt ?? null,
    fontWeight: f.font_weight ?? null,
    color: f.color ?? null,
    horizontalAlignment: f.horizontal_alignment,
    verticalAlignment: f.vertical_alignment
  }));
}

// ── history ─────────────────────────────────────────────────────────────────────────────────────

export type History = { past: DraftField[][]; present: DraftField[]; future: DraftField[][] };

export function initHistory(fields: DraftField[]): History {
  return { past: [], present: fields, future: [] };
}

/**
 * Record a new state. A commit that changes nothing is dropped rather than pushed — dragging a field one
 * pixel and back would otherwise cost two undos to escape, which reads as undo being broken. The redo
 * stack is cleared because editing after undoing forks the timeline.
 */
export function commit(history: History, next: DraftField[]): History {
  if (same(history.present, next)) return history;
  return { past: [...history.past, history.present].slice(-HISTORY_LIMIT), present: next, future: [] };
}

export function undo(history: History): History {
  if (history.past.length === 0) return history;
  return {
    past: history.past.slice(0, -1),
    present: history.past[history.past.length - 1],
    future: [history.present, ...history.future].slice(0, HISTORY_LIMIT)
  };
}

export function redo(history: History): History {
  if (history.future.length === 0) return history;
  const [next, ...rest] = history.future;
  return { past: [...history.past, history.present].slice(-HISTORY_LIMIT), present: next, future: rest };
}

export function canUndo(h: History): boolean { return h.past.length > 0; }
export function canRedo(h: History): boolean { return h.future.length > 0; }

/** Structural equality by serialisation. These lists are small and JSON-shaped by definition, so this is
 *  both correct and cheaper than a hand-written deep compare that has to track the field type as it
 *  gains properties. */
export function same(a: DraftField[], b: DraftField[]): boolean {
  return JSON.stringify(a) === JSON.stringify(b);
}
