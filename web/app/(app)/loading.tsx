import { ListSkeleton } from "@/components/ui/skeleton";

/**
 * Segment-level loading UI for the whole signed-in app.
 *
 * Seven `loading.tsx` files existed, all on deep leaf routes under `/host`. Everything else in
 * `(app)` — tickets, orders, groups, settings, the event pages people actually navigate between —
 * had none, and a Next server segment with no `loading.tsx` does not stream: the browser holds the
 * *previous* page, fully interactive and completely wrong, until the new one's data arrives. The
 * user gets no signal that their tap registered, which is the same failure Phase 42 fixed on the
 * submit button, one level up.
 *
 * Deliberately generic. A segment-level fallback covers routes with very different shapes, so it
 * shows a page is coming rather than pretending to know its layout — a skeleton that guesses wrong
 * costs more than one that stays vague, because it shifts under the content that replaces it.
 */
export default function AppLoading() {
  return (
    <div className="space-y-6" aria-busy="true">
      <span className="sr-only" role="status">
        Loading.
      </span>
      <div className="space-y-2">
        <div className="h-8 w-48 max-w-full animate-pulse rounded-md bg-elevated" />
        <div className="h-4 w-full max-w-80 animate-pulse rounded-md bg-elevated" />
      </div>
      <ListSkeleton rows={5} />
    </div>
  );
}
