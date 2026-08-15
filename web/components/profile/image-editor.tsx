"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { Button } from "@/components/ui/button";
import {
  ASPECT, BRIGHTNESS_RANGE, CONTRAST_RANGE, DEFAULT_EDITS, ZOOM_RANGE,
  clampOffsets, coverScale, filterString, isDefaultEdits, normaliseRotation, outputSize,
  type Edits, type Slot
} from "@/lib/image-edit";

/**
 * Crop / zoom / reposition / rotate / brightness / contrast, on a canvas, before anything is uploaded.
 *
 * The editor never touches the network: it hands a `Blob` to its parent and lets the parent decide what
 * an upload means. That split is what makes Cancel honest — nothing has been sent, so there is nothing
 * to undo, and the previously saved image is untouched by definition.
 *
 * Drawing goes through `drawTo`, used for BOTH the live preview and the saved render, so the two cannot
 * drift. The only difference between them is the canvas they target.
 */
export function ImageEditor({ slot, file, onSave, onCancel, saving }: {
  slot: Slot;
  file: File;
  onSave: (blob: Blob) => void | Promise<void>;
  onCancel: () => void;
  saving?: boolean;
}) {
  const [edits, setEdits] = useState<Edits>(DEFAULT_EDITS);
  const [image, setImage] = useState<HTMLImageElement | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const previewRef = useRef<HTMLCanvasElement | null>(null);
  const dragRef = useRef<{ x: number; y: number; ox: number; oy: number } | null>(null);

  const aspect = ASPECT[slot];
  // The preview is a fixed CSS-pixel box; the saved render is `outputSize`. Both are the same crop —
  // only the resolution differs — so the preview frame's dimensions drive the pan clamp too.
  const frameW = 288;
  const frameH = Math.round(frameW / aspect);

  useEffect(() => {
    const url = URL.createObjectURL(file);
    const img = new Image();
    img.onload = () => setImage(img);
    img.onerror = () => setLoadError("That image couldn't be opened. It may be corrupt — try another file.");
    img.src = url;
    return () => URL.revokeObjectURL(url);
  }, [file]);

  const drawTo = useCallback((canvas: HTMLCanvasElement, w: number, h: number) => {
    const ctx = canvas.getContext("2d");
    // jsdom has no 2D context. Bailing keeps the component mountable in tests, where the controls and
    // their wiring are what matter; pixels are covered by the geometry unit tests instead.
    if (!ctx || !image) return;
    canvas.width = w;
    canvas.height = h;
    ctx.clearRect(0, 0, w, h);
    ctx.save();
    ctx.filter = filterString(edits);
    ctx.translate(w / 2 + edits.offsetX * (w / frameW), h / 2 + edits.offsetY * (h / frameH));
    ctx.rotate((edits.rotation * Math.PI) / 180);
    const scale = coverScale(image.width, image.height, w, h, edits.rotation) * edits.zoom;
    ctx.drawImage(
      image,
      (-image.width * scale) / 2,
      (-image.height * scale) / 2,
      image.width * scale,
      image.height * scale
    );
    ctx.restore();
  }, [image, edits, frameW, frameH]);

  useEffect(() => {
    if (previewRef.current) drawTo(previewRef.current, frameW, frameH);
  }, [drawTo, frameW, frameH]);

  // Zoom and rotation both change how much image there is to pan across, so a previously legal offset
  // can end up outside the frame. Re-clamping here rather than at each control keeps the invariant in
  // one place: whatever the controls do, the offset is always inside `panBounds` afterwards.
  const update = useCallback((patch: Partial<Edits>) => {
    setEdits((prev) => {
      const next = { ...prev, ...patch };
      if (!image) return next;
      return { ...next, ...clampOffsets(image.width, image.height, frameW, frameH, next) };
    });
  }, [image, frameW, frameH]);

  function onPointerDown(e: React.PointerEvent<HTMLCanvasElement>) {
    if (!image) return;
    e.currentTarget.setPointerCapture(e.pointerId);
    dragRef.current = { x: e.clientX, y: e.clientY, ox: edits.offsetX, oy: edits.offsetY };
  }
  function onPointerMove(e: React.PointerEvent<HTMLCanvasElement>) {
    const d = dragRef.current;
    if (!d) return;
    update({ offsetX: d.ox + (e.clientX - d.x), offsetY: d.oy + (e.clientY - d.y) });
  }
  function onPointerUp(e: React.PointerEvent<HTMLCanvasElement>) {
    dragRef.current = null;
    if (e.currentTarget.hasPointerCapture(e.pointerId)) e.currentTarget.releasePointerCapture(e.pointerId);
  }

  // Arrow keys pan too — a drag-only editor is unreachable by keyboard, and the crop is the whole
  // point of the screen.
  function onKeyDown(e: React.KeyboardEvent<HTMLCanvasElement>) {
    const step = e.shiftKey ? 20 : 4;
    const moves: Record<string, Partial<Edits>> = {
      ArrowLeft: { offsetX: edits.offsetX - step },
      ArrowRight: { offsetX: edits.offsetX + step },
      ArrowUp: { offsetY: edits.offsetY - step },
      ArrowDown: { offsetY: edits.offsetY + step }
    };
    const move = moves[e.key];
    if (!move) return;
    e.preventDefault();
    update(move);
  }

  async function handleSave() {
    const canvas = document.createElement("canvas");
    const { width, height } = outputSize(slot);
    drawTo(canvas, width, height);
    const blob = await new Promise<Blob | null>((resolve) => {
      if (typeof canvas.toBlob !== "function") return resolve(null);
      // WebP at 0.9 is materially smaller than JPEG at visually equal quality, and every browser that
      // can run this editor can encode it. PNG would be lossless and several times the size for a photo.
      canvas.toBlob((b) => resolve(b), "image/webp", 0.9);
    });
    if (!blob) {
      setLoadError("This browser couldn't process the image. Try a different browser.");
      return;
    }
    await onSave(blob);
  }

  if (loadError) {
    return (
      <div className="rounded-md border border-border bg-surface p-4">
        <p role="alert" className="text-sm text-danger">{loadError}</p>
        <div className="mt-3"><Button type="button" variant="secondary" onClick={onCancel}>Cancel</Button></div>
      </div>
    );
  }

  return (
    <div className="rounded-md border border-border bg-surface p-4" data-testid="image-editor">
      <p className="text-sm text-muted">
        Drag the image to reposition it, or use the arrow keys. The area inside the frame is what gets saved.
      </p>

      <div className="mt-3 flex flex-col gap-4 sm:flex-row sm:items-start">
        <canvas
          ref={previewRef}
          role="img"
          tabIndex={0}
          aria-label="Image preview — drag or use arrow keys to reposition"
          onPointerDown={onPointerDown}
          onPointerMove={onPointerMove}
          onPointerUp={onPointerUp}
          onPointerCancel={onPointerUp}
          onKeyDown={onKeyDown}
          style={{ width: frameW, height: frameH, maxWidth: "100%", touchAction: "none" }}
          className={`shrink-0 cursor-move border border-border bg-elevated focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent ${slot === "avatar" ? "rounded-full" : "rounded-md"}`}
        />

        <div className="min-w-0 flex-1 space-y-3">
          <Slider label="Zoom" value={edits.zoom} range={ZOOM_RANGE} suffix="×"
            onChange={(zoom) => update({ zoom })} />
          <Slider label="Brightness" value={edits.brightness} range={BRIGHTNESS_RANGE} percent
            onChange={(brightness) => update({ brightness })} />
          <Slider label="Contrast" value={edits.contrast} range={CONTRAST_RANGE} percent
            onChange={(contrast) => update({ contrast })} />

          <div className="flex flex-wrap items-center gap-2">
            <Button type="button" variant="secondary"
              onClick={() => update({ rotation: normaliseRotation(edits.rotation - 90) })}>
              Rotate left
            </Button>
            <Button type="button" variant="secondary"
              onClick={() => update({ rotation: normaliseRotation(edits.rotation + 90) })}>
              Rotate right
            </Button>
            <button
              type="button"
              className="rounded-md px-2 py-2 text-sm text-muted underline disabled:no-underline disabled:opacity-50"
              disabled={isDefaultEdits(edits)}
              onClick={() => setEdits(DEFAULT_EDITS)}
            >
              Reset edits
            </button>
          </div>
        </div>
      </div>

      <div className="mt-4 flex flex-wrap gap-2">
        {/* Disabled until the bitmap has actually decoded: saving before that would encode an empty
            canvas, which uploads successfully and leaves a blank avatar. */}
        <Button type="button" onClick={handleSave} disabled={!image || saving}>
          {saving ? "Saving…" : "Save image"}
        </Button>
        <Button type="button" variant="secondary" onClick={onCancel} disabled={saving}>Cancel</Button>
      </div>
    </div>
  );
}

function Slider({ label, value, range, onChange, suffix, percent }: {
  label: string;
  value: number;
  range: { min: number; max: number; step: number };
  onChange: (v: number) => void;
  suffix?: string;
  percent?: boolean;
}) {
  const id = `edit-${label.toLowerCase()}`;
  return (
    <div>
      <div className="flex items-center justify-between">
        <label htmlFor={id} className="text-sm font-medium text-text">{label}</label>
        <span className="text-xs tabular-nums text-muted">
          {percent ? `${Math.round(value * 100)}%` : `${value.toFixed(2)}${suffix ?? ""}`}
        </span>
      </div>
      <input
        id={id}
        type="range"
        className="mt-1 w-full"
        min={range.min}
        max={range.max}
        step={range.step}
        value={value}
        onChange={(e) => onChange(Number(e.target.value))}
      />
    </div>
  );
}
