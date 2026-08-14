import { z } from "zod";
import { api } from "@/lib/api";

/// Typed access to the frozen chat contract (D-104). Mirrors `lib/api.ts`: one function per
/// endpoint, zod-validated, accessToken passed in — never read from a module-level singleton.
///
/// **Wire format is snake_case; the types here are camelCase (D-292).** The comment this replaces said
/// chat endpoints answer in ASP.NET's default camelCase, and that was true until D-259's addendum added
/// `SnakeCaseResponseConverter` — which applies to every record in `Kurx.Application.Abstractions`,
/// including `MyChatView` and `ChatRoomView`. Nothing updated this file, so `z.string()` met `undefined`
/// and **every parse threw**: `/chats` catches the rejection and renders "No event chats", so the Events
/// tab was silently empty for everyone rather than visibly broken. Verified against `docs/api/openapi.json`,
/// which publishes `room_id` / `event_title` / `presence_enabled`. Exactly the D-245 failure again.
///
/// Keys are normalised on the way in rather than the schemas rewritten, because the camelCase names are
/// this module's public surface and sixty-odd references across fifteen files consume them.

function authHeaders(accessToken: string) {
  return { headers: { Authorization: `Bearer ${accessToken}` } };
}

/// snake_case → camelCase over a parsed JSON tree, applied before every zod parse below.
///
/// Only `_x` sequences are rewritten, so header maps (`Content-Type`) and any other hyphenated or
/// already-camel key pass through untouched.
function camelizeKeys<T>(value: T): T {
  if (Array.isArray(value)) return value.map((v) => camelizeKeys(v)) as unknown as T;
  if (value === null || typeof value !== "object") return value;
  return Object.fromEntries(
    Object.entries(value as Record<string, unknown>).map(([key, v]) => [
      key.replace(/_([a-z0-9])/g, (_m, c: string) => c.toUpperCase()),
      camelizeKeys(v)
    ])
  ) as T;
}

export const chatCapabilitiesSchema = z.object({
  canPost: z.boolean(),
  canReply: z.boolean(),
  canUpload: z.boolean(),
  canPin: z.boolean(),
  canDelete: z.boolean(),
  canModerate: z.boolean(),
  canMentionAll: z.boolean(),
  // D-301. Defaulted, not required: an older server omits them and a hard requirement would fail the
  // whole parse — the failure mode D-292 already cost this surface once.
  //
  // `canManageRoom` and `canManageModerators` are deliberately NOT implied by `canModerate`. A UI that
  // rendered lock-room off canModerate alone would offer a Moderator a control the server refuses.
  canManageRoom: z.boolean().default(false),
  canManageModerators: z.boolean().default(false),
  myRole: z.string().default("Member")
});
export type ChatCapabilities = z.infer<typeof chatCapabilitiesSchema>;

export const chatAttachmentSchema = z.object({
  id: z.string(),
  url: z.string(),
  fileName: z.string(),
  contentType: z.string(),
  sizeBytes: z.number(),
  width: z.number().nullable().optional(),
  height: z.number().nullable().optional()
});
export type ChatAttachment = z.infer<typeof chatAttachmentSchema>;

export const attachmentTicketSchema = z.object({
  key: z.string(),
  url: z.string(),
  headers: z.record(z.string())
});
export type AttachmentTicket = z.infer<typeof attachmentTicketSchema>;

/// D-295 — one emoji on a message. `mine` is server-computed for the requesting caller, so the client
/// renders the toggled state without scanning the id list.
export const chatReactionSchema = z.object({
  emoji: z.string(),
  count: z.number(),
  mine: z.boolean().default(false),
  userIds: z.array(z.string()).default([])
});
export type ChatReaction = z.infer<typeof chatReactionSchema>;

/// `host` is derived server-side from the URL and is the one field a sender cannot fake — render it,
/// so a card may misdescribe the page but never mislead about where the tap goes.
export const chatLinkPreviewSchema = z.object({
  url: z.string(),
  host: z.string(),
  title: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  imageUrl: z.string().nullable().optional()
});
export type ChatLinkPreview = z.infer<typeof chatLinkPreviewSchema>;

export const chatSearchHitSchema = z.object({
  messageId: z.string(),
  roomId: z.string(),
  roomLabel: z.string(),
  senderId: z.string().nullable().optional(),
  senderName: z.string().nullable().optional(),
  body: z.string(),
  createdAt: z.string()
});
export type ChatSearchHit = z.infer<typeof chatSearchHitSchema>;

