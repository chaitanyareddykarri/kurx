"use client";

import { ReactNode, useRef, useState } from "react";
import { ToastProvider, useOverlay } from "@kurx/ui";
import { Sidebar } from "@/components/layout/sidebar";
import { Topbar } from "@/components/layout/topbar";
import type { PlatformRole } from "@/lib/roles";
import type { DashboardSummary } from "@/lib/api";

/** The console frame: fixed sidebar on desktop, off-canvas drawer on mobile, sticky
 *  topbar, and the routed main region. Role-based nav is filtered inside Sidebar. */
export function AppShell({
  children,
  roles,
  name,
  summary
}: {
  children: ReactNode;
  roles: PlatformRole[];
  name: string;
  summary: DashboardSummary | null;
}) {
  const [mobileOpen, setMobileOpen] = useState(false);
  const drawerRef = useRef<HTMLElement>(null);

  /*
   * The mobile drawer declared `role="dialog"` and `aria-modal="true"` and implemented none of it:
   * focus never entered the panel, Tab walked straight onto the page behind, focus was never restored
   * to the menu button on close, the body kept scrolling, and Escape did nothing. Claiming the
   * background is inert without making it inert is worse than omitting the attribute, because
   * assistive tech believes it — which is exactly what audit S1-2 said, and what Phase 9 built
   * `useOverlay` to fix for `Dialog` and `Sheet`. Admin's own navigation was never wired to it.
   *
   * Not `Sheet`: it opens right or bottom only, and forces a title header this drawer does not want
   * around a full sidebar. `useOverlay` is the shared behaviour without the chrome.
   */
  useOverlay({ open: mobileOpen, onClose: () => setMobileOpen(false), panelRef: drawerRef });

  return (
    <ToastProvider>
      <div className="min-h-screen bg-background">
        <a
          href="#main"
          className="sr-only focus:not-sr-only focus:absolute focus:left-3 focus:top-3 focus:z-toast focus:rounded-md focus:border focus:border-border focus:bg-surface focus:px-3 focus:py-2 focus:text-sm"
        >
          Skip to content
        </a>

        {/* Desktop sidebar */}
        <aside className="fixed inset-y-0 left-0 z-30 hidden w-64 border-r border-border bg-surface lg:block">
          <Sidebar roles={roles} summary={summary} />
        </aside>

        {/* Mobile drawer */}
        {mobileOpen ? (
          <div className="fixed inset-0 z-modal lg:hidden">
            <div className="absolute inset-0 bg-black/60" onClick={() => setMobileOpen(false)} aria-hidden />
            <aside
              ref={drawerRef}
              role="dialog"
              aria-modal="true"
              aria-label="Navigation"
              tabIndex={-1}
              className="absolute inset-y-0 left-0 w-64 border-r border-border bg-surface focus:outline-none"
            >
              <Sidebar roles={roles} summary={summary} onNavigate={() => setMobileOpen(false)} />
            </aside>
          </div>
        ) : null}

        <div className="lg:pl-64">
          <Topbar name={name} roles={roles} onMenu={() => setMobileOpen(true)} />
          <main id="main" className="p-4 sm:p-6">
            {children}
          </main>
        </div>
      </div>
    </ToastProvider>
  );
}
