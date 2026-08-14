import NextLink from "next/link";
import { AnchorHTMLAttributes, ButtonHTMLAttributes, ReactNode } from "react";

/**
 * Kurx buttons. See `docs/ui-ux/accessibility-foundation.md` §1 and §4.
 *
 * `primary` carries `text-on-accent` — INK, not white. White on the ember fill
 * measures 2.86:1, below the AA floor, and that is what this component shipped
 * before D-286; the identical defect existed in Flutter's `filledButtonTheme`.
 * Ink on the same ember is 6.08:1, so the fill keeps its full vividness.
 *
 * `secondary` uses `border-strong`, not `border`: a button's edge is what
 * identifies it as a control, so it must clear 3:1 (WCAG 1.4.11). `border` is
 * decorative at 1.30:1.
 *
 * Default height is 44px — the touch floor. `sm` (36px) exists for dense admin
 * rows, which are pointer-only surfaces where 32-36px is acceptable.
 */

const variants = {
  primary: "border-accent bg-accent text-on-accent hover:bg-accent/90",
  secondary: "border-border-strong bg-surface text-text hover:bg-elevated",
  ghost: "border-transparent bg-transparent text-muted hover:text-text hover:bg-surface",
  // Destructive actions were previously hand-rolled per call site with no shared
  // treatment, which is how a delete button ends up looking like a save button.
  danger: "border-danger bg-danger text-background hover:bg-danger/90"
} as const;

const sizes = {
  sm: "h-9 px-3 text-caption",
  md: "h-11 px-4 text-label",
  lg: "h-12 px-5 text-body"
} as const;

const base =
  "inline-flex items-center justify-center gap-2 rounded-md border font-semibold transition duration-fast ease-kurx " +
  // WCAG 2.4.7. The shared Button carried NO focus styles at all, so every button on both surfaces
  // was invisible to a keyboard user — found in Phase 46.3b by focusing each control in a live page
  // and comparing computed style before and after. Nothing in source review, 372 tests or lint sees
  // this, because the defect is the absence of a class rather than the presence of a wrong one.
  // Same declaration the hand-rolled buttons already used (admin's ConfirmDecisionButton).
  "focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent " +
  "disabled:cursor-not-allowed disabled:opacity-50 aria-disabled:cursor-not-allowed aria-disabled:opacity-50";

type Variant = keyof typeof variants;
type Size = keyof typeof sizes;

type ButtonProps = ButtonHTMLAttributes<HTMLButtonElement> & {
  variant?: Variant;
  size?: Size;
};

/**
 * `type="button"` unless the caller asks otherwise.
 *
 * HTML defaults a `<button>` inside a form to `type="submit"`, and this component never set one — so
 * every `Button` placed in a form submitted it, whether or not that was the intent. Phase 30 found it
 * the hard way: a moderation decision submitted **twice**, because `ConfirmDialog` renders inside the
 * form it guards, so its confirm button submitted the form *and* the guard's own `requestSubmit()`
 * did. The same dialog's **Cancel** button submitted the form too.
 *
 * 44 call sites already write `type="submit"` explicitly, which is what made this safe to flip: the
 * ones that want a submit say so, and the ~38 that pass only `onClick` were relying on the default
 * doing nothing — which, inside a form, it did not.
 */
export function Button({ className = "", variant = "primary", size = "md", type = "button", ...props }: ButtonProps) {
  return <button type={type} className={`${base} ${sizes[size]} ${variants[variant]} ${className}`} {...props} />;
}

type LinkButtonProps = AnchorHTMLAttributes<HTMLAnchorElement> & {
  href: string;
  children: ReactNode;
  variant?: Variant;
  size?: Size;
};

export function LinkButton({
  className = "",
  variant = "primary",
  size = "md",
  href,
  children,
  ...props
}: LinkButtonProps) {
  return (
    <NextLink href={href} className={`${base} ${sizes[size]} ${variants[variant]} ${className}`} {...props}>
      {children}
    </NextLink>
  );
}

/**
 * An icon-only control.
 *
 * `label` is required and becomes the accessible name — an icon-only button
 * without one is invisible to assistive tech, and the audit found this pattern
 * (`p-2` around an 18px icon) in the web header, both overlay close buttons and
 * the admin topbar.
 *
 * Those call sites were also 32-34px, under the 44px floor. The hit area here is
 * the minimum by construction, so it cannot be got wrong per-call-site: `md`
 * renders a 44px box regardless of the icon inside it. Use `sm` only on
 * pointer-only admin density.
 */
const iconSizes = {
  sm: "h-8 w-8",
  md: "h-11 w-11",
  lg: "h-12 w-12"
} as const;

type IconButtonProps = Omit<ButtonHTMLAttributes<HTMLButtonElement>, "aria-label"> & {
  label: string;
  variant?: Variant;
  size?: Size;
};

export function IconButton({
  className = "",
  variant = "ghost",
  size = "md",
  label,
  children,
  ...props
}: IconButtonProps) {
  return (
    <button
      aria-label={label}
      title={label}
      className={`${base} shrink-0 rounded-md ${iconSizes[size]} ${variants[variant]} ${className}`}
      {...props}
    >
      {children}
    </button>
  );
}

/**
 * A textual link.
 *
 * Uses `accent-text`, not `accent`. The ember fill is 2.80:1 against the light
 * background and is not text-safe by design; `accent-text` is its darkened
 * text-carrying counterpart (identical on dark, where ember already passes).
 */
type LinkProps = AnchorHTMLAttributes<HTMLAnchorElement> & {
  href: string;
  children: ReactNode;
  /** Renders in the current text colour, underlined — for links inside prose. */
  muted?: boolean;
};

export function Link({ className = "", href, children, muted = false, ...props }: LinkProps) {
  const tone = muted ? "text-text underline underline-offset-2" : "text-accent-text hover:underline";
  return (
    <NextLink
      href={href}
      className={`rounded-sm transition duration-fast ease-kurx ${tone} ${className}`}
      {...props}
    >
      {children}
    </NextLink>
  );
}
