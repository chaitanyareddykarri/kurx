"use client";

import { useState } from "react";
import { Button } from "@kurx/ui";
import type { DraftField } from "@/lib/certificate-editor";
import { CANONICAL_FIELDS, fieldLabel, fieldSample, toKey } from "@/lib/certificate-fields";

/**
 * The text on the certificate, as a list of things you can type into (D-359).
 *
 * This replaces a layer list. A layer list answers "what objects exist and in what order", which is a
 * question a designer asks and nobody else — and answering it put z-order, masking and rotation in front
 * of someone who wanted to correct a spelling. What people actually arrive wanting is: *change these
 * words*. So every piece of fixed text is simply a labelled box with the words in it, and typing in the
 * box changes the certificate.
 *
 * The two kinds are kept plainly apart, in words rather than by styling:
 *
 *   - **Changes for each person** — filled in from the participant list. There is nothing to type here, so
 *     no box is offered; showing an empty input for a value that arrives later invites someone to type a
 *     single name into it and send it to two hundred people.
 *   - **The same on every certificate** — a box with the words, and typing replaces them.
 */
export function CertificateTextPanel({
  fields, selectedId, canManage, onSelect, onChangeText, onDelete, onAdd
}: {
  fields: DraftField[];
  selectedId: string | null;
  canManage: boolean;
  onSelect: (id: string) => void;
  onChangeText: (id: string, text: string) => void;
  onDelete: (id: string) => void;
  onAdd: (fieldKey: string | null, label: string) => void;
}) {
  const perPerson = fields.filter((f) => f.kind === "dynamicfield");
  const fixed = fields.filter((f) => f.kind === "text");
  const other = fields.filter((f) => f.kind !== "dynamicfield" && f.kind !== "text");

  return (
    <div className="space-y-5">
      {perPerson.length > 0 ? (
        <Group
          title="Changes for each person"
          hint="Filled in automatically from your participant list."
        >
          {perPerson.map((field) => (
            <PerPersonRow
              key={field.id}
              field={field}
              selected={field.id === selectedId}
              canManage={canManage}
              onSelect={onSelect}
              onDelete={onDelete}
            />
          ))}
        </Group>
      ) : null}

      {fixed.length > 0 ? (
        <Group
          title="The same on every certificate"
          hint="Type in any box to change the words."
        >
          {fixed.map((field) => (
            <FixedTextRow
              key={field.id}
              field={field}
              selected={field.id === selectedId}
              canManage={canManage}
              onSelect={onSelect}
              onChangeText={onChangeText}
              onDelete={onDelete}
            />
          ))}
        </Group>
      ) : null}

      {other.length > 0 ? (
        <Group title="Also on the certificate" hint="">
          {other.map((field) => (
            <PerPersonRow
              key={field.id}
              field={field}
              selected={field.id === selectedId}
              canManage={canManage}
              onSelect={onSelect}
              onDelete={onDelete}
            />
          ))}
        </Group>
      ) : null}

      {canManage ? <AddSomething onAdd={onAdd} /> : null}
    </div>
  );
}

function Group({ title, hint, children }: { title: string; hint: string; children: React.ReactNode }) {
  return (
    <section>
      <h3 className="text-base font-bold text-text">{title}</h3>
      {hint ? <p className="mt-0.5 text-sm text-muted">{hint}</p> : null}
      <div className="mt-3 space-y-3">{children}</div>
    </section>
  );
}

/** Big enough to hit, labelled in words, and never a lone icon. */
function DeleteButton({ name, onClick }: { name: string; onClick: () => void }) {
  return (
    <button
      type="button"
      onClick={onClick}
      aria-label={`Remove ${name} from the certificate`}
      title={`Remove ${name}`}
      className="flex h-11 shrink-0 items-center gap-1.5 rounded-lg border border-border px-3 text-sm font-semibold text-text hover:border-danger hover:bg-danger/10 hover:text-danger focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
    >
      <span aria-hidden className="text-lg leading-none">🗑</span>
      Remove
    </button>
  );
}

function PerPersonRow({ field, selected, canManage, onSelect, onDelete }: {
  field: DraftField;
  selected: boolean;
  canManage: boolean;
  onSelect: (id: string) => void;
  onDelete: (id: string) => void;
}) {
  const name = field.kind === "dynamicfield"
    ? fieldLabel(field.field_key, field.label)
    : field.kind === "qrcode" ? "Verification code" : "Picture";

  return (
    <div
      className={`rounded-xl border p-3 ${selected ? "border-accent bg-accent/5" : "border-border bg-surface"}`}
    >
      <div className="flex items-center gap-3">
        <button
          type="button"
          onClick={() => onSelect(field.id)}
          className="min-w-0 flex-1 text-left focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
        >
          <span className="block text-sm font-semibold text-text">{name}</span>
          <span className="block truncate text-sm text-muted">
            {field.kind === "dynamicfield"
              ? `For example: ${fieldSample(field.field_key, field.label)}`
              : field.kind === "qrcode" ? "Lets anyone check the certificate is real" : "An image"}
          </span>
        </button>
        {canManage ? <DeleteButton name={name} onClick={() => onDelete(field.id)} /> : null}
      </div>
    </div>
  );
}

