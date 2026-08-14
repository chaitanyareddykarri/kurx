"use client";

import { X } from "lucide-react";

export type ActiveFilter = { id: string; label: string; value: string };

/**
 * The applied-filter row.
 *
 * Filters chosen in a `FilterBar` are otherwise only visible in the controls
 * themselves — which on a narrow screen are collapsed or scrolled out of view,
 * so a user sees a short result list with no explanation. Each chip names both
 * the facet and its value ("City: Bengaluru", not "Bengaluru"), because a bare
 * value out of context is guesswork.
 *
 * Each remove button's accessible name says what it removes, so a screen-reader
 * user hearing five "Remove" buttons in a row can tell them apart.
 */
export function FilterChips({
  filters,
  onRemove,
  onClearAll,
  className = ""
}: {
  filters: ActiveFilter[];
  onRemove: (id: string) => void;
  onClearAll?: () => void;
  className?: string;
}) {
  if (filters.length === 0) return null;

  return (
    <div className={`flex flex-wrap items-center gap-sm ${className}`}>
      <h2 className="sr-only">Active filters</h2>
      <ul className="flex flex-wrap items-center gap-sm">
        {filters.map((f) => (
          <li key={f.id}>
            <span className="inline-flex items-center gap-1 rounded-pill border border-border-strong bg-elevated py-0.5 pl-2.5 pr-0.5 text-caption text-text">
              <span>
                <span className="text-muted">{f.label}:</span> {f.value}
              </span>
              <button
                type="button"
                onClick={() => onRemove(f.id)}
                aria-label={`Remove filter ${f.label}: ${f.value}`}
                className="grid h-8 w-8 place-items-center rounded-full text-muted transition duration-fast hover:text-text focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-accent"
              >
                <X size={13} aria-hidden="true" />
              </button>
            </span>
          </li>
        ))}
      </ul>
      {onClearAll && filters.length > 1 ? (
        <button
          type="button"
          onClick={onClearAll}
          className="inline-flex min-h-11 items-center rounded-md px-2 text-caption text-accent-text transition duration-fast hover:underline"
        >
          Clear all
        </button>
      ) : null}
    </div>
  );
}
