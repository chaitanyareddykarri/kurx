"use client";

import { useEffect, useRef, useState } from "react";
import { stepUpStatus } from "@/lib/auth-api";
import { Button } from "@/components/ui/button";

/**
 * Step-up required (Phase 2F, AM6). A genuinely sensitive action wants a *fresh* proof of device
 * possession on top of the session. The browser can't produce a device signature itself, and the
 * backend exposes no cross-device push for step-up — so the flow here is: the user confirms it's them
 * in the Kurx mobile app (which grants a short server-side window), this polls `/step-up/status`, and
 * the original action is retried automatically once the grant lands.
 *
 * Deliberately NOT the login-waiting component: that collects a session from a login challenge, this
 * only observes an already-authenticated user's step-up grant. They share the polling shape, not the
 * security semantics.
 */
export function StepUpRequired({
  accessToken,
  reason,
  onCancel,
  onSatisfied
}: {
  accessToken: string;
  reason: string;
  onCancel: () => void;
  /** Retries the original operation. Runs at most once, when the step-up grant is observed. */
  onSatisfied: () => Promise<void>;
}) {
  const [checking, setChecking] = useState(false);
  const settled = useRef(false);

  useEffect(() => {
    let timer: ReturnType<typeof setInterval> | undefined;

    async function poll() {
      if (settled.current) return;
      const status = await stepUpStatus(accessToken).catch(() => null);
      if (!status || settled.current) return;
      if (status.satisfied) {
        settled.current = true;
        if (timer) clearInterval(timer);
        await onSatisfied();
      }
    }

    timer = setInterval(poll, 2500);
    return () => {
      if (timer) clearInterval(timer);
    };
  }, [accessToken, onSatisfied]);

  async function checkNow() {
    setChecking(true);
    const status = await stepUpStatus(accessToken).catch(() => null);
    setChecking(false);
    if (status?.satisfied && !settled.current) {
      settled.current = true;
      await onSatisfied();
    }
  }

  return (
    <div className="space-y-4 rounded-lg border border-border bg-surface p-5">
      <div>
        <h3 className="text-base font-semibold text-text">Confirm it&apos;s you</h3>
        <p className="mt-1 text-sm text-muted">{reason}</p>
      </div>
      <ol className="space-y-1.5 text-sm text-text">
        <li>1. Open the Kurx app on your phone.</li>
        <li>
          2. Go to <span className="font-medium">Security → Confirm it&apos;s you</span> and approve with your device.
        </li>
        <li>3. We&apos;ll finish here automatically.</li>
      </ol>
      <p className="text-xs text-muted" role="status" aria-live="polite">
        Waiting for confirmation on your phone…
      </p>
      <div className="flex flex-wrap gap-2">
        <Button type="button" variant="secondary" onClick={checkNow} disabled={checking}>
          {checking ? "Checking…" : "I've confirmed — continue"}
        </Button>
        <Button type="button" variant="ghost" onClick={onCancel}>
          Cancel
        </Button>
      </div>
    </div>
  );
}
