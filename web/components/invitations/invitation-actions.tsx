"use client";

import { useState, useTransition } from "react";
import { useRouter } from "next/navigation";
import { Button } from "@/components/ui/button";
import { respondToInvitationAction } from "@/lib/invitation-actions";

/** D-266 M6 — accept / decline. The label is "Accept" and never "Register" or "Going": accepting grants
 *  eligibility to register, and implying otherwise is the misreading that makes an invited guest of a paid
 *  event think they already have a place. */
export function InvitationActions({ invitationId }: { invitationId: string }) {
  const router = useRouter();
  const [isPending, startTransition] = useTransition();
  const [error, setError] = useState<string | null>(null);

  function respond(accept: boolean) {
    startTransition(async () => {
      setError(null);
      const res = await respondToInvitationAction(invitationId, accept);
      if ("error" in res) { setError(res.error ?? "That could not be saved."); return; }
      router.refresh();
    });
  }

  return (
    <div className="flex flex-col items-end gap-1.5">
      <div className="flex gap-2">
        <Button disabled={isPending} onClick={() => respond(true)}>Accept</Button>
        <Button variant="ghost" disabled={isPending} onClick={() => respond(false)}>Decline</Button>
      </div>
      {error ? <p role="alert" className="text-xs text-danger">{error}</p> : null}
    </div>
  );
}
