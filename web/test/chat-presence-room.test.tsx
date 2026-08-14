import { act, renderHook, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { ChatEvent } from "@/lib/use-chat-hub";

/// The room hook's realtime wiring: how presence events are routed, what a reconnect re-reads, and
/// what happens to indicators when the socket dies. The pure reducers are covered in
/// `chat-presence.test.tsx`; this file covers the plumbing around them.

const fetchRoomAction = vi.fn();
const fetchMessagesAction = vi.fn();
const markReadAction = vi.fn();
const sendMessageAction = vi.fn();

vi.mock("@/lib/chat-actions", () => ({
  fetchRoomAction: (...args: unknown[]) => fetchRoomAction(...args),
  fetchMessagesAction: (...args: unknown[]) => fetchMessagesAction(...args),
  markReadAction: (...args: unknown[]) => markReadAction(...args),
  sendMessageAction: (...args: unknown[]) => sendMessageAction(...args)
}));

/// Stands in for the socket: the test drives `onEvent` / `onReconnected` by hand and watches what
/// the hook invokes back.
let hub: {
  onEvent: (event: ChatEvent) => void;
  onReconnected: () => void;
  live: boolean;
};
const sendTyping = vi.fn();
const heartbeat = vi.fn();

vi.mock("@/lib/use-chat-hub", () => ({
  useChatHub: (options: { onEvent: (e: ChatEvent) => void; onReconnected: () => void }) => {
    hub.onEvent = options.onEvent;
    hub.onReconnected = options.onReconnected;
    return { live: hub.live, sendTyping, heartbeat };
  },
  parseChatEvent: () => null
}));

const { useChatRoom } = await import("@/lib/use-chat-room");

const room = (over: Record<string, unknown> = {}) => ({
  roomId: "room-1",
  kind: "Event",
  status: "Active",
  postPolicy: "Everyone",
  myRole: "Member",
  unreadCount: 0,
  capabilities: {
    canPost: true, canReply: true, canUpload: true, canPin: false,
    canDelete: true, canModerate: false, canMentionAll: false
  },
  pinnedMessages: [],
  onlineUserIds: ["user-2"],
  presenceEnabled: true,
  ...over
});

beforeEach(() => {
  vi.clearAllMocks();
  hub = { onEvent: () => {}, onReconnected: () => {}, live: true };
  fetchRoomAction.mockResolvedValue(room());
  fetchMessagesAction.mockResolvedValue({ messages: [] });
  markReadAction.mockResolvedValue(undefined);
});

async function mounted() {
  // The mount effect resolves two awaits before settling; wrapping the render itself keeps those
  // updates inside act rather than leaving warnings all over the output.
  let view!: ReturnType<typeof renderHook<ReturnType<typeof useChatRoom>, unknown>>;
  await act(async () => {
    // The argument is the ROOM id as of D-292 — it used to be an event id, with the room id derived
    // from the fetched room. It must match the mocked room's id, because the hub event filter and
    // the typing calls below are both keyed on it.
    view = renderHook(() => useChatRoom("room-1", "me"));
  });
  await waitFor(() => expect(view.result.current.room).toBeDefined());
  return view;
}

describe("room presence wiring", () => {
  it("seeds the roster and presence availability from the room fetch", async () => {
    const { result } = await mounted();
    expect(result.current.presence.enabled).toBe(true);
    expect([...result.current.presence.online]).toEqual(["user-2"]);
  });

  it("applies presence events locally instead of refetching the room", async () => {
    const { result } = await mounted();
    const roomCalls = fetchRoomAction.mock.calls.length;
    const messageCalls = fetchMessagesAction.mock.calls.length;

    act(() => {
      hub.onEvent({ type: "PresenceChanged", roomId: "room-1", payload: { userId: "u3", online: true } });
      hub.onEvent({
        type: "TypingChanged",
        roomId: "room-1",
        payload: { userId: "u3", userName: "Carol", isTyping: true, ttlSeconds: 8 }
      });
      hub.onEvent({
        type: "ReadReceiptChanged",
        roomId: "room-1",
        payload: { userId: "u3", lastReadMessageId: "m9" }
      });
    });

    await waitFor(() => expect(result.current.presence.online.has("u3")).toBe(true));
    expect(result.current.presence.typing.size).toBe(1);
    expect(result.current.presence.readPointers.get("u3")).toBe("m9");

    // The whole point: a typing keystroke must not become a room + messages round trip.
    expect(fetchRoomAction.mock.calls.length).toBe(roomCalls);
    expect(fetchMessagesAction.mock.calls.length).toBe(messageCalls);
  });

  it("ignores events addressed to another room", async () => {
    const { result } = await mounted();
    act(() => {
      hub.onEvent({ type: "PresenceChanged", roomId: "other", payload: { userId: "u9", online: true } });
    });
    expect(result.current.presence.online.has("u9")).toBe(false);
  });

  it("ignores an unknown event type entirely, without refetching", async () => {
    const { result } = await mounted();
    const before = result.current.presence;
    const roomCalls = fetchRoomAction.mock.calls.length;

    act(() => {
      hub.onEvent({ type: "ReactionAdded", roomId: "room-1", payload: { emoji: "🎉" } });
    });

    expect(result.current.presence.online).toBe(before.online);
    // A future server-side event type must not turn into a request storm on an older client.
    expect(fetchRoomAction.mock.calls.length).toBe(roomCalls);
  });

  it("still re-reads the server for room-level events", async () => {
    await mounted();
    const roomCalls = fetchRoomAction.mock.calls.length;

    await act(async () => {
      hub.onEvent({ type: "RoomUpdated", roomId: "room-1", payload: {} });
    });

    // Capabilities can change with the room, so these stay server-driven rather than local.
    await waitFor(() => expect(fetchRoomAction.mock.calls.length).toBeGreaterThan(roomCalls));
  });

  it("re-reads the roster on reconnect rather than trusting what it held", async () => {
    const { result } = await mounted();
    act(() => {
      hub.onEvent({ type: "PresenceChanged", roomId: "room-1", payload: { userId: "u3", online: true } });
    });
    await waitFor(() => expect(result.current.presence.online.has("u3")).toBe(true));

    // u3 left while the socket was down; the server's answer replaces the local roster outright.
    fetchRoomAction.mockResolvedValue(room({ onlineUserIds: ["user-2"] }));
    await act(async () => {
      hub.onReconnected();
    });

    await waitFor(() => expect(result.current.presence.online.has("u3")).toBe(false));
    expect(result.current.presence.online.has("user-2")).toBe(true);
  });

  it("clears indicators when the socket dies but keeps read receipts", async () => {
    const { result, rerender } = await mounted();
    act(() => {
      hub.onEvent({
        type: "TypingChanged",
        roomId: "room-1",
        payload: { userId: "u3", userName: "Carol", isTyping: true }
      });
      hub.onEvent({
        type: "ReadReceiptChanged",
        roomId: "room-1",
        payload: { userId: "u3", lastReadMessageId: "m9" }
      });
    });
    await waitFor(() => expect(result.current.presence.typing.size).toBe(1));

    hub.live = false;
    rerender();

    await waitFor(() => expect(result.current.presence.online.size).toBe(0));
    expect(result.current.presence.typing.size).toBe(0);
    // Read stays read — only presence is unknowable while disconnected.
    expect(result.current.presence.readPointers.get("u3")).toBe("m9");
  });

  it("reports typing once per burst, not once per keystroke", async () => {
    const { result } = await mounted();

    act(() => {
      result.current.notifyTyping("h");
      result.current.notifyTyping("he");
      result.current.notifyTyping("hel");
    });

    expect(sendTyping).toHaveBeenCalledTimes(1);
    expect(sendTyping).toHaveBeenCalledWith("room-1", true);
  });

  it("stops typing as soon as the composer is emptied", async () => {
    const { result } = await mounted();

    act(() => {
      result.current.notifyTyping("hi");
      result.current.notifyTyping("");
    });

    expect(sendTyping).toHaveBeenNthCalledWith(1, "room-1", true);
    expect(sendTyping).toHaveBeenNthCalledWith(2, "room-1", false);
  });

  it("stays silent when the server has no presence store", async () => {
    fetchRoomAction.mockResolvedValue(room({ presenceEnabled: false, onlineUserIds: [] }));
    const { result } = await mounted();

    act(() => {
      result.current.notifyTyping("hello");
    });

    expect(sendTyping).not.toHaveBeenCalled();
  });
});
