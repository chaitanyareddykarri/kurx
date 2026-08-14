import { Card } from "@kurx/ui";

// Loading state — shown while identity + org capabilities are fetched on the server.
export default function VerificationLoading() {
  return (
    <div className="space-y-6" aria-busy="true" aria-label="Loading verification status">
      <div className="space-y-2">
        <div className="h-4 w-28 animate-pulse rounded bg-elevated" />
        <div className="h-8 w-72 animate-pulse rounded bg-elevated" />
      </div>
      {[0, 1].map((i) => (
        <Card key={i}>
          <div className="h-6 w-40 animate-pulse rounded bg-elevated" />
          <div className="mt-4 space-y-3">
            <div className="h-4 w-full animate-pulse rounded bg-elevated" />
            <div className="h-4 w-5/6 animate-pulse rounded bg-elevated" />
            <div className="h-4 w-2/3 animate-pulse rounded bg-elevated" />
          </div>
        </Card>
      ))}
    </div>
  );
}
