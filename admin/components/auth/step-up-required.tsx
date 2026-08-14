"use client";

import { useEffect, useRef, useState } from "react";
import { Button } from "@kurx/ui";
import { stepUpStatusAction } from "@/lib/security-actions";

/**
 * Step-up required (Phase 2F, AM6) — staff parity with the web app. The console can't produce a device
 * signature, and there's no cross-device push for step-up, so the staff member confirms it's them in
 * the Kurx mobile app (a short server-side grant); this polls `/step-up/status` server-side and retries
 * the original action once the grant lands.
 */
export function StepUpRequired({
  reason,
  onCancel,
  onSatisfied
}: {
  reason: string;
  onCancel: () => void;
  onSatisfied: () => Promise<void>;
}) {
  const [checking, setChecking] = useState(false);
  const settled = useRef(false);

  useEffect(() => {
    let timer: ReturnType<typeof setInterval> | undefined;

    async function poll() {
      if (settled.current) return;
      const status = await stepUpStatusAction();
      if (settled.current) return;
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
  }, [onSatisfied]);

  async function checkNow() {
    setChecking(true);
    const status = await stepUpStatusAction();
    setChecking(false);
    if (status.satisfied && !settled.current) {
      settled.current = true;
      await onSatisfied();
    }
  }

  return (
    <div className="space-y-4 rounded-lg border border-border bg-background p-5">
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
        <Button variant="secondary" onClick={checkNow} disabled={checking}>
          {checking ? "Checking…" : "I've confirmed — continue"}
        </Button>
        <Button variant="ghost" onClick={onCancel}>
          Cancel
        </Button>
      </div>
    </div>
  );
}
