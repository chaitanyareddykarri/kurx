"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { redeemRecovery, startRecovery } from "@/lib/auth-api";
import { saveSession } from "@/lib/session";
import { Button } from "@/components/ui/button";
import { toE164Identifier } from "@kurx/ui";
import { Field, Input } from "@/components/ui/field";

/**
 * Account recovery (AM7/D-083) — the way back in when every device is gone.
 *
 * The server returns one opaque `invalid_recovery` for every failure (unknown account, wrong OTP,
 * wrong or already-used code), so this UI deliberately shows a single generic message rather than
 * inventing more specific ones. Guessing at which half failed would hand an attacker the account
 * enumeration the backend is careful not to leak.
 */
export function RecoveryPanel() {
  const router = useRouter();
  const [step, setStep] = useState<"identify" | "redeem">("identify");
  const [identifier, setIdentifier] = useState("");
  const [otpCode, setOtpCode] = useState("");
  const [recoveryCode, setRecoveryCode] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");

  async function sendCode() {
    setError("");
    setBusy(true);
    try {
      // Always succeeds by design — never reveals whether the account exists.
      await startRecovery(toE164Identifier(identifier));
      setStep("redeem");
    } catch {
      setError("Couldn't start recovery. Please try again.");
    } finally {
      setBusy(false);
    }
  }

  async function redeem() {
    setError("");
    setBusy(true);
    try {
      const tokens = await redeemRecovery(toE164Identifier(identifier), otpCode.trim(), recoveryCode.trim());
      await saveSession(tokens.access_token, tokens.refresh_token);
      // Recovery revoked every old session and suspended every device, so the very next thing the
      // user needs is to set this device up again.
      // REG-001: this pushed to /account/security, which is not a route — so the last
      // step of account recovery landed on a 404. The security page is /settings/security.
      router.push("/settings/security?recovered=1");
      router.refresh();
    } catch {
      setError("That combination didn't work. Check the code from your phone and your recovery code.");
    } finally {
      setBusy(false);
    }
  }

  if (step === "identify") {
    return (
      <div className="space-y-4">
        <div>
          <h1 className="text-xl font-semibold">Recover your account</h1>
          <p className="text-sm text-muted">
            We&apos;ll text a code to your registered number. You&apos;ll also need one of your saved
            recovery codes.
          </p>
        </div>
        <Field label="Phone number or email">
          <Input
            value={identifier}
            onChange={(e) => setIdentifier(e.target.value)}
            placeholder="Email, or phone including your country code"
            autoComplete="username"
          />
        </Field>
        {error && (
          <p className="text-sm text-danger" role="alert">
            {error}
          </p>
        )}
        <Button className="w-full" disabled={busy || identifier.trim().length < 3} onClick={sendCode}>
          {busy ? "Sending…" : "Send code"}
        </Button>
      </div>
    );
  }

  return (
    <div className="space-y-4">
      <div>
        <h1 className="text-xl font-semibold">Enter both codes</h1>
        <p className="text-sm text-muted">
          If that account exists, we sent a code to it. Enter it with one of your recovery codes.
        </p>
      </div>
      <Field label="Code from your phone">
        <Input value={otpCode} onChange={(e) => setOtpCode(e.target.value)} inputMode="numeric" autoComplete="one-time-code" />
      </Field>
      <Field label="Recovery code">
        <Input
          value={recoveryCode}
          onChange={(e) => setRecoveryCode(e.target.value)}
          placeholder="a1b2c3d4-e5f6a7b8"
          className="font-mono"
        />
      </Field>
      {error && (
        <p className="text-sm text-danger" role="alert">
          {error}
        </p>
      )}
      <Button className="w-full" disabled={busy || !otpCode.trim() || !recoveryCode.trim()} onClick={redeem}>
        {busy ? "Verifying…" : "Recover account"}
      </Button>
      <p className="text-xs text-muted">
        Recovering signs out every existing session and requires your devices to be set up again.
      </p>
    </div>
  );
}
