import { ListSkeleton } from "@/components/ui/skeleton";

export default function DiscoverLoading() {
  return (
    <div className="space-y-6">
      <div className="space-y-2">
        <div className="h-8 w-56 animate-pulse rounded-md bg-elevated" />
        {/* `w-96` is 384px, wider than the 328px content box at the 360px floor, so the skeleton
            itself pushed the page into horizontal scroll before any content had loaded. */}
        <div className="h-4 w-full max-w-96 animate-pulse rounded-md bg-elevated" />
      </div>
      <div className="grid gap-6 lg:grid-cols-[260px_1fr]">
        <div className="h-fit space-y-4 rounded-lg border border-border bg-surface p-4">
          <ListSkeleton rows={6} />
        </div>
        <ListSkeleton rows={6} />
      </div>
    </div>
  );
}
