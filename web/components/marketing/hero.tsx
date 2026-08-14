import { ArrowRight, CalendarCheck, Check, ShieldCheck, Smartphone } from "lucide-react";
import { LinkButton } from "@/components/ui/button";

/**
 * The editorial hero (D-291, "Option 4"): a text column and a product showcase on one baseline,
 * with a structured stat card breaking the showcase's lower edge.
 *
 * **One layout, two themes.** Every colour here is a token, so light and dark are the same DOM with
 * different custom-property values — no `dark:` variant, no theme prop, no conditional markup. That
 * is the whole reason the structure cannot drift between them: there is only one structure.
 *
 * The showcase is the product's own console rather than a photograph. The brief's reference used a
 * stock crowd shot; a screenshot of the thing being sold is the more honest claim, and it needs no
 * binary asset in a repo that ships none.
 */

const TRUST = [
  ["Secure by design", ShieldCheck],
  ["Real-time sync", CalendarCheck],
  ["Works offline", Smartphone]
] as const;

/**
 * Illustrative console figures, unchanged from the previous hero. They sit INSIDE the depicted
 * product UI and are read as a screenshot's contents, which is why the stat card overlaps the
 * console rather than floating free of it — detached, the same numbers would read as a claim about
 * the platform's real volume.
 */
const STATS = [
  [CalendarCheck, "1,284", "Tickets sold"],
  [ShieldCheck, "9.2K", "Certificates issued"],
  [Smartphone, "560", "Check-ins today"]
] as const;

const FEED = [
  ["Annual Tech Conference", "Live"],
  ["Summer Music Festival", "Upcoming"],
  ["Design Systems Workshop", "Upcoming"]
] as const;

/**
 * A decorative activity ribbon closing the console — bars only, no labels, `aria-hidden`, and a
 * fixed sequence rather than a random one so the page renders identically on every request.
 *
 * It exists to give the console a lower band for the stat card to break into. Without it the card
 * landed on the feed rows and covered their text, which the brief rules out and which no amount of
 * z-index makes acceptable.
 */
const ACTIVITY = [38, 52, 44, 66, 58, 79, 63, 88, 71, 94, 82, 100] as const;

export function Hero() {
  return (
    <section className="relative overflow-hidden border-b border-border bg-background">
      <div className="grid-bg absolute inset-0" aria-hidden />

      <div className="container-shell relative grid gap-12 py-16 sm:py-20 lg:grid-cols-[minmax(0,1fr)_minmax(0,1.05fr)] lg:items-center lg:gap-16 lg:py-24">
        <div>
          <p className="inline-flex items-center rounded-pill border border-accent/30 bg-accent/10 px-3 py-1 text-caption font-semibold text-accent-text">
            All-in-one event platform
          </p>

          {/*
            The selective blue emphasis the brief asks for. `accent-text`, not `accent`: the fill is
            3.26:1 as text on the dark ground (D-288), and a headline is still text.
          */}
          <h1 className="mt-6 text-4xl font-semibold leading-[1.05] tracking-tight text-text sm:text-5xl lg:text-6xl">
            Events.
            <br />
            <span className="text-accent-text">Simplified.</span>
          </h1>

          <p className="mt-6 max-w-xl text-body-lg leading-8 text-muted">
            Discover events, book tickets, verify certificates, and run organizer operations from a
            single powerful platform.
          </p>

          <div className="mt-8 flex flex-wrap gap-3">
            <LinkButton href="/login">
              Get Started <ArrowRight size={16} aria-hidden />
            </LinkButton>
            <LinkButton href="/features" variant="secondary">
              Explore Features
            </LinkButton>
          </div>

          {/*
            Capability statements, not endorsements. The reference's "Organizers love us" is a
            testimonial, and there is nobody to attribute it to.
          */}
          <ul className="mt-8 flex flex-wrap gap-x-6 gap-y-2">
            {TRUST.map(([label, Icon]) => (
              <li key={label} className="flex items-center gap-2 text-caption text-muted">
                <span className="grid h-5 w-5 place-items-center rounded-full bg-accent/15 text-accent-text">
                  <Check size={12} aria-hidden />
                </span>
                <Icon size={14} className="text-accent-text" aria-hidden />
                {label}
              </li>
            ))}
          </ul>
        </div>

        {/*
          `lg:pb-16` reserves the band the stat card sits in, so `bottom-0` overlaps the console's
          lower edge without escaping the section. `right-6` keeps it inside the column at every
          width, which is what stops the overlap from becoming horizontal overflow.
        */}
        <div className="relative lg:pb-16">
          <div className="overflow-hidden rounded-xl border border-border bg-surface shadow-lg">
            <div className="flex items-center justify-between gap-3 border-b border-border px-5 py-4">
              <span className="text-label text-text">Live event console</span>
              <span className="rounded-pill border border-success/30 px-2 py-1 text-micro text-success">
                SignalR ready
              </span>
            </div>
            <div className="space-y-3 p-5">
              {FEED.map(([name, state]) => (
                <div
                  key={name}
                  className="flex items-center justify-between gap-3 rounded-md border border-border bg-elevated px-4 py-3"
                >
                  <span className="min-w-0 truncate text-body text-text">{name}</span>
                  <span
                    className={`shrink-0 text-micro ${state === "Live" ? "text-success" : "text-muted"}`}
                  >
                    {state}
                  </span>
                </div>
              ))}

              <div
                aria-hidden
                className="flex h-24 items-end gap-1.5 rounded-md border border-border bg-elevated p-3 lg:h-36"
              >
                {ACTIVITY.map((height, i) => (
                  <span key={i} className="flex-1 rounded-sm bg-accent/30" style={{ height: `${height}%` }} />
                ))}
              </div>
            </div>
          </div>

          <div className="mt-4 rounded-xl border border-border bg-surface p-4 shadow-lg lg:absolute lg:bottom-0 lg:right-6 lg:mt-0 lg:w-72">
            <ul className="space-y-3">
              {STATS.map(([Icon, value, label]) => (
                <li key={label} className="flex items-center gap-3">
                  <span className="grid h-9 w-9 shrink-0 place-items-center rounded-md border border-border bg-elevated text-accent-text">
                    <Icon size={16} aria-hidden />
                  </span>
                  <span className="min-w-0">
                    <strong className="block text-h3 text-text">{value}</strong>
                    <span className="text-caption text-muted">{label}</span>
                  </span>
                </li>
              ))}
            </ul>
          </div>
        </div>
      </div>
    </section>
  );
}
