import Link from "next/link";
import { ArrowRight, BadgeCheck, ScanLine, SlidersHorizontal, Ticket } from "lucide-react";

/**
 * The organized feature section (D-291) — structured cards on one grid, which is what Option 4 asks
 * for in place of scattered floating panels.
 *
 * Every card is one shape: icon tile, title, one sentence, one link. Equal weight is the point; a
 * card that grew a second paragraph or a second action would break the row's rhythm, which is
 * exactly the "random text floating on a white wall" the brief rules out.
 *
 * Copy describes shipped surfaces only — each link goes somewhere that exists.
 */
const FEATURES = [
  {
    icon: Ticket,
    title: "Discover & book",
    body: "Find events, book tickets, and keep every booking in one place.",
    href: "/discover",
    action: "Explore events"
  },
  {
    icon: BadgeCheck,
    title: "Certificates",
    body: "Issue and verify certificates that hold up to an outside check.",
    href: "/features",
    action: "How it works"
  },
  {
    icon: SlidersHorizontal,
    title: "Organizer tools",
    body: "Run events, attendees, payouts, and analytics from one workspace.",
    href: "/features",
    action: "See the tools"
  },
  {
    icon: ScanLine,
    title: "Mobile check-in",
    body: "Scan and check in at the door, with a queue that survives no signal.",
    href: "#download-app",
    action: "Get the app"
  }
] as const;

export function FeatureGrid() {
  return (
    <section className="border-b border-border bg-background">
      <div className="container-shell py-16 sm:py-20">
        <p className="text-caption font-semibold text-accent-text">Platform</p>
        <h2 className="mt-2 max-w-2xl text-3xl font-semibold tracking-tight text-text sm:text-4xl">
          Everything an event needs, in one place.
        </h2>

        <ul className="mt-10 grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          {FEATURES.map(({ icon: Icon, title, body, href, action }) => (
            <li
              key={title}
              className="flex flex-col rounded-lg border border-border bg-surface p-5 shadow-sm"
            >
              <span className="grid h-10 w-10 place-items-center rounded-md border border-border bg-elevated text-accent-text">
                <Icon size={18} aria-hidden />
              </span>
              <h3 className="mt-4 text-h3 text-text">{title}</h3>
              <p className="mt-2 flex-1 text-body text-muted">{body}</p>
              <Link
                href={href}
                className="mt-4 inline-flex min-h-11 items-center gap-1.5 text-label text-accent-text focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
              >
                {action}
                <ArrowRight size={14} aria-hidden />
              </Link>
            </li>
          ))}
        </ul>
      </div>
    </section>
  );
}
