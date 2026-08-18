import Link from "next/link";
import { ArrowRight, BadgeCheck, ScanLine, SlidersHorizontal, Ticket } from "lucide-react";

import { MotionPanel } from "@/components/ui/motion-panel";
import { Atmosphere } from "./atmosphere";
import { DEPTH, plane } from "./depth";
import { Tilt } from "./spatial";

/**
 * Scene 5 — the platform, as four objects on one shelf.
 *
 * The content is untouched: four cards, one shape each — icon, title, one sentence, one link — and
 * every link still goes to a route that exists. Equal weight is still the point; a card that grew a
 * second paragraph or a second action would break the row, which is the "random text floating on a
 * white wall" the original brief ruled out.
 *
 * The change is depth. The icon plinth sits forward of the card face and the copy sits on it, so
 * turning a card separates the two — the same trick as the category tiles, at a smaller angle
 * because these carry a paragraph and a link rather than a single word.
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
    <section className="relative border-b border-border bg-background">
      <Atmosphere />

      <div className="container-shell relative py-16 sm:py-20">
        <p className="text-caption font-semibold text-accent-text">Platform</p>
        <h2 className="mt-2 max-w-2xl text-3xl font-semibold tracking-tight text-text sm:text-4xl">
          Everything an event needs, in one place.
        </h2>

        <ul className="mt-10 grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          {FEATURES.map(({ icon: Icon, title, body, href, action }) => (
            <li key={title} className="[perspective:1100px]">
              <MotionPanel>
                <Tilt
                  max={6}
                  className="flex h-full flex-col rounded-lg border border-border bg-surface p-5 shadow-sm transition-[border-color,box-shadow] duration-base ease-kurx hover:border-accent/40 hover:shadow-lg"
                >
                  <span
                    className="grid h-11 w-11 place-items-center rounded-md border border-border bg-elevated text-accent-text shadow-md"
                    style={plane(DEPTH.raised)}
                  >
                    <Icon size={18} aria-hidden />
                  </span>
                  <h3 className="mt-4 text-h3 text-text" style={plane(DEPTH.base)}>
                    {title}
                  </h3>
                  <p className="mt-2 flex-1 text-body text-muted" style={plane(DEPTH.base)}>
                    {body}
                  </p>
                  {/*
                    The link keeps its own 44px target and its own focus ring. Depth is applied to the
                    row it sits in, never to the interactive element itself — a focus outline drawn on
                    a transformed plane is drawn at an angle, and a keyboard user needs it square.
                  */}
                  <span className="mt-4 block" style={plane(DEPTH.raised)}>
                    <Link
                      href={href}
                      className="inline-flex min-h-11 items-center gap-1.5 text-label text-accent-text focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
                    >
                      {action}
                      <ArrowRight size={14} aria-hidden />
                    </Link>
                  </span>
                </Tilt>
              </MotionPanel>
            </li>
          ))}
        </ul>
      </div>
    </section>
  );
}
