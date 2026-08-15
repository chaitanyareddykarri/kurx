import { z } from "zod";

/**
 * The OCR boundary's shape, on the client (D-355, Phase 12).
 *
 * Its own module, with no dependency on `lib/api.ts`, for the same reason `lib/certificate-editor.ts` is
 * separate: this is a contract, and a contract should be testable without booting the API client that
 * happens to carry it over the wire.
 *
 * OCR is deferred by decision. No build registers an engine, so `available` is always false today — and
 * an empty `regions` with `available: false` is NOT a claim that the design has no text on it. That
 * distinction is the point of the flag: one is a fact about the deployment, the other about the design.
 */

export const textDetectionSchema = z.object({
  available: z.boolean(),
  /** Coordinates are percentages of the page, 0–100, matching the editor's own field coordinates — so a
   *  detected region maps straight onto a field without the editor learning anything about image
   *  dimensions. Confidence is normalised to 0–1 across engines. */
  regions: z.array(z.object({
    text: z.string(),
    x: z.number(),
    y: z.number(),
    width: z.number(),
    height: z.number(),
    confidence: z.number(),
  })),
  reason: z.string().nullable(),
});

export type TextDetection = z.infer<typeof textDetectionSchema>;

/** What the editor asks. The only thing availability controls: everything else — placing, moving,
 *  saving — never consults it. */
export function offersAssistance(detection: TextDetection): boolean {
  return detection.available;
}

/** The answer a missing, disabled or broken detector produces. A detector that is any of those must cost
 *  the editor nothing, so every failure collapses to this rather than surfacing as an error. */
export const DETECTION_UNAVAILABLE: TextDetection = { available: false, regions: [], reason: null };
