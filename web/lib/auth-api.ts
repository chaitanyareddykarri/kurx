import { api, apiErrorMessage, apiErrorStatus } from "@/lib/api";

/**
 * Client for the trusted-device authentication platform (AM3–AM9, D-181/D-182).
 *
 * Kept separate from `lib/api.ts` (which is the legacy OTP + product surface) so the two auth
 * generations stay visibly distinct while both are supported — see D-089's staged migration.
 */

// ── Password-first login (Phase 2B / D-182) ─────────────────────────────────

/**
 * One second factor the backend says this account can use (D-283). The client renders these and never
 * decides them: `method` is the stable machine key to switch on, `label` is the copy to show, and `hint`
 * is an already-masked destination or device name. Never parse `label` — it is localised server-side.
 */
export type SecondFactorMethod = {
  method: "trusted_device" | "passkey" | "sms_otp" | "email_otp" | "recovery_code";
  rank: number;
  label: string;
  hint: string | null;
};

export type PasswordLoginResult =
  | { outcome: "session"; access_token: string; refresh_token: string; user_id: string }
  | {
      outcome: "device_approval";
      challenge_id: string;
      poll_token: string;
      match_number: number;
      expires_at: string;
      methods: SecondFactorMethod[];
    }
  | {
      outcome: "second_factor";
      challenge_id: string;
      poll_token: string;
      expires_at: string;
      methods: SecondFactorMethod[];
    }
  | { outcome: "error"; error: string; status: number };

/**
 * Factor 1 = password. The backend replies with either a session (a valid trusted-browser cookie was
 * factor 2), or `device_approval` (a challenge was pushed to the user's trusted devices → wait), or an
 * error. `withCredentials` lets the browser store/send the httpOnly `kurx_tb` trusted-browser cookie —
 * that is what makes "remember this browser" skip the device round-trip next time.
 */
export async function loginPassword(
  identifier: string,
  password: string,
  rememberBrowser: boolean
): Promise<PasswordLoginResult> {
  try {
    const { data } = await api.post(
      "/v1/auth/login/password",
      { identifier, password, rememberBrowser, surface: "web" },
      { withCredentials: true }
    );
    if (data.next === "device_approval") {
      return {
        outcome: "device_approval",
        challenge_id: data.challenge_id,
        poll_token: data.poll_token,
        match_number: data.match_number,
        expires_at: data.expires_at,
        methods: data.methods ?? []
      };
    }
    if (data.next === "second_factor") {
      return {
        outcome: "second_factor",
        challenge_id: data.challenge_id,
        poll_token: data.poll_token,
        expires_at: data.expires_at,
        methods: data.methods ?? []
      };
    }
    return {
      outcome: "session",
      access_token: data.access_token,
      refresh_token: data.refresh_token,
      user_id: data.user_id
    };
  } catch (err) {
    // The backend's `error` code drives the copy the UI shows; status distinguishes 401 / 423 / 400.
    return { outcome: "error", error: apiErrorMessage(err), status: apiErrorStatus(err) ?? 0 };
  }
}

// ── Second factor by one-time code (D-280) ───────────────────────────────────
// The method list is the backend's (D-283): this module never decides what is available, and the panel
// renders whatever arrives. Both calls carry the poll token issued by the password step, which is what
// proves this caller passed factor 1.

export async function sendSecondFactorCode(
  challengeId: string,
  pollToken: string,
  method: string
): Promise<{ ok: boolean; error?: string }> {
  try {
    await api.post("/v1/auth/login/second-factor/send", {
      challengeId,
      pollToken,
      method
    });
    return { ok: true };
  } catch (err) {
    return { ok: false, error: apiErrorMessage(err) };
  }
}

export async function verifySecondFactorCode(
  challengeId: string,
  pollToken: string,
  code: string
): Promise<{ ok: boolean; access_token?: string; refresh_token?: string; error?: string }> {
  try {
    // `withCredentials`, same as the password step: a successful verify is where the backend issues the
    // httpOnly `kurx_tb` cookie when the user asked to be remembered. Without it the browser drops the
    // Set-Cookie, so "remember this browser" silently did nothing on the one-time-code path and the user
    // was challenged again on every login — with no error to explain why.
    const { data } = await api.post(
      "/v1/auth/login/second-factor/verify",
      { challengeId, pollToken, code },
      { withCredentials: true }
    );
    return { ok: true, access_token: data.access_token, refresh_token: data.refresh_token };
  } catch (err) {
    return { ok: false, error: apiErrorMessage(err) };
  }
}

// ── Device-approval login status (AM4/D-080) ─────────────────────────────────
// The password-first login's device-approval branch (D-182) polls this. The passwordless
// `/login/start` initiation was removed in D-289 — no client ever called it.

