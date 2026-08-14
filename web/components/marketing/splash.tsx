/**
 * The opening sequence on `/` (D-290): grid → glowing ring → mark → tagline → reveal.
 *
 * A server component on purpose. It renders into the first paint, so the marketing page
 * is never briefly visible before the splash covers it, and the whole sequence is CSS
 * (`app/globals.css`) so it clears itself with no script. See that file for why this is
 * not Framer Motion despite Framer being installed.
 *
 * Two properties make it safe to lay a full-viewport element over a live page:
 *
 *   aria-hidden          it is decoration and announces nothing. The marketing content
 *                        underneath is the accessible page from the first byte, so a
 *                        screen reader never waits 2.6s for it.
 *   pointer-events: none it cannot swallow a click or trap focus. Tab order, the skip
 *                        link and every control below behave as if it were not there —
 *                        which also means an impatient user just clicks straight through.
 *
 * Total run is 2600ms; the veil holds until 74% and then fades, so the content is
 * revealed at ~1.9s and fully clear at 2.6s.
 */
export function Splash() {
  return (
    <div
      aria-hidden
      className="splash pointer-events-none fixed inset-0 z-toast grid place-items-center overflow-hidden bg-background"
    >
      <div className="grid-bg absolute inset-0" />

      <div className="relative flex flex-col items-center px-6 text-center">
        {/*
          The ring is sized in `rem` and the mark sits inside it, so the pair scales as one
          object and stays centred from 360px up without a breakpoint.
        */}
        <div className="splash-ring relative grid h-28 w-28 place-items-center sm:h-32 sm:w-32">
          <span
            className="absolute inset-0 rounded-full border border-accent/70"
            style={{ boxShadow: "0 0 24px rgb(var(--color-accent) / 0.35), inset 0 0 18px rgb(var(--color-accent) / 0.15)" }}
          />
          <span
            className="splash-mark text-3xl font-bold tracking-normal text-text sm:text-4xl"
            style={{ textShadow: "0 0 18px rgb(var(--color-accent) / 0.45)" }}
          >
            K
          </span>
        </div>

        <p className="splash-tagline mt-7 text-lg font-medium text-text sm:text-xl">Events. Simplified.</p>
      </div>
    </div>
  );
}
