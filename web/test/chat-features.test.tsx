import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ChatSearch } from "@/components/chat/chat-search";
import { RoomMediaButton } from "@/components/chat/room-media";
import { chatCapabilitiesSchema, chatMessageSchema, PIN_DURATIONS } from "@/lib/chat-api";

// `lib/api` is a server module: it wraps its readers in React's `cache`, which does not exist in the
// test environment. Everything under test here goes through the mocked actions anyway.
vi.mock("@/lib/api", () => ({
  api: { get: vi.fn(), post: vi.fn(), delete: vi.fn(), patch: vi.fn() }
}));

const searchMessagesAction = vi.fn();
const roomMediaAction = vi.fn();
const attachmentUrlAction = vi.fn();

vi.mock("@/lib/chat-actions", () => ({
  searchMessagesAction: (...args: unknown[]) => searchMessagesAction(...args),
  roomMediaAction: (...args: unknown[]) => roomMediaAction(...args),
  attachmentUrlAction: (...args: unknown[]) => attachmentUrlAction(...args)
}));

vi.mock("next/link", () => ({
  // See the note in discover-filter-sections.test.tsx: a click on a bare `<a href>` schedules a jsdom
  // navigation that exits the runner non-zero long after the test passed.
  default: ({ children, href }: { children: React.ReactNode; href: string }) => (
    <a href={href} onClick={(e) => e.preventDefault()}>{children}</a>
  )
}));

function hit(extra: Record<string, unknown> = {}) {
  return {
    messageId: "m1",
    roomId: "r1",
    roomLabel: "Design Summit",
    senderId: "u2",
    senderName: "Alice",
    body: "the keynote starts at nine",
    createdAt: "2026-08-01T09:00:00Z",
    ...extra
  };
}

beforeEach(() => {
  searchMessagesAction.mockReset();
  roomMediaAction.mockReset();
  attachmentUrlAction.mockReset();
});

describe("ChatSearch (D-295)", () => {
  it("does not query the server below the minimum length", async () => {
    render(<ChatSearch />);
    await userEvent.type(screen.getByLabelText("Search messages"), "a");

    // The server refuses a single character with `query_too_short`. Firing it anyway would turn a
    // hint into an error the reader cannot act on.
    await new Promise((r) => setTimeout(r, 350));
    expect(searchMessagesAction).not.toHaveBeenCalled();
  });

  it("searches once for a typed word rather than once per keystroke", async () => {
    searchMessagesAction.mockResolvedValue([hit()]);
    render(<ChatSearch />);

    await userEvent.type(screen.getByLabelText("Search messages"), "keynote");
    await waitFor(() => expect(screen.getByText(/the keynote starts at nine/)).toBeInTheDocument());

    // Debounced: seven characters past the minimum would otherwise be five full-text queries.
    expect(searchMessagesAction).toHaveBeenCalledTimes(1);
    expect(searchMessagesAction).toHaveBeenCalledWith("keynote", undefined);
  });

  it("links a hit to its room, not its event", async () => {
    searchMessagesAction.mockResolvedValue([hit()]);
    render(<ChatSearch />);
    await userEvent.type(screen.getByLabelText("Search messages"), "keynote");

    // D-292: navigation is keyed on roomId. A DM has no event, so an eventId link cannot resolve.
    const link = await screen.findByRole("link");
    expect(link).toHaveAttribute("href", "/chats/r1");
  });

  it("scopes to one room when asked", async () => {
    searchMessagesAction.mockResolvedValue([]);
    render(<ChatSearch roomId="r9" />);

    await userEvent.type(screen.getByLabelText("Search messages"), "agenda");
    await waitFor(() => expect(searchMessagesAction).toHaveBeenCalledWith("agenda", "r9"));
  });

  it("reports a failed search instead of rendering an empty result", async () => {
    searchMessagesAction.mockRejectedValue(new Error("offline"));
    render(<ChatSearch />);

    await userEvent.type(screen.getByLabelText("Search messages"), "agenda");

    // "No messages match" would be a lie: the difference between nothing found and nothing asked is
    // the whole point of the message.
    expect(await screen.findByText(/Search is unavailable/)).toBeInTheDocument();
    expect(screen.queryByText(/No messages match/)).not.toBeInTheDocument();
  });
});

