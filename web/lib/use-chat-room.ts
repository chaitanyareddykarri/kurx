"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import {
  fetchMessagesAction,
  fetchRoomAction,
  markReadAction,
  sendMessageAction
} from "@/lib/chat-actions";
import type { ChatRoom } from "@/lib/chat-api";
import {
  isLocalId,
  mergeMessages,
  newClientMessageId,
  newerCursor,
  olderCursor,
  optimisticMessage,
  toLocal,
  type LocalChatMessage
} from "@/lib/chat-merge";
import {
  applyPresenceEvent,
  clearLivePresence,
  emptyPresence,
  isPresenceEvent,
  pruneTyping,
  syncRoster,
  type PresenceState
} from "@/lib/chat-presence";
import { useChatHub } from "@/lib/use-chat-hub";

/// How often a connected tab refreshes its presence TTL, against the server's 120s window.
const HEARTBEAT_MS = 45_000;
/// Quiet period after the last keystroke before "stopped typing" is sent.
const TYPING_IDLE_MS = 3_000;

/// Events that change room-level state (capabilities, pins, membership) and are therefore resolved
/// by re-reading the server rather than mutating locally. Kept in step with the Flutter client.
const ROOM_LEVEL_EVENTS = new Set([
  "MessageDeleted",
  // D-293: an edit rewrites a message already on screen. Re-reading is how MessageDeleted has always
  // been handled, and an edit is rare enough that the extra page fetch is not worth a merge path.
  "MessageEdited",
  "MessagePinned",
  "RoomUpdated",
  "MemberMuted",
  "MemberBanned",
  // D-299. Both of these were broadcast by the server and handled by nobody.
  //
  // `ReactionChanged`: reactions were not live at all — another member's 👍 only appeared after a manual
  // refresh, because the summary lives on the message and nothing re-read it.
  //
  // `MemberRemoved`: the eviction event added with D-294, the one a REFUND takes. The server pulls the
  // socket out of the group and the client kept showing a live room that had simply gone quiet, with
  // sends failing and no explanation. `MemberBanned` beside it has always been handled; this is the
  // sibling that was missed.
  "ReactionChanged",
  "MemberRemoved"
]);

