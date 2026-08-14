import { Card } from "@kurx/ui";

// Loading state — shown while events + invitations are fetched on the server.
export default function InvitationsLoading() {
  return (
    <div className="space-y-6" aria-busy="true" aria-label="Loading invitations">
      <div className="space-y-2">
        <div className="h-4 w-28 animate-pulse rounded bg-elevated" />
        <div className="h-8 w-48 animate-pulse rounded bg-elevated" />
      </div>
      <div className="grid gap-4 sm:grid-cols-3 xl:grid-cols-5">
        {[0, 1, 2, 3, 4].map((i) => (
          <div key={i} className="rounded-lg border border-border bg-surface p-4">
            <div className="h-4 w-16 animate-pulse rounded bg-elevated" />
            <div className="mt-3 h-7 w-10 animate-pulse rounded bg-elevated" />
          </div>
        ))}
      </div>
      <Card><div className="h-24 animate-pulse rounded bg-elevated" /></Card>
    </div>
  );
}
