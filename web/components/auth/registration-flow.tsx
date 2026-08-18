"use client";

import { useEffect, useState, useTransition } from "react";
import { useRouter } from "next/navigation";
import { Check } from "lucide-react";
import { Field, Input } from "@/components/ui/field";
import { Button } from "@/components/ui/button";
import { PhoneField } from "@kurx/ui";
import { OnboardingForm } from "@/components/auth/onboarding-form";
import {
  registrationStatusAction,
  sendSignupOtpAction,
  verifySignupOtpAction,
  startEmailVerificationAction,
  completeEmailVerificationAction,
  setPasswordAction,
  passwordPolicyAction
} from "@/lib/registration-actions";
import type { RegistrationStatus } from "@/lib/api";

type Step = "signup" | "profile" | "password" | "email" | "success";

/**
 * Registration ceremony wizard (Phase 2D). One route, distinct screens, driven by
 * `/registration/status`: phone-OTP signup (creates the account + session) → complete profile →
 * set a password → verify email → success. Phone is verified implicitly by the OTP signup, so it
 * renders as a completed step.
 *
 * Order matches the server's `remaining` list (D-311): the two steps that gate `needs_onboarding`
 * come first, then the skippable one. A password used to be an optional follow-up advertised on the
 * success screen — but the server now counts an account without one as unfinished, so leaving it out
 * produced a web account that mobile immediately pushed back into onboarding.
 */
export function RegistrationFlow({ initialStatus }: { initialStatus: RegistrationStatus | null }) {
  const [status, setStatus] = useState<RegistrationStatus | null>(initialStatus);
  const [step, setStep] = useState<Step>(initialStatus ? nextStep(initialStatus) : "signup");

  async function refreshAndAdvance() {
    const res = await registrationStatusAction();
    if (res.ok) {
      setStatus(res.status);
      setStep(nextStep(res.status));
    }
  }

  return (
    <div className="space-y-5">
      {status ? <Checklist status={status} current={step} /> : null}

      {step === "signup" ? (
        <SignupStep onSignedUp={refreshAndAdvance} />
      ) : step === "profile" ? (
        <ProfileStep onDone={refreshAndAdvance} />
      ) : step === "password" ? (
        <PasswordStep onDone={refreshAndAdvance} />
      ) : step === "email" ? (
        <EmailStep
          initialEmail={status?.email ?? ""}
          onDone={refreshAndAdvance}
          // Email is the last step and the only skippable one, so skipping finishes. It previously
          // jumped back to profile, which is now behind the user by the time this renders.
          onSkip={() => setStep("success")}
        />
      ) : (
        <SuccessStep status={status} />
      )}
    </div>
  );
}

function nextStep(s: RegistrationStatus): Step {
  if (s.remaining.includes("complete_profile")) return "profile";
  if (s.remaining.includes("create_password")) return "password";
  if (s.remaining.includes("verify_email")) return "email";
  return "success";
}

/** The persistent "registration status" panel — the checklist the ceremony walks through. */
function Checklist({ status, current }: { status: RegistrationStatus; current: Step }) {
  // Same order as the steps and as the server's `remaining` list. "Profile complete" reads the
  // step's own flag rather than needs_onboarding, which now also covers the password.
  const items = [
    { label: "Phone verified", done: status.phone_verified, active: false },
    {
      label: "Profile complete",
      done: !status.remaining.includes("complete_profile"),
      active: current === "profile"
    },
    { label: "Password set", done: status.has_password, active: current === "password" },
    { label: "Email verified", done: status.email_verified, active: current === "email" }
  ];
  return (
    // Named, because "list of 3 items" tells a screen-reader user nothing about
    // what the list is for.
    <ol aria-label="Registration progress" className="space-y-2 rounded-lg border border-border bg-surface p-4">
      {items.map((item) => (
        <li key={item.label} className="flex items-center gap-2.5 text-body">
          <span
            className={`flex h-5 w-5 items-center justify-center rounded-full border text-micro ${
              item.done
                ? "border-accent bg-accent text-on-accent"
                : item.active
                  ? "border-accent text-accent-text"
                  : "border-border-strong text-muted"
            }`}
            aria-hidden="true"
          >
            {item.done ? <Check size={12} /> : null}
          </span>
          {/*
            State was carried by the marker's colour and a line-through, with the
            marker aria-hidden — so a screen reader heard three bare labels and
            no indication of which were done or which was current. The whole
            point of a progress checklist was invisible.
          */}
          <span className={item.done ? "text-muted line-through" : item.active ? "font-medium text-text" : "text-text"}>
            <span className="sr-only">
              {item.done ? "Done: " : item.active ? "Current step: " : "Not started: "}
            </span>
            {item.label}
          </span>
        </li>
      ))}
    </ol>
  );
}

function Panel({ title, subtitle, children }: { title: string; subtitle?: string; children: React.ReactNode }) {
  return (
    <div className="space-y-4 rounded-lg border border-border bg-surface p-5">
      <div className="space-y-1">
        <h2 className="text-h3 text-text">{title}</h2>
        {subtitle ? <p className="text-body text-muted">{subtitle}</p> : null}
      </div>
      {children}
    </div>
  );
}

