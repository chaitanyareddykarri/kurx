"use client";

import { useState, useTransition } from "react";
import { Button } from "@/components/ui/button";
import { sendSecondFactorCode, verifySecondFactorCode, type SecondFactorMethod } from "@/lib/auth-api";

// `border-strong` identifies the control (WCAG 1.4.11); `border` is decorative at 1.35:1 (D-288).
const inputCls =
  "h-10 w-full rounded-md border border-border-strong bg-background px-3 text-sm text-text placeholder:text-muted focus:border-accent focus:outline-none";

/**
 * Second-factor chooser (D-280 / D-283).
 *
 * The backend decides which methods exist for this account and in what order; this component renders that
 * list and nothing else. It deliberately has **no hardcoded method list and no client-side ranking** — that
 * is the whole point of the contract, and it is what stops web, mobile and admin drifting apart.
 *
 * Only code-delivered methods are actionable here. `trusted_device` and `passkey` have their own ceremonies
 * and are reached from the panel that owns them, so they render as a hint rather than a button.
 */
export function SecondFactorPanel({
  challengeId,
  pollToken,
  methods,
  onSession,
  onBack
}: {
  challengeId: string;
  pollToken: string;
  methods: SecondFactorMethod[];
  onSession: (access: string, refresh: string) => Promise<void>;
  onBack: () => void;
}) {
  const [chosen, setChosen] = useState<SecondFactorMethod | null>(null);
  const [code, setCode] = useState("");
  const [error, setError] = useState("");
  const [pending, startTransition] = useTransition();

  const codeMethods = methods.filter((m) => m.method === "sms_otp" || m.method === "email_otp");
  const otherMethods = methods.filter((m) => m.method !== "sms_otp" && m.method !== "email_otp");

  function choose(method: SecondFactorMethod) {
    setError("");
    startTransition(async () => {
      const result = await sendSecondFactorCode(challengeId, pollToken, method.method);
      if (!result.ok) {
        setError("Couldn't send a code just now. Try again, or pick another method.");
        return;
      }
      setChosen(method);
    });
  }

  function verify() {
    setError("");
    startTransition(async () => {
      const result = await verifySecondFactorCode(challengeId, pollToken, code);
      if (!result.ok || !result.access_token || !result.refresh_token) {
        setError("That code didn't work. Check the digits, or request a new one.");
        return;
      }
      await onSession(result.access_token, result.refresh_token);
    });
  }

  if (chosen) {
    return (
      <div className="space-y-3">
        <p className="text-sm text-muted">
          We sent a 6-digit code to {chosen.hint ?? "you"}.
        </p>
        <input
          value={code}
          onChange={(event) => setCode(event.target.value.replace(/\D/g, "").slice(0, 6))}
          onKeyDown={(event) => {
            if (event.key === "Enter" && code.length === 6 && !pending) verify();
          }}
          inputMode="numeric"
          maxLength={6}
          autoComplete="one-time-code"
          placeholder="6-digit code"
          aria-label="One-time code"
          className={inputCls}
        />
        <Button type="button" onClick={verify} disabled={pending || code.length !== 6}>
          {pending ? "Verifying…" : "Verify and sign in"}
        </Button>
        {error ? (
          <p role="alert" className="text-sm text-danger">
            {error}
          </p>
        ) : null}
        <button
          type="button"
          className="text-sm text-muted underline"
          onClick={() => {
            setChosen(null);
            setCode("");
            setError("");
          }}
        >
          Use a different method
        </button>
      </div>
    );
  }

  return (
    <div className="space-y-3">
      <p className="text-sm text-muted">Confirm it&apos;s you to finish signing in.</p>

      {codeMethods.map((method) => (
        <Button key={method.method} type="button" onClick={() => choose(method)} disabled={pending}>
          {method.label}
          {method.hint ? ` (${method.hint})` : ""}
        </Button>
      ))}

      {/* Every account has a phone, so the backend effectively always offers a code method — but if it
          ever offers none, the screen above renders a heading and nothing else, leaving the user stuck
          mid-login with no explanation and no way forward. Say what happened and point at the ceremonies
          that do exist. */}
      {codeMethods.length === 0 ? (
        <p className="text-sm text-muted">
          {otherMethods.length > 0
            ? "This account has no method that can receive a one-time code. Go back and sign in with one of the methods below."
            : "This account has no second factor set up. Go back and use account recovery to get in."}
        </p>
      ) : null}

      {otherMethods.length > 0 ? (
        <p className="pt-1 text-sm text-muted">
          Also available on this account: {otherMethods.map((m) => m.label).join(", ")}.
        </p>
      ) : null}

      {error ? (
        <p role="alert" className="text-sm text-danger">
          {error}
        </p>
      ) : null}

      <button type="button" className="text-sm text-muted underline" onClick={onBack}>
        Back to sign in
      </button>
    </div>
  );
}
