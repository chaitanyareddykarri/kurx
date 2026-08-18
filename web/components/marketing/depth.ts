/**
 * The depth language for the marketing page (D-380) — five fixed planes, in px along Z.
 *
 * Named rather than numeric so a scene says where an object *is* instead of picking a number by eye,
 * which is the same failure the token scales were introduced to fix (D-285 §3). Depth is Z, not
 * z-index: the existing `zIndex` tokens still own paint order, and these never compete with them.
 *
 * Below `lg` no ancestor sets `perspective`, and `translateZ` without a perspective ancestor renders
 * no change at all — so the identical markup flattens to a clean 2D stack on phones with no second
 * code path and no `lg:` variant on a transform.
 *
 * **Deliberately not in `spatial.tsx`.** That file is `"use client"`, and the server components in
 * this directory call `plane()` while rendering. An import from a client module is replaced by a
 * client reference at the boundary, so calling it on the server gets a proxy rather than a function.
 * Splitting the plain values out is what lets the sections stay server-rendered — which is the whole
 * reason their copy is in the HTML for a crawler and a screen reader in the first place.
 */

import type { CSSProperties } from "react";

export const DEPTH = {
  /** Ambient light and connective tissue, behind everything. */
  ambient: -70,
  /** The plane the primary object sits on. */
  base: 0,
  /** Secondary objects, close enough to read as attached. */
  raised: 30,
  /** Objects that read as floating free. */
  float: 60,
  /** Foreground accents — badges, attestations. */
  front: 90
} as const;

/** Places a child on a depth plane, composing with any 2D transform it already needs. */
export function plane(z: number, transform2d = ""): CSSProperties {
  return { transform: `${transform2d} translateZ(${z}px)`.trim() };
}
