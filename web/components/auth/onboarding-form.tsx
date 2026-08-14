"use client";

import { useRef, useState, useTransition } from "react";
import { useRouter } from "next/navigation";
import { checkUsernameAvailability } from "@/lib/api";
import { completeOnboardingAction } from "@/lib/actions";
import { Field, Input } from "@/components/ui/field";
import { Button } from "@/components/ui/button";

type Status = "idle" | "checking" | "available" | "unavailable" | "reserved" | "invalid";

export function OnboardingForm({ initialName, onDone }: { initialName: string; onDone?: () => void }) {
  const router = useRouter();
  const [name, setName] = useState(initialName);
  const [username, setUsername] = useState("");
  const [dateOfBirth, setDateOfBirth] = useState("");
  const [bio, setBio] = useState("");
  const [status, setStatus] = useState<Status>("idle");
  const [error, setError] = useState("");
  const [pending, startTransition] = useTransition();
  const debounceRef = useRef<ReturnType<typeof setTimeout> | undefined>(undefined);

  function onUsernameChange(value: string) {
    const v = value.trim().toLowerCase();
    setUsername(v);
    clearTimeout(debounceRef.current);
    if (v.length < 3) {
      setStatus(v.length === 0 ? "idle" : "invalid");
      return;
    }
    setStatus("checking");
    debounceRef.current = setTimeout(async () => {
      const result = await checkUsernameAvailability(v).catch(() => null);
      setStatus(result ? result.status : "idle");
    }, 350);
  }

  function submit() {
    setError("");
    startTransition(async () => {
      try {
        // Bio is optional, and only sent when written — an empty string would overwrite a bio the
        // person had already set from another surface.
        await completeOnboardingAction(name.trim(), username, dateOfBirth, bio.trim() || undefined);
        if (onDone) {
          onDone();
        } else {
          router.push("/discover");
          router.refresh();
        }
      } catch {
        setError("Could not save your profile. Please try again.");
      }
    });
  }

  // Youngest permitted account holder, mirroring Kurx.Domain.Onboarding.MinimumAgeYears. The input's
  // `max` is a convenience; the server re-validates and is the authority.
  const latestBirthDate = new Date();
  latestBirthDate.setFullYear(latestBirthDate.getFullYear() - 13);
  const maxDateOfBirth = latestBirthDate.toISOString().slice(0, 10);

  const canSubmit =
    name.trim().length > 0 && status === "available" && dateOfBirth !== "" && !pending;
  const usernameError =
    status === "unavailable" || status === "reserved"
      ? "That username is taken or reserved."
      : status === "invalid"
        ? "3-30 characters: letters, numbers, underscore, dot."
        : undefined;
  const usernameHelper =
    status === "checking"
      ? "Checking availability…"
      : status === "available"
        ? `@${username} is available`
        : "Letters, numbers, and underscores only.";

  return (
    <div className="space-y-4 rounded-lg border border-border bg-surface p-5">
      <Field label="Name">
        <Input value={name} onChange={(event) => setName(event.target.value)} placeholder="Your full name" />
      </Field>
      <Field label="Username" error={usernameError} helper={usernameError ? undefined : usernameHelper}>
        <Input
          value={username}
          onChange={(event) => onUsernameChange(event.target.value)}
          placeholder="yourname"
          aria-label="Username"
        />
      </Field>
      <Field label="Date of birth" helper="You must be at least 13 to use Kurx.">
        <Input
          type="date"
          value={dateOfBirth}
          onChange={(event) => setDateOfBirth(event.target.value)}
          max={maxDateOfBirth}
          aria-label="Date of birth"
        />
      </Field>
      {/* Optional, and short here on purpose — the full 2000 characters the server allows are
          available later in profile settings. Mirrors mobile's onboarding field. */}
      <Field label="Bio" helper="Optional — you can add this later.">
        <textarea
          value={bio}
          onChange={(event) => setBio(event.target.value)}
          maxLength={300}
          rows={3}
          placeholder="A line or two about you"
          aria-label="Bio"
          className="w-full rounded-md border border-border bg-background px-3 py-2 text-sm text-text placeholder:text-muted focus:outline-none focus:ring-2 focus:ring-accent"
        />
      </Field>
      {error ? <p className="text-sm text-danger">{error}</p> : null}
      <Button type="button" onClick={submit} disabled={!canSubmit} className="w-full justify-center">
        {pending ? "Saving…" : "Continue"}
      </Button>
    </div>
  );
}
