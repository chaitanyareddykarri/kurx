import type { ChatEvent } from "@/lib/use-chat-hub";

/// Presence, typing and read receipts as pure data (D-114 backend, D-118 Flutter).
///
/// Every rule lives here rather than in the hook or the components: the reducers are the same
/// decisions the Flutter client makes, so parity is a matter of reading one file against another
/// instead of comparing two component trees.

/// Someone the server says is typing, and when we stop believing it.
///
/// The expiry comes from the server's `ttlSeconds`, so a lost "stopped" event cannot strand an
/// indicator on screen — the receiver forgets on its own.
export type TypingUser = {
  userId: string;
  name: string;
  expiresAt: number;
};

export type PresenceState = {
  /// Everyone the server says is connected. Seeded from the room fetch, then maintained purely by
  /// `PresenceChanged` — never inferred from a message arriving, never polled.
  online: ReadonlySet<string>;
  typing: ReadonlyMap<string, TypingUser>;
  /// Each member's furthest-read message id. Room-level, not per message.
  readPointers: ReadonlyMap<string, string>;
  /// D-295 - furthest message each member's device has acknowledged receiving. Separate map from
  /// readPointers because delivered and read are different facts: a message can be on someone's
  /// phone for an hour before they open the room.
  deliveredPointers: ReadonlyMap<string, string>;
  /// False when the server has no shared presence store. An empty roster then means "we cannot
  /// know", not "nobody is here".
  enabled: boolean;
};

export const emptyPresence: PresenceState = {
  online: new Set(),
  typing: new Map(),
  readPointers: new Map(),
  deliveredPointers: new Map(),
  enabled: false
};

const DEFAULT_TYPING_TTL_SECONDS = 8;

function str(value: unknown): string | undefined {
  return value == null ? undefined : String(value);
}

/// Applies a server presence transition. The server only emits on an actual change, so this is a
/// straight set operation — no local inference.
function applyPresence(
  state: PresenceState,
  payload: Record<string, unknown> | null | undefined
): PresenceState {
  const userId = str(payload?.userId);
  if (!userId) return state;

  const online = new Set(state.online);
  if (payload?.online === true) {
    if (state.online.has(userId)) return state; // duplicate — keep the same reference
    online.add(userId);
    return { ...state, online };
  }

  online.delete(userId);
  // Someone who went offline cannot still be typing.
  const typing = new Map(state.typing);
  typing.delete(userId);
  return { ...state, online, typing };
}

function applyTyping(
  state: PresenceState,
  payload: Record<string, unknown> | null | undefined,
  now = Date.now()
): PresenceState {
  const userId = str(payload?.userId);
  if (!userId) return state;

  const typing = new Map(state.typing);
  if (payload?.isTyping === true) {
    const ttl =
      typeof payload.ttlSeconds === "number" ? payload.ttlSeconds : DEFAULT_TYPING_TTL_SECONDS;
    // A repeat simply pushes the expiry out — duplicates are absorbed, not stacked.
    typing.set(userId, {
      userId,
      name: str(payload.userName) ?? "Someone",
      expiresAt: now + ttl * 1000
    });
  } else {
    if (!state.typing.has(userId)) return state;
    typing.delete(userId);
  }

  return { ...state, typing };
}

/// Read pointers only ever move forward. Server ids are UUIDv7, so a lexical compare is a time
/// compare — an out-of-order or replayed event is discarded rather than walking a receipt back.
function applyReadReceipt(
  state: PresenceState,
  payload: Record<string, unknown> | null | undefined
): PresenceState {
  const userId = str(payload?.userId);
  const messageId = str(payload?.lastReadMessageId);
  if (!userId || !messageId) return state;

  const current = state.readPointers.get(userId);
  if (current !== undefined && messageId <= current) return state;

  const readPointers = new Map(state.readPointers);
  readPointers.set(userId, messageId);
  return { ...state, readPointers };
}

/// D-295 - same forward-only rule as read receipts, and for the same reason: UUIDv7 ids sort by time,
/// so a replayed or out-of-order event is discarded rather than walking a receipt backwards.
function applyDeliveryReceipt(
  state: PresenceState,
  payload: Record<string, unknown> | null | undefined
): PresenceState {
  const userId = str(payload?.userId);
  const messageId = str(payload?.messageId);
  if (!userId || !messageId) return state;

  const current = state.deliveredPointers.get(userId);
  if (current !== undefined && messageId <= current) return state;

  const deliveredPointers = new Map(state.deliveredPointers);
  deliveredPointers.set(userId, messageId);
  return { ...state, deliveredPointers };
}

