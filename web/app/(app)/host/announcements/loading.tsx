import { Card } from "@kurx/ui";

// Loading state — shown while events + announcements are fetched on the server.
export default function AnnouncementsLoading() {
  return (
    <div className="space-y-6" aria-busy="true" aria-label="Loading announcements">
      <div className="space-y-2">
        <div className="h-4 w-28 animate-pulse rounded bg-elevated" />
        <div className="h-8 w-56 animate-pulse rounded bg-elevated" />
      </div>
      <div className="flex gap-2">
        {[0, 1, 2].map((i) => <div key={i} className="h-8 w-24 animate-pulse rounded-md bg-elevated" />)}
      </div>
      <div className="space-y-3">
        {[0, 1].map((i) => <Card key={i}><div className="h-16 animate-pulse rounded bg-elevated" /></Card>)}
      </div>
    </div>
  );
}
