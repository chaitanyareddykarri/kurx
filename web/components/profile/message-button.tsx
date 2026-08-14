"use client";

import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";
import { Button, Spinner } from "@kurx/ui";
import { openDmAction } from "@/lib/account-actions";

/// Starts (or reopens) a direct conversation from a public profile (D-264).
///
/// The server call is idempotent, so this never has to ask whether a conversation already exists — it
/// just opens the one room for the pair and navigates to it. `blocked` comes back as a plain error
/// rather than a distinct state: the person tapping already knows this account exists.
export function MessageButton({ targetUserId, targetName }: { targetUserId: string; targetName?: string }) {
  const router = useRouter();
  const [error, setError] = useState<string | null>(null);
  const [pending, startTransition] = useTransition();

  function open() {
    setError(null);
    startTransition(async () => {
      const result = await openDmAction(targetUserId);
      if ("error" in result) setError(result.error);
      else if ("roomId" in result) router.push(`/chats/${result.roomId}`);
    });
  }

  return (
    <span className="inline-flex flex-col items-start gap-1">
      <Button
        variant="secondary"
        aria-label={targetName ? `Message ${targetName}` : "Message this person"}
        onClick={open}
        disabled={pending}
      >
        {/* The pending label was a bare "…", which is not a word — screen readers read it as nothing
            or as "dot dot dot", and the button's name changed to punctuation mid-action. */}
        {pending ? <Spinner size={16} label="Opening the conversation" /> : "Message"}
      </Button>
      {/* `blocked` and every other refusal arrived here as red text with no live role, so the one
          explanation of why nothing happened was inaudible. */}
      {error ? <span role="alert" className="text-xs text-danger">{error}</span> : null}
    </span>
  );
}
