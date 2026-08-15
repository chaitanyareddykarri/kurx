"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { Button } from "@kurx/ui";
import {
  detectTemplateTextAction, getArtworkColourAction, getArtworkMapAction, saveFieldsAction,
  updateTemplateAction
} from "@/lib/certificate-actions";
import { overlapsArtwork, type ArtworkMap } from "@/lib/certificate-artwork-map";
import { BackgroundUpload } from "@/components/host/certificates/background-upload";
import { CertificateCanvas } from "@/components/host/certificates/certificate-canvas";
import type { CertificateFieldKind, CertificateTemplate } from "@/lib/certificate-api";
import {
  FONT_FAMILIES, FONT_WEIGHTS, HORIZONTAL_ALIGNMENTS, PAGE_ASPECT, VERTICAL_ALIGNMENTS, ZOOM_STEPS,
  addField, canRedo, canUndo, commit, duplicateField, initHistory, moveField, newField, normaliseOrder,
  coverArtwork, nextFreeSlot, redo, removeField, reorderField, resizeField, rotateField, same, squareOnPage,
  toDraft, toInput, undo, updateField,
  type DraftField, type History
} from "@/lib/certificate-editor";

/**
 * The certificate editor (D-344, Phase 3).
 *
 * **Simple by default, advanced on demand.** The default surface is a list of details to add and a canvas
 * to place them on. Everything finer — exact coordinates, rotation, z-order, masking — lives behind
 * "Advanced", because the common case is "put the name here" and burying that under twenty controls is
 * how a tool stops being usable by the person who actually runs the event.
 *
 * **No OCR anywhere.** Every field is placed by the creator. Detection arrives later as a source of
 * suggestions, and this editor has to remain complete without it.
 *
 * Every mutation goes through the pure functions in `lib/certificate-editor` and lands in history via
 * `commit`, so undo is a stack of whole field lists rather than a stack of inverse operations.
 */
