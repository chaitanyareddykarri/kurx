"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { Button } from "@kurx/ui";
import {
  detectTemplateTextAction, getArtworkColourAction, getArtworkMapAction, saveFieldsAction,
  updateTemplateAction
} from "@/lib/certificate-actions";
import { overlapsArtwork, type ArtworkMap } from "@/lib/certificate-artwork-map";
import { CertificateTextPanel } from "@/components/host/certificates/certificate-text-panel";
import { CertificateSteps, type Step, type StepId } from "@/components/host/certificates/certificate-steps";
import { autoLayout, foldDetection } from "@/lib/certificate-autolayout";
import type { TextDetection } from "@/lib/certificate-detection";
import { toKey } from "@/lib/certificate-fields";
import { BackgroundUpload } from "@/components/host/certificates/background-upload";
import { CertificateSizePicker } from "@/components/host/certificates/certificate-size-picker";
import { CertificateCanvas } from "@/components/host/certificates/certificate-canvas";
import type { PageSizePreset, CertificateTemplate, CertificateFieldKind } from "@/lib/certificate-api";
import {
  pageAspect,
  mmToPoints,
  addField, canRedo, canUndo, commit, initHistory, moveField, newField, normaliseOrder,
  coverArtwork, nextFreeSlot, redo, removeField, resizeField, same,
  toDraft, toInput, undo, updateField,
  type DraftField, type History, startDrawing } from "@/lib/certificate-editor";

/**
 * Making a certificate, as five steps (D-359).
 *
 * **A guided tool, not a design editor.** The person using this runs an event; they have not been trained
 * on anything, and they will use it once every few months. So the screen answers four questions at all
 * times — what step am I on, what does this button do, where do I click, what happens next — and every
 * control that does not help answer one of them has been taken out.
 *
 * What went, and why: the zoom selector, rotation, z-order, duplicate, layer ordering, per-field alignment
 * and the whole "Advanced" panel. None of them are needed to put a name on a certificate, and each one was
 * a thing to read past. Text size, colour and bold survive because "make it bigger" is a thing people
 * genuinely want and needs no explaining.
 *
 * **Plain words everywhere.** Nothing user-facing says element, handle, field (as a noun), mask, render or
 * z-order. It says *text*, *picture*, *changes for each person*, *the same on every certificate*.
 *
 * **Preview is the real thing.** It asks the server for a rendered sample using the same renderer that
 * issues certificates, rather than showing the browser's approximation of it — because a preview that is
 * only nearly right is worse than none, and the difference is discovered after two hundred are sent.
 *
 * Every mutation still goes through the pure functions in `lib/certificate-editor` and lands in history via
 * `commit`, so Undo is a stack of whole field lists rather than a stack of inverse operations.
 */