export type LoginStatus = {
  status: "pending" | "approved" | "rejected" | "expired" | "consumed";
  access_token?: string;
  refresh_token?: string;
  user_id?: string;
};

export async function pollDeviceLogin(challengeId: string, pollToken: string): Promise<LoginStatus> {
  // withCredentials so the browser stores the `kurx_tb` cookie the backend sets on approval when the
  // login was started with "remember this browser" (D-182). Harmless for logins that did not ask for it.
  const { data } = await api.post(
    "/v1/auth/login/status",
    { challengeId, pollToken },
    { withCredentials: true }
  );
  return data;
}

// ── Passkeys (AM3/D-086) ────────────────────────────────────────────────────

export async function passkeyRegisterOptions(accessToken: string) {
  const { data } = await api.post(
    "/v1/auth/passkeys/register/options",
    {},
    { headers: { Authorization: `Bearer ${accessToken}` } }
  );
  return data as { challenge_id: string; options: Record<string, unknown> };
}

export async function passkeyRegister(
  accessToken: string,
  challengeId: string,
  response: unknown,
  deviceName: string
) {
  const { data } = await api.post(
    "/v1/auth/passkeys/register",
    { challengeId, response, deviceName },
    { headers: { Authorization: `Bearer ${accessToken}` } }
  );
  return data as { ok: boolean; device_id: string };
}

export async function passkeyLoginOptions(identifier: string) {
  const { data } = await api.post("/v1/auth/passkeys/login/options", { identifier });
  return data as { challenge_id: string; options: Record<string, unknown> };
}

export async function passkeyLogin(challengeId: string, response: unknown) {
  const { data } = await api.post("/v1/auth/passkeys/login", { challengeId, response });
  return data as { access_token: string; refresh_token: string; user_id: string };
}

export type PasskeyView = {
  id: string;
  name: string | null;
  platform: string;
  state: string;
  lastSeenAt: string | null;
  createdAt: string;
};

export async function listPasskeys(accessToken: string): Promise<PasskeyView[]> {
  const { data } = await api.get("/v1/auth/passkeys", {
    headers: { Authorization: `Bearer ${accessToken}` }
  });
  return data;
}

// ── Devices + sessions (AM2/AM5, D-081) ─────────────────────────────────────

export type DeviceView = PasskeyView;

export async function listDevices(accessToken: string): Promise<DeviceView[]> {
  const { data } = await api.get("/v1/auth/devices", {
    headers: { Authorization: `Bearer ${accessToken}` }
  });
  return data;
}

export async function revokeDevice(accessToken: string, deviceId: string) {
  await api.post(
    `/v1/auth/devices/${deviceId}/revoke`,
    {},
    { headers: { Authorization: `Bearer ${accessToken}` } }
  );
}

export type SessionView = {
  id: string;
  deviceId: string | null;
  deviceName: string | null;
  platform: string | null;
  isCurrent: boolean;
  createdAt: string;
  lastRotatedAt: string | null;
};

export async function listSessions(accessToken: string): Promise<SessionView[]> {
  const { data } = await api.get("/v1/auth/sessions", {
    headers: { Authorization: `Bearer ${accessToken}` }
  });
  return data;
}

export async function revokeSession(accessToken: string, sessionId: string) {
  await api.post(
    `/v1/auth/sessions/${sessionId}/revoke`,
    {},
    { headers: { Authorization: `Bearer ${accessToken}` } }
  );
}

// ── Step-up (AM6/D-084) ─────────────────────────────────────────────────────
// The browser can't produce a device signature, so it only observes the step-up grant (the mobile
// app starts + signs the challenge). `/step-up/start` is therefore not called from the web.

export type StepUpStatus = { satisfied: boolean; valid_until: string | null; can_step_up: boolean };

export async function stepUpStatus(accessToken: string): Promise<StepUpStatus> {
  const { data } = await api.get("/v1/auth/step-up/status", {
    headers: { Authorization: `Bearer ${accessToken}` }
  });
  return data;
}

// ── Recovery codes (AM7/D-083) ──────────────────────────────────────────────

export async function recoveryCodesRemaining(accessToken: string): Promise<number> {
  const { data } = await api.get("/v1/auth/recovery-codes", {
    headers: { Authorization: `Bearer ${accessToken}` }
  });
  return data.remaining;
}

/** Returns the plaintext codes. This is the only time they exist outside the server — show once. */
export async function generateRecoveryCodes(accessToken: string): Promise<string[]> {
  const { data } = await api.post(
    "/v1/auth/recovery-codes",
    {},
    { headers: { Authorization: `Bearer ${accessToken}` } }
  );
  return data.codes;
}

export async function startRecovery(identifier: string) {
  await api.post("/v1/auth/recovery/start", { identifier });
}

