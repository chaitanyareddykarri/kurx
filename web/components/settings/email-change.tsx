"use client";

import { useFormState, useFormStatus } from "react-dom";
import { Button } from "@/components/ui/button";
import { startEmailChangeAction, completeEmailChangeAction } from "@/lib/account-actions";

// `border-strong` identifies the control (WCAG 1.4.11); `border` is decorative at 1.35:1 (D-288).
const inputCls = "h-10 w-full rounded-md border border-border-strong bg-background px-3 text-sm text-text";

function Submit({ label }: { label: string }) {
  const { pending } = useFormStatus();
  return <Button type="submit" disabled={pending}>{pending ? "…" : label}</Button>;
}

/// Email change (D-263). Two steps, because the code goes to the NEW address — proving the person
/// asking can actually receive mail there. The address currently on file is told a change was
/// requested at step one, which is the only warning a compromised account ever gets.
export function EmailChange({ current }: { current?: string | null }) {
  const [startState, startAction] = useFormState(startEmailChangeAction, null);
  const [doneState, completeAction] = useFormState(completeEmailChangeAction, null);

  const sentTo = startState && "email" in startState ? startState.email : "";

  if (doneState && "ok" in doneState) {
    return <p className="text-sm text-text">Email updated.</p>;
  }

  return (
    <div className="space-y-4">
      {current ? (
        <p className="text-sm text-muted">Current: <span className="text-text">{current}</span></p>
      ) : (
        <p className="text-sm text-muted">No email on file yet.</p>
      )}

      <form action={startAction} className="flex flex-wrap items-end gap-2">
        <label className="flex-1 min-w-[14rem]">
          <span className="mb-1 block text-xs text-muted">New email</span>
          <input name="email" type="email" required className={inputCls} placeholder="you@example.com" />
        </label>
        <Submit label={sentTo ? "Resend code" : "Send code"} />
      </form>
      {startState && "error" in startState ? (
        <p className="text-sm text-danger">{startState.error}</p>
      ) : null}

      {sentTo ? (
        <form action={completeAction} className="flex flex-wrap items-end gap-2">
          <input type="hidden" name="email" value={sentTo} />
          <label className="flex-1 min-w-[10rem]">
            <span className="mb-1 block text-xs text-muted">
              Code sent to {sentTo}
            </span>
            <input name="code" inputMode="numeric" autoComplete="one-time-code" required className={inputCls} placeholder="6-digit code" />
          </label>
          <Submit label="Confirm" />
        </form>
      ) : null}
      {doneState && "error" in doneState ? (
        <p className="text-sm text-danger">{doneState.error}</p>
      ) : null}
    </div>
  );
}
