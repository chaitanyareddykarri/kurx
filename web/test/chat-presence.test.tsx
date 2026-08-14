import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { MessageBubble } from "@/components/chat/message-bubble";
import { OnlineCount, PresenceAvatar, TypingBanner } from "@/components/chat/presence";
import type { LocalChatMessage } from "@/lib/chat-merge";
import {
  activeTypists,
  applyPresenceEvent,
  clearLivePresence,
  emptyPresence,
  highestReadByOthers,
  isOnline,
  isPresenceEvent,
  onlineCount,
  pruneTyping,
  syncRoster,
  typingLabel,
  type PresenceState
} from "@/lib/chat-presence";
import type { ChatEvent } from "@/lib/use-chat-hub";

const event = (type: string, payload: Record<string, unknown>): ChatEvent => ({
  type,
  roomId: "room-1",
  payload
});

const enabled: PresenceState = { ...emptyPresence, enabled: true };

function message(over: Partial<LocalChatMessage> = {}): LocalChatMessage {
  return {
    id: "m1",
    roomId: "room-1",
    senderId: "user-2",
    senderName: "Alice",
    senderRole: "Member",
    kind: "Text",
    body: "hello",
    isPinned: false,
    isDeleted: false,
    attachments: [],
    createdAt: "2026-07-19T10:00:00Z",
    status: "sent",
    ...over
  };
}

describe("online presence", () => {
  it("seeds the roster from the room fetch", () => {
    const state = syncRoster(emptyPresence, ["user-2", "user-3"], true);
    expect(state.enabled).toBe(true);
    expect([...state.online]).toEqual(["user-2", "user-3"]);
  });

  it("adds and removes a member on server events", () => {
    let state = applyPresenceEvent(enabled, event("PresenceChanged", { userId: "u2", online: true }));
    expect(isOnline(state, "u2")).toBe(true);

    state = applyPresenceEvent(state, event("PresenceChanged", { userId: "u2", online: false }));
    expect(isOnline(state, "u2")).toBe(false);
  });

  it("never infers presence from a message arriving", () => {
    const state = applyPresenceEvent(
      enabled,
      { type: "MessageReceived", roomId: "room-1", id: "m1", payload: { senderId: "u9" } }
    );
    // Obviously they are connected — but only the server may say so.
    expect(state.online.size).toBe(0);
  });

  it("keeps the same object for a duplicate online event, so React can skip the render", () => {
    const first = applyPresenceEvent(enabled, event("PresenceChanged", { userId: "u2", online: true }));
    const again = applyPresenceEvent(first, event("PresenceChanged", { userId: "u2", online: true }));
    expect(again).toBe(first);
  });

  it("ignores an unknown event type rather than throwing", () => {
    const state = applyPresenceEvent(enabled, event("ReactionAdded", { emoji: "🎉" }));
    expect(state).toBe(enabled);
    expect(isPresenceEvent("ReactionAdded")).toBe(false);
  });

  it("reports an unavailable roster as unknown, not as everyone offline", () => {
    const disabled = syncRoster(emptyPresence, [], false);
    expect(onlineCount(disabled, "me")).toBeUndefined();
    expect(isOnline(disabled, "u2")).toBe(false);
  });

  it("excludes me from the count of who else is here", () => {
    const state = syncRoster(emptyPresence, ["me", "u2"], true);
    expect(onlineCount(state, "me")).toBe(1);
  });
});

describe("typing", () => {
  it("absorbs repeats instead of stacking entries, pushing the expiry out", () => {
    let state = enabled;
    for (const at of [1_000, 2_000, 3_000]) {
      state = applyPresenceEvent(
        state,
        event("TypingChanged", { userId: "u2", userName: "Alice", isTyping: true, ttlSeconds: 8 }),
        at
      );
    }
    expect(state.typing.size).toBe(1);
    expect(state.typing.get("u2")?.expiresAt).toBe(3_000 + 8_000);
  });

  it("expires without a stop event ever arriving", () => {
    const state = applyPresenceEvent(
      enabled,
      event("TypingChanged", { userId: "u2", userName: "Alice", isTyping: true, ttlSeconds: 8 }),
      0
    );
    expect(activeTypists(state, "me", 7_000)).toHaveLength(1);
    expect(activeTypists(state, "me", 9_000)).toHaveLength(0);
    expect(pruneTyping(state, 9_000).typing.size).toBe(0);
  });

  it("leaves state untouched when a sweep finds nothing expired", () => {
    const state = applyPresenceEvent(
      enabled,
      event("TypingChanged", { userId: "u2", userName: "Alice", isTyping: true, ttlSeconds: 8 }),
      0
    );
    expect(pruneTyping(state, 1_000)).toBe(state);
  });

  it("never shows my own typing back to me", () => {
    const state = applyPresenceEvent(
      enabled,
      event("TypingChanged", { userId: "me", userName: "Me", isTyping: true }),
      0
    );
    expect(activeTypists(state, "me", 0)).toHaveLength(0);
  });

  it("clears typing when that member goes offline", () => {
    let state = applyPresenceEvent(
      enabled,
      event("TypingChanged", { userId: "u2", userName: "Alice", isTyping: true }),
      0
    );
    state = applyPresenceEvent(state, event("PresenceChanged", { userId: "u2", online: false }));
    expect(state.typing.size).toBe(0);
  });

  it("orders typists stably and overflows past two", () => {
    const users = [
      { userId: "c", name: "Carol", expiresAt: 9_000 },
      { userId: "a", name: "Alice", expiresAt: 9_000 },
      { userId: "b", name: "Bob", expiresAt: 9_000 }
    ];
    const state: PresenceState = { ...enabled, typing: new Map(users.map((u) => [u.userId, u])) };
    const sorted = activeTypists(state, "me", 0);

    expect(sorted.map((t) => t.name)).toEqual(["Alice", "Bob", "Carol"]);
    expect(typingLabel(sorted.slice(0, 1))).toBe("Alice is typing…");
    expect(typingLabel(sorted.slice(0, 2))).toBe("Alice and Bob are typing…");
    expect(typingLabel(sorted)).toBe("Alice and 2 others are typing…");
  });
});