/** Step 1 — phone-OTP signup. Creates the account (if new) and mints the session. */
function SignupStep({ onSignedUp }: { onSignedUp: () => Promise<void> }) {
  const [phone, setPhone] = useState("");
  const [phoneValid, setPhoneValid] = useState(false);
  const [code, setCode] = useState("");
  const [stage, setStage] = useState<"phone" | "code">("phone");
  const [error, setError] = useState("");
  const [pending, start] = useTransition();

  function send() {
    setError("");
    start(async () => {
      const res = await sendSignupOtpAction(phone);
      if (res.ok) setStage("code");
      else setError("Couldn't send a code. Check the number and try again.");
    });
  }

  function verify() {
    setError("");
    start(async () => {
      const res = await verifySignupOtpAction(phone, code);
      if (res.ok) await onSignedUp();
      else setError("That code didn't work. Request a new one and try again.");
    });
  }

  return (
    /*
      "Verify your phone", not "Create your account". Every other panel in this wizard names its own
      step — "Verify your email", "You're all set!" — and this one named the whole ceremony, which is
      what the page's `h1` already says. Side by side in the two-column shell (D-384) that read as the
      same sentence printed twice. It also matches the checklist above it, which calls this step
      "Phone verified".
    */
    <Panel title="Verify your phone" subtitle="We'll send a one-time code to your phone to get started.">
      <div className="space-y-3">
        {/*
          `label`, like every other `PhoneField` call site in the app. Without it the control's only
          name was its placeholder, which disappears the moment somebody types — the same defect the
          sign-in fields carried (D-384 §3), on the very first field of signup.
        */}
        <PhoneField
          label="Phone number"
          value={phone}
          onChange={(e164, valid) => {
            setPhone(e164);
            setPhoneValid(valid);
          }}
          disabled={stage === "code"}
          onEnter={stage === "phone" && phoneValid ? send : undefined}
        />
        {stage === "code" ? (
          <Field label="Verification code" helper={`Sent to ${phone}`}>
            {/*
              `autoComplete="one-time-code"` is what makes iOS and Android offer the code from the
              SMS they just received. `test/auth-otp.test.tsx` spells out why it matters — "without
              it every user retypes the code by hand on the highest-friction step" — but asserts it
              against the shared primitive, so this field, the one real signup actually uses, never
              declared it (D-384 §3).

              The `aria-label="One-time code"` that used to sit here is gone. `aria-label` overrides
              the `<label>` `Field` renders, so the control was announced as "One-time code" while
              the words above it read "Verification code" — a name a speech-input user cannot say and
              a screen-reader user cannot match to the screen (WCAG 2.5.3). The visible label is now
              the accessible name, which is the whole contract `Field` exists to provide.
            */}
            <Input
              value={code}
              onChange={(event) => setCode(event.target.value.replace(/\D/g, "").slice(0, 6))}
              inputMode="numeric"
              autoComplete="one-time-code"
              maxLength={6}
              placeholder="6-digit code"
            />
          </Field>
        ) : null}
        <Button
          type="button"
          onClick={stage === "phone" ? send : verify}
          disabled={pending || (stage === "phone" ? !phoneValid : code.length !== 6)}
          className="w-full justify-center"
        >
          {pending ? "Please wait…" : stage === "phone" ? "Send code" : "Verify & continue"}
        </Button>
        {stage === "code" ? (
          <button type="button" className="w-full text-center text-sm text-muted underline" onClick={() => setStage("phone")}>
            Change number
          </button>
        ) : null}
        {error ? <p role="alert" className="text-sm text-danger">{error}</p> : null}
      </div>
    </Panel>
  );
}

/** Step 2 — verify email (recovery + notifications channel; never an auth factor). Skippable. */
function EmailStep({
  initialEmail,
  onDone,
  onSkip
}: {
  initialEmail: string;
  onDone: () => Promise<void>;
  onSkip: () => void;
}) {
  const [email, setEmail] = useState(initialEmail);
  const [code, setCode] = useState("");
  const [stage, setStage] = useState<"email" | "code">("email");
  const [error, setError] = useState("");
  const [pending, start] = useTransition();

  function send() {
    setError("");
    start(async () => {
      const res = await startEmailVerificationAction(email);
      if (res.ok) setStage("code");
      else setError(emailErrorCopy(res.error));
    });
  }

  function verify() {
    setError("");
    start(async () => {
      const res = await completeEmailVerificationAction(email, code);
      if (res.ok) await onDone();
      else setError(emailErrorCopy(res.error));
    });
  }

  return (
    <Panel title="Verify your email" subtitle="Used to recover your account and for important notifications.">
      <div className="space-y-3">
        <Field label="Email">
          <Input
            value={email}
            onChange={(event) => setEmail(event.target.value)}
            type="email"
            autoComplete="email"
            placeholder="you@example.com"
            disabled={stage === "code"}
          />
        </Field>
        {stage === "code" ? (
          <Field label="Verification code" helper={`Sent to ${email}`}>
            <Input
              value={code}
              onChange={(event) => setCode(event.target.value.replace(/\D/g, "").slice(0, 6))}
              inputMode="numeric"
              maxLength={6}
              placeholder="6-digit code"
              aria-label="Email code"
            />
          </Field>
        ) : null}
        <Button
          type="button"
          onClick={stage === "email" ? send : verify}
          disabled={pending || (stage === "email" ? !email.includes("@") : code.length !== 6)}
          className="w-full justify-center"
        >
          {pending ? "Please wait…" : stage === "email" ? "Send code" : "Verify email"}
        </Button>
        <div className="flex items-center justify-between text-sm text-muted">
          {stage === "code" ? (
            <button type="button" className="underline" onClick={() => setStage("email")}>
              Change email
            </button>
          ) : (
            <span />
          )}
          <button type="button" className="underline" onClick={onSkip}>
            Skip for now
          </button>
        </div>
        {error ? <p role="alert" className="text-sm text-danger">{error}</p> : null}
      </div>
    </Panel>
  );
}

