import { requireEventOrg } from "@/lib/event-org";
import { listCategories, listSubcategories, listOrgVenues } from "@/lib/api";
import { Card } from "@kurx/ui";
import { EditEventForm } from "@/components/host/edit-event-form";
import { VenueSection } from "@/components/host/event-sections";
import { VenueManager } from "@/components/host/venue-manager";

export default async function EventDetailsPage({ params }: { params: { id: string } }) {
  const { session, orgId, event } = await requireEventOrg(params.id);
  const [categories, subcategories, venues] = await Promise.all([
    listCategories(),
    listSubcategories(),
    listOrgVenues(session.accessToken, orgId).catch(() => [])
  ]);

  return (
    <div className="space-y-6">
      <Card>
        <h2 className="mb-4 text-lg font-semibold">Event details</h2>
        <EditEventForm orgId={orgId} event={event} categories={categories} subcategories={subcategories} />
      </Card>
      <Card>
        <h2 className="mb-4 text-lg font-semibold">Venue</h2>
        <VenueSection orgId={orgId} eventId={event.id} venueName={event.venue.name} />
      </Card>
      <Card>
        <h2 className="mb-4 text-lg font-semibold">Venue library</h2>
        <VenueManager orgId={orgId} eventId={event.id} venues={venues} />
      </Card>
    </div>
  );
}
