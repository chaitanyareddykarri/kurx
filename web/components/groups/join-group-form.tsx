"use client";

import { useFormState, useFormStatus } from "react-dom";
import { joinGroupAction } from "@/lib/group-actions";
import { Button } from "@/components/ui/button";

const inputClass = "h-10 w-full rounded-md border border-border bg-background px-3 text-sm text-text";

function SubmitButton() {
  const { pending } = useFormStatus();
  return <Button type="submit" disabled={pending} className="shrink-0">{pending ? "Joining…" : "Join group"}</Button>;
}

export function JoinGroupForm() {
  const [state, formAction] = useFormState(joinGroupAction, null);

  return (
    <div>
      <form action={formAction} className="flex flex-col gap-3 sm:flex-row sm:items-end">
        <div className="flex-1">
          <label className="text-sm font-medium text-text" htmlFor="join-code">Join code</label>
          <input id="join-code" name="joinCode" required minLength={4} maxLength={10}
            placeholder="e.g. AB12CD" className={`mt-1 ${inputClass}`} />
        </div>
        <div className="flex-1">
          <label className="text-sm font-medium text-text" htmlFor="display-name">
            Your name in the group <span className="text-muted">(optional)</span>
          </label>
          <input id="display-name" name="displayName" placeholder="Optional" className={`mt-1 ${inputClass}`} />
        </div>
        <SubmitButton />
      </form>
      {state && "error" in state ? <p className="mt-2 text-sm text-danger">{String(state.error)}</p> : null}
      {state && "ok" in state ? <p className="mt-2 text-sm text-accent">Joined — your group appears below.</p> : null}
    </div>
  );
}
