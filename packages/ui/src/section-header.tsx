import { ChevronRight } from "lucide-react";

/** Section title row with an optional trailing action, heading feed groups and lists. */
export function SectionHeader({
  title,
  subtitle,
  actionLabel,
  onAction
}: {
  title: string;
  subtitle?: string;
  actionLabel?: string;
  onAction?: () => void;
}) {
  return (
    <div className="flex items-center justify-between gap-4 py-2">
      <div>
        <h2 className="text-lg font-bold tracking-tight text-text">{title}</h2>
        {subtitle ? <p className="text-sm text-muted">{subtitle}</p> : null}
      </div>
      {actionLabel && onAction ? (
        <button
          type="button"
          onClick={onAction}
          className="inline-flex min-h-11 items-center gap-0.5 rounded-md text-sm font-bold text-accent-text transition duration-fast hover:opacity-80 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
        >
          {actionLabel}
          <ChevronRight size={16} aria-hidden />
        </button>
      ) : null}
    </div>
  );
}
