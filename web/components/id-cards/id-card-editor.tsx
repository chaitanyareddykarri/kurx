"use client";

import { useMemo, useState, useTransition } from "react";
import { Lock, RotateCcw, Save, Download, Printer } from "lucide-react";
import { Badge, Button, Field, Input, Select, useToast } from "@kurx/ui";
import type { IdCard } from "@/lib/api";
import { saveIdCardAction, generateIdCardAction } from "@/lib/id-card-actions";

/** Layout knobs the holder may move. Held as a small named set rather than free-form geometry: the
 * card is a fixed CR80 rectangle whose fields the renderer lays out, so "reposition" here means
 * nudging the two elements that are actually positional (photo and QR) and scaling them — not a
 * free canvas, which would let a holder push their own name off a printed card. */
export type CardLayout = {
  photoScale: number;
  photoOffsetY: number;
  qrScale: number;
  showPhone: boolean;
  showEmail: boolean;
  showAddress: boolean;
};

const DEFAULT_LAYOUT: CardLayout = {
  photoScale: 100, photoOffsetY: 0, qrScale: 100,
  showPhone: true, showEmail: true, showAddress: false,
};

const TEMPLATES = [
  { value: "StandardCollege", label: "Standard College" },
  { value: "ModernCollege", label: "Modern College" },
  { value: "TechFest", label: "Tech Fest" },
  { value: "CulturalFest", label: "Cultural Fest" },
  { value: "EventParticipant", label: "Event Participant" },
  { value: "StaffFaculty", label: "Staff / Faculty" },
  { value: "Volunteer", label: "Volunteer" },
] as const;

/** Mirrors IdCardRenderer.PaletteFor so the preview is the card, not an impression of it. Kept as a
 * literal map for the same reason the server does: adding a template must force a colour decision. */
const PALETTE: Record<string, { accent: string; rail: boolean }> = {
  StandardCollege: { accent: "#1D4ED8", rail: false },
  ModernCollege: { accent: "#0F172A", rail: true },
  TechFest: { accent: "#4338CA", rail: true },
  CulturalFest: { accent: "#BE185D", rail: true },
  EventParticipant: { accent: "#047857", rail: false },
  StaffFaculty: { accent: "#7C2D12", rail: false },
  Volunteer: { accent: "#B45309", rail: false },
};

type HolderFields = {
  bloodGroup: string;
  address: string;
  emergencyContactName: string;
  emergencyContactPhone: string;
};

