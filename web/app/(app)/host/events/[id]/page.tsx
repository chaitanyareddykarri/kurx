import { requireEventOrg } from "@/lib/event-org";
import { Card } from "@kurx/ui";
import { formatDateTime } from "@/lib/formatters";

/** Visibility is a stored enum; an unrecognised value is shown as-is rather than mapped to a guess. */
function visibilityLabel(visibility: string): string {
  const key = visibility.toLowerCase().replace(/[\s_-]/g, "");
  return { public: "Public", unlisted: "Unlisted", private: "Private", inviteonly: "Invite only" }[key] ?? visibility;
}

export default async function EventOverviewPage({ params }: { params: { id: string } }) {
  const { session, orgId, event } = await requireEventOrg(params.id);

  const facts: [string, string][] = [
    ["Starts", formatDateTime(event.starts_at)],
    ["Ends", formatDateTime(event.ends_at)],
    ["Venue", `${event.venue.name || "Not set"}${event.venue.city ? ` · ${event.venue.city}` : ""}`],
    ["Visibility", visibilityLabel(event.visibility)],
    ["Capacity", event.capacity != null ? String(event.capacity) : "Unlimited"],
    ["Timezone", event.timezone]
  ];

  return (
    <div className="space-y-4">
      {event.subtitle ? <p className="text-muted">{event.subtitle}</p> : null}
      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
        {facts.map(([label, value]) => (
          <Card key={label}>
            <p className="text-caption uppercase tracking-wide text-muted">{label}</p>
            {/* `capitalize` ran over every value indiscriminately, so a timezone rendered as
                "Asia/kolkata" and a venue's own capitalisation was overwritten. Values that need
                normalising are normalised at the source instead. */}
            <p className="mt-1 font-medium text-text">{value}</p>
          </Card>
        ))}
      </div>
      {event.description ? (
        <Card>
          <h2 className="font-semibold text-text">Description</h2>
          <p className="mt-2 whitespace-pre-wrap text-sm text-muted">{event.description}</p>
        </Card>
      ) : null}
    </div>
  );
}
