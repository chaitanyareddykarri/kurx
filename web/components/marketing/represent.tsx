import { ArrowRight, BadgeCheck, CalendarCheck, UserRound } from "lucide-react";

import { MotionPanel } from "@/components/ui/motion-panel";
import { Atmosphere } from "./atmosphere";
import { DEPTH, plane } from "./depth";
import { Tilt } from "./spatial";

/**
 * Scene 4 — how organizing actually works here.
 *
 * This section exists because the default assumption a visitor brings to an event platform is
 * *organization signs up → organization logs in → organization posts events*, and on Kurx that is
 * wrong at every step. There are no organization accounts and no organization rosters (D-267/D-268):
 * a **user owns the event**, and the organization on it is the one that user **represents**
 * (D-271, D-379). A homepage that leaves the default assumption standing sends people looking for a
 * login that does not exist.
 *
 * So the three steps are the real gate, in the real order: personal sign-in, then authorization
 * evidence a reviewer approves, then an event that carries both facts. Nothing here is aspirational
 * — it is the flow behind `host/representing` and the review queue.
 *
 * Step 2 is the only teal object in the section, per P2: representation is an attestation, and it is
 * the one thing on this page a visitor should understand is *checked* rather than claimed.
 */
const STEPS = [
  {
    icon: UserRound,
    step: "You",
    title: "Sign in as yourself",
    body: "One personal account. There is no organization login to hand around, because organizations do not have accounts.",
    tone: "accent" as const
  },
  {
    icon: BadgeCheck,
    step: "Represents",
    title: "Prove you represent them",
    body: "Submit the organization's authorization. A reviewer checks it before the event can go to publication.",
    tone: "teal" as const
  },
  {
    icon: CalendarCheck,
    step: "Event",
    title: "The event carries both",
    body: "You own and run it from your host workspace. The organization is named on it, because you showed you speak for them.",
    tone: "accent" as const
  }
];

export function Represent() {
  return (
    <section className="relative border-b border-border bg-surface">
      <Atmosphere tone="teal" />

      <div className="container-shell relative py-16 sm:py-20">
        <p className="text-caption font-semibold text-accent-text">How organizing works</p>
        <h2 className="mt-2 max-w-3xl text-3xl font-semibold tracking-tight text-text sm:text-4xl">
          A person creates the event. The organization is who they represent.
        </h2>
        <p className="mt-4 max-w-2xl text-body-lg leading-8 text-muted">
          Kurx has no organization accounts. You sign in as yourself, show you are authorized to act
          for an organization, and the event goes out under both names.
        </p>

        <ol className="relative mt-12 grid gap-4 lg:grid-cols-3 lg:gap-6">
          {/*
            The thread the three objects hang on. It sits behind them and stops short of both ends, so
            it reads as a connection between the cards rather than a rule ruling the whole row — and
            it is only drawn where the cards are actually side by side.
          */}
          <div
            aria-hidden
            className="absolute inset-x-[16%] top-16 hidden h-px lg:block"
            style={{
              background:
                "linear-gradient(90deg, transparent, rgb(var(--color-accent) / 0.4), rgb(var(--color-teal) / 0.5), rgb(var(--color-accent) / 0.4), transparent)"
            }}
          />

          {STEPS.map(({ icon: Icon, step, title, body, tone }, i) => (
            <li key={title} className="relative [perspective:1100px]">
              <MotionPanel>
                <Tilt
                  max={6}
                  className="flex h-full flex-col rounded-lg border border-border bg-background p-6 shadow-md transition-[border-color,box-shadow] duration-base ease-kurx hover:border-accent/40 hover:shadow-lg"
                >
                  <div className="flex items-center gap-3" style={plane(DEPTH.raised)}>
                    <span
                      className={`grid h-11 w-11 shrink-0 place-items-center rounded-md border bg-elevated shadow-md ${
                        tone === "teal" ? "border-teal/40 text-teal" : "border-border text-accent-text"
                      }`}
                    >
                      <Icon size={20} aria-hidden />
                    </span>
                    <span className="text-micro uppercase tracking-wider text-muted">{step}</span>
                  </div>

                  <h3 className="mt-5 text-h3 text-text" style={plane(DEPTH.base)}>
                    {title}
                  </h3>
                  <p className="mt-2 text-body text-muted" style={plane(DEPTH.base)}>
                    {body}
                  </p>
                </Tilt>
              </MotionPanel>

              {/* Direction, between the cards, on the plane in front of them. */}
              {i < STEPS.length - 1 ? (
                <span
                  aria-hidden
                  className="absolute -right-3 top-16 hidden h-6 w-6 place-items-center rounded-full border border-border bg-surface text-muted lg:grid"
                >
                  <ArrowRight size={12} />
                </span>
              ) : null}
            </li>
          ))}
        </ol>
      </div>
    </section>
  );
}