export const chatMessageSchema = z.object({
  id: z.string(),
  clientMessageId: z.string().nullable().optional(),
  roomId: z.string(),
  senderId: z.string().nullable().optional(),
  senderName: z.string().nullable().optional(),
  senderRole: z.string().nullable().optional(),
  kind: z.string(),
  body: z.string(),
  replyToMessageId: z.string().nullable().optional(),
  isPinned: z.boolean(),
  isDeleted: z.boolean(),
  attachments: z.array(chatAttachmentSchema).default([]),
  createdAt: z.string(),
  // D-293. Optional so an older server simply reports no edit rather than failing the parse.
  editedAt: z.string().nullable().optional(),
  // D-295. All optional for the same reason — an older server omits them and the parse still holds.
  reactions: z.array(chatReactionSchema).nullable().optional(),
  forwardedFrom: z
    .object({ messageId: z.string(), senderName: z.string().nullable().optional() })
    .nullable()
    .optional(),
  linkPreview: chatLinkPreviewSchema.nullable().optional(),
  // D-296. `isPinned` above is already computed against this instant server-side, so this is only ever
  // used to say WHEN the pin lapses — never to decide whether it still holds.
  pinnedUntil: z.string().nullable().optional()
});
export type ChatMessage = z.infer<typeof chatMessageSchema>;

export const chatRoomSchema = z.object({
  roomId: z.string(),
  kind: z.string(),
  status: z.string(),
  postPolicy: z.string(),
  myRole: z.string(),
  mutedUntil: z.string().nullable().optional(),
  unreadCount: z.number(),
  capabilities: chatCapabilitiesSchema,
  pinnedMessages: z.array(chatMessageSchema).default([]),
  // Presence (D-114). Defaulted rather than required: zod strips unknown keys, so without these the
  // fields would be silently dropped, and an older server simply reports presence as unavailable.
  onlineUserIds: z.array(z.string()).default([]),
  presenceEnabled: z.boolean().default(false),
  // Direct-room request gate (D-292). Null on an event room, so optional rather than defaulted — the
  // absence is meaningful and must not be flattened into a state. `capabilities.canPost` already
  // accounts for a declined room; these exist so the client can render Accept/Decline on a pending one
  // instead of a composer, and tell "your request" from "their request" via dmInitiatedBy.
  dmRequestState: z.enum(["pending", "accepted", "declined"]).nullable().optional(),
  dmInitiatedBy: z.string().nullable().optional()
});
export type ChatRoom = z.infer<typeof chatRoomSchema>;


export const chatMessagePageSchema = z.object({
  messages: z.array(chatMessageSchema),
  olderCursor: z.string().nullable().optional(),
  newerCursor: z.string().nullable().optional()
});
export type ChatMessagePage = z.infer<typeof chatMessagePageSchema>;

export const myChatSchema = z.object({
  roomId: z.string(),
  eventId: z.string(),
  eventTitle: z.string(),
  bannerUrl: z.string().nullable().optional(),
  lastMessagePreview: z.string().nullable().optional(),
  unreadCount: z.number(),
  lastActivity: z.string().nullable().optional(),
  // D-295. Defaulted rather than required so a client pinned to an older backend still parses.
  pinned: z.boolean().default(false),
  notificationsMuted: z.boolean().default(false),
  // D-306 — filed out of the active list by this reader. Defaulted for the same reason as the two above.
  archived: z.boolean().default(false)
});
export type MyChat = z.infer<typeof myChatSchema>;

/// D-306 — `archived` selects which side of the reader's own filing to return, mirroring
/// `/v1/me/dm?archived=`. Omitted means the active list, which is what every existing caller wants.
export async function listMyChats(accessToken: string, archived = false) {
  const { data } = await api.get("/v1/me/chats", {
    ...authHeaders(accessToken),
    params: { archived }
  });
  return z.array(myChatSchema).parse(camelizeKeys(data));
}

/// One room by its own id (D-292). **This is the navigation primitive** — a direct room has no event,
/// so keying the client on eventId made every DM row a dead link. Both kinds resolve through here.
export async function getChatRoomById(accessToken: string, roomId: string) {
  const { data } = await api.get(`/v1/chat/rooms/${roomId}`, authHeaders(accessToken));
  return chatRoomSchema.parse(camelizeKeys(data));
}

/// Event-addressed lookup, kept for callers that hold an eventId and not a roomId. Prefer
/// `getChatRoomById`; this exists so an existing deep link keeps resolving.
export async function getChatRoom(accessToken: string, eventId: string) {
  const { data } = await api.get(`/v1/events/${eventId}/chat`, authHeaders(accessToken));
  return chatRoomSchema.parse(camelizeKeys(data));
}

