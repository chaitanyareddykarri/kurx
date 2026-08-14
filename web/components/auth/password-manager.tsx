"use client";

import { useEffect, useState } from "react";
import { apiErrorMessage, apiErrorStatus } from "@/lib/api";
import { changePassword, passwordStatus, setPassword } from "@/lib/auth-api";
import { passwordErrorCopy, passwordIssue } from "@/lib/password";
import { Button } from "@/components/ui/button";
import { PasswordField } from "@/components/auth/password-field";

/**
 * Password create/change (D-126/D-129), rendered on the security page. `GET /password/status` decides
 * which one to show — OTP-era accounts have no password until they create one; a change requires the
 * current password (a stolen access token must not be able to silently take the account).
 */
export function PasswordManager({ accessToken }: { accessToken: string }) {
  const [hasPassword, setHasPassword] = useState<boolean | null>(null);
  const [current, setCurrent] = useState("");
  const [next, setNext] = useState("");
  const [confirm, setConfirm] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [done, setDone] = useState(false);

  useEffect(() => {
    passwordStatus(accessToken)
      .then((status) => setHasPassword(status.has_password))
      .catch(() => setHasPassword(null));
  }, [accessToken]);

  function reset() {
    setCurrent("");
    setNext("");
    setConfirm("");
  }

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
    try {
      if (hasPassword) {
        await changePassword(accessToken, current, next);
      } else {
        await setPassword(accessToken, next);
      }
      setDone(true);
      setHasPassword(true);
      reset();
    } catch (err) {
      setError(passwordErrorCopy(apiErrorMessage(err), apiErrorStatus(err)));
    } finally {
      setBusy(false);
    }
  }

  if (hasPassword === null) {
    return <p className="text-sm text-muted">Loading…</p>;
  }

  return (
    <section className="space-y-4">
      <div>
        <h2 className="text-lg font-semibold">{hasPassword ? "Change password" : "Create a password"}</h2>
        <p className="text-sm text-muted">
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
            <PasswordField
              label="Current password"
              value={current}
              onChange={setCurrent}
              id="current-password"
              autoComplete="current-password"
            />
          ) : null}
          <PasswordField
            label={hasPassword ? "New password" : "Password"}
            value={next}
            onChange={setNext}
            id="new-password"
            autoComplete="new-password"
            showStrength
          />
          <PasswordField
            label="Confirm password"
            value={confirm}
            onChange={setConfirm}
            id="confirm-password"
            autoComplete="new-password"
            onEnter={submit}
          />
          {error ? <p role="alert" className="text-sm text-danger">{error}</p> : null}
          <Button
            type="button"
            onClick={submit}
            disabled={busy || !next || !confirm || (hasPassword && !current)}
          >
            {busy ? "Saving…" : hasPassword ? "Change password" : "Create password"}
          </Button>
        </div>
      )}
    </section>
  );
}
