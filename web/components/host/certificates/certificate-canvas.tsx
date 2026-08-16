"use client";

import { useEffect, useRef } from "react";
import type { DraftField } from "@/lib/certificate-editor";
import { PAGE_ASPECT } from "@/lib/certificate-editor";
import { fieldLabel, fieldSample } from "@/lib/certificate-fields";

/**
 * The certificate itself, as the editing surface (D-355).
 *
 * **The page is the interface.** A creator clicks the words they can see and types over them; they never
 * meet a coordinate, a bounding box or a layer. Everything technical — outlines, handles, field names —
 * appears only for the one element under the cursor and disappears the moment it is deselected, so an
 * untouched canvas is very nearly the finished document.
 *
 * **This is a layout surface, not the output.** The server is the only renderer, so the *type* here is an
 * approximation: the browser's fonts and line breaking, not QuestPDF's. The uploaded artwork is exact —
 * it is the same image the server paints. Preview says which is which rather than implying the whole
 * canvas is the final thing, because that is a lie discovered only after certificates are sent.
 *
 * Every coordinate is a percentage, which is what CSS wants, so positioning needs no scale factor.
 */
export function CertificateCanvas({
  pageSize, backgroundUrl, fields, width, selectedId, interactive = false, editingId,
  showEditable = false,
  onSelect, onPointerDown, onBackgroundClick, onTextChange, onEditDone, onStartEditing
}: {
  pageSize: string;
  backgroundUrl?: string | null;
  fields: DraftField[];
  width: number;
  selectedId?: string | null;
  interactive?: boolean;
  /** Outline every piece of text that can be changed, not only the one under the cursor.
   *
   *  While someone is *editing*, "which words can I click?" is the only question that matters, and a page
   *  that answers it solely on hover makes them sweep the mouse around hunting. The outlines come off for
   *  preview, where the question is "is this right?" and any mark that will not print is a lie. */
  showEditable?: boolean;
  /** The field currently being typed into. Only one at a time — this is a page, not a form. */
  editingId?: string | null;
  onSelect?: (id: string) => void;
  onPointerDown?: (e: React.PointerEvent, id: string, mode: "move" | "resize") => void;
  onBackgroundClick?: () => void;
  onTextChange?: (id: string, text: string) => void;
  onEditDone?: () => void;
  onStartEditing?: (id: string) => void;
}) {
  const aspect = PAGE_ASPECT[pageSize] ?? PAGE_ASPECT["a4-landscape"];
  const height = Math.round(width / aspect);

  return (
    <div
      data-testid="certificate-canvas"
      onClick={onBackgroundClick}
      // Paper, not a debugging surface: a soft shadow and a hairline, on a neutral workspace.
      className="relative shrink-0 overflow-hidden rounded-lg bg-white shadow-[0_8px_30px_rgba(15,23,42,.14)] ring-1 ring-black/5"
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
            No design uploaded yet. Upload your certificate — it becomes the page, and everything you add
            sits on top of it.
          </p>
        </div>
      )}

      {[...fields].sort((a, b) => a.z_order - b.z_order).map((field) => (
        <FieldBox
          key={field.id}
          field={field}
          canvasWidth={width}
          selected={field.id === selectedId}
          editing={field.id === editingId}
          interactive={interactive}
          showEditable={showEditable}
          onSelect={onSelect}
          onPointerDown={onPointerDown}
          onTextChange={onTextChange}
          onEditDone={onEditDone}
          onStartEditing={onStartEditing}
        />
      ))}
    </div>
  );
}

