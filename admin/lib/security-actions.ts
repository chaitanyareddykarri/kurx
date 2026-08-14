"use server";

import {
  apiErrorMessage,
  apiErrorStatus,
  generateRecoveryCodes,
  revokeDevice,
  revokeSession,
  revokeTrustedBrowser,
  signOutEverywhere,
  stepUpStatus
} from "@/lib/api";
import { clearSession, currentSession } from "@/lib/session";

/**
 * Security Center mutations for staff (Phase 2E). Reads are done in the server component; these
 * actions perform the writes with the session token (kept server-side) and let the page re-fetch via
 * router.refresh().
 */

type Result = { ok: true } | { ok: false; error: string; status?: number };

export async function revokeSessionAction(id: string): Promise<Result> {
  const session = await currentSession();
  if (!session) return { ok: false, error: "not_authenticated" };
  try {
    await revokeSession(session.accessToken, id);
    return { ok: true };
  } catch (err) {
    return { ok: false, error: apiErrorMessage(err), status: apiErrorStatus(err) };
  }
}

export async function revokeDeviceAction(id: string): Promise<Result> {
  const session = await currentSession();
  if (!session) return { ok: false, error: "not_authenticated" };
  try {
    await revokeDevice(session.accessToken, id);
    return { ok: true };
  } catch (err) {
    return { ok: false, error: apiErrorMessage(err), status: apiErrorStatus(err) };
  }
}

export async function revokeBrowserAction(id: string): Promise<Result> {
  const session = await currentSession();
  if (!session) return { ok: false, error: "not_authenticated" };
  try {
    await revokeTrustedBrowser(session.accessToken, id);
    return { ok: true };
  } catch (err) {
    return { ok: false, error: apiErrorMessage(err), status: apiErrorStatus(err) };
  }
}

export async function generateRecoveryCodesAction(): Promise<
  { ok: true; codes: string[] } | { ok: false; error: string; status?: number }
> {
  const session = await currentSession();
  if (!session) return { ok: false, error: "not_authenticated" };
  try {
    return { ok: true, codes: await generateRecoveryCodes(session.accessToken) };
  } catch (err) {
    // 403 is the step-up gate (D-084), not a permissions bug.
    return {
      ok: false,
      error: apiErrorStatus(err) === 403 ? "step_up_required" : apiErrorMessage(err),
      status: apiErrorStatus(err)
    };
  }
}

/** Polls whether the staff user has a recent step-up grant (satisfied within the server window). Used by
 *  the step-up flow to know when a sensitive action can be retried. Defaults to false on any failure. */
export async function stepUpStatusAction(): Promise<{ satisfied: boolean }> {
  const session = await currentSession();
  if (!session) return { satisfied: false };
  try {
    return { satisfied: (await stepUpStatus(session.accessToken)).satisfied };
  } catch {
    return { satisfied: false };
  }
}

/** The panic button: revokes every session (this one included) and forgets every trusted browser, then
 *  clears the local console session so the next navigation lands on sign-in. */
export async function signOutEverywhereAction(): Promise<Result> {
  const session = await currentSession();
  if (!session) return { ok: false, error: "not_authenticated" };
  try {
    await signOutEverywhere(session.accessToken);
    await clearSession();
    return { ok: true };
  } catch (err) {
    return { ok: false, error: apiErrorMessage(err), status: apiErrorStatus(err) };
  }
}
