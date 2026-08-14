"use client";

import { AnimatePresence, motion } from "framer-motion";
import { X } from "lucide-react";
import { ReactNode, useId, useRef } from "react";

import { IconButton } from "./button";
import { useOverlay } from "./use-overlay";

/**
 * A centered modal dialog. Controlled via `open`/`onClose`.
 *
 * Focus trapping, focus restore, Escape and body scroll lock come from
 * `useOverlay` — see that file for what was wrong before (audit S1-2).
 *
 * The accessible name is wired with `aria-labelledby` pointing at the rendered
 * heading rather than `aria-label` duplicating it. Two reasons: the name then
 * cannot drift from what is on screen, and it keeps working when the title is a
 * `ReactNode` rather than a string — which is the S1-3 failure `Sheet` had.
 */
export function Dialog({
  open,
  onClose,
  title,
  description,
  children,
  footer
}: {
  open: boolean;
  onClose: () => void;
  title: ReactNode;
  /** Announced with the title; use for the one-line consequence of a destructive action. */
  description?: string;
  children: ReactNode;
  footer?: ReactNode;
}) {
  const panelRef = useRef<HTMLDivElement>(null);
  const titleId = useId();
  const descId = useId();

  useOverlay({ open, onClose, panelRef });

  return (
    <AnimatePresence>
      {open ? (
        <motion.div
          className="fixed inset-0 z-modal grid place-items-center bg-black/60 p-4"
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
            aria-describedby={description ? descId : undefined}
            tabIndex={-1}
            /*
              Bounded to the viewport, with only the BODY scrolling.
              
              The panel had no vertical bound at all and its body no overflow, so a dialog taller than
              the viewport simply grew past it — and because the overlay centres its child, it
              overflowed off the top and the bottom at once with no way to reach either end. The
              footer is where Cancel and Confirm live, so on a short viewport (a phone in landscape is
              640×360) the confirmation could be neither confirmed nor dismissed except by Escape.
              
              This is the primitive behind every `ConfirmDialog` on web and admin, including the
              thirteen destructive confirmations Phase 21 added.
              
              `dvh` rather than `vh` because mobile browser chrome makes `vh` taller than what is
              actually visible; a browser without `dvh` ignores the declaration and lands back on
              today's behaviour rather than breaking.
            */
            className="flex max-h-[calc(100dvh-2rem)] w-full max-w-md flex-col rounded-xl border border-border bg-surface shadow-lg focus:outline-none"
            initial={{ opacity: 0, scale: 0.96, y: 8 }}
            animate={{ opacity: 1, scale: 1, y: 0 }}
            exit={{ opacity: 0, scale: 0.96, y: 8 }}
            transition={{ duration: 0.18 }}
            onClick={(e) => e.stopPropagation()}
          >
            <div className="flex shrink-0 items-start justify-between gap-md border-b border-border px-5 py-3.5">
              <div className="min-w-0">
                <h2 id={titleId} className="text-h3 text-text">
                  {title}
                </h2>
                {description ? (
                  <p id={descId} className="mt-1 text-caption text-muted">
                    {description}
                  </p>
                ) : null}
              </div>
              <IconButton label="Close" size="sm" onClick={onClose} className="-mr-1.5 -mt-1">
                <X size={18} aria-hidden="true" />
              </IconButton>
            </div>
            <div className="flex-1 overflow-y-auto px-5 py-4 text-body text-muted">{children}</div>
            {footer ? (
              <div className="flex shrink-0 flex-wrap justify-end gap-2 border-t border-border px-5 py-3.5">{footer}</div>
            ) : null}
          </motion.div>
        </motion.div>
      ) : null}
    </AnimatePresence>
  );
}
