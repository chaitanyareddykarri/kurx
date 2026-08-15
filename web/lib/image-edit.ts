/**
 * Profile-image editing: the rules and the geometry, with no DOM in sight.
 *
 * The editor component owns a canvas, pointer drags and object URLs, none of which jsdom implements —
 * so everything a test needs to pin lives here instead: what counts as an acceptable file, and where a
 * given zoom/offset/rotation puts the source image inside the output square. The component reads these
 * and draws; it decides nothing on its own.
 */

/** Formats the profile-image slots accept. `image/gif` and `image/avif` are deliberately absent from
 *  the editable set: a GIF loses its animation the moment it is re-encoded through a canvas, and AVIF
 *  has no `toBlob` encoder in Safari, so both would silently degrade rather than fail. */
export const ACCEPTED_IMAGE_TYPES = ["image/jpeg", "image/png", "image/webp"] as const;
export const ACCEPT_ATTRIBUTE = ACCEPTED_IMAGE_TYPES.join(",");

export const MAX_IMAGE_BYTES = 5 * 1024 * 1024;
/** Below this the crop would upscale past the point of usefulness — an avatar renders at 128px on the
 *  profile header and 2× that on a retina panel, so anything under 128 source pixels is already soft. */
export const MIN_IMAGE_DIMENSION = 128;

export const OUTPUT_SIZE = { avatar: 512, cover: 1200 } as const;
export const ASPECT = { avatar: 1, cover: 3 } as const;

export type Slot = keyof typeof ASPECT;

/** A human sentence, or null when the file is acceptable. Checked before any byte is read. */
export function validateImageFile(file: { type: string; size: number }): string | null {
  if (!(ACCEPTED_IMAGE_TYPES as readonly string[]).includes(file.type)) {
    return "That file type isn't supported. Use a JPG, PNG or WebP image.";
  }
  if (file.size > MAX_IMAGE_BYTES) {
    return `That image is ${formatBytes(file.size)}. The limit is 5 MB — try a smaller one.`;
  }
  if (file.size === 0) return "That file is empty. Choose a different image.";
  return null;
}

/** Checked after decode, because dimensions are not knowable from the file metadata alone. */
export function validateImageSize(width: number, height: number): string | null {
  if (width < MIN_IMAGE_DIMENSION || height < MIN_IMAGE_DIMENSION) {
    return `That image is ${width}×${height}. Both sides need to be at least ${MIN_IMAGE_DIMENSION} pixels.`;
  }
  return null;
}

export function formatBytes(bytes: number): string {
  if (bytes >= 1024 * 1024) return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  if (bytes >= 1024) return `${Math.round(bytes / 1024)} KB`;
  return `${bytes} B`;
}

export type Edits = {
  /** 1 = the image exactly covers the crop frame. Never below 1, or the frame would show gaps. */
  zoom: number;
  /** Pan, in output pixels, from centred. */
  offsetX: number;
  offsetY: number;
  /** Multiples of 90°; the editor only offers right angles, so the cover fit stays exact. */
  rotation: number;
  /** 1 = unchanged, matching the CSS filter functions these map to. */
  brightness: number;
  contrast: number;
};

export const DEFAULT_EDITS: Edits = {
  zoom: 1,
  offsetX: 0,
  offsetY: 0,
  rotation: 0,
  brightness: 1,
  contrast: 1
};

export const ZOOM_RANGE = { min: 1, max: 4, step: 0.01 } as const;
export const BRIGHTNESS_RANGE = { min: 0.5, max: 1.5, step: 0.01 } as const;
export const CONTRAST_RANGE = { min: 0.5, max: 1.5, step: 0.01 } as const;

export function isDefaultEdits(e: Edits): boolean {
  return (
    e.zoom === DEFAULT_EDITS.zoom &&
    e.offsetX === DEFAULT_EDITS.offsetX &&
    e.offsetY === DEFAULT_EDITS.offsetY &&
    e.rotation === DEFAULT_EDITS.rotation &&
    e.brightness === DEFAULT_EDITS.brightness &&
    e.contrast === DEFAULT_EDITS.contrast
  );
}

export function normaliseRotation(deg: number): number {
  return ((deg % 360) + 360) % 360;
}

/**
 * How to draw `source` so it covers a `frameW × frameH` crop at the given zoom and rotation.
 *
 * "Cover", not "contain": the crop frame is what gets saved, so any gap inside it would be baked into
 * the avatar as transparent pixels. At a quarter turn the source's width and height swap relative to
 * the frame, which is why the cover ratio is computed against the *rotated* extent rather than the
 * raw one — without that, rotating a wide photo into a square frame leaves two bars.
 */
export function coverScale(
  sourceW: number,
  sourceH: number,
  frameW: number,
  frameH: number,
  rotation = 0
): number {
  const quarterTurned = normaliseRotation(rotation) % 180 === 90;
  const effectiveW = quarterTurned ? sourceH : sourceW;
  const effectiveH = quarterTurned ? sourceW : sourceH;
  return Math.max(frameW / effectiveW, frameH / effectiveH);
}

/**
 * The largest pan that keeps the frame covered, per axis. Dragging is clamped to this so the user
 * cannot pull the image off its own crop — the failure it prevents is a saved avatar with a
 * transparent wedge in one corner, which looks like a rendering bug rather than a bad drag.
 */
export function panBounds(
  sourceW: number,
  sourceH: number,
  frameW: number,
  frameH: number,
  edits: Pick<Edits, "zoom" | "rotation">
): { maxX: number; maxY: number } {
  const scale = coverScale(sourceW, sourceH, frameW, frameH, edits.rotation) * edits.zoom;
  const quarterTurned = normaliseRotation(edits.rotation) % 180 === 90;
  const drawnW = (quarterTurned ? sourceH : sourceW) * scale;
  const drawnH = (quarterTurned ? sourceW : sourceH) * scale;
  return {
    maxX: Math.max(0, (drawnW - frameW) / 2),
    maxY: Math.max(0, (drawnH - frameH) / 2)
  };
}

export function clamp(value: number, min: number, max: number): number {
  return Math.min(max, Math.max(min, value));
}

/** Pan clamped into `panBounds`. Applied on every drag and again whenever zoom or rotation changes,
 *  because shrinking the drawn image can strand an offset that was legal a moment ago. */
export function clampOffsets(
  sourceW: number,
  sourceH: number,
  frameW: number,
  frameH: number,
  edits: Edits
): { offsetX: number; offsetY: number } {
  const { maxX, maxY } = panBounds(sourceW, sourceH, frameW, frameH, edits);
  return {
    offsetX: clamp(edits.offsetX, -maxX, maxX),
    offsetY: clamp(edits.offsetY, -maxY, maxY)
  };
}

/** The `filter` string for both the live preview and the saved render, so what is previewed is what
 *  is encoded. Returns "none" at defaults rather than a no-op filter — some engines rasterise a
 *  filtered layer even when the filter changes nothing. */
export function filterString(edits: Pick<Edits, "brightness" | "contrast">): string {
  if (edits.brightness === 1 && edits.contrast === 1) return "none";
  return `brightness(${edits.brightness}) contrast(${edits.contrast})`;
}

/** Output dimensions for a slot's saved image. */
export function outputSize(slot: Slot): { width: number; height: number } {
  const width = OUTPUT_SIZE[slot];
  return { width, height: Math.round(width / ASPECT[slot]) };
}
