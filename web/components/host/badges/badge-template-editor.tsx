"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { Alert, Button, Card, Checkbox, Field, Select, Spinner } from "@kurx/ui";
import { BadgeCanvas, FIELD_LABELS } from "@/components/host/badges/badge-canvas";
import {
  BADGE_FIELD_KEYS, getBadgeFieldDefaults, getIdCardTemplate, idCardAssetUrl, presignIdCardAsset,
  previewIdCardTemplate, saveIdCardTemplate,
  type BadgeField, type BadgeFieldKey, type BadgeSize, type IdCardTemplate
} from "@/lib/badge-api";

type Props = { eventId: string; accessToken: string; sizes: BadgeSize[] };
type Kind = "attendee" | "staff";

/** Canvas width in CSS pixels. Height follows the card's real aspect, so the shape on screen is the
 *  shape that prints. */
const CANVAS_W = 300;

/**
 * The ID card designer (D-362) — upload artwork, drag fields onto it, preview, save.
 *
 * Two surfaces on purpose. The **canvas** is fast and approximate: the browser's fonts, positioned
 * exactly. The **preview** is the truth: rendered by the server through the same engine that prints. A
 * design tool with only the first lies about the output; one with only the second is unusable to drag on.
 */
export function BadgeTemplateEditor({ eventId, accessToken, sizes }: Props) {
  const [spec, setSpec] = useState<IdCardTemplate | null>(null);
  const [fields, setFields] = useState<BadgeField[]>([]);
  const [selected, setSelected] = useState<BadgeFieldKey | null>(null);
  const [kind, setKind] = useState<Kind>("attendee");
  const [artworkUrl, setArtworkUrl] = useState<string | null>(null);
  const [preview, setPreview] = useState<string | null>(null);
  const [previewing, setPreviewing] = useState(false);
  const [saving, setSaving] = useState(false);
  const [notice, setNotice] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const canvasRef = useRef<HTMLDivElement | null>(null);
  const previousPreviewUrl = useRef<string | null>(null);
  // The defaults last seeded onto the canvas, and the size they were for. Changing the card size
  // re-seeds only while the layout is still untouched: once an organiser has moved something, their
  // placements are theirs, and percentages carry across sizes by design.
  const defaultSeed = useRef<{ sizeKey: string; json: string } | null>(null);
  const drag = useRef<{ key: BadgeFieldKey; mode: "move" | "resize"; startX: number; startY: number; field: BadgeField } | null>(null);

  const size = sizes.find((s) => s.key === spec?.sizeKey) ?? sizes[0];
  const canvasH = size ? Math.round(CANVAS_W * (size.height_mm / size.width_mm)) : CANVAS_W;

  // ── Load ────────────────────────────────────────────────────────────────────────────────────
  useEffect(() => {
    getIdCardTemplate(accessToken, eventId)
      .then(async (loaded) => {
        setSpec(loaded);
        // A saved design with no fields means "the built-in layout", so the canvas seeds from the
        // server's own defaults (D-385). Rendering it as an empty card would show a badge the server
        // would never print, leave every checkbox unticked against a card that has all of them, and turn
        // the first tick into a one-field layout that replaces the whole default — silently dropping the
        // QR, which is the credential.
        const seeded = loaded.fields
          ?? await getBadgeFieldDefaults(accessToken, eventId, loaded.sizeKey, "staff").catch(() => []);
        setFields(seeded);
        defaultSeed.current = { sizeKey: loaded.sizeKey, json: JSON.stringify(seeded) };
        if (loaded.backgroundKey) {
          setArtworkUrl(await idCardAssetUrl(accessToken, eventId, loaded.backgroundKey).catch(() => null));
        }
      })
      .catch(() => setError("The card design couldn't be loaded. Nothing has been changed."));
  }, [accessToken, eventId]);

  // ── Preview ─────────────────────────────────────────────────────────────────────────────────
  const refreshPreview = useCallback(
    async (current: IdCardTemplate, currentFields: BadgeField[], forKind: Kind) => {
      setPreviewing(true);
      try {
        const blob = await previewIdCardTemplate(
          accessToken, eventId, { ...current, fields: currentFields.length ? currentFields : null }, forKind
        );
        const url = URL.createObjectURL(blob);
        if (previousPreviewUrl.current) URL.revokeObjectURL(previousPreviewUrl.current);
        previousPreviewUrl.current = url;
        setPreview(url);
      } catch {
        setError("The preview couldn't be rendered. Your changes are still here.");
      } finally {
        setPreviewing(false);
      }
    },
    [accessToken, eventId]
  );

  // Debounced: dragging fires continuously, and each change is a server render.
  useEffect(() => {
    if (!spec) return;
    const t = setTimeout(() => void refreshPreview(spec, fields, kind), 400);
    return () => clearTimeout(t);
  }, [spec, fields, kind, refreshPreview]);

  useEffect(() => () => {
    if (previousPreviewUrl.current) URL.revokeObjectURL(previousPreviewUrl.current);
  }, []);

  // ── Dragging ────────────────────────────────────────────────────────────────────────────────
  useEffect(() => {
    function onMove(e: PointerEvent) {
      const d = drag.current;
      const box = canvasRef.current?.getBoundingClientRect();
      if (!d || !box) return;

      // Deltas as percentages of the card, which is the unit everything is stored in — so no scale
      // factor survives past this line.
      const dx = ((e.clientX - d.startX) / box.width) * 100;
      const dy = ((e.clientY - d.startY) / box.height) * 100;

      setFields((current) =>
        current.map((f) => {
          if (f.key !== d.key) return f;
          return d.mode === "move"
            ? { ...f, x: clamp(d.field.x + dx, 0, 99), y: clamp(d.field.y + dy, 0, 99) }
            : { ...f, width: clamp(d.field.width + dx, 2, 100), height: clamp(d.field.height + dy, 2, 100) };
        })
      );
    }
    function onUp() {
      drag.current = null;
    }
    window.addEventListener("pointermove", onMove);
    window.addEventListener("pointerup", onUp);
    return () => {
      window.removeEventListener("pointermove", onMove);
      window.removeEventListener("pointerup", onUp);
    };
  }, []);

  if (error && !spec) return <Alert tone="danger">{error}</Alert>;
  if (!spec || !size) return <Card><Spinner size={20} /></Card>;

  const set = (patch: Partial<IdCardTemplate>) => {
    setSpec({ ...spec, ...patch });
    setNotice(null);
  };

  const patchField = (key: BadgeFieldKey, patch: Partial<BadgeField>) =>
    setFields((current) => current.map((f) => (f.key === key ? { ...f, ...patch } : f)));

  /**
   * CR80 is the one landscape size and has its own arrangement, so switching to or from it re-seeds the
   * canvas from that size's built-in layout — but only while the organiser has not moved anything. Their
   * own placements are never overwritten by a size change.
   */
  async function changeSize(sizeKey: string) {
    set({ sizeKey });
    const untouched = defaultSeed.current !== null && defaultSeed.current.json === JSON.stringify(fields);
    if (!untouched) return;
    const seeded = await getBadgeFieldDefaults(accessToken, eventId, sizeKey, "staff").catch(() => null);
    if (!seeded) return;
    setFields(seeded);
    defaultSeed.current = { sizeKey, json: JSON.stringify(seeded) };
  }

  function toggleField(key: BadgeFieldKey, on: boolean) {
    setFields((current) => {
      const existing = current.find((f) => f.key === key);
      if (existing) return current.map((f) => (f.key === key ? { ...f, enabled: on } : f));
      // Newly added fields land in the middle, where they are visible and obviously need placing.
      return [...current, {
        key, x: 30, y: 45, width: 40, height: 8,
        fontSizePt: 10, color: null, align: "center", weight: null, zOrder: 3, enabled: on
      }];
    });
    setSelected(key);
  }

  async function upload(file: File, purpose: "background" | "logo") {
    setError(null);
    try {
      const presign = await presignIdCardAsset(accessToken, eventId, file.type, purpose);
      const res = await fetch(presign.url, { method: "PUT", headers: presign.headers, body: file });
      if (!res.ok) throw new Error("upload failed");

      if (purpose === "background") {
        set({ backgroundKey: presign.key });
        setArtworkUrl(await idCardAssetUrl(accessToken, eventId, presign.key).catch(() => null));
      } else {
        set({ logoKey: presign.key });
      }
    } catch {
      setError(`The ${purpose} couldn't be uploaded. PNG, JPEG or WebP.`);
    }
  }

  async function save() {
    // Re-narrowed inside the closure: TypeScript does not carry the component-body null check into an
    // async function, so spreading `spec` here would type every property as optional.
    if (!spec) return;
    setSaving(true);
    setError(null);
    try {
      const saved = await saveIdCardTemplate(accessToken, eventId, {
        ...spec,
        fields: fields.length ? fields : null
      });
      setSpec(saved);
      setFields(saved.fields ?? []);
      setNotice("Design saved. Re-issue cards to print them with it.");
    } catch {
      setError("The design couldn't be saved.");
    } finally {
      setSaving(false);
    }
  }

  const selectedField = fields.find((f) => f.key === selected) ?? null;

  return (
    <Card>
      <div className="space-y-4">
        <div>
          <h2 className="text-sm font-semibold text-text">Card design</h2>
          <p className="mt-1 text-xs text-muted">
            Upload your card artwork, then drag the fields onto it. The preview on the right is rendered by
            the server with the same engine that prints, so it is exactly what you get.
          </p>
        </div>

        <div className="grid gap-6 lg:grid-cols-[auto_auto_minmax(0,1fr)]">
          {/* Canvas */}
          <div className="space-y-2">
            <p className="text-xs font-medium text-muted">Layout — drag to move, corner to resize</p>
            <div ref={canvasRef}>
              <BadgeCanvas
                fields={fields}
                width={CANVAS_W}
                height={canvasH}
                backgroundUrl={artworkUrl}
                selectedKey={selected}
                onSelect={setSelected}
                onBackgroundClick={() => setSelected(null)}
                onPointerDown={(e, key, mode) => {
                  const field = fields.find((f) => f.key === key);
                  if (!field) return;
                  e.preventDefault();
                  setSelected(key);
                  drag.current = { key, mode, startX: e.clientX, startY: e.clientY, field };
                }}
              />
            </div>
            {fields.length === 0 && (
              <p className="max-w-[300px] text-xs text-muted">
                The built-in layout couldn&apos;t be loaded, so the card is showing empty. It still prints
                correctly — the server falls back to its own layout. Reload to try again.
              </p>
            )}
          </div>

          {/* Preview */}
          <div className="space-y-2">
            <div className="flex items-center gap-2">
              <p className="text-xs font-medium text-muted">Preview</p>
              {previewing && <Spinner size={12} />}
            </div>
            <div
              className="flex items-center justify-center rounded-lg border border-border bg-surface-2 p-3"
              style={{ width: CANVAS_W + 24, minHeight: canvasH }}
            >
              {preview ? (
                // eslint-disable-next-line @next/next/no-img-element -- blob: URL cannot use next/image
                <img src={preview} alt="Card preview" className="w-full shadow-lg" />
              ) : (
                <p className="text-xs text-muted">Rendering…</p>
              )}
            </div>
            <Select value={kind} onChange={(e) => setKind(e.target.value as Kind)} aria-label="Preview which card">
              <option value="attendee">Attendee card</option>
              <option value="staff">Staff card</option>
            </Select>
          </div>

          {/* Controls */}
          <div className="space-y-5">
            <Field label="Card size">
              <Select value={spec.sizeKey} onChange={(e) => void changeSize(e.target.value)}>
                {sizes.map((s) => (
                  <option key={s.key} value={s.key}>{s.label}</option>
                ))}
              </Select>
            </Field>

            <fieldset className="space-y-2">
              <legend className="text-sm font-medium text-text">Fields on the card</legend>
              {BADGE_FIELD_KEYS.map((key) => (
                <Checkbox
                  key={key}
                  checked={Boolean(fields.find((f) => f.key === key)?.enabled)}
                  onChange={(e) => toggleField(key, e.target.checked)}
                  label={FIELD_LABELS[key]}
                />
              ))}
            </fieldset>

            {selectedField && (
              <div className="space-y-3 rounded-md border border-border p-3">
                <p className="text-sm font-medium text-text">{FIELD_LABELS[selectedField.key]}</p>
                <div className="grid grid-cols-2 gap-3">
                  <Field label="Size (pt)">
                    <input
                      type="number" min={4} max={72}
                      value={selectedField.fontSizePt ?? 10}
                      onChange={(e) => patchField(selectedField.key, { fontSizePt: Number(e.target.value) })}
                      className="h-9 w-full rounded-md border border-border bg-surface px-2 text-sm text-text"
                    />
                  </Field>
                  <Field label="Align">
                    <Select
                      value={selectedField.align}
                      onChange={(e) => patchField(selectedField.key, { align: e.target.value })}
                    >
                      <option value="left">Left</option>
                      <option value="center">Center</option>
                      <option value="right">Right</option>
                    </Select>
                  </Field>
                </div>
                <div className="flex items-center gap-3">
                  <Checkbox
                    checked={selectedField.weight === "bold"}
                    onChange={(e) => patchField(selectedField.key, { weight: e.target.checked ? "bold" : "normal" })}
                    label="Bold"
                  />
                  <input
                    type="color"
                    value={selectedField.color ?? "#111827"}
                    onChange={(e) => patchField(selectedField.key, { color: e.target.value })}
                    className="h-9 w-14 cursor-pointer rounded-md border border-border bg-surface"
                    aria-label="Field colour"
                  />
                </div>
              </div>
            )}

            <div className="grid gap-4 sm:grid-cols-2">
              <Field label="Accent colour">
                <input
                  type="color"
                  value={spec.accentColor ?? "#111827"}
                  onChange={(e) => set({ accentColor: e.target.value })}
                  className="h-10 w-full cursor-pointer rounded-md border border-border bg-surface"
                  aria-label="Accent colour"
                />
              </Field>
              <Field label="Name colour">
                <input
                  type="color"
                  value={spec.textColor ?? "#111827"}
                  onChange={(e) => set({ textColor: e.target.value })}
                  className="h-10 w-full cursor-pointer rounded-md border border-border bg-surface"
                  aria-label="Name colour"
                />
              </Field>
            </div>

            <Field label="Card artwork" helper="Your designed card. Everything else sits on top of it.">
              <div className="flex flex-wrap items-center gap-3">
                <input
                  type="file" accept="image/png,image/jpeg,image/webp"
                  onChange={(e) => {
                    const f = e.target.files?.[0];
                    if (f) void upload(f, "background");
                  }}
                  className="text-sm text-muted file:mr-3 file:rounded-md file:border-0 file:bg-surface-3 file:px-3 file:py-2 file:text-sm file:text-text"
                />
                {spec.backgroundKey && (
                  <Button variant="ghost" onClick={() => { set({ backgroundKey: null }); setArtworkUrl(null); }}>
                    Remove
                  </Button>
                )}
              </div>
            </Field>

            <Field label="Logo" helper="Optional mark, placed as a field.">
              <div className="flex flex-wrap items-center gap-3">
                <input
                  type="file" accept="image/png,image/jpeg,image/webp"
                  onChange={(e) => {
                    const f = e.target.files?.[0];
                    if (f) void upload(f, "logo");
                  }}
                  className="text-sm text-muted file:mr-3 file:rounded-md file:border-0 file:bg-surface-3 file:px-3 file:py-2 file:text-sm file:text-text"
                />
                {spec.logoKey && (
                  <Button variant="ghost" onClick={() => set({ logoKey: null })}>Remove</Button>
                )}
              </div>
            </Field>

            {notice && <Alert tone="success">{notice}</Alert>}
            {error && <Alert tone="danger">{error}</Alert>}

            <Button onClick={save} disabled={saving}>
              {saving ? <Spinner size={16} /> : null}
              {saving ? "Saving…" : "Save design"}
            </Button>
          </div>
        </div>
      </div>
    </Card>
  );
}

function clamp(v: number, lo: number, hi: number) {
  return Math.min(hi, Math.max(lo, v));
}
