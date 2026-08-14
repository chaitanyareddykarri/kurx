import { Skeleton } from "@kurx/ui";

/**
 * The profile's loading state.
 *
 * **Shaped like the header it replaces, not a generic spinner.** The page is server-rendered and
 * fans out to twelve independent section reads, so the gap before first paint is real. A skeleton
 * that matches the final geometry — cover band, overlapping avatar, name, two headlines, stat row —
 * means the layout does not jump when data lands, which is the actual cost a spinner leaves unpaid.
 *
 * `aria-busy` with a polite live region announces the wait once; the shapes themselves are
 * `aria-hidden`, because a screen reader reading out eleven empty boxes is worse than silence.
 */
function Line({ className = "" }: { className?: string }) {
  return <Skeleton className={`h-4 rounded-md ${className}`} />;
}

export default function ProfileLoading() {
  return (
    <main className="container-shell py-lg sm:py-xl" aria-busy="true">
      <p className="sr-only" role="status">
        Loading profile
      </p>

      <div aria-hidden="true">
        <section className="overflow-hidden rounded-lg border border-border bg-surface">
          <Skeleton className="aspect-[4/1] min-h-32 w-full sm:aspect-[5/1]" />

          <div className="px-lg pb-lg sm:px-xl sm:pb-xl">
            <div className="flex flex-wrap items-end justify-between gap-lg">
              {/* Same negative offset and ring as the real avatar, so nothing shifts on swap. */}
              <div className="-mt-14 rounded-full ring-4 ring-surface sm:-mt-16">
                <Skeleton className="h-32 w-32 rounded-full" />
              </div>
              <div className="ml-auto flex gap-sm pt-md">
                <Skeleton className="h-11 w-28 rounded-md" />
                <Skeleton className="h-11 w-24 rounded-md" />
              </div>
            </div>

            <div className="mt-lg space-y-md">
              <Line className="w-64 max-w-full sm:h-9" />
              <Line className="w-32" />
              <Line className="w-80 max-w-full" />
              <Line className="w-56 max-w-full" />
            </div>
          </div>
        </section>

        <div className="mt-lg grid grid-cols-2 gap-md sm:grid-cols-5">
          {Array.from({ length: 5 }, (_, i) => (
            <Skeleton key={i} className="h-20 rounded-lg" />
          ))}
        </div>

        <div className="mt-lg space-y-lg">
          <Skeleton className="h-44 rounded-lg" />
          <Skeleton className="h-64 rounded-lg" />
        </div>
      </div>
    </main>
  );
}