export function IdCardEditor({ card, holderPhone, holderEmail, holderDob }: {
  card: IdCard;
  holderPhone: string | null;
  holderEmail: string | null;
  holderDob: string | null;
}) {
  const toast = useToast();
  const [pending, startTransition] = useTransition();

  // The server is the origin of truth for "original". Restore reverts to what was loaded, not to a
  // hardcoded blank — otherwise "restore original values" would destroy data the holder had saved
  // in an earlier session, which is the opposite of what the button promises.
  const original = useMemo(() => ({
    template: card.template,
    layout: DEFAULT_LAYOUT,
    fields: { bloodGroup: "", address: "", emergencyContactName: "", emergencyContactPhone: "" } as HolderFields,
  }), [card.template]);

  const [template, setTemplate] = useState(original.template);
  const [layout, setLayout] = useState<CardLayout>(original.layout);
  const [fields, setFields] = useState<HolderFields>(original.fields);
  const [dirty, setDirty] = useState(false);

  const palette = PALETTE[template] ?? PALETTE.StandardCollege;
  const locked = card.is_revoked;

  function edit<K extends keyof HolderFields>(key: K, value: string) {
    setFields((f) => ({ ...f, [key]: value }));
    setDirty(true);
  }

  function nudge<K extends keyof CardLayout>(key: K, value: CardLayout[K]) {
    setLayout((l) => ({ ...l, [key]: value }));
    setDirty(true);
  }

  function restore() {
    setTemplate(original.template);
    setLayout(original.layout);
    setFields(original.fields);
    setDirty(false);
    toast("Restored the values this card was issued with.", "info");
  }

  function save(thenGenerate: boolean) {
    startTransition(async () => {
      const res = await saveIdCardAction(card.id, {
        template,
        layoutJson: JSON.stringify(layout),
        bloodGroup: fields.bloodGroup || undefined,
        address: fields.address || undefined,
        emergencyContactName: fields.emergencyContactName || undefined,
        emergencyContactPhone: fields.emergencyContactPhone || undefined,
      });
      if (!res.ok) { toast(res.error, "error"); return; }
      setDirty(false);

      if (!thenGenerate) { toast("Saved. Your changes apply to this card only.", "success"); return; }

      const gen = await generateIdCardAction(card.id);
      gen.ok
        ? toast("Card generated. The PDF and image are ready to download.", "success")
        : toast(gen.error, "error");
    });
  }

  return (
    <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_minmax(0,420px)]">
      {/* ── Controls ─────────────────────────────────────────────────────────────────────── */}
      <div className="space-y-6 lg:order-1">
        <section className="rounded-lg border border-border bg-surface p-4">
          <h2 className="text-sm font-semibold text-text">Template</h2>
          <p className="mt-1 text-xs text-muted">Changes the card&apos;s layout and colour. Every template shows the same fields.</p>
          <div className="mt-3">
            <Field label="Card template" htmlFor="template">
              <Select
                id="template"
                value={template}
                disabled={locked}
                onChange={(e) => { setTemplate(e.target.value); setDirty(true); }}
              >
                {TEMPLATES.map((t) => <option key={t.value} value={t.value}>{t.label}</option>)}
              </Select>
            </Field>
          </div>
        </section>

        {/* The asserted block. Shown, never editable — this is D-331 surfacing in the UI rather than
            hiding as a 403. A holder who can see WHY the field is locked asks their college, not us. */}
        <section className="rounded-lg border border-border bg-surface p-4">
          <div className="flex items-center gap-2">
            <Lock size={14} className="text-muted" aria-hidden />
            <h2 className="text-sm font-semibold text-text">Issued by {card.org_name}</h2>
          </div>
          <p className="mt-1 text-xs text-muted">
            Your college sets these. They can&apos;t be edited here — that&apos;s what makes the card
            proof of anything. Contact {card.org_name} if something is wrong.
          </p>
          <dl className="mt-3 grid grid-cols-2 gap-x-4 gap-y-2 text-sm">
            <Asserted label="Student ID" value={card.student_id} />
            <Asserted label="Department" value={card.department} />
            <Asserted label="Course" value={card.course} />
            <Asserted label="Year" value={card.year} />
            <Asserted label="Valid from" value={card.valid_from} />
            <Asserted label="Valid until" value={card.valid_until} />
          </dl>
        </section>

        <section className="rounded-lg border border-border bg-surface p-4">
          <h2 className="text-sm font-semibold text-text">Your details</h2>
          <p className="mt-1 text-xs text-muted">
            Optional, and yours to give. These print on the card but are never shown on the public
            verification page.
          </p>
          <div className="mt-3 grid gap-3 sm:grid-cols-2">
            <Field label="Blood group" htmlFor="bloodGroup" helper="Printed for medical staff.">
              <Input id="bloodGroup" value={fields.bloodGroup} disabled={locked}
                onChange={(e) => edit("bloodGroup", e.target.value)} placeholder="O+" />
            </Field>
            <Field label="Emergency contact name" htmlFor="ecName">
              <Input id="ecName" value={fields.emergencyContactName} disabled={locked}
                onChange={(e) => edit("emergencyContactName", e.target.value)} />
            </Field>
            <Field label="Emergency contact phone" htmlFor="ecPhone">
              <Input id="ecPhone" value={fields.emergencyContactPhone} disabled={locked}
                onChange={(e) => edit("emergencyContactPhone", e.target.value)} />
            </Field>
            <Field label="Address" htmlFor="address">
              <Input id="address" value={fields.address} disabled={locked}
                onChange={(e) => edit("address", e.target.value)} />
            </Field>
          </div>
        </section>

        <section className="rounded-lg border border-border bg-surface p-4">
          <h2 className="text-sm font-semibold text-text">Layout</h2>
          <p className="mt-1 text-xs text-muted">Adjusts this card only. Sliders rather than dragging, so it works the same on a phone.</p>
          <div className="mt-3 space-y-4">
            <Slider label="Photo size" value={layout.photoScale} min={70} max={130} disabled={locked}
              onChange={(v) => nudge("photoScale", v)} />
            <Slider label="Photo position" value={layout.photoOffsetY} min={-10} max={10} disabled={locked}
              onChange={(v) => nudge("photoOffsetY", v)} suffix="px" />
            <Slider label="QR size" value={layout.qrScale} min={70} max={130} disabled={locked}
              onChange={(v) => nudge("qrScale", v)} />
            <fieldset className="space-y-2">
              <legend className="text-xs font-medium text-muted">Show on card</legend>
              <Toggle label="Phone number" checked={layout.showPhone} disabled={locked}
                onChange={(v) => nudge("showPhone", v)} />
              <Toggle label="Email address" checked={layout.showEmail} disabled={locked}
                onChange={(v) => nudge("showEmail", v)} />
              <Toggle label="Address" checked={layout.showAddress} disabled={locked}
                onChange={(v) => nudge("showAddress", v)} />
            </fieldset>
          </div>
        </section>

        <div className="flex flex-wrap gap-2">
          <Button onClick={() => save(false)} disabled={locked || pending || !dirty}>
            <Save size={14} aria-hidden /> Save
          </Button>
          <Button variant="primary" onClick={() => save(true)} disabled={locked || pending}>
            Save &amp; generate
          </Button>
          <Button variant="secondary" onClick={restore} disabled={locked || pending || !dirty}>
            <RotateCcw size={14} aria-hidden /> Restore original
          </Button>
        </div>

        {card.is_revoked && (
          <p role="status" className="rounded-lg border border-dashed border-border bg-surface p-3 text-sm text-muted">
            This card was revoked{card.revoked_reason ? `: ${card.revoked_reason}` : ""}. It can&apos;t be
            edited or regenerated — ask {card.org_name} to issue a replacement.
          </p>
        )}
      </div>

      {/* ── Live preview ────────────────────────────────────────────────────────────────── */}
      <div className="lg:order-2">
        <div className="lg:sticky lg:top-6 space-y-3">
          <div className="flex items-center justify-between">
            <h2 className="text-sm font-semibold text-text">Preview</h2>
            <Badge tone={card.status === "active" ? "success" : card.status === "revoked" ? "danger" : "neutral"}>
              {card.status}
            </Badge>
          </div>

          <CardPreview
            card={card} template={template} layout={layout} fields={fields}
            accent={palette.accent} rail={palette.rail}
            phone={holderPhone} email={holderEmail} dob={holderDob}
          />

          <p className="text-xs text-muted">
            Preview only. The downloadable card is rendered on the server, so it is what actually prints.
          </p>

          <div className="flex flex-wrap gap-2">
            <Button variant="secondary" disabled={!card.pdf_url}
              onClick={() => card.pdf_url && window.open(card.pdf_url, "_blank", "noopener")}>
              <Download size={14} aria-hidden /> PDF
            </Button>
            <Button variant="secondary" disabled={!card.png_url}
              onClick={() => card.png_url && window.open(card.png_url, "_blank", "noopener")}>
              <Download size={14} aria-hidden /> Image
            </Button>
            <Button variant="ghost" disabled={!card.pdf_url} onClick={() => window.print()}>
              <Printer size={14} aria-hidden /> Print
            </Button>
          </div>
          {!card.pdf_url && (
            <p className="text-xs text-muted">Generate the card to enable downloads.</p>
          )}
        </div>
      </div>
    </div>
  );
}

