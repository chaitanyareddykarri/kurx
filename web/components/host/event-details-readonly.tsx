import type { EventDetail } from "@/lib/api";

/*
 * D-388 — the approved values, as they are live right now.
 *
 * Deliberately NOT a disabled form. A greyed-out input still reads as "editable, just not yet" and
 * invites the host to hunt for whatever unlocks it; a definition list reads as a record. It is also the
 * honest shape: these fields are not this host's to set any more, they are what a reviewer approved.
 *
 * The operational fields — contact details, website, banner, registration windows — are absent here
 * because they are still directly editable and belong on the editable form beside this, not in a
 * read-only record of what is protected.
 */

function Row({ label, value }: { label: string; value: string | null | undefined }) {
  return (
    <div className="grid gap-1 border-b border-border py-3 last:border-0 sm:grid-cols-[10rem_1fr]">
      <dt className="text-xs font-semibold uppercase tracking-wide text-muted">{label}</dt>
      <dd className="whitespace-pre-wrap text-sm text-text">
        {value && value.length > 0 ? value : <span className="text-muted">— not set —</span>}
      </dd>
    </div>
  );
}

/// Rendered on the server, in the event's own timezone. The host reads "3:30 PM", never an ISO string,
/// and never a time silently shifted into the reader's own zone — which for an event in Kolkata read by
/// someone travelling is a different day.
function when(iso: string, timezone: string): string {
  try {
    return new Intl.DateTimeFormat("en-IN", {
      dateStyle: "medium",
      timeStyle: "short",
      timeZone: timezone
    }).format(new Date(iso));
  } catch {
    // An event carrying an unrecognised timezone must still render its dates. Falling back to UTC and
    // saying so beats throwing on the whole page.
    return `${new Date(iso).toUTCString()} (UTC)`;
  }
}

export function EventDetailsReadonly({
  event,
  categoryName,
  typeName
}: {
  event: EventDetail;
  categoryName?: string;
  typeName?: string;
}) {
  return (
    <dl className="mt-2">
      <Row label="Title" value={event.title} />
      <Row label="Subtitle" value={event.subtitle} />
      <Row label="Description" value={event.description} />
      <Row label="Category" value={categoryName ?? event.category_id} />
      <Row label="Type" value={typeName ?? event.type_id ?? undefined} />
      <Row label="Starts" value={`${when(event.starts_at, event.timezone)} (${event.timezone})`} />
      <Row label="Ends" value={`${when(event.ends_at, event.timezone)} (${event.timezone})`} />
      <Row label="Venue" value={event.venue.name} />
      <Row label="Venue address" value={event.venue.address} />
      <Row label="City" value={event.venue.city} />
      <Row label="Capacity" value={event.capacity === null ? undefined : String(event.capacity)} />
      <Row label="Visibility" value={event.visibility} />
    </dl>
  );
}
