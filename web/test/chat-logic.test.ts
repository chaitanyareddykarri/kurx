import { describe, expect, it } from "vitest";
import {
  cannotPostReason,
  compareMessages,
  isLocalId,
  mergeMessages,
  newClientMessageId,
  newerCursor,
  olderCursor,
  optimisticMessage,
  sortRooms,
  toLocal,
  type LocalChatMessage
} from "@/lib/chat-merge";
import { parseChatEvent } from "@/lib/use-chat-hub";

function msg(
  id: string,
  createdAt: string,
  extra: Partial<LocalChatMessage> = {}
): LocalChatMessage {
  return toLocal(
    {
      id,
      roomId: "room-1",
      clientMessageId: null,
      senderId: "user-2",
      senderName: "Someone",
      senderRole: "Member",
      kind: "Text",
      body: `body-${id}`,
      replyToMessageId: null,
      isPinned: false,
      isDeleted: false,
      attachments: [],
      createdAt
    },
    extra.status ?? "sent"
  ) as LocalChatMessage;
}

describe("message ordering", () => {
  it("orders by (createdAt, id), never createdAt alone", () => {
    // Identical timestamps — the case CreatedAt-only ordering gets wrong. The backend breaks the
    // tie on id, and the client must agree or paging and merge disagree.
    const a = msg("aaa", "2026-07-19T10:00:00Z");
    const b = msg("bbb", "2026-07-19T10:00:00Z");
    expect(mergeMessages([b], [a]).map((m) => m.id)).toEqual(["aaa", "bbb"]);
    expect(compareMessages(a, b)).toBeLessThan(0);
  });

  it("orders by time first when timestamps differ", () => {
    const older = msg("zzz", "2026-07-19T09:00:00Z");
    const newer = msg("aaa", "2026-07-19T10:00:00Z");
    expect(mergeMessages([newer], [older]).map((m) => m.id)).toEqual(["zzz", "aaa"]);
  });
});

describe("de-duplication", () => {
  it("stores a repeated message once — reconnect re-fetches overlapping pages", () => {
    const m = msg("x", "2026-07-19T10:00:00Z");
    expect(mergeMessages([m], [m])).toHaveLength(1);
  });

  it("collapses the optimistic row onto the confirmed one via clientMessageId", () => {
    const optimistic = optimisticMessage("room-1", "hello", "c-1", "user-1");
    const confirmed = msg("server-1", optimistic.createdAt, {});
    confirmed.clientMessageId = "c-1";

    const merged = mergeMessages([optimistic], [confirmed]);

    // One message under the server's identity — not two bubbles saying the same thing.
    expect(merged).toHaveLength(1);
    expect(merged[0].id).toBe("server-1");
    expect(merged[0].status).toBe("sent");
  });

  it("keeps distinct messages from different senders apart", () => {
    const a = optimisticMessage("room-1", "mine", "c-1");
    const b = msg("server-9", "2026-07-19T10:00:01Z");
    expect(mergeMessages([a], [b])).toHaveLength(2);
  });
});

describe("cursors", () => {
  const list = [
    msg("m1", "2026-07-19T10:00:00Z"),
    msg("m2", "2026-07-19T10:00:01Z"),
    optimisticMessage("room-1", "not sent yet", "c-9")
  ];

  it("olderCursor is the oldest confirmed message", () => {
    expect(olderCursor(list)).toBe("m1");
  });

  it("newerCursor ignores unconfirmed messages", () => {
    // Paging or marking read from a local id would be meaningless to the server.
    expect(newerCursor(list)).toBe("m2");
  });

  it("returns undefined when nothing is confirmed", () => {
    expect(newerCursor([optimisticMessage("room-1", "x", "c-1")])).toBeUndefined();
    expect(olderCursor([])).toBeUndefined();
  });
});

describe("optimistic send", () => {
  it("produces a pending row with a local id that is never mistaken for a server id", () => {
    const optimistic = optimisticMessage("room-1", "hello", "c-1", "user-1");
    expect(optimistic.status).toBe("pending");
    expect(isLocalId(optimistic.id)).toBe(true);
    expect(optimistic.clientMessageId).toBe("c-1");
  });

  it("generates a v4 uuid the server will accept as a Guid", () => {
    const id = newClientMessageId();
    expect(id).toMatch(
      /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i
    );
  });

  it("generates a distinct id per message", () => {
    const ids = new Set(Array.from({ length: 50 }, () => newClientMessageId()));
    expect(ids.size).toBe(50);
  });
});

