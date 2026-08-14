import { EventCard } from "@/components/events/event-card";
import { listSavedEvents } from "@/lib/api";
import { requireSession } from "@/lib/session";

export default async function SavedPage() {
  const session = await requireSession();
  const saved = await listSavedEvents(session.accessToken).catch(() => []);

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-3xl font-semibold">Saved events</h1>
        <p className="mt-2 text-sm text-muted">Events you bookmarked. They stay here until you unsave them.</p>
      </div>

      {saved.length === 0 ? (
        <p className="text-muted">Nothing saved yet. Tap “Save event” on any event to keep it here.</p>
      ) : (
        <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
          {saved.map((e) => <EventCard key={e.slug} event={e} />)}
        </div>
      )}
    </div>
  );
}
