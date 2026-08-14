"use client";

import { useEffect, useRef } from "react";
import { useFormState } from "react-dom";
import { useToast } from "@kurx/ui";
import { reviewEventAction } from "@/lib/admin-actions";
import { ConfirmDecisionButton } from "@/components/admin/confirm-decision-button";


/** Approve (publish) or reject an event under review (M8, D-057). The gate is org/organizer trust —
 *  the reviewer confirms the org is verified and the organizer is paid-capable, then publishes. */
export function EventReviewForm({ orgId, eventId }: { orgId: string; eventId: string }) {
  const [state, formAction] = useFormState(reviewEventAction.bind(null, orgId, eventId), null);
  const toast = useToast();

  // One result, one toast. `reactStrictMode` double-invokes effects in development, so the bare
  // effect fired the same error toast twice for a single click — which reads as two failed requests
  // and sent a reviewer looking for a retry bug that was not there. The ref keys on the state object
  // identity `useFormState` hands back, so a genuine second submission still toasts.
  const toasted = useRef<unknown>(null);
  useEffect(() => {
    if (!state || toasted.current === state) return;
    toasted.current = state;
    if ("error" in state) toast(String(state.error), "error");
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [state]);

  if (state && "ok" in state) {
    return <p role="status" className="mt-3 text-xs font-medium text-success">Decision recorded.</p>;
  }

  return (
    <form action={formAction} className="mt-3 flex flex-wrap items-center gap-2">
      {/* Both fired on one click, side by side. Approving publishes the event to everyone;
          rejecting takes it away from an organiser who has usually spent days on it. */}
      <ConfirmDecisionButton
        name="action"
        value="publish"
        label="Approve & publish"
        tone="approve"
        title="Approve and publish this event?"
        description="It becomes visible to everyone and can start taking registrations immediately."
        confirmLabel="Approve & publish"
      />
      <ConfirmDecisionButton
        name="action"
        value="reject"
        label="Reject"
        tone="reject"
        title="Reject this event?"
        description="The organiser is told it was rejected and it cannot be published without a fresh review."
        confirmLabel="Reject"
      />
      {state && "error" in state ? <p role="alert" className="text-xs text-danger">{String(state.error)}</p> : null}
    </form>
  );
}
