"use client";

import { BadgeCheck, CalendarDays, MapPin } from "lucide-react";

import { DEPTH, plane } from "./depth";
import { Float, Tilt } from "./spatial";

/**
 * The hero's spatial composition (D-380) — a small event ecosystem seen from slightly off-axis.
 *
 * It replaces the hand-drawn "live event console" the hero used to show. That was a picture of a
 * screen, and a picture of a screen is a flat claim; this is the objects the product actually deals
 * in — events, when and where they are, and an attestation attached to one — arranged in a space.
 *
 * **The whole scene is `aria-hidden` and every string in it is illustrative.** The text column beside
 * it is the accessible, crawlable hero, and it says everything this depicts. The three event names
 * are the ones the previous hero already shipped, reused verbatim rather than invented: no new claim
 * enters the page, and there is no number anywhere in here to be mistaken for a metric.
 *
 * Depth comes from `translateZ` on these children under one rotating parent, so the parallax between
 * near and far objects is perspective doing its job rather than five elements animated to agree.
 * Below `lg` nothing sets `perspective`, so the same markup lays down flat — see `depth.ts`.
 *
 * **The card art is token-tinted, not `EventPlaceholder`.** The shared placeholder's gradients are
 * the warm ember/amber/wine set inherited from the pre-D-288 palette, and three of its five entries
 * render as orange on a blue-on-black page — in the hero, next to the headline, that reads as a
 * different product. Real event cards keep using it (the Featured section below is full of them,
 * and mobile draws the identical art); these objects are illustrations of an event rather than
 * events, so they are built from `--color-accent` and `--color-teal` like everything else here.
 */

/**
 * Positions are percentages of the stage box and are duplicated in the connector geometry below,
 * which is the one place in this file where two numbers have to agree. They are kept adjacent for
 * that reason: moving a card without moving its node leaves a line pointing at nothing, which is
 * exactly how the first version of this scene looked.
 *
 * The card widths also have to leave the connectors somewhere to be seen. At 60%/46% the primary and
 * the workshop overlapped, and the line between them was drawn perfectly and then covered by the two
 * cards it joined — visible depth, invisible relationship.
 */
const EVENTS = [
  {
    title: "Annual Tech Conference",
    when: "Fri, 12 Sep · 9:30 AM",
    where: "Bengaluru",
    art: "linear-gradient(135deg, rgb(var(--color-accent) / 0.75), rgb(var(--color-accent) / 0.25) 55%, rgb(var(--color-teal) / 0.30))"
  },
  {
    title: "Design Systems Workshop",
    when: "Sat, 20 Sep",
    where: "Online",
    art: "linear-gradient(135deg, rgb(var(--color-teal) / 0.65), rgb(var(--color-accent) / 0.30))"
  },
  {
    title: "Summer Music Festival",
    when: "Sun, 5 Oct",
    where: "Goa",
    art: "linear-gradient(135deg, rgb(var(--color-accent) / 0.55), rgb(var(--color-accent) / 0.15))"
  }
] as const;

function StageCard({
  title,
  when,
  where,
  art,
  compact = false
}: {
  title: string;
  when: string;
  where: string;
  art: string;
  compact?: boolean;
}) {
  return (
    <div className="overflow-hidden rounded-lg border border-border bg-surface shadow-lg">
      <div className={compact ? "h-11" : "h-20"} style={{ background: art }} />
      <div className={compact ? "p-3" : "p-4"}>
        <p className="truncate text-label text-text">{title}</p>
        <p className="mt-1.5 flex items-center gap-1.5 text-micro text-muted">
          <CalendarDays size={12} aria-hidden /> {when}
        </p>
        {compact ? null : (
          <p className="mt-1 flex items-center gap-1.5 text-micro text-muted">
            <MapPin size={12} aria-hidden /> {where}
          </p>
        )}
      </div>
    </div>
  );
}

