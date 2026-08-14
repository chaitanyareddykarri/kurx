import { redirect } from "next/navigation";
import { Mic } from "lucide-react";
import { Card, EmptyState } from "@kurx/ui";
import { requireStaffSession } from "@/lib/session";
import { PageHeader } from "@/components/layout/page-header";
import {
  listAdminEvents, listOrgSpeakers, listEventSpeakers, apiErrorMessage, apiErrorStatus
} from "@/lib/api";
import { EventPicker } from "@/components/admin/event-picker";
import { SpeakersManager } from "@/components/admin/speakers-manager";

// Speakers are ORG-scoped (/v1/orgs/{orgId}/speakers), and there is still no admin org list, so the
// org is resolved from a chosen event — /v1/admin/events returns org_id per event. Same navigation
// pattern as Certificates and Competitions.
// SuperAdmin only: CanManage(isAdmin, role) short-circuits on the kurx_admin claim, which only
// SuperAdmin holds; any other staff role would reach the backend without it and get a 403.
export default async function SpeakersPage({
  searchParams
}: {
  searchParams: { eventId?: string; q?: string };
}) {
  const session = await requireStaffSession();
  if (!session.roles.includes("SuperAdmin")) redirect("/");

  const eventId = searchParams.eventId ?? "";
  const q = (searchParams.q ?? "").trim();

  try {
    const { items: events } = await listAdminEvents(session.accessToken, q ? { q } : undefined);
    const selected = events.find((e) => e.event_id === eventId) ?? null;

    // The org roster and the event line-up are independent reads; an event with no speakers is normal.
    const [roster, lineup] = selected
      ? await Promise.all([
          listOrgSpeakers(session.accessToken, selected.representing_org_id),
          listEventSpeakers(session.accessToken, selected.representing_org_id, selected.event_id)
        ])
      : [[], []];

    return (
      <div className="space-y-6">
        <PageHeader
          kicker="Events"
          title="Speakers"
          description="An organisation's speaker roster, and which of them appear on the selected event. Deleting a speaker also removes them from every event and session they were on."
        />

        <EventPicker events={events} selectedId={eventId} query={q} />

        {selected ? (
          <SpeakersManager
            orgId={selected.representing_org_id}
            orgName={selected.org_name}
            eventId={selected.event_id}
            eventTitle={selected.title}
            roster={roster}
            lineup={lineup}
          />
        ) : (
          <Card>
            <EmptyState
              icon={<Mic size={20} />}
              title="Choose an event"
              message="Pick an event above to manage its organisation's speakers and its line-up."
            />
          </Card>
        )}
      </div>
    );
  } catch (err) {
    const forbidden = apiErrorStatus(err) === 403;
    return (
      <div className="space-y-4">
        <h1 className="text-2xl font-semibold text-text">Speakers</h1>
        <Card>
          {forbidden ? (
            <p className="text-sm text-muted">You don&apos;t have access to speakers.</p>
          ) : (
            <p className="text-sm text-muted">
              Couldn&apos;t load speakers: <span className="text-text">{apiErrorMessage(err)}</span>
            </p>
          )}
        </Card>
      </div>
    );
  }
}
