/**
 * WebAuthn browser glue for the passkey rail (AM3, D-086).
 *
 * The server speaks fido2-net-lib's JSON, in which every binary field is base64url. The browser's
 * `navigator.credentials` API speaks ArrayBuffers. This module is the translation layer, and it is
 * deliberately the only place that conversion happens — getting the encoding subtly wrong (base64 vs
 * base64url, or missing padding) is the classic way a passkey integration fails with an opaque
 * "invalid signature" from the server rather than a useful client error.
 *
 * Conversion is done by hand rather than via `PublicKeyCredential.parseCreationOptionsFromJSON`,
 * which is still not available across all the browsers we target.
 */

export function base64UrlToBuffer(value: string): ArrayBuffer {
  const padded = value.replace(/-/g, "+").replace(/_/g, "/");
  const binary = atob(padded.padEnd(padded.length + ((4 - (padded.length % 4)) % 4), "="));
  const bytes = new Uint8Array(binary.length);
  for (let i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i);
  return bytes.buffer;
}

export function bufferToBase64Url(buffer: ArrayBuffer): string {
  const bytes = new Uint8Array(buffer);
  let binary = "";
  for (let i = 0; i < bytes.length; i++) binary += String.fromCharCode(bytes[i]);
  return btoa(binary).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
}

/** True when this browser can do passkeys at all. Checked before offering the UI. */
export function isPasskeySupported(): boolean {
  return (
    typeof window !== "undefined" &&
    typeof window.PublicKeyCredential !== "undefined" &&
    typeof navigator.credentials?.create === "function"
  );
}

/**
 * True when the device has a built-in authenticator (Touch ID / Face ID / Windows Hello).
 * Used only to choose wording — a security key still works when this is false.
 */
export async function hasPlatformAuthenticator(): Promise<boolean> {
  if (!isPasskeySupported()) return false;
  try {
    return await window.PublicKeyCredential.isUserVerifyingPlatformAuthenticatorAvailable();
  } catch {
    return false;
  }
}

type ServerCredentialDescriptor = { id: string; type: string; transports?: AuthenticatorTransport[] };

/** fido2-net-lib's CredentialCreateOptions JSON, in the fields the browser needs. */
type ServerCreateOptions = {
  rp: { id?: string; name: string };
  user: { id: string; name: string; displayName: string };
  challenge: string;
  pubKeyCredParams: PublicKeyCredentialParameters[];
  timeout?: number;
  attestation?: AttestationConveyancePreference;
  authenticatorSelection?: AuthenticatorSelectionCriteria;
  excludeCredentials?: ServerCredentialDescriptor[];
};

type ServerRequestOptions = {
  challenge: string;
  timeout?: number;
  rpId?: string;
  allowCredentials?: ServerCredentialDescriptor[];
  userVerification?: UserVerificationRequirement;
};

/** Runs the registration ceremony and returns the JSON the server expects back. */
export async function createPasskey(options: ServerCreateOptions) {
  const credential = (await navigator.credentials.create({
    publicKey: {
      ...options,
      challenge: base64UrlToBuffer(options.challenge),
      user: { ...options.user, id: base64UrlToBuffer(options.user.id) },
      excludeCredentials: (options.excludeCredentials ?? []).map((c) => ({
        ...c,
        id: base64UrlToBuffer(c.id),
        type: "public-key" as const
      }))
    }
  })) as PublicKeyCredential | null;

  if (!credential) throw new Error("Passkey creation was cancelled.");
  const response = credential.response as AuthenticatorAttestationResponse;

  return {
    id: credential.id,
    rawId: bufferToBase64Url(credential.rawId),
    type: credential.type,
    extensions: credential.getClientExtensionResults(),
    response: {
      attestationObject: bufferToBase64Url(response.attestationObject),
      clientDataJSON: bufferToBase64Url(response.clientDataJSON)
    }
  };
}

/** Runs the assertion (sign-in) ceremony and returns the JSON the server expects back. */
export async function getPasskeyAssertion(options: ServerRequestOptions) {
  const credential = (await navigator.credentials.get({
    publicKey: {
      ...options,
      challenge: base64UrlToBuffer(options.challenge),
      allowCredentials: (options.allowCredentials ?? []).map((c) => ({
        ...c,
        id: base64UrlToBuffer(c.id),
        type: "public-key" as const
      }))
    }
  })) as PublicKeyCredential | null;

  if (!credential) throw new Error("Passkey sign-in was cancelled.");
  const response = credential.response as AuthenticatorAssertionResponse;

  return {
    id: credential.id,
    rawId: bufferToBase64Url(credential.rawId),
    type: credential.type,
    extensions: credential.getClientExtensionResults(),
    response: {
      authenticatorData: bufferToBase64Url(response.authenticatorData),
      clientDataJSON: bufferToBase64Url(response.clientDataJSON),
      signature: bufferToBase64Url(response.signature),
      // Null for a non-discoverable credential; the server tolerates its absence.
      userHandle: response.userHandle ? bufferToBase64Url(response.userHandle) : null
    }
  };
}

/**
 * Turns a WebAuthn DOMException into something a person can act on. The raw messages are famously
 * unhelpful ("The operation either timed out or was not allowed"), and the same error covers both
 * "you cancelled" and "wrong domain", so we disambiguate on name where we can.
 */
export function passkeyErrorMessage(err: unknown): string {
  if (err instanceof DOMException) {
    switch (err.name) {
      case "NotAllowedError":
        return "Passkey prompt was dismissed or timed out. Please try again.";
      case "InvalidStateError":
        return "This device already has a passkey for your account.";
      case "SecurityError":
        return "This site's domain isn't allowed to use passkeys. Contact support.";
      case "NotSupportedError":
        return "This browser or device doesn't support passkeys.";
      case "AbortError":
        return "Passkey request was cancelled.";
    }
  }
  return err instanceof Error ? err.message : "Passkey request failed.";
}
