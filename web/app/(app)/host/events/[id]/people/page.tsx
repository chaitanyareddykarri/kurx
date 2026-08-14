import { requireEventOrg } from "@/lib/event-org";
import { listEventSpeakers, listEventSponsors } from "@/lib/api";
import { Card } from "@kurx/ui";
import { SpeakersSection, SponsorsSection } from "@/components/host/event-sections";

export default async function EventPeoplePage({ params }: { params: { id: string } }) {
  const { session, orgId } = await requireEventOrg(params.id);
  const [speakers, sponsors] = await Promise.all([
    listEventSpeakers(session.accessToken, orgId, params.id),
    listEventSponsors(session.accessToken, orgId, params.id)
  ]);

  return (
    <div className="space-y-6">
      <Card>
        <h2 className="mb-4 text-lg font-semibold">Speakers</h2>
        <SpeakersSection orgId={orgId} eventId={params.id} speakers={speakers} />
      </Card>
      <Card>
        <h2 className="mb-4 text-lg font-semibold">Sponsors</h2>
        <SponsorsSection orgId={orgId} eventId={params.id} sponsors={sponsors} />
      </Card>
    </div>
  );
}
