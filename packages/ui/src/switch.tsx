"use client";

/**
 * A toggle switch. Controlled: pass `checked` + `onChange`.
 *
 * Three accessibility corrections over the pre-D-286 version
 * (`docs/ui-ux/accessibility-foundation.md` §1, §4):
 *
 *  - The track was 24px tall and was the whole hit area. The button is now 44px
 *    with the track drawn inside it, so the touch floor is met without changing
 *    how the control looks.
 *  - The unchecked track used `border`, which is decorative at 1.30:1 and cannot
 *    identify a control (WCAG 1.4.11). It now uses `border-strong`.
 *  - The knob was `bg-white`. On the ember track that is 2.80:1 — below the 3:1
 *    required for the part that conveys state. It is now `on-accent` ink at
 *    6.08:1 when on, and `muted` on the elevated track when off.
 *
 * `label` is optional only because a `<label htmlFor>` elsewhere may already name
 * it; a switch with neither is unnamed to assistive tech.
 */
export function Switch({
  checked,
  onChange,
  label,
  disabled = false
}: {
  checked: boolean;
  onChange: (next: boolean) => void;
  label?: string;
  disabled?: boolean;
}) {
  return (
    <button
      type="button"
      role="switch"
      aria-checked={checked}
      aria-label={label}
      disabled={disabled}
      onClick={() => onChange(!checked)}
      className="inline-flex h-11 w-11 shrink-0 items-center justify-center rounded-md transition duration-fast ease-kurx focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent disabled:cursor-not-allowed disabled:opacity-50"
    >
      <span
        className={`relative inline-flex h-6 w-11 items-center rounded-pill border transition duration-fast ease-kurx ${
          checked ? "border-accent bg-accent" : "border-border-strong bg-elevated"
        }`}
      >
        <span
          className={`inline-block h-4 w-4 rounded-full transition duration-fast ease-kurx ${
            checked ? "translate-x-5 bg-on-accent" : "translate-x-1 bg-muted"
          }`}
        />
      </span>
    </button>
  );
}
