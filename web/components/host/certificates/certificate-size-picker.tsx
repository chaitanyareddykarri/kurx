"use client";

import { useMemo, useState } from "react";
import { Button } from "@kurx/ui";
import {
  CUSTOM_PAGE_MAX_MM, CUSTOM_PAGE_MIN_MM, validateCustomPage
} from "@/lib/certificate-editor";
import type { PageSizePreset } from "@/lib/certificate-api";

/**
 * Choosing the page a certificate prints onto (D-361).
 *
 * **Why this exists in an editor D-359 deliberately stripped.** Zoom, rotation and z-order were removed
 * because they are design preferences someone fixing a spelling should not have to meet. Page size is not
 * one: printing A5 artwork onto an A4 page produces a physically wrong certificate, and the person cannot
 * discover that until it comes off the printer.
 *
 * So it follows D-359's rules rather than being exempt from them. One control. Every option carries its
 * measurements, so the choice needs no outside knowledge of what "Legal" is. Orientation is two labelled
 * buttons, not a concept to infer. Errors say what to do — "Width needs to be at least 50 mm", never
 * `page_size_out_of_range`.
 */
export function CertificateSizePicker({
  presets, pageSize, pageWidthMm, pageHeightMm, disabled, warnArtworkWillRescale, onChange
}: {
  presets: PageSizePreset[];
  pageSize: string;
  pageWidthMm: number;
  pageHeightMm: number;
  disabled?: boolean;
  /** Set once artwork has been uploaded. Changing the page then re-shapes a picture that was made for
   *  the old one — the single consequence that is not obvious from the control itself, so it is stated
   *  rather than left to be discovered on a printed certificate. */
  warnArtworkWillRescale?: boolean;
  onChange: (next: { pageSize: string; pageWidthMm?: number; pageHeightMm?: number }) => void;
}) {
  const isCustom = pageSize === "custom";
  // Falls back to A4 landscape when a stale payload carries no measurements, so the boxes start at a
  // real page rather than "NaN".
  const safeW = Number.isFinite(pageWidthMm) ? pageWidthMm : 297;
  const safeH = Number.isFinite(pageHeightMm) ? pageHeightMm : 210;
  const [customW, setCustomW] = useState(String(Math.round(safeW)));
  const [customH, setCustomH] = useState(String(Math.round(safeH)));
  const [customError, setCustomError] = useState<string | null>(null);

  // One entry per paper; orientation is chosen separately. Fourteen flat options would make the reader
  // do the grouping in their head.
  const families = useMemo(() => {
    const seen = new Map<string, { family: string; label: string }>();
    for (const p of presets) if (!seen.has(p.family)) seen.set(p.family, { family: p.family, label: p.label });
    return [...seen.values()];
  }, [presets]);

  const current = presets.find((p) => p.slug === pageSize);
  const family = current?.family ?? (isCustom ? "custom" : "a4");
  const landscape = isCustom ? safeW >= safeH : (current?.landscape ?? true);

  function chooseFamily(nextFamily: string) {
    if (nextFamily === "custom") {
      // Seed the boxes from the page in hand, so switching to Custom starts where you were rather than
      // at an arbitrary default you have to overwrite.
      const w = Math.round(safeW);
      const h = Math.round(safeH);
      setCustomW(String(w));
      setCustomH(String(h));
      setCustomError(null);
      onChange({ pageSize: "custom", pageWidthMm: w, pageHeightMm: h });
      return;
    }
    const wanted = presets.find((p) => p.family === nextFamily && p.landscape === landscape)
      ?? presets.find((p) => p.family === nextFamily);
    if (wanted) onChange({ pageSize: wanted.slug });
  }

  function chooseOrientation(wantLandscape: boolean) {
    if (isCustom) {
      // Turning a custom page over is a swap, not a new size — the aspect ratio is preserved exactly,
      // which is what the request asks for when switching orientation.
      const w = Number(customW);
      const h = Number(customH);
      if (!Number.isFinite(w) || !Number.isFinite(h)) return;
      const [nw, nh] = wantLandscape === w >= h ? [w, h] : [h, w];
      setCustomW(String(nw));
      setCustomH(String(nh));
      onChange({ pageSize: "custom", pageWidthMm: nw, pageHeightMm: nh });
      return;
    }
    const wanted = presets.find((p) => p.family === family && p.landscape === wantLandscape);
    if (wanted) onChange({ pageSize: wanted.slug });
  }

  function commitCustom(nextW: string, nextH: string) {
    setCustomW(nextW);
    setCustomH(nextH);
    const w = Number(nextW);
    const h = Number(nextH);
    const problem = validateCustomPage(w, h);
    setCustomError(problem);
    // Only a valid page is pushed upward. A half-typed "5" must not momentarily resize the canvas to
    // 5 mm, and an invalid one must not be saveable.
    if (!problem) onChange({ pageSize: "custom", pageWidthMm: w, pageHeightMm: h });
  }

  return (
    <section className="rounded-xl border border-border bg-surface p-4">
      <h3 className="text-sm font-semibold text-text">Certificate size</h3>
      <p className="mt-1 text-xs text-muted">
        The paper this prints on. Pick the one your artwork was made for — everything you place is
        positioned against it.
      </p>

      {warnArtworkWillRescale ? (
        // Plain consequence, not a scare: what moves and what does not. Everything placed keeps its
        // position because coordinates are percentages of the page; the uploaded picture is stretched to
        // the new shape, which is the part nobody would guess.
        <p role="status" className="mt-2 rounded-md border border-warning/40 bg-warning/10 px-2 py-1.5 text-xs text-text">
          Changing this reshapes the picture you uploaded to fit the new paper. Everything you have placed
          keeps its position on the page.
        </p>
      ) : null}

      <div className="mt-3">
        <label className="text-xs font-medium text-muted" htmlFor="cert-page-family">Paper</label>
        <select
          id="cert-page-family"
          value={isCustom ? "custom" : family}
          disabled={disabled}
          onChange={(e) => chooseFamily(e.target.value)}
          className="mt-1 h-11 w-full rounded-md border border-border-strong bg-background px-3 text-sm text-text"
        >
          {families.map((f) => {
            // The measurements live on the option itself: nobody should need to know what Legal is.
            const portrait = presets.find((p) => p.family === f.family && !p.landscape);
            return (
              <option key={f.family} value={f.family}>
                {f.label}
                {portrait ? ` — ${format(portrait.width_mm)} × ${format(portrait.height_mm)} mm` : ""}
              </option>
            );
          })}
          <option value="custom">Custom size — set your own</option>
        </select>
      </div>

      <fieldset className="mt-3" disabled={disabled}>
        <legend className="text-xs font-medium text-muted">Orientation</legend>
        <div className="mt-1 flex gap-2">
          {([["Portrait", false], ["Landscape", true]] as const).map(([label, wantLandscape]) => (
            <button
              key={label}
              type="button"
              aria-pressed={landscape === wantLandscape}
              onClick={() => chooseOrientation(wantLandscape)}
              className={`h-11 flex-1 rounded-md border px-3 text-sm font-medium ${
                landscape === wantLandscape
                  ? "border-accent bg-accent/10 text-text"
                  : "border-border text-muted hover:bg-elevated hover:text-text"
              }`}
            >
              {label}
            </button>
          ))}
        </div>
      </fieldset>

      {isCustom ? (
        <div className="mt-3 grid grid-cols-2 gap-2">
          <label className="text-xs font-medium text-muted" htmlFor="cert-page-w">
            Width (mm)
            <input
              id="cert-page-w"
              type="number"
              inputMode="decimal"
              min={CUSTOM_PAGE_MIN_MM}
              max={CUSTOM_PAGE_MAX_MM}
              value={customW}
              disabled={disabled}
              onChange={(e) => commitCustom(e.target.value, customH)}
              className="mt-1 h-11 w-full rounded-md border border-border-strong bg-background px-3 text-sm text-text"
            />
          </label>
          <label className="text-xs font-medium text-muted" htmlFor="cert-page-h">
            Height (mm)
            <input
              id="cert-page-h"
              type="number"
              inputMode="decimal"
              min={CUSTOM_PAGE_MIN_MM}
              max={CUSTOM_PAGE_MAX_MM}
              value={customH}
              disabled={disabled}
              onChange={(e) => commitCustom(customW, e.target.value)}
              className="mt-1 h-11 w-full rounded-md border border-border-strong bg-background px-3 text-sm text-text"
            />
          </label>
          {customError ? (
            <p role="alert" className="col-span-2 text-xs text-danger">{customError}</p>
          ) : (
            <p className="col-span-2 text-xs text-muted">
              Between {CUSTOM_PAGE_MIN_MM} and {CUSTOM_PAGE_MAX_MM} mm on each side.
            </p>
          )}
        </div>
      ) : null}

      {/* Always state the page in hand, in both units — the artwork was probably specified in one of
          them, and converting in your head is exactly the kind of work D-359 removes. */}
      <p className="mt-3 text-xs text-muted">
        This certificate is <strong className="text-text">{format(pageWidthMm)} × {format(pageHeightMm)} mm</strong>
        {" "}({inches(pageWidthMm)} × {inches(pageHeightMm)} in).
      </p>
    </section>
  );
}

/** Millimetres without a trailing `.0` — A4 is "210", Letter is "215.9".
 *
 *  Guards against a missing measurement rather than trusting the schema: a payload cached before the
 *  dimensions travelled with the template would otherwise crash the whole editor on render, and a
 *  dash in one line of a summary is a far better failure than a blank screen. */
function format(mm: number | null | undefined): string {
  if (typeof mm !== "number" || !Number.isFinite(mm)) return "—";
  return Number.isInteger(mm) ? String(mm) : mm.toFixed(1);
}

function inches(mm: number | null | undefined): string {
  if (typeof mm !== "number" || !Number.isFinite(mm)) return "—";
  const value = mm / 25.4;
  return Number.isInteger(value) ? String(value) : value.toFixed(1);
}
