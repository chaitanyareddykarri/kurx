"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { passkeyLogin, passkeyLoginOptions } from "@/lib/auth-api";
import { getPasskeyAssertion, isPasskeySupported, passkeyErrorMessage } from "@/lib/webauthn";
import { saveSession } from "@/lib/session";
import { apiErrorMessage } from "@/lib/api";
import { Button } from "@/components/ui/button";

/**
 * Passkey sign-in (AM3/D-086).
 *
 * Note the deliberate absence of an "account not found" path: `/passkeys/login/options` returns
 * well-formed options with an empty credential list for an unknown identifier, so this component
 * cannot — and must not try to — tell the user whether the account exists. A failure is always the
 * same generic message, which is what preserves the anti-enumeration property end to end.
 */
export function PasskeySignIn({ identifier }: { identifier: string }) {
  const router = useRouter();
  const [supported, setSupported] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");

  useEffect(() => setSupported(isPasskeySupported()), []);
  if (!supported) return null;

  async function signIn() {
    setError("");
    setBusy(true);
    try {
      const { challenge_id, options } = await passkeyLoginOptions(identifier);
      const assertion = await getPasskeyAssertion(options as never);
      const tokens = await passkeyLogin(challenge_id, assertion);
      await saveSession(tokens.access_token, tokens.refresh_token);
      router.push("/discover");
      router.refresh();
    } catch (err) {
      // DOMExceptions come from the browser ceremony; anything else is the API refusing.
      setError(
        err instanceof DOMException
          ? passkeyErrorMessage(err)
          : "Couldn't sign in with a passkey. Try another method."
      );
      void apiErrorMessage(err);
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="space-y-2">
      <Button type="button" variant="secondary" className="w-full" disabled={busy || !identifier} onClick={signIn}>
        {busy ? "Waiting for your passkey…" : "Sign in with a passkey"}
      </Button>
      {error && (
        <p className="text-sm text-danger" role="alert">
          {error}
        </p>
      )}
    </div>
  );
}
