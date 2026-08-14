// Loading state — shown while events + analytics are fetched on the server.
export default function AnalyticsLoading() {
  return (
    <div className="space-y-6" aria-busy="true" aria-label="Loading analytics">
      <div className="space-y-2">
        <div className="h-4 w-28 animate-pulse rounded bg-elevated" />
        <div className="h-8 w-52 animate-pulse rounded bg-elevated" />
      </div>
      <div className="flex gap-2">
        {[0, 1, 2].map((i) => <div key={i} className="h-8 w-24 animate-pulse rounded-md bg-elevated" />)}
      </div>
      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
        {[0, 1, 2, 3, 4, 5].map((i) => (
          <div key={i} className="rounded-lg border border-border bg-surface p-4">
            <div className="h-4 w-24 animate-pulse rounded bg-elevated" />
            <div className="mt-3 h-7 w-20 animate-pulse rounded bg-elevated" />
          </div>
        ))}
      </div>
    </div>
  );
}
