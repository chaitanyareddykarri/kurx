"use client";

import { useEffect, useState } from "react";

export type ProfileNavItem = { id: string; label: string };

/**
 * Sticky in-page navigation for the profile.
 *
 * Real anchors, not buttons: the sections exist in the document whether or not JS runs, so
 * `href="#id"` already works and the observer only adds the active mark on top. Building this from
 * `onClick` + `scrollIntoView` would have broken middle-click, open-in-new-tab and no-JS entirely
 * for zero gain.
 *
 * `rootMargin` biases the observer toward the top of the viewport so the item highlights when its
 * section reaches reading position rather than when it merely peeks in from the bottom.
 */
export function ProfileNav({ items }: { items: readonly ProfileNavItem[] }) {
  const [active, setActive] = useState(items[0]?.id ?? "");

  useEffect(() => {
    if (items.length === 0) return;
    const observer = new IntersectionObserver(
      (entries) => {
        // Topmost intersecting section wins, so two visible sections resolve deterministically
        // instead of flickering between them as the user scrolls.
        const visible = entries
          .filter((e) => e.isIntersecting)
          .sort((a, b) => a.boundingClientRect.top - b.boundingClientRect.top);
        if (visible[0]) setActive(visible[0].target.id);
      },
      { rootMargin: "-96px 0px -55% 0px", threshold: 0 }
    );

    const nodes = items
      .map((i) => document.getElementById(i.id))
      .filter((n): n is HTMLElement => n !== null);
    nodes.forEach((n) => observer.observe(n));
    return () => observer.disconnect();
  }, [items]);

  if (items.length === 0) return null;

  return (
    <nav
      aria-label="Profile sections"
      className="sticky top-0 z-header -mx-lg border-b border-border bg-background/95 px-lg backdrop-blur"
    >
      <ul className="flex gap-1 overflow-x-auto">
        {items.map((item) => {
          const isActive = active === item.id;
          return (
            <li key={item.id}>
              <a
                href={`#${item.id}`}
                aria-current={isActive ? "true" : undefined}
                className={`inline-flex min-h-11 items-center whitespace-nowrap border-b-2 px-3 text-sm font-medium transition duration-fast ${
                  isActive
                    ? "border-accent text-text"
                    : "border-transparent text-muted hover:text-text"
                }`}
              >
                {item.label}
              </a>
            </li>
          );
        })}
      </ul>
    </nav>
  );
}