describe("Shared media (D-295)", () => {
  it("loads nothing until the panel is opened", async () => {
    roomMediaAction.mockResolvedValue([]);
    render(<RoomMediaButton roomId="r1" />);

    // The query spans the room's whole history; most readers never open the panel.
    expect(roomMediaAction).not.toHaveBeenCalled();

    await userEvent.click(screen.getByRole("button", { name: "Shared media" }));
    await waitFor(() => expect(roomMediaAction).toHaveBeenCalledWith("r1"));
  });

  it("says so when a room has nothing shared", async () => {
    roomMediaAction.mockResolvedValue([]);
    render(<RoomMediaButton roomId="r1" />);
    await userEvent.click(screen.getByRole("button", { name: "Shared media" }));

    expect(await screen.findByText(/Nothing has been shared/)).toBeInTheDocument();
  });

  it("renders each item from the URL the list already returned", async () => {
    roomMediaAction.mockResolvedValue([
      {
        id: "a1",
        url: "https://signed.example/poster.png",
        fileName: "poster.png",
        contentType: "image/png",
        sizeBytes: 2048
      },
      {
        id: "a2",
        url: "https://signed.example/brief.pdf",
        fileName: "brief.pdf",
        contentType: "application/pdf",
        sizeBytes: 4096
      }
    ]);
    render(<RoomMediaButton roomId="r1" />);
    await userEvent.click(screen.getByRole("button", { name: "Shared media" }));

    // The list endpoint already presigns every row. Minting a second URL per item would be up to 60
    // extra round trips for links the client is holding.
    const img = await screen.findByAltText("poster.png");
    expect(img).toHaveAttribute("src", "https://signed.example/poster.png");
    expect(screen.getByRole("link", { name: /brief\.pdf/ })).toHaveAttribute(
      "href",
      "https://signed.example/brief.pdf"
    );
    expect(attachmentUrlAction).not.toHaveBeenCalled();
  });
});

describe("Pin windows (D-296)", () => {
  it("offers only durations inside the server's 1-hour..30-day bounds", () => {
    for (const d of PIN_DURATIONS) {
      expect(d.hours).toBeGreaterThanOrEqual(1);
      expect(d.hours).toBeLessThanOrEqual(24 * 30);
    }
  });

  it("parses a pinned message that carries an expiry", () => {
    const parsed = chatMessageSchema.parse({
      id: "m1",
      roomId: "r1",
      kind: "Text",
      body: "read this",
      isPinned: true,
      isDeleted: false,
      attachments: [],
      createdAt: "2026-08-01T09:00:00Z",
      pinnedUntil: "2026-08-08T09:00:00Z"
    });
    expect(parsed.pinnedUntil).toBe("2026-08-08T09:00:00Z");
  });

  it("still parses a message from a server that predates pin expiry", () => {
    // Optional rather than required: an older backend omits the field, and a hard requirement would
    // fail the whole parse — the failure mode D-292 already cost this surface once.
    const parsed = chatMessageSchema.parse({
      id: "m1",
      roomId: "r1",
      kind: "Text",
      body: "read this",
      isPinned: true,
      isDeleted: false,
      attachments: [],
      createdAt: "2026-08-01T09:00:00Z"
    });
    expect(parsed.pinnedUntil).toBeUndefined();
  });
});

describe("Chat moderator capabilities (D-301)", () => {
  const base = {
    canPost: true,
    canReply: true,
    canUpload: true,
    canPin: true,
    canDelete: true,
    canModerate: true,
    canMentionAll: false
  };

  it("defaults the host-only flags to false when an older server omits them", () => {
    // Absent must read as "no host powers" — the safe direction. A required field would instead fail
    // the whole parse, which is the D-292 failure mode.
    const caps = chatCapabilitiesSchema.parse(base);
    expect(caps.canManageRoom).toBe(false);
    expect(caps.canManageModerators).toBe(false);
    expect(caps.myRole).toBe("Member");
  });

  it("keeps room management separate from moderation", () => {
    // A Moderator: may moderate and pin, may NOT lock the room or promote. If these were ever derived
    // from canModerate, the UI would offer a Moderator two controls the server refuses.
    const moderator = chatCapabilitiesSchema.parse({
      ...base,
      canManageRoom: false,
      canManageModerators: false,
      myRole: "Moderator"
    });
    expect(moderator.canModerate).toBe(true);
    expect(moderator.canManageRoom).toBe(false);
    expect(moderator.canManageModerators).toBe(false);

    const host = chatCapabilitiesSchema.parse({
      ...base,
      canManageRoom: true,
      canManageModerators: true,
      myRole: "Host"
    });
    expect(host.canManageRoom).toBe(true);
    expect(host.canManageModerators).toBe(true);
  });
});