describe("capability-driven posting", () => {
  const base = { status: "Active", postPolicy: "Everyone", mutedUntil: null };

  it("allows posting when the server says so", () => {
    expect(cannotPostReason({ ...base, capabilities: { canPost: true } })).toBeNull();
  });

  it("names the lock first", () => {
    expect(
      cannotPostReason({ ...base, status: "Locked", capabilities: { canPost: false } })
    ).toBe("This event has ended. Chat is read-only.");
  });

  it("names archiving separately from read-only (D-122)", () => {
    expect(
      cannotPostReason({
        status: "Archived",
        postPolicy: "Everyone",
        capabilities: { canPost: false }
      })
    ).toBe("This chat has been archived. You can still read the history.");
  });

  it("still reports a server-permitted post as allowed whatever the status string says", () => {
    // Capabilities are authoritative: an unknown future status must not invent a refusal.
    expect(
      cannotPostReason({
        status: "SomethingNew",
        postPolicy: "Everyone",
        capabilities: { canPost: true }
      })
    ).toBeNull();
  });

  it("names an active mute", () => {
    const future = new Date(Date.now() + 60_000).toISOString();
    expect(
      cannotPostReason({ ...base, mutedUntil: future, capabilities: { canPost: false } })
    ).toBe("You are muted in this chat.");
  });

  it("ignores a lapsed mute and falls through to the policy", () => {
    const past = new Date(Date.now() - 60_000).toISOString();
    expect(
      cannotPostReason({
        ...base,
        postPolicy: "HostsOnly",
        mutedUntil: past,
        capabilities: { canPost: false }
      })
    ).toBe("Only hosts can post right now.");
  });

  it("trusts capabilities over the policy string", () => {
    // Policy says hosts-only, but the server already resolved that this user may post. The client
    // must never re-derive permission from role + policy.
    expect(
      cannotPostReason({ ...base, postPolicy: "HostsOnly", capabilities: { canPost: true } })
    ).toBeNull();
  });
});

describe("realtime envelope", () => {
  it("parses the versioned envelope including the gap-detection id", () => {
    const event = parseChatEvent({
      v: 1,
      type: "MessageReceived",
      roomId: "room-1",
      id: "msg-7",
      payload: { body: "hi" }
    });
    expect(event).toEqual(
      expect.objectContaining({ type: "MessageReceived", roomId: "room-1", id: "msg-7" })
    );
  });

  it("accepts an unknown type, so new server events cannot break an old client", () => {
    const event = parseChatEvent({ v: 2, type: "InventedLater", roomId: "room-1", extra: true });
    expect(event?.type).toBe("InventedLater");
  });

  it("drops a malformed envelope instead of throwing", () => {
    expect(parseChatEvent(null)).toBeNull();
    expect(parseChatEvent("nope")).toBeNull();
    expect(parseChatEvent({ type: "MessageReceived" })).toBeNull(); // no roomId
  });
});

describe("room list ordering", () => {
  it("prefers locally-known activity over the server's lastActivity", () => {
    // lastActivity is the *event's* updated-at (documented debt), so a known local timestamp wins.
    const rooms = [
      { eventTitle: "A", lastActivity: "2026-07-19T12:00:00Z" },
      { eventTitle: "B", lastActivity: "2026-07-19T09:00:00Z" }
    ];
    const sorted = sortRooms(rooms, (r) =>
      r.eventTitle === "B" ? "2026-07-19T23:00:00Z" : undefined
    );
    expect(sorted.map((r) => r.eventTitle)).toEqual(["B", "A"]);
  });

  it("sorts rooms with no activity last, alphabetically", () => {
    const rooms = [
      { eventTitle: "Zeta", lastActivity: null },
      { eventTitle: "Alpha", lastActivity: null },
      { eventTitle: "Recent", lastActivity: "2026-07-19T10:00:00Z" }
    ];
    expect(sortRooms(rooms, () => undefined).map((r) => r.eventTitle)).toEqual([
      "Recent",
      "Alpha",
      "Zeta"
    ]);
  });
});
