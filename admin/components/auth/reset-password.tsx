"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { ShieldCheck } from "lucide-react";
import { Button, Spinner, toE164Identifier } from "@kurx/ui";
import { KurxAdminLogo } from "@/components/brand/logo";
import { completePasswordResetAction, startPasswordResetAction } from "@/lib/password-actions";
import { passwordErrorCopy, passwordIssue } from "@/lib/password";
import { PasswordField } from "@/components/auth/password-field";

// `border-strong` identifies the control (WCAG 1.4.11); `border` is decorative at 1.35:1 (D-288).
const inputCls =
  "h-11 w-full rounded-md border border-border-strong bg-background px-3 text-sm text-text placeholder:text-muted focus:border-accent focus:outline-none";

/**
 * Staff forgot-password → reset (Phase 2C, D-127, INV-B). Anonymous. OTP alone can never reset a
 * password, but the second factor is EITHER a recovery code OR a satisfied step-up — `PasswordResetService`
 * falls back to `stepUp.StatusAsync(...).Satisfied` whenever no code is supplied. This form required the
 * code to submit, a rule the server never had, so a staff account that never minted codes could not
 * reset at all. Optional here now; the server answers `second_factor_required` if neither factor holds.
 *
 * On success the account is confirmed staff and the console session is kept; a non-staff account is
 * signed straight back out.
 */
export function ResetPassword() {
  const router = useRouter();
  const [step, setStep] = useState<"identify" | "reset">("identify");
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
    const res = await startPasswordResetAction(toE164Identifier(identifier));
    setBusy(false);
    if (res.ok) setStep("reset");
    else setError("Couldn't start the reset. Please try again.");
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
    const res = await completePasswordResetAction(toE164Identifier(identifier), otpCode, password, recoveryCode);
    setBusy(false);
    if (res.ok) {
      router.replace("/");
      return;
    }
    setError(res.error === "not_staff" ? "This account doesn't have Kurx admin access." : passwordErrorCopy(res.error, res.status));
  }

  return (
    <div className="w-full max-w-sm rounded-lg border border-border bg-surface p-6 shadow-github">
      <div className="mb-6 flex flex-col items-center gap-3 text-center">
        <KurxAdminLogo />
        <div>
          <h1 className="text-lg font-semibold text-text">Reset your password</h1>
          <p className="mt-1 text-sm text-muted">
            {step === "identify"
              ? "We'll text a code to your registered number. You'll also need a second factor — a recovery code, or a device you've already verified."
              : "Enter the code and a new password. Add a recovery code, or reset from a device you've already verified."}
          </p>
        </div>
      </div>

      {step === "identify" ? (
        <div className="space-y-3">
          <input
            aria-label="Phone, email, or username"
            autoComplete="username"
            value={identifier}
            onChange={(event) => setIdentifier(event.target.value)}
            placeholder="Phone, email, or username"
            className={inputCls}
          />
          <Button onClick={sendCode} disabled={busy || identifier.trim().length < 3} className="w-full">
            {busy ? <Spinner size={16} className="text-white" /> : null}
            Send code
          </Button>
        </div>
      ) : (
        <div className="space-y-3">
          <input
            aria-label="Code from your phone"
            inputMode="numeric"
            autoComplete="one-time-code"
            value={otpCode}
            onChange={(event) => setOtpCode(event.target.value)}
            placeholder="Code from your phone"
            className={inputCls}
          />
          {/* Optional — the server takes a satisfied step-up instead. */}
          <input
            aria-label="Recovery code (optional)"
            value={recoveryCode}
            onChange={(event) => setRecoveryCode(event.target.value)}
            placeholder="Recovery code — optional"
            className={`${inputCls} font-mono`}
          />
          <PasswordField label="New password" value={password} onChange={setPassword} autoComplete="new-password" showStrength />
          <PasswordField label="Confirm new password" value={confirm} onChange={setConfirm} autoComplete="new-password" onEnter={complete} />
          <Button
            onClick={complete}
            disabled={busy || !otpCode.trim() || !password || !confirm}
            className="w-full"
          >
            {busy ? <Spinner size={16} className="text-white" /> : null}
            Reset password
          </Button>
        </div>
      )}

      {error ? (
        <p role="alert" className="mt-3 flex items-center gap-1.5 text-sm text-danger">
          <ShieldCheck size={14} /> {error}
        </p>
      ) : null}

      <button
        type="button"
        onClick={() => router.replace("/login")}
        className="mt-3 w-full text-center text-xs text-muted hover:text-text"
      >
        Back to sign in
      </button>
    </div>
  );
}
