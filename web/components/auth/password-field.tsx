"use client";

import { Field, Input } from "@/components/ui/field";
import { passwordStrength } from "@/lib/password";

/**
 * Password input with an optional real-time strength meter. The meter is a soft hint only (the
 * backend has no composition rules, D-129) — it never blocks submission; length does that.
 */
export function PasswordField({
  label,
  value,
  onChange,
  id,
  autoComplete,
  showStrength = false,
  error,
  helper,
  onEnter
}: {
  label: string;
  value: string;
  onChange: (value: string) => void;
  id?: string;
  autoComplete?: string;
  showStrength?: boolean;
  error?: string;
  helper?: string;
  onEnter?: () => void;
}) {
  const strength = showStrength ? passwordStrength(value) : null;
  const colors = ["bg-border", "bg-danger", "bg-danger", "bg-accent", "bg-accent"];

  return (
    <div className="space-y-1.5">
      <Field label={label} htmlFor={id} error={error} helper={error ? undefined : helper}>
        <Input
          id={id}
          type="password"
          value={value}
          onChange={(event) => onChange(event.target.value)}
          onKeyDown={(event) => {
            if (event.key === "Enter" && onEnter) onEnter();
          }}
          autoComplete={autoComplete}
          error={Boolean(error)}
        />
      </Field>
      {strength && value.length > 0 ? (
        <div className="flex items-center gap-2" aria-live="polite">
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
    </div>
  );
}
