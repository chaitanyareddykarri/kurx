"use server";

import { revalidatePath } from "next/cache";
import { requireSession } from "@/lib/session";
import { createAnnouncement, cancelAnnouncement, updateAnnouncement, apiErrorMessage } from "@/lib/api";

function str(formData: FormData, key: string): string | undefined {
  const v = formData.get(key);
  return typeof v === "string" && v.length > 0 ? v : undefined;
}

export async function createAnnouncementAction(eventId: string, _: unknown, formData: FormData) {
  const session = await requireSession();
  const channels = formData.getAll("channels").filter((v): v is string => typeof v === "string");
  if (channels.length === 0) return { error: "Select at least one channel." };
  try {
    await createAnnouncement(session.accessToken, eventId, {
      title: str(formData, "title"),
      body: str(formData, "body"),
      audience: str(formData, "audience") ?? "AllTicketHolders",
      channels,
      scheduledAt: str(formData, "scheduledAt"),
      includeChildEvents: formData.get("includeChildEvents") != null
    });
  } catch (err) {
    return { error: apiErrorMessage(err) };
  }
  revalidatePath("/host/announcements");
  return { ok: true };
}

export async function cancelAnnouncementAction(announcementId: string, _eventId: string) {
  const session = await requireSession();
  await cancelAnnouncement(session.accessToken, announcementId);
  revalidatePath("/host/announcements");
}

export async function updateAnnouncementAction(announcementId: string, eventId: string, _: unknown, formData: FormData) {
  const session = await requireSession();
  try {
    await updateAnnouncement(session.accessToken, announcementId, {
      title: str(formData, "title"),
      body: str(formData, "body")
    });
  } catch {
    /* the re-render reflects the unchanged announcement */
  }
  revalidatePath(`/host/events/${eventId}/announcements`);
}
