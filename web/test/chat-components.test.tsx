import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { MessageBubble } from "@/components/chat/message-bubble";
import { RoomList } from "@/components/chat/room-list";
import { toLocal, type LocalChatMessage } from "@/lib/chat-merge";
import type { MyChat } from "@/lib/chat-api";

const push = vi.fn();
vi.mock("next/navigation", () => ({ useRouter: () => ({ push }) }));

function msg(extra: Partial<LocalChatMessage> = {}): LocalChatMessage {
  return {
    ...toLocal({
      id: "m1",
      roomId: "room-1",
      clientMessageId: null,
      senderId: "user-2",
      senderName: "Alice",
      senderRole: "Member",
      kind: "Text",
      body: "hello",
      replyToMessageId: null,
      isPinned: false,
      isDeleted: false,
      attachments: [],
      createdAt: "2026-07-19T10:00:00Z"
    }),
    ...extra
  };
}

describe("MessageBubble", () => {
  it("renders an incoming message with its sender", () => {
    render(<MessageBubble message={msg()} isMine={false} showSender />);
    expect(screen.getByText("hello")).toBeInTheDocument();
    expect(screen.getByText("Alice")).toBeInTheDocument();
  });

  it("hides the sender on a consecutive message from the same person", () => {
    render(<MessageBubble message={msg()} isMine={false} showSender={false} />);
    expect(screen.getByText("hello")).toBeInTheDocument();
    expect(screen.queryByText("Alice")).not.toBeInTheDocument();
  });

  it("marks a host message", () => {
    render(<MessageBubble message={msg({ senderRole: "Host" })} isMine={false} showSender />);
    expect(screen.getByText("Host")).toBeInTheDocument();
  });

  it("shows send state — sending, sent, not read receipts", () => {
    const { rerender } = render(
      <MessageBubble message={msg({ status: "pending" })} isMine showSender={false} />
    );
    expect(screen.getByLabelText("Sending")).toBeInTheDocument();

    rerender(<MessageBubble message={msg()} isMine showSender={false} />);
    expect(screen.getByLabelText("Sent")).toBeInTheDocument();
  });

  it("offers retry and discard on a failed message", async () => {
    const user = userEvent.setup();
    const onRetry = vi.fn();
    const onDiscard = vi.fn();

    render(
      <MessageBubble
        message={msg({ status: "failed", clientMessageId: "c-1" })}
        isMine
        showSender={false}
        onRetry={onRetry}
        onDiscard={onDiscard}
      />
    );

    expect(screen.getByText("Not sent")).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Retry" }));
    await user.click(screen.getByRole("button", { name: "Discard" }));
    expect(onRetry).toHaveBeenCalledOnce();
    expect(onDiscard).toHaveBeenCalledOnce();
  });

  it("shows a tombstone for a deleted message, never the original body", () => {
    render(<MessageBubble message={msg({ isDeleted: true, body: "secret" })} isMine={false} showSender />);
    expect(screen.getByText("Message deleted")).toBeInTheDocument();
    expect(screen.queryByText("secret")).not.toBeInTheDocument();
  });

  it("renders a system message as a status notice", () => {
    render(
      <MessageBubble
        message={msg({ kind: "System", body: "Chat is now read-only.", senderName: null })}
        isMine={false}
        showSender
      />
    );
    const notice = screen.getByRole("status");
    expect(within(notice).getByText("Chat is now read-only.")).toBeInTheDocument();
  });

  it("opens actions on right-click without showing the browser menu", async () => {
    const user = userEvent.setup();
    const onOpenActions = vi.fn();
    render(
      <MessageBubble message={msg()} isMine={false} showSender onOpenActions={onOpenActions} />
    );

    await user.pointer({ target: screen.getByText("hello"), keys: "[MouseRight]" });
    expect(onOpenActions).toHaveBeenCalled();
  });

  it("exposes the timestamp as a machine-readable time element", () => {
    const { container } = render(<MessageBubble message={msg()} isMine={false} showSender />);
    expect(container.querySelector("time")).toHaveAttribute("datetime", "2026-07-19T10:00:00Z");
  });
});

