import { Bell, User } from "lucide-react";
import Link from "next/link";
import { ReactNode } from "react";

import { KurxLogo } from "@/components/brand/logo";
import { MobileNav } from "@/components/layout/mobile-nav";
import { SidebarNav } from "@/components/layout/sidebar-nav";
import { ThemeToggle } from "@/components/layout/theme-toggle";

/**
 * The signed-in application shell.
 *
 * Structure (`docs/ui-ux/information-architecture.md` §2): a five-area primary
 * navigation identical to the Flutter shell's tabs, with Profile and
 * Notifications as fixed header corners rather than nav entries.
 *
 * Three Phase 11 corrections:
 *
 *  - **Navigation exists below `lg` at all.** The sidebar was `hidden … lg:block`
 *    with nothing else anywhere, so on a phone or tablet the entire application
 *    was unreachable except by typing URLs. `MobileNav` is the fix.
 *  - **A skip link.** No page in web or admin had one, so a keyboard user tabbed
 *    through every navigation item on every page before reaching content.
 *  - **Real landmarks.** Each `<nav>` is named, so a screen reader can tell the
 *    primary map from the secondary list, and `<main>` is the skip target.
 */
export function AppShell({ children }: { children: ReactNode }) {
  return (
    <div className="min-h-screen bg-background">
      {/*
        Visually hidden until focused. First focusable element on the page, so
        Tab-then-Enter goes straight to content.
      */}
      <a
        href="#main-content"
        className="sr-only focus:not-sr-only focus:fixed focus:left-4 focus:top-4 focus:z-toast focus:rounded-md focus:border focus:border-border-strong focus:bg-surface focus:px-4 focus:py-2 focus:text-label focus:text-text"
      >
        Skip to content
      </a>

      <aside className="fixed inset-y-0 left-0 z-header hidden w-64 overflow-y-auto border-r border-border bg-surface p-4 lg:block">
        <Link href="/discover" className="mb-6 block rounded-md" aria-label="Kurx home">
          <KurxLogo />
        </Link>
        <SidebarNav />
      </aside>

      <div className="lg:pl-64">
        <header className="sticky top-0 z-sticky border-b border-border bg-background/90 backdrop-blur">
          <div className="flex h-14 items-center gap-2 px-4">
            <Link
              href="/profile"
              className="flex min-h-11 items-center gap-2 rounded-md px-2 text-body text-muted transition duration-fast hover:bg-elevated hover:text-text"
            >
              <User size={18} aria-hidden="true" />
              <span className="hidden sm:inline">Profile</span>
              <span className="sr-only sm:hidden">Profile</span>
            </Link>
            <div className="lg:hidden">
              <KurxLogo compact />
            </div>
            <span className="flex-1" />
            <Link
              href="/notifications"
              className="inline-flex h-11 w-11 items-center justify-center rounded-md text-muted transition duration-fast hover:bg-elevated hover:text-text"
            >
              <Bell size={18} aria-hidden="true" />
              <span className="sr-only">Notifications</span>
            </Link>
            <ThemeToggle />
          </div>
        </header>

        {/*
          `pb-20` on small screens reserves room for the fixed bottom bar so the
          last row of any list is not trapped underneath it.
        */}
        <main id="main-content" tabIndex={-1} className="p-4 pb-20 focus:outline-none sm:p-6 sm:pb-24 lg:pb-6">
          {children}
        </main>
      </div>

      <MobileNav />
    </div>
  );
}
