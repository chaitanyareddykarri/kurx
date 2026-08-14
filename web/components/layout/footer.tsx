import Link from "next/link";

import { KurxLogo } from "@/components/brand/logo";
import { siteConfig } from "@/lib/site";

/**
 * The public site footer.
 *
 * There was no footer anywhere in web, so the legal pages (`/privacy`,
 * `/terms`) were reachable only by typing their URLs — no page linked to
 * either. For a ticketing product taking payments in India that is a real gap,
 * not only a navigational one.
 *
 * Grouped into named `<nav>` landmarks rather than one flat list, so a screen
 * reader can skip a group instead of walking every link.
 */
const GROUPS: Array<{ label: string; links: Array<{ href: string; label: string }> }> = [
  {
    label: "Product",
    links: [
      { href: "/features", label: "Features" },
      { href: "/pricing", label: "Pricing" },
      { href: "/discover", label: "Discover events" }
    ]
  },
  {
    label: "Company",
    links: [
      { href: "/about", label: "About" },
      { href: "/blog", label: "Blog" },
      { href: "/contact", label: "Contact" }
    ]
  },
  {
    label: "Legal",
    links: [
      { href: "/privacy", label: "Privacy" },
      { href: "/terms", label: "Terms" },
      { href: "/support", label: "Support" }
    ]
  }
];

export function Footer() {
  return (
    <footer className="mt-3xl border-t border-border bg-surface">
      <div className="container-shell flex flex-col gap-xl py-xl sm:flex-row sm:justify-between">
        <div>
          <KurxLogo />
          <p className="mt-md max-w-xs text-caption text-muted">{siteConfig.description}</p>
        </div>
        <div className="grid grid-cols-2 gap-xl sm:grid-cols-3">
          {GROUPS.map((group) => (
            <nav key={group.label} aria-label={group.label}>
              <h2 className="text-micro uppercase tracking-wide text-muted">{group.label}</h2>
              <ul className="mt-sm space-y-1">
                {group.links.map((link) => (
                  <li key={link.href}>
                    <Link
                      href={link.href}
                      // `min-w-11` too, not just the height: a short label like "Blog" was a 31px-wide
                      // target, and this project's floor is 44 in BOTH axes below 768px. The box is
                      // wider than the text; `justify-start` keeps the text where it was.
                      className="inline-flex min-h-11 min-w-11 items-center justify-start rounded-md text-body text-muted transition duration-fast hover:text-text focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
                    >
                      {link.label}
                    </Link>
                  </li>
                ))}
              </ul>
            </nav>
          ))}
        </div>
      </div>
      <div className="border-t border-border">
        <p className="container-shell py-md text-caption text-muted">
          © {new Date().getFullYear()} {siteConfig.name}
        </p>
      </div>
    </footer>
  );
}
