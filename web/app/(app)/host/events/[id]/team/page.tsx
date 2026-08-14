import { requireEventOrg } from "@/lib/event-org";
import { listEventAssignments, getAllyStatusBatch, apiErrorMessage, type AllyRelationStatus } from "@/lib/api";
import { Card } from "@kurx/ui";
import { AssignmentsSection } from "@/components/host/assignments-section";

export default async function EventTeamPage({ params }: { params: { id: string } }) {
  const { session, orgId } = await requireEventOrg(params.id);


  let assignments;
  try {
    assignments = await listEventAssignments(session.accessToken, orgId, params.id);
  } catch (err) {
    return (
      <Card>
        <h2 className="mb-4 text-lg font-semibold">Event staff &amp; volunteers</h2>
        <p className="text-sm text-muted">Couldn&apos;t load the team: <span className="text-text">{apiErrorMessage(err)}</span></p>
      </Card>
    );
  }

  const otherUserIds = assignments.map((a) => a.user_id).filter((id) => id !== session.me.id);
  const allyStatus = await getAllyStatusBatch(session.accessToken, otherUserIds).catch(() => ({}) as Record<string, AllyRelationStatus>);

  return (
    <Card>
      <h2 className="mb-4 text-lg font-semibold">Event staff &amp; volunteers</h2>
      <AssignmentsSection orgId={orgId} eventId={params.id} assignments={assignments}
        myUserId={session.me.id} allyStatus={allyStatus} />
    </Card>
  );
}
