import { Card } from "@kurx/ui";

// Loading state — shown while events + ticket types are fetched on the server.
export default function TicketsLoading() {
  return (
    <div className="space-y-6" aria-busy="true" aria-label="Loading tickets">
      <div className="space-y-2">
        <div className="h-4 w-28 animate-pulse rounded bg-elevated" />
        <div className="h-8 w-40 animate-pulse rounded bg-elevated" />
      </div>
      <div className="flex gap-2">
        {[0, 1, 2].map((i) => <div key={i} className="h-8 w-24 animate-pulse rounded-md bg-elevated" />)}
      </div>
      <div className="grid gap-3 md:grid-cols-2">
        {[0, 1].map((i) => <Card key={i}><div className="h-16 animate-pulse rounded bg-elevated" /></Card>)}
      </div>
    </div>
  );
}