/// Drops everything that is only true while connected. Read receipts survive: a message that was
/// read stays read, whereas a stale online dot would be presence we invented.
export function clearLivePresence(state: PresenceState): PresenceState {
  if (state.online.size === 0 && state.typing.size === 0) return state;
  return { ...state, online: new Set(), typing: new Map() };
}

/// Replaces the roster wholesale — the reconnect path, where what we held during the gap is
/// unknowable and is re-read rather than carried over.
export function syncRoster(
  state: PresenceState,
  onlineUserIds: readonly string[],
  enabled: boolean
): PresenceState {
  return { ...state, online: new Set(onlineUserIds), enabled };
}

/// Routes one realtime event. Returns the same state object for anything it does not own, so an
/// unknown event type is ignored exactly as the envelope contract requires (D-104).
export function applyPresenceEvent(
  state: PresenceState,
  event: ChatEvent,
  now = Date.now()
): PresenceState {
  switch (event.type) {
    case "PresenceChanged":
      return applyPresence(state, event.payload);
    case "TypingChanged":
      return applyTyping(state, event.payload, now);
    case "ReadReceiptChanged":
      return applyReadReceipt(state, event.payload);
    case "DeliveryReceiptChanged":
      return applyDeliveryReceipt(state, event.payload);
    default:
      return state;
  }
}

export function isPresenceEvent(type: string): boolean {
  return (
    type === "PresenceChanged" ||
    type === "TypingChanged" ||
    type === "ReadReceiptChanged" ||
    type === "DeliveryReceiptChanged"
  );
}

/// Removes expired typists. Returns the same object when nothing changed, so a periodic sweep does
/// not re-render the tree for no reason.
export function pruneTyping(state: PresenceState, now = Date.now()): PresenceState {
  let expired = false;
  state.typing.forEach((t) => {
    if (t.expiresAt <= now) expired = true;
  });
  if (!expired) return state;

  const typing = new Map<string, TypingUser>();
  state.typing.forEach((t, id) => {
    if (t.expiresAt > now) typing.set(id, t);
  });
  return { ...state, typing };
}

// ── Derived selectors ─────────────────────────────────────────────────────────────────────────

/// Typists other than me, still within their TTL, in a stable order so the banner does not jitter
/// as the set changes.
export function activeTypists(
  state: PresenceState,
  myUserId?: string,
  now = Date.now()
): TypingUser[] {
  return Array.from(state.typing.values())
    .filter((t) => t.userId !== myUserId && t.expiresAt > now)
    .sort((a, b) => a.name.localeCompare(b.name));
}

export function isOnline(state: PresenceState, userId?: string | null): boolean {
  return state.enabled && userId != null && state.online.has(userId);
}

/// The newest message any *other* member has read. One value for the whole list, so a receipt
/// update is evaluated once rather than per bubble.
export function highestDeliveredByOthers(
  state: PresenceState,
  myUserId?: string
): string | undefined {
  let best: string | undefined;
  state.deliveredPointers.forEach((messageId, userId) => {
    if (userId === myUserId) return;
    if (best === undefined || messageId > best) best = messageId;
  });
  return best;
}

export function highestReadByOthers(state: PresenceState, myUserId?: string): string | undefined {
  let best: string | undefined;
  state.readPointers.forEach((messageId, userId) => {
    if (userId === myUserId) return;
    if (best === undefined || messageId > best) best = messageId;
  });
  return best;
}

/// How many other people are present, or `undefined` when presence is unavailable — which the UI
/// must render as "no indicator", never as "nobody is here".
export function onlineCount(state: PresenceState, myUserId?: string): number | undefined {
  if (!state.enabled) return undefined;
  let count = 0;
  state.online.forEach((id) => {
    if (id !== myUserId) count += 1;
  });
  return count;
}

/// "Alice is typing…", "Alice and Bob are typing…", then an overflow form — naming everyone would
/// wrap and jitter.
export function typingLabel(typists: readonly TypingUser[]): string | undefined {
  if (typists.length === 0) return undefined;
  if (typists.length === 1) return `${typists[0].name} is typing…`;
  if (typists.length === 2) return `${typists[0].name} and ${typists[1].name} are typing…`;
  return `${typists[0].name} and ${typists.length - 1} others are typing…`;
}
