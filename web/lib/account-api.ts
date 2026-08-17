import { z } from "zod";
import { api } from "@/lib/api";

/// Typed access to the account-settings contract (D-263) and direct messages (D-264).
///
/// Keys are **snake_case**: every response DTO lives in `Kurx.Application.Abstractions`, the namespace
/// `SnakeCaseResponseConverter` keys off (D-259). Request bodies stay camelCase — the converter
/// delegates Read, so the platform contract is camelCase in, snake_case out.

function authHeaders(accessToken: string) {
  return { headers: { Authorization: `Bearer ${accessToken}` } };
}

// ── Notification preferences ─────────────────────────────────────────────────

/// Mirrors the backend `NotificationCategory` enum. Order is the render order of the settings screen,
/// grouped roughly by how often a person cares about them.
export const NOTIFICATION_CATEGORIES = [
  "messages",
  "connection_requests",
  "posts",
  "event_updates",
  "announcements",
  "invitations",
  "staff_invitations",
  "team_invitations",
  "payments",
  "certificates",
  "security",
  "system"
] as const;
export type NotificationCategory = (typeof NOTIFICATION_CATEGORIES)[number];

export const CATEGORY_LABEL: Record<string, string> = {
  messages: "Messages",
  connection_requests: "Connection requests",
  posts: "Posts",
  event_updates: "Event updates",
  announcements: "Announcements",
  invitations: "Invitations",
  staff_invitations: "Staff invitations",
  team_invitations: "Team invitations",
  payments: "Payments & refunds",
  certificates: "Certificates",
  security: "Security",
  system: "System"
};

/// `locked` categories cannot be switched off. The server refuses the write regardless of what the UI
/// renders — this flag exists so the switch looks disabled rather than failing on tap.
export const notificationPreferenceSchema = z.object({
  category: z.string(),
  in_app: z.boolean(),
  push: z.boolean(),
  email: z.boolean(),
  whats_app: z.boolean(),
  locked: z.boolean().default(false)
});
export type NotificationPreference = z.infer<typeof notificationPreferenceSchema>;

export const notificationPreferencesSchema = z.object({
  categories: z.array(notificationPreferenceSchema).default([])
});
export type NotificationPreferences = z.infer<typeof notificationPreferencesSchema>;

export async function getNotificationPreferences(accessToken: string): Promise<NotificationPreferences> {
  const { data } = await api.get("/v1/me/notification-preferences", authHeaders(accessToken));
  return notificationPreferencesSchema.parse(data);
}

export type NotificationPreferenceChange = {
  category: string;
  inApp?: boolean;
  push?: boolean;
  email?: boolean;
  whatsApp?: boolean;
};

export async function updateNotificationPreferences(
  accessToken: string,
  categories: NotificationPreferenceChange[]
): Promise<NotificationPreferences> {
  const { data } = await api.patch("/v1/me/notification-preferences", { categories }, authHeaders(accessToken));
  return notificationPreferencesSchema.parse(data);
}

// ── Blocks ───────────────────────────────────────────────────────────────────

export const blockedUserSchema = z.object({
  user_id: z.string(),
  name: z.string(),
  username: z.string().nullable().optional(),
  avatar_key: z.string().nullable().optional(),
  /// Presigned companion (D-302) — what renders; the key alone is not fetchable.
  avatar_url: z.string().nullable().optional(),
  created_at: z.string()
});
export type BlockedUser = z.infer<typeof blockedUserSchema>;

export async function listBlocks(accessToken: string): Promise<BlockedUser[]> {
  const { data } = await api.get("/v1/me/blocks", authHeaders(accessToken));
  return z.array(blockedUserSchema).parse(data);
}

export async function blockUser(accessToken: string, userId: string): Promise<void> {
  await api.post(`/v1/me/blocks/${userId}`, {}, authHeaders(accessToken));
}

export async function unblockUser(accessToken: string, userId: string): Promise<void> {
  await api.delete(`/v1/me/blocks/${userId}`, authHeaders(accessToken));
}

// ── Username history ─────────────────────────────────────────────────────────

export const usernameHistorySchema = z.object({
  username: z.string(),
  released_at: z.string()
});
export type UsernameHistoryEntry = z.infer<typeof usernameHistorySchema>;

export async function getUsernameHistory(accessToken: string): Promise<UsernameHistoryEntry[]> {
  const { data } = await api.get("/v1/me/username-history", authHeaders(accessToken));
  return z.array(usernameHistorySchema).parse(data);
}