/// Keyset pagination. At most one of before/after — the server rejects both with `cursor_conflict`.
/// Cursors are opaque and are passed back exactly as received.
export async function listChatMessages(
  accessToken: string,
  roomId: string,
  opts: { before?: string; after?: string; limit?: number } = {}
) {
  const { data } = await api.get(`/v1/chat/rooms/${roomId}/messages`, {
    ...authHeaders(accessToken),
    params: {
      ...(opts.before ? { before: opts.before } : {}),
      ...(opts.after ? { after: opts.after } : {}),
      limit: opts.limit ?? 50
    }
  });
  return chatMessagePageSchema.parse(camelizeKeys(data));
}

/// Idempotent on clientMessageId: a retry returns the message the first attempt created rather than
/// a duplicate. Generate the id before the first attempt, never per-attempt.
export async function sendChatMessage(
  accessToken: string,
  roomId: string,
  input: { body: string; clientMessageId: string; replyToMessageId?: string; attachmentIds?: string[] }
) {
  const { data } = await api.post(`/v1/chat/rooms/${roomId}/messages`, input, authHeaders(accessToken));
  return chatMessageSchema.parse(camelizeKeys(data));
}

/// The read pointer is a message id, not a timestamp, and the server only moves it forward.
export async function markChatRead(accessToken: string, roomId: string, lastReadMessageId: string) {
  await api.post(`/v1/chat/rooms/${roomId}/read`, { lastReadMessageId }, authHeaders(accessToken));
}

/// D-293 — edit your own message. Returns the updated view; the server refuses a no-op with 409.
export async function editChatMessage(accessToken: string, messageId: string, body: string) {
  const { data } = await api.patch(`/v1/chat/messages/${messageId}`, { body }, authHeaders(accessToken));
  return chatMessageSchema.parse(camelizeKeys(data));
}

/// Delete for EVERYONE — redacts the message for the whole room.
export async function deleteChatMessage(accessToken: string, messageId: string) {
  await api.delete(`/v1/chat/messages/${messageId}`, authHeaders(accessToken));
}

/// D-293 — delete for ME. Hides it from this reader only; nobody else's view changes.
export async function hideChatMessage(accessToken: string, messageId: string) {
  await api.delete(`/v1/chat/messages/${messageId}/for-me`, authHeaders(accessToken));
}

// ── D-295 ─────────────────────────────────────────────────────────────────────

/// One call, not add/remove: the server decides whether a tap means on or off, so two devices can
/// never disagree about which to send. Returns the message's whole reaction summary.
export async function toggleChatReaction(accessToken: string, messageId: string, emoji: string) {
  const { data } = await api.post(
    `/v1/chat/messages/${messageId}/reactions`,
    { emoji },
    authHeaders(accessToken)
  );
  return z.array(chatReactionSchema).parse(camelizeKeys(data));
}

/// Scoped server-side to the rooms the caller belongs to. `roomId` narrows it to one conversation.
export async function searchChatMessages(
  accessToken: string,
  q: string,
  opts: { roomId?: string; limit?: number } = {}
) {
  const { data } = await api.get(`/v1/chat/search`, {
    ...authHeaders(accessToken),
    params: { q, ...(opts.roomId ? { roomId: opts.roomId } : {}), limit: opts.limit ?? 25 }
  });
  return z.array(chatSearchHitSchema).parse(camelizeKeys(data));
}

/// The ✓✓ before a read receipt. Forward-only server-side, so a late call is harmless.
export async function markChatDelivered(accessToken: string, roomId: string, messageId: string) {
  await api.post(`/v1/chat/rooms/${roomId}/delivered`, { messageId }, authHeaders(accessToken));
}

/// Attachments are deliberately not carried by a forward — the server refuses a file-only message.
export async function forwardChatMessage(
  accessToken: string,
  messageId: string,
  targetRoomId: string,
  clientMessageId?: string
) {
  const { data } = await api.post(
    `/v1/chat/messages/${messageId}/forward`,
    { targetRoomId, clientMessageId },
    authHeaders(accessToken)
  );
  return chatMessageSchema.parse(camelizeKeys(data));
}

/// Personal filing, like archiving — says nothing to anyone else in the room.
export async function setChatPinned(accessToken: string, roomId: string, pinned: boolean) {
  const url = `/v1/chat/rooms/${roomId}/pin`;
  if (pinned) await api.post(url, {}, authHeaders(accessToken));
  else await api.delete(url, authHeaders(accessToken));
}