describe("RoomList", () => {
  const rooms: MyChat[] = [
    {
      roomId: "r1",
      eventId: "ev-1",
      eventTitle: "Sunburn Festival",
      bannerUrl: null,
      lastMessagePreview: "See you there",
      unreadCount: 3,
      lastActivity: "2026-07-19T12:00:00Z",
      pinned: false,
      notificationsMuted: false,
      archived: false
    },
    {
      roomId: "r2",
      eventId: "ev-2",
      eventTitle: "Tech Summit",
      bannerUrl: null,
      lastMessagePreview: null,
      unreadCount: 0,
      lastActivity: "2026-07-19T09:00:00Z",
      pinned: false,
      notificationsMuted: false,
      archived: false
    }
  ];

  it("lists rooms with unread counts and a preview fallback", () => {
    render(<RoomList rooms={rooms} />);
    expect(screen.getByText("Sunburn Festival")).toBeInTheDocument();
    expect(screen.getByLabelText("3 unread messages")).toBeInTheDocument();
    expect(screen.getByText("No messages yet")).toBeInTheDocument();
  });

  // Direct conversations live in the Messages inbox tabs (D-264), not in this pane.
  it("has no direct-message section — this pane is the event half of Messages", () => {
    render(<RoomList rooms={rooms} />);
    expect(screen.queryByText(/direct/i)).not.toBeInTheDocument();
  });

  it("marks the active room for assistive tech", () => {
    render(<RoomList rooms={rooms} activeRoomId="r2" />);
    // Exact name, not a substring: the row's filing controls (D-306) also carry the event title, so
    // `/Tech Summit/` now matches four buttons. Naming the open button precisely is what this assertion
    // always meant.
    expect(screen.getByRole("button", { name: "Open Tech Summit" })).toHaveAttribute(
      "aria-current",
      "page"
    );
  });

  it("filters as you type", async () => {
    const user = userEvent.setup();
    render(<RoomList rooms={rooms} />);

    await user.type(screen.getByLabelText("Filter chats"), "tech");
    expect(screen.queryByText("Sunburn Festival")).not.toBeInTheDocument();
    expect(screen.getByText("Tech Summit")).toBeInTheDocument();
  });

  it("navigates by keyboard: arrow to move, Enter to open", async () => {
    const user = userEvent.setup();
    push.mockClear();
    render(<RoomList rooms={rooms} />);

    const filter = screen.getByLabelText("Filter chats");
    await user.click(filter);
    await user.keyboard("{ArrowDown}{Enter}");

    // Ordered newest-first, so ArrowDown lands on the second room. The route is keyed on the ROOM
    // id (D-292), so navigation asserts r2 rather than the event id it used to carry.
    expect(push).toHaveBeenCalledWith("/chats/r2");
  });

  it("opens a room on click — the deep-link URL is the route", async () => {
    const user = userEvent.setup();
    push.mockClear();
    render(<RoomList rooms={rooms} />);

    await user.click(screen.getByRole("button", { name: "Open Sunburn Festival" }));
    expect(push).toHaveBeenCalledWith("/chats/r1");
  });

  it("tells the user when a filter matches nothing", async () => {
    const user = userEvent.setup();
    render(<RoomList rooms={rooms} />);
    await user.type(screen.getByLabelText("Filter chats"), "zzzz");
    expect(screen.getByText(/No chats match/)).toBeInTheDocument();
  });
});

describe("accessibility", () => {
  it("the room list is a labelled list with reachable controls", () => {
    render(<RoomList rooms={[]} />);
    expect(screen.getByLabelText("Filter chats")).toBeInTheDocument();
    expect(screen.getByRole("list", { name: "Your event chats" })).toBeInTheDocument();
  });

  it("every message action is a real button, reachable by keyboard", async () => {
    const user = userEvent.setup();
    const onRetry = vi.fn();
    render(
      <MessageBubble
        message={msg({ status: "failed", clientMessageId: "c-1" })}
        isMine
        showSender={false}
        onRetry={onRetry}
        onDiscard={vi.fn()}
      />
    );

    await user.tab();
    expect(screen.getByRole("button", { name: "Retry" })).toHaveFocus();
    await user.keyboard("{Enter}");
    expect(onRetry).toHaveBeenCalled();
  });

  it("send status is announced by label rather than colour alone", () => {
    render(<MessageBubble message={msg({ status: "failed" })} isMine showSender={false} />);
    expect(screen.getByLabelText("Not sent")).toBeInTheDocument();
  });
});
