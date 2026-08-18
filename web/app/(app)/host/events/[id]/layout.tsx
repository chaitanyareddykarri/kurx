import { Badge } from "@kurx/ui";
import { requireEventOrg } from "@/lib/event-org";
import { eventStatusOf } from "@/lib/event-status";
import { getWorkspaceCapabilities, can } from "@/lib/capabilities";
import { EventStatusActions } from "@/components/host/event-status-actions";
import { EventWorkspaceTabs } from "@/components/host/event-workspace-tabs";

/// Event workspace shell. Every event opens as a tabbed workspace (not a long edit form); which tabs and
/// which status actions appear is driven by the caller's live capabilities, never hardcoded.
export default async function EventWorkspaceLayout({
  children,
  params
}: {
  children: React.ReactNode;
  params: { id: string };
}) {
  // The event resolves its own organization (D-267) — permissions are read for the org that actually
  // owns it, not for whichever one an organization switcher happened to have selected.
  const { session, event, orgId } = await requireEventOrg(params.id);
  const caps = await getWorkspaceCapabilities(session.accessToken, orgId);

  const base = `/host/events/${event.id}`;
  const canEdit = can(caps, "events", "update");
  const canAttendees = (caps.permissions.attendees ?? []).includes("view");
  const canAnalytics = (caps.permissions.analytics ?? []).includes("view");
  const canTeam = (caps.permissions.volunteers ?? []).includes("view");

  const tabs = [
    { key: "overview", label: "Overview", href: base },
    ...(canEdit
      ? [
          // D-266 M8 — publish blockers, institutional authorization and the capability-driven module
          // list. Gated on canEdit because everything on it is an organiser action.
          { key: "readiness", label: "Readiness", href: `${base}/readiness` },
          // There is no Representing tab. Representation is answered once, inside Create Event, and the
          // only thing that ever re-opens it is a reviewer asking for changes — which is an edit of the
          // event, so it lives on Details (Edit Event) rather than as a standing workspace section.
          // A second form here is what made organisers believe creation had not finished.
          { key: "details", label: "Details", href: `${base}/details` },
          { key: "schedule", label: "Schedule", href: `${base}/schedule` },
          { key: "people", label: "Speakers & Sponsors", href: `${base}/people` },
          { key: "media", label: "Media", href: `${base}/media` },
          { key: "tickets", label: "Tickets", href: `${base}/tickets` }
        ]
      : []),
    // Registrations and Attendees are separate on purpose: a registration may still be pending or
    // waitlisted, an attendee is an issued admission. Both sit behind the same view permission.
    ...(canAttendees
      ? [
          { key: "registrations", label: "Registrations", href: `${base}/registrations` },
          { key: "attendees", label: "Attendees", href: `${base}/attendees` }
        ]
      : []),
    ...(canEdit
      ? [
          { key: "checkin", label: "Check-in", href: `${base}/checkin` },
          // D-362 — badge printing. Next to Check-in on purpose: a badge is what gets scanned there.
          { key: "badges", label: "Badges", href: `${base}/badges` },
          // D-355 — the certificate module. Issuance lives with the event it certifies.
          { key: "certificates", label: "Certificates", href: `${base}/certificates` },
          { key: "announcements", label: "Announcements", href: `${base}/announcements` },
          { key: "invitations", label: "Invitations", href: `${base}/invitations` },
          { key: "chat", label: "Chat", href: `${base}/chat` }
        ]
      : []),
    ...(canTeam ? [{ key: "team", label: "Team", href: `${base}/team` }] : []),
    ...(canAnalytics
      ? [
          { key: "analytics", label: "Analytics", href: `${base}/analytics` },
          { key: "reviews", label: "Reviews", href: `${base}/reviews` }
        ]
      : [])
  ];

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-4">
        <div className="min-w-0">
          {/* The status was uppercased raw, so a host waiting on a decision read "PENDINGREVIEW",
              and the whole line sat on `text-accent-text` at 2.80:1 in the light theme. The status is a
              `Badge` now, which is the shape the rest of the product uses for it. */}
          <div className="flex flex-wrap items-center gap-2">
            {caps.representation.kind === "organization" ? (
              <p className="text-sm font-semibold text-accent-text">Representing {caps.representation.name}</p>
            ) : null}
            <Badge tone={eventStatusOf(event.status).tone}>{eventStatusOf(event.status).label}</Badge>
          </div>
          <h1 className="mt-1 break-words text-3xl font-semibold text-text">{event.title}</h1>
        </div>
        {canEdit ? <EventStatusActions orgId={orgId} eventId={event.id} status={event.status} /> : null}
      </div>
      <EventWorkspaceTabs tabs={tabs} />
      <div>{children}</div>
    </div>
  );
}