function Asserted({ label, value }: { label: string; value: string | null }) {
  return (
    <div>
      <dt className="text-xs text-muted">{label}</dt>
      <dd className="text-sm text-text">{value ?? <span className="text-muted">—</span>}</dd>
    </div>
  );
}

function Slider({ label, value, min, max, onChange, disabled, suffix }: {
  label: string; value: number; min: number; max: number;
  onChange: (v: number) => void; disabled?: boolean; suffix?: string;
}) {
  const id = `slider-${label.replace(/\s+/g, "-").toLowerCase()}`;
  return (
    <div>
      <div className="flex items-center justify-between">
        <label htmlFor={id} className="text-xs font-medium text-muted">{label}</label>
        <span className="text-xs tabular-nums text-muted">{value}{suffix ?? "%"}</span>
      </div>
      <input
        id={id} type="range" min={min} max={max} value={value} disabled={disabled}
        onChange={(e) => onChange(Number(e.target.value))}
        className="mt-1 w-full accent-[var(--color-accent,#1D4ED8)]"
      />
    </div>
  );
}

function Toggle({ label, checked, onChange, disabled }: {
  label: string; checked: boolean; onChange: (v: boolean) => void; disabled?: boolean;
}) {
  return (
    <label className="flex items-center gap-2 text-sm text-text">
      <input type="checkbox" checked={checked} disabled={disabled}
        onChange={(e) => onChange(e.target.checked)} className="size-4 rounded border-border" />
      {label}
    </label>
  );
}

