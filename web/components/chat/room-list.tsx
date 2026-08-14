"use client";

import { useRouter } from "next/navigation";
import { useEffect, useMemo, useRef, useState, useTransition } from "react";
import { Avatar } from "@/components/ui/avatar";
import type { MyChat } from "@/lib/chat-api";
import { setChatArchivedAction, setMutedAction, setPinnedAction } from "@/lib/chat-actions";
import { sortRooms } from "@/lib/chat-merge";

/// The event-rooms sidebar.
///
/// Direct conversations are not listed here — they live in the Messages inbox tabs (D-264), which is
/// the surface that owns Direct / Requests / Archived. This pane is the event half of that split.
///
/// Desktop affordances the Flutter list has no equivalent for: type-to-filter, ↑/↓ to move between
/// rooms, Enter to open. Behaviour (which rooms, what ordering, what counts) is identical.
export function RoomList({
  rooms,
  activeRoomId
}: {
  rooms: MyChat[];
  /// The ROOM id, not the event id (D-292) — navigation is keyed on ChatRoom identity so an event
  /// room and a direct room share one route.
  activeRoomId?: string;
}) {
  const router = useRouter();
  const [filter, setFilter] = useState("");
  const [cursor, setCursor] = useState(0);
  const listRef = useRef<HTMLUListElement>(null);
  const [, startTransition] = useTransition();
  const [local, setLocal] = useState(rooms);
  const [error, setError] = useState<string | null>(null);

  // The server list is authoritative; local state exists only so a filing action shows immediately.
  useEffect(() => setLocal(rooms), [rooms]);

  /// Optimistic, matching the DM rows in `messages-tabs`: paint the change, then call. Every one of
  /// these is forward-only personal state, so a failed call self-corrects on the next read rather than
  /// needing a rollback — but the user is told, because silently reverting a tap they made is worse
  /// than an explanation.
  function file(room: MyChat, next: { pinned?: boolean; muted?: boolean; archived?: boolean }) {
    setError(null);
    setLocal((cur) =>
      cur.map((r) =>
        r.roomId !== room.roomId
          ? r
          : {
              ...r,
              pinned: next.pinned ?? r.pinned,
              notificationsMuted: next.muted ?? r.notificationsMuted,
              archived: next.archived ?? r.archived
            }
      )
    );
    startTransition(async () => {
      try {
        if (next.pinned !== undefined) await setPinnedAction(room.roomId, next.pinned);
        if (next.muted !== undefined)
          await setMutedAction(room.roomId, next.muted ? new Date(Date.now() + 8 * 3600_000).toISOString() : null);
        if (next.archived !== undefined) await setChatArchivedAction(room.roomId, next.archived);
      } catch {
        setError("That change could not be saved. It will correct itself on refresh.");
      }
    });
  }

  // `lastActivity` is the last message's timestamp as of D-292 (it was the event's updated-at, which
  // sorted this list by something unrelated to chat). No local message store on web, so the server
  // value is what we sort on.
  const ordered = useMemo(() => sortRooms(local, () => undefined), [local]);

  const visible = useMemo(() => {
    const q = filter.trim().toLowerCase();
    return q ? ordered.filter((r) => r.eventTitle.toLowerCase().includes(q)) : ordered;
  }, [ordered, filter]);

  useEffect(() => {
    setCursor(0);
  }, [filter]);

  const open = (roomId: string) => router.push(`/chats/${roomId}`);

  return (
    <div className="flex h-full min-h-0 flex-col">
      <div className="border-b border-border p-3">
        <label htmlFor="room-filter" className="sr-only">
          Filter chats
        </label>
        <input
          id="room-filter"
          value={filter}
          onChange={(e) => setFilter(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === "ArrowDown") {
              e.preventDefault();
              setCursor((c) => Math.min(c + 1, visible.length - 1));
            } else if (e.key === "ArrowUp") {
              e.preventDefault();
              setCursor((c) => Math.max(c - 1, 0));
            } else if (e.key === "Enter" && visible[cursor]) {
              e.preventDefault();
              open(visible[cursor].roomId);
            }
          }}
          placeholder="Filter chats…"
          className="w-full rounded-md border border-border bg-surface px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-accent"
        />
      </div>

      {error && (
        <p role="alert" className="px-4 pb-2 text-xs text-danger">
          {error}
        </p>
      )}

      <ul ref={listRef} className="min-h-0 flex-1 overflow-y-auto" aria-label="Your event chats">
        {visible.length === 0 && (
          <li className="px-4 py-6 text-sm text-muted">No chats match “{filter}”.</li>
        )}

        {visible.map((room, i) => {
          const active = room.roomId === activeRoomId;
          return (
            <li
              key={room.roomId}
              className={[
                "flex items-center border-b border-border transition",
                active ? "bg-elevated" : i === cursor ? "bg-elevated/50" : "hover:bg-elevated/50"
              ].join(" ")}
            >
              <button
                type="button"
                onClick={() => open(room.roomId)}
                aria-current={active ? "page" : undefined}
                // Explicit, because the row is no longer the only button in its `li` (D-306): the
                // filing controls beside it also name the room, so "the button for this room" needs to
                // be something assistive tech — and a test — can address exactly.
                aria-label={`Open ${room.eventTitle}`}
                className="flex min-w-0 flex-1 items-center gap-3 px-4 py-3 text-left"
              >
                <Avatar name={room.eventTitle} src={room.bannerUrl ?? undefined} size={36} />
                <span className="min-w-0 flex-1">
                  <span
                    className={`block truncate text-sm ${room.unreadCount > 0 ? "font-bold" : "font-semibold"}`}
                  >
                    {room.eventTitle}
                  </span>
                  <span className="block truncate text-xs text-muted">
                    {room.lastMessagePreview || "No messages yet"}
                  </span>
                </span>
                {room.unreadCount > 0 && (
                  <span
                    className="shrink-0 rounded-full bg-accent px-1.5 py-0.5 text-[10px] font-bold text-on-accent"
                    aria-label={`${room.unreadCount} unread messages`}
                  >
                    {room.unreadCount > 99 ? "99+" : room.unreadCount}
                  </span>
                )}
              </button>

              {/* D-306 — the event half of the conversation menu. These three are personal filing on
                  `ChatMember`, identical in meaning to the DM controls in `messages-tabs`, and the
                  event rows simply never got them: the whole menu was bound to the DM list, so an event
                  chat could not be pinned, muted or archived from anywhere on web. Siblings of the open
                  button rather than children, because a button cannot nest inside a button. */}
              <span className="flex shrink-0 items-center gap-2 pr-3">
                <button
                  type="button"
                  onClick={() => void file(room, { pinned: !room.pinned })}
                  className="text-xs text-muted hover:text-text"
                  aria-label={`${room.pinned ? "Unpin" : "Pin"} ${room.eventTitle}`}
                >
                  {room.pinned ? "Unpin" : "Pin"}
                </button>
                <button
                  type="button"
                  onClick={() => void file(room, { muted: !room.notificationsMuted })}
                  className="text-xs text-muted hover:text-text"
                  aria-label={`${room.notificationsMuted ? "Unmute" : "Mute"} ${room.eventTitle}`}
                >
                  {room.notificationsMuted ? "Unmute" : "Mute"}
                </button>
                <button
                  type="button"
                  onClick={() => void file(room, { archived: !room.archived })}
                  className="text-xs text-muted hover:text-text"
                  aria-label={`${room.archived ? "Restore" : "Archive"} ${room.eventTitle}`}
                >
                  {room.archived ? "Restore" : "Archive"}
                </button>
              </span>
            </li>
          );
        })}
      </ul>
    </div>
  );
}
