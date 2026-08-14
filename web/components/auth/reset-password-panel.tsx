"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { apiErrorMessage, apiErrorStatus } from "@/lib/api";
import { completePasswordReset, startPasswordReset } from "@/lib/auth-api";
import { passwordErrorCopy, passwordIssue } from "@/lib/password";
import { saveSession } from "@/lib/session";
import { Button } from "@/components/ui/button";
import { toE164Identifier } from "@kurx/ui";
import { Field, Input } from "@/components/ui/field";
import { PasswordField } from "@/components/auth/password-field";

/**
 * Forgot-password → reset ceremony (Phase 2C, D-127, INV-B). OTP alone can never reset a password, but
 * the second factor is EITHER a recovery code OR a satisfied step-up — see `PasswordResetService`:
 *
 *     var secondFactor = !string.IsNullOrWhiteSpace(recoveryCode)
 *         ? await recovery.ConsumeAsync(...)          // a code, if one was supplied
 *         : (await stepUp.StatusAsync(...)).Satisfied; // otherwise a device that proved possession
 *
 * This form used to require the recovery code to submit at all, which was a rule the server never had.
 * Since codes are only ever minted by an explicit, step-up-gated call (`POST /v1/auth/recovery-codes`),
 * the overwhelming majority of accounts hold none — so the one screen for a locked-out user refused
 * every one of them before the request was even sent. The field is optional here now; the server
 * decides, and answers `second_factor_required` when neither factor is present.
 *
 * On success the backend re-secures the account (every session and trusted browser is revoked) and
 * returns a fresh session, so this ends signed in.
 */
export function ResetPasswordPanel() {
  const router = useRouter();
  const [step, setStep] = useState<"identify" | "reset" | "done">("identify");
  const [identifier, setIdentifier] = useState("");
  const [otpCode, setOtpCode] = useState("");
  const [recoveryCode, setRecoveryCode] = useState("");
  const [password, setPassword] = useState("");
  const [confirm, setConfirm] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");

  async function sendCode() {
    setError("");
    setBusy(true);
    try {
      // Always succeeds by design — never reveals whether the account exists.
      await startPasswordReset(toE164Identifier(identifier));
      setStep("reset");
    } catch {
      setError("Couldn't start the reset. Please try again.");
    } finally {
      setBusy(false);
    }
  }

  async function complete() {
    setError("");
    const issue = passwordIssue(password);
    if (issue) {
      setError(issue);
      return;
    }
    if (password !== confirm) {
      setError("The two passwords don't match.");
      return;
    }
    setBusy(true);
    try {
      const tokens = await completePasswordReset(
        toE164Identifier(identifier),
        otpCode.trim(),
        password,
        recoveryCode.trim()
      );
      await saveSession(tokens.access_token, tokens.refresh_token);
      setStep("done");
    } catch (err) {
      setError(passwordErrorCopy(apiErrorMessage(err), apiErrorStatus(err)));
    } finally {
      setBusy(false);
    }
  }

  if (step === "done") {
    return (
      <div className="space-y-4 text-center">
        <h1 className="text-xl font-semibold">Password reset</h1>
        <p className="text-sm text-muted">
          Your password is updated and you&apos;re signed in. Every other session was signed out.
        </p>
        <Button
          className="w-full justify-center"
          onClick={() => {
            router.push("/discover");
            router.refresh();
          }}
        >
          Continue to Kurx
        </Button>
      </div>
    );
  }

  if (step === "identify") {
    return (
      <div className="space-y-4">
        <div>
          <h1 className="text-xl font-semibold">Reset your password</h1>
          <p className="text-sm text-muted">
            We&apos;ll text a code to your registered number. You&apos;ll also need a second factor — a saved
            recovery code, or a device you&apos;ve already verified.
          </p>
        </div>
        <Field label="Phone, email, or username">
          <Input
            value={identifier}
            onChange={(event) => setIdentifier(event.target.value)}
            placeholder="Email, or phone including your country code"
            autoComplete="username"
          />
        </Field>
        {error ? <p className="text-sm text-danger" role="alert">{error}</p> : null}
        <Button className="w-full justify-center" disabled={busy || identifier.trim().length < 3} onClick={sendCode}>
          {busy ? "Sending…" : "Send code"}
        </Button>
      </div>
    );
  }

  return (
    <div className="space-y-4">
      <div>
        <h1 className="text-xl font-semibold">Enter your codes and a new password</h1>
        <p className="text-sm text-muted">
          If that account exists, we sent a code to it. A code alone can never reset a password — add a recovery
          code below, or reset from a device you&apos;ve already verified.
        </p>
      </div>
      <Field label="Code from your phone">
        <Input value={otpCode} onChange={(event) => setOtpCode(event.target.value)} inputMode="numeric" autoComplete="one-time-code" />
      </Field>
      {/* Optional, and labelled so — the server accepts a satisfied step-up instead. Leaving it
          required here is what made this screen unusable for an account that never minted codes. */}
      <Field label="Recovery code (optional)" helper="Leave blank if you're resetting from a device you've already verified.">
        <Input
          value={recoveryCode}
          onChange={(event) => setRecoveryCode(event.target.value)}
          placeholder="a1b2c3d4-e5f6a7b8"
          className="font-mono"
        />
      </Field>
      <PasswordField label="New password" value={password} onChange={setPassword} autoComplete="new-password" showStrength />
      <PasswordField label="Confirm new password" value={confirm} onChange={setConfirm} autoComplete="new-password" onEnter={complete} />
      {error ? <p className="text-sm text-danger" role="alert">{error}</p> : null}
      <Button
        className="w-full justify-center"
        disabled={busy || !otpCode.trim() || !password || !confirm}
        onClick={complete}
      >
        {busy ? "Resetting…" : "Reset password"}
      </Button>
      <p className="text-xs text-muted">Resetting signs out every existing session.</p>
    </div>
  );
}
