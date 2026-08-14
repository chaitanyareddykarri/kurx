"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useEffect, useState } from "react";
import { ChevronDown } from "lucide-react";
import { KurxAdminLogo } from "@/components/brand/logo";
import { NAV } from "@/components/layout/nav-config";
import { hasRole, type PlatformRole } from "@/lib/roles";
import type { DashboardSummary } from "@/lib/api";

const COLLAPSE_KEY = "kurx-admin-sidebar-collapsed";

/** Real counts only — never a fabricated badge. Groups with no summary-backed number get none. */
function countsByHref(summary: DashboardSummary | null): Record<string, number> {
  if (!summary) return {};
  return {
    "/verification": summary.pending_org_verifications + summary.pending_membership_claims,
    "/events/pending": summary.pending_events,
    "/blacklist": summary.blacklist_entries
  };
}

export function Sidebar({
  roles,
  summary,
  onNavigate
}: {
  roles: PlatformRole[];
  summary: DashboardSummary | null;
  onNavigate?: () => void;
}) {
  const pathname = usePathname();
  const counts = countsByHref(summary);
  const [collapsed, setCollapsed] = useState<Record<string, boolean>>({});

  useEffect(() => {
    try {
      const raw = window.localStorage.getItem(COLLAPSE_KEY);
      if (raw) setCollapsed(JSON.parse(raw));
    } catch {
      // Corrupt/blocked storage just means every group starts expanded.
    }
  }, []);

  function toggleGroup(label: string) {
    setCollapsed((prev) => {
      const next = { ...prev, [label]: !prev[label] };
      try {
        window.localStorage.setItem(COLLAPSE_KEY, JSON.stringify(next));
      } catch {
        // Best-effort persistence only.
      }
      return next;
    });
  }

  return (
    <nav aria-label="Admin navigation" className="flex h-full flex-col gap-5 overflow-y-auto p-4">
      <Link href="/" onClick={onNavigate} className="inline-flex min-h-11 items-center rounded-md px-2 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent">
        <KurxAdminLogo />
      </Link>

      {NAV.map((group) => {
        const items = group.items.filter((i) => hasRole(roles, i.roles));
        if (items.length === 0) return null;
        const isOpen = !collapsed[group.label];

        return (
          <div key={group.label || "home"} className="space-y-1">
            {group.label ? (
              <button
                type="button"
                onClick={() => toggleGroup(group.label)}
                aria-expanded={isOpen}
                className="flex min-h-11 w-full items-center justify-between gap-2 rounded-md px-2 text-xs font-semibold uppercase tracking-wide text-muted transition duration-fast hover:text-text focus-visible:outline focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-accent"
              >
                {group.label}
                <ChevronDown size={13} aria-hidden className={`transition-transform ${isOpen ? "" : "-rotate-90"}`} />
              </button>
            ) : null}
            {isOpen ? (
              <ul className="space-y-0.5">
                {items.map((item) => {
                  const Icon = item.icon;
                  const active = pathname === item.href;
                  const count = counts[item.href];

                  // Every nav item is built and reachable as of Phase 26 — the five that were not
                  // are gone rather than rendered as permanently disabled spans. `ready` stays on the
                  // type so a future item can be staged, but nothing sets it false today.
                  if (!item.ready) return null;


                  return (
                    <li key={item.href} className="relative">
                      {active ? (
                        <span className="absolute inset-y-1.5 left-0 w-0.5 rounded-full bg-accent" aria-hidden />
                      ) : null}
                      <Link
                        href={item.href}
                        onClick={onNavigate}
                        aria-current={active ? "page" : undefined}
                        className={`flex min-h-11 items-center justify-between gap-3 rounded-md px-3 text-sm transition duration-fast focus-visible:outline focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-accent ${
                          active
                            ? "bg-elevated font-semibold text-text"
                            : "text-muted hover:bg-elevated hover:text-text"
                        }`}
                      >
                        <span className="flex items-center gap-3">
                          <Icon size={17} aria-hidden /> {item.label}
                        </span>
                        {/* A bare number beside a label reads as "Verification Queue 12", which does
                            not say what twelve of. The tint was also the Phase 6 trap — `text-accent`
                            on `bg-accent/15` eats exactly the contrast headroom the token was solved
                            for, and every tinted Badge tone failed AA on both themes for it. */}
                        {count ? (
                          <span
                            className={`rounded-full px-1.5 py-0.5 text-[10px] font-semibold ${
                              active ? "bg-elevated text-accent-text" : "bg-elevated text-muted"
                            }`}
                          >
                            <span aria-hidden>{count}</span>
                            <span className="sr-only">{count} waiting</span>
                          </span>
                        ) : null}
                      </Link>
                    </li>
                  );
                })}
              </ul>
            ) : null}
          </div>
        );
      })}
    </nav>
  );
}
