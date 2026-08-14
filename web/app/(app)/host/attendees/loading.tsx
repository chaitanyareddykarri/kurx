import { Card } from "@kurx/ui";

// Loading state — shown while events + attendees are fetched on the server.
export default function AttendeesLoading() {
  return (
    <div className="space-y-6" aria-busy="true" aria-label="Loading attendees">
      <div className="space-y-2">
        <div className="h-4 w-28 animate-pulse rounded bg-elevated" />
        <div className="h-8 w-44 animate-pulse rounded bg-elevated" />
      </div>
      <div className="flex gap-2">
        {[0, 1, 2].map((i) => <div key={i} className="h-8 w-24 animate-pulse rounded-md bg-elevated" />)}
      </div>
      <Card className="p-0">
        <div className="space-y-px">
          {[0, 1, 2, 3].map((i) => (
            <div key={i} className="flex items-center justify-between px-5 py-4">
              <div className="h-4 w-40 animate-pulse rounded bg-elevated" />
              <div className="h-4 w-20 animate-pulse rounded bg-elevated" />
            </div>
          ))}
        </div>
      </Card>
    </div>
  );
}
