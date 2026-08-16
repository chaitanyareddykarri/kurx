import type { DraftField } from "@/lib/certificate-editor";
import { newField } from "@/lib/certificate-editor";
import { matchCanonicalField, fieldLabel, leadsIntoName } from "@/lib/certificate-fields";
import type { TextDetection } from "@/lib/certificate-detection";

/**
 * Turning detected text into things a creator can click (D-355).
 *
 * The step that makes an uploaded raster editable. Each line the engine found becomes a real element at
 * the same place, **covering** the printed characters underneath — because the alternative is what the
 * creator was doing by hand: place a box, click Cover, repeat for every line on the page.
 *
 * Pure and separate from the editor so the decisions here — what becomes a dynamic field, what gets
 * covered, what is too small to bother with — can be tested directly.
 */

/** Detected text this short is punctuation, a stray mark, or a border artefact wearing a letter's shape. */
const MIN_TEXT_LENGTH = 2;

/**
 * The bar for *covering artwork*, which is a higher bar than "is this text".
 *
 * Every element built here paints over what it sits on, so a misread does not merely add a wrong box —
 * it destroys part of the design. A stylised logo read as "© 'our" arrives at 0.76 and would hide the
 * creator's own mark behind three wrong characters. Below this line the honest move is to leave the
 * artwork alone: the printed pixels are still right, and Add field is one click away.
 *
 * The engine still *reports* weaker regions; this only governs what gets covered automatically.
 */
const MIN_CONFIDENCE = 0.8;

/** A region smaller than this is not clickable at any sane zoom. */
const MIN_WIDTH_PERCENT = 1.5;
const MIN_HEIGHT_PERCENT = 0.8;

export interface AutoLayoutResult {
  fields: DraftField[];
  /** How many became fill-per-recipient fields, for the "detected N fields" confirmation. */
  dynamicCount: number;
  /** Canonical keys recognised, in page order, so the confirmation can name them. */
  dynamicKeys: string[];
}

/**
 * Builds elements from a detection pass.
 *
 * Every region becomes an element that covers what it sits on. Ones whose words name a known concept —
 * "Recipient's Full Name", "Date of Issue", or even a sample like "John Doe" — become fields that fill
 * per recipient; the rest stay as fixed text the creator can retype.
 */
export function autoLayout(detection: TextDetection, pageHeightPt = 842): AutoLayoutResult {
  if (!detection.available) return { fields: [], dynamicCount: 0, dynamicKeys: [] };

  const fields: DraftField[] = [];
  const dynamicKeys: string[] = [];

  const usable = detection.regions.filter((region) => {
    const text = region.text.trim();
    return text.length >= MIN_TEXT_LENGTH
      && region.confidence >= MIN_CONFIDENCE
      && region.width >= MIN_WIDTH_PERCENT
      && region.height >= MIN_HEIGHT_PERCENT;
  });

  const announced = namesAnnouncedByALeadIn(usable);

  usable.forEach((region, index) => {
    const text = region.text.trim();
    const key = matchCanonicalField(text) ?? (announced.has(region) ? "participant_name" : null);

    const base = key
      ? newField("dynamicfield", { fieldKey: key, label: fieldLabel(key) })
      : newField("text");

    fields.push({
      ...base,
      static_text: key ? null : text,
      x: clamp(region.x, 0, 100),
      y: clamp(region.y, 0, 100),
      width: clamp(region.width, 1, 100 - clamp(region.x, 0, 100)),
      height: clamp(region.height, 1, 100 - clamp(region.y, 0, 100)),
      // Size the type to ONE LINE of the box the engine measured, so the replacement reads like the
      // original. Dividing by the line count is what keeps a merged paragraph from rendering its first
      // few words at heading size.
      font_size_pt: Math.round(clamp(
        (region.height / Math.max(1, region.lines ?? 1) / 100) * pageHeightPt * 0.72, 6, 120)),
      // A multi-line block needs its leading set, or the wrapped lines sit on top of each other.
      line_height: (region.lines ?? 1) > 1 ? 1.35 : null,
      // A field that REPLACES what is printed has to cover it, or the creator sees their new text and the
      // original characters underneath — the duplication detection exists to remove. Text that is merely
      // being made clickable covers nothing: the artwork already says it, and painting a flat rectangle
      // over unchanged words erases the design's watermark and texture, leaving the smooth hard-edged
      // patch that reads as "this was edited". It starts covering the moment it is actually edited.
      is_masking: key !== null,
      mirrors_artwork: key === null,
      // Sampled now and carried either way, ready for the edit that may never come.
      background_color: region.ground ?? "#FFFFFF",
      // Page order, so the panel lists them the way the certificate reads.
      z_order: index,
      horizontal_alignment: "center",
      vertical_alignment: "middle",
    });

    if (key && !dynamicKeys.includes(key)) dynamicKeys.push(key);
  });

  return { fields, dynamicCount: fields.filter((f) => f.kind === "dynamicfield").length, dynamicKeys };
}