export async function redeemRecovery(identifier: string, otpCode: string, recoveryCode: string) {
  const { data } = await api.post("/v1/auth/recovery/redeem", { identifier, otpCode, recoveryCode });
  return data as { access_token: string; refresh_token: string; user_id: string };
}

// ── Security Center (Phase 2E) ───────────────────────────────────────────────
// One overview call + activity feed + "sign out everywhere". The per-factor lists (devices,
// sessions, passkeys, recovery, trusted browsers) come from their own endpoints below.

export type SecurityOverview = {
  has_password: boolean;
  email: string | null;
  email_verified: boolean;
  phone: string | null;
  phone_verified: boolean;
  trusted_browsers: number;
  trusted_devices: number;
  passkeys: number;
  active_sessions: number;
  recovery_codes_remaining: number;
  step_up_satisfied: boolean;
  can_step_up: boolean;
};

export async function securityOverview(accessToken: string): Promise<SecurityOverview> {
  const { data } = await api.get("/v1/auth/security-center", {
    headers: { Authorization: `Bearer ${accessToken}` }
  });
  return data as SecurityOverview;
}

export type SecurityActivityItem = { type: string; severity: string; context: string | null; created_at: string };

export async function securityActivity(accessToken: string, limit = 50): Promise<SecurityActivityItem[]> {
  const { data } = await api.get("/v1/auth/security-center/activity", {
    headers: { Authorization: `Bearer ${accessToken}` },
    params: { limit }
  });
  return data as SecurityActivityItem[];
}

/** The panic button: revokes every session and forgets every trusted browser. Returns the count. */
export async function signOutEverywhere(accessToken: string): Promise<number> {
  const { data } = await api.post(
    "/v1/auth/security-center/sign-out-all",
    {},
    { headers: { Authorization: `Bearer ${accessToken}` } }
  );
  return (data as { revoked: number }).revoked;
}

// ── Trusted browsers (Phase 2A/D-127) — camelCase (C# record projection) ─────

export type TrustedBrowserView = {
  id: string;
  label: string | null;
  browser: string | null;
  operatingSystem: string | null;
  ip: string | null;
  approxLocation: string | null;
  isCurrent: boolean;
  createdAt: string;
  lastUsedAt: string | null;
  expiresAt: string;
};

export async function listTrustedBrowsers(accessToken: string): Promise<TrustedBrowserView[]> {
  const { data } = await api.get("/v1/auth/trusted-browsers", {
    headers: { Authorization: `Bearer ${accessToken}` }
  });
  return data as TrustedBrowserView[];
}

export async function revokeTrustedBrowser(accessToken: string, id: string) {
  await api.post(
    `/v1/auth/trusted-browsers/${id}/revoke`,
    {},
    { headers: { Authorization: `Bearer ${accessToken}` } }
  );
}

// ── Password lifecycle (Phase 2C/2D, D-126/D-127/D-129) ──────────────────────
// Create/change are authenticated; the reset ceremony is anonymous (the user can't sign in) and
// requires OTP + a recovery code (INV-B). Policy is NIST SP 800-63B: length is the control, no
// composition rules — mirrored client-side in lib/password.ts so the two never drift.

export type PasswordStatus = { has_password: boolean; min_length: number; max_length: number };

export async function passwordStatus(accessToken: string): Promise<PasswordStatus> {
  const { data } = await api.get("/v1/auth/password/status", {
    headers: { Authorization: `Bearer ${accessToken}` }
  });
  return data as PasswordStatus;
}

/** First-time creation. Fails with `password_already_set` if one exists — use changePassword then. */
export async function setPassword(accessToken: string, password: string) {
  await api.post("/v1/auth/password/set", { password }, { headers: { Authorization: `Bearer ${accessToken}` } });
}

export async function changePassword(accessToken: string, currentPassword: string, newPassword: string) {
  await api.post(
    "/v1/auth/password/change",
    { currentPassword, newPassword },
    { headers: { Authorization: `Bearer ${accessToken}` } }
  );
}

/** Anonymous — always succeeds (anti-enumeration), sending a reset OTP to the account if it exists. */
export async function startPasswordReset(identifier: string) {
  await api.post("/v1/auth/password/reset/start", { identifier });
}

/** Anonymous. OTP + recovery code (INV-B). On success returns a fresh session (the reset logs you in). */
export async function completePasswordReset(
  identifier: string,
  otpCode: string,
  newPassword: string,
  recoveryCode: string
) {
  const { data } = await api.post("/v1/auth/password/reset/complete", {
    identifier,
    otpCode,
    newPassword,
    recoveryCode
  });
  return data as { access_token: string; refresh_token: string; user_id: string };
}
