"use client";

import { AnimatePresence, motion } from "framer-motion";
import { ReactNode, useCallback, useEffect, useId, useRef, useState } from "react";

/**
 * Non-modal overlays: Popover and Menu.
 *
 * Distinct from `Dialog`/`Sheet` on purpose. Those are modal — they trap focus
 * and make the page inert. These are transient: focus moves in, `Escape` returns
 * it to the trigger, and an outside click dismisses. Trapping focus in a dropdown
 * is a common and irritating bug, so `useOverlay` is deliberately not used here.
 *
 * See `docs/ui-ux/accessibility-foundation.md` §2.4 and §6.
 */

/**
 * Shared dismiss-on-outside-click + Escape wiring for a non-modal overlay.
 *
 * The two reasons are separate handlers on purpose. This listens in the capture
 * phase (so a nested overlay closes before its parent sees the key), which means
 * it runs *before* any React `onKeyDown` and stops propagation — so a component
 * that wanted to do something extra on Escape, like restoring focus to its
 * trigger, would never get the chance. `onEscape` is that hook.
 */
function useDismiss(
  open: boolean,
  rootRef: React.RefObject<HTMLElement>,
  { onEscape, onOutside }: { onEscape: () => void; onOutside: () => void }
) {
  useEffect(() => {
    if (!open) return;
    const onPointer = (e: PointerEvent) => {
      if (!rootRef.current?.contains(e.target as Node)) onOutside();
    };
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") {
        e.stopPropagation();
        onEscape();
      }
    };
    document.addEventListener("pointerdown", onPointer);
    document.addEventListener("keydown", onKey, true);
    return () => {
      document.removeEventListener("pointerdown", onPointer);
      document.removeEventListener("keydown", onKey, true);
    };
  }, [open, onEscape, onOutside, rootRef]);
}

const panel =
  "absolute z-overlay mt-1 min-w-[12rem] rounded-lg border border-border-strong bg-surface p-1 shadow-lg";

/**
 * A popover anchored to its trigger.
 *
 * `trigger` receives the props it must carry — this component owns
 * `aria-expanded` and `aria-haspopup` rather than trusting each call site to
 * remember them.
 */
export function Popover({
  trigger,
  children,
  align = "start",
  className = ""
}: {
  trigger: (props: { "aria-expanded": boolean; "aria-haspopup": "dialog"; onClick: () => void }) => ReactNode;
  children: ReactNode;
  align?: "start" | "end";
  className?: string;
}) {
  const [open, setOpen] = useState(false);
  const rootRef = useRef<HTMLDivElement>(null);
  const close = useCallback(() => setOpen(false), []);
  useDismiss(open, rootRef, { onEscape: close, onOutside: close });

  return (
    <div ref={rootRef} className={`relative inline-block ${className}`}>
      {trigger({ "aria-expanded": open, "aria-haspopup": "dialog", onClick: () => setOpen((o) => !o) })}
      <AnimatePresence>
        {open ? (
          <motion.div
            initial={{ opacity: 0, y: -4 }}
            animate={{ opacity: 1, y: 0 }}
            exit={{ opacity: 0, y: -4 }}
            transition={{ duration: 0.12 }}
            className={`${panel} ${align === "end" ? "right-0" : "left-0"} p-lg`}
          >
            {children}
          </motion.div>
        ) : null}
      </AnimatePresence>
    </div>
  );
}

export type MenuItem = {
  label: string;
  onSelect: () => void;
  icon?: ReactNode;
  /** Renders in the danger tone and is announced as destructive. */
  destructive?: boolean;
  disabled?: boolean;
};

/**
 * A dropdown menu — the account menu, row actions, context actions.
 *
 * Arrow keys move, Home/End jump, Escape closes and restores focus to the
 * trigger, and typing a letter jumps to the next item starting with it. The
 * menu is one tab stop: `Tab` leaves it rather than walking every item, which is
 * what `role="menu"` promises.
 */
