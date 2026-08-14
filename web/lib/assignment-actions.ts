"use server";

import { revalidatePath } from "next/cache";
import { requireSession } from "@/lib/session";
import { assignToEvent, removeEventAssignment, respondToAssignment, apiErrorMessage } from "@/lib/api";

function str(formData: FormData, key: string): string | undefined {
  const v = formData.get(key);
  return typeof v === "string" && v.length > 0 ? v : undefined;
}

export async function assignAction(orgId: string, eventId: string, _: unknown, formData: FormData) {
  const session = await requireSession();
  const phone = str(formData, "phone");
  const role = str(formData, "role") ?? "Volunteer";
  if (!phone) return { error: "Phone is required." };
  try {
    await assignToEvent(session.accessToken, orgId, eventId, {
      phone, role, customRole: str(formData, "customRole"), notes: str(formData, "notes")
    });
  } catch (err) {
    return { error: apiErrorMessage(err) };
  }
  revalidatePath(`/host/events/${eventId}`);
  return { ok: true };
}

export async function removeAssignmentAction(orgId: string, eventId: string, id: string) {
  const session = await requireSession();
  await removeEventAssignment(session.accessToken, orgId, eventId, id);
  revalidatePath(`/host/events/${eventId}`);
}

/**
 * Accept or decline an invite to staff an event (D-319). The refusal is returned rather than thrown:
 * `not_pending` is the ordinary outcome of answering an invite twice — two tabs, or a back button —
 * and it needs to read as "already answered", not as a crash.
 */
export async function respondToAssignmentAction(id: string, accept: boolean) {
  const session = await requireSession();
  try {
    await respondToAssignment(session.accessToken, id, accept);
  } catch (err) {
    return { error: apiErrorMessage(err) };
  }
  revalidatePath("/assignments");
  return { ok: true };
}
