import { AlertTriangle } from "lucide-react";

/**
 * Shown when a profile section could not be loaded (D-235).
 *
 * The wording matters more than the styling. It must say the failure is ours and temporary, and it
 * must never imply anything about the person whose profile this is — the whole point of separating
 * this from the empty state is that "we could not load this" and "this person has none" are different
 * claims, and only one of them is ever true here.
 *
 * `role="status"` rather than `role="alert"`: a section that failed to load is information, not an
 * interruption, and a profile with three failed sections should not fire three assertive
 * announcements at a screen-reader user.
 */
export function SectionUnavailable({ label }: { label: string }) {
  return (
    <div
      role="status"
      className="flex items-center gap-2 rounded-md border border-dashed border-border bg-surface px-3 py-2 text-sm text-muted"
    >
      <AlertTriangle size={14} className="shrink-0" aria-hidden />
      <span>{label} couldn&apos;t be loaded. This is a temporary problem on our side — try refreshing.</span>
    </div>
  );
}
