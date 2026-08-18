"use client";

import { Award, Music2, Presentation, Trophy, Wrench } from "lucide-react";

import { Atmosphere } from "./atmosphere";
import { DEPTH, plane } from "./depth";
import { Tilt } from "./spatial";

/**
 * Scene 2 — the kinds of event the product models, as objects rather than icons in a row.
 *
 * The content is unchanged and the reason for it is unchanged: Option 4's original brief put a
 * "Trusted by event organizers worldwide" logo row here — TechFest, DevCon, EduSum, DesignWeek —
 * which are mockup placeholders, and shipping them would state that four named organizations use
 * Kurx. Invented social proof is the one thing on this page that would be a lie rather than a style
 * choice, so the band carries the real taxonomy instead (`event_visuals.dart`, taxonomy seed).
 * Swap it for customer logos the moment there are customers to name.
 *
 * What changed is that each one is now a physical tile: the icon sits on its own plane above the
 * card face, so turning the tile toward the pointer moves the icon further than the label under it.
 * That separation is the entire effect — it is why five flat boxes now read as five objects.
 *
 * These are display, not navigation. `/discover` and `/categories/[id]` are behind auth, so linking
 * them from a signed-out page would be five CTAs into a login wall.
 */
const CATEGORIES = [
  ["Music", Music2],
  ["Tech", Presentation],
  ["Workshops", Wrench],
  ["Sports", Trophy],
  ["Conferences", Award]
] as const;

export function CapabilityStrip() {
  return (
    <section className="relative border-b border-border bg-surface">
      <Atmosphere />
      <div className="container-shell relative py-14 sm:py-16">
        <p className="text-center text-caption text-muted">Built for every kind of event</p>

        <ul className="mt-8 grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-5 lg:gap-4">
          {CATEGORIES.map(([label, Icon]) => (
            <li
              key={label}
              // Perspective per tile, not per row: each object is seen head-on from its own position,
              // which is how a shelf of objects looks. One shared vanishing point across the row
              // would skew the outer two as if the whole strip were a single turned panel.
              //
              // The lift lives here and the rotation lives on `Tilt` inside, because framer owns that
              // element's transform outright — a Tailwind translate on the same node would be
              // overwritten on the next frame.
              className="transition-transform duration-base ease-kurx hover:-translate-y-1 [perspective:900px]"
            >
              <Tilt
                max={12}
                className="flex h-full flex-col items-center gap-2 rounded-lg border border-border bg-elevated px-4 pb-4 pt-5 shadow-sm transition-[border-color,box-shadow] duration-base ease-kurx hover:border-accent/40 hover:shadow-lg"
              >
                {/*
                  The plinth and the shadow it casts, as two elements on two planes. The blurred
                  ellipse stays on the card face while the plinth stands 30px in front of it, so
                  turning the tile slides the object across its own shadow — which is the cue that
                  says "standing on" rather than "printed on", and it costs one div.
                */}
                <span className="relative grid place-items-center">
                  <span
                    className="absolute h-3 w-10 rounded-full bg-black/50 blur-md"
                    style={plane(DEPTH.base, "translateY(18px)")}
                  />
                  <span
                    className="relative grid h-14 w-14 place-items-center rounded-lg border border-border text-accent-text shadow-lg"
                    style={{
                      ...plane(DEPTH.raised),
                      // A lit face: brighter at the top-left corner the page's light comes from.
                      background:
                        "linear-gradient(145deg, rgb(var(--color-accent) / 0.22), rgb(var(--color-surface)) 60%)"
                    }}
                  >
                    <Icon size={22} aria-hidden />
                  </span>
                </span>

                <span className="mt-3 text-label text-text" style={plane(DEPTH.base)}>
                  {label}
                </span>
              </Tilt>
            </li>
          ))}
        </ul>
      </div>
    </section>
  );
}