function FieldBox({
  field, canvasWidth, selected, editing, interactive, showEditable,
  onSelect, onPointerDown, onTextChange, onEditDone, onStartEditing
}: {
  field: DraftField;
  canvasWidth: number;
  selected: boolean;
  editing: boolean;
  interactive: boolean;
  showEditable: boolean;
  onSelect?: (id: string) => void;
  onPointerDown?: (e: React.PointerEvent, id: string, mode: "move" | "resize") => void;
  onTextChange?: (id: string, text: string) => void;
  onEditDone?: () => void;
  onStartEditing?: (id: string) => void;
}) {
  const editable = useRef<HTMLDivElement | null>(null);
  const isText = field.kind === "text" || field.kind === "dynamicfield";
  // Only FIXED text is editable in place. A dynamic field shows a sample of what will be filled in per
  // recipient; typing over that sample would quietly break the binding and print one person's name on
  // every certificate. Changing which field it is happens in the panel, where it is a deliberate choice.
  const editable_kind = field.kind === "text";

  // Every changeable piece of text is outlined while editing. Mirrored text especially: it draws nothing
  // of its own, so without a mark there is no way to tell the words under the cursor are reachable at all.
  const editableHint = interactive && showEditable && isText && !selected && !editing;

  // Put the caret in the text the moment editing starts, rather than making the creator click twice.
  useEffect(() => {
    if (!editing || !editable.current) return;
    const node = editable.current;
    node.focus();
    const range = document.createRange();
    range.selectNodeContents(node);
    const selection = window.getSelection();
    selection?.removeAllRanges();
    selection?.addRange(range);
  }, [editing]);

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
    fontStyle: field.font_style === "italic" ? "italic" : undefined,
    textDecoration: field.underline ? "underline" : undefined,
    letterSpacing: field.letter_spacing ? `${field.letter_spacing}em` : undefined,
    // Point sizes are against the real page; scaling by the canvas width keeps relative type honest even
    // though the absolute size here means nothing.
    fontSize: field.font_size_pt ? `${(field.font_size_pt / 842) * canvasWidth}px` : undefined,
    lineHeight: field.line_height ?? 1.15,
    // A masking field's fill is load-bearing, not decoration: it is what hides text printed into the
    // uploaded artwork beneath. A mirroring one paints nothing — the artwork below IS its appearance, and
    // filling over it here would show the creator a patch the certificate will not have.
    backgroundColor: field.mirrors_artwork ? undefined : (field.background_color ?? undefined),
    // Chrome ONLY when this element is the one being worked on. An unselected canvas is the certificate.
    outline: editing ? "2px solid #2563EB"
      : selected ? "1.5px solid #2563EB"
      : editableHint ? "2px dashed rgba(37,99,235,.45)"
      : interactive ? "1px solid transparent"
      : undefined,
    outlineOffset: selected || editing ? 2 : editableHint ? 1 : undefined,
    borderRadius: editableHint || selected || editing ? 3 : undefined,
    cursor: interactive ? (editing ? "text" : "move") : "default",
    userSelect: editing ? "text" : "none",
    overflow: "hidden",
  };

  // What the page actually says. A dynamic field shows realistic sample text rather than {a_token},
  // because the creator is judging a certificate, not reading a schema.
  //
  // A mirroring element draws nothing at all until it is being edited. The uploaded design is already
  // showing these exact words underneath; drawing them again in the browser's font would double them on
  // the canvas and misrepresent a certificate that will carry the artwork's own type. It is still a full
  // element — clickable, selectable, listed — just an invisible one.
  const mirroring = field.mirrors_artwork && !editing;
  const shown =
    mirroring ? ""
    : field.kind === "text" ? (field.static_text ?? "")
    : field.kind === "dynamicfield" ? fieldSample(field.field_key, field.label)
    : "";

  const body = isText ? (
    <div
      ref={editable}
      contentEditable={editing && editable_kind}
      suppressContentEditableWarning
      className="w-full px-1 outline-none"
      style={{ whiteSpace: "pre-wrap", wordBreak: "break-word" }}
      onBlur={(e) => {
        if (!editing) return;
        onTextChange?.(field.id, e.currentTarget.textContent ?? "");
        onEditDone?.();
      }}
      onKeyDown={(e) => {
        if (!editing) return;
        e.stopPropagation();
        // Enter commits; Shift+Enter is a deliberate line break, which certificates genuinely use.
        if (e.key === "Enter" && !e.shiftKey) { e.preventDefault(); e.currentTarget.blur(); }
        if (e.key === "Escape") { e.preventDefault(); onEditDone?.(); }
      }}
    >
      {shown}
    </div>
  ) : field.kind === "qrcode" ? (
    <span style={{ fontSize: 9, letterSpacing: 1 }}>QR</span>
  ) : (
    <span style={{ fontSize: 9 }}>IMAGE</span>
  );

  // Placeholders for the two kinds that have nothing to draw until generation, shown only while editing
  // so they do not appear on a quiet canvas.
  const chrome =
    !interactive ? {}
    : field.kind === "qrcode" ? { background: field.background_color ?? "rgba(15,23,42,.06)" }
    : field.kind === "image" ? { background: field.background_color ?? "rgba(100,116,139,.10)" }
    : {};

  return (
    <div
      role="button"
      tabIndex={interactive ? 0 : -1}
      aria-label={`${fieldLabel(field.field_key, field.label ?? field.static_text)} field`}
      aria-pressed={selected}
      onClick={(e) => { e.stopPropagation(); onSelect?.(field.id); }}
      // Click to select, click again — or double-click — to type. Two gestures for the same thing,
      // because "click the text and change it" is the one interaction that has to be discoverable.
      onDoubleClick={(e) => { e.stopPropagation(); if (interactive && editable_kind) onStartEditing?.(field.id); }}
      onPointerDown={(e) => { if (interactive && !editing) onPointerDown?.(e, field.id, "move"); }}
      style={{ ...style, ...chrome }}
    >
      {body}

      {/* Says "you can type here" in the one symbol everyone already reads that way. Only while editing
          is being done, and never on the preview, because it does not print. */}
      {editableHint ? (
        <span
          aria-hidden
          style={{
            position: "absolute", right: -9, top: -9,
            width: 18, height: 18, borderRadius: 9,
            background: "#2563EB", color: "white",
            fontSize: 10, lineHeight: "18px", textAlign: "center",
            fontFamily: "system-ui, sans-serif", fontStyle: "normal", textDecoration: "none",
            letterSpacing: 0, fontWeight: 700,
          }}
        >
          ✎
        </span>
      ) : null}

      {/* The field's name, shown beside it only while it is selected — never printed onto the page. */}
      {selected && interactive ? (
        <span
          aria-hidden
          style={{
            position: "absolute", left: 0, top: -18, whiteSpace: "nowrap",
            fontSize: 11, lineHeight: "16px", padding: "0 6px", borderRadius: 4,
            background: "#2563EB", color: "white", fontWeight: 600, letterSpacing: 0,
            fontFamily: "system-ui, sans-serif", fontStyle: "normal", textDecoration: "none",
          }}
        >
          {field.kind === "dynamicfield"
            ? fieldLabel(field.field_key, field.label)
            : field.kind === "text" ? "Text" : field.kind === "qrcode" ? "Verification QR" : "Image"}
        </span>
      ) : null}

      {selected && interactive && !editing ? (
        // Its own pointer target rather than an edge hit-test: an edge that is both "drag me" and
        // "resize me" depending on a few pixels is a coin flip at every grab.
        <span
          role="button"
          aria-label="Resize"
          onPointerDown={(e) => { e.stopPropagation(); onPointerDown?.(e, field.id, "resize"); }}
          style={{
            position: "absolute", right: -6, bottom: -6, width: 12, height: 12,
            borderRadius: 3, background: "#2563EB", border: "2px solid white", cursor: "nwse-resize",
          }}
        />
      ) : null}
    </div>
  );
}
