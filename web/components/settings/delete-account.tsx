"use client";

import { useState, useTransition } from "react";
import { useFormState, useFormStatus } from "react-dom";
import { Button } from "@kurx/ui";
import { requestAccountDeletionAction, cancelAccountDeletionAction } from "@/lib/account-actions";
import type { AccountDeletion } from "@/lib/account-api";

// `border-strong` and the 44px floor, like every other control since Phase 6.
const inputCls =
  "min-h-11 w-full rounded-md border border-border-strong bg-background px-3 text-body text-text " +
  "transition duration-fast focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/30";

function Submit({ label }: { label: string }) {
  const { pending } = useFormStatus();
  // The `danger` variant, not `secondary`: this schedules account deletion, and
  // it looked identical to the Cancel button beside it.
  return (
    <Button type="submit" variant="danger" disabled={pending}>
      {pending ? "Scheduling…" : label}
    </Button>
  );
}

function formatDate(iso?: string | null) {
  if (!iso) return "";
  return new Date(iso).toLocaleDateString(undefined, { day: "numeric", month: "long", year: "numeric" });
}

/// Account deletion (D-263). Scheduled, not immediate: 30 days in which signing in and pressing cancel
/// undoes it entirely. The typed confirmation is a speed bump against a mis-tap — the server's step-up
/// gate is the actual control.
export function DeleteAccount({ initial }: { initial: AccountDeletion | null }) {
  const [pendingDeletion, setPendingDeletion] = useState(initial);
  const [expanded, setExpanded] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [, startTransition] = useTransition();
  const [requestState, requestAction] = useFormState(requestAccountDeletionAction, null);

  // The action revalidates the route, but reflecting it here immediately keeps the panel honest
  // without a full reload.
  if (requestState && "ok" in requestState && !pendingDeletion) {
    setPendingDeletion({ pending: true, scheduled_for: null, requested_at: null });
  }

  function cancel() {
    setError(null);
    startTransition(async () => {
      const result = await cancelAccountDeletionAction();
      if ("error" in result) setError(result.error);
      else setPendingDeletion(null);
    });
  }

  if (pendingDeletion?.pending) {
    return (
      // `role="status"`: the panel swaps to this state after the form submits,
      // and "your account is scheduled for deletion" is not something a
      // screen-reader user should have to go looking for.
      <div className="space-y-3" role="status">
        <p className="text-body text-text">
          Your account is scheduled for deletion
          {pendingDeletion.scheduled_for ? ` on ${formatDate(pendingDeletion.scheduled_for)}` : ""}.
        </p>
        <p className="text-body text-muted">
          Nothing has been removed yet. Cancel any time before that date and your account carries on
          as normal.
        </p>
        {error ? (
          <p role="alert" className="text-body text-danger">
            {error}
          </p>
        ) : null}
        <Button onClick={cancel}>Keep my account</Button>
      </div>
    );
  }

  return (
    <div className="space-y-3">
      <p className="text-body text-muted">
        Deleting removes your profile, posts and comments after a 30-day grace period. Your orders,
        tickets and certificates are kept — we&apos;re required to retain those records, and a
        certificate someone else needs to verify has to stay verifiable.
      </p>

      {!expanded ? (
        <Button variant="secondary" onClick={() => setExpanded(true)}>Delete my account…</Button>
      ) : (
        <form action={requestAction} className="space-y-3">
          <label className="block">
            <span className="mb-1 block text-label text-muted">Why are you leaving? (optional)</span>
            <input name="reason" className={inputCls} placeholder="Tell us if you'd like" />
          </label>
          <label className="block">
            <span className="mb-1 block text-label text-muted">
              Type <span className="font-mono text-text">DELETE</span> to confirm
            </span>
            <input name="confirm" required className={inputCls} placeholder="DELETE" autoComplete="off" />
          </label>
          <div className="flex gap-2">
            <Submit label="Schedule deletion" />
            <Button type="button" variant="secondary" onClick={() => setExpanded(false)}>Cancel</Button>
          </div>
        </form>
      )}
      {requestState && "error" in requestState ? (
        <p role="alert" className="text-body text-danger">
          {requestState.error}
        </p>
      ) : null}
    </div>
  );
}
