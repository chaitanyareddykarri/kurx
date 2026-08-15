"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { Button } from "@kurx/ui";
import { copyToEventAction, copyToLibraryAction } from "@/lib/certificate-actions";
import type { CertificateTemplate } from "@/lib/certificate-api";

/**
 * Reusing a design across events (D-344, Phase 13).
 *
 * Both directions are described to the creator as *copying*, never as linking or sharing, because that is
 * what actually happens and the difference matters to them: a design used on last year's event and edited
 * this year must not change what last year's certificates claim to look like. Wording that implied one
 * shared design would set up exactly that surprise.
 */

/** "Keep this design for next time." Lives on an event's design. */
export function SaveToLibrary({ eventId, template }: {
  eventId: string;
  template: CertificateTemplate;
}) {
  const router = useRouter();
  const [open, setOpen] = useState(false);
  const [name, setName] = useState(template.name);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);

  if (saved) {
    return (
      <p className="text-sm text-slate-600">
        Saved to your designs. This event&apos;s copy is unchanged — the two are separate from now on.
      </p>
    );
  }

  if (!open) {
    return (
      <button type="button" onClick={() => setOpen(true)} className="text-sm underline">
        Save to my designs
      </button>
    );
  }

  return (
    <div className="space-y-3 rounded-md border border-slate-200 p-4">
      <h4 className="text-sm font-semibold text-slate-900">Save a copy to your designs</h4>
      <p className="text-sm text-slate-600">
        You&apos;ll be able to reuse it on future events. It&apos;s a copy: editing it later won&apos;t
        change this event&apos;s certificates.
      </p>
      <label className="block">
        <span className="text-sm font-medium text-slate-900">Name</span>
        <input
          value={name}
          onChange={(e) => setName(e.target.value)}
          maxLength={200}
          className="mt-1 w-full rounded border-slate-300 text-sm"
        />
      </label>
      {error && <p role="alert" className="text-sm text-red-700">{error}</p>}
      <div className="flex gap-2">
        <Button
          type="button"
          size="sm"
          disabled={busy}
          onClick={async () => {
            setBusy(true);
            setError(null);
            const result = await copyToLibraryAction(eventId, template.id, name.trim());
            setBusy(false);
            if (result.ok) { setSaved(true); router.refresh(); } else setError(result.error);
          }}
        >
          {busy ? "Saving…" : "Save a copy"}
        </Button>
        <Button type="button" size="sm" variant="secondary" disabled={busy} onClick={() => setOpen(false)}>
          Cancel
        </Button>
      </div>
    </div>
  );
}

/** "Start from one I've used before." Lives on an event's design list. */
export function UseSavedDesign({ eventId, library }: {
  eventId: string;
  library: CertificateTemplate[];
}) {
  const router = useRouter();
  const [open, setOpen] = useState(false);
  const [busy, setBusy] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  // Nothing saved yet means nothing to offer. A picker with an empty list is a dead end that has to be
  // explained; its absence explains itself.
  if (library.length === 0) return null;

  if (!open) {
    return (
      <button type="button" onClick={() => setOpen(true)} className="text-sm underline">
        Use one of my saved designs
      </button>
    );
  }

  return (
    <div className="space-y-3 rounded-md border border-slate-200 p-4">
      <h4 className="text-sm font-semibold text-slate-900">Your saved designs</h4>
      <p className="text-sm text-slate-600">
        A copy is added to this event. Your saved one stays as it is.
      </p>

      <ul className="space-y-2">
        {library.map((template) => (
          <li key={template.id} className="flex flex-wrap items-center justify-between gap-3 border-b pb-2">
            <div className="flex items-center gap-3">
              {template.background_url && (
                // eslint-disable-next-line @next/next/no-img-element
                <img src={template.background_url} alt="" aria-hidden="true"
                     className="h-10 w-16 rounded border border-slate-200 object-cover" />
              )}
              <div>
                <div className="text-sm font-medium text-slate-900">{template.name}</div>
                <div className="text-xs text-slate-500">
                  {template.page_size === "a4-portrait" ? "A4 portrait" : "A4 landscape"} ·{" "}
                  {template.fields.length} {template.fields.length === 1 ? "field" : "fields"}
                </div>
              </div>
            </div>
            <Button
              type="button"
              size="sm"
              variant="secondary"
              disabled={busy !== null}
              onClick={async () => {
                setBusy(template.id);
                setError(null);
                const result = await copyToEventAction(eventId, template.id);
                setBusy(null);
                if (result.ok) { setOpen(false); router.refresh(); } else setError(result.error);
              }}
            >
              {busy === template.id ? "Adding…" : "Use this"}
            </Button>
          </li>
        ))}
      </ul>

      {error && <p role="alert" className="text-sm text-red-700">{error}</p>}

      <Button type="button" size="sm" variant="secondary" onClick={() => setOpen(false)}>
        Cancel
      </Button>
    </div>
  );
}
