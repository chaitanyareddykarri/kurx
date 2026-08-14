import { requireEventOrg } from "@/lib/event-org";
import { listEventSessions } from "@/lib/api";
import { Card } from "@kurx/ui";
import { ScheduleSection } from "@/components/host/event-sections";

export default async function EventSchedulePage({ params }: { params: { id: string } }) {
  const { session, orgId } = await requireEventOrg(params.id);
  const sessions = await listEventSessions(session.accessToken, orgId, params.id);

  return (
    <Card>
      <h2 className="mb-4 text-lg font-semibold">Schedule</h2>
      <ScheduleSection orgId={orgId} eventId={params.id} sessions={sessions} />
    </Card>
  );
}
