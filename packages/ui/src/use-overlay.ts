"use client";

import { RefObject, useEffect, useRef } from "react";

/**
 * The focus and scroll behaviour every modal overlay owes its user.
 *
 * **Closes audit S1-2.** `Dialog` and `Sheet` both asserted `aria-modal="true"`
 * while doing none of this: focus never entered the panel, `Tab` walked straight
 * onto the page behind (which stayed fully operable), focus was never restored on
 * close, and the body kept scrolling. Claiming the background is inert without
 * making it inert is worse than omitting the attribute, because assistive tech
 * believes it.
 *
 * `ConfirmDialog` composes `Dialog`, so every destructive confirmation in web and
 * admin inherited the defect — including admin's approve/reject actions.
 *
 * Deliberately hand-rolled: Kurx has no focus-management dependency, and
 * `.claude/CLAUDE.md` §5 rules out adding one for what is ~60 lines.
 */

const FOCUSABLE = [
  "a[href]",
  "button:not([disabled])",
  "input:not([disabled])",
  "select:not([disabled])",
  "textarea:not([disabled])",
  '[tabindex]:not([tabindex="-1"])'
].join(",");

function focusable(root: HTMLElement): HTMLElement[] {
  return Array.from(root.querySelectorAll<HTMLElement>(FOCUSABLE)).filter(
    (el) => el.offsetParent !== null || el === document.activeElement
  );
}

export function useOverlay({
  open,
  onClose,
  panelRef
}: {
  open: boolean;
  onClose: () => void;
  panelRef: RefObject<HTMLElement>;
}) {
  const restoreTo = useRef<HTMLElement | null>(null);

  useEffect(() => {
    if (!open) return;

    // Remember where focus came from so it can go back. Captured before the
    // panel mounts, because that is the last moment it is still correct.
    restoreTo.current = document.activeElement as HTMLElement | null;

    const panel = panelRef.current;
    if (panel) {
      const first = focusable(panel)[0];
      // The panel itself when it has no focusable content, so focus is never
      // left on <body> behind an open modal.
      (first ?? panel).focus();
    }

    const onKeyDown = (e: KeyboardEvent) => {
      if (e.key === "Escape") {
        e.stopPropagation();
        onClose();
        return;
      }
      if (e.key !== "Tab") return;

      const el = panelRef.current;
      if (!el) return;
      const items = focusable(el);
      if (items.length === 0) {
        // Nothing to move to — keep focus in the panel rather than letting it
        // escape to the inert page behind.
        e.preventDefault();
        return;
      }
      const first = items[0];
      const last = items[items.length - 1];
      const active = document.activeElement;

      if (e.shiftKey && (active === first || active === el)) {
        e.preventDefault();
        last.focus();
      } else if (!e.shiftKey && active === last) {
        e.preventDefault();
        first.focus();
      } else if (!el.contains(active)) {
        // Focus escaped some other way (a click on the page, a programmatic
        // move). Pull it back.
        e.preventDefault();
        first.focus();
      }
    };

    document.addEventListener("keydown", onKeyDown, true);

    // Scroll lock. Padding compensates for the scrollbar so the page does not
    // shift sideways as the overlay opens.
    const { overflow, paddingRight } = document.body.style;
    const gap = window.innerWidth - document.documentElement.clientWidth;
    document.body.style.overflow = "hidden";
    if (gap > 0) document.body.style.paddingRight = `${gap}px`;

    return () => {
      document.removeEventListener("keydown", onKeyDown, true);
      document.body.style.overflow = overflow;
      document.body.style.paddingRight = paddingRight;
      // Only restore if focus is still somewhere inside the closing overlay —
      // if the user has already clicked elsewhere, yanking it back is worse.
      const active = document.activeElement;
      if (restoreTo.current && (!active || active === document.body || panelRef.current?.contains(active))) {
        restoreTo.current.focus?.();
      }
    };
  }, [open, onClose, panelRef]);
}
