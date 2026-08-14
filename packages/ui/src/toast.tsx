"use client";

import { AnimatePresence, motion } from "framer-motion";
import { AlertCircle, AlertTriangle, CheckCircle2, Info, X } from "lucide-react";
import { createContext, ReactNode, useCallback, useContext, useMemo, useRef, useState } from "react";

type Tone = "success" | "error" | "warning" | "info";
type Toast = { id: number; message: string; tone: Tone };

const ToastContext = createContext<(message: string, tone?: Tone) => void>(() => {});

const TONES: Record<Tone, { Icon: typeof Info; className: string; prefix: string }> = {
  // The visually-hidden prefix is what makes the tone perceivable without colour
  // — an icon plus a hue is two channels that a screen reader reads as neither.
  success: { Icon: CheckCircle2, className: "text-success", prefix: "Success:" },
  error: { Icon: AlertCircle, className: "text-danger", prefix: "Error:" },
  warning: { Icon: AlertTriangle, className: "text-warning", prefix: "Warning:" },
  info: { Icon: Info, className: "text-accent-text", prefix: "" }
};

/**
 * Wrap a subtree to enable `useToast()`. Toasts stack bottom-right.
 *
 * **Closes audit S1-4.** This had no `aria-live` region and no `role="status"`,
 * so the repo-mandated feedback channel for row-action outcomes
 * (`frontend-conventions.md`) announced nothing at all — an admin bulk action
 * reported its result to sighted users only. It also auto-dismissed after a fixed
 * 3200 ms with no dismiss control and no pause, failing WCAG 2.2.1 (Timing
 * Adjustable), which is a short window for a long error message.
 *
 * Now: a persistent polite live region (present in the DOM *before* any message
 * arrives, or it is not announced), a manual dismiss on every toast, errors that
 * do not auto-dismiss at all, and a timer that pauses while the pointer is over
 * the stack.
 */
export function ToastProvider({ children }: { children: ReactNode }) {
  const [toasts, setToasts] = useState<Toast[]>([]);
  const timers = useRef(new Map<number, ReturnType<typeof setTimeout>>());
  const nextId = useRef(0);

  const dismiss = useCallback((id: number) => {
    const t = timers.current.get(id);
    if (t) clearTimeout(t);
    timers.current.delete(id);
    setToasts((list) => list.filter((x) => x.id !== id));
  }, []);

  const schedule = useCallback(
    (id: number, tone: Tone) => {
      // Errors persist: they are the ones a user most needs time to read, and
      // often the only record of what failed.
      if (tone === "error") return;
      timers.current.set(
        id,
        setTimeout(() => dismiss(id), 6000)
      );
    },
    [dismiss]
  );

  const push = useCallback(
    (message: string, tone: Tone = "info") => {
      // A monotonic counter, not Date.now() + Math.random(): two toasts pushed in
      // the same tick could previously collide on key and drop one.
      const id = nextId.current++;
      setToasts((list) => [...list, { id, message, tone }]);
      schedule(id, tone);
    },
    [schedule]
  );

  const pause = useCallback(() => {
    timers.current.forEach((t) => clearTimeout(t));
    timers.current.clear();
  }, []);

  const resume = useCallback(() => {
    setToasts((list) => {
      list.forEach((t) => {
        if (!timers.current.has(t.id)) schedule(t.id, t.tone);
      });
      return list;
    });
  }, [schedule]);

  const value = useMemo(() => push, [push]);

  return (
    <ToastContext.Provider value={value}>
      {children}
      {/*
        The live region is rendered unconditionally. A region inserted at the same
        moment as its first message is not announced by most screen readers — it
        has to already exist for the mutation to be observed.
      */}
      <div
        role="status"
        aria-live="polite"
        aria-atomic="false"
        onMouseEnter={pause}
        onMouseLeave={resume}
        className="pointer-events-none fixed bottom-4 right-4 z-toast flex w-80 max-w-[calc(100vw-2rem)] flex-col gap-2"
      >
        <AnimatePresence>
          {toasts.map((t) => {
            const { Icon, className, prefix } = TONES[t.tone];
            return (
              <motion.div
                key={t.id}
                layout
                initial={{ opacity: 0, y: 16, scale: 0.96 }}
                animate={{ opacity: 1, y: 0, scale: 1 }}
                exit={{ opacity: 0, x: 24 }}
                className="pointer-events-auto flex items-start gap-2.5 rounded-md border border-border-strong bg-elevated px-3.5 py-3 text-body text-text shadow-md"
              >
                <Icon size={18} aria-hidden="true" className={`mt-0.5 shrink-0 ${className}`} />
                <span className="flex-1">
                  {prefix ? <span className="sr-only">{prefix} </span> : null}
                  {t.message}
                </span>
                <button
                  type="button"
                  aria-label="Dismiss"
                  onClick={() => dismiss(t.id)}
                  className="-m-1.5 shrink-0 rounded-sm p-1.5 text-muted transition duration-fast hover:text-text focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
                >
                  <X size={15} aria-hidden="true" />
                </button>
              </motion.div>
            );
          })}
        </AnimatePresence>
      </div>
    </ToastContext.Provider>
  );
}

export function useToast() {
  return useContext(ToastContext);
}
