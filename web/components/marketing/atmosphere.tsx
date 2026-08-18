/**
 * Layer 1 of the depth system (D-380): the light a section sits in.
 *
 * A server component and pure CSS, so it is in the first paint and costs no JavaScript. Both pools
 * are tinted from `--color-accent` / `--color-teal`, which is what keeps one DOM working in both
 * themes: on the near-black ground they read as light falling across the page, and on the light
 * theme the same values are a faint blue wash rather than a grey haze.
 *
 * **One element, two gradients in a single `background-image`.** The first version painted each pool
 * as its own absolutely-positioned div sized at 150% of the section, which only worked because the
 * section clipped the overflow — six sections of that is eighteen oversized layers, each needing a
 * clip, to draw two soft shapes. `radial-gradient(… at x% y%)` places the same pools inside the
 * element's own box: nothing overflows, nothing needs cropping, and the sections no longer carry an
 * `overflow: hidden` that exists only to hide the atmosphere. The hero keeps its clip because its
 * stage objects genuinely sit outside the content box.
 *
 * `tone` follows P2 — blue where a section is about acting, teal where it is about attestation. It is
 * not a decorative choice, which is why there are exactly two.
 *
 * Always `aria-hidden`, always `pointer-events-none`: this is atmosphere, and nothing in it is
 * information or a target.
 */
export function Atmosphere({
  tone = "accent",
  grid = false,
  className = ""
}: {
  tone?: "accent" | "teal";
  grid?: boolean;
  className?: string;
}) {
  const hue = tone === "teal" ? "var(--color-teal)" : "var(--color-accent)";

  return (
    <div aria-hidden className={`pointer-events-none absolute inset-0 ${className}`}>
      {grid ? <div className="grid-bg absolute inset-0 opacity-60" /> : null}

      <div
        className="absolute inset-0"
        style={{
          backgroundImage: [
            `radial-gradient(65% 95% at 10% 0%, rgb(${hue} / 0.20), transparent 72%)`,
            `radial-gradient(55% 85% at 92% 100%, rgb(${hue} / 0.14), transparent 72%)`
          ].join(", ")
        }}
      />

      {/*
        A single hairline along the top edge. Sections still meet at a border, but the border now
        catches light at the centre and fades at both ends, which is what stops seven stacked bands
        from reading as seven boxes.
      */}
      <div
        className="absolute inset-x-0 top-0 h-px"
        style={{ background: `linear-gradient(90deg, transparent, rgb(${hue} / 0.5), transparent)` }}
      />
    </div>
  );
}