describe("read receipts", () => {
  it("only ever moves the pointer forward", () => {
    let state = applyPresenceEvent(
      enabled,
      event("ReadReceiptChanged", { userId: "u2", lastReadMessageId: "m5" })
    );
    state = applyPresenceEvent(
      state,
      event("ReadReceiptChanged", { userId: "u2", lastReadMessageId: "m3" })
    );
    expect(state.readPointers.get("u2")).toBe("m5");
  });

  it("discards a duplicate without producing a new object", () => {
    const first = applyPresenceEvent(
      enabled,
      event("ReadReceiptChanged", { userId: "u2", lastReadMessageId: "m5" })
    );
    const again = applyPresenceEvent(
      first,
      event("ReadReceiptChanged", { userId: "u2", lastReadMessageId: "m5" })
    );
    expect(again).toBe(first);
  });

  it("takes the furthest pointer across members and ignores my own", () => {
    let state = enabled;
    for (const [userId, id] of [["u2", "m2"], ["u3", "m7"], ["me", "m9"]]) {
      state = applyPresenceEvent(
        state,
        event("ReadReceiptChanged", { userId, lastReadMessageId: id })
      );
    }
    expect(highestReadByOthers(state, "me")).toBe("m7");
  });
});

describe("offline behaviour", () => {
  it("clears online and typing but preserves read receipts", () => {
    let state = syncRoster(enabled, ["u2"], true);
    state = applyPresenceEvent(
      state,
      event("TypingChanged", { userId: "u2", userName: "Alice", isTyping: true })
    );
    state = applyPresenceEvent(
      state,
      event("ReadReceiptChanged", { userId: "u2", lastReadMessageId: "m5" })
    );

    const offline = clearLivePresence(state);
    expect(offline.online.size).toBe(0);
    expect(offline.typing.size).toBe(0);
    // A message that was read stays read; a stale dot would be presence we invented.
    expect(offline.readPointers.get("u2")).toBe("m5");
    expect(offline.enabled).toBe(true);
  });

  it("replaces rather than merges the roster on reconnect", () => {
    const before = syncRoster(enabled, ["u2", "u3"], true);
    const after = syncRoster(before, ["u3"], true);
    // u2 left during the gap: the server's answer wins outright.
    expect([...after.online]).toEqual(["u3"]);
  });
});

describe("presence components", () => {
  it("omits the dot entirely when presence is unavailable", () => {
    render(<PresenceAvatar name="Alice" online={false} presenceKnown={false} />);
    expect(screen.queryByRole("img", { name: /offline/i })).toBeNull();
  });

  it("announces online and offline as words, not colour alone", () => {
    const { rerender } = render(<PresenceAvatar name="Alice" online presenceKnown />);
    expect(screen.getByRole("img", { name: "Alice, online" })).toBeInTheDocument();

    rerender(<PresenceAvatar name="Alice" online={false} presenceKnown />);
    expect(screen.getByRole("img", { name: "Alice, offline" })).toBeInTheDocument();
  });

  it("renders the typing banner as a live region and empties without unmounting it", () => {
    const typist = { userId: "u2", name: "Alice", expiresAt: Date.now() + 8_000 };
    const { container, rerender } = render(<TypingBanner typists={[typist]} />);

    const region = container.querySelector("[aria-live='polite']");
    expect(region).toBeInTheDocument();
    expect(screen.getByText("Alice is typing…")).toBeInTheDocument();

    rerender(<TypingBanner typists={[]} />);
    // Still mounted, just empty — remounting the region makes some readers miss the next message.
    expect(container.querySelector("[aria-live='polite']")).toBeInTheDocument();
    expect(screen.queryByText(/is typing/)).toBeNull();
  });

  it("hides the online count when presence is unavailable", () => {
    const { container, rerender } = render(<OnlineCount count={undefined} />);
    expect(container).toBeEmptyDOMElement();

    rerender(<OnlineCount count={0} />);
    expect(container).toBeEmptyDOMElement();

    rerender(<OnlineCount count={2} />);
    expect(screen.getByText("2 others online")).toBeInTheDocument();
  });

  it("shows a read tick only once someone else has read the message", () => {
    const { rerender } = render(<MessageBubble message={message()} isMine showSender={false} />);
    expect(screen.getByLabelText("Sent")).toBeInTheDocument();

    rerender(<MessageBubble message={message()} isMine showSender={false} read />);
    expect(screen.getByLabelText("Read")).toBeInTheDocument();
    expect(screen.queryByLabelText("Sent")).toBeNull();
  });

  it("never shows a read tick on a message that is still sending", () => {
    render(<MessageBubble message={message({ status: "pending" })} isMine showSender={false} read />);
    expect(screen.getByLabelText("Sending")).toBeInTheDocument();
    expect(screen.queryByLabelText("Read")).toBeNull();
  });

  it("shows a sender's online dot in their bubble", () => {
    render(<MessageBubble message={message()} isMine={false} showSender senderOnline presenceKnown />);
    expect(screen.getByRole("img", { name: "Alice, online" })).toBeInTheDocument();
  });
});