function clamp(value: number, min: number, max: number): number {
  return Math.min(max, Math.max(min, value));
}

/**
 * The line a lead-in phrase points at.
 *
 * "Presented to" does not name the recipient — it says the recipient is written directly beneath it. A
 * design carrying `Presented to / OLIVER SMITH` gives us nothing to match `OLIVER SMITH` against; no
 * vocabulary contains it, and none should, because tomorrow it is somebody else. The certificate's own
 * layout is the only thing that identifies it, and it identifies it plainly.
 *
 * Deliberately strict. The line must sit directly below, overlap it horizontally, be close enough to be
 * the same thought, and not already be something else — because promoting the wrong line prints the
 * recipient's name over the course title on every certificate in the run.
 */
function namesAnnouncedByALeadIn(regions: TextDetection["regions"]): Set<TextDetection["regions"][number]> {
  const found = new Set<TextDetection["regions"][number]>();

  for (const lead of regions) {
    if (!leadsIntoName(lead.text.trim())) continue;

    const below = regions
      .filter((r) => r !== lead && r.y > lead.y)
      .filter((r) => Math.min(lead.x + lead.width, r.x + r.width) - Math.max(lead.x, r.x) > 0)
      // Within three of its own lines: a certificate puts the name right under the words announcing it,
      // and anything further down is a different part of the page.
      .filter((r) => r.y - (lead.y + lead.height) <= lead.height * 3)
      .sort((a, b) => a.y - b.y);

    const name = below[0];
    if (name && !matchCanonicalField(name.text.trim()) && !leadsIntoName(name.text.trim())) {
      found.add(name);
    }
  }
  return found;
}

/**
 * Folds a fresh detection into what is already on the design.
 *
 * Detection is an interpretation of the artwork, and re-running it produces a *new* interpretation of the
 * same words — not additional words. Appending was the obvious first implementation and it is wrong in the
 * commonest case there is: a creator who scans a design that has already been scanned ends up with two
 * elements on every line, the older one still covering the artwork with whatever it was built from. The
 * new interpretation is invisible underneath the old one, so every improvement to detection appears to
 * have done nothing at all.
 *
 * So a detected element **supersedes** anything already sitting where it sits. Overlap is the test rather
 * than provenance: an element covering these exact words is describing this exact text, whoever placed it,
 * and keeping both would show the words twice. Anything elsewhere on the page — a signature line, a hand
 * placed field, a QR — is untouched, which is what the original append was protecting.
 */
export function foldDetection(
  existing: DraftField[], detected: DraftField[], detection?: TextDetection
): DraftField[] {
  // Everywhere the engine READ, not merely everywhere it produced a block. The two differ exactly where
  // the new reading was too weak to act on — and that is the case that matters most, because the old
  // element sitting there came from a reading we would now refuse to make. Keeping it means a superseded
  // misreading survives its own replacement and goes on covering the artwork, which is what a stale
  // `© 'our` did to a logo. Declining a region means "leave the artwork alone here"; dropping what sits
  // on it is how that actually happens.
  const read = (detection?.regions ?? []).map((r) => box(r.x, r.y, r.width, r.height));
  const fresh = detected.map((d) => box(d.x, d.y, d.width, d.height));
  const superseded = [...read, ...fresh];

  const kept = existing.filter((field) => !superseded.some((s) => covers(s, field)));
  return [...kept, ...detected];
}

interface Box { x: number; y: number; width: number; height: number }

function box(x: number, y: number, width: number, height: number): Box {
  return { x, y, width, height };
}

/** How much of an existing element a new one must cover before it is describing the same text. Below this
 *  they are neighbours — certificate lines sit close together, and a shared edge is not a duplicate. */
const SUPERSEDE_OVERLAP = 0.6;

function covers(fresh: Box, old: DraftField): boolean {
  const w = Math.min(fresh.x + fresh.width, old.x + old.width) - Math.max(fresh.x, old.x);
  const h = Math.min(fresh.y + fresh.height, old.y + old.height) - Math.max(fresh.y, old.y);
  if (w <= 0 || h <= 0) return false;

  const area = old.width * old.height;
  return area > 0 && (w * h) / area >= SUPERSEDE_OVERLAP;
}
