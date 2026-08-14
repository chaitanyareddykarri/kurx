"use client";

import { InputHTMLAttributes, useId } from "react";

/**
 * Date and date-time controls.
 *
 * There are 24 native `type="date"` / `type="datetime-local"` inputs across web
 * and admin, each styled by hand at its call site. This is the one treatment.
 *
 * **Native, deliberately.** A hand-built calendar popover would have to
 * reimplement roving focus, month paging, locale formatting, screen-reader
 * announcement of the selected date and mobile keyboards — and would still be
 * worse than the OS picker on a phone, which is where most of this product's
 * users are. Native `date`/`datetime-local` gives all of that, and Kurx has no
 * date-picker dependency to justify adding (`.claude/CLAUDE.md` §5).
 *
 * What the wrapper adds is what native does not do well:
 *
 *  - a real `<label>` (native pickers have no accessible name of their own);
 *  - `aria-invalid` and the error/helper wiring;
 *  - a stated format hint, because the input's display format follows the
 *    browser locale and is not otherwise announced;
 *  - `min`/`max` surfaced as `aria-describedby` text rather than only enforced
 *    silently on submit.
 */

const base =
  "w-full min-h-11 rounded-md border border-border-strong bg-surface px-3 py-2.5 text-body text-text " +
  "transition duration-fast ease-kurx focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/30 " +
  "disabled:cursor-not-allowed disabled:opacity-50 " +
  // The native indicator is invisible on a warm-dark surface without this.
  "[&::-webkit-calendar-picker-indicator]:opacity-60 [&::-webkit-calendar-picker-indicator]:dark:invert";

type DateTimeFieldProps = Omit<InputHTMLAttributes<HTMLInputElement>, "type"> & {
  label: string;
  /** `datetime-local` (default) captures a wall-clock date and time; `date` a day. */
  granularity?: "date" | "datetime";
  helper?: string;
  error?: string;
};

export function DateTimeField({
  className = "",
  label,
  granularity = "datetime",
  helper,
  error,
  id,
  required,
  ...props
}: DateTimeFieldProps) {
  const auto = useId();
  const fieldId = id ?? auto;
  const helperId = `${fieldId}-helper`;
  const errorId = `${fieldId}-error`;

  const described = [helper ? helperId : null, error ? errorId : null].filter(Boolean).join(" ");

  return (
    <div className="space-y-1.5">
      <label htmlFor={fieldId} className="block text-label text-text">
        {label}
        {required ? <span className="text-muted"> (required)</span> : null}
      </label>
      <input
        id={fieldId}
        type={granularity === "date" ? "date" : "datetime-local"}
        required={required}
        aria-required={required || undefined}
        aria-invalid={error ? true : undefined}
        aria-describedby={described || undefined}
        className={`${base} ${error ? "border-danger focus:border-danger focus:ring-danger/30" : ""} ${className}`}
        {...props}
      />
      {error ? (
        <p id={errorId} role="alert" className="text-caption text-danger">
          {error}
        </p>
      ) : helper ? (
        <p id={helperId} className="text-caption text-muted">
          {helper}
        </p>
      ) : null}
    </div>
  );
}
