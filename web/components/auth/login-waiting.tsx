"use client";

import { useEffect, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { HubConnection, HubConnectionBuilder, HttpTransportType } from "@microsoft/signalr";
import { pollDeviceLogin, type LoginStatus } from "@/lib/auth-api";
import { saveSession } from "@/lib/session";
import { siteConfig } from "@/lib/site";

type Props = {
  challengeId: string;
  pollToken: string;
  matchNumber: number;
  expiresAt: string;
  onCancel: () => void;
};

/**
 * The waiting screen for a device-approval sign-in (AM4/D-080, realtime per AM9/D-087).
 *
 * Two things worth knowing about this component:
 *
 * 1. **SignalR is an accelerator, never the source of truth.** The hub only pushes a *status*; tokens
 *    are always collected over HTTP from `/login/status`, which is where the "minted exactly once"
 *    guarantee lives. Polling therefore runs regardless of whether the socket connects — if the hub is
 *    unreachable (offline, proxy, corporate firewall) the screen still works, just less instantly.
 *
 * 2. **The match number must be displayed.** It is the anti-push-fatigue defence: the user is meant to
 *    compare it with the number on their phone before approving, so it is shown prominently rather than
 *    tucked away.
 */
export function LoginWaiting({ challengeId, pollToken, matchNumber, expiresAt, onCancel }: Props) {
  const router = useRouter();
  const [status, setStatus] = useState<LoginStatus["status"]>("pending");
  const [error, setError] = useState("");
  const [secondsLeft, setSecondsLeft] = useState(() => remainingSeconds(expiresAt));
  const settled = useRef(false);

  useEffect(() => {
    let connection: HubConnection | undefined;
    let pollTimer: ReturnType<typeof setInterval> | undefined;
    let tickTimer: ReturnType<typeof setInterval> | undefined;

    // Collect the session. Guarded so the realtime push and a poll landing together can't both run it.
    async function settle() {
      if (settled.current) return;
      const result = await pollDeviceLogin(challengeId, pollToken).catch(() => null);
      if (!result) return;

      if (result.status === "approved" && result.access_token && result.refresh_token) {
        settled.current = true;
        await saveSession(result.access_token, result.refresh_token);
        router.push("/discover");
        router.refresh();
        return;
      }
      if (result.status !== "pending") {
        settled.current = true;
        setStatus(result.status);
      }
    }

    // Polling is the guaranteed path and always runs (AM4's stated fallback contract).
    pollTimer = setInterval(settle, 2000);

    tickTimer = setInterval(() => {
      const left = remainingSeconds(expiresAt);
      setSecondsLeft(left);
      if (left <= 0 && !settled.current) {
        settled.current = true;
        setStatus("expired");
      }
    }, 1000);

    // Realtime is best-effort: any failure here degrades to the polling above rather than surfacing.
    (async () => {
      try {
        connection = new HubConnectionBuilder()
          .withUrl(`${siteConfig.apiBaseUrl}/hubs/login`, {
            transport: HttpTransportType.WebSockets | HttpTransportType.LongPolling
          })
          .withAutomaticReconnect()
          .build();

        connection.on("login_status", () => void settle());
        await connection.start();
        // The poll token authorizes the subscription — the browser has no session yet (D-087).
        await connection.invoke("Watch", challengeId, pollToken);
      } catch {
        connection = undefined;
      }
    })();

    return () => {
      if (pollTimer) clearInterval(pollTimer);
      if (tickTimer) clearInterval(tickTimer);
      void connection?.stop();
    };
  }, [challengeId, pollToken, expiresAt, router]);

  if (status === "rejected" || status === "expired" || status === "consumed") {
    return (
      <div className="space-y-4 text-center">
        <p className="text-lg font-medium">
          {status === "rejected"
            ? "Sign-in was declined on your device."
            : status === "expired"
              ? "This sign-in request expired."
              : "This sign-in request was already used."}
        </p>
        <button className="text-sm underline" onClick={onCancel}>
          Try again
        </button>
      </div>
    );
  }

  return (
    <div className="space-y-6 text-center">
      <div>
        <p className="text-sm text-muted">Open Kurx on your phone to approve.</p>
        <p className="mt-1 text-sm text-muted">Enter this number there to confirm it&apos;s you:</p>
      </div>

      <div
        className="mx-auto flex h-20 w-20 items-center justify-center rounded-2xl border-2 text-3xl font-bold tabular-nums"
        aria-label={`Match number ${matchNumber}`}
      >
        {matchNumber}
      </div>

      <p className="text-xs text-muted" role="status" aria-live="polite">
        {secondsLeft > 0 ? `Waiting for approval — ${secondsLeft}s left` : "Expiring…"}
      </p>

      {error && <p className="text-sm text-danger">{error}</p>}

      <button className="text-sm underline" onClick={onCancel}>
        Cancel
      </button>
    </div>
  );
}

function remainingSeconds(expiresAt: string): number {
  return Math.max(0, Math.ceil((new Date(expiresAt).getTime() - Date.now()) / 1000));
}