function FixedTextRow({ field, selected, canManage, onSelect, onChangeText, onDelete }: {
  field: DraftField;
  selected: boolean;
  canManage: boolean;
  onSelect: (id: string) => void;
  onChangeText: (id: string, text: string) => void;
  onDelete: (id: string) => void;
}) {
  const words = field.static_text ?? "";
  // Named after what it says, so the label reads "Edit Presented to" rather than "Edit text 4". The words
  // themselves are the only name a certificate's own lines have.
  const shortName = words.trim() ? words.trim().split(/\s+/).slice(0, 4).join(" ") : "this text";
  const id = `text-${field.id}`;

  return (
    <div
      className={`rounded-xl border p-3 ${selected ? "border-accent bg-accent/5" : "border-border bg-surface"}`}
    >
      <label htmlFor={id} className="block text-sm font-semibold text-text">
        Edit “{shortName}{words.trim().split(/\s+/).length > 4 ? "…" : ""}”
      </label>

      <div className="mt-2 flex items-start gap-3">
        <textarea
          id={id}
          value={words}
          rows={Math.min(4, Math.max(1, Math.ceil(words.length / 40)))}
          disabled={!canManage}
          onFocus={() => onSelect(field.id)}
          onChange={(e) => onChangeText(field.id, e.target.value)}
          className="min-h-[44px] w-full flex-1 resize-y rounded-lg border border-border bg-background px-3 py-2.5 text-base text-text focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-accent"
        />
        {canManage ? <DeleteButton name={`“${shortName}”`} onClick={() => onDelete(field.id)} /> : null}
      </div>
    </div>
  );
}

/**
 * Adding something the design does not already say.
 *
 * Behind a single button, because it is the uncommon case: the design is read on upload, so nearly
 * everything is already there. Presenting a menu of twelve field names as the *first* thing is what made
 * people think they had to build the certificate by hand.
 */
function AddSomething({ onAdd }: { onAdd: (fieldKey: string | null, label: string) => void }) {
  const [open, setOpen] = useState(false);
  const [custom, setCustom] = useState("");

  if (!open) {
    return (
      <button
        type="button"
        onClick={() => setOpen(true)}
        className="flex h-14 w-full items-center justify-center gap-2 rounded-xl border-2 border-dashed border-border text-base font-semibold text-text hover:border-accent hover:bg-accent/5 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
      >
        <span aria-hidden className="text-2xl leading-none">＋</span>
        Add something else
      </button>
    );
  }

  return (
    <section className="rounded-xl border border-border bg-surface p-3">
      <div className="flex items-center justify-between gap-2">
        <h3 className="text-base font-bold text-text">Add something else</h3>
        <Button type="button" variant="secondary" onClick={() => setOpen(false)}>Close</Button>
      </div>
      <p className="mt-1 text-sm text-muted">Pick what it should be filled in with.</p>

      <div className="mt-3 grid gap-2 sm:grid-cols-2">
        {CANONICAL_FIELDS.map((field) => (
          <button
            key={field.key}
            type="button"
            onClick={() => { onAdd(field.key, field.label); setOpen(false); }}
            className="flex min-h-[56px] flex-col justify-center rounded-lg border border-border px-3 py-2 text-left hover:border-accent hover:bg-accent/5 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
          >
            <span className="text-sm font-semibold text-text">{field.label}</span>
            <span className="truncate text-sm text-muted">For example: {field.sample}</span>
          </button>
        ))}
      </div>

      <div className="mt-3 border-t border-border pt-3">
        <label htmlFor="custom-field" className="block text-sm font-semibold text-text">
          Something not listed
        </label>
        <div className="mt-1.5 flex gap-2">
          <input
            id="custom-field"
            value={custom}
            onChange={(e) => setCustom(e.target.value)}
            placeholder="For example: Cohort number"
            className="h-12 min-w-0 flex-1 rounded-lg border border-border bg-background px-3 text-base text-text focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-accent"
            onKeyDown={(e) => {
              if (e.key !== "Enter" || !custom.trim()) return;
              e.preventDefault();
              onAdd(toKey(custom), custom.trim());
              setCustom(""); setOpen(false);
            }}
          />
          <Button
            type="button"
            disabled={!custom.trim()}
            onClick={() => { onAdd(toKey(custom), custom.trim()); setCustom(""); setOpen(false); }}
          >
            Add
          </Button>
        </div>
      </div>
    </section>
  );
}
