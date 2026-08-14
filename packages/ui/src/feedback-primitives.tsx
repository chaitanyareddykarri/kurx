"use client";

import { ReactNode, useId, useState } from "react";

/**
 * Divider, Tooltip and Progress — none of which existed in the design system.
 * See `docs/ui-ux/accessibility-foundation.md` §3 and §6.
 */

/**
 * A rule between sections.
 *
 * Decorative by default and therefore `role="presentation"`: a screen reader
 * announcing "separator" between every card is noise. Pass `semantic` when the
 * rule genuinely divides two regions a user needs to know are distinct.
 */
export function Divider({
  className = "",
  orientation = "horizontal",
  semantic = false,
  label
}: {
  className?: string;
  orientation?: "horizontal" | "vertical";
  semantic?: boolean;
  /** Renders a centred caption in the rule (e.g. "or"). Implies `semantic`. */
  label?: string;
}) {
  const line = orientation === "horizontal" ? "h-px w-full" : "h-full w-px";

  if (label) {
    return (
      <div className={`flex items-center gap-md ${className}`} role="separator" aria-label={label}>
        <span className="h-px flex-1 bg-border" />
        <span className="text-caption text-muted">{label}</span>
        <span className="h-px flex-1 bg-border" />
      </div>
    );
  }

  return (
    <div
      role={semantic ? "separator" : "presentation"}
      aria-orientation={semantic && orientation === "vertical" ? "vertical" : undefined}
      className={`${line} shrink-0 bg-border ${className}`}
    />
  );
}

/**
 * A hover/focus hint.
 *
 * Opens on focus as well as hover — a tooltip reachable only by pointer is
 * invisible to keyboard users, which is one of the named anti-patterns in the
 * accessibility contract.
 *
 * It is **never the only source of an accessible name**: the trigger must already
 * be named (an `IconButton` always is). This adds description, not identity, so it
 * wires `aria-describedby` rather than `aria-labelledby`.
 */
export function Tooltip({
  content,
  children,
  side = "top",
  className = ""
}: {
  content: string;
  children: ReactNode;
  side?: "top" | "bottom";
  className?: string;
}) {
  const id = useId();
  const [open, setOpen] = useState(false);

  return (
    <span
      className={`relative inline-flex ${className}`}
      onMouseEnter={() => setOpen(true)}
      onMouseLeave={() => setOpen(false)}
      onFocus={() => setOpen(true)}
      onBlur={() => setOpen(false)}
      // Escape closes it without moving focus, per the overlay contract.
      onKeyDown={(e) => e.key === "Escape" && setOpen(false)}
    >
      <span aria-describedby={open ? id : undefined} className="inline-flex">
        {children}
      </span>
      {open ? (
        <span
          role="tooltip"
          id={id}
          className={`pointer-events-none absolute left-1/2 z-overlay w-max max-w-[16rem] -translate-x-1/2 rounded-md border border-border-strong bg-elevated px-2.5 py-1.5 text-caption text-text shadow-md ${
            side === "top" ? "bottom-full mb-1.5" : "top-full mt-1.5"
          }`}
        >
          {content}
        </span>
      ) : null}
    </span>
  );
}

/**
 * A determinate progress bar.
 *
 * `role="progressbar"` with the aria value trio, so the position is announced
 * rather than only drawn. `label` is required — "63%" of what is not inferable.
 *
 * For indeterminate waits use `Spinner`; for content that is about to appear use
 * `Skeleton`.
 */
export function Progress({
  value,
  max = 100,
  label,
  /** Shown beside the label, e.g. "3 of 5 steps". Falls back to a percentage. */
  valueLabel,
  tone = "accent",
  className = ""
}: {
  value: number;
  max?: number;
  label: string;
  valueLabel?: string;
  tone?: "accent" | "success" | "danger";
  className?: string;
}) {
  const pct = Math.max(0, Math.min(100, (value / max) * 100));
  const text = valueLabel ?? `${Math.round(pct)}%`;
  const fill = { accent: "bg-accent", success: "bg-success", danger: "bg-danger" }[tone];

  return (
    <div className={className}>
      <div className="flex items-baseline justify-between gap-md">
        <span className="text-label text-text">{label}</span>
        <span className="text-caption text-muted">{text}</span>
      </div>
      <div
        role="progressbar"
        aria-label={label}
        aria-valuenow={value}
        aria-valuemin={0}
        aria-valuemax={max}
        aria-valuetext={text}
        className="mt-sm h-1.5 w-full overflow-hidden rounded-pill bg-elevated"
      >
        <div
          className={`h-full rounded-pill transition-[width] duration-base ease-kurx ${fill}`}
          style={{ width: `${pct}%` }}
        />
      </div>
    </div>
  );
}
