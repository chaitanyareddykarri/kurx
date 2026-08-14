import { requireEventOrg } from "@/lib/event-org";
import { Card } from "@kurx/ui";
import { CheckinPanel } from "@/components/host/checkin-panel";

export default async function EventCheckinPage({ params }: { params: { id: string } }) {
  const { orgId } = await requireEventOrg(params.id);
  return (
    <Card>
      <h2 className="mb-2 text-lg font-semibold">Check-in</h2>
      <p className="mb-4 text-sm text-muted">
        Enter or scan a ticket code to admit an attendee. Duplicate scans are flagged, not double-counted.
      </p>
      <CheckinPanel eventId={params.id} />
    </Card>
  );
}
