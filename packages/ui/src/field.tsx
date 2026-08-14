"use client";

import {
  InputHTMLAttributes,
  ReactNode,
  SelectHTMLAttributes,
  TextareaHTMLAttributes,
  cloneElement,
  forwardRef,
  isValidElement,
  useId
} from "react";

/**
 * `border-strong`, not `border`: an input's edge is the only thing that shows
 * where the control is, so WCAG 1.4.11 applies and it must clear 3:1. `border`
 * is decorative at 1.30:1 and was what this shipped before D-286 — the field
 * boundary was effectively invisible.
 *
 * Height is 44px (`min-h-11`) to meet the touch floor, and the text size is
 * `body` rather than `text-sm`: an input the user types prose into should not be
 * set at caption size (audit S2-3).
 */
/**
 * Exported so surfaces that must hand-roll a control (a native `<select>` inside a
 * table cell, a server-action form with no client component to hold `Field`) get
 * the same contract instead of copying an approximation of it.
 *
 * Phase 21 found the approximation: `"h-10 w-full rounded-md border border-border
 * bg-background px-3 text-sm text-text"` copy-pasted into 20 local `inputClass` /
 * `textareaClass` constants across 17 host files — 40px against the 44px floor,
 * on the DECORATIVE border token at 1.30:1 where WCAG 1.4.11 wants 3:1 for a
 * boundary that identifies a control, and with no focus ring at all. Prefer
 * `Field` + `Input`; reach for this only where that genuinely cannot go.
 */
export const controlClass =
  "w-full min-h-11 rounded-md border border-border-strong bg-surface px-3 py-2.5 text-body text-text " +
  "placeholder:text-muted transition duration-fast ease-kurx " +
  "focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/30 " +
  "disabled:cursor-not-allowed disabled:opacity-50";

const base = controlClass;

const invalid = "border-danger focus:border-danger focus:ring-danger/30";

/**
 * Label + helper/error wrapper: the one place a form control gets its accessible
 * wiring.
 *
 * **This closes audit S1-1.** `aria-describedby` and `aria-invalid` appeared
 * ZERO times across all 115 web and admin screens, because this component
 * rendered the error as a bare `<p>` with no `id` and never linked it, while
 * `Input`'s `error` prop only recoloured a border. Every validation failure in
 * Kurx was therefore communicated by colour alone — inaudible to a screen reader
 * and invisible to a colour-blind user, failing WCAG 1.3.1, 3.3.1 and 1.4.1.
 *
 * `Field` now generates one id per field and pushes four things onto its child:
 * `id`, `aria-describedby` (helper and/or error), `aria-invalid`, and
 * `aria-required`. The error node carries `role="alert"` so it is announced when
 * it appears rather than only when focus lands on the field.
 *
 * The wiring is injected via `cloneElement` so **existing call sites need no
 * change** — `<Field label="x"><Input /></Field>` gains the whole contract with
 * no edit. A caller that sets its own `id` or `aria-describedby` wins; nothing
 * here overrides an explicit choice.
 */
export function Field({
  label,
  htmlFor,
  helper,
  error,
  required,
  className = "",
  children
}: {
  label: string;
  /** Only needed when the control is not the direct child (the wiring is automatic otherwise). */
  htmlFor?: string;
  helper?: string;
  error?: string;
  required?: boolean;
  /** For the wrapper, so a field can take part in its parent's layout (`flex-1` in a search row). */
  className?: string;
  children: ReactNode;
}) {
  const auto = useId();
  // A child that brought its own id keeps it, and the LABEL must follow it there
  // — pointing `htmlFor` at the generated id instead would leave the control
  // unlabelled, which is the failure this component exists to prevent.
  const childId = isValidElement(children)
    ? ((children.props as Record<string, unknown>).id as string | undefined)
    : undefined;
  const fieldId = htmlFor ?? childId ?? auto;
  const helperId = `${fieldId}-helper`;
  const errorId = `${fieldId}-error`;

  const describedBy = [helper ? helperId : null, error ? errorId : null].filter(Boolean).join(" ");

  // Only wire a single element child; a fragment or list is left alone and the
  // caller is expected to pass `htmlFor` and wire it themselves.
  const control = isValidElement(children)
    ? cloneElement(children as React.ReactElement<Record<string, unknown>>, {
        id: fieldId,
        "aria-describedby":
          (children.props as Record<string, unknown>)["aria-describedby"] ?? (describedBy || undefined),
        "aria-invalid": (children.props as Record<string, unknown>)["aria-invalid"] ?? (error ? true : undefined),
        "aria-required": (children.props as Record<string, unknown>)["aria-required"] ?? (required || undefined),
        error: (children.props as Record<string, unknown>).error ?? (error ? true : undefined)
      })
    : children;

  return (
    <div className={`space-y-1.5 ${className}`}>
      <label htmlFor={fieldId} className="block text-label text-text">
        {label}
        {/* Spelled out, not a bare asterisk: "*" has no meaning to a screen
            reader and no legend on most of these forms. */}
        {required ? <span className="font-normal text-muted"> (required)</span> : null}
      </label>
      {control}
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

/**
 * Refs are forwarded because focus management needs them: moving focus to a
 * field that has just appeared, or to the first field that failed validation, is
 * part of the accessibility contract (§2.3) and is impossible without one.
 */
export const Input = forwardRef<HTMLInputElement, InputHTMLAttributes<HTMLInputElement> & { error?: boolean }>(
  function Input({ className = "", error, ...props }, ref) {
    return (
      <input
        ref={ref}
        aria-invalid={error || undefined}
        className={`${base} ${error ? invalid : ""} ${className}`}
        {...props}
      />
    );
  }
);

export const Textarea = forwardRef<
  HTMLTextAreaElement,
  TextareaHTMLAttributes<HTMLTextAreaElement> & { error?: boolean }
>(function Textarea({ className = "", error, ...props }, ref) {
  return (
    <textarea
      ref={ref}
      aria-invalid={error || undefined}
      className={`${base} min-h-24 resize-y ${error ? invalid : ""} ${className}`}
      {...props}
    />
  );
});

export const Select = forwardRef<
  HTMLSelectElement,
  SelectHTMLAttributes<HTMLSelectElement> & { error?: boolean }
>(function Select({ className = "", error, children, ...props }, ref) {
  return (
    <select
      ref={ref}
      aria-invalid={error || undefined}
      className={`${base} appearance-none ${error ? invalid : ""} ${className}`}
      {...props}
    >
      {children}
    </select>
  );
});
