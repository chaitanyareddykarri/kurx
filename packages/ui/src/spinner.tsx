import { Loader2 } from "lucide-react";

/**
 * The one inline spinner — prefer skeletons for page/section loads, this for
 * buttons and inline waits.
 *
 * The status is announced from a `role="status"` wrapper with visually-hidden
 * text, not from `aria-label` on the icon. `aria-label` on an `<svg>` with no
 * role is unreliably exposed across screen readers, so the previous version was
 * silent on several of them. The icon itself is now explicitly decorative.
 */
export function Spinner({
  size = 18,
  className = "",
  /** Announced to assistive tech. Set something specific where the context warrants it. */
  label = "Loading",
  /**
   * Renders the icon alone, with no status role.
   *
   * Use inside a region that already announces the wait — nesting one
   * `role="status"` inside another means the message is announced twice, and
   * screen readers differ on which one wins.
   */
  decorative = false
}: {
  size?: number;
  className?: string;
  label?: string;
  decorative?: boolean;
}) {
  const icon = <Loader2 size={size} aria-hidden="true" className={`animate-spin text-accent ${className}`} />;

  if (decorative) return icon;

  return (
    <span role="status" className="inline-flex items-center">
      {icon}
      <span className="sr-only">{label}</span>
    </span>
  );
}
