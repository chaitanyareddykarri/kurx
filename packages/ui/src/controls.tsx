"use client";

import { InputHTMLAttributes, ReactNode, useId } from "react";

/**
 * Selection controls. See `docs/ui-ux/accessibility-foundation.md` §2.4 and §4.
 *
 * All three wrap a NATIVE input rather than reimplementing one on a div. Native
 * gives keyboard operation, form participation, `:checked` state and screen-reader
 * semantics for free — reimplementing them is how `aria-checked` drifts out of
 * sync with what the user sees. The native control is visually replaced with
 * `appearance-none`, not hidden, so focus and checked state remain real.
 *
 * Before this, Checkbox / Radio / Slider did not exist in the design system at
 * all and were hand-rolled per call site across 8+ files.
 *
 * The tick and dot are drawn in `on-accent` ink on the ember fill (6.08:1).
 * White would be 2.86:1 — the D-286 failure.
 */

const focus =
  "focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent";

/** Row wrapper giving the control a 44px touch target without inflating the box. */
function ControlRow({
  htmlFor,
  label,
  description,
  children,
  disabled
}: {
  htmlFor: string;
  label: ReactNode;
  description?: string;
  children: ReactNode;
  disabled?: boolean;
}) {
  return (
    <div className={`flex min-h-11 items-start gap-md py-1.5 ${disabled ? "opacity-50" : ""}`}>
      <div className="flex h-6 shrink-0 items-center">{children}</div>
      <label htmlFor={htmlFor} className={`select-none ${disabled ? "" : "cursor-pointer"}`}>
        <span className="block text-label text-text">{label}</span>
        {description ? <span className="mt-0.5 block text-caption text-muted">{description}</span> : null}
      </label>
    </div>
  );
}

const box =
  "peer h-5 w-5 shrink-0 appearance-none border border-border-strong bg-surface " +
  "transition duration-fast ease-kurx checked:border-accent checked:bg-accent " +
  "disabled:cursor-not-allowed " + focus;

type CheckboxProps = Omit<InputHTMLAttributes<HTMLInputElement>, "type"> & {
  label: ReactNode;
  description?: string;
};

export function Checkbox({ className = "", label, description, id, ...props }: CheckboxProps) {
  const auto = useId();
  const fieldId = id ?? auto;
  return (
    <ControlRow htmlFor={fieldId} label={label} description={description} disabled={props.disabled}>
      <span className="relative inline-flex">
        <input id={fieldId} type="checkbox" className={`${box} rounded-sm ${className}`} {...props} />
        {/* Ink tick, drawn over the ember fill. `peer-checked` keeps the native
            input as the single source of truth for state. */}
        <svg
          aria-hidden="true"
          viewBox="0 0 20 20"
          className="pointer-events-none absolute inset-0 h-5 w-5 scale-90 text-on-accent opacity-0 transition duration-fast peer-checked:scale-100 peer-checked:opacity-100"
        >
          <path
            d="M5 10.5l3.2 3.2L15 7"
            fill="none"
            stroke="currentColor"
            strokeWidth="2.2"
            strokeLinecap="round"
            strokeLinejoin="round"
          />
        </svg>
      </span>
    </ControlRow>
  );
}

type RadioProps = Omit<InputHTMLAttributes<HTMLInputElement>, "type"> & {
  label: ReactNode;
  description?: string;
};

export function Radio({ className = "", label, description, id, ...props }: RadioProps) {
  const auto = useId();
  const fieldId = id ?? auto;
  return (
    <ControlRow htmlFor={fieldId} label={label} description={description} disabled={props.disabled}>
      <span className="relative inline-flex">
        <input id={fieldId} type="radio" className={`${box} rounded-full ${className}`} {...props} />
        <span
          aria-hidden="true"
          className="pointer-events-none absolute left-1/2 top-1/2 h-2 w-2 -translate-x-1/2 -translate-y-1/2 rounded-full bg-on-accent opacity-0 transition duration-fast peer-checked:opacity-100"
        />
      </span>
    </ControlRow>
  );
}

/**
 * A radio group. `<fieldset>`/`<legend>` is the only construct that reliably
 * announces "what am I choosing between?" — a heading above loose radios does
 * not. Arrow-key navigation and the single tab stop come from the native
 * `name` grouping.
 */
export function RadioGroup({
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
    <fieldset className={className}>
      <legend className="text-label text-text">{legend}</legend>
      {description ? <p className="mt-0.5 text-caption text-muted">{description}</p> : null}
      <div className="mt-sm">{children}</div>
    </fieldset>
  );
}

type SliderProps = Omit<InputHTMLAttributes<HTMLInputElement>, "type"> & {
  label: string;
  /** Rendered beside the label, e.g. a formatted price. Also used as the value text. */
  valueLabel?: string;
};

export function Slider({ className = "", label, valueLabel, id, ...props }: SliderProps) {
  const auto = useId();
  const fieldId = id ?? auto;
  return (
    <div className="py-1.5">
      <div className="flex items-baseline justify-between gap-md">
        <label htmlFor={fieldId} className="text-label text-text">
          {label}
        </label>
        {valueLabel ? <span className="text-caption text-muted">{valueLabel}</span> : null}
      </div>
      <input
        id={fieldId}
        type="range"
        // aria-valuetext so a screen reader announces "₹1,200", not "1200".
        aria-valuetext={valueLabel}
        className={`mt-sm h-11 w-full cursor-pointer appearance-none bg-transparent
          [&::-webkit-slider-runnable-track]:h-1.5 [&::-webkit-slider-runnable-track]:rounded-pill [&::-webkit-slider-runnable-track]:bg-elevated
          [&::-moz-range-track]:h-1.5 [&::-moz-range-track]:rounded-pill [&::-moz-range-track]:bg-elevated
          [&::-webkit-slider-thumb]:mt-[-7px] [&::-webkit-slider-thumb]:h-5 [&::-webkit-slider-thumb]:w-5 [&::-webkit-slider-thumb]:appearance-none [&::-webkit-slider-thumb]:rounded-full [&::-webkit-slider-thumb]:border-2 [&::-webkit-slider-thumb]:border-accent [&::-webkit-slider-thumb]:bg-surface
          [&::-moz-range-thumb]:h-5 [&::-moz-range-thumb]:w-5 [&::-moz-range-thumb]:rounded-full [&::-moz-range-thumb]:border-2 [&::-moz-range-thumb]:border-accent [&::-moz-range-thumb]:bg-surface
          disabled:cursor-not-allowed disabled:opacity-50 ${focus} ${className}`}
        {...props}
      />
    </div>
  );
}
