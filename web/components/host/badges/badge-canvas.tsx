"use client";

import type React from "react";
import type { BadgeField, BadgeFieldKey } from "@/lib/badge-api";

/**
 * The ID card itself, as the editing surface (D-362).
 *
 * **The card is the interface.** Fields are dragged and resized on the card, never through coordinate
 * inputs. Everything technical — outlines, handles, names — shows only for the element under the cursor.
 *
 * **This is a layout surface, not the output.** The server is the only renderer, so the type here is an
 * approximation: the browser's fonts, not QuestPDF's. The uploaded artwork is exact — the same image the
 * server paints. The Preview panel beside it is the truth, which is why both exist.
 *
 * Deliberately a sibling of `certificate-canvas.tsx` rather than an import: that file is being rewritten
 * in another branch, and its `pageWidthMm`/`pageHeightMm` support — which is what a badge needs — is not
 * committed yet. **Merge the two once it lands**; there is no reason for this codebase to own two canvases.
 */

export const FIELD_LABELS: Record<BadgeFieldKey, string> = {
  photo: "Photo",
  logo: "Logo",
  qr: "QR code",
  holder_name: "Name",
  subtitle: "Role / tier",
  access_level: "Access level",
  event_name: "Event name",
  event_date: "Event date",
  card_number: "Card number",
  accent_band: "Colour band"
};

/** What each field shows on the canvas. The server substitutes real values at render time. */
const FIELD_SAMPLES: Record<BadgeFieldKey, string> = {
  photo: "Photo",
  logo: "Logo",
  qr: "QR",
  holder_name: "Priya Raghunathan",
  subtitle: "General Admission",
  access_level: "ALL ACCESS",
  event_name: "Sample Hackathon 2026",
  event_date: "01 Oct 2026",
  card_number: "KRX-00001",
  accent_band: ""
};

export function BadgeCanvas({
  fields, width, height, backgroundUrl, selectedKey, onSelect, onPointerDown, onBackgroundClick
}: {
  fields: BadgeField[];
  width: number;
  height: number;
  backgroundUrl?: string | null;
  selectedKey?: BadgeFieldKey | null;
  onSelect?: (key: BadgeFieldKey) => void;
  onPointerDown?: (e: React.PointerEvent, key: BadgeFieldKey, mode: "move" | "resize") => void;
  onBackgroundClick?: () => void;
}) {
  return (
    <div
      data-testid="badge-canvas"
      onClick={onBackgroundClick}
      className="relative shrink-0 overflow-hidden rounded-lg bg-white shadow-[0_8px_30px_rgba(15,23,42,.14)] ring-1 ring-black/5"
      style={{ width, height, touchAction: "none" }}
    >
      {backgroundUrl && (
        // `contain` so the whole uploaded design is visible: the artwork is the one thing the organiser
        // actually made, and silently cropping its edges damages it.
        // eslint-disable-next-line @next/next/no-img-element
        <img
          src={backgroundUrl}
          alt="" aria-hidden
          draggable={false}
          className="pointer-events-none absolute inset-0 h-full w-full select-none"
          style={{ objectFit: "contain" }}
        />
      )}

      {[...fields]
        .filter((f) => f.enabled)
        .sort((a, b) => a.zOrder - b.zOrder)
        .map((field) => (
          <FieldBox
            key={field.key}
            field={field}
            selected={field.key === selectedKey}
            onSelect={onSelect}
            onPointerDown={onPointerDown}
          />
        ))}
    </div>
  );
}

function FieldBox({
  field, selected, onSelect, onPointerDown
}: {
  field: BadgeField;
  selected: boolean;
  onSelect?: (key: BadgeFieldKey) => void;
  onPointerDown?: (e: React.PointerEvent, key: BadgeFieldKey, mode: "move" | "resize") => void;
}) {
  const isBand = field.key === "accent_band";
  const isImage = field.key === "photo" || field.key === "logo" || field.key === "qr";

  return (
    <div
      role="button"
      tabIndex={0}
      aria-label={FIELD_LABELS[field.key]}
      onClick={(e) => {
        e.stopPropagation();
        onSelect?.(field.key);
      }}
      onKeyDown={(e) => {
        if (e.key === "Enter" || e.key === " ") {
          e.preventDefault();
          onSelect?.(field.key);
        }
      }}
      onPointerDown={(e) => onPointerDown?.(e, field.key, "move")}
      className={`absolute flex cursor-move items-center overflow-hidden ${
        selected ? "outline outline-2 outline-offset-1 outline-accent" : "hover:outline hover:outline-1 hover:outline-border-strong"
      }`}
      style={{
        left: `${field.x}%`,
        top: `${field.y}%`,
        width: `${field.width}%`,
        height: `${field.height}%`,
        backgroundColor: isBand ? field.color ?? "#111827" : undefined,
        justifyContent:
          field.align === "left" ? "flex-start" : field.align === "right" ? "flex-end" : "center"
      }}
    >
      {isImage ? (
        <div className="grid h-full w-full place-items-center border border-dashed border-border-strong bg-surface-2 text-[10px] text-muted">
          {FIELD_SAMPLES[field.key]}
        </div>
      ) : (
        !isBand && (
          <span
            className="w-full truncate px-1"
            style={{
              color: field.color ?? "#111827",
              fontWeight: field.weight === "bold" ? 700 : 400,
              // Approximate only — the server's type is the real one. Scaled from the point size so
              // relative sizes on the canvas match relative sizes on the card.
              fontSize: `${Math.max(7, (field.fontSizePt ?? 10) * 1.1)}px`,
              textAlign: field.align as React.CSSProperties["textAlign"]
            }}
          >
            {FIELD_SAMPLES[field.key]}
          </span>
        )
      )}

      {selected && (
        <span
          role="presentation"
          onPointerDown={(e) => {
            e.stopPropagation();
            onPointerDown?.(e, field.key, "resize");
          }}
          className="absolute -bottom-1 -right-1 h-3 w-3 cursor-se-resize rounded-sm border border-white bg-accent"
        />
      )}
    </div>
  );
}
