"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";

/**
 * The event workspace's tab strip — up to seventeen destinations, depending on the caller's live
 * capabilities.
 *
 * Deliberately links in a `<nav>` rather than the ARIA tabs pattern: each destination is its own
 * route, so they must be shareable, bookmarkable and openable in a new tab, and `role="tab"` would
 * promise arrow-key navigation between panels that do not exist on this page.
 *
 * What it was missing is what the same shape was missing on `/workspace`: the current destination was
 * marked by a hue and a bottom border and nothing else, so a screen-reader user working through
 * seventeen links had no way to tell which one they were on. `aria-current` is what says it.
 */
export function EventWorkspaceTabs({ tabs }: { tabs: { key: string; label: string; href: string }[] }) {
  const pathname = usePathname();
  return (
    <nav aria-label="Event workspace" className="flex gap-1 overflow-x-auto border-b border-border">
      {tabs.map((t) => {
        const active = pathname === t.href;
        return (
          <Link
            key={t.key}
            href={t.href}
            aria-current={active ? "page" : undefined}
            className={`inline-flex min-h-11 shrink-0 items-center whitespace-nowrap border-b-2 px-4 text-sm font-medium transition duration-fast focus-visible:outline focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-accent ${
              active ? "border-accent text-text" : "border-transparent text-muted hover:text-text"
            }`}
          >
            {t.label}
          </Link>
        );
      })}
    </nav>
  );
}
