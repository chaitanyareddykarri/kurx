import { requireEventOrg } from "@/lib/event-org";
import { Card } from "@kurx/ui";
import { BannerSection, MediaSection } from "@/components/host/event-sections";

export default async function EventMediaPage({ params }: { params: { id: string } }) {
  const { orgId, event } = await requireEventOrg(params.id);

  return (
    <div className="space-y-6">
      {/*
        Banner first: it is the one image attendees actually see on a card, and until D-302 there was no
        way to set it anywhere in the product. The gallery below is secondary — it appears on the event
        page, not in discovery.
      */}
      <Card>
        <h2 className="mb-1 text-lg font-semibold">Banner</h2>
        <p className="mb-4 text-sm text-muted">
          Shown on your event&apos;s card in discovery and at the top of its page.
        </p>
        <BannerSection orgId={orgId} eventId={event.id} bannerUrl={event.banner_url ?? null} />
      </Card>

      <Card>
        <h2 className="mb-1 text-lg font-semibold">Gallery &amp; documents</h2>
        <p className="mb-4 text-sm text-muted">
          Photos, posters and PDFs shown on the event page.
        </p>
        <MediaSection orgId={orgId} eventId={event.id} media={event.media} />
      </Card>
    </div>
  );
}
