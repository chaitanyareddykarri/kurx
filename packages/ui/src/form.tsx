"use client";

import { ReactNode } from "react";

import { Spinner } from "./spinner";

/**
 * Form composition: grouping, submission state, and the summary that makes a
 * failed submit navigable.
 *
 * See `docs/ui-ux/accessibility-foundation.md` §3.2. The per-field wiring lives
 * in `Field`; this is everything above it.
 */

/**
 * A related set of fields.
 *
 * `<fieldset>`/`<legend>` rather than a heading + div: a legend is the only thing
 * that gets announced as the *context* of the controls inside it, so a screen
 * reader user hears "Venue — Address line 1" rather than a bare "Address line 1"
 * three groups into a long form.
 */
export function FormGroup({
  legend,
  description,
  children,
  className = ""
}: {
  legend: string;
  description?: string;
  children: ReactNode;
  className?: string;
}) {
  return (
    <fieldset className={`space-y-lg ${className}`}>
      {/* `<legend>` must be the FIRST child of the fieldset or it does not become
          the group's accessible name — wrapping it in a div silently produces an
          unnamed group, which is what this shipped with until a test caught it. */}
      <legend className="text-h3 text-text">{legend}</legend>
      {description ? <p className="-mt-md text-caption text-muted">{description}</p> : null}
      {children}
    </fieldset>
  );
}

/**
 * The error summary shown after a failed submit.
 *
 * Errors beside their fields are necessary but not sufficient on a long form: a
 * user who submits and lands back at the top has no idea what went wrong twelve
 * fields down. This lists them as links to each field, and takes focus so the
 * failure is announced immediately.
 *
 * Backend validation shape is unchanged — the API returns RFC7807 ProblemDetails
 * and callers map it to `{ id, message }` pairs; nothing here parses a response.
 */
export function FormErrorSummary({
  errors,
  title = "Please fix the following"
}: {
  errors: Array<{ id: string; message: string }>;
  title?: string;
}) {
  if (errors.length === 0) return null;
  return (
    <div
      role="alert"
      tabIndex={-1}
      className="rounded-md border border-danger bg-elevated p-lg text-body text-text"
    >
      <p className="font-semibold text-danger">{title}</p>
      <ul className="mt-sm list-inside list-disc space-y-1">
        {errors.map((e) => (
          <li key={e.id}>
            <a href={`#${e.id}`} className="rounded-sm text-accent-text hover:underline focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent">
              {e.message}
            </a>
          </li>
        ))}
      </ul>
    </div>
  );
}

/**
 * The action row at the foot of a form.
 *
 * Reverse DOM order on narrow screens is deliberate: the primary action renders
 * last in the markup so it is the final tab stop, but `flex-col-reverse` puts it
 * on top visually where a thumb reaches it. Tab order and visual order can
 * legitimately differ here because both are correct for their own modality.
 */
export function FormActions({
  children,
  align = "end",
  className = ""
}: {
  children: ReactNode;
  align?: "start" | "end" | "between";
  className?: string;
}) {
  const justify = { start: "sm:justify-start", end: "sm:justify-end", between: "sm:justify-between" }[align];
  return (
    <div className={`flex flex-col-reverse gap-sm sm:flex-row sm:items-center ${justify} ${className}`}>
      {children}
    </div>
  );
}

/**
 * Wraps a form's submitting state.
 *
 * `aria-busy` marks the region in-flight, and the visually-hidden status line
 * announces it — a spinner inside a button is silent, so a screen-reader user
 * otherwise gets no feedback between pressing submit and the response landing.
 * Controls inside are disabled to prevent a double submit.
 */
export function FormBusy({
  busy,
  label = "Submitting",
  children,
  className = ""
}: {
  busy: boolean;
  label?: string;
  children: ReactNode;
  className?: string;
}) {
  return (
    <div aria-busy={busy || undefined} className={className}>
      <fieldset disabled={busy} className="contents">
        {children}
      </fieldset>
      {busy ? (
        <p role="status" className="mt-sm flex items-center gap-sm text-caption text-muted">
          <Spinner size={14} decorative />
          <span aria-hidden="true">{label}…</span>
        </p>
      ) : null}
    </div>
  );
}

/**
 * Progress through a multi-step form (event creation, registration, onboarding).
 *
 * An ordered list, so the count and position are structural rather than
 * decorative, and `aria-current="step"` marks where the user is. Completed steps
 * say so in text, not by colour alone.
 */
export function FormSteps({
  steps,
  current,
  className = ""
}: {
  steps: string[];
  /** Zero-based index of the active step. */
  current: number;
  className?: string;
}) {
  return (
    <nav aria-label="Progress" className={className}>
      <ol className="flex flex-wrap items-center gap-x-md gap-y-sm">
        {steps.map((step, i) => {
          const done = i < current;
          const active = i === current;
          return (
            <li key={step} className="flex items-center gap-sm">
              <span
                aria-hidden="true"
                className={`inline-flex h-6 w-6 shrink-0 items-center justify-center rounded-full border text-micro ${
                  active
                    ? "border-accent bg-accent text-on-accent"
                    : done
                      ? "border-teal text-teal"
                      : "border-border-strong text-muted"
                }`}
              >
                {done ? "✓" : i + 1}
              </span>
              <span
                aria-current={active ? "step" : undefined}
                className={`text-caption ${active ? "font-semibold text-text" : "text-muted"}`}
              >
                {step}
                {done ? <span className="sr-only"> (completed)</span> : null}
                {active ? <span className="sr-only"> (current step)</span> : null}
              </span>
            </li>
          );
        })}
      </ol>
    </nav>
  );
}
