"use client";

import { useEffect, useState } from "react";
import { Button, Spinner } from "@kurx/ui";
import { passwordStatusAction, setOrChangePasswordAction } from "@/lib/password-actions";
import { passwordErrorCopy, passwordIssue } from "@/lib/password";
import { PasswordField } from "@/components/auth/password-field";

/**
 * Staff create/change password (D-126/D-129). `GET /password/status` decides which — a staff account
 * that has only ever used one-time codes has no password until it creates one; a change requires the
 * current password so a stolen session can't silently take the account. Runs entirely through server
 * actions (the console keeps the token server-side).
 */
export function PasswordManager() {
  const [hasPassword, setHasPassword] = useState<boolean | null>(null);
  const [current, setCurrent] = useState("");
  const [next, setNext] = useState("");
  const [confirm, setConfirm] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [done, setDone] = useState(false);

  useEffect(() => {
    passwordStatusAction().then((res) => setHasPassword(res.ok ? res.hasPassword : false));
  }, []);

  async function submit() {
    setError("");
    const issue = passwordIssue(next);
    if (issue) {
      setError(issue);
      return;
    }
    if (next !== confirm) {
      setError("The two passwords don't match.");
      return;
    }
    setBusy(true);
    const res = await setOrChangePasswordAction(hasPassword ?? false, current, next);
    setBusy(false);
    if (res.ok) {
      setDone(true);
      setHasPassword(true);
      setCurrent("");
      setNext("");
      setConfirm("");
    } else {
      setError(passwordErrorCopy(res.error, res.status));
    }
  }

  if (hasPassword === null) {
    return <p className="text-sm text-muted">Loading…</p>;
  }

  return (
    <section className="space-y-4">
      <div>
        <h2 className="text-lg font-semibold text-text">{hasPassword ? "Change password" : "Create a password"}</h2>
        <p className="mt-1 text-sm text-muted">
          {hasPassword
            ? "You'll be asked for your current password. Changing it signs out untrusted browsers."
            : "Add a password so you can sign in without a one-time code. At least 12 characters."}
        </p>
      </div>

      {done ? (
        <p role="status" className="rounded-md border border-accent/40 bg-accent/10 p-3 text-sm text-text">
          {hasPassword ? "Password updated." : "Password created."}{" "}
          <button type="button" className="underline" onClick={() => setDone(false)}>
            Change it again
          </button>
        </p>
      ) : (
        <div className="space-y-3">
          {hasPassword ? (
            <PasswordField label="Current password" value={current} onChange={setCurrent} autoComplete="current-password" />
          ) : null}
          <PasswordField
            label={hasPassword ? "New password" : "Password"}
            value={next}
            onChange={setNext}
            autoComplete="new-password"
            showStrength
          />
          <PasswordField
            label="Confirm password"
            value={confirm}
            onChange={setConfirm}
            autoComplete="new-password"
            onEnter={submit}
          />
          {error ? <p role="alert" className="text-sm text-danger">{error}</p> : null}
          <Button onClick={submit} disabled={busy || !next || !confirm || (hasPassword && !current)}>
            {busy ? <Spinner size={16} className="text-white" /> : null}
            {hasPassword ? "Change password" : "Create password"}
          </Button>
        </div>
      )}
    </section>
  );
}
