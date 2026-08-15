import { z } from "zod";

/**
 * Where the uploaded design already has something printed on it (D-344).
 *
 * Its own pure module — no API client — so the overlap arithmetic can be tested directly. The editor
 * fetches the map once when a design opens and consults it locally as a field is dragged: a request per
 * mouse move would be unusable, and reading the artwork's pixels in the browser is blocked by canvas
 * tainting whenever storage is a different origin, which it is by default.
 */

export const artworkMapSchema = z.object({
  columns: z.number(),
  rows: z.number(),
  /** Row-major, one character per cell: `1` printed, `0` clear. */
  cells: z.string(),
  /** False when there was no artwork to look at, or it could not be read. */
  analysed: z.boolean(),
});

export type ArtworkMap = z.infer<typeof artworkMapSchema>;

/** Fraction of a field's footprint that sits over printed artwork, 0–1. */
export function coverage(
  map: ArtworkMap, box: { x: number; y: number; width: number; height: number }
): number {
  if (!map.analysed || map.columns <= 0 || map.rows <= 0) return 0;

  let busy = 0;
  let total = 0;

  for (let row = 0; row < map.rows; row++) {
    for (let col = 0; col < map.columns; col++) {
      // A cell counts when its centre falls inside the box, which keeps a field that merely grazes a
      // neighbouring cell from being blamed for what is in it.
      const cx = ((col + 0.5) / map.columns) * 100;
      const cy = ((row + 0.5) / map.rows) * 100;
      if (cx < box.x || cx > box.x + box.width) continue;
      if (cy < box.y || cy > box.y + box.height) continue;

      total++;
      if (map.cells[row * map.columns + col] === "1") busy++;
    }
  }

  return total === 0 ? 0 : busy / total;
}

/**
 * How much overlap is worth mentioning.
 *
 * Set well above zero on purpose. A field will often clip a border or a rule by a cell or two, and a
 * warning that fires on every design is one people learn to dismiss — at which point it is worse than no
 * warning, because the one time it matters looks like all the times it did not.
 */
export const OVERLAP_THRESHOLD = 0.18;

export function overlapsArtwork(
  map: ArtworkMap | null, box: { x: number; y: number; width: number; height: number }
): boolean {
  return map !== null && coverage(map, box) >= OVERLAP_THRESHOLD;
}
