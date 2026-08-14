import { redirect } from "next/navigation";
import { Trophy } from "lucide-react";
import { Card, EmptyState } from "@kurx/ui";
import { requireStaffSession } from "@/lib/session";
import { PageHeader } from "@/components/layout/page-header";
import { listAdminEvents, listStages, apiErrorMessage, apiErrorStatus } from "@/lib/api";
import { EventPicker } from "@/components/admin/event-picker";
import { StagesManager } from "@/components/admin/stages-manager";

// Competition engine (V3 §10). Stages are event-scoped, so this is "pick an event, then run its
// stages" — the same shape as Certificates, and it needs no admin org list.
// SuperAdmin only: the backend gate is IsOrganiserAsync = isAdmin || event:manage, and the
// kurx_admin claim that satisfies `isAdmin` is granted to SuperAdmin alone.
export default async function CompetitionsPage({
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
    const stages = selected ? await listStages(session.accessToken, selected.event_id) : [];

    return (
      <div className="space-y-6">
        <PageHeader
          kicker="Events"
          title="Competitions"
          description="Stages, rosters and results for a competition event. A stage runs Draft → Live → Closed; results are computed deterministically, then published."
        />

        <EventPicker events={events} selectedId={eventId} query={q} />

        {selected ? (
          <StagesManager eventId={selected.event_id} eventTitle={selected.title} stages={stages} query={q} />
        ) : (
          <Card>
            <EmptyState
              icon={<Trophy size={20} />}
              title="Choose an event"
              message="Search for an event above to see and manage its competition stages."
            />
          </Card>
        )}
      </div>
    );
  } catch (err) {
    const forbidden = apiErrorStatus(err) === 403;
    return (
      <div className="space-y-4">
        <h1 className="text-2xl font-semibold text-text">Competitions</h1>
        <Card>
          {forbidden ? (
            <p className="text-sm text-muted">You don&apos;t have access to competitions.</p>
          ) : (
            <p className="text-sm text-muted">
              Couldn&apos;t load competitions: <span className="text-text">{apiErrorMessage(err)}</span>
            </p>
          )}
        </Card>
      </div>
    );
  }
}
