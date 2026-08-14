"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";

// User-scoped settings sections. Every entry is backed by real user-level endpoints and nothing here
// is org-, event- or admin-scoped.
//
// Three entries were removed rather than relocated in the nav, because each had a better owner:
//   Identity      → the profile's Verification section, where its status is already shown
//   Notifications → the notifications screen, beside the notifications it governs
//   Representing  → not an account setting; it is authority to act, which belongs to hosting
// Their routes still resolve, so no existing link breaks.
const TABS = [
  { href: "/settings", label: "Overview" },
  { href: "/settings/profile", label: "Edit profile" },
  { href: "/settings/account", label: "Account" },
  { href: "/settings/privacy", label: "Privacy" },
  { href: "/settings/security", label: "Security" },
  { href: "/settings/blocked", label: "Blocked" },
  { href: "/settings/help", label: "Help" },
  { href: "/settings/legal", label: "Legal" }
];

export function SettingsNav() {
  const pathname = usePathname();
  return (
    // Named, and each tab declares whether it is the current page — neither was
    // true before, so the section list gave a screen-reader user no sense of
    // where in Settings they were.
    <nav aria-label="Settings sections" className="flex gap-1 overflow-x-auto border-b border-border">
      {TABS.map((t) => {
        const active = t.href === "/settings" ? pathname === "/settings" : pathname.startsWith(t.href);
        return (
          <Link
            key={t.href}
            href={t.href}
            aria-current={active ? "page" : undefined}
            className={`inline-flex min-h-11 items-center whitespace-nowrap border-b-2 px-4 text-body font-medium transition duration-fast ${
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
