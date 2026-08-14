"use client";

import { useState, useTransition } from "react";
import { useRouter } from "next/navigation";
import { Button } from "@/components/ui/button";
import { redeemInviteLinkAction } from "@/lib/invitation-actions";

const inputClass = "h-10 w-full rounded-md border border-border bg-background px-3 text-sm text-text";

/** D-266 M6 (D9 Method B) — claim a seat on an invite link.
 *
 *  Redeeming is **idempotent per user** server-side, so a double-click or a refresh consumes no second
 *  seat; this component therefore does not need to guard against re-submission for correctness, only for
 *  clarity. A wrong passcode is refused *before* the seat is claimed, so guessing cannot drain a link. */
export function RedeemInviteLink({
  token, requiresPasscode, eventSlug,
}: { token: string; requiresPasscode: boolean; eventSlug: string }) {
  const router = useRouter();
  const [isPending, startTransition] = useTransition();
  const [passcode, setPasscode] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [done, setDone] = useState(false);

  function redeem() {
    startTransition(async () => {
      setError(null);
      const res = await redeemInviteLinkAction(token, requiresPasscode ? passcode : undefined);
      if ("error" in res) { setError(res.error ?? "That didn't work."); return; }
      setDone(true);
      router.refresh();
    });
  }

  if (done) {
    return (
      <div className="mt-4 space-y-3">
        <p className="rounded-md border border-success/40 bg-success/10 px-3 py-2 text-sm text-text">
          You&apos;re in. You can now register for this event.
        </p>
        <a href={`/e/${eventSlug}`}
          className="inline-flex min-h-11 items-center rounded-md bg-accent px-4 text-label font-semibold text-on-accent">
          Continue to registration
        </a>
      </div>
    );
  }

  return (
    <div className="mt-4 space-y-3">
      {requiresPasscode ? (
        <label className="block text-sm text-muted">
          Passcode
          <input className={inputClass} value={passcode} type="password"
            placeholder="The organiser will have shared this"
            onChange={(e) => setPasscode(e.target.value)} />
        </label>
      ) : null}

      <Button disabled={isPending || (requiresPasscode && passcode.length === 0)} onClick={redeem}>
        {isPending ? "Accepting…" : "Accept invitation"}
      </Button>

      {error ? (
        <p role="alert" className="rounded-md border border-danger/40 bg-danger/10 px-3 py-2 text-sm text-danger">
          {error}
        </p>
      ) : null}
    </div>
  );
}
