import { Search } from "lucide-react";
import { Badge, Button, Card, Field, Input, Select } from "@kurx/ui";
import type { AdminEvent } from "@/lib/api";

/** Mirrors the backend default for GET /v1/admin/events (`limit ?? 50`), which takes no offset. */
const EVENT_LIST_LIMIT = 50;

/** Server-rendered event chooser for event-scoped admin screens. A plain GET form so the choice
 *  lives in the URL — shareable, back-button-safe, and no client state to keep in sync. */
export function EventPicker({
  events,
  selectedId,
  query
}: {
  events: AdminEvent[];
  selectedId: string;
  query: string;
}) {
  return (
    <Card>
      <form method="get" className="space-y-3">
        <div className="max-w-xl">
          <label htmlFor="event-q" className="sr-only">
            Search events
          </label>
          <div className="relative">
            <Search size={15} className="pointer-events-none absolute left-2.5 top-2.5 text-muted" />
            <Input
              id="event-q"
              name="q"
              defaultValue={query}
              placeholder="Search events by title"
              className="pl-8"
            />
          </div>
        </div>

        <Field label="Event" htmlFor="event-id">
          <Select id="event-id" name="eventId" defaultValue={selectedId} className="max-w-xl">
            <option value="">— select an event —</option>
            {events.map((e) => (
              <option key={e.event_id} value={e.event_id}>
                {e.title} · {e.org_name} · {e.status}
              </option>
            ))}
          </Select>
        </Field>

        {/* One submit for the whole form: the search box and the select are applied together, so a
            second button would just be a differently-labelled duplicate of this one. */}
        <div className="flex flex-wrap items-center gap-3">
          <Button type="submit">Apply</Button>
          {events.length === 0 ? (
            <Badge tone="muted">{query ? "No events match that search" : "No events found"}</Badge>
          ) : (
            <span className="text-xs text-muted">
              {events.length} event{events.length === 1 ? "" : "s"} listed
              {/* The backend caps this list at 50 with no paging param, so anything beyond that is
                  only reachable by searching. Saying so beats a silently truncated dropdown. */}
              {events.length >= EVENT_LIST_LIMIT ? " (capped — search to narrow)" : ""}
            </span>
          )}
        </div>
      </form>
    </Card>
  );
}
