"use client";

import { ReactNode } from "react";

/**
 * A single-select group rendered as cards.
 *
 * The create-event wizard had five of these — Representing, Visibility, Pricing, Category, Type — and
 * every one was a grid of plain `<button>`s. To a screen reader that is N unrelated buttons: no group
 * name, no indication the choices are mutually exclusive, and **no way to tell which one is chosen**,
 * because selection was carried entirely by `border-accent ring-1 ring-accent`. On the Category step
 * that is a grid of buttons with nothing but a name on each.
 *
 * Built on real `<input type="radio">` elements rather than `role="radio"` and `aria-checked`: the
 * browser then owns arrow-key navigation, the roving tabindex and mutual exclusion, none of which can
 * drift out of step with the visuals. The input is visually hidden but not `display: none` — it must
 * stay focusable, and `peer-focus-visible` is what draws the ring on the card around it.
 *
 * `<fieldset>`/`<legend>` for the grouping, and the legend is the FIRST child: a `<legend>` that is
 * not leaves the group silently unnamed, which is a trap this program has already paid for once.
 */
export function SelectCardGroup({
  legend,
  description,
  name,
  className = "",
  children
}: {
  legend: string;
  description?: string;
  /** Shared radio `name` — what makes the browser treat the options as one group. */
  name: string;
  className?: string;
  children: ReactNode;
}) {
  return (
    <fieldset>
      <legend className="text-sm text-muted">{legend}</legend>
      {description ? <p className="mt-0.5 text-caption text-muted">{description}</p> : null}
      <div className={`mt-3 ${className}`} role="presentation" data-group={name}>
        {children}
      </div>
    </fieldset>
  );
}

export function SelectCard({
  name,
  value,
  checked,
  onSelect,
  disabled = false,
  icon,
  title,
  description
}: {
  name: string;
  value: string;
  checked: boolean;
  onSelect: () => void;
  disabled?: boolean;
  icon?: ReactNode;
  title: string;
  description?: ReactNode;
}) {
  return (
    <label
      className={`relative block w-full rounded-lg border p-4 text-left transition duration-fast ${
        disabled
          ? "cursor-not-allowed border-border opacity-50"
          : checked
            ? "cursor-pointer border-accent ring-1 ring-accent"
            : "cursor-pointer border-border hover:border-accent/60"
      } has-[:focus-visible]:outline has-[:focus-visible]:outline-2 has-[:focus-visible]:outline-offset-2 has-[:focus-visible]:outline-accent`}
    >
      <input
        type="radio"
        name={name}
        value={value}
        checked={checked}
        disabled={disabled}
        onChange={onSelect}
        // `sr-only` rather than `hidden`: a hidden input is not focusable, and the whole point is
        // that the browser can move between these with the arrow keys.
        className="sr-only"
      />
      {icon}
      <strong className="mt-3 block text-text">{title}</strong>
      {description ? <span className="mt-1 block text-xs text-muted">{description}</span> : null}
      {/* Selection was border-and-ring only. A checkmark is a second channel that survives greyscale;
          the state itself reaches assistive tech from the radio, not from either of them. */}
      {checked ? (
        <span aria-hidden className="absolute right-3 top-3 text-accent-text">
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="3" strokeLinecap="round" strokeLinejoin="round">
            <path d="M20 6 9 17l-5-5" />
          </svg>
        </span>
      ) : null}
    </label>
  );
}
