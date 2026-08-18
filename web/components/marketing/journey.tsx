import { BadgeCheck, CalendarDays, Ticket, UserRound } from "lucide-react";

import { MotionPanel } from "@/components/ui/motion-panel";
import { Atmosphere } from "./atmosphere";
import { DEPTH, plane } from "./depth";
import { Tilt } from "./spatial";

/**
 * Scene 6 — where participation ends up.
 *
 * The visual identity's one-line reading of this product is *"the warm, credible record of what you
 * showed up for"*, and the strongest reason a person returns is that Kurx holds that record. The
 * page had nowhere that said so. This is that place.
 *
 * The four plates step **toward the viewer** — `base → raised → float → front`, the depth scale used
 * end to end and in order. That is the whole idea rendered as geometry: the further along you are,
 * the more present the record is. It needs no arrow, no number and no explanation under it.
 *
 * Every claim is a shipped surface: registrations and attendance exist, certificates are issued and
 * are verifiable by someone outside Kurx (`/certificates`), and `/u/{username}` is a real public
 * profile. Nothing here promises a feature that is not there, and there is no statistic to invent.
 */
const JOURNEY = [
  {
    icon: CalendarDays,
    label: "Event",
    body: "Find something worth showing up for.",
    z: DEPTH.base,
    tone: "accent" as const
  },
  {
    icon: Ticket,
    label: "Participation",
    body: "Register, book a ticket, and turn up.",
    z: DEPTH.raised,
    tone: "accent" as const
  },
  {
    icon: BadgeCheck,
    label: "Verified activity",
    body: "Attendance and certificates recorded against your account — and checkable from outside it.",
    z: DEPTH.float,
    tone: "teal" as const
  },
  {
    icon: UserRound,
    label: "Your profile",
    body: "A profile that carries what you actually did, not what you typed into a box.",
    z: DEPTH.front,
    tone: "accent" as const
  }
];

export function Journey() {
  return (
    <section className="relative border-b border-border bg-surface">
      <Atmosphere tone="teal" />

      <div className="container-shell relative py-16 sm:py-20">
        <p className="text-caption font-semibold text-accent-text">Your record</p>
        <h2 className="mt-2 max-w-3xl text-3xl font-semibold tracking-tight text-text sm:text-4xl">
          The events you show up for become part of your profile.
        </h2>
        <p className="mt-4 max-w-2xl text-body-lg leading-8 text-muted">
          Bookings, attendance and certificates collect against your account instead of scattering
          across inboxes — a record of participation someone outside Kurx can check.
        </p>

        {/*
          One perspective for the whole staircase, unlike the category shelf: these four are meant to
          be read as a single receding run, so they share a vanishing point and the far end genuinely
          looks further away.

          `Tilt` is capped lower here than anywhere else on the page. This is the widest rotating
          element in the composition, and rotation costs the outer plates the most — at the row's ends
          a few extra degrees turn into visible shear across a card that has a heading and a sentence
          on it. Three degrees is enough to make the run feel touched without bending the text.
        */}
        <div className="mt-12 [perspective:1600px]">
          <Tilt max={3}>
            <ol className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4 lg:gap-5">
              {JOURNEY.map(({ icon: Icon, label, body, z, tone }) => (
                <li key={label} style={plane(z)}>
                  <MotionPanel>
                    <div
                      className={`flex h-full flex-col rounded-lg border bg-background p-5 shadow-md ${
                        tone === "teal" ? "border-teal/35" : "border-border"
                      }`}
                    >
                      <span
                        className={`grid h-10 w-10 place-items-center rounded-md border bg-elevated shadow-sm ${
                          tone === "teal" ? "border-teal/40 text-teal" : "border-border text-accent-text"
                        }`}
                      >
                        <Icon size={18} aria-hidden />
                      </span>
                      <h3 className="mt-4 text-label text-text">{label}</h3>
                      <p className="mt-2 text-body text-muted">{body}</p>
                    </div>
                  </MotionPanel>
                </li>
              ))}
            </ol>
          </Tilt>
        </div>
      </div>
    </section>
  );
}
