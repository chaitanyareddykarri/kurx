import type { ChatMessage } from "@/lib/chat-api";

/// Framework-free chat logic, kept out of React so it can be tested directly and so web and Flutter
/// cannot drift. Every rule here mirrors the Dart implementation in
/// `mobile/lib/core/storage/chat_cache.dart` and `chat_repository_impl.dart`.

export type SendStatus = "sent" | "pending" | "failed";

/// A message plus the local send state. Server messages are always `sent`; only messages this
/// browser originated are ever `pending` or `failed`.
export type LocalChatMessage = ChatMessage & { status: SendStatus };

export function toLocal(message: ChatMessage, status: SendStatus = "sent"): LocalChatMessage {
  return { ...message, status };
}

/// Total order over a room's messages: `(createdAt, id)`, never `createdAt` alone.
///
/// Server ids are UUIDv7 and therefore k-sortable, but rows created before D-104 hold v4 ids — and
/// `createdAt` is not unique, because DateTime.UtcNow has ~15ms resolution. The pair is the same
/// sort key the backend's keyset pagination uses; agreeing with it is what keeps merge, paging and
/// scroll position consistent.
export function compareMessages(a: LocalChatMessage, b: LocalChatMessage): number {
  const byTime = a.createdAt.localeCompare(b.createdAt);
  return byTime !== 0 ? byTime : a.id.localeCompare(b.id);
}

/// Merges `incoming` into `existing`, de-duplicating and re-sorting.
///
/// De-duplication is by server id **and** by `clientMessageId`. When the server echoes a message
/// this browser sent, the optimistic row and the confirmed row are one message under two
/// identities; collapsing them is what stops a self-sent message rendering twice, and what makes a
/// reconnect (which re-fetches overlapping pages) idempotent.
export function mergeMessages(
  existing: readonly LocalChatMessage[],
  incoming: readonly LocalChatMessage[]
): LocalChatMessage[] {
  const byId = new Map<string, LocalChatMessage>();
  const idByClientId = new Map<string, string>();

  const put = (m: LocalChatMessage) => {
    const clientId = m.clientMessageId ?? undefined;
    if (clientId) {
      const priorId = idByClientId.get(clientId);
      // A confirmed message supersedes the optimistic row that carried the same clientMessageId.
      if (priorId && priorId !== m.id) byId.delete(priorId);
      idByClientId.set(clientId, m.id);
    }
    byId.set(m.id, m);
  };

  existing.forEach(put);
  incoming.forEach(put);

  return [...byId.values()].sort(compareMessages);
}

/// Oldest confirmed message — the cursor for loading further back.
export function olderCursor(messages: readonly LocalChatMessage[]): string | undefined {
  const confirmed = messages.filter((m) => m.status === "sent");
  return confirmed.length > 0 ? confirmed[0].id : undefined;
}

/// Newest confirmed message — the cursor for the reconnect catch-up and the read pointer.
export function newerCursor(messages: readonly LocalChatMessage[]): string | undefined {
  const confirmed = messages.filter((m) => m.status === "sent");
  return confirmed.length > 0 ? confirmed[confirmed.length - 1].id : undefined;
}

/// RFC 4122 v4, formatted as the server expects a Guid. Generated once at compose time — before the
/// first network attempt — so every retry carries the same id and the server can dedupe.
export function newClientMessageId(): string {
  const c = globalThis.crypto;
  if (c && typeof c.randomUUID === "function") return c.randomUUID();

  // Fallback for older browsers and jsdom builds without randomUUID.
  const bytes = new Uint8Array(16);
  if (c && typeof c.getRandomValues === "function") c.getRandomValues(bytes);
  else for (let i = 0; i < 16; i++) bytes[i] = Math.floor(Math.random() * 256);

  bytes[6] = (bytes[6] & 0x0f) | 0x40;
  bytes[8] = (bytes[8] & 0x3f) | 0x80;
  const hex = [...bytes].map((b) => b.toString(16).padStart(2, "0")).join("");
  return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`;
}

/// Builds the optimistic row rendered the instant the user hits send. The temporary id is prefixed
/// so it can never be confused with — or collide with — a server id, and so the read-pointer sync
/// can refuse to send it.
export function optimisticMessage(
  roomId: string,
  body: string,
  clientMessageId: string,
  senderId?: string,
  senderName?: string
): LocalChatMessage {
  return {
    id: `local:${clientMessageId}`,
    roomId,
    clientMessageId,
    senderId: senderId ?? null,
    senderName: senderName ?? null,
    senderRole: null,
    kind: "Text",
    body,
    replyToMessageId: null,
    isPinned: false,
    isDeleted: false,
    attachments: [],
    createdAt: new Date().toISOString(),
    status: "pending"
  };
}

export function isLocalId(id: string): boolean {
  return id.startsWith("local:");
}

/// Why the composer is disabled, or null when the user may post.
///
/// Ordered most-specific first so the message names the real reason. Read from the server-computed
/// capabilities — never re-derived from role + policy, which is what lets a future chat mode ship
/// without a client release.
export function cannotPostReason(room: {
  status: string;
  postPolicy: string;
  mutedUntil?: string | null;
  dmRequestState?: string | null;
  capabilities: { canPost: boolean };
}): string | null {
  if (room.capabilities.canPost) return null;
  // A declined request outranks the room-state reasons below: the room is Active and the caller is
  // neither muted nor policy-blocked, so without this the honest refusal rendered as the catch-all
  // "You cannot post in this chat" (D-292).
  if (room.dmRequestState === "declined") return "This person declined your message request.";
  // Archived is checked first: it is the narrower state, and "archived" and "read-only" are
  // different answers to the same question (D-122).
  if (room.status === "Archived") return "This chat has been archived. You can still read the history.";
  if (room.status === "Locked") return "This event has ended. Chat is read-only.";
  if (room.mutedUntil && new Date(room.mutedUntil) > new Date()) return "You are muted in this chat.";
  if (room.postPolicy === "HostsOnly") return "Only hosts can post right now.";
  return "You cannot post in this chat.";
}

/// Sort for the room list.
///
/// `lastActivity` is the last MESSAGE's timestamp as of D-292 — it used to be the event's updated-at,
/// which is why this once carried a documented-debt note. The local override is kept because a client
/// that has just sent a message knows about it before the next list fetch does.
export function sortRooms<T extends { eventTitle: string; lastActivity?: string | null }>(
  rooms: readonly T[],
  localActivity: (room: T) => string | undefined
): T[] {
  return [...rooms].sort((a, b) => {
    const at = localActivity(a) ?? a.lastActivity ?? undefined;
    const bt = localActivity(b) ?? b.lastActivity ?? undefined;
    if (!at && !bt) return a.eventTitle.localeCompare(b.eventTitle);
    if (!at) return 1;
    if (!bt) return -1;
    return bt.localeCompare(at);
  });
}