export function Menu({
  trigger,
  items,
  align = "end",
  className = ""
}: {
  trigger: (props: { "aria-expanded": boolean; "aria-haspopup": "menu"; onClick: () => void }) => ReactNode;
  items: MenuItem[];
  align?: "start" | "end";
  className?: string;
}) {
  const [open, setOpen] = useState(false);
  const [active, setActive] = useState(0);
  const rootRef = useRef<HTMLDivElement>(null);
  const itemRefs = useRef<Array<HTMLButtonElement | null>>([]);
  const triggerRef = useRef<HTMLElement | null>(null);
  const id = useId();

  const close = useCallback(
    (restoreFocus = false) => {
      setOpen(false);
      if (restoreFocus) triggerRef.current?.focus();
    },
    []
  );

  // Escape returns focus to the trigger; an outside click does not, because the
  // user has already moved their attention somewhere else.
  const closeAndRestore = useCallback(() => close(true), [close]);
  const closeQuietly = useCallback(() => close(false), [close]);
  useDismiss(open, rootRef, { onEscape: closeAndRestore, onOutside: closeQuietly });

  const enabled = items.map((it, i) => (it.disabled ? -1 : i)).filter((i) => i >= 0);

  useEffect(() => {
    if (open) itemRefs.current[active]?.focus();
  }, [open, active]);

  function onKeyDown(e: React.KeyboardEvent) {
    if (!open) return;
    const pos = enabled.indexOf(active);
    if (e.key === "ArrowDown") {
      e.preventDefault();
      setActive(enabled[(pos + 1) % enabled.length] ?? enabled[0]);
    } else if (e.key === "ArrowUp") {
      e.preventDefault();
      setActive(enabled[(pos - 1 + enabled.length) % enabled.length] ?? enabled[0]);
    } else if (e.key === "Home") {
      e.preventDefault();
      setActive(enabled[0]);
    } else if (e.key === "End") {
      e.preventDefault();
      setActive(enabled[enabled.length - 1]);
    } else if (e.key.length === 1 && /\S/.test(e.key)) {
      const match = enabled.find((i) => items[i].label.toLowerCase().startsWith(e.key.toLowerCase()));
      if (match !== undefined) setActive(match);
    }
  }

  return (
    <div ref={rootRef} className={`relative inline-block ${className}`} onKeyDown={onKeyDown}>
      <span
        ref={(el) => {
          triggerRef.current = el?.firstElementChild as HTMLElement | null;
        }}
      >
        {trigger({
          "aria-expanded": open,
          "aria-haspopup": "menu",
          onClick: () => {
            setActive(enabled[0] ?? 0);
            setOpen((o) => !o);
          }
        })}
      </span>
      <AnimatePresence>
        {open ? (
          <motion.div
            role="menu"
            id={id}
            initial={{ opacity: 0, y: -4 }}
            animate={{ opacity: 1, y: 0 }}
            exit={{ opacity: 0, y: -4 }}
            transition={{ duration: 0.12 }}
            className={`${panel} ${align === "end" ? "right-0" : "left-0"}`}
          >
            {items.map((item, i) => (
              <button
                key={item.label}
                ref={(el) => {
                  itemRefs.current[i] = el;
                }}
                type="button"
                role="menuitem"
                // One tab stop for the whole menu — Tab leaves, arrows move.
                tabIndex={i === active ? 0 : -1}
                disabled={item.disabled}
                onClick={() => {
                  item.onSelect();
                  close(true);
                }}
                className={`flex w-full min-h-11 items-center gap-sm rounded-md px-3 text-left text-body transition duration-fast
                  disabled:cursor-not-allowed disabled:opacity-50
                  focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-[-2px] focus-visible:outline-accent
                  ${item.destructive ? "text-danger hover:bg-danger/10" : "text-text hover:bg-elevated"}`}
              >
                {item.icon}
                <span className="flex-1">{item.label}</span>
                {/* Destructiveness carried in text, not only in colour. */}
                {item.destructive ? <span className="sr-only">(destructive)</span> : null}
              </button>
            ))}
          </motion.div>
        ) : null}
      </AnimatePresence>
    </div>
  );
}
