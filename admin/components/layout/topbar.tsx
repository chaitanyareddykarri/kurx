"use client";

import { useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { useRouter, usePathname } from "next/navigation";
import { ChevronRight, Menu, Search } from "lucide-react";
import { Dialog } from "@kurx/ui";
import { ThemeToggle } from "@/components/layout/theme-toggle";
import { UserMenu } from "@/components/layout/user-menu";
import { KurxAdminLogo } from "@/components/brand/logo";
import { NAV, type NavGroup } from "@/components/layout/nav-config";
import { hasRole, type PlatformRole } from "@/lib/roles";

/** Longest-href match wins, so a nested route (`/users/[id]`) still resolves to its list-page
 *  crumb ("Users") rather than falling through to Dashboard's `/`. */
function activeCrumb(pathname: string, roles: PlatformRole[]) {
  let best: { group: NavGroup; label: string; href: string } | null = null;
  for (const group of NAV) {
    for (const item of group.items) {
      if (!item.ready || !hasRole(roles, item.roles)) continue;
      const matches = item.href === "/" ? pathname === "/" : pathname === item.href || pathname.startsWith(`${item.href}/`);
      if (matches && (!best || item.href.length > best.href.length)) {
        best = { group, label: item.label, href: item.href };
      }
    }
  }
  return best;
}

function NavPalette({ roles, onClose }: { roles: PlatformRole[]; onClose: () => void }) {
  const router = useRouter();
  const [query, setQuery] = useState("");

  const results = useMemo(() => {
    const q = query.trim().toLowerCase();
    return NAV.flatMap((group) =>
      group.items
        .filter((i) => i.ready && hasRole(roles, i.roles))
        .filter((i) => !q || i.label.toLowerCase().includes(q) || group.label.toLowerCase().includes(q))
        .map((i) => ({ ...i, groupLabel: group.label }))
    );
  }, [query, roles]);

  function go(href: string) {
    router.push(href);
    onClose();
  }

  return (
    <Dialog open onClose={onClose} title="Jump to">
      <div className="-mx-5 -mt-4 border-b border-border px-5 py-3">
        <div className="relative">
          <Search size={15} className="pointer-events-none absolute left-2.5 top-2.5 text-muted" />
          {/* eslint-disable-next-line jsx-a11y/no-autofocus -- palette opens only on explicit Ctrl/Cmd+K, autofocus is the expected behavior */}
          <input
            autoFocus
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === "Enter" && results[0]) go(results[0].href);
            }}
            placeholder="Search pages…"
            className="h-9 w-full rounded-md border border-border-strong bg-background pl-8 pr-3 text-sm text-text placeholder:text-muted focus:border-accent focus:outline-none"
          />
        </div>
      </div>
      <ul className="-mx-5 -mb-4 max-h-80 overflow-y-auto py-2">
        {results.length === 0 ? (
          <li className="px-5 py-6 text-center text-sm text-muted">No pages match &ldquo;{query}&rdquo;.</li>
        ) : (
          results.map((item) => {
            const Icon = item.icon;
            return (
              <li key={item.href}>
                <button
                  type="button"
                  onClick={() => go(item.href)}
                  className="flex w-full items-center gap-3 px-5 py-2 text-left text-sm text-text hover:bg-elevated"
                >
                  <Icon size={16} className="text-muted" />
                  {item.label}
                  <span className="ml-auto text-xs text-muted">{item.groupLabel}</span>
                </button>
              </li>
            );
          })
        )}
      </ul>
    </Dialog>
  );
}

export function Topbar({
  name,
  roles,
  onMenu
}: {
  name: string;
  roles: PlatformRole[];
  onMenu: () => void;
}) {
  const pathname = usePathname();
  const [paletteOpen, setPaletteOpen] = useState(false);
  const crumb = activeCrumb(pathname, roles);

  useEffect(() => {
    function onKey(e: KeyboardEvent) {
      if ((e.metaKey || e.ctrlKey) && e.key.toLowerCase() === "k") {
        e.preventDefault();
        setPaletteOpen(true);
      }
    }
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, []);

  return (
    <header className="sticky top-0 z-20 border-b border-border bg-background/90 backdrop-blur">
      <div className="flex h-14 items-center justify-between gap-3 px-4">
        <div className="flex min-w-0 items-center gap-3">
          <button
            type="button"
            onClick={onMenu}
            aria-label="Open navigation"
            className="grid h-10 w-10 shrink-0 place-items-center rounded-md border border-border bg-surface text-muted hover:bg-elevated hover:text-text lg:hidden"
          >
            <Menu size={18} />
          </button>
          {/* At 320px the icon cluster alone fills the bar; the wordmark returns at xs. */}
          <div className="hidden xs:block lg:hidden">
            <KurxAdminLogo compact />
          </div>

          <nav aria-label="Breadcrumb" className="hidden min-w-0 items-center gap-1.5 text-sm text-muted lg:flex">
            <Link href="/" className="shrink-0 hover:text-text">
              Admin
            </Link>
            {crumb && crumb.href !== "/" ? (
              <>
                <ChevronRight size={14} className="shrink-0" />
                <span className="shrink-0">{crumb.group.label}</span>
                <ChevronRight size={14} className="shrink-0" />
                <span className="truncate font-medium text-text">{crumb.label}</span>
              </>
            ) : null}
          </nav>
        </div>

        <div className="flex items-center gap-2">
          <button
            type="button"
            onClick={() => setPaletteOpen(true)}
            className="hidden h-10 items-center gap-2 rounded-md border border-border bg-surface px-3 text-sm text-muted transition hover:bg-elevated hover:text-text sm:flex"
          >
            <Search size={15} />
            Search
            <kbd className="rounded border border-border bg-elevated px-1.5 py-0.5 text-[10px] font-semibold text-muted">
              ⌘K
            </kbd>
          </button>
          <button
            type="button"
            onClick={() => setPaletteOpen(true)}
            aria-label="Search pages"
            className="grid h-10 w-10 place-items-center rounded-md border border-border bg-surface text-muted hover:bg-elevated hover:text-text sm:hidden"
          >
            <Search size={17} />
          </button>
          <ThemeToggle />
          <UserMenu name={name} roles={roles} />
        </div>
      </div>

      {paletteOpen ? <NavPalette roles={roles} onClose={() => setPaletteOpen(false)} /> : null}
    </header>
  );
}
