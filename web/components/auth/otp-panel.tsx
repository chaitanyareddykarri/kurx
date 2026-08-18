"use client";

import { useState, useTransition } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { getMe } from "@/lib/api";
import { loginPassword } from "@/lib/auth-api";
import { saveSession } from "@/lib/session";
import { Button, toE164Identifier } from "@kurx/ui";
import { Field, Input } from "@/components/ui/field";
import { PasskeySignIn } from "@/components/auth/passkey-sign-in";
import { LoginWaiting } from "@/components/auth/login-waiting";
import { SecondFactorPanel } from "@/components/auth/second-factor-panel";
import type { SecondFactorMethod } from "@/lib/auth-api";

type DeviceApproval = { challengeId: string; pollToken: string; matchNumber: number; expiresAt: string };
type SecondFactor = { challengeId: string; pollToken: string; methods: SecondFactorMethod[] };

/**
 * Password-first sign-in (Phase 2B / D-182). Factor 1 is the password; factor 2 is a trusted-browser
 * cookie (→ session immediately), a trusted-device approval (→ waiting screen), or whichever factors the
 * backend says the account actually holds (→ `SecondFactorPanel`, D-283).
 *
 * **There is no one-time-code option on this screen (D-320).** A code is still how an account is created
 * (`/register`) and still how the backend may satisfy factor 2 after a correct password — but it is not a
 * way to skip the password, because offering it here meant nobody ever typed one and every sign-in cost
 * an SMS.
 */
