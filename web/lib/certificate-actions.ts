"use server";

import { revalidatePath } from "next/cache";
import { requireSession } from "@/lib/session";
import { generateCertificates, revokeCertificate, apiErrorMessage } from "@/lib/api";

export async function generateCertificatesAction(eventId: string, _: unknown, _formData: FormData) {
  const session = await requireSession();
  try {
    const res = await generateCertificates(session.accessToken, eventId);
    revalidatePath(`/host/events/${eventId}/certificates`);
    return { ok: true as const, generated: res.generated };
  } catch (err) {
    return { error: apiErrorMessage(err) };
  }
}

export async function revokeCertificateAction(certificateId: string, eventId: string, formData: FormData) {
  const session = await requireSession();
  const reason = (formData.get("reason") as string | null)?.trim() || "Revoked by organizer";
  try {
    await revokeCertificate(session.accessToken, certificateId, reason);
  } catch {
    // swallow — the roster re-render reflects the (unchanged) state
  }
  revalidatePath(`/host/events/${eventId}/certificates`);
}