/** A CR80-proportioned preview (85.6 × 54 mm ≈ 1.585:1). Proportions are held by aspect-ratio and
 * everything inside scales with container queries rather than fixed px, so the same markup is legible
 * on a phone and sharp on a desktop without a second mobile layout. */
function CardPreview({ card, template, layout, fields, accent, rail, phone, email, dob }: {
  card: IdCard; template: string; layout: CardLayout; fields: HolderFields;
  accent: string; rail: boolean; phone: string | null; email: string | null; dob: string | null;
}) {
  const rows: Array<[string, string | null]> = [
    ["ID", card.student_id],
    ["Dept", card.department],
    ["Course", [card.course, card.year].filter(Boolean).join(" · ") || null],
    ["DOB", dob],
    ["Blood", fields.bloodGroup || null],
    ["Phone", layout.showPhone ? phone : null],
    ["Email", layout.showEmail ? email : null],
    ["Address", layout.showAddress ? fields.address || null : null],
  ];
  const emergency = [fields.emergencyContactName, fields.emergencyContactPhone].filter(Boolean).join(" · ");

  const validity =
    card.valid_from && card.valid_until ? `${card.valid_from} – ${card.valid_until}`
    : card.valid_until ? `Valid until ${card.valid_until}`
    : card.valid_from ? `Valid from ${card.valid_from}`
    : "No expiry";

  return (
    <div
      className="w-full overflow-hidden rounded-lg border border-border bg-white text-black shadow-sm"
      style={{ aspectRatio: "85.6 / 54" }}
      aria-label={`Preview of ${card.holder_name}'s ID card`}
    >
      <div className={rail ? "flex h-full" : "flex h-full flex-col"}>
        {rail ? (
          <div className="flex w-[18%] shrink-0 flex-col justify-between p-[3%] text-white" style={{ background: accent }}>
            <span className="text-[2.6cqw] font-semibold leading-tight">{card.org_name}</span>
            <span className="text-[2.2cqw] opacity-90">{card.card_number}</span>
          </div>
        ) : (
          <div className="flex items-center gap-[2%] px-[4%] py-[2.5%] text-white" style={{ background: accent }}>
            <span className="truncate text-[3.2cqw] font-semibold">{card.org_name}</span>
          </div>
        )}

        <div className="flex min-w-0 flex-1 gap-[3%] p-[3.5%]">
          <div className="flex w-[22%] shrink-0 flex-col items-center gap-[6%]">
            <div
              className="w-full overflow-hidden rounded bg-neutral-200"
              style={{ aspectRatio: "3 / 4", transform: `scale(${layout.photoScale / 100}) translateY(${layout.photoOffsetY}px)` }}
            >
              {card.photo_url
                // Not decorative: this is the photograph that will print on the card, and a reader
                // checking their own preview needs to know whether one is there at all.
                // eslint-disable-next-line @next/next/no-img-element -- presigned URL, arbitrary host
                ? <img src={card.photo_url} alt={`Photograph of ${card.holder_name}`} className="size-full object-cover" />
                : null}
            </div>
            <div className="aspect-square w-[78%] bg-neutral-900" style={{ transform: `scale(${layout.qrScale / 100})` }} aria-hidden />
          </div>

          <div className="flex min-w-0 flex-1 flex-col">
            <p className="truncate text-[4.2cqw] font-bold leading-tight">{card.holder_name}</p>
            <dl className="mt-[2%] space-y-[1%]">
              {rows.filter(([, v]) => v).map(([k, v]) => (
                <div key={k} className="flex gap-[3%] text-[2.4cqw] leading-tight">
                  <dt className="shrink-0 text-neutral-500">{k}</dt>
                  <dd className="truncate">{v}</dd>
                </div>
              ))}
              {emergency && (
                <div className="flex gap-[3%] text-[2.4cqw] leading-tight">
                  <dt className="shrink-0 text-neutral-500">ICE</dt>
                  <dd className="truncate">{emergency}</dd>
                </div>
              )}
            </dl>
            <p className="mt-auto text-[2.2cqw] text-neutral-500">{validity}</p>
          </div>
        </div>
      </div>
    </div>
  );
}