export function OtpPanel() {
  const router = useRouter();
  const [identifier, setIdentifier] = useState("");
  const [password, setPassword] = useState("");
  const [remember, setRemember] = useState(false);
  const [error, setError] = useState("");
  const [pending, startTransition] = useTransition();
  const [approval, setApproval] = useState<DeviceApproval | null>(null);
  const [secondFactor, setSecondFactor] = useState<SecondFactor | null>(null);

  async function afterSession(access: string, refresh: string) {
    await saveSession(access, refresh);
    const me = await getMe(access);
    router.push(me.needs_onboarding ? "/register" : "/discover");
    router.refresh();
  }

  // The waiting screen owns the whole panel while a device approval is in flight — leaving the form
  // visible would invite a second, competing sign-in.
  if (approval) {
    return (
      <div id="login" className="scroll-mt-20 rounded-lg border border-border bg-surface p-5">
        <h2 className="text-h3 text-text">Approve on your phone</h2>
        <div className="mt-4">
          <LoginWaiting
            challengeId={approval.challengeId}
            pollToken={approval.pollToken}
            matchNumber={approval.matchNumber}
            expiresAt={approval.expiresAt}
            onCancel={() => setApproval(null)}
          />
        </div>
      </div>
    );
  }

  // The account has no trusted device (or is on a surface that prefers a code): the backend told us which
  // factors it actually has, and this panel renders exactly that list (D-283).
  if (secondFactor) {
    return (
      <div id="login" className="scroll-mt-20 rounded-lg border border-border bg-surface p-5">
        <h2 className="text-h3 text-text">One more step</h2>
        <div className="mt-4">
          <SecondFactorPanel
            challengeId={secondFactor.challengeId}
            pollToken={secondFactor.pollToken}
            methods={secondFactor.methods}
            onSession={afterSession}
            onBack={() => setSecondFactor(null)}
          />
        </div>
      </div>
    );
  }

  function signIn() {
    setError("");
    startTransition(async () => {
      const result = await loginPassword(toE164Identifier(identifier), password, remember);
      switch (result.outcome) {
        case "session":
          await afterSession(result.access_token, result.refresh_token);
          break;
        case "device_approval":
          setApproval({
            challengeId: result.challenge_id,
            pollToken: result.poll_token,
            matchNumber: result.match_number,
            expiresAt: result.expires_at
          });
          break;
        case "second_factor":
          setSecondFactor({
            challengeId: result.challenge_id,
            pollToken: result.poll_token,
            methods: result.methods
          });
          break;
        case "error":
          setError(loginErrorCopy(result.error, result.status));
          break;
      }
    });
  }

  return (
    <div id="login" className="scroll-mt-20 rounded-lg border border-border bg-surface p-5">
      <h2 className="text-h3 text-text">Sign in</h2>
      <div className="mt-5 space-y-4">
        {/*
          `Field` + `Input`, not the local `inputCls` this used to hand-roll.
          
          Both fields carried a `placeholder` and an `aria-label` and no visible `<label>`, so the
          name of each control vanished the moment somebody typed into it. `test/auth-otp.test.tsx`
          already asserts the rule — "has a real label, not a placeholder standing in for one" — but
          it asserts it against the shared primitive, so the sign-in screen was free to keep the
          defect while the rule stayed green. `field.tsx` was written to end exactly this pattern;
          its own comment counts the twenty copied `inputClass` constants it replaced, and this was
          the twenty-first. Using it also brings `aria-describedby`, `aria-invalid` and the focus
          ring for free (D-384 §3).
        */}
        <Field label="Phone, email, or username">
          <Input
            value={identifier}
            onChange={(event) => setIdentifier(event.target.value)}
            autoComplete="username"
          />
        </Field>
        <Field label="Password">
          <Input
            value={password}
            onChange={(event) => setPassword(event.target.value)}
            onKeyDown={(event) => {
              if (event.key === "Enter" && identifier && password && !pending) signIn();
            }}
            type="password"
            autoComplete="current-password"
          />
        </Field>

        {/*
          The checkbox was a bare native control — roughly 13px, against a 44px floor, and the only
          thing on this card the design system did not draw. The input keeps its native semantics and
          keyboard behaviour; the label around it supplies the target size, so the whole row is the
          hit area rather than a 13px square beside some text.
        */}
        <label className="-mx-2 flex min-h-11 cursor-pointer select-none items-center gap-2.5 rounded-md px-2 text-body text-muted transition-colors duration-fast hover:text-text">
          <input
            type="checkbox"
            checked={remember}
            onChange={(event) => setRemember(event.target.checked)}
            className="h-4 w-4 shrink-0 cursor-pointer accent-accent"
          />
          Remember this browser
        </label>

        <Button type="button" onClick={signIn} disabled={pending || !identifier || !password} className="w-full justify-center">
          {pending ? "Signing in…" : "Sign in"}
        </Button>
        {error ? (
          <p role="alert" className="text-body text-danger">
            {error}
          </p>
        ) : null}

        {/* Passkey — phishing-resistant same-device sign-in (AM3/D-086). Needs an identifier to resolve. */}
        {identifier.trim().length > 2 ? <PasskeySignIn identifier={toE164Identifier(identifier)} /> : null}

        {/*
          A 20px-tall target on the sign-in panel — a standalone control, not a link inside a
          sentence, so the 44px floor `regression-criteria.md` §2.5 sets below 768px applies.
          Verified at 360 and 414 in a browser during Phase 25.

          Its former neighbour, "First time? Sign in with a code", is gone (D-320). Beside a password
          field it was a one-tap password bypass, and since every use mints and delivers a one-time
          code, the cheap path was also the one that cost money on every single sign-in. Signing up
          still runs on a code — through /register below, where it is unavoidable — and a returning
          user who cannot use their password goes through "Forgot password?".
        */}
        <div className="flex flex-wrap items-center justify-end gap-2 text-body text-muted">
          <Link
            href="/reset"
            className="inline-flex min-h-11 items-center rounded-md underline underline-offset-2 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
          >
            Forgot password?
          </Link>
        </div>

        {/*
          `Link`, not `<a href>`. Both of this card's internal destinations were full document
          navigations — a white flash and a fresh download of the app shell to reach two routes that
          are already in the bundle.
        */}
        <p className="border-t border-border pt-4 text-body text-muted">
          New to Kurx?{" "}
          <Link href="/register" className="font-medium text-accent-text underline underline-offset-2">
            Create an account
          </Link>
        </p>
      </div>
    </div>
  );
}

function loginErrorCopy(code: string, status: number): string {
  switch (code) {
    case "invalid_credentials":
      return "Incorrect phone/email/username or password.";
    case "account_locked":
      return "Too many attempts — this account is locked for a few minutes. Try again shortly.";
    case "no_second_factor":
      // Reachable only when the account holds nothing at all — no phone, no verified email, no
      // device, no passkey, no recovery code. With any one of those the backend offers it instead
      // (D-280/D-283) and `SecondFactorPanel` renders it, so this must not point at a control the
      // panel no longer has. Matches the app's copy word for word.
      return "Your password is correct, but there's no way to confirm it's you on this account yet. Add a phone number or verify your email to finish signing in.";
    default:
      return status === 423
        ? "This account is temporarily locked. Try again shortly."
        : "Couldn't sign you in. Check your details and try again.";
  }
}

// `OtpFallback` lived here and is deleted with its trigger (D-320). Signing in by code is not gone
// from the product — it is the whole of `/register`'s first step, where an account does not exist
// yet and no other factor can — but it is no longer reachable from the password screen.