export function CertificateTemplateEditor({ template: initial, canManage, pageSizes }: {
  template: CertificateTemplate;
  canManage: boolean;
  /** The page-size catalogue (D-361), fetched on the server so the dimensions have one home. Defaulted
   *  to empty: the picker then offers only Custom, which is degraded but not broken. */
  pageSizes?: PageSizePreset[];
}) {
  const router = useRouter();
  const sizes = pageSizes ?? [];
  const [template, setTemplate] = useState(initial);
  const [history, setHistory] = useState<History>(() => initHistory(initial.fields.map(toDraft)));
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [name, setName] = useState(initial.name);
  const [saving, setSaving] = useState(false);
  const [message, setMessage] = useState<{ ok: boolean; text: string } | null>(null);
  const [localBackground, setLocalBackground] = useState<string | null>(null);
  const [canDetectText, setCanDetectText] = useState(false);
  /** Still working out whether this deployment can read designs, and whether this one has been read.
   *  Counts as busy: otherwise the Find text step offers a button, then withdraws it a moment later when
   *  the automatic pass starts, then offers it again — a control that flickers on load reads as broken. */
  const [settling, setSettling] = useState(true);
  const [artwork, setArtwork] = useState<ArtworkMap | null>(null);
  const [covering, setCovering] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [scanning, setScanning] = useState(false);
  /** Why reading failed, in words the reader can act on. Null means it has not failed. */
  const [readFailed, setReadFailed] = useState<string | null>(null);
  /** The step the user asked for. Null means "wherever the design actually is" — see `step`. */
  const [goneTo, setGoneTo] = useState<StepId | null>(null);
  const [savingSize, setSavingSize] = useState(false);

  /**
   * Persists a page-size change (D-361).
   *
   * Saved immediately rather than gathered into the main Save, because the page is the coordinate space
   * every field's percentage is relative to — the canvas has to redraw at the new shape straight away or
   * the creator is placing fields against a page they are not on. The server is authoritative for a
   * preset's dimensions, so the returned template replaces local state wholesale rather than being
   * patched from what was sent.
   */
  async function changeSize(next: { pageSize: string; pageWidthMm?: number; pageHeightMm?: number }) {
    if (!canManage) return;
    setSavingSize(true);
    setMessage(null);
    const result = await updateTemplateAction(template.id, next, template.event_id);
    setSavingSize(false);
    if ("error" in result && result.error) {
      setMessage({ ok: false, text: "That size could not be saved. Please try again." });
      return;
    }
    if ("template" in result && result.template) setTemplate(result.template);
  }
  const dragRef = useRef<{ id: string; mode: "move" | "resize"; x: number; y: number } | null>(null);

  const fields = history.present;
  const selected = fields.find((f) => f.id === selectedId) ?? null;
  const dirty = !same(initial.fields.map(toDraft), fields) || name !== template.name;

  const aspect = pageAspect(template.page_size, template.page_width_mm, template.page_height_mm);
  const width = 720;
  const height = Math.round(width / aspect);
  const backgroundUrl = localBackground ?? template.background_url ?? null;

  const hasDesign = Boolean(backgroundUrl);
  const hasText = fields.length > 0;

  /**
   * Where the user is.
   *
   * Derived from the design itself, so it is right on arrival without anyone having clicked anything —
   * a half-finished certificate opened a week later lands on the step it actually stopped at. An explicit
   * click wins, but only as far as the design allows: you cannot preview a page with nothing on it.
   */
  const step: StepId =
    !hasDesign ? "upload"
    : goneTo && (goneTo !== "preview" && goneTo !== "download" ? true : hasText) ? goneTo
    : !hasText ? "detect"
    : "edit";

  const apply = useCallback((next: DraftField[]) => setHistory((h) => commit(h, next)), []);

  useEffect(() => {
    let cancelled = false;
    void detectTemplateTextAction(initial.id).then((result) => {
      if (cancelled) return;
      setCanDetectText(result.available);
      // Only settled once a readable design has had its automatic pass; an unreadable one settles here.
      if (!result.available) setSettling(false);
    });
    return () => { cancelled = true; };
  }, [initial.id]);

  /**
   * Reads an uploaded design without being asked (D-358).
   *
   * The words are already on the certificate. Presenting an empty panel and asking someone to retype and
   * reposition each one is asking them to do by hand the job the reader exists to do. Only when there is
   * nothing to lose — once anything has been placed, a re-read is offered instead.
   */
  const autoRead = useRef<string | null>(null);
  useEffect(() => {
    if (!canManage || !canDetectText || !backgroundUrl) { setSettling(false); return; }
    if (fields.length > 0 || scanning) { setSettling(false); return; }
    if (autoRead.current === backgroundUrl) { setSettling(false); return; }

    autoRead.current = backgroundUrl;
    void read({ silent: true }).finally(() => setSettling(false));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [canManage, canDetectText, backgroundUrl, fields.length]);

  useEffect(() => {
    let cancelled = false;
    void getArtworkMapAction(initial.id).then((map) => {
      if (!cancelled) setArtwork(map);
    });
    return () => { cancelled = true; };
  }, [initial.id, template.background_url]);

  // Arrow keys nudge and Delete removes, because a canvas reachable only by mouse excludes anyone using a
  // keyboard — and nudging is more precise than dragging for the case that matters, lining text up with a
  // rule printed on the design.
  useEffect(() => {
    function onKey(e: KeyboardEvent) {
      const target = e.target as HTMLElement | null;
      if (target && ["INPUT", "TEXTAREA", "SELECT"].includes(target.tagName)) return;
      if (editingId) return;

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
  }, [fields, selectedId, editingId, apply]);

  /** Click selects; clicking the selected text again types into it. Double-click does the same, because
   *  neither gesture is guessable and offering both costs nothing. */
  function startOrSelect(id: string) {
    const field = fields.find((f) => f.id === id);
    if (selectedId === id && field?.kind === "text") setEditingId(id);
    else { setSelectedId(id); setEditingId(null); }
  }

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

  /**
   * Finds the text printed on the design and makes it changeable.
   *
   * `silent` is the automatic first pass: a design nobody asked us to read must not report failure at
   * someone who never invoked the feature. An explicit Try Again does report, because then it is the
   * answer to a question they asked.
   */
  async function read({ silent = false } = {}) {
    // Any read counts as having read this design — including one the user asked for before the automatic
    // pass got its turn. Without this, the silent pass fires afterwards and clears the failure message
    // they just asked to see, leaving a screen that looks like the button did nothing.
    autoRead.current = backgroundUrl;

    setScanning(true);
    setReadFailed(null);
    setMessage(null);

    const detection = await detectTemplateTextAction(initial.id);
    // The page's real height in points, from its millimetres (D-361). `autoLayout` scales detected
    // type by it, so it has to be the HEIGHT of the actual page — this previously read
    // `page_size === "a4-portrait" ? 1191 : 842`, which was one axis out in both orientations (A4 is
    // 842pt tall portrait and 595pt tall landscape, and 1191pt is A3), so every automatically-sized
    // field came out around 40% too large. Computed now, so it is also right for the twelve sizes
    // that did not exist when those constants were written.
    const result = autoLayout(detection, mmToPoints(template.page_height_mm));
    setScanning(false);

    if (!detection.available || result.fields.length === 0) {
      // Never the machine's words. "DetectionError: element_coordinates_invalid" tells the person holding
      // a certificate nothing they can act on; "try a clearer picture" tells them exactly what to do.
      if (!silent) {
        setReadFailed(detection.available
          ? "Try uploading a clearer picture of your certificate, or add the text yourself."
          : "Text finding is not switched on here. You can still add the text yourself.");
      }
      return;
    }

    apply(foldDetection(fields, result.fields, detection));
    setSelectedId(null);
    setGoneTo("edit");
    setMessage({
      ok: true,
      text: `Found ${result.fields.length} piece${result.fields.length === 1 ? "" : "s"} of text you can change.`,
    });
  }

  /// `kind` defaults to a text field — the overwhelming case. A participant photo is an `image` field
  /// whose key names which picture the server paints, which is why the kind travels with the key.
  function addNamedField(fieldKey: string | null, label: string, kind: CertificateFieldKind = "dynamicfield") {
    const base = newField(kind, { fieldKey: fieldKey ?? toKey(label), label });
    const field = { ...base, ...nextFreeSlot(fields, base, (c) => overlapsArtwork(artwork, c)) };
    apply(addField(fields, field));
    setSelectedId(field.id);
  }

  function deleteField(id: string) {
    apply(removeField(fields, id));
    if (selectedId === id) setSelectedId(null);
  }

  async function save(): Promise<boolean> {
    setSaving(true);
    setMessage(null);

    if (name.trim() && name.trim() !== template.name) {
      const renamed = await updateTemplateAction(template.id, { name: name.trim() }, template.event_id);
      if ("error" in renamed && renamed.error) {
        setSaving(false);
        setMessage({ ok: false, text: "Your changes could not be saved. Please try again." });
        return false;
      }
      if ("template" in renamed && renamed.template) setTemplate(renamed.template);
    }

    const result = await saveFieldsAction(template.id, toInput(fields), template.event_id);
    setSaving(false);

    if ("error" in result && result.error) {
      setMessage({ ok: false, text: "Your changes could not be saved. Please try again." });
      return false;
    }
    if ("template" in result && result.template) {
      setTemplate(result.template);
      setHistory(initHistory(result.template.fields.map(toDraft)));
      setMessage({ ok: true, text: "Saved." });
      router.refresh();
    }
    return true;
  }

  const steps: Step[] = [
    { id: "upload", label: "Upload", hint: "Choose your certificate picture.", done: hasDesign, enabled: true },
    { id: "detect", label: "Find text", hint: "We find the text you can change.", done: hasText, enabled: hasDesign },
    { id: "edit", label: "Edit", hint: "Click any text to change it.", done: hasText && !dirty, enabled: hasText },
    { id: "preview", label: "Preview", hint: "See exactly how it will look.", done: false, enabled: hasText },
    { id: "download", label: "Download", hint: "Save a copy to your computer.", done: false, enabled: hasText },
  ];

  return (
    <div className="space-y-7">
      <header className="space-y-2">
        <h1 className="text-2xl font-bold text-text">Make your certificate</h1>
        <label className="sr-only" htmlFor="template-name">Certificate name</label>
        <input
          id="template-name"
          value={name}
          onChange={(e) => setName(e.target.value)}
          disabled={!canManage}
          className="h-12 w-full max-w-md rounded-lg border border-border bg-background px-3 text-base font-semibold text-text focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-accent"
        />
      </header>

      <CertificateSteps steps={steps} current={step} onGo={setGoneTo} />

      {message ? (
        <p
          role={message.ok ? "status" : "alert"}
          className={`rounded-lg border px-4 py-3 text-base ${
            message.ok
              ? "border-success/40 bg-success/10 text-text"
              : "border-danger/40 bg-danger/10 text-text"
          }`}
        >
          <span aria-hidden className="mr-2">{message.ok ? "✓" : "!"}</span>
          {message.text}
        </p>
      ) : null}

      {step === "upload" ? (
        <UploadStep template={template} canManage={canManage}
          onUploaded={(next, preview) => { setTemplate(next); setLocalBackground(preview); }}
          pageSizes={sizes} savingSize={savingSize} onSizeChange={changeSize} />
      ) : null}

      {step === "detect" ? (
        <DetectStep
          scanning={scanning || settling}
          failed={readFailed}
          canManage={canManage}
          onRead={() => void read()}
          onSkip={() => setGoneTo("edit")}
          preview={
            <CertificateCanvas pageSize={template.page_size}
              pageWidthMm={template.page_width_mm} pageHeightMm={template.page_height_mm} backgroundUrl={backgroundUrl}
              fields={[]} width={480} interactive={false} />
          }
        />
      ) : null}

      {step === "edit" ? (
        <EditStep
          template={template}
          fields={fields}
          selected={selected}
          selectedId={selectedId}
          canManage={canManage}
          backgroundUrl={backgroundUrl}
          width={width}
          editingId={editingId}
          artwork={artwork}
          covering={covering}
          scanning={scanning}
          canDetectText={canDetectText}
          history={history}
          saving={saving}
          dirty={dirty}
          onUndo={() => setHistory(undo)}
          onRedo={() => setHistory(redo)}
          onPointerMove={onPointerMove}
          onPointerUp={onPointerUp}
          onPointerDown={onPointerDown}
          onSelect={startOrSelect}
          onDeselect={() => { setSelectedId(null); setEditingId(null); }}
          onStartEditing={(id) => { setSelectedId(id); setEditingId(id); }}
          onEditDone={() => setEditingId(null)}
          onChangeText={(id, text) =>
            apply(updateField(fields, id, startDrawing(fields.find((f) => f.id === id), text)))}
          onChange={(patch) => selected && apply(updateField(fields, selected.id, patch))}
          onDelete={deleteField}
          onAdd={addNamedField}
          onReRead={() => void read()}
          onCover={async () => {
            if (!selected) return;
            setCovering(true);
            const ground = await getArtworkColourAction(template.id, selected);
            setCovering(false);
            if (ground) apply(updateField(fields, selected.id, coverArtwork(selected, ground)));
          }}
          onSave={() => void save()}
          onNext={() => setGoneTo("preview")}
          pageSizes={sizes}
          savingSize={savingSize}
          onSizeChange={changeSize}
        />
      ) : null}

      {step === "preview" || step === "download" ? (
        <PreviewStep
          templateId={template.id}
          download={step === "download"}
          dirty={dirty}
          saving={saving}
          canManage={canManage}
          onSave={save}
          onBack={() => setGoneTo("edit")}
          onNext={() => setGoneTo("download")}
        />
      ) : null}
    </div>
  );
}

/** A big icon, a big heading, one sentence, one button. Nothing else on the screen. */
function UploadStep({ template, canManage, onUploaded, pageSizes, savingSize, onSizeChange }: {
  template: CertificateTemplate;
  canManage: boolean;
  onUploaded: (t: CertificateTemplate, preview: string) => void;
  pageSizes: PageSizePreset[];
  savingSize: boolean;
  onSizeChange: (next: { pageSize: string; pageWidthMm?: number; pageHeightMm?: number }) => void;
}) {
  return (
    <section className="rounded-2xl border-2 border-dashed border-border bg-surface px-6 py-14 text-center">
      <span aria-hidden className="block text-6xl leading-none">🖼️</span>
      <h2 className="mt-5 text-xl font-bold text-text">Upload your certificate</h2>
      <p className="mx-auto mt-2 max-w-md text-base text-muted">
        Choose a certificate image to get started. We will find the text on it for you.
      </p>
      <div className="mt-6 flex justify-center">
        {canManage ? (
          <BackgroundUpload template={template} prominent onUploaded={onUploaded} />
        ) : (
          <p className="text-base text-muted">You do not have permission to change this certificate.</p>
        )}
      </div>
      <p className="mt-4 text-sm text-muted">PNG, JPG or WebP · up to 15MB</p>

      {/* The page belongs with the artwork: they are one decision, and changing the size after fields are
          placed moves every one of them. Left-aligned inside a centred panel because a form read
          centre-aligned is harder to scan than the prose above it. */}
      <div className="mx-auto mt-8 max-w-md text-left">
        <CertificateSizePicker
          presets={pageSizes}
          pageSize={template.page_size}
          pageWidthMm={template.page_width_mm}
          pageHeightMm={template.page_height_mm}
          disabled={!canManage || savingSize}
          onChange={onSizeChange}
        />
      </div>
    </section>
  );
}

/** Normally passed straight through by the automatic read. This is what a failure looks like, and it is
 *  the only place the reader is ever a button. */
function DetectStep({ scanning, failed, canManage, onRead, onSkip, preview }: {
  scanning: boolean;
  failed: string | null;
  canManage: boolean;
  onRead: () => void;
  onSkip: () => void;
  preview: React.ReactNode;
}) {
  return (
    <section className="rounded-2xl border border-border bg-surface p-6">
      <div className="grid gap-6 md:grid-cols-[1fr_auto] md:items-center">
        <div>
          <span aria-hidden className="block text-5xl leading-none">{failed ? "😕" : "🔍"}</span>
          <h2 className="mt-4 text-xl font-bold text-text">
            {scanning ? "Finding your text…" : failed ? "We couldn’t find the text" : "Find the text"}
          </h2>
          <p className="mt-2 max-w-md text-base text-muted">
            {scanning
              ? "This takes a few seconds."
              : failed
                ? failed
                : "We’ll find the text on your certificate so you can change it."}
          </p>

          {!scanning && canManage ? (
            <div className="mt-6 flex flex-wrap gap-3">
              <Button type="button" size="xl" onClick={onRead}>
                <span aria-hidden className="mr-2 text-lg">🔍</span>
                {failed ? "Try Again" : "Find Text"}
              </Button>
              <Button type="button" size="lg" variant="secondary" onClick={onSkip}>
                Add the text myself
              </Button>
            </div>
          ) : null}
        </div>

        <div className="justify-self-center opacity-90">{preview}</div>
      </div>
    </section>
  );
}

function EditStep(props: {
  template: CertificateTemplate;
  fields: DraftField[];
  selected: DraftField | null;
  selectedId: string | null;
  canManage: boolean;
  backgroundUrl: string | null;
  width: number;
  editingId: string | null;
  artwork: ArtworkMap | null;
  covering: boolean;
  scanning: boolean;
  canDetectText: boolean;
  history: History;
  saving: boolean;
  dirty: boolean;
  onUndo: () => void;
  onRedo: () => void;
  onPointerMove: (e: React.PointerEvent) => void;
  onPointerUp: (e: React.PointerEvent) => void;
  onPointerDown: (e: React.PointerEvent, id: string, mode: "move" | "resize") => void;
  onSelect: (id: string) => void;
  onDeselect: () => void;
  onStartEditing: (id: string) => void;
  onEditDone: () => void;
  onChangeText: (id: string, text: string) => void;
  onChange: (patch: Partial<DraftField>) => void;
  onDelete: (id: string) => void;
  onAdd: (key: string | null, label: string) => void;
  onReRead: () => void;
  onCover: () => void;
  onSave: () => void;
  onNext: () => void;
  pageSizes: PageSizePreset[];
  savingSize: boolean;
  onSizeChange: (next: { pageSize: string; pageWidthMm?: number; pageHeightMm?: number }) => void;
}) {
  const { selected, canManage } = props;
  const overlapping = selected && props.artwork
    && overlapsArtwork(props.artwork, selected) && !selected.is_masking;

  return (
    <section className="space-y-5">
      <div>
        <h2 className="text-xl font-bold text-text">Edit your certificate</h2>
        <p className="mt-1 text-base text-muted">
          Click any text on the certificate to change it. Everything else stays exactly as you designed it.
        </p>
      </div>

      <div className="grid gap-6 lg:grid-cols-[auto_360px]">
        <div className="min-w-0 space-y-4">
          <div
            className="overflow-auto"
            onPointerMove={props.onPointerMove}
            onPointerUp={props.onPointerUp}
            onPointerLeave={props.onPointerUp}
          >
            <CertificateCanvas
              pageSize={props.template.page_size}
              pageWidthMm={props.template.page_width_mm}
              pageHeightMm={props.template.page_height_mm}
              backgroundUrl={props.backgroundUrl}
              fields={props.fields}
              width={props.width}
              selectedId={props.selectedId}
              interactive={canManage}
              editingId={props.editingId}
              showEditable
              onSelect={props.onSelect}
              onPointerDown={props.onPointerDown}
              onBackgroundClick={props.onDeselect}
              onStartEditing={props.onStartEditing}
              onTextChange={props.onChangeText}
              onEditDone={props.onEditDone}
            />
          </div>

          <p className="text-sm text-muted">
            <span aria-hidden className="mr-1">ℹ️</span>
            Dashed boxes show the text you can change. Your design itself is shown exactly as it will print.
          </p>

          {overlapping ? (
            <div role="status" className="rounded-xl border border-warning/50 bg-warning/10 p-4">
              <p className="text-base font-semibold text-text">This sits on top of your design</p>
              <p className="mt-1 text-sm text-muted">
                Your certificate already has something printed here, so both would show. Hide what is
                underneath?
              </p>
              <Button type="button" variant="secondary" className="mt-3"
                disabled={!canManage || props.covering} onClick={props.onCover}>
                {props.covering ? "Matching your design…" : "Hide what’s underneath"}
              </Button>
            </div>
          ) : null}

          {canManage ? (
            <div className="flex flex-wrap items-center gap-3">
              <ToolButton icon="↶" label="Undo" disabled={!canUndo(props.history)} onClick={props.onUndo} />
              <ToolButton icon="↷" label="Redo" disabled={!canRedo(props.history)} onClick={props.onRedo} />
              {props.canDetectText ? (
                <ToolButton icon="🔍" label={props.scanning ? "Finding…" : "Find text again"}
                  disabled={props.scanning} onClick={props.onReRead} />
              ) : null}
            </div>
          ) : null}
        </div>

        <div className="space-y-5">
          {selected && canManage ? <Appearance field={selected} onChange={props.onChange} /> : null}

          <CertificateTextPanel
            fields={normaliseOrder(props.fields)}
            selectedId={props.selectedId}
            canManage={canManage}
            onSelect={props.onSelect}
            onChangeText={props.onChangeText}
            onDelete={props.onDelete}
            onAdd={props.onAdd}
          />

          {/* Last in the column, under the text tools. Someone on this step came to fix wording; the page
              is a thing they occasionally need to correct, not the job in front of them, so it sits below
              the work rather than competing with it. */}
          <CertificateSizePicker
            presets={props.pageSizes}
            pageSize={props.template.page_size}
            pageWidthMm={props.template.page_width_mm}
            pageHeightMm={props.template.page_height_mm}
            disabled={!canManage || props.savingSize}
            warnArtworkWillRescale={Boolean(props.backgroundUrl)}
            onChange={props.onSizeChange}
          />
        </div>
      </div>

      {canManage ? (
        <div className="flex flex-wrap items-center gap-3 border-t border-border pt-5">
          <Button type="button" size="xl" onClick={props.onNext}>
            <span aria-hidden className="mr-2 text-lg">👁️</span>
            Preview
          </Button>
          <Button type="button" size="lg" variant="secondary" disabled={props.saving || !props.dirty}
            onClick={props.onSave}>
            <span aria-hidden className="mr-2 text-lg">💾</span>
            {props.saving ? "Saving…" : props.dirty ? "Save" : "Saved"}
          </Button>
        </div>
      ) : null}
    </section>
  );
}

/** Icon and word together, always. An icon alone is a puzzle for anyone who does not already know it. */
function ToolButton({ icon, label, disabled, onClick }: {
  icon: string; label: string; disabled?: boolean; onClick: () => void;
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      disabled={disabled}
      title={label}
      className="flex h-12 items-center gap-2 rounded-lg border border-border bg-surface px-4 text-base font-semibold text-text hover:bg-elevated disabled:opacity-40 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
    >
      <span aria-hidden className="text-lg leading-none">{icon}</span>
      {label}
    </button>
  );
}

/**
 * How the selected text looks: bigger, smaller, bolder, a different colour.
 *
 * Three controls, because these are the three things people ask for by name. Rotation, spacing, line
 * height and alignment were all here once; none of them are needed to put a name on a certificate, and
 * every one of them was something to read past on the way to the size.
 */
function Appearance({ field, onChange }: {
  field: DraftField;
  onChange: (patch: Partial<DraftField>) => void;
}) {
  if (field.kind !== "text" && field.kind !== "dynamicfield") return null;
  const size = field.font_size_pt ?? 24;

  return (
    <section className="rounded-xl border border-border bg-surface p-4">
      <h3 className="text-base font-bold text-text">How this text looks</h3>

      <div className="mt-3 flex flex-wrap items-center gap-3">
        <div className="flex items-center gap-2">
          <span className="text-sm font-semibold text-text">Size</span>
          <button
            type="button"
            aria-label="Make text smaller"
            title="Make text smaller"
            onClick={() => onChange({ font_size_pt: Math.max(6, Math.round(size - 2)) })}
            className="h-12 w-12 rounded-lg border border-border text-xl font-bold text-text hover:bg-elevated focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
          >
            −
          </button>
          <span className="min-w-[3ch] text-center text-base font-semibold text-text" aria-live="polite">
            {Math.round(size)}
          </span>
          <button
            type="button"
            aria-label="Make text bigger"
            title="Make text bigger"
            onClick={() => onChange({ font_size_pt: Math.min(120, Math.round(size + 2)) })}
            className="h-12 w-12 rounded-lg border border-border text-xl font-bold text-text hover:bg-elevated focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
          >
            ＋
          </button>
        </div>

        <button
          type="button"
          aria-pressed={field.font_weight === "bold"}
          onClick={() => onChange({ font_weight: field.font_weight === "bold" ? "normal" : "bold" })}
          className={`h-12 rounded-lg border px-4 text-base font-bold focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent ${
            field.font_weight === "bold"
              ? "border-accent bg-accent/10 text-text"
              : "border-border text-text hover:bg-elevated"
          }`}
        >
          Bold{field.font_weight === "bold" ? " ✓" : ""}
        </button>

        <label className="flex items-center gap-2">
          <span className="text-sm font-semibold text-text">Colour</span>
          <input
            type="color"
            aria-label="Text colour"
            value={field.color ?? "#0F172A"}
            onChange={(e) => onChange({ color: e.target.value })}
            className="h-12 w-14 cursor-pointer rounded-lg border border-border bg-background p-1"
          />
        </label>
      </div>
    </section>
  );
}

/**
 * The certificate as it will actually print, and a copy to keep.
 *
 * Asked of the server, which renders it with the same code that issues certificates. The editing canvas
 * uses the browser's fonts and line breaking, so it is close but not exact — and "close" is discovered
 * after the certificates are sent. Since the server renders what has been *saved*, an unsaved change is
 * saved first rather than quietly left out of the picture.
 */
function PreviewStep({ templateId, download, dirty, saving, canManage, onSave, onBack, onNext }: {
  templateId: string;
  download: boolean;
  dirty: boolean;
  saving: boolean;
  canManage: boolean;
  onSave: () => Promise<boolean>;
  onBack: () => void;
  onNext: () => void;
}) {
  const [url, setUrl] = useState<string | null>(null);
  const [state, setState] = useState<"loading" | "ready" | "failed">("loading");
  const [downloading, setDownloading] = useState(false);

  useEffect(() => {
    let cancelled = false;
    let objectUrl: string | null = null;

    async function load() {
      setState("loading");
      if (dirty && canManage && !(await onSave())) {
        if (!cancelled) setState("failed");
        return;
      }
      try {
        const response = await fetch(`/api/certificate-templates/${templateId}/preview?format=png`,
          { cache: "no-store" });
        if (!response.ok) throw new Error("preview");
        objectUrl = URL.createObjectURL(await response.blob());
        if (!cancelled) { setUrl(objectUrl); setState("ready"); }
      } catch {
        if (!cancelled) setState("failed");
      }
    }
    void load();

    return () => {
      cancelled = true;
      if (objectUrl) URL.revokeObjectURL(objectUrl);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [templateId]);

  async function saveCopy() {
    setDownloading(true);
    try {
      const response = await fetch(`/api/certificate-templates/${templateId}/preview?format=pdf`,
        { cache: "no-store" });
      if (!response.ok) return;
      const blob = await response.blob();
      const href = URL.createObjectURL(blob);
      const link = document.createElement("a");
      link.href = href;
      link.download = "certificate-sample.pdf";
      link.click();
      URL.revokeObjectURL(href);
    } finally {
      setDownloading(false);
    }
  }

  return (
    <section className="space-y-5">
      <div>
        <h2 className="text-xl font-bold text-text">
          {download ? "Download your certificate" : "Preview your certificate"}
        </h2>
        <p className="mt-1 text-base text-muted">
          {download
            ? "Save a sample to your computer to check or share."
            : "Check your certificate before you finish. This is exactly how it will print."}
        </p>
      </div>

      <div className="flex min-h-[320px] items-center justify-center rounded-2xl border border-border bg-elevated p-6">
        {state === "loading" ? (
          <p role="status" className="text-base text-muted">
            {saving ? "Saving your changes…" : "Getting your certificate ready…"}
          </p>
        ) : state === "failed" ? (
          <div className="text-center">
            <span aria-hidden className="block text-5xl leading-none">😕</span>
            <p className="mt-4 text-lg font-bold text-text">We couldn’t show your certificate</p>
            <p className="mt-1 text-base text-muted">Please check your connection and try again.</p>
            <Button type="button" size="lg" className="mt-5" onClick={onBack}>Back to Edit</Button>
          </div>
        ) : (
          // eslint-disable-next-line @next/next/no-img-element
          <img src={url ?? ""} alt="Your certificate, filled in with example details"
            className="h-auto w-full max-w-3xl rounded-lg shadow-lg" />
        )}
      </div>

      <p className="text-sm text-muted">
        <span aria-hidden className="mr-1">ℹ️</span>
        Example details are shown. Each certificate is filled in from your participant list.
      </p>

      <div className="flex flex-wrap items-center gap-3 border-t border-border pt-5">
        <Button type="button" size="lg" variant="secondary" onClick={onBack}>
          <span aria-hidden className="mr-2 text-lg">←</span>
          Back to Edit
        </Button>

        {download ? (
          <Button type="button" size="xl" disabled={state !== "ready" || downloading} onClick={saveCopy}>
            <span aria-hidden className="mr-2 text-lg">⬇</span>
            {downloading ? "Downloading…" : "Download PDF"}
          </Button>
        ) : (
          <Button type="button" size="xl" disabled={state !== "ready"} onClick={onNext}>
            <span aria-hidden className="mr-2 text-lg">⬇</span>
            Looks good — Download
          </Button>
        )}
      </div>
    </section>
  );
}
