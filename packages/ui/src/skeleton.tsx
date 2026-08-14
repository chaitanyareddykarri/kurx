/**
 * A pulsing placeholder block. Compose these into list/card skeletons so loading
 * reads consistently.
 *
 * `aria-hidden` because a skeleton is a picture of content that does not exist
 * yet — announcing it produces noise, and the surrounding region should carry
 * `aria-busy` instead. `ListSkeleton` provides that region.
 *
 * The pulse is clamped automatically under `prefers-reduced-motion` by the global
 * rule in `tokens.css`.
 */
export function Skeleton({ className = "" }: { className?: string }) {
  return <div aria-hidden="true" className={`animate-pulse rounded-md bg-elevated ${className}`} />;
}

/**
 * A vertical list skeleton for card lists (search results, feeds).
 *
 * Announces the wait once, politely, rather than leaving a screen-reader user on
 * a silent empty page: `aria-busy` marks the region as in-flight and the
 * visually-hidden label says what is happening.
 */
export function ListSkeleton({ rows = 5, label = "Loading" }: { rows?: number; label?: string }) {
  return (
    <div role="status" aria-busy="true" className="space-y-3">
      <span className="sr-only">{label}</span>
      {Array.from({ length: rows }).map((_, i) => (
        <Skeleton key={i} className="h-20 w-full" />
      ))}
    </div>
  );
}
