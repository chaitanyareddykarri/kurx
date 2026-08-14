"use client";

import { ChevronDown } from "lucide-react";
import { ReactNode, useId, useState } from "react";

/**
 * A collapsible filter group: a full-width header button, a chevron that turns, and its options
 * animating open beneath it. Starts CLOSED — the rails are 21 and 13 options long, and showing both
 * on arrival buried the results under two screens of pills.
 *
 * The open state is deliberately client-side rather than another query param. The options inside are
 * `<Link>`s, so picking one is a soft navigation; the App Router re-renders this segment but keeps
 * client component state for a component that stays mounted at the same position, which is what leaves
 * the section open across a pick. Putting `open` in the URL instead would mean threading it through
 * `hrefWith`'s whitelist on every chip, page link and remove-"×" — and it would make "which accordion
 * was showing" part of a shareable link, which it is not.
 *
 * Height is animated with the grid `0fr → 1fr` technique, not `max-height`. A max-height has to be
 * guessed, and any guess is wrong at some breakpoint: too small clips the last row, too large makes the
 * close look like it stalls before moving.
 */
export function FilterSection({
  title,
  /** Rendered next to the title when collapsed — the current pick stays legible with the group shut. */
  selectedLabel,
  children
}: {
  title: string;
  selectedLabel?: string;
  children: ReactNode;
}) {
  const [open, setOpen] = useState(false);
  const contentId = useId();

  return (
    <section className="border-b border-border pb-3 last:border-b-0">
      <h2>
        <button
          type="button"
          onClick={() => setOpen((v) => !v)}
          aria-expanded={open}
          aria-controls={contentId}
          className="flex min-h-11 w-full items-center justify-between gap-3 rounded-md text-left focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
        >
          <span className="flex min-w-0 items-center gap-2">
            <span className="text-micro uppercase tracking-wide text-muted">{title}</span>
            {/* Only while shut: with the group open the tick on the chip already says which one it is,
                and showing both reads as two different selections. */}
            {!open && selectedLabel ? (
              <span className="truncate rounded-full border border-accent bg-accent/15 px-2 py-0.5 text-caption text-text">
                {selectedLabel}
              </span>
            ) : null}
          </span>
          <ChevronDown
            size={16}
            aria-hidden="true"
            className={`shrink-0 text-muted transition-transform duration-300 motion-reduce:transition-none ${
              open ? "rotate-180" : ""
            }`}
          />
        </button>
      </h2>

      <div
        id={contentId}
        // `inert` while shut, so the collapsed options are skipped by Tab and by screen readers. Height
        // alone does not do that: a 0px row still holds focusable links.
        {...(!open ? ({ inert: "" } as Record<string, string>) : {})}
        className={`grid transition-[grid-template-rows] duration-300 ease-out motion-reduce:transition-none ${
          open ? "grid-rows-[1fr]" : "grid-rows-[0fr]"
        }`}
      >
        <div className="overflow-hidden">
          <div className="pt-3">{children}</div>
        </div>
      </div>
    </section>
  );
}
