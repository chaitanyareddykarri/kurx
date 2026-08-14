import { ListSkeleton } from "@/components/ui/skeleton";

/**
 * Segment-level loading UI for the public site — the event pages, discovery and organiser profiles
 * a first-time visitor lands on, none of which had one. See the note in `(app)/loading.tsx`: without
 * this, the previous page stays on screen until the next one's data arrives.
 *
 * This matters more here than anywhere else in the product, because the visitor has no reason to
 * assume the site is working.
 */
export default function PublicLoading() {
  return (
    <main className="container-shell py-10" aria-busy="true">
      <span className="sr-only" role="status">
        Loading.
      </span>
      <div className="space-y-2">
        <div className="h-9 w-64 max-w-full animate-pulse rounded-md bg-elevated" />
        <div className="h-4 w-full max-w-96 animate-pulse rounded-md bg-elevated" />
      </div>
      <div className="mt-8">
        <ListSkeleton rows={4} />
      </div>
    </main>
  );
}
