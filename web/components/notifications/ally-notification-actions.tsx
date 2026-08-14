"use client";

import { useState, useTransition } from "react";
import { Button } from "@kurx/ui";
import { apiErrorMessage } from "@/lib/api";
import { respondAllyRequestAction } from "@/lib/social-actions";

/** Inline Accept/Decline on a "New Ally Request" notification card — reuses the same server action
 * the Allies page already uses, so accepting here and accepting there behave identically. */
export function AllyNotificationActions({ connectionId }: { connectionId: string }) {
  const [resolved, setResolved] = useState<"accepted" | "declined" | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [pending, start] = useTransition();

  if (resolved) {
    return <p className="mt-2 text-xs text-muted">{resolved === "accepted" ? "Accepted" : "Declined"}</p>;
  }

  const respond = (accept: boolean) =>
    start(async () => {
      setError(null);
      try {
        await respondAllyRequestAction(connectionId, accept);
        setResolved(accept ? "accepted" : "declined");
      } catch (err) {
        setError(apiErrorMessage(err));
      }
    });

  return (
    <div className="mt-2">
      <div className="flex gap-2">
        <Button variant="secondary" disabled={pending} onClick={() => respond(true)}>
          Accept
        </Button>
        <Button variant="secondary" disabled={pending} onClick={() => respond(false)}>
          Decline
        </Button>
      </div>
      {error && <p className="mt-1 text-xs text-danger">{error}</p>}
    </div>
  );
}
