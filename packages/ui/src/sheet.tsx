"use client";

import { AnimatePresence, motion, useReducedMotion } from "framer-motion";
import { X } from "lucide-react";
import { ReactNode, useId, useRef } from "react";

import { IconButton } from "./button";
import { useOverlay } from "./use-overlay";

/**
 * A slide-in panel (side or bottom) with backdrop + Escape-to-close. `size="xl"`
 * widens the right variant for a data-dense workspace (e.g. the admin event
 * detail view) rather than a narrow aside — still a Sheet (list stays reachable,
 * dismissable), never a full page navigation.
 *
 * **Closes audit S1-3.** The accessible name was
 * `aria-label={typeof title === "string" ? title : undefined}`, so whenever the
 * title was a `ReactNode` — the normal case in the admin workspaces
 * (`event-workspace-sheet`, `org-detail-content`, `taxonomy-detail-sheet`) — the
 * name silently became nothing and a screen reader announced "dialog" and
 * stopped. `aria-labelledby` pointing at the rendered heading works for any node.
 *
 * Focus trap, restore and scroll lock come from `useOverlay` (S1-2).
 *
 * Under `prefers-reduced-motion` the panel fades instead of sliding: clamping the
 * duration is not enough for a large positional move, which is the distinction
 * `docs/ui-ux/accessibility-foundation.md` §5 draws.
 */
export function Sheet({
  open,
  onClose,
  title,
  side = "right",
  size = "sm",
  actions,
  children
}: {
  open: boolean;
  onClose: () => void;
  title: ReactNode;
  side?: "right" | "bottom";
  size?: "sm" | "xl";
  actions?: ReactNode;
  children: ReactNode;
}) {
  const panelRef = useRef<HTMLDivElement>(null);
  const titleId = useId();
  const reduced = useReducedMotion();

  useOverlay({ open, onClose, panelRef });

  const isSide = side === "right";
  const hidden = reduced ? { opacity: 0 } : isSide ? { x: "100%" } : { y: "100%" };
  const shown = reduced ? { opacity: 1 } : isSide ? { x: 0 } : { y: 0 };
  const widthClass = size === "xl" ? "max-w-4xl" : "max-w-sm";
  const position = isSide
    ? `right-0 top-0 h-full w-full ${widthClass} border-l`
    : "bottom-0 left-0 w-full max-h-[80vh] rounded-t-xl border-t";

  return (
    <AnimatePresence>
      {open ? (
        <motion.div
          className="fixed inset-0 z-modal bg-black/60"
          initial={{ opacity: 0 }}
          animate={{ opacity: 1 }}
          exit={{ opacity: 0 }}
          onClick={onClose}
        >
          <motion.div
            ref={panelRef}
            role="dialog"
            aria-modal="true"
            aria-labelledby={titleId}
            tabIndex={-1}
            className={`absolute flex flex-col ${position} border-border bg-surface shadow-lg focus:outline-none`}
            initial={hidden}
            animate={shown}
            exit={hidden}
            transition={reduced ? { duration: 0.08 } : { type: "spring", stiffness: 320, damping: 34 }}
            onClick={(e) => e.stopPropagation()}
          >
            <div className="flex shrink-0 items-center justify-between gap-4 border-b border-border p-5">
              <div id={titleId} className="min-w-0 text-h3 text-text">
                {title}
              </div>
              <div className="flex shrink-0 items-center gap-2">
                {actions}
                <IconButton label="Close" size="sm" onClick={onClose}>
                  <X size={18} aria-hidden="true" />
                </IconButton>
              </div>
            </div>
            <div className="flex-1 overflow-y-auto p-5 text-body">{children}</div>
          </motion.div>
        </motion.div>
      ) : null}
    </AnimatePresence>
  );
}
