"use server";

import { revalidatePath } from "next/cache";
import { requireSession } from "@/lib/session";
import { addInvitation, sendInvitations, revokeInvitation, resendInvitation, importInvitationsCsv, apiErrorMessage,
  respondToInvitation, redeemInviteLink, searchUsersForInvite } from "@/lib/api";

function str(formData: FormData, key: string): string | undefined {
  const v = formData.get(key);
  return typeof v === "string" && v.length > 0 ? v : undefined;
}

export async function addInvitationAction(eventId: string, _: unknown, formData: FormData) {
  const session = await requireSession();
  const email = str(formData, "email");
  const phone = str(formData, "phone");
  if (!email && !phone) return { error: "Provide an email or phone." };
  try {
    await addInvitation(session.accessToken, eventId, {
      name: str(formData, "name"),
      email,
      phone,
      channel: str(formData, "channel") ?? "Email"
    });
  } catch (err) {
    return { error: apiErrorMessage(err) };
  }
  revalidatePath("/host/invitations");
  return { ok: true };
}

export async function sendInvitationsAction(eventId: string) {
  const session = await requireSession();
  await sendInvitations(session.accessToken, eventId);
  revalidatePath("/host/invitations");
}

export async function revokeInvitationAction(invitationId: string, _eventId: string) {
  const session = await requireSession();
  await revokeInvitation(session.accessToken, invitationId);
  revalidatePath("/host/invitations");
}

export async function resendInvitationAction(invitationId: string, eventId: string) {
  const session = await requireSession();
  await resendInvitation(session.accessToken, invitationId);
  revalidatePath(`/host/events/${eventId}/invitations`);
}

export async function importInvitationsAction(eventId: string, _: unknown, formData: FormData) {
  const file = formData.get("file");
  if (!(file instanceof File) || file.size === 0) return;
  const session = await requireSession();
  try {
    await importInvitationsCsv(session.accessToken, eventId, file);
  } catch {
    /* backend validation errors surface on next load */
  }
  revalidatePath(`/host/events/${eventId}/invitations`);
}

// ── D-266 M6 · the invitee's side ───────────────────────────────────────────────────────────

/** Server refusal codes → copy a guest can act on. Unmapped codes fall through verbatim so an unexpected
 *  refusal stays diagnosable rather than becoming "something went wrong". */
const INVITE_ERRORS: Record<string, string> = {
  not_invited: "This invitation isn't yours, or it has been withdrawn.",
  invitation_already_responded: "You've already answered this invitation.",
  invite_link_expired: "This invite link has expired. Ask the organiser for a new one.",
  invite_link_revoked: "The organiser has turned this link off.",
  invite_link_exhausted: "Every place on this link has been taken.",
  invite_link_passcode_required: "This link needs a passcode.",
  invite_link_passcode_invalid: "That passcode isn't right.",
};

export async function respondToInvitationAction(invitationId: string, accept: boolean) {
  const session = await requireSession();
  try {
    await respondToInvitation(session.accessToken, invitationId, accept);
  } catch (err) {
    const code = apiErrorMessage(err);
    return { error: INVITE_ERRORS[code] ?? code };
  }
  revalidatePath("/invitations");
  return { ok: true as const };
}

export async function redeemInviteLinkAction(token: string, passcode?: string) {
  const session = await requireSession();
  try {
    await redeemInviteLink(session.accessToken, token, passcode);
  } catch (err) {
    const code = apiErrorMessage(err);
    return { error: INVITE_ERRORS[code] ?? code };
  }
  revalidatePath(`/i/${token}`);
  return { ok: true as const };
}

/** D-266 M6 (D9 Method A) — username lookup for the invite picker. Reads the same index as the public
 *  profile search; there is deliberately no second user index. */
export async function searchUsersAction(q: string) {
  const session = await requireSession();
  try {
    return { users: await searchUsersForInvite(session.accessToken, q) };
  } catch (err) {
    return { error: apiErrorMessage(err) };
  }
}

/** Invites a Kurx user by username. Reuses the SAME endpoint the email/phone form posts to — Method A is a
 *  delivery method inside one policy, not a second invitation system, so it must not get a second route. */
export async function inviteByUsernameAction(eventId: string, username: string) {
  const session = await requireSession();
  try {
    await addInvitation(session.accessToken, eventId, {
      // No name/email/phone: the invited user IS the address (D9 rule 3). The server fills the display
      // name from their account, falling back to @username.
      channel: "Email", username,
    });
  } catch (err) {
    const code = apiErrorMessage(err);
    return { error: INVITE_ERRORS[code] ?? (code === "duplicate_invitation" ? "They're already invited." : code) };
  }
  revalidatePath(`/host/events/${eventId}/invitations`);
  return { ok: true as const };
}
