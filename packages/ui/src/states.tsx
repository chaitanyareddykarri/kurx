"use client";

import { AlertCircle, AlertTriangle, CheckCircle2, FileQuestion, Info, Lock, RefreshCw, WifiOff } from "lucide-react";
import { ReactNode } from "react";

import { Button } from "./button";

/**
 * Inline messages and whole-region states.
 *
 * The audit found 8 `loading.tsx` for 88 web routes and exactly one `error.tsx`,
 * so a slow query showed a blank frame and a thrown error lost the shell. These
 * are the pieces those routes are built from; `EmptyState`/`ErrorState` already
 * existed and stay.
 *
 * Every state here says **what happened and what the user can do next** — the
 * Phase 44 requirement, brought forward to the component so a screen cannot ship
 * a dead end by omission.
 */

const TONES = {
  info: { Icon: Info, border: "border-border-strong", fg: "text-accent-text", prefix: "" },
  success: { Icon: CheckCircle2, border: "border-success/50", fg: "text-success", prefix: "Success:" },
  warning: { Icon: AlertTriangle, border: "border-warning/50", fg: "text-warning", prefix: "Warning:" },
  danger: { Icon: AlertCircle, border: "border-danger/50", fg: "text-danger", prefix: "Error:" }
} as const;

type Tone = keyof typeof TONES;

/**
 * An inline message tied to the content around it.
 *
 * `role="alert"` only when the message is a consequence of something the user
 * just did — an alert interrupts, and interrupting to announce a static page
 * notice is worse than saying nothing. Static notices are plain regions.
 *
 * The tone prefix is visually hidden rather than drawn, so tone survives for a
 * screen reader without duplicating the icon for everyone else.
 */
export function Alert({
  tone = "info",
  title,
  children,
  live = false,
  action,
  className = ""
}: {
  tone?: Tone;
  title?: string;
  children: ReactNode;
  /** Set when the message appears in response to a user action. */
  live?: boolean;
  action?: ReactNode;
  className?: string;
}) {
  const { Icon, border, fg, prefix } = TONES[tone];
  return (
    <div
      role={live ? "alert" : undefined}
      className={`flex items-start gap-md rounded-lg border bg-elevated p-lg ${border} ${className}`}
    >
      <Icon size={18} aria-hidden="true" className={`mt-0.5 shrink-0 ${fg}`} />
      <div className="flex-1 space-y-1">
        {prefix ? <span className="sr-only">{prefix} </span> : null}
        {title ? <p className="text-label text-text">{title}</p> : null}
        <div className="text-body text-muted">{children}</div>
        {action ? <div className="pt-sm">{action}</div> : null}
      </div>
    </div>
  );
}

/**
 * A full-width notice at the top of a region or page — connectivity, degraded
 * service, an account action required.
 *
 * Distinct from `Alert` by placement and persistence, not by styling: a banner
 * stays until its condition clears, so it is never auto-dismissed and never
 * `role="alert"`.
 */
export function Banner({
  tone = "info",
  children,
  action,
  className = ""
}: {
  tone?: Tone;
  children: ReactNode;
  action?: ReactNode;
  className?: string;
}) {
  const { Icon, fg, prefix } = TONES[tone];
  return (
    <div
      className={`flex flex-wrap items-center gap-md border-b border-border-strong bg-elevated px-lg py-md ${className}`}
    >
      <Icon size={16} aria-hidden="true" className={`shrink-0 ${fg}`} />
      <p className="flex-1 text-body text-text">
        {prefix ? <span className="sr-only">{prefix} </span> : null}
        {children}
      </p>
      {action}
    </div>
  );
}

/** Shared frame for the whole-region states below. */
function StateFrame({
  icon,
  title,
  message,
  children,
  live = false
}: {
  icon: ReactNode;
  title: string;
  message?: string;
  children?: ReactNode;
  live?: boolean;
}) {
  return (
    <div
      role={live ? "alert" : undefined}
      className="flex flex-col items-center justify-center gap-md px-xl py-3xl text-center"
    >
      <div aria-hidden="true">{icon}</div>
      <h3 className="text-h3 text-text">{title}</h3>
      {message ? <p className="max-w-md text-body text-muted">{message}</p> : null}
      {children ? <div className="pt-sm">{children}</div> : null}
    </div>
  );
}

/**
 * The user is signed in but not permitted.
 *
 * Deliberately distinct from NotFound: telling someone "this does not exist" when
 * it does and they simply lack access is a different, more confusing failure —
 * though note the backend intentionally returns 404 for hidden resources (D-018),
 * so this is for the cases where a 403 is actually surfaced.
 */
export function PermissionDeniedState({ message, action }: { message?: string; action?: ReactNode }) {
  return (
    <StateFrame
      icon={<Lock size={36} className="text-muted" />}
      title="You don't have access to this"
      message={message ?? "Ask the event's host or a Kurx admin if you think you should."}
    >
      {action}
    </StateFrame>
  );
}

export function NotFoundState({ message, action }: { message?: string; action?: ReactNode }) {
  return (
    <StateFrame
      icon={<FileQuestion size={36} className="text-muted" />}
      title="We couldn't find that"
      message={message ?? "It may have been removed, or the link may be wrong."}
    >
      {action}
    </StateFrame>
  );
}

/**
 * Offline. Separate from `ErrorState` because the remedy is different — there is
 * nothing to retry until the connection returns, so the copy says so rather than
 * inviting a user to hammer a button.
 */
export function OfflineState({ onRetry }: { onRetry?: () => void }) {
  return (
    <StateFrame
      live
      icon={<WifiOff size={36} className="text-warning" />}
      title="You're offline"
      message="Your tickets and saved events are still available. Everything else will load when you reconnect."
    >
      {onRetry ? (
        <Button variant="secondary" onClick={onRetry}>
          <RefreshCw size={15} aria-hidden="true" /> Try again
        </Button>
      ) : null}
    </StateFrame>
  );
}