/// Files a room out of the caller's active list, or brings it back (D-295/D-306).
///
/// The ROOM-scoped route, not `/v1/dm/{id}/archive`. Both reach the same `SetArchivedAsync`, but only
/// this one accepts an event room — and until D-306 gave `/v1/me/chats` an `archived` parameter there
/// was no way to list what it hid, so archiving an event chat put it beyond reach. That is why this
/// function did not exist while its endpoint did.
export async function setChatArchived(accessToken: string, roomId: string, archived: boolean) {
  const url = `/v1/chat/rooms/${roomId}/archive`;
  if (archived) await api.post(url, {}, authHeaders(accessToken));
  else await api.delete(url, authHeaders(accessToken));
}

/// Silences the caller's OWN notifications. Not the host mute, which stops a member posting.
export async function setChatMuted(accessToken: string, roomId: string, until: string | null) {
  const url = `/v1/chat/rooms/${roomId}/mute`;
  if (until) await api.post(url, { until }, authHeaders(accessToken));
  else await api.delete(url, authHeaders(accessToken));
}

/// The shared-media grid: every live attachment in a room, newest first.
export async function listRoomMedia(accessToken: string, roomId: string, limit = 60) {
  const { data } = await api.get(`/v1/chat/rooms/${roomId}/media`, {
    ...authHeaders(accessToken),
    params: { limit }
  });
  return z.array(chatAttachmentSchema).parse(camelizeKeys(data));
}

export async function reportChatMessage(accessToken: string, messageId: string, reason: string) {
  await api.post(`/v1/chat/messages/${messageId}/report`, { reason }, authHeaders(accessToken));
}

// ── Host moderation ────────────────────────────────────────────────────────────────────────

// ── Attachments (D-110) ───────────────────────────────────────────────────────────────────

export async function presignChatAttachment(
  accessToken: string,
  roomId: string,
  input: { fileName: string; contentType: string; sizeBytes: number }
) {
  const { data } = await api.post(
    `/v1/chat/rooms/${roomId}/attachments/presign`,
    input,
    authHeaders(accessToken)
  );
  return attachmentTicketSchema.parse(camelizeKeys(data));
}

export async function confirmChatAttachment(accessToken: string, roomId: string, storageKey: string) {
  const { data } = await api.post(
    `/v1/chat/rooms/${roomId}/attachments/confirm`,
    { storageKey },
    authHeaders(accessToken)
  );
  return chatAttachmentSchema.parse(camelizeKeys(data));
}

/// Fresh signed download URL. Minted per use — the signature expires and the server re-checks room
/// membership every time, so a cached URL would fail in a way the user cannot act on.
export async function getChatAttachmentUrl(accessToken: string, attachmentId: string) {
  const { data } = await api.get(`/v1/chat/attachments/${attachmentId}/url`, authHeaders(accessToken));
  return z.object({ url: z.string() }).parse(data).url;
}

/// D-296 — `durationHours` is how long the pin lasts. Omitted takes the server's 7-day default.
/// D-301 — promote a Member to chat Moderator, or demote one. Hosts only; the server re-checks.
export async function setChatModerator(
  accessToken: string,
  roomId: string,
  userId: string,
  moderator: boolean
) {
  const url = `/v1/chat/rooms/${roomId}/members/${userId}/moderator`;
  if (moderator) await api.post(url, {}, authHeaders(accessToken));
  else await api.delete(url, authHeaders(accessToken));
}

export async function pinChatMessage(
  accessToken: string,
  messageId: string,
  pin: boolean,
  durationHours?: number
) {
  if (pin)
    await api.post(
      `/v1/chat/messages/${messageId}/pin`,
      { durationHours: durationHours ?? null },
      authHeaders(accessToken)
    );
  else await api.delete(`/v1/chat/messages/${messageId}/pin`, authHeaders(accessToken));
}

/// D-296 — the pin windows offered in the UI, mirroring the server's 1-hour..30-day bounds. Kept here
/// so web and mobile offer the same menu rather than each inventing its own.
export const PIN_DURATIONS = [
  { label: "24 hours", hours: 24 },
  { label: "7 days", hours: 24 * 7 },
  { label: "30 days", hours: 24 * 30 }
] as const;

export async function muteChatMember(
  accessToken: string,
  roomId: string,
  userId: string,
  minutes: number
) {
  await api.post(
    `/v1/chat/rooms/${roomId}/members/${userId}/mute`,
    { minutes },
    authHeaders(accessToken)
  );
}

export async function banChatMember(accessToken: string, roomId: string, userId: string) {
  await api.post(`/v1/chat/rooms/${roomId}/members/${userId}/ban`, {}, authHeaders(accessToken));
}

export async function updateChatRoom(
  accessToken: string,
  roomId: string,
  input: { postPolicy?: string; status?: string }
) {
  await api.patch(`/v1/chat/rooms/${roomId}`, input, authHeaders(accessToken));
}