export function HeroStage() {
  const [primary, workshop, festival] = EVENTS;

  return (
    <div
      aria-hidden
      // `perspective` only from `lg`. It is the single switch between the spatial composition and the
      // flat one — no duplicated markup, no `lg:` variant on a transform, nothing to keep in sync.
      className="relative mx-auto w-full max-w-[420px] sm:max-w-[480px] lg:max-w-none lg:[perspective:1500px]"
    >
      {/*
        Two compositions, one DOM. Below `lg` the objects are grid items in normal flow — primary card
        across the top, the two satellites side by side under it, the attestation centred beneath — and
        the box takes its height from them. From `lg` every child switches to `lg:absolute` and the box
        takes a fixed aspect, which is what turns the same four objects into the spatial arrangement.

        This is not the desktop scene scaled down. Absolutely positioning four objects by percentage
        inside a 374px-wide box put the attestation chip straight through the primary card's date and
        venue lines: legible at 1440, unreadable on a phone. Flow layout is what a small screen is
        good at, so on a small screen the scene uses it.
      */}
      <Tilt max={7} className="relative grid w-full grid-cols-2 gap-3 sm:gap-4 lg:block lg:aspect-[4/3]">
        {/* Layer 1 — the pool of light the objects sit in, wider than the cluster so it haloes past it. */}
        <div
          className="absolute left-1/2 top-1/2 hidden h-[95%] w-[105%] rounded-full lg:block"
          style={{
            ...plane(DEPTH.ambient, "translate(-50%, -50%)"),
            background: "radial-gradient(closest-side, rgb(var(--color-accent) / 0.22), rgb(var(--color-accent) / 0.06), transparent)"
          }}
        />

        {/*
          Layer 1 — the relationships, drawn from the primary card's centre to each of the other three
          objects. It sits just behind the base plane rather than back at `ambient`: perspective scales
          a far plane down, and a connector that has to *land* on a card must share the card's depth or
          it points somewhere the card is not.

          `vectorEffect` keeps the hairline one pixel wide at every scale the perspective applies, and
          `preserveAspectRatio="none"` is what lets the percentage coordinates track the box.
        */}
        <svg
          className="absolute inset-0 hidden h-full w-full lg:block"
          viewBox="0 0 100 100"
          preserveAspectRatio="none"
          style={plane(DEPTH.base - 10)}
        >
          <g
            stroke="rgb(var(--color-accent) / 0.45)"
            strokeWidth="1"
            fill="none"
            vectorEffect="non-scaling-stroke"
          >
            <path d="M28 38 L79 11" />
            <path d="M28 38 L70 85" />
            <path d="M28 38 L18 71" />
          </g>
        </svg>

        {/* Layer 3 — the primary object, on the base plane and the only one at full detail. */}
        <div className="col-span-2 lg:absolute lg:left-0 lg:top-[20%] lg:w-[56%]" style={plane(DEPTH.base)}>
          <StageCard {...primary} />
        </div>

        {/* Layer 4 — satellites. Further forward, so they sweep further as the scene turns. */}
        <Float delay={0.8} className="lg:absolute lg:right-0 lg:top-0 lg:w-[42%]" style={plane(DEPTH.float)}>
          <StageCard {...workshop} compact />
        </Float>

        <Float delay={2.1} distance={5} className="lg:absolute lg:bottom-[4%] lg:right-[8%] lg:w-[44%]" style={plane(DEPTH.raised)}>
          <StageCard {...festival} compact />
        </Float>

        {/*
          Layer 5 — the attestation, furthest forward and the only teal object in the scene.

          P2 is the reason it is teal and the reason there is exactly one: teal attests, and a second
          teal object here would turn a signal into a colour scheme.
        */}
        <Float delay={1.4} distance={9} className="col-span-2 justify-self-center lg:absolute lg:bottom-[26%] lg:left-[6%] lg:justify-self-auto" style={plane(DEPTH.front)}>
          <span className="inline-flex items-center gap-2 rounded-pill border border-teal/40 bg-surface px-3 py-2 text-micro text-text shadow-lg">
            <BadgeCheck size={14} className="text-teal" aria-hidden />
            Attendance verified
          </span>
        </Float>
      </Tilt>
    </div>
  );
}
