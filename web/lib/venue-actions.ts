"use server";

import { revalidatePath } from "next/cache";
import { requireSession } from "@/lib/session";
import { updateOrgVenue, deleteOrgVenue, addVenueImage, presignOrgDoc } from "@/lib/api";

function str(formData: FormData, key: string): string | undefined {
  const v = formData.get(key);
  return typeof v === "string" && v.length > 0 ? v : undefined;
}
function num(formData: FormData, key: string): number | undefined {
  const v = str(formData, key);
  return v === undefined ? undefined : Number(v);
}

export async function updateVenueAction(orgId: string, eventId: string, venueId: string, _: unknown, formData: FormData) {
  const session = await requireSession();
  try {
    await updateOrgVenue(session.accessToken, orgId, venueId, {
      name: str(formData, "name"),
      address: str(formData, "address"),
      city: str(formData, "city"),
      capacity: num(formData, "capacity")
    });
  } catch {
    /* the re-render reflects the unchanged venue */
  }
  revalidatePath(`/host/events/${eventId}/details`);
}

export async function deleteVenueAction(orgId: string, eventId: string, venueId: string) {
  const session = await requireSession();
  await deleteOrgVenue(session.accessToken, orgId, venueId);
  revalidatePath(`/host/events/${eventId}/details`);
}

export async function addVenueImageAction(orgId: string, eventId: string, venueId: string, _: unknown, formData: FormData) {
  const file = formData.get("file");
  if (!(file instanceof File) || file.size === 0) return;
  const session = await requireSession();
  try {
    const presigned = await presignOrgDoc(session.accessToken, orgId, file.type || "image/jpeg", file.size);
    await fetch(presigned.url, { method: "PUT", headers: presigned.headers, body: await file.arrayBuffer() });
    await addVenueImage(session.accessToken, orgId, venueId, presigned.key);
  } catch {
    /* upload errors surface on next load */
  }
  revalidatePath(`/host/events/${eventId}/details`);
}
