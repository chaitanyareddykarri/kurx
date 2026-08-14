import { redirect } from "next/navigation";
import { Handshake } from "lucide-react";
import { Card, EmptyState } from "@kurx/ui";
import { requireStaffSession } from "@/lib/session";
import { PageHeader } from "@/components/layout/page-header";
import {
  listAdminEvents, listOrgSponsors, listEventSponsors, apiErrorMessage, apiErrorStatus
} from "@/lib/api";
import { EventPicker } from "@/components/admin/event-picker";
import { SponsorsManager } from "@/components/admin/sponsors-manager";

// Sponsors are ORG-scoped (/v1/orgs/{orgId}/sponsors) and there is no admin org list, so the org is
// resolved from a chosen event — the same navigation as Certificates, Competitions and Speakers.
// SuperAdmin only: CanManage(isAdmin, role) short-circuits on the kurx_admin claim.
export default async function SponsorsPage({
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

    const [roster, lineup] = selected
      ? await Promise.all([
          listOrgSponsors(session.accessToken, selected.representing_org_id),
          listEventSponsors(session.accessToken, selected.representing_org_id, selected.event_id)
        ])
      : [[], []];

    return (
      <div className="space-y-6">
        <PageHeader
          kicker="Events"
          title="Sponsors"
          description="An organisation's sponsor roster, and which of them appear on the selected event. Deleting a sponsor removes it from every event it was attached to."
        />

        <EventPicker events={events} selectedId={eventId} query={q} />

        {selected ? (
          <SponsorsManager
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
              icon={<Handshake size={20} />}
              title="Choose an event"
              message="Pick an event above to manage its organisation's sponsors and its line-up."
            />
          </Card>
        )}
      </div>
    );
  } catch (err) {
    const forbidden = apiErrorStatus(err) === 403;
    return (
      <div className="space-y-4">
        <h1 className="text-2xl font-semibold text-text">Sponsors</h1>
        <Card>
          {forbidden ? (
            <p className="text-sm text-muted">You don&apos;t have access to sponsors.</p>
          ) : (
            <p className="text-sm text-muted">
              Couldn&apos;t load sponsors: <span className="text-text">{apiErrorMessage(err)}</span>
            </p>
          )}
        </Card>
      </div>
    );
  }
}
