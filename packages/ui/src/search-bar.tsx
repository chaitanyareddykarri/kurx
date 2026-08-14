"use client";

import { Search, X } from "lucide-react";
import { useEffect, useId, useRef, useState } from "react";

type SearchBarProps = {
  /** Controlled query value. */
  value: string;
  /** Fires after `debounceMs` of no typing (or immediately on clear/submit). */
  onChange: (value: string) => void;
  placeholder?: string;
  debounceMs?: number;
  className?: string;
  "aria-label"?: string;
  /**
   * Number of results the current query produced.
   *
   * Supply it wherever the caller knows: a debounced search silently replaces
   * the page under a screen-reader user, who otherwise gets no signal that
   * anything happened. With this, the count is announced politely after each
   * settle.
   */
  resultCount?: number;
  /** Id of the element the results are rendered into, for `aria-controls`. */
  controls?: string;
};

/**
 * Debounced search input. Emits `onChange` after the user pauses typing, so
 * callers can drive queries directly off it without wiring their own debounce.
 *
 * Phase 12 corrections: the field used `border` (1.30:1 — a boundary that cannot
 * identify a control, WCAG 1.4.11), stood 40px tall against a 44px touch floor,
 * and its clear button was a 24px target.
 */
export function SearchBar({
  value,
  onChange,
  placeholder = "Search…",
  debounceMs = 300,
  className = "",
  "aria-label": ariaLabel = "Search",
  resultCount,
  controls
}: SearchBarProps) {
  const [text, setText] = useState(value);
  const timer = useRef<ReturnType<typeof setTimeout>>();
  const statusId = useId();
  // Only announce once the user has actually searched — otherwise the initial
  // unfiltered count is read out on every page load for no reason.
  const [touched, setTouched] = useState(false);

  // Keep local input in sync when the controlled value is reset externally.
  useEffect(() => setText(value), [value]);

  function emit(next: string, immediate = false) {
    setText(next);
    setTouched(true);
    clearTimeout(timer.current);
    if (immediate) {
      onChange(next);
      return;
    }
    timer.current = setTimeout(() => onChange(next), debounceMs);
  }

  useEffect(() => () => clearTimeout(timer.current), []);

  return (
    <div role="search" className={`relative ${className}`}>
      <Search
        size={16}
        aria-hidden="true"
        className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-muted"
      />
      <input
        type="search"
        value={text}
        aria-label={ariaLabel}
        aria-controls={controls}
        aria-describedby={resultCount === undefined ? undefined : statusId}
        placeholder={placeholder}
        onChange={(e) => emit(e.target.value)}
        onKeyDown={(e) => e.key === "Enter" && emit(text, true)}
        className="min-h-11 w-full rounded-md border border-border-strong bg-background pl-9 pr-12 text-body text-text placeholder:text-muted transition duration-fast focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/30"
      />
      {text ? (
        <button
          type="button"
          aria-label="Clear search"
          onClick={() => emit("", true)}
          className="absolute right-1 top-1/2 grid h-10 w-10 -translate-y-1/2 place-items-center rounded-md text-muted transition duration-fast hover:text-text focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
        >
          <X size={16} aria-hidden="true" />
        </button>
      ) : null}
      {resultCount === undefined ? null : (
        <p id={statusId} role="status" className="sr-only">
          {touched ? `${resultCount} ${resultCount === 1 ? "result" : "results"}` : ""}
        </p>
      )}
    </div>
  );
}
