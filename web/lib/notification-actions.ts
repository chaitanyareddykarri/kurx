"use server";

import { revalidatePath } from "next/cache";
import { requireSession } from "@/lib/session";
import { markNotificationRead, markAllNotificationsRead } from "@/lib/api";

export async function markReadAction(id: string) {
  const session = await requireSession();
  await markNotificationRead(session.accessToken, id);
  revalidatePath("/notifications");
}

export async function markAllReadAction() {
  const session = await requireSession();
  await markAllNotificationsRead(session.accessToken);
  revalidatePath("/notifications");
}
