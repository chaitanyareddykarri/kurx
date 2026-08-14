"use client";

import { Menu as MenuIcon } from "lucide-react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { useState } from "react";

import { IconButton, Sheet } from "@kurx/ui";

import { primaryNav, secondaryNav, type NavItem } from "@/components/layout/nav-config";

/**
 * Navigation below `lg`.
 *
 * **This closes a critical gap.** The shell's only navigation was a sidebar
 * marked `hidden … lg:block`, with no drawer, bottom bar or hamburger anywhere
 * else — so below 1024px a user could not reach Home, Community, Posts,
 * Messages, Workspace, Tickets, Saved, Groups, Create event or Settings at all,
 * except by typing a URL. On a mobile-first, India-facing product that is the
 * whole application being unreachable on a phone.
 *
 * The bottom bar carries the same five areas as the Flutter shell
 * (`docs/ui-ux/information-architecture.md` §2), so the two surfaces teach one
 * map; "More" opens everything else in a Sheet, which already provides the focus
 * trap, Escape handling and scroll lock.
 */

function isActive(pathname: string, href: string) {
  return pathname === href || pathname.startsWith(`${href}/`);
}

function MoreLink({ item, onNavigate }: { item: NavItem; onNavigate: () => void }) {
  const Icon = item.icon;
  return (
    <Link
      href={item.href}
      onClick={onNavigate}
      className="flex min-h-11 items-center gap-md rounded-md px-3 text-body text-text transition duration-fast hover:bg-elevated"
    >
      <Icon size={18} aria-hidden="true" className="shrink-0 text-muted" />
      {item.label}
    </Link>
  );
}

export function MobileNav() {
  const pathname = usePathname();
  const [moreOpen, setMoreOpen] = useState(false);
  const close = () => setMoreOpen(false);

  return (
    <>
      <nav
        aria-label="Primary"
        // `pb-[env(safe-area-inset-bottom)]` keeps the bar clear of the iOS home
        // indicator without a magic number.
        className="fixed inset-x-0 bottom-0 z-header border-t border-border bg-background/95 pb-[env(safe-area-inset-bottom)] backdrop-blur lg:hidden"
      >
        <ul className="flex items-stretch">
          {primaryNav.map((item) => {
            const Icon = item.icon;
            const active = isActive(pathname, item.href);
            return (
              <li key={item.href} className="flex-1">
                <Link
                  href={item.href}
                  aria-current={active ? "page" : undefined}
                  className={`flex min-h-14 flex-col items-center justify-center gap-0.5 px-1 py-1.5 text-micro transition duration-fast ${
                    active ? "text-accent-text" : "text-muted"
                  }`}
                >
                  <Icon size={20} aria-hidden="true" />
                  <span className="truncate">{item.label}</span>
                </Link>
              </li>
            );
          })}
          <li className="flex-1">
            <button
              type="button"
              onClick={() => setMoreOpen(true)}
              aria-haspopup="dialog"
              aria-expanded={moreOpen}
              className="flex min-h-14 w-full flex-col items-center justify-center gap-0.5 px-1 py-1.5 text-micro text-muted transition duration-fast"
            >
              <MenuIcon size={20} aria-hidden="true" />
              <span>More</span>
            </button>
          </li>
        </ul>
      </nav>

      <Sheet open={moreOpen} onClose={close} side="bottom" title="More">
        <nav aria-label="Secondary" className="space-y-1">
          {secondaryNav.map((item) => (
            <MoreLink key={item.href} item={item} onNavigate={close} />
          ))}
        </nav>
      </Sheet>
    </>
  );
}
