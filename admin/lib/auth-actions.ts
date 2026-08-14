"use server";

import { cookies } from "next/headers";
import { api, requestOtp, verifyOtp, logout, apiErrorMessage, apiErrorStatus } from "@/lib/api";
import { saveSession, clearSession, currentSession } from "@/lib/session";
import { deriveRoles, isStaff } from "@/lib/roles";

type ActionResult = { ok: true } | { ok: false; error: string };

const TB_COOKIE = "kurx_tb";

/**
 * One second factor the backend says this account can use (D-283). The console renders these and never
 * decides them — no hardcoded method list lives in this app.
 */
export type SecondFactorMethod = {
  method: "trusted_device" | "passkey" | "sms_otp" | "email_otp" | "recovery_code";
  rank: number;
  label: string;
  hint: string | null;
};

export type LoginActionResult =
  | { ok: true; outcome: "session" }
  | {
      ok: true;
      outcome: "device_approval";
      challengeId: string;
      pollToken: string;
      matchNumber: number;
      expiresAt: string;
      methods: SecondFactorMethod[];
    }
  | {
      ok: true;
      outcome: "second_factor";
      challengeId: string;
      pollToken: string;
      expiresAt: string;
      methods: SecondFactorMethod[];
    }
  | { ok: false; error: string; status?: number };

/**
 * Password-first login (Phase 2B / D-182). The console runs auth server-side, so this action proxies the
 * `kurx_tb` trusted-browser cookie in both directions — forwarding the browser's cookie to the backend and
 * re-storing whatever the backend sets — which is what makes "remember this browser" work here as it does
 * on web. On a session it also confirms the account is staff (D-040 live roles).
 */
export async function loginPasswordAction(
  identifier: string,
  password: string,
  remember: boolean
): Promise<LoginActionResult> {
  try {
    const tb = cookies().get(TB_COOKIE)?.value;
    const res = await api.post(
      "/v1/auth/login/password",
      { identifier, password, rememberBrowser: remember, surface: "admin" },
      { headers: tb ? { Cookie: `${TB_COOKIE}=${tb}` } : {} }
    );
    storeTrustedBrowserCookie(res.headers["set-cookie"]);

    if (res.data?.next === "device_approval") {
      return {
        ok: true,
        outcome: "device_approval",
        challengeId: res.data.challenge_id,
        pollToken: res.data.poll_token,
        matchNumber: res.data.match_number,
        expiresAt: res.data.expires_at,
        methods: res.data.methods ?? []
      };
    }
    if (res.data?.next === "second_factor") {
      return {
        ok: true,
        outcome: "second_factor",
        challengeId: res.data.challenge_id,
        pollToken: res.data.poll_token,
        expiresAt: res.data.expires_at,
        methods: res.data.methods ?? []
      };
    }
    return await completeSession(res.data.access_token, res.data.refresh_token);
  } catch (err) {
    return { ok: false, error: apiErrorMessage(err), status: apiErrorStatus(err) };
  }
}

/** Asks the backend to deliver a code for a method it offered. The poll token proves factor 1 passed. */
export async function sendSecondFactorCodeAction(
  challengeId: string,
  pollToken: string,
  method: string
): Promise<{ ok: boolean; error?: string }> {
  try {
    await api.post("/v1/auth/login/second-factor/send", { challengeId, pollToken, method });
    return { ok: true };
  } catch (err) {
    return { ok: false, error: apiErrorMessage(err) };
  }
}

/** Verifies the code and, on success, persists the session and confirms the account is staff. */
export async function verifySecondFactorCodeAction(
  challengeId: string,
  pollToken: string,
  code: string
): Promise<LoginActionResult> {
  try {
    const res = await api.post("/v1/auth/login/second-factor/verify", { challengeId, pollToken, code });
    // A successful verify is the other place the backend issues `kurx_tb`. Storing it here is what makes
    // the "remember this browser" promise in loginPasswordAction hold on the one-time-code path too —
    // without it the console re-challenged on every sign-in and nothing reported a failure.
    storeTrustedBrowserCookie(res.headers["set-cookie"]);
    return await completeSession(res.data.access_token, res.data.refresh_token);
  } catch (err) {
    return { ok: false, error: apiErrorMessage(err), status: apiErrorStatus(err) };
  }
}