/** Step 2 — profile (name, username, date of birth). Reuses the shared onboarding form. */
function ProfileStep({ onDone }: { onDone: () => Promise<void> }) {
  return (
    <Panel
      title="Complete your profile"
      subtitle="Your name, a public username, and your date of birth."
    >
      <OnboardingForm initialName="" onDone={() => void onDone()} />
    </Panel>
  );
}

/**
 * Step 3 — the account's first password. Required (D-311): an account reachable only by SMS code is
 * locked out the moment the number is lost.
 *
 * The minimum is read from the server rather than assumed, because a client-side guess that is more
 * permissive lets someone type and confirm a password the backend then refuses.
 */
function PasswordStep({ onDone }: { onDone: () => Promise<void> }) {
  const [password, setPasswordValue] = useState("");
  const [confirm, setConfirm] = useState("");
  const [minLength, setMinLength] = useState(12);
  const [error, setError] = useState("");
  const [pending, startTransition] = useTransition();

  useEffect(() => {
    void passwordPolicyAction().then((res) => {
      if (res.ok) setMinLength(res.minLength);
    });
  }, []);

  const tooShort = password.length > 0 && password.length < minLength;
  const mismatch = confirm.length > 0 && password !== confirm;
  const canSubmit = password.length >= minLength && password === confirm && !pending;

  function submit() {
    setError("");
    startTransition(async () => {
      const res = await setPasswordAction(password);
      if (res.ok) {
        await onDone();
      } else {
        setError(res.error);
      }
    });
  }

  return (
    <Panel
      title="Set a password"
      subtitle="Required — it is how you sign in, and how you get back in if you lose this number."
    >
      <div className="space-y-4">
        <Field label="Password" error={tooShort ? `At least ${minLength} characters` : undefined}>
          <Input
            type="password"
            value={password}
            autoComplete="new-password"
            onChange={(event) => setPasswordValue(event.target.value)}
          />
        </Field>
        <Field label="Confirm password" error={mismatch ? "Passwords do not match" : undefined}>
          <Input
            type="password"
            value={confirm}
            autoComplete="new-password"
            onChange={(event) => setConfirm(event.target.value)}
          />
        </Field>
        {error ? <p role="alert" className="text-sm text-danger">{error}</p> : null}
        <Button onClick={submit} disabled={!canSubmit}>
          {pending ? "Saving…" : "Set password"}
        </Button>
      </div>
    </Panel>
  );
}

/** Success — registration complete; the optional follow-ups live in Security settings. */
function SuccessStep({ status }: { status: RegistrationStatus | null }) {
  const router = useRouter();
  const suggestPassword = status ? !status.has_password : true;
  const suggestDevice = status ? !status.has_trusted_device : true;

  return (
    <Panel title="You're all set!" subtitle="Your Kurx account is ready.">
      <div className="space-y-4">
        {suggestPassword || suggestDevice ? (
          <div className="rounded-md border border-border bg-background p-3 text-sm text-muted">
            To secure your account, you can also
            {suggestPassword ? " set a password" : ""}
            {suggestPassword && suggestDevice ? " and" : ""}
            {suggestDevice ? " add a trusted device" : ""} anytime in Security settings.
          </div>
        ) : null}
        <Button
          type="button"
          onClick={() => {
            router.push("/discover");
            router.refresh();
          }}
          className="w-full justify-center"
        >
          Continue to Kurx
        </Button>
      </div>
    </Panel>
  );
}

function emailErrorCopy(code: string): string {
  switch (code) {
    case "invalid_email":
      return "Enter a valid email address.";
    case "email_taken":
      return "That email is already linked to another account.";
    case "resend_cooldown":
    case "rate_limited":
      return "Please wait a moment before requesting another code.";
    case "invalid_code":
      return "That code isn't right. Check it and try again.";
    default:
      return "Couldn't verify that email. Please try again.";
  }
}
