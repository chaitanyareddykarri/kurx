"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";

import { primaryNav, secondaryNav, type NavItem } from "@/components/layout/nav-config";

/**
 * The desktop sidebar's link lists.
 *
 * A client component only so the active item can be marked from the pathname —
 * previously no sidebar item indicated the current page at all, in any state,
 * which left a user with no positional cue anywhere in the signed-in app.
 *
 * Each list is its own named `<nav>` so a screen reader can distinguish the
 * primary map from the secondary list rather than hearing one undifferentiated
 * run of links.
 */
function NavList({ items, label }: { items: NavItem[]; label: string }) {
  const pathname = usePathname();
  return (
    <nav aria-label={label}>
      <ul className="space-y-1">
        {items.map((item) => {
          const Icon = item.icon;
          const active = !item.external && (pathname === item.href || pathname.startsWith(`${item.href}/`));
          return (
            <li key={item.href}>
              <Link
                href={item.href}
                aria-current={active ? "page" : undefined}
                className={`flex min-h-11 items-center gap-md rounded-md px-3 text-body transition duration-fast ${
                  active ? "bg-elevated font-semibold text-text" : "text-muted hover:bg-elevated hover:text-text"
                }`}
              >
                <Icon size={17} aria-hidden="true" className="shrink-0" />
                {item.label}
                {/* Leaving the app is worth announcing; the admin console is a
                    separate origin (D-195). */}
                {item.external ? <span className="sr-only"> (opens the admin console)</span> : null}
              </Link>
            </li>
          );
        })}
      </ul>
    </nav>
  );
}

/** No reviewer branch: this app is the user account and shows no admin surface to anyone. The
 *  console has its own sign-in. See `nav-config.ts` for why the group was deleted rather than hidden. */
export function SidebarNav() {
  return (
    <div className="space-y-3">
      <NavList items={primaryNav} label="Primary" />
      <hr className="border-border" />
      <NavList items={secondaryNav} label="Secondary" />
    </div>
  );
}