// ── Email change ─────────────────────────────────────────────────────────────

export async function startEmailChange(accessToken: string, newEmail: string): Promise<void> {
  await api.post("/v1/me/email/change/start", { newEmail }, authHeaders(accessToken));
}

export const emailChangeSchema = z.object({
  email: z.string().nullable().optional(),
  email_verified: z.boolean().default(false)
});

export async function completeEmailChange(accessToken: string, newEmail: string, code: string) {
  const { data } = await api.post("/v1/me/email/change/complete", { newEmail, code }, authHeaders(accessToken));
  return emailChangeSchema.parse(data);
}

// ── Deletion ─────────────────────────────────────────────────────────────────

export const accountDeletionSchema = z.object({
  pending: z.boolean(),
  scheduled_for: z.string().nullable().optional(),
  requested_at: z.string().nullable().optional()
});
export type AccountDeletion = z.infer<typeof accountDeletionSchema>;

/// Null when nothing is scheduled. The API answers 404 for that case rather than `pending: false`, so
/// a client cannot mistake "never requested" for "cancelled" — this maps that to a plain absence.
export async function getAccountDeletion(accessToken: string): Promise<AccountDeletion | null> {
  try {
    const { data } = await api.get("/v1/me/deletion", authHeaders(accessToken));
    return accountDeletionSchema.parse(data);
  } catch (err) {
    if ((err as { response?: { status?: number } })?.response?.status === 404) return null;
    throw err;
  }
}

export async function requestAccountDeletion(accessToken: string, reason?: string): Promise<AccountDeletion> {
  const { data } = await api.post("/v1/me/deletion", { reason }, authHeaders(accessToken));
  return accountDeletionSchema.parse(data);
}

export async function cancelAccountDeletion(accessToken: string): Promise<void> {
  await api.delete("/v1/me/deletion", authHeaders(accessToken));
}

// ── Direct messages (D-264) ──────────────────────────────────────────────────

export const dmRoomSchema = z.object({
  room_id: z.string(),
  other_user_id: z.string(),
  other_name: z.string(),
  other_username: z.string().nullable().optional(),
  other_avatar_key: z.string().nullable().optional(),
  other_avatar_url: z.string().nullable().optional(),
  request_state: z.string(),
  is_request: z.boolean().default(false),
  archived: z.boolean().default(false),
  last_message_preview: z.string().nullable().optional(),
  unread_count: z.number().default(0),
  last_activity: z.string().nullable().optional(),
  // D-295. Defaulted so an older backend simply reports neither.
  pinned: z.boolean().default(false),
  notifications_muted: z.boolean().default(false)
});
export type DmRoom = z.infer<typeof dmRoomSchema>;

/// Returns the room id. Idempotent server-side, so calling it twice is safe and cheap — the client
/// never needs to check whether a conversation already exists.
export async function openDm(accessToken: string, userId: string): Promise<string> {
  const { data } = await api.post(`/v1/dm/${userId}`, {}, authHeaders(accessToken));
  return z.object({ room_id: z.string() }).parse(data).room_id;
}

export async function listDms(accessToken: string, archived = false): Promise<DmRoom[]> {
  const { data } = await api.get("/v1/me/dm", { ...authHeaders(accessToken), params: { archived } });
  return z.array(dmRoomSchema).parse(data);
}

export async function listDmRequests(accessToken: string): Promise<DmRoom[]> {
  const { data } = await api.get("/v1/me/dm/requests", authHeaders(accessToken));
  return z.array(dmRoomSchema).parse(data);
}

export async function respondToDmRequest(accessToken: string, roomId: string, accept: boolean): Promise<void> {
  await api.post(`/v1/dm/${roomId}/requests/${accept ? "accept" : "decline"}`, {}, authHeaders(accessToken));
}

export async function setDmArchived(accessToken: string, roomId: string, archived: boolean): Promise<void> {
  if (archived) await api.post(`/v1/dm/${roomId}/archive`, {}, authHeaders(accessToken));
  else await api.delete(`/v1/dm/${roomId}/archive`, authHeaders(accessToken));
}

// ── Language (D-263) ─────────────────────────────────────────────────────────

/// Extends the existing profile PATCH rather than adding a route. `en` | `hi`; anything else is
/// refused server-side as `invalid_language` rather than silently falling back.
export async function setLanguage(accessToken: string, language: string): Promise<void> {
  await api.patch("/v1/me/profile", { language }, authHeaders(accessToken));
}
