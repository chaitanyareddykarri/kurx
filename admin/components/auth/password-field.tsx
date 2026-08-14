"use client";

import { passwordStrength } from "@/lib/password";

// `border-strong` identifies the control (WCAG 1.4.11); `border` is decorative at 1.35:1 (D-288).
const inputCls =
  "h-11 w-full rounded-md border border-border-strong bg-background px-3 text-sm text-text placeholder:text-muted focus:border-accent focus:outline-none";

/** Password input with an optional soft strength meter (a hint only — length gates submission). */
export function PasswordField({
  label,
  value,
  onChange,
  autoComplete,
  showStrength = false,
  onEnter
}: {
  label: string;
  value: string;
  onChange: (value: string) => void;
  autoComplete?: string;
  showStrength?: boolean;
  onEnter?: () => void;
}) {
  const strength = showStrength ? passwordStrength(value) : null;
  const colors = ["bg-border", "bg-danger", "bg-danger", "bg-accent", "bg-accent"];

  return (
    <label className="block text-sm text-muted">
      {label}
      <input
        type="password"
        value={value}
        onChange={(event) => onChange(event.target.value)}
        onKeyDown={(event) => {
          if (event.key === "Enter" && onEnter) onEnter();
        }}
        autoComplete={autoComplete}
        className={`mt-1 ${inputCls}`}
      />
      {strength && value.length > 0 ? (
        <div className="mt-1.5 flex items-center gap-2" aria-live="polite">
          <div className="flex h-1 flex-1 gap-1">
            {[1, 2, 3, 4].map((segment) => (
              <div
                key={segment}
                className={`h-full flex-1 rounded-full ${segment <= strength.score ? colors[strength.score] : "bg-border"}`}
              />
            ))}
          </div>
          <span className="w-12 shrink-0 text-right text-xs text-muted">{strength.label}</span>
        </div>
      ) : null}
    </label>
  );
}