/** Polls a device-approval sign-in; on approval, persists the session and confirms staff. */
export async function pollDeviceLoginAction(
  challengeId: string,
  pollToken: string
): Promise<{ status: string; ok?: boolean; error?: string }> {
  try {
    const tb = cookies().get(TB_COOKIE)?.value;
    const res = await api.post(
      "/v1/auth/login/status",
      { challengeId, pollToken },
      { headers: tb ? { Cookie: `${TB_COOKIE}=${tb}` } : {} }
    );
    storeTrustedBrowserCookie(res.headers["set-cookie"]);
    const data = res.data;
    if (data.status === "approved" && data.access_token && data.refresh_token) {
      const done = await completeSession(data.access_token, data.refresh_token);
      return done.ok ? { status: "approved", ok: true } : { status: "approved", ok: false, error: done.error };
    }
    return { status: data.status };
  } catch (err) {
    return { status: "error", error: apiErrorMessage(err) };
  }
}

async function completeSession(access: string, refresh: string): Promise<LoginActionResult> {
  await saveSession(access, refresh);
  const session = await currentSession();
  if (!session || !isStaff(deriveRoles(session.me))) {
    await clearSession();
    return { ok: false, error: "This account doesn't have Kurx admin access." };
  }
  return { ok: true, outcome: "session" };
}

/** Re-stores the backend's httpOnly `kurx_tb` cookie on the console's own domain so it survives to the
 *  next sign-in — the server-side equivalent of the browser storing a cross-site Set-Cookie. */
function storeTrustedBrowserCookie(setCookie: string[] | string | undefined) {
  if (!setCookie) return;
  const headers = Array.isArray(setCookie) ? setCookie : [setCookie];
  const tb = headers.find((header) => header.startsWith(`${TB_COOKIE}=`));
  if (!tb) return;
  const value = tb.split(";")[0].slice(`${TB_COOKIE}=`.length);
  if (!value) return;
  cookies().set(TB_COOKIE, value, {
    httpOnly: true,
    sameSite: "lax",
    secure: process.env.NODE_ENV === "production",
    path: "/",
    maxAge: 60 * 60 * 24 * 30
  });
}

/** Step 1 of login — send the OTP. Reuses the shared backend; no admin-specific API. */
export async function requestOtpAction(phone: string): Promise<ActionResult> {
  if (!phone || phone.trim().length < 8) return { ok: false, error: "Enter a valid phone number." };
  try {
    await requestOtp(phone.trim());
    return { ok: true };
  } catch (err) {
    return { ok: false, error: apiErrorMessage(err) };
  }
}

/** Step 2 — verify the OTP, persist the session, and confirm the account is staff. */
export async function verifyOtpAction(phone: string, code: string): Promise<ActionResult> {
  if (!code || code.trim().length < 4) return { ok: false, error: "Enter the code you received." };
  try {
    const tokens = await verifyOtp(phone.trim(), code.trim());
    await saveSession(tokens.access_token, tokens.refresh_token);
    // Confirm staff up front so a non-admin gets a clear message instead of a redirect loop.
    const session = await currentSession();
    if (!session || !isStaff(deriveRoles(session.me))) {
      await clearSession();
      return { ok: false, error: "This account doesn't have Kurx admin access." };
    }
    return { ok: true };
  } catch (err) {
    return { ok: false, error: apiErrorMessage(err) };
  }
}

export async function logoutAction() {
  const session = await currentSession();
  if (session) {
    try {
      await logout(session.refreshToken);
    } catch {
      // Best-effort server revoke; the local cookies are cleared regardless.
    }
  }
  await clearSession();
}
