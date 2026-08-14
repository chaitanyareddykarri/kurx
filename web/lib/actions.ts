"use server";

import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { z } from "zod";
import { clearSession, currentSession, saveSession } from "@/lib/session";
import { logout, requestOtp, updateProfile, verifyOtp } from "@/lib/api";

const phoneSchema = z.string().min(8).max(16);
const codeSchema = z.string().min(4).max(8);

export async function requestOtpAction(_: unknown, formData: FormData) {
  const phone = phoneSchema.parse(formData.get("phone"));
  await requestOtp(phone);
  return { ok: true };
}

export async function verifyOtpAction(_: unknown, formData: FormData) {
  const phone = phoneSchema.parse(formData.get("phone"));
  const code = codeSchema.parse(formData.get("code"));
  const result = await verifyOtp(phone, code);
  await saveSession(result.access_token, result.refresh_token);
  return { ok: true };
}

export async function logoutAction() {
  // Best-effort server-side revoke of the refresh token (D-009/D-014) before dropping local cookies.
  const refresh = cookies().get("kurx_refresh")?.value;
  if (refresh) {
    try {
      await logout(refresh);
    } catch {
      // still clear locally even if the revoke call fails
    }
  }
  await clearSession();
  redirect("/");
}

export async function completeOnboardingAction(
  name: string,
  username: string,
  dateOfBirth: string,
  bio?: string,
) {
  const session = await currentSession();
  if (!session) redirect("/?login=required#login");
  // dateOfBirth is required, not optional: without it the server keeps `complete_profile` in
  // `remaining` and the registration wizard returns to this step forever (D-311).
  await updateProfile(session.accessToken, { name, username, dateOfBirth, bio });
}
