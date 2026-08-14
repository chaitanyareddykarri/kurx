"use client";

import { ReactNode } from "react";
import { SelectHTMLAttributes } from "react";

type FilterBarProps = {
  children: ReactNode;
  /** When any filter is active, a "Clear filters" affordance appears. */
  activeCount?: number;
  onClear?: () => void;
  className?: string;
};

/** Horizontal container for filter controls with a consistent "Clear filters" action.
 *  Compose `FilterSelect` (or any control) inside it. */
export function FilterBar({ children, activeCount = 0, onClear, className = "" }: FilterBarProps) {
  return (
    <div className={`flex flex-wrap items-end gap-3 ${className}`}>
      {children}
      {activeCount > 0 && onClear ? (
        <button
          type="button"
          onClick={onClear}
          className="inline-flex min-h-11 items-center rounded-md px-3 text-body font-medium text-muted transition duration-fast hover:text-text focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
        >
          Clear filters{activeCount ? ` (${activeCount})` : ""}
        </button>
      ) : null}
    </div>
  );
}

type FilterSelectProps = SelectHTMLAttributes<HTMLSelectElement> & {
  label: string;
  options: { value: string; label: string }[];
};

/** Native, token-styled select for filters — accessible and dependency-free. */
export function FilterSelect({ label, options, className = "", id, ...props }: FilterSelectProps) {
  const selectId = id ?? `filter-${label.toLowerCase().replace(/\s+/g, "-")}`;
  return (
    <label htmlFor={selectId} className="flex flex-col gap-1 text-caption text-muted">
      {label}
      <select
        id={selectId}
        className={`min-h-11 rounded-md border border-border-strong bg-background px-2 text-body text-text transition duration-fast focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/30 ${className}`}
        {...props}
      >
        {options.map((o) => (
          <option key={o.value} value={o.value}>
            {o.label}
          </option>
        ))}
      </select>
    </label>
  );
}
