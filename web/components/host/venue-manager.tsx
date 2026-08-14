"use client";

import { Button } from "@kurx/ui";
import { ConfirmSubmitButton } from "@/components/host/confirm-submit-button";
import { updateVenueAction, deleteVenueAction, addVenueImageAction } from "@/lib/venue-actions";

type VenueRow = { id: string; name: string; address: string; city: string; capacity: number | null; image_keys: string[] };

// `border-strong`, not `border`: an input's edge is what shows where the control is (WCAG 1.4.11),
// and `border` is the decorative token at 1.30:1. 44px is the touch floor Phase 6 set.
const inputClass =
  "min-h-11 w-full rounded-md border border-border-strong bg-background px-3 text-sm text-text " +
  "focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/30";

export function VenueManager({ orgId, eventId, venues }: { orgId: string; eventId: string; venues: VenueRow[] }) {
  if (venues.length === 0) {
    return <p className="text-sm text-muted">No venues in the library yet. Save one above to reuse it.</p>;
  }
  return (
    <div className="space-y-3">
      {venues.map((v) => (
        <div key={v.id} className="rounded-md border border-border p-3 text-sm">
          <div className="flex items-start justify-between gap-2">
            <div>
              <span className="font-medium">{v.name}</span>
              <div className="text-xs text-muted">
                {[v.city, v.address].filter(Boolean).join(" · ") || "No address"}
                {v.capacity ? ` · cap ${v.capacity}` : ""} · {v.image_keys.length} image(s)
              </div>
            </div>
            <form action={deleteVenueAction.bind(null, orgId, eventId, v.id)}>
              <ConfirmSubmitButton
                label="Delete"
                title={`Delete the venue ${v.name}?`}
                description="Events already pointing at it keep the details they were saved with. This cannot be undone."
              />
            </form>
          </div>
          <details className="mt-2">
            <summary className="cursor-pointer text-xs text-accent-text">Edit &amp; images</summary>
            <div className="mt-2 space-y-2">
              <form action={updateVenueAction.bind(null, orgId, eventId, v.id, null)} className="grid gap-2 sm:grid-cols-2">
                <input name="name" defaultValue={v.name} required className={inputClass} />
                <input name="city" defaultValue={v.city} placeholder="City" className={inputClass} />
                <input name="address" defaultValue={v.address} placeholder="Address" aria-label="Address" className={`${inputClass} sm:col-span-2`} />
                <input name="capacity" type="number" min={1} defaultValue={v.capacity ?? ""} placeholder="Capacity" className={inputClass} />
                <div className="sm:col-span-2"><Button type="submit" variant="secondary">Save venue</Button></div>
              </form>
              <form action={addVenueImageAction.bind(null, orgId, eventId, v.id, null)} className="flex flex-wrap items-center gap-2">
                <input name="file" type="file" accept="image/*" required className="text-xs" />
                <Button type="submit" variant="secondary">Add image</Button>
              </form>
              <p className="text-xs text-muted">Removing an individual image needs the venue API to expose image ids — tracked as a backend gap.</p>
            </div>
          </details>
        </div>
      ))}
    </div>
  );
}
