"use server";

import { revalidatePath } from "next/cache";
import { grantStaff, revokeStaffRole, apiErrorMessage } from "@/lib/api";
import { currentSession } from "@/lib/session";

type ActionResult = { ok: true } | { ok: false; error: string };

/** Staff mutations are SuperAdmin-only; the backend enforces it too (defense in depth). */
async function superAdminToken(): Promise<string | null> {
  const s = await currentSession();
  if (!s || !s.roles.includes("SuperAdmin")) return null;
  return s.accessToken;
}

export async function grantStaffAction(phone: string, role: string): Promise<ActionResult> {
  const token = await superAdminToken();
  if (!token) return { ok: false, error: "SuperAdmin access required." };
  if (!phone.trim()) return { ok: false, error: "Enter a phone number." };
  try {
    await grantStaff(token, { phone: phone.trim(), role });
    revalidatePath("/staff");
    return { ok: true };
  } catch (err) {
    return { ok: false, error: apiErrorMessage(err) };
  }
}

export async function revokeStaffAction(userId: string, role: string): Promise<ActionResult> {
  const token = await superAdminToken();
  if (!token) return { ok: false, error: "SuperAdmin access required." };
  try {
    await revokeStaffRole(token, userId, role);
    revalidatePath("/staff");
    return { ok: true };
  } catch (err) {
    return { ok: false, error: apiErrorMessage(err) };
  }
}
