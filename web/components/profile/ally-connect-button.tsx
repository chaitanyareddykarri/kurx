"use client";

import { useState, useTransition } from "react";
import { Check } from "lucide-react";
import { Button } from "@kurx/ui";
import { requestAllyAction, respondAllyRequestAction, revokeAllyAction } from "@/lib/social-actions";
import { apiErrorMessage } from "@/lib/api";

export type AllyRelation = "none" | "outgoing" | "incoming" | "accepted";

/**
 * The connect control, in all four relation states.
 *
 * Two Phase 18A corrections:
 *
 * 1. **The error was announced to nobody.** A failed connect rendered a red caption with no live
 *    role, so the only report that anything went wrong was a colour change — the S1-1 pattern, in a
 *    component the Phase 7 sweep did not reach because the text is not a `Field` error.
 * 2. **Two buttons were named for their state, not their action.** "Requested" *cancels* the
 *    request; "Ally ✓" *removes* the ally. A control's accessible name has to say what activating it
 *    does, and both said the opposite — the second by way of a literal `✓`, which screen readers
 *    variously read as "check mark" or skip entirely. The visible label still shows the state,
 *    which is the useful thing to see; the accessible name now states the consequence.
 */
export function AllyConnectButton({
  targetUserId,
  targetName,
  initialRelation,
  initialConnectionId,
}: {
  targetUserId: string;
  /** Used to distinguish otherwise identical controls in a list of people. */
  targetName?: string;
  initialRelation: AllyRelation;
  initialConnectionId: string | null;
}) {
  const [relation, setRelation] = useState(initialRelation);
  const [connectionId, setConnectionId] = useState(initialConnectionId);
  const [error, setError] = useState<string | null>(null);
  const [pending, startTransition] = useTransition();

  const of = targetName ? ` ${targetName}` : "";

  function connect() {
    startTransition(async () => {
      try {
        setError(null);
        const conn = await requestAllyAction(targetUserId);
        setConnectionId(conn.id);
        setRelation(conn.status === "Accepted" ? "accepted" : "outgoing");
      } catch (err) {
        setError(apiErrorMessage(err));
      }
    });
  }

  function respond(accept: boolean) {
    if (!connectionId) return;
    startTransition(async () => {
      try {
        setError(null);
        await respondAllyRequestAction(connectionId, accept);
        setRelation(accept ? "accepted" : "none");
      } catch (err) {
        setError(apiErrorMessage(err));
      }
    });
  }

  function remove() {
    if (!connectionId) return;
    startTransition(async () => {
      try {
        setError(null);
        await revokeAllyAction(connectionId);
        setRelation("none");
        setConnectionId(null);
      } catch (err) {
        setError(apiErrorMessage(err));
      }
    });
  }

  return (
    <div className="flex flex-col items-end gap-1.5">
      {relation === "none" && (
        <Button aria-label={`Connect with${of || " this person"}`} onClick={connect} disabled={pending}>
          {pending ? "Sending…" : "Connect"}
        </Button>
      )}
      {relation === "outgoing" && (
        <Button aria-label={`Cancel your ally request to${of || " this person"}`} variant="secondary" onClick={remove} disabled={pending}>
          {pending ? "Cancelling…" : "Requested"}
        </Button>
      )}
      {relation === "incoming" && (
        <div className="flex gap-2">
          <Button aria-label={`Accept${of || " this ally request"}`} onClick={() => respond(true)} disabled={pending}>Accept</Button>
          <Button aria-label={`Decline${of || " this ally request"}`} variant="secondary" onClick={() => respond(false)} disabled={pending}>Decline</Button>
        </div>
      )}
      {relation === "accepted" && (
        <Button aria-label={`Remove${of || " this person"} from your allies`} variant="secondary" onClick={remove} disabled={pending}>
          {pending ? (
            "Removing…"
          ) : (
            <>
              <Check size={14} aria-hidden /> Ally
            </>
          )}
        </Button>
      )}
      {error && <p role="alert" className="text-xs text-danger">{error}</p>}
    </div>
  );
}
