"use client";

import { MotionConfig, motion, useMotionValue, useSpring, useTransform } from "framer-motion";
import { useEffect, useState, type CSSProperties, type ReactNode } from "react";

/**
 * The spatial toolkit for the marketing page (D-380).
 *
 * Everything here is CSS 3D — `perspective` + `preserve-3d` — driven by the framer-motion that was
 * already in the dependency list. There is no WebGL and no renderer: the objects in these scenes are
 * real DOM, so they cost ~4KB of app code instead of ~160KB of engine, they paint with the first
 * HTML rather than after a canvas boots, and there is no fallback path that can rot because the
 * fallback IS the markup.
 *
 * D-380 confines all of this to `components/marketing/**`. Visual-identity P1 (opaque, no glow) and
 * P6 (motion never performs) still govern every signed-in surface, so nothing here may be lifted
 * into the product app, the host workspace, admin or Flutter without a further decision.
 */

/**
 * Whether this visitor gets pointer tilt (D-380 §6).
 *
 * One media query answers both halves: a fine pointer (so touch devices never inherit a hover-only
 * interaction) and no reduced-motion preference. The device check is separate because it cannot be
 * expressed as a query — `deviceMemory` is Chromium-only, so its absence means "don't downgrade"
 * rather than "downgrade".
 *
 * Starts `false` and turns on after mount. That direction matters: tilt at rest is zero rotation,
 * which is exactly what the server rendered, so enabling it changes nothing on screen. Starting
 * `true` would have to be undone on a touch device, and that IS a visible flash.
 */
function useTilt(): boolean {
  const [on, setOn] = useState(false);

  useEffect(() => {
    const nav = navigator as Navigator & { deviceMemory?: number };
    if ((nav.hardwareConcurrency ?? 8) < 4 || (nav.deviceMemory ?? 8) < 4) return;

    const q = window.matchMedia("(pointer: fine) and (prefers-reduced-motion: no-preference)");
    const sync = () => setOn(q.matches);
    sync();
    q.addEventListener("change", sync);
    return () => q.removeEventListener("change", sync);
  }, []);

  return on;
}

const SPRING = { stiffness: 120, damping: 18, mass: 0.6 } as const;

/**
 * Rotates its subtree toward the pointer, so the whole scene parallaxes at once.
 *
 * The rotation lives on the parent and the children carry `translateZ`, which is what makes this
 * real parallax rather than N independently-animated elements: objects further forward sweep further
 * across the frame because that is what perspective does to them, for free, at any depth.
 *
 * The pointer never reaches React state. `clientX/Y` feeds motion values, springs read them and
 * framer writes the transform on its own frame loop, so moving the mouse across the hero costs zero
 * renders. A `useState` here would re-render the entire scene at pointer rate.
 *
 * `max` is deliberately single-digit degrees. Past about 10 the cards start to shear and the text
 * inside them stops being comfortable to read, which is the line between depth and a gimmick.
 */
export function Tilt({
  children,
  max = 7,
  className = "",
  style
}: {
  children: ReactNode;
  max?: number;
  className?: string;
  style?: CSSProperties;
}) {
  const on = useTilt();
  const px = useMotionValue(0);
  const py = useMotionValue(0);

  // Near side lifts toward the pointer: cursor at the top tips the top edge forward, which is what
  // makes the object read as facing the viewer rather than being pressed away from them.
  const rotateX = useSpring(useTransform(py, [-0.5, 0.5], [-max, max]), SPRING);
  const rotateY = useSpring(useTransform(px, [-0.5, 0.5], [max, -max]), SPRING);

  return (
    <motion.div
      className={className}
      style={{ ...style, rotateX, rotateY, transformStyle: "preserve-3d" }}
      onPointerMove={
        on
          ? (e) => {
              const r = e.currentTarget.getBoundingClientRect();
              px.set((e.clientX - r.left) / r.width - 0.5);
              py.set((e.clientY - r.top) / r.height - 0.5);
            }
          : undefined
      }
      onPointerLeave={
        on
          ? () => {
              px.set(0);
              py.set(0);
            }
          : undefined
      }
    >
      {children}
    </motion.div>
  );
}

/**
 * Idle drift — the difference between objects suspended in a space and objects pasted onto one.
 *
 * Long durations and prime-ish `delay` offsets keep the objects from ever syncing up into a visible
 * pulse. Reduced motion is not handled here: `SpatialRoot` puts framer in `reducedMotion="user"`,
 * which drops transform animations at the library level, so this component needs no branch and
 * cannot forget one.
 */
export function Float({
  children,
  distance = 7,
  duration = 9,
  delay = 0,
  className = "",
  style
}: {
  children: ReactNode;
  distance?: number;
  duration?: number;
  delay?: number;
  className?: string;
  style?: CSSProperties;
}) {
  return (
    <motion.div
      className={className}
      style={style}
      animate={{ y: [0, -distance, 0] }}
      transition={{ duration, delay, repeat: Infinity, ease: "easeInOut" }}
    >
      {children}
    </motion.div>
  );
}

/**
 * Wraps the page in framer's own reduced-motion mode (D-380 §4).
 *
 * `reducedMotion="user"` makes every framer component below it drop transform and layout animation
 * while keeping opacity — so the entrance reveals still read as arrivals, but nothing moves. Doing
 * it once at the root also covers `MotionPanel`, which is shared product code this page reuses and
 * which checks nothing itself; fixing it here rather than editing it keeps the change inside the
 * boundary D-380 drew.
 *
 * Children are server-rendered and passed through, so this adds a provider, not a client boundary
 * around the page's content.
 */
export function SpatialRoot({ children }: { children: ReactNode }) {
  return <MotionConfig reducedMotion="user">{children}</MotionConfig>;
}

/**
 * Gives an existing card physical presence without knowing anything about it.
 *
 * It wraps rather than replaces, so `EventCard` — and through it `LinkCard`, the status badge, the
 * media frame and the seeded placeholder art — is the same shared component discovery and the host
 * workspace render. The spatial page gains depth; the design system gains nothing to keep in sync.
 *
 * `LinkCard` covers its whole surface with an absolutely-positioned anchor, and an ancestor
 * transform moves hit-testing with the pixels, so a tilted card is clicked exactly where it appears.
 * The tilt is capped low for the same reason a text-bearing object always is: past a few degrees the
 * title starts to shear, and a card nobody wants to read is a worse card.
 */
export function SpatialCard({ children, className = "" }: { children: ReactNode; className?: string }) {
  return (
    <div
      className={`rounded-lg transition-[transform,box-shadow] duration-base ease-kurx hover:-translate-y-1.5 hover:shadow-lg [perspective:1100px] ${className}`}
    >
      <Tilt max={5} className="h-full">
        {children}
      </Tilt>
    </div>
  );
}
