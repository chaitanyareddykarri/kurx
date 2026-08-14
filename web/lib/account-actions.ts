"use server";

import { revalidatePath } from "next/cache";
import { requireSession } from "@/lib/session";
import { apiErrorMessage } from "@/lib/api";
import {
  updateNotificationPreferences, blockUser, unblockUser,
  startEmailChange, completeEmailChange,
  requestAccountDeletion, cancelAccountDeletion,
  respondToDmRequest, setDmArchived, openDm, setLanguage,
  type NotificationPreferenceChange
} from "@/lib/account-api";

/// Server actions for account settings (D-263) and direct messages (D-264). One per user-visible
/// operation, each returning `{ ok }` or `{ error }` — the shape the settings forms already read.

export type ActionResult = { ok: true } | { error: string } | { ok: true; roomId: string };

function fail(err: unknown): { error: string } {
  return { error: apiErrorMessage(err) };
}

// ── Notification preferences ─────────────────────────────────────────────────

export async function updateNotificationPreferenceAction(
  category: string,
  channel: "inApp" | "push" | "email" | "whatsApp",
  value: boolean
): Promise<ActionResult> {
  const session = await requireSession();
  const change = { category, [channel]: value } as NotificationPreferenceChange;
  try {
    await updateNotificationPreferences(session.accessToken, [change]);
  } catch (err) {
    // `category_not_optional` lands here when something tries to mute security. The switch is already
    // rendered disabled; this is the server having the last word anyway.
    return fail(err);
  }
  revalidatePath("/settings/notifications");
  return { ok: true };
}

// ── Blocks ───────────────────────────────────────────────────────────────────

export async function blockUserAction(userId: string): Promise<ActionResult> {
  const session = await requireSession();
  try {
    await blockUser(session.accessToken, userId);
  } catch (err) {
    return fail(err);
  }
  // A block changes what the feed and the messages list return, so both are stale now.
  revalidatePath("/settings/privacy");
  revalidatePath("/posts");
  revalidatePath("/chats");
  return { ok: true };
}

export async function unblockUserAction(userId: string): Promise<ActionResult> {
  const session = await requireSession();
  try {
    await unblockUser(session.accessToken, userId);
  } catch (err) {
    return fail(err);
  }
  revalidatePath("/settings/privacy");
  revalidatePath("/posts");
  return { ok: true };
}

// ── Email change ─────────────────────────────────────────────────────────────

export async function startEmailChangeAction(_: unknown, formData: FormData): Promise<ActionResult & { email?: string }> {
  const session = await requireSession();
  const email = String(formData.get("email") ?? "").trim();
  if (!email) return { error: "Enter an email address." };
  try {
    await startEmailChange(session.accessToken, email);
  } catch (err) {
    return fail(err);
  }
  return { ok: true, email };
}

export async function completeEmailChangeAction(_: unknown, formData: FormData): Promise<ActionResult> {
  const session = await requireSession();
  const email = String(formData.get("email") ?? "").trim();
  const code = String(formData.get("code") ?? "").trim();
  if (!email || !code) return { error: "Enter the new email and the code." };
  try {
    await completeEmailChange(session.accessToken, email, code);
  } catch (err) {
    return fail(err);
  }
  revalidatePath("/settings/account");
  return { ok: true };
}

// ── Deletion ─────────────────────────────────────────────────────────────────

export async function requestAccountDeletionAction(_: unknown, formData: FormData): Promise<ActionResult> {
  const session = await requireSession();
  // The confirm phrase is a client-side speed bump, not a security control — the server's step-up gate
  // is the real one. It exists so a mis-tap cannot start a 30-day clock.
  if (String(formData.get("confirm") ?? "").trim().toUpperCase() !== "DELETE")
    return { error: 'Type DELETE to confirm.' };
  try {
    await requestAccountDeletion(session.accessToken, String(formData.get("reason") ?? "").trim() || undefined);
  } catch (err) {
    return fail(err);
  }
  revalidatePath("/settings/account");
  return { ok: true };
}

export async function cancelAccountDeletionAction(): Promise<ActionResult> {
  const session = await requireSession();
  try {
    await cancelAccountDeletion(session.accessToken);
  } catch (err) {
    return fail(err);
  }
  revalidatePath("/settings/account");
  return { ok: true };
}

// ── Direct messages ──────────────────────────────────────────────────────────

export async function openDmAction(userId: string): Promise<ActionResult> {
  const session = await requireSession();
  try {
    const roomId = await openDm(session.accessToken, userId);
    revalidatePath("/chats");
    return { ok: true, roomId };
  } catch (err) {
    return fail(err);
  }
}

export async function respondToDmRequestAction(roomId: string, accept: boolean): Promise<ActionResult> {
  const session = await requireSession();
  try {
    await respondToDmRequest(session.accessToken, roomId, accept);
  } catch (err) {
    return fail(err);
  }
  revalidatePath("/chats");
  return { ok: true };
}

export async function setDmArchivedAction(roomId: string, archived: boolean): Promise<ActionResult> {
  const session = await requireSession();
  try {
    await setDmArchived(session.accessToken, roomId, archived);
  } catch (err) {
    return fail(err);
  }
  revalidatePath("/chats");
  return { ok: true };
}

// ── Language ─────────────────────────────────────────────────────────────────

/// Persists the UI language on the account (D-263). The cookie is what makes the switch feel
/// instant; this is what makes it survive a new browser or the mobile app.
///
/// Deliberately tolerant of a signed-out caller: `requireSession` throws, the switcher swallows it,
/// and the cookie-only behaviour that existed before this is exactly what remains.
export async function setLanguageAction(language: string): Promise<ActionResult> {
  const session = await requireSession();
  try {
    await setLanguage(session.accessToken, language);
  } catch (err) {
    return fail(err);
  }
  return { ok: true };
}
