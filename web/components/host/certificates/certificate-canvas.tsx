"use client";

import type { DraftField } from "@/lib/certificate-editor";
import { PAGE_ASPECT } from "@/lib/certificate-editor";

/**
 * The uploaded design with its fields drawn on top (D-344, Phase 3).
 *
 * **This is a layout surface, not the output.** The server is the only renderer, so the *type* here is an
 * approximation — the browser's fonts and line breaking, not QuestPDF's. The uploaded artwork, however,
 * is exact: it is the same image the server paints. The editor says which is which rather than implying
 * the whole canvas is a preview, because that is a lie discovered only after certificates are sent.
 *
 * Every coordinate in a field is a percentage, which is exactly what CSS wants — so positioning is a
 * direct translation with no scale factor to get wrong.
 */
export function CertificateCanvas({
  pageSize, backgroundUrl, fields, width, selectedId, interactive = false,
  onSelect, onPointerDown, onBackgroundClick
}: {
  pageSize: string;
  backgroundUrl?: string | null;
  fields: DraftField[];
  width: number;
  selectedId?: string | null;
  interactive?: boolean;
  onSelect?: (id: string) => void;
  onPointerDown?: (e: React.PointerEvent, id: string, mode: "move" | "resize") => void;
  onBackgroundClick?: () => void;
}) {
  const aspect = PAGE_ASPECT[pageSize] ?? PAGE_ASPECT["a4-landscape"];
  const height = Math.round(width / aspect);

  return (
    <div
      data-testid="certificate-canvas"
      onClick={onBackgroundClick}
      className="relative shrink-0 overflow-hidden rounded-md border border-border bg-white shadow-sm"
      style={{ width, height, touchAction: "none" }}
    >
      {backgroundUrl ? (
        // `contain` so the whole uploaded design is visible and nothing is cropped: the artwork is the
        // one thing the creator actually made, and silently trimming its edges damages it.
        // eslint-disable-next-line @next/next/no-img-element
        <img
          src={backgroundUrl}
          alt="" aria-hidden
          draggable={false}
          className="pointer-events-none absolute inset-0 h-full w-full select-none"
          style={{ objectFit: "contain" }}
        />
      ) : (
        <div className="absolute inset-0 grid place-items-center px-6 text-center">
          <p className="text-xs text-muted">
            No design uploaded yet. Upload your certificate artwork — it becomes the page, and everything
            you add sits on top of it.
          </p>
        </div>
      )}

      {[...fields].sort((a, b) => a.z_order - b.z_order).map((field) => (
        <FieldBox
          key={field.id}
          field={field}
          canvasWidth={width}
          selected={field.id === selectedId}
          interactive={interactive}
          onSelect={onSelect}
          onPointerDown={onPointerDown}
        />
      ))}
    </div>
  );
}

function FieldBox({ field, canvasWidth, selected, interactive, onSelect, onPointerDown }: {
  field: DraftField;
  canvasWidth: number;
  selected: boolean;
  interactive: boolean;
  onSelect?: (id: string) => void;
  onPointerDown?: (e: React.PointerEvent, id: string, mode: "move" | "resize") => void;
}) {
  const style: React.CSSProperties = {
    position: "absolute",
    left: `${field.x}%`,
    top: `${field.y}%`,
    width: `${field.width}%`,
    height: `${field.height}%`,
    transform: field.rotation ? `rotate(${field.rotation}deg)` : undefined,
    display: "flex",
    alignItems: field.vertical_alignment === "top" ? "flex-start"
      : field.vertical_alignment === "bottom" ? "flex-end" : "center",
    justifyContent: field.horizontal_alignment === "right" ? "flex-end"
      : field.horizontal_alignment === "center" ? "center" : "flex-start",
    textAlign: field.horizontal_alignment as React.CSSProperties["textAlign"],
    color: field.color ?? "#0F172A",
    fontFamily: field.font_family === "serif" ? "Georgia, serif"
      : field.font_family === "mono" ? "ui-monospace, monospace" : undefined,
    fontWeight: field.font_weight === "bold" ? 700 : undefined,
    // Point sizes are against the real page; scaling by the canvas width keeps relative type honest even
    // though the absolute size here means nothing.
    fontSize: field.font_size_pt ? `${(field.font_size_pt / 842) * canvasWidth}px` : undefined,
    lineHeight: 1.15,
    // A masking field's fill is load-bearing, not decoration: it is what hides the printed text beneath.
    backgroundColor: field.background_color ?? undefined,
    outline: selected ? "2px solid #2563EB"
      : field.kind === "dynamicfield" ? "1px dashed rgba(100,116,139,.55)" : undefined,
    cursor: interactive ? "move" : "default",
    userSelect: "none",
    overflow: "hidden"
  };

  const body =
    field.kind === "text" ? <span className="truncate px-1">{field.static_text}</span>
    : field.kind === "dynamicfield"
      ? <span className="truncate px-1 opacity-80">{`[${field.label || field.field_key}]`}</span>
    : field.kind === "qrcode" ? <span style={{ fontSize: 9, letterSpacing: 1 }}>QR</span>
    : <span style={{ fontSize: 9 }}>IMAGE</span>;

  const chrome =
    field.kind === "qrcode" ? { background: field.background_color ?? "rgba(15,23,42,.08)" }
    : field.kind === "image" ? { background: field.background_color ?? "rgba(100,116,139,.12)" }
    : {};

  return (
    <div
      role="button"
      tabIndex={interactive ? 0 : -1}
      aria-label={`${field.kind} field ${field.label ?? field.field_key ?? field.static_text ?? field.id}`}
      aria-pressed={selected}
      onClick={(e) => { e.stopPropagation(); onSelect?.(field.id); }}
      onPointerDown={(e) => interactive && onPointerDown?.(e, field.id, "move")}
      style={{ ...style, ...chrome }}
    >
      {body}
      {selected && interactive ? (
        // Its own pointer target rather than an edge hit-test: an edge that is both "drag me" and
        // "resize me" depending on a few pixels is a coin flip at every grab.
        <span
          role="button"
          aria-label="Resize field"
          onPointerDown={(e) => { e.stopPropagation(); onPointerDown?.(e, field.id, "resize"); }}
          style={{
            position: "absolute", right: -6, bottom: -6, width: 12, height: 12,
            borderRadius: 2, background: "#2563EB", cursor: "nwse-resize"
          }}
        />
      ) : null}
    </div>
  );
}
