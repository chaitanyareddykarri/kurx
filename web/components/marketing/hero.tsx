import { ArrowRight, CalendarCheck, Check, ShieldCheck, Smartphone } from "lucide-react";

import { LinkButton } from "@/components/ui/button";
import { Atmosphere } from "./atmosphere";
import { HeroStage } from "./hero-stage";

/**
 * The spatial hero (D-380).
 *
 * **The text column is the hero.** It is ordinary server-rendered HTML — eyebrow, `h1`, lead, two
 * CTAs — and it paints before a line of scene code runs. If the spatial layer fails to hydrate, is
 * blocked, or is switched off by a motion preference, a visitor still gets what Kurx is, why it
 * matters and the primary action, in that order. That is the condition the whole exception is
 * granted on (D-380 §2), not a nicety.
 *
 * What changed is the right-hand column. It used to be a hand-drawn picture of the product's console
 * with three illustrative statistics overlapping it. The picture-of-a-screen was a flat claim and
 * the statistics were numbers nobody could stand behind, so both are gone; `HeroStage` shows the
 * objects the product deals in instead, and carries no figure at all.
 */

const TRUST = [
  ["Secure by design", ShieldCheck],
  ["Real-time sync", CalendarCheck],
  ["Works offline", Smartphone]
] as const;

export function Hero() {
  return (
    <section className="relative overflow-hidden border-b border-border bg-background">
      <Atmosphere grid />

      <div className="container-shell relative grid gap-12 py-16 sm:py-20 lg:grid-cols-[minmax(0,1fr)_minmax(0,1.05fr)] lg:items-center lg:gap-16 lg:py-28">
        <div>
          <p className="inline-flex items-center rounded-pill border border-accent/30 bg-accent/10 px-3 py-1 text-caption font-semibold text-accent-text">
            All-in-one event platform
          </p>

          {/*
            `accent-text`, not `accent`: the fill measures 3.26:1 as text on the dark ground (D-288),
            and a headline is still text.
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

          {/* Capability statements, not endorsements — there is nobody to attribute a quote to. */}
          <ul className="mt-10 flex flex-wrap gap-x-6 gap-y-2">
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

        <HeroStage />
      </div>
    </section>
  );
}
