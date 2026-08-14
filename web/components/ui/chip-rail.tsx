"use client";

import Link from "next/link";
import { Check } from "lucide-react";

import { chipClass } from "@/components/ui/chip";

export type ChipRailItem = {
  /** Stable React key — a slug or id, never the index. */
  key: string;
  label: string;
  href: string;
  selected?: boolean;
};

/**
 * A filter rail laid out as a uniform responsive grid rather than `flex-wrap`.
 *
 * Free-flowing pills wrap wherever the text happens to run out, so 21 kinds and 13 categories rendered
 * as two ragged blocks with rows breaking at different places and a stray chip stranded on its own line.
 * A grid fixes the column count per breakpoint instead, so every chip shares one width and the rows line
 * up into columns. The cost is truncation on the longest labels, which `title` and the wider breakpoints
 * cover; the gain is that the section reads as a table of options instead of a spill of pills.
 *
 * Client component, and it has to be: `chipClass` is re-exported from `@kurx/ui`'s chip module, which
 * is marked `"use client"`. Importing a plain FUNCTION across that boundary from a server component
 * hands back a client reference, not the function — it renders fine at build time and throws
 * `chipClass is not a function` on the first real request. The chips are still `<Link>`s and the URL is
 * still the whole filter state; only the class helper forces the boundary. Moving `chipClass` into its
 * own module without `"use client"` would let this go back to being a server component.
 */
export function ChipRail({ items, ariaLabel }: { items: ChipRailItem[]; ariaLabel: string }) {
  if (items.length === 0) return null;
  return (
    <nav
      aria-label={ariaLabel}
      className="grid grid-cols-2 gap-2 sm:grid-cols-3 lg:grid-cols-4 xl:grid-cols-6"
    >
      {items.map((item) => (
        <Link
          key={item.key}
          href={item.href}
          title={item.label}
          aria-current={item.selected ? "true" : undefined}
          // `w-full` makes the pill fill its cell so the columns actually align; `min-w-0` is what lets
          // the label inside it truncate instead of blowing the track wider than its siblings.
          className={`${chipClass(Boolean(item.selected))} w-full min-w-0 justify-start`}
        >
          {/* The accent tint alone is easy to miss on a rail this size, and it is the only signal a
              filter is active. The tick is redundant on purpose. */}
          {item.selected ? <Check size={14} aria-hidden="true" className="shrink-0" /> : null}
          <span className="truncate">{item.label}</span>
        </Link>
      ))}
    </nav>
  );
}
