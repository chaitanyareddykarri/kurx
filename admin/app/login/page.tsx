"use client";

import { Suspense, useEffect, useRef, useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { ShieldCheck } from "lucide-react";
import { Button, Spinner, PhoneField, toE164Identifier } from "@kurx/ui";
import { KurxAdminLogo } from "@/components/brand/logo";
import {
  requestOtpAction,
  verifyOtpAction,
  loginPasswordAction,
  pollDeviceLoginAction,
  sendSecondFactorCodeAction,
  verifySecondFactorCodeAction,
  type SecondFactorMethod
} from "@/lib/auth-actions";

// `border-strong`, not `border`: WCAG 1.4.11 governs a boundary that identifies a control, and the
// decorative token is 1.35:1 against `bg-background`. Same string in password-field.tsx and
// reset-password.tsx — the whole admin auth flow carried the defect (D-288).
const inputCls =
  "h-11 w-full rounded-md border border-border-strong bg-background px-3 text-sm text-text placeholder:text-muted focus:border-accent focus:outline-none";

type Approval = { challengeId: string; pollToken: string; matchNumber: number; expiresAt: string };
type SecondFactorChallenge = { challengeId: string; pollToken: string; methods: SecondFactorMethod[] };

/**
 * `subtitle` is optional, and the first screen deliberately has none.
 *
 * It read "Kurx staff sign-in.", which described how access is obtained to someone who, by
 * definition, does not have it — a platform role is granted internally and never requested through
 * this form. "Internal console" already says what this is; the second line only invited a request
 * there is no channel for. The step screens below keep their subtitles, which say what to DO.
 */
function Shell({ subtitle, children }: { subtitle?: string; children: React.ReactNode }) {
  return (
    <div className="w-full max-w-sm rounded-lg border border-border bg-surface p-6 shadow-github">
      <div className="mb-6 flex flex-col items-center gap-3 text-center">
        <KurxAdminLogo />
        <div>
          <h1 className="text-lg font-semibold text-text">Internal console</h1>
          {subtitle ? <p className="mt-1 text-sm text-muted">{subtitle}</p> : null}
        </div>
      </div>
      {children}
    </div>
  );
}

function ErrorLine({ message }: { message: string }) {
  return (
    <p role="alert" className="mt-3 flex items-center gap-1.5 text-sm text-danger">
      <ShieldCheck size={14} /> {message}
    </p>
  );
}

function loginErrorCopy(code: string, status?: number): string {
  switch (code) {
    case "invalid_credentials":
      return "Incorrect phone/email/username or password.";
    case "account_locked":
      return "Too many attempts — this account is locked for a few minutes.";
    case "no_second_factor":
      // Not "…with a code below" any more: there is no control below (D-320). This branch flips the
      // form to `OtpForm` itself, so the copy must describe what just happened, not point at a button.
      return "Password is correct, but this browser isn't trusted and there's no device to approve. Sending you a one-time code instead.";
    default:
      return status === 423 ? "This account is temporarily locked." : code || "Couldn't sign you in.";
  }
}

function LoginForm() {
  const router = useRouter();
  const params = useSearchParams();
  const next = params.get("next") || "/";

  const [mode, setMode] = useState<"password" | "otp">("password");
  const [identifier, setIdentifier] = useState("");
  const [password, setPassword] = useState("");
  const [remember, setRemember] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [approval, setApproval] = useState<Approval | null>(null);
  const [secondFactor, setSecondFactor] = useState<SecondFactorChallenge | null>(null);

  async function signIn(event: React.FormEvent) {
    event.preventDefault();
    setError(null);
    setBusy(true);
    const res = await loginPasswordAction(toE164Identifier(identifier), password, remember);
    setBusy(false);
    if (res.ok && res.outcome === "session") {
      router.replace(next);
      return;
    }
    if (res.ok && res.outcome === "device_approval") {
      setApproval({
        challengeId: res.challengeId,
        pollToken: res.pollToken,
        matchNumber: res.matchNumber,
        expiresAt: res.expiresAt
      });
      return;
    }
    if (res.ok && res.outcome === "second_factor") {
      setSecondFactor({
        challengeId: res.challengeId,
        pollToken: res.pollToken,
        methods: res.methods
      });
      return;
    }
    setError(loginErrorCopy(res.error, res.status));
    if (res.error === "no_second_factor") setMode("otp");
  }

  if (approval) {
    return <ApprovalWaiting approval={approval} onCancel={() => setApproval(null)} onDone={() => router.replace(next)} />;
  }
  if (secondFactor) {
    return (
      <SecondFactorForm
        challenge={secondFactor}
        onCancel={() => setSecondFactor(null)}
        onDone={() => router.replace(next)}
      />
    );
  }

  if (mode === "otp") {
    return <OtpForm initialPhone={identifier} next={next} onBack={() => setMode("password")} />;
  }

  return (
    <Shell>
      <form onSubmit={signIn} className="space-y-3">
        <input
          aria-label="Phone, email, or username"
          autoComplete="username"
          required
          value={identifier}
          onChange={(event) => setIdentifier(event.target.value)}
          placeholder="Phone, email, or username"
          className={inputCls}
        />
        <input
          aria-label="Password"
          type="password"
          autoComplete="current-password"
          required
          value={password}
          onChange={(event) => setPassword(event.target.value)}
          placeholder="Password"
          className={inputCls}
        />
        <label className="flex items-center gap-2 text-sm text-muted">
          <input type="checkbox" checked={remember} onChange={(event) => setRemember(event.target.checked)} />
          Remember this browser
        </label>
        <Button type="submit" disabled={busy} className="w-full">
          {busy ? <Spinner size={16} className="text-white" /> : null}
          Sign in
        </Button>
      </form>
      {/*
        No manual "sign in with a code" control (D-320): offered beside the password field it is a
        one-tap password bypass, and every use of it mints and delivers an SMS. The console still
        falls back to `OtpForm` automatically on `no_second_factor` above — that is a lockout guard
        for a staff account with nothing else, not a shortcut anyone can choose.
      */}
      <div className="mt-3 flex items-center justify-end text-xs text-muted">
        <a href="/reset" className="hover:text-text">
          Forgot password?
        </a>
      </div>
      {error ? <ErrorLine message={error} /> : null}
    </Shell>
  );
}

/**
 * Second-factor chooser (D-280 / D-283). Renders exactly the methods the backend returned, in its order —
 * the console has no hardcoded method list and no local ranking, which is what keeps it consistent with
 * web and mobile.
 */
function SecondFactorForm({
  challenge,
  onCancel,
  onDone
}: {
  challenge: SecondFactorChallenge;
  onCancel: () => void;
  onDone: () => void;
}) {
  const [chosen, setChosen] = useState<SecondFactorMethod | null>(null);
  const [code, setCode] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const codeMethods = challenge.methods.filter(
    (m) => m.method === "sms_otp" || m.method === "email_otp"
  );
  const otherMethods = challenge.methods.filter(
    (m) => m.method !== "sms_otp" && m.method !== "email_otp"
  );

  async function choose(method: SecondFactorMethod) {
    setError(null);
    setBusy(true);
    const res = await sendSecondFactorCodeAction(challenge.challengeId, challenge.pollToken, method.method);
    setBusy(false);
    if (!res.ok) {
      setError("Couldn't send a code just now. Try again, or pick another method.");
      return;
    }
    setChosen(method);
  }

  async function verify(event: React.FormEvent) {
    event.preventDefault();
    setError(null);
    setBusy(true);
    const res = await verifySecondFactorCodeAction(challenge.challengeId, challenge.pollToken, code);
    setBusy(false);
    if (res.ok) {
      onDone();
      return;
    }
    setError(res.error || "That code didn't work. Check the digits, or request a new one.");
  }

  if (chosen) {
    return (
      <Shell subtitle={`Enter the code we sent to ${chosen.hint ?? "you"}.`}>
        <form onSubmit={verify} className="space-y-3">
          <input
            aria-label="One-time code"
            inputMode="numeric"
            maxLength={6}
            autoComplete="one-time-code"
            required
            value={code}
            onChange={(event) => setCode(event.target.value.replace(/\D/g, "").slice(0, 6))}
            placeholder="6-digit code"
            className={inputCls}
          />
          <Button type="submit" disabled={busy || code.length !== 6} className="w-full">
            {busy ? <Spinner size={16} className="text-white" /> : null}
            Verify and sign in
          </Button>
        </form>
        {error ? <ErrorLine message={error} /> : null}
        <button
          type="button"
          onClick={() => {
            setChosen(null);
            setCode("");
            setError(null);
          }}
          className="mt-3 text-xs text-muted hover:text-text"
        >
          Use a different method
        </button>
      </Shell>
    );
  }

  return (
    <Shell subtitle="Confirm it's you to finish signing in.">
      <div className="space-y-2">
        {codeMethods.map((method) => (
          <Button
            key={method.method}
            type="button"
            onClick={() => choose(method)}
            disabled={busy}
            className="w-full"
          >
            {method.label}
            {method.hint ? ` (${method.hint})` : ""}
          </Button>
        ))}
      </div>
      {otherMethods.length > 0 ? (
        <p className="mt-3 text-xs text-muted">
          Also on this account: {otherMethods.map((m) => m.label).join(", ")}.
        </p>
      ) : null}
      {error ? <ErrorLine message={error} /> : null}
      <button type="button" onClick={onCancel} className="mt-3 text-xs text-muted hover:text-text">
        Back to sign in
      </button>
    </Shell>
  );
}

/** Device-approval waiting screen: shows the match number the user enters on their phone, and polls the
 *  status server-side (which persists the session + confirms staff on approval). */
function ApprovalWaiting({ approval, onCancel, onDone }: { approval: Approval; onCancel: () => void; onDone: () => void }) {
  const [status, setStatus] = useState<string>("pending");
  const [error, setError] = useState<string | null>(null);
  const settled = useRef(false);

  useEffect(() => {
    const timer = setInterval(async () => {
      if (settled.current) return;
      const res = await pollDeviceLoginAction(approval.challengeId, approval.pollToken);
      if (res.status === "approved") {
        settled.current = true;
        if (res.ok) onDone();
        else {
          setError(res.error ?? "This account doesn't have Kurx admin access.");
          setStatus("error");
        }
      } else if (res.status !== "pending") {
        settled.current = true;
        setStatus(res.status);
      }
    }, 2000);
    return () => clearInterval(timer);
  }, [approval, onDone]);

  return (
    <Shell subtitle="Approve this sign-in on your phone.">
      <p className="text-sm text-muted">Open Kurx on your phone and enter this number to confirm it&apos;s you:</p>
      <div
        className="mx-auto my-4 flex h-16 w-16 items-center justify-center rounded-2xl border-2 border-border text-2xl font-bold tabular-nums text-text"
        aria-label={`Match number ${approval.matchNumber}`}
      >
        {approval.matchNumber}
      </div>
      {status === "pending" ? (
        <p className="text-center text-xs text-muted" role="status" aria-live="polite">
          Waiting for approval…
        </p>
      ) : (
        <p className="text-center text-sm text-danger">
          {status === "rejected"
            ? "Sign-in was declined on your device."
            : status === "expired"
              ? "This sign-in request expired."
              : (error ?? "This sign-in request was already used.")}
        </p>
      )}
      <button type="button" onClick={onCancel} className="mt-3 w-full text-center text-xs text-muted hover:text-text">
        Cancel
      </button>
    </Shell>
  );
}

/** One-time-code sign-in — the bootstrap path for staff accounts without a password yet. */
function OtpForm({ initialPhone, next, onBack }: { initialPhone: string; next: string; onBack: () => void }) {
  const router = useRouter();
  const [step, setStep] = useState<"phone" | "code">("phone");
  const [phone, setPhone] = useState(() => {
    const seed = initialPhone.trim();
    return /^[+\d()\-\s]+$/.test(seed) ? toE164Identifier(seed) : "";
  });
  const [phoneValid, setPhoneValid] = useState(false);
  const [code, setCode] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function sendCode(event: React.FormEvent) {
    event.preventDefault();
    setError(null);
    setBusy(true);
    const res = await requestOtpAction(phone);
    setBusy(false);
    if (res.ok) setStep("code");
    else setError(res.error);
  }

  async function verify(event: React.FormEvent) {
    event.preventDefault();
    setError(null);
    setBusy(true);
    const res = await verifyOtpAction(phone, code);
    setBusy(false);
    if (res.ok) router.replace(next);
    else setError(res.error);
  }

  return (
    <Shell subtitle="Sign in with a one-time code.">
      {step === "phone" ? (
        <form onSubmit={sendCode} className="space-y-3">
          <PhoneField
            label="Phone number"
            value={phone}
            onChange={(e164, valid) => {
              setPhone(e164);
              setPhoneValid(valid);
            }}
          />
          <Button type="submit" disabled={busy || !phoneValid} className="w-full">
            {busy ? <Spinner size={16} className="text-white" /> : null}
            Send code
          </Button>
        </form>
      ) : (
        <form onSubmit={verify} className="space-y-3">
          <label htmlFor="code" className="block text-sm text-muted">
            Verification code
            <input
              id="code"
              inputMode="numeric"
              autoComplete="one-time-code"
              required
              value={code}
              onChange={(event) => setCode(event.target.value)}
              placeholder="6-digit code"
              className={`mt-1 ${inputCls}`}
            />
          </label>
          <Button type="submit" disabled={busy} className="w-full">
            {busy ? <Spinner size={16} className="text-white" /> : null}
            Verify &amp; enter
          </Button>
        </form>
      )}
      <button type="button" onClick={onBack} className="mt-3 w-full text-center text-xs text-muted hover:text-text">
        Back to password sign-in
      </button>
      {error ? <ErrorLine message={error} /> : null}
    </Shell>
  );
}

export default function LoginPage() {
  return (
    <div className="grid min-h-screen place-items-center bg-background p-4">
      <Suspense fallback={<Spinner />}>
        <LoginForm />
      </Suspense>
    </div>
  );
}
