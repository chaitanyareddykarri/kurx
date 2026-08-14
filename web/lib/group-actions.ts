"use server";

import { revalidatePath } from "next/cache";
import { requireSession } from "@/lib/session";
import { joinGroup, apiErrorMessage } from "@/lib/api";

// Join an existing group registration with the leader's code. The backend validates the code (4–10
// chars) and enforces capacity/competition rules; we only surface its error.
export async function joinGroupAction(_prev: unknown, formData: FormData) {
  const joinCode = String(formData.get("joinCode") ?? "").trim();
  const displayName = String(formData.get("displayName") ?? "").trim() || undefined;
  if (joinCode.length < 4) return { error: "Enter the join code from your group leader." };

  const session = await requireSession();
  try {
    await joinGroup(session.accessToken, { joinCode, displayName });
  } catch (err) {
    return { error: apiErrorMessage(err) };
  }
  revalidatePath("/groups");
  return { ok: true };
}