/// Drives one open chat room: history, pagination, optimistic send, live updates and reconnect
/// catch-up. Behaviour is identical to the Flutter `ChatRoomController` — same endpoints, ordering,
/// optimistic flow and read-pointer rules — with the shared decisions living in `chat-merge.ts`.
export function useChatRoom(roomId: string, currentUserId?: string) {
  const [room, setRoom] = useState<ChatRoom | undefined>();
  const [messages, setMessages] = useState<LocalChatMessage[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadingOlder, setLoadingOlder] = useState(false);
  const [hasMoreOlder, setHasMoreOlder] = useState(true);
  const [error, setError] = useState<unknown>();

  /// Presence, typing and read receipts — one piece of state inside the room, exactly as on Flutter.
  /// No presence store, no context, no component-local maps.
  const [presence, setPresence] = useState<PresenceState>(emptyPresence);

  // The room id now arrives as an argument (D-292), so this no longer derives it from the loaded
  // room. That also removes a startup delay: the hub subscription and the event filter below had to
  // wait for the room fetch to resolve before they knew which room they were for.
  const messagesRef = useRef<LocalChatMessage[]>([]);
  messagesRef.current = messages;

  // Read pointer already synced, so a re-render cannot spam the endpoint.
  const syncedPointer = useRef<string | undefined>();

  const apply = useCallback((incoming: LocalChatMessage[]) => {
    setMessages((prev) => mergeMessages(prev, incoming));
  }, []);

  const syncReadPointer = useCallback(
    async (list: LocalChatMessage[], id: string) => {
      const newest = newerCursor(list);
      // Never sync an unconfirmed local id — it is not a server message.
      if (!newest || isLocalId(newest) || syncedPointer.current === newest) return;
      syncedPointer.current = newest;
      try {
        await markReadAction(id, newest);
      } catch {
        // Best effort: the pointer only moves forward server-side, so a later successful call
        // supersedes this one. Nothing to reconcile, nothing to retry.
      }
    },
    []
  );

  const loadLatest = useCallback(
    async (id: string) => {
      const page = await fetchMessagesAction(id, { limit: 50 });
      const incoming = page.messages.map((m) => toLocal(m));
      setMessages((prev) => {
        const merged = mergeMessages(prev, incoming);
        void syncReadPointer(merged, id);
        return merged;
      });
    },
    [syncReadPointer]
  );

  // Initial load: room (and therefore capabilities) first, then the newest page.
  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(undefined);

    void (async () => {
      try {
        const loaded = await fetchRoomAction(roomId);
        if (cancelled) return;
        setRoom(loaded);
        // Initial synchronisation: everything after this arrives as an event.
        setPresence((p) => syncRoster(p, loaded.onlineUserIds, loaded.presenceEnabled));
        await loadLatest(loaded.roomId);
      } catch (e) {
        if (!cancelled) setError(e);
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [roomId, loadLatest]);

  /// Everything after the newest message held. The gap-recovery path after a reconnect.
  const catchUp = useCallback(async () => {
    if (!roomId) return;
    const cursor = newerCursor(messagesRef.current);
    if (!cursor) {
      await loadLatest(roomId);
      return;
    }
    try {
      const page = await fetchMessagesAction(roomId, { after: cursor, limit: 50 });
      apply(page.messages.map((m) => toLocal(m)));
    } catch {
      // Still unreachable; the next reconnect or manual refresh retries.
    }
  }, [roomId, apply, loadLatest]);

  /// Re-reads just the roster. Used on reconnect, where the messages are recovered separately by
  /// the `?after=` catch-up.
  const refreshRoster = useCallback(async () => {
    try {
      const loaded = await fetchRoomAction(roomId);
      setPresence((p) => syncRoster(p, loaded.onlineUserIds, loaded.presenceEnabled));
    } catch {
      // Keep the last known roster; the next reconnect tries again.
    }
  }, [roomId]);

  const refresh = useCallback(async () => {
    try {
      const loaded = await fetchRoomAction(roomId);
      setRoom(loaded);
      setPresence((p) => syncRoster(p, loaded.onlineUserIds, loaded.presenceEnabled));
      await loadLatest(loaded.roomId);
      setError(undefined);
    } catch (e) {
      setError(e);
    }
  }, [roomId, loadLatest]);

  const { live, sendTyping, heartbeat } = useChatHub({
    roomId,
    onEvent: (event) => {
      if (event.roomId !== roomId) return;
      // Presence events are themselves authoritative and arrive far more often than anything else —
      // they are applied locally. Routing them through the refetch below would turn every keystroke
      // in the room into a room + messages round trip.
      if (isPresenceEvent(event.type)) {
        setPresence((p) => applyPresenceEvent(p, event));
        return;
      }
      // Everything else resolves to a server read rather than a local mutation, so capabilities —
      // and therefore the whole UI — stay server-driven, and a missed event self-heals.
      if (event.type === "MessageReceived") void catchUp();
      else if (ROOM_LEVEL_EVENTS.has(event.type)) void refresh();
      // Anything else is a type this build does not know. Ignoring it is the envelope contract
      // (D-104) and matches Flutter; refetching on every unrecognised event would let a future
      // server-side event type quietly turn into a request storm on older clients.
    },
    onReconnected: () => {
      void catchUp();
      // The roster held during the gap is unknowable, so it is re-read rather than carried over.
      void refreshRoster();
    }
  });

  // Losing the socket clears online and typing but never read receipts: a stale dot would be
  // presence we invented, whereas a message that was read stays read.
  useEffect(() => {
    if (!live) setPresence(clearLivePresence);
  }, [live]);

  // Sweeps expired typists, so a lost "stopped" event cannot strand an indicator. Runs only while
  // somebody is typing — an idle room holds no interval.
  const typingCount = presence.typing.size;
  useEffect(() => {
    if (typingCount === 0) return;
    const id = setInterval(() => setPresence((p) => pruneTyping(p)), 1_000);
    return () => clearInterval(id);
  }, [typingCount]);

  useEffect(() => {
    if (!live) return;
    const id = setInterval(() => void heartbeat(), HEARTBEAT_MS);
    return () => clearInterval(id);
  }, [live, heartbeat]);

  /// Reports composer activity. Debounced: one `started` per burst and one `stopped` after a pause,
  /// so a fast typist produces two events rather than one per keystroke.
  const typingSent = useRef(false);
  const typingTimer = useRef<ReturnType<typeof setTimeout> | undefined>(undefined);

  const notifyTyping = useCallback(
    (text: string) => {
      if (!roomId || !presence.enabled) return;
      if (typingTimer.current) clearTimeout(typingTimer.current);

      if (text.trim().length === 0) {
        // An empty composer is not typing.
        if (typingSent.current) {
          typingSent.current = false;
          void sendTyping(roomId, false);
        }
        return;
      }

      if (!typingSent.current) {
        typingSent.current = true;
        void sendTyping(roomId, true);
      }
      typingTimer.current = setTimeout(() => {
        typingSent.current = false;
        void sendTyping(roomId, false);
      }, TYPING_IDLE_MS);
    },
    [roomId, presence.enabled, sendTyping]
  );

  const stopTyping = useCallback(() => {
    if (typingTimer.current) clearTimeout(typingTimer.current);
    if (roomId && typingSent.current) {
      typingSent.current = false;
      void sendTyping(roomId, false);
    }
  }, [roomId, sendTyping]);

  // Unmounting mid-burst would otherwise leave the room believing this user is still typing until
  // the TTL expires.
  useEffect(() => stopTyping, [stopTyping]);

  const loadOlder = useCallback(async () => {
    if (!roomId || loadingOlder || !hasMoreOlder) return;
    const cursor = olderCursor(messagesRef.current);
    if (!cursor) return;

    setLoadingOlder(true);
    try {
      const page = await fetchMessagesAction(roomId, { before: cursor, limit: 50 });
      if (page.messages.length === 0) setHasMoreOlder(false);
      else apply(page.messages.map((m) => toLocal(m)));
    } catch {
      // Leave hasMoreOlder alone so a transient failure can be retried by scrolling again.
    } finally {
      setLoadingOlder(false);
    }
  }, [roomId, loadingOlder, hasMoreOlder, apply]);

  const send = useCallback(
    async (body: string, attachmentIds: string[] = [], replyToMessageId?: string) => {
      const text = body.trim();
      // An attachment with no caption is a valid message — the file is the content.
      if (!roomId || (!text && attachmentIds.length === 0)) return;

      stopTyping();

      const clientMessageId = newClientMessageId();
      const optimistic = optimisticMessage(roomId, text, clientMessageId, currentUserId);
      setMessages((prev) => mergeMessages(prev, [optimistic]));

      try {
        const sent = await sendMessageAction(
          // The 4th argument has always been `replyToMessageId`; it was hard-coded `undefined`, which
          // is why replying was plumbed end to end on both clients and reachable from neither.
          roomId, text, clientMessageId, replyToMessageId,
          attachmentIds.length > 0 ? attachmentIds : undefined
        );
        // Merging by clientMessageId collapses the optimistic row onto the server's copy.
        setMessages((prev) => mergeMessages(prev, [toLocal(sent)]));
      } catch {
        // A refusal (muted, locked, banned, too long) cannot succeed on an identical retry, so the
        // user is told and offered Retry/Discard rather than having their text silently dropped.
        setMessages((prev) =>
          prev.map((m) => (m.clientMessageId === clientMessageId ? { ...m, status: "failed" } : m))
        );
      }
    },
    [roomId, currentUserId, stopTyping]
  );

  /// Re-sends a failed message with its **original** clientMessageId — that is what makes the retry
  /// idempotent rather than creating a second message.
  const retry = useCallback(
    async (clientMessageId: string) => {
      if (!roomId) return;
      const target = messagesRef.current.find((m) => m.clientMessageId === clientMessageId);
      if (!target) return;

      setMessages((prev) =>
        prev.map((m) => (m.clientMessageId === clientMessageId ? { ...m, status: "pending" } : m))
      );

      try {
        const sent = await sendMessageAction(roomId, target.body, clientMessageId);
        setMessages((prev) => mergeMessages(prev, [toLocal(sent)]));
      } catch {
        setMessages((prev) =>
          prev.map((m) => (m.clientMessageId === clientMessageId ? { ...m, status: "failed" } : m))
        );
      }
    },
    [roomId]
  );

  const discard = useCallback((clientMessageId: string) => {
    setMessages((prev) => prev.filter((m) => m.clientMessageId !== clientMessageId));
  }, []);

  return {
    room,
    messages,
    loading,
    loadingOlder,
    hasMoreOlder,
    live,
    error,
    presence,
    notifyTyping,
    loadOlder,
    send,
    retry,
    discard,
    refresh
  };
}
