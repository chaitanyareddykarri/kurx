"use server";

import { requireSession } from "@/lib/session";
import { scanGate, apiErrorMessage } from "@/lib/api";

/// Admit a ticket at the gate (organizer check-in). Returns the scan outcome; a rejected/duplicate scan is
/// a normal result the UI shows, not a thrown error.
export async function scanTicketAction(eventId: string, code: string) {
  const session = await requireSession();
  try {
    const r = await scanGate(session.accessToken, eventId, code.trim());
    return { ok: true as const, admitted: r.admitted, isDuplicate: r.is_duplicate, message: r.message };
  } catch (err) {
    return { ok: false as const, error: apiErrorMessage(err) };
  }
}