export function CertificateTemplateEditor({ template: initial, canManage }: {
  template: CertificateTemplate;
  canManage: boolean;
}) {
  const router = useRouter();
  const [template, setTemplate] = useState(initial);
  const [history, setHistory] = useState<History>(() => initHistory(initial.fields.map(toDraft)));
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [zoom, setZoom] = useState(1);
  const [advanced, setAdvanced] = useState(false);
  const [name, setName] = useState(initial.name);
  const [saving, setSaving] = useState(false);
  const [message, setMessage] = useState<{ ok: boolean; text: string } | null>(null);
  const [localBackground, setLocalBackground] = useState<string | null>(null);
  // The OCR extension point (D-344, Phase 12). No build registers an engine today, so this stays false
  // and nothing extra renders. Probed rather than assumed so that turning one on is a server-side change
  // with no edit here — which is the entire reason the boundary exists.
  const [canDetectText, setCanDetectText] = useState(false);
  // Where the uploaded design already has something printed. Fetched once per design and consulted
  // locally while dragging — see lib/certificate-artwork-map.ts for why it is not asked per move.
  const [artwork, setArtwork] = useState<ArtworkMap | null>(null);
  const [covering, setCovering] = useState(false);
  const dragRef = useRef<{ id: string; mode: "move" | "resize"; x: number; y: number } | null>(null);

  const fields = history.present;
  const selected = fields.find((f) => f.id === selectedId) ?? null;
  const dirty = !same(initial.fields.map(toDraft), fields) || name !== template.name;

  const aspect = PAGE_ASPECT[template.page_size] ?? PAGE_ASPECT["a4-landscape"];
  const width = Math.round(720 * zoom);
  const height = Math.round(width / aspect);
  const backgroundUrl = localBackground ?? template.background_url ?? null;

  const apply = useCallback((next: DraftField[]) => setHistory((h) => commit(h, next)), []);

  // Asked once, and never blocking: the action swallows every failure into "unavailable", so a detector
  // that is missing, off or broken costs the editor nothing. Manual placement never consults this.
  useEffect(() => {
    let cancelled = false;
    void detectTemplateTextAction(initial.id).then((result) => {
      if (!cancelled) setCanDetectText(result.available);
    });
    return () => { cancelled = true; };
  }, [initial.id]);

  // Re-read when the artwork itself changes, since a new design invalidates the old map entirely.
  useEffect(() => {
    let cancelled = false;
    void getArtworkMapAction(initial.id).then((map) => {
      if (!cancelled) setArtwork(map);
    });
    return () => { cancelled = true; };
  }, [initial.id, template.background_url]);

  // Keyboard is the whole point of an editor: a canvas reachable only by mouse excludes anyone using a
  // keyboard, and arrow-key nudging is more precise than dragging for the case that matters most —
  // aligning a field to a rule printed on the artwork.
  useEffect(() => {
    function onKey(e: KeyboardEvent) {
      const target = e.target as HTMLElement | null;
      if (target && ["INPUT", "TEXTAREA", "SELECT"].includes(target.tagName)) return;

      if ((e.metaKey || e.ctrlKey) && e.key.toLowerCase() === "z") {
        e.preventDefault();
        setHistory((h) => (e.shiftKey ? redo(h) : undo(h)));
        return;
      }
      if (!selectedId) return;
      if (e.key === "Delete" || e.key === "Backspace") {
        e.preventDefault();
        apply(removeField(fields, selectedId));
        setSelectedId(null);
        return;
      }
      const step = e.shiftKey ? 5 : 0.5;
      const moves: Record<string, [number, number]> = {
        ArrowLeft: [-step, 0], ArrowRight: [step, 0], ArrowUp: [0, -step], ArrowDown: [0, step]
      };
      const move = moves[e.key];
      if (move) {
        e.preventDefault();
        apply(moveField(fields, selectedId, move[0], move[1]));
      }
    }
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [fields, selectedId, apply]);

  function onPointerDown(e: React.PointerEvent, id: string, mode: "move" | "resize") {
    if (!canManage) return;
    e.stopPropagation();
    setSelectedId(id);
    (e.currentTarget as HTMLElement).setPointerCapture(e.pointerId);
    dragRef.current = { id, mode, x: e.clientX, y: e.clientY };
  }

  function onPointerMove(e: React.PointerEvent) {
    const drag = dragRef.current;
    if (!drag) return;
    // Pixels → percent once, here. Everything downstream is percentages, which is what makes the design
    // resolution-independent.
    const dx = ((e.clientX - drag.x) / width) * 100;
    const dy = ((e.clientY - drag.y) / height) * 100;
    if (Math.abs(dx) < 0.01 && Math.abs(dy) < 0.01) return;
    dragRef.current = { ...drag, x: e.clientX, y: e.clientY };
    setHistory((h) => commit(h, drag.mode === "move"
      ? moveField(h.present, drag.id, dx, dy)
      : resizeField(h.present, drag.id, dx, dy)));
  }

  function onPointerUp(e: React.PointerEvent) {
    dragRef.current = null;
    const el = e.currentTarget as HTMLElement;
    if (el.hasPointerCapture?.(e.pointerId)) el.releasePointerCapture(e.pointerId);
  }

  function add(kind: CertificateFieldKind, fieldKey?: string, label?: string) {
    const base = kind === "qrcode"
      ? { ...newField(kind), ...squareOnPage(13, template.page_size) }
      : newField(kind, { fieldKey, label });

    // Land somewhere free rather than always at the same coordinates — three fields added in a row used
    // to stack exactly on top of each other, which prints as illegible overlapping text. Artwork is
    // avoided too where a map is available, so a new field does not start life on top of the design's
    // own title.
    const field = {
      ...base,
      ...nextFreeSlot(fields, base, (candidate) => overlapsArtwork(artwork, candidate)),
    };

    apply(addField(fields, field));
    setSelectedId(field.id);
  }

  async function save() {
    setSaving(true);
    setMessage(null);

    if (name.trim() && name.trim() !== template.name) {
      const renamed = await updateTemplateAction(template.id, { name: name.trim() }, template.event_id);
      if ("error" in renamed && renamed.error) { setSaving(false); setMessage({ ok: false, text: renamed.error }); return; }
      if ("template" in renamed && renamed.template) setTemplate(renamed.template);
    }

    const result = await saveFieldsAction(template.id, toInput(fields), template.event_id);
    setSaving(false);

    if ("error" in result && result.error) { setMessage({ ok: false, text: result.error }); return; }
    if ("template" in result && result.template) {
      setTemplate(result.template);
      setHistory(initHistory(result.template.fields.map(toDraft)));
      setMessage({
        ok: true,
        text: result.template.version > template.version
          ? `Saved as version ${result.template.version}. Certificates already issued keep the version they were made with.`
          : "Design saved."
      });
      router.refresh();
    }
  }

  return (
    <div className="space-y-4">
      {/* ── Top bar: upload, preview-of-record, save ─────────────────────────────────────────── */}
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="min-w-0">
          <label className="sr-only" htmlFor="template-name">Design name</label>
          <input
            id="template-name"
            value={name}
            onChange={(e) => setName(e.target.value)}
            disabled={!canManage}
            className="h-10 w-72 max-w-full rounded-md border border-border bg-background px-3 text-sm font-semibold text-text"
          />
          <p className="mt-1 text-[11px] text-muted">
            Version {template.version} · {template.status}
            {template.has_issued_certificates ? " · certificates issued" : ""}
          </p>
        </div>

        <div className="flex flex-wrap items-center gap-2">
          {canManage ? (
            <BackgroundUpload
              template={template}
              onUploaded={(next, preview) => { setTemplate(next); setLocalBackground(preview); }}
            />
          ) : null}
          <Button type="button" variant="secondary" size="sm" disabled={!canUndo(history)}
            onClick={() => setHistory(undo)} aria-label="Undo">Undo</Button>
          <Button type="button" variant="secondary" size="sm" disabled={!canRedo(history)}
            onClick={() => setHistory(redo)} aria-label="Redo">Redo</Button>
          <label className="sr-only" htmlFor="zoom">Zoom</label>
          <select id="zoom" value={zoom} onChange={(e) => setZoom(Number(e.target.value))}
            className="h-9 rounded-md border border-border bg-background px-2 text-sm text-text">
            {ZOOM_STEPS.map((z) => <option key={z} value={z}>{Math.round(z * 100)}%</option>)}
          </select>
          <Button type="button" onClick={save} disabled={!canManage || saving || !dirty}>
            {saving ? "Saving…" : "Save"}
          </Button>
        </div>
      </div>

      {message ? (
        <p role={message.ok ? "status" : "alert"}
          className={`text-sm ${message.ok ? "text-muted" : "text-danger"}`}>{message.text}</p>
      ) : null}

      {/* Honest about what is exact and what is not. The artwork IS the artwork; only the type is an
          approximation, because the browser's fonts are not the renderer's. */}
      <p className="rounded-md border border-dashed border-border px-3 py-2 text-xs text-muted">
        Your uploaded design is shown exactly as it will print. The <strong>text</strong> is an
        approximation here — fonts and spacing are the browser&apos;s; the certificate itself is rendered
        on the server.
      </p>

      <div className="grid gap-4 lg:grid-cols-[auto_320px]">
        <div className="min-w-0 space-y-3">
          <div className="overflow-auto" onPointerMove={onPointerMove} onPointerUp={onPointerUp} onPointerLeave={onPointerUp}>
            <CertificateCanvas
              pageSize={template.page_size}
              backgroundUrl={backgroundUrl}
              fields={fields}
              width={width}
              selectedId={selectedId}
              interactive={canManage}
              onSelect={setSelectedId}
              onPointerDown={onPointerDown}
              onBackgroundClick={() => setSelectedId(null)}
            />
          </div>

          {canManage ? <AddDetails onAdd={add} canDetectText={canDetectText} /> : null}

          {/* The mistake this catches renders as `[Recipient's Full Name] John Doe` on a finished
              certificate: the placeholder is printed into the uploaded image, and the field's value is
              drawn on top of it. The renderer cannot edit those pixels, so the only place to catch it is
              here, while the layout is still being decided. */}
          {selected && artwork && overlapsArtwork(artwork, selected) && !selected.is_masking ? (
            <div role="status" className="mt-3 rounded-md border border-warning/40 bg-warning/10 px-3 py-2 text-sm text-text">
              <p>
                Your design already has something printed here, and it will still be there behind this
                field — the certificate would show both.
              </p>
              <Button
                type="button"
                variant="secondary"
                size="sm"
                className="mt-2"
                disabled={!canManage || covering}
                onClick={async () => {
                  setCovering(true);
                  // Sampled from the artwork around the field, never assumed: a white patch on cream
                  // stock is a smear exactly where it was meant to be invisible. No colour means no
                  // change — a guessed one would be worse than leaving the overlap visible.
                  const ground = await getArtworkColourAction(template.id, selected);
                  setCovering(false);
                  if (ground) apply(updateField(fields, selected.id, coverArtwork(selected, ground)));
                }}
              >
                {covering ? "Matching the design…" : "Cover what's printed here"}
              </Button>
            </div>
          ) : null}

          {selected?.is_masking ? (
            <p className="mt-3 text-xs text-muted">
              This field paints over the design behind it, so only its value shows. Clear the background
              colour under Advanced to let the design through again.
            </p>
          ) : null}
        </div>

        <div className="space-y-4">
          {selected && canManage ? (
            <Inspector
              field={selected}
              advanced={advanced}
              onToggleAdvanced={() => setAdvanced((v) => !v)}
              onChange={(patch) => apply(updateField(fields, selected.id, patch))}
              onRotate={(d) => apply(rotateField(fields, selected.id, d))}
              onReorder={(dir) => apply(reorderField(fields, selected.id, dir))}
              onDuplicate={() => {
                const next = duplicateField(fields, selected.id);
                if (next) { apply(next.fields); setSelectedId(next.id); }
              }}
              onDelete={() => { apply(removeField(fields, selected.id)); setSelectedId(null); }}
            />
          ) : (
            <section className="rounded-md border border-border bg-surface p-3">
              <h3 className="text-sm font-semibold text-text">Nothing selected</h3>
              <p className="mt-1 text-xs text-muted">
                Add a detail below, then click it on the design to move, resize or restyle it.
              </p>
            </section>
          )}

          <FieldList
            fields={normaliseOrder(fields)}
            selectedId={selectedId}
            canManage={canManage}
            onSelect={setSelectedId}
            onReorder={(id, dir) => apply(reorderField(fields, id, dir))}
          />
        </div>
      </div>
    </div>
  );
}

/**
 * "+ Add Details" — a flat list of what a certificate can say, not a palette of drawing tools.
 *
 * The dynamic keys offered here are suggestions, not a fixed vocabulary: the key is a free string, and a
 * creator whose spreadsheet has a column we never thought of types their own. A closed enum would mean a
 * code change for every new column.
 */
function AddDetails({ onAdd, canDetectText }: {
  onAdd: (kind: CertificateFieldKind, fieldKey?: string, label?: string) => void;
  /** True only when this deployment has a text detector configured. False in every build today, so the
   *  control below does not render and the editor is exactly what it was. */
  canDetectText?: boolean;
}) {
  const [custom, setCustom] = useState("");

  const common: { key: string; label: string }[] = [
    { key: "participant_name", label: "Participant name" },
    { key: "event_name", label: "Event name" },
    { key: "event_date", label: "Date" },
    { key: "achievement", label: "Achievement" },
    { key: "organizer_name", label: "Organizer" },
    { key: "certificate_id", label: "Certificate ID" }
  ];

  return (
    <section className="rounded-md border border-border bg-surface p-3">
      <h3 className="text-sm font-semibold text-text">+ Add Details</h3>
      <p className="mt-0.5 text-xs text-muted">
        Each one is filled in per recipient when certificates are generated. Click to add, then drag it
        into place on your design.
      </p>

      <ul className="mt-3 flex flex-wrap gap-2">
        {common.map((f) => (
          <li key={f.key}>
            <button
              type="button"
              onClick={() => onAdd("dynamicfield", f.key, f.label)}
              className="rounded-full border border-border px-3 py-1.5 text-xs text-text hover:border-accent hover:bg-accent/10"
            >
              {f.label}
            </button>
          </li>
        ))}
      </ul>

      <div className="mt-3 flex flex-wrap items-end gap-2 border-t border-border pt-3">
        <label className="text-[11px] text-muted">
          Your own field
          <input
            value={custom}
            onChange={(e) => setCustom(e.target.value)}
            placeholder="e.g. employee_grade"
            aria-label="Custom field key"
            className="mt-1 block h-8 w-48 rounded-md border border-border bg-background px-2 text-xs text-text"
          />
        </label>
        <Button
          type="button" variant="secondary" size="sm" disabled={!custom.trim()}
          onClick={() => { onAdd("dynamicfield", custom.trim(), custom.trim()); setCustom(""); }}
        >
          Add field
        </Button>
      </div>

      <div className="mt-3 flex flex-wrap items-center gap-2 border-t border-border pt-3">
        <span className="text-[11px] uppercase tracking-wide text-muted">Same on every copy</span>
        <Button type="button" variant="secondary" size="sm" onClick={() => onAdd("text")}>Fixed text</Button>
        <Button type="button" variant="secondary" size="sm" onClick={() => onAdd("image")}>Signature or logo</Button>
        <Button type="button" variant="secondary" size="sm" onClick={() => onAdd("qrcode")}>Verification QR</Button>
      </div>

      {/* The extension point, rendered only where an engine exists. Deliberately additive: it would place
          suggested fields alongside the hand-placed ones, never replace them, because a detector guessing
          wrong must cost a creator a delete rather than their layout. */}
      {canDetectText ? (
        <div className="mt-3 border-t border-border pt-3">
          <span className="text-[11px] uppercase tracking-wide text-muted">Assisted</span>
        </div>
      ) : null}
    </section>
  );
}

function Inspector({ field, advanced, onToggleAdvanced, onChange, onRotate, onReorder, onDuplicate, onDelete }: {
  field: DraftField;
  advanced: boolean;
  onToggleAdvanced: () => void;
  onChange: (patch: Partial<DraftField>) => void;
  onRotate: (delta: number) => void;
  onReorder: (dir: "front" | "forward" | "backward" | "back") => void;
  onDuplicate: () => void;
  onDelete: () => void;
}) {
  const isText = field.kind === "text" || field.kind === "dynamicfield";

  return (
    <section className="space-y-3 rounded-md border border-border bg-surface p-3">
      <div className="flex items-center justify-between gap-2">
        <h3 className="text-sm font-semibold text-text">
          {field.label || field.field_key || (field.kind === "text" ? "Fixed text" : field.kind)}
        </h3>
        <button type="button" onClick={onToggleAdvanced} className="text-[11px] text-muted underline">
          {advanced ? "Simple" : "Advanced"}
        </button>
      </div>

      {field.kind === "text" ? (
        <Field label="Text">
          <input value={field.static_text ?? ""} onChange={(e) => onChange({ static_text: e.target.value })}
            className={inputCls} />
        </Field>
      ) : null}

      {field.kind === "dynamicfield" ? (
        <Field label="Field key">
          <input
            value={field.field_key ?? ""}
            onChange={(e) => onChange({ field_key: e.target.value })}
            aria-label="Field key"
            className={inputCls}
          />
        </Field>
      ) : null}

      {isText ? (
        <>
          <div className="grid grid-cols-2 gap-2">
            <Field label="Font">
              <select value={field.font_family ?? "sans"} onChange={(e) => onChange({ font_family: e.target.value })} className={inputCls}>
                {FONT_FAMILIES.map((f) => <option key={f} value={f}>{f}</option>)}
              </select>
            </Field>
            <Field label="Weight">
              <select value={field.font_weight ?? "normal"} onChange={(e) => onChange({ font_weight: e.target.value })} className={inputCls}>
                {FONT_WEIGHTS.map((f) => <option key={f} value={f}>{f}</option>)}
              </select>
            </Field>
          </div>
          <div className="grid grid-cols-2 gap-2">
            <Num label="Size (pt)" value={field.font_size_pt ?? 24} onChange={(v) => onChange({ font_size_pt: v })} />
            <Field label="Align">
              <select value={field.horizontal_alignment} onChange={(e) => onChange({ horizontal_alignment: e.target.value })} className={inputCls}>
                {HORIZONTAL_ALIGNMENTS.map((a) => <option key={a} value={a}>{a}</option>)}
              </select>
            </Field>
          </div>
          <Field label="Text colour">
            <input type="color" value={field.color ?? "#0F172A"} onChange={(e) => onChange({ color: e.target.value })}
              className="h-9 w-full rounded-md border border-border bg-background" />
          </Field>
        </>
      ) : null}

      {advanced ? (
        <>
          <div className="grid grid-cols-4 gap-2">
            <Num label="X" value={field.x} onChange={(v) => onChange({ x: v })} />
            <Num label="Y" value={field.y} onChange={(v) => onChange({ y: v })} />
            <Num label="W" value={field.width} onChange={(v) => onChange({ width: v })} />
            <Num label="H" value={field.height} onChange={(v) => onChange({ height: v })} />
          </div>

          <Field label="Rotation">
            <div className="flex items-center gap-2">
              <Button type="button" variant="secondary" size="sm" onClick={() => onRotate(-90)}>−90°</Button>
              <Button type="button" variant="secondary" size="sm" onClick={() => onRotate(90)}>+90°</Button>
              <span className="text-xs tabular-nums text-muted">{field.rotation}°</span>
            </div>
          </Field>

          {isText ? (
            <Field label="Vertical align">
              <select value={field.vertical_alignment} onChange={(e) => onChange({ vertical_alignment: e.target.value })} className={inputCls}>
                {VERTICAL_ALIGNMENTS.map((a) => <option key={a} value={a}>{a}</option>)}
              </select>
            </Field>
          ) : null}

          <Field label="Depth">
            <div className="flex flex-wrap items-center gap-1">
              <Button type="button" variant="secondary" size="sm" onClick={() => onReorder("back")}>Back</Button>
              <Button type="button" variant="secondary" size="sm" onClick={() => onReorder("backward")}>−</Button>
              <Button type="button" variant="secondary" size="sm" onClick={() => onReorder("forward")}>+</Button>
              <Button type="button" variant="secondary" size="sm" onClick={() => onReorder("front")}>Front</Button>
            </div>
          </Field>

          {/* Cover and replace. Stated plainly because it is not editing: the printed text stays where it
              is, under a patch. Moving this field reveals it again. */}
          <div className="rounded border border-dashed border-border p-2">
            <label className="flex items-center gap-2 text-xs text-text">
              <input
                type="checkbox"
                checked={field.is_masking}
                onChange={(e) => onChange({
                  is_masking: e.target.checked,
                  background_color: e.target.checked ? field.background_color ?? "#FFFFFF" : null
                })}
              />
              Cover printed text underneath
            </label>
            <p className="mt-1 text-[11px] text-muted">
              Your design cannot be un-printed. This paints over that area in the colour below and writes
              on top — so if you move it, the original text shows through again.
            </p>
            {field.is_masking ? (
              <input
                type="color"
                value={field.background_color ?? "#FFFFFF"}
                onChange={(e) => onChange({ background_color: e.target.value })}
                aria-label="Cover colour"
                className="mt-2 h-8 w-full rounded-md border border-border bg-background"
              />
            ) : null}
          </div>

          <label className="flex items-center gap-2 text-xs text-muted">
            <input type="checkbox" checked={field.is_required}
              onChange={(e) => onChange({ is_required: e.target.checked })} />
            Refuse to generate if this value is missing
          </label>
        </>
      ) : null}

      <div className="flex items-center justify-end gap-1 pt-1">
        <Button type="button" variant="secondary" size="sm" onClick={onDuplicate}>Duplicate</Button>
        <Button type="button" variant="ghost" size="sm" onClick={onDelete}>Delete</Button>
      </div>
    </section>
  );
}

function FieldList({ fields, selectedId, canManage, onSelect, onReorder }: {
  fields: DraftField[];
  selectedId: string | null;
  canManage: boolean;
  onSelect: (id: string) => void;
  onReorder: (id: string, dir: "front" | "forward" | "backward" | "back") => void;
}) {
  // Reversed: the list reads top-of-stack first, which is what "layers" means everywhere else.
  const ordered = [...fields].reverse();

  return (
    <section className="rounded-md border border-border bg-surface p-3">
      <h3 className="text-sm font-semibold text-text">On this design</h3>
      {ordered.length === 0 ? (
        <p className="mt-2 text-xs text-muted">Nothing added yet.</p>
      ) : (
        <ul className="mt-2 space-y-1">
          {ordered.map((f) => (
            <li key={f.id} className="flex items-center gap-1">
              <button
                type="button"
                onClick={() => onSelect(f.id)}
                aria-pressed={f.id === selectedId}
                className={`min-w-0 flex-1 truncate rounded px-2 py-1 text-left text-xs ${
                  f.id === selectedId ? "bg-accent/10 text-text" : "text-muted hover:bg-elevated hover:text-text"
                }`}
              >
                <span className="uppercase opacity-60">{f.kind === "dynamicfield" ? "field" : f.kind}</span>{" "}
                {f.label || f.field_key || f.static_text || f.id}
                {f.is_masking ? <span className="ml-1 opacity-60">(covers)</span> : null}
              </button>
              {canManage ? (
                <>
                  <button type="button" aria-label={`Bring ${f.id} forward`} onClick={() => onReorder(f.id, "forward")}
                    className="rounded px-1 text-xs text-muted hover:text-text">↑</button>
                  <button type="button" aria-label={`Send ${f.id} backward`} onClick={() => onReorder(f.id, "backward")}
                    className="rounded px-1 text-xs text-muted hover:text-text">↓</button>
                </>
              ) : null}
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}

const inputCls = "h-9 w-full rounded-md border border-border bg-background px-2 text-sm text-text";

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <label className="block text-xs text-muted">
      {label}
      <div className="mt-1">{children}</div>
    </label>
  );
}

function Num({ label, value, onChange }: { label: string; value: number; onChange: (v: number) => void }) {
  return (
    <label className="block text-xs text-muted">
      {label}
      <input
        type="number"
        value={value}
        step={0.5}
        onChange={(e) => onChange(Number(e.target.value))}
        aria-label={label}
        className="mt-1 h-9 w-full rounded-md border border-border bg-background px-2 text-sm text-text"
      />
    </label>
  );
}
