"use client";

import { Archive, Mail, MessageCircle } from "lucide-react";
import Link from "next/link";
import { useState, useTransition } from "react";
import { Button } from "@/components/ui/button";
import { EmptyState } from "@kurx/ui";
import { respondToDmRequestAction, setDmArchivedAction } from "@/lib/account-actions";
import { setMutedAction, setPinnedAction } from "@/lib/chat-actions";
import type { DmRoom } from "@/lib/account-api";

/// The Messages area (D-264): Direct · Events · Requests · Archived.
///
/// Event rooms keep their existing list component untouched — a DM and an event room are the same
/// `ChatRoom`, so the only thing that differs is what the row is *about*: a person rather than an event.

export type MessagesTab = "direct" | "events" | "requests" | "archived";

function initials(name: string) {
  return name.trim().slice(0, 1).toUpperCase() || "?";
}

function timeAgo(iso?: string | null) {
  if (!iso) return "";
  const diff = Date.now() - new Date(iso).getTime();
  const mins = Math.floor(diff / 60000);
  if (mins < 1) return "now";
  if (mins < 60) return `${mins}m`;
  const hours = Math.floor(mins / 60);
  if (hours < 24) return `${hours}h`;
  return `${Math.floor(hours / 24)}d`;
}

function DmRow({
  room,
  children
}: {
  room: DmRoom;
  children?: React.ReactNode;
}) {
  return (
    <li className="flex items-center gap-3 border-b border-border px-3 py-3 last:border-b-0">
      <span
        aria-hidden
        className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-surface text-sm text-muted"
      >
        {initials(room.other_name)}
      </span>
      <Link href={`/chats/${room.room_id}`} className="min-w-0 flex-1">
        <span className="flex items-baseline justify-between gap-2">
          <span className="flex min-w-0 items-center gap-1.5">
            {/* D-295 — pinned and muted are stated in text for screen readers, not colour alone. */}
            {room.pinned && (
              <span aria-label="Pinned" title="Pinned" className="shrink-0 text-xs text-muted">
                📌
              </span>
            )}
            {room.notifications_muted && (
              <span aria-label="Muted" title="Muted" className="shrink-0 text-xs text-muted">
                🔕
              </span>
            )}
            <span className="truncate text-sm font-medium text-text">{room.other_name}</span>
          </span>
          <span className="shrink-0 text-xs text-muted">{timeAgo(room.last_activity)}</span>
        </span>
        <span className="mt-0.5 flex items-center gap-2">
          <span className="truncate text-xs text-muted">
            {room.last_message_preview ?? "No messages yet"}
          </span>
          {room.unread_count > 0 ? (
            <span className="ml-auto shrink-0 rounded-full bg-accent px-1.5 py-0.5 text-[11px] text-white">
              {room.unread_count}
            </span>
          ) : null}
        </span>
      </Link>
      {children}
    </li>
  );
}

export function MessagesTabs({
  direct,
  requests,
  archived,
  eventList
}: {
  direct: DmRoom[];
  requests: DmRoom[];
  archived: DmRoom[];
  eventList: React.ReactNode;
}) {
  const [tab, setTab] = useState<MessagesTab>(direct.length === 0 && requests.length > 0 ? "requests" : "direct");
  const [dmRooms, setDmRooms] = useState(direct);
  const [requestRooms, setRequestRooms] = useState(requests);
  const [archivedRooms, setArchivedRooms] = useState(archived);
  const [error, setError] = useState<string | null>(null);
  const [, startTransition] = useTransition();

  function respond(room: DmRoom, accept: boolean) {
    setError(null);
    setRequestRooms((cur) => cur.filter((r) => r.room_id !== room.room_id));
    if (accept) setDmRooms((cur) => [{ ...room, is_request: false, request_state: "accepted" }, ...cur]);

    startTransition(async () => {
      const result = await respondToDmRequestAction(room.room_id, accept);
      if ("error" in result) {
        setError(result.error);
        setRequestRooms((cur) => [room, ...cur]);
        if (accept) setDmRooms((cur) => cur.filter((r) => r.room_id !== room.room_id));
      }
    });
  }

  function archive(room: DmRoom, next: boolean) {
    setError(null);
    if (next) {
      setDmRooms((cur) => cur.filter((r) => r.room_id !== room.room_id));
      setArchivedRooms((cur) => [{ ...room, archived: true }, ...cur]);
    } else {
      setArchivedRooms((cur) => cur.filter((r) => r.room_id !== room.room_id));
      setDmRooms((cur) => [{ ...room, archived: false }, ...cur]);
    }
    startTransition(async () => {
      const result = await setDmArchivedAction(room.room_id, next);
      if ("error" in result) setError(result.error);
    });
  }

  // D-295 — pin and mute. Optimistic like `archive` above: the row moves at once and the server call
  // is forward-only, so a failure is corrected by the next read rather than needing a rollback path.
  function pin(room: DmRoom, next: boolean) {
    setDmRooms((cur) =>
      cur
        .map((r) => (r.room_id === room.room_id ? { ...r, pinned: next } : r))
        // Pinned first, matching the server's own ordering — otherwise the row would jump on refresh.
        .sort((a, b) => Number(b.pinned) - Number(a.pinned))
    );
    startTransition(async () => {
      try {
        await setPinnedAction(room.room_id, next);
      } catch {
        setError("That conversation could not be pinned.");
      }
    });
  }

  function mute(room: DmRoom, next: boolean) {
    setDmRooms((cur) =>
      cur.map((r) => (r.room_id === room.room_id ? { ...r, notifications_muted: next } : r))
    );
    startTransition(async () => {
      try {
        // Eight hours: the duration people actually pick. An indefinite mute is the one users forget
        // they set and then report as missing notifications.
        await setMutedAction(room.room_id, next ? new Date(Date.now() + 8 * 3600_000).toISOString() : null);
      } catch {
        setError("That conversation could not be muted.");
      }
    });
  }

  const TABS: { key: MessagesTab; label: string; count?: number }[] = [
    { key: "direct", label: "Direct", count: dmRooms.length },
    { key: "events", label: "Events" },
    { key: "requests", label: "Requests", count: requestRooms.length },
    { key: "archived", label: "Archived", count: archivedRooms.length }
  ];

  return (
    <div className="flex min-h-0 flex-col">
      <nav className="flex gap-1 overflow-x-auto border-b border-border px-2" role="tablist">
        {TABS.map((t) => (
          <button
            key={t.key}
            role="tab"
            aria-selected={tab === t.key}
            onClick={() => setTab(t.key)}
            className={`whitespace-nowrap border-b-2 px-3 py-2.5 text-sm font-medium transition ${
              tab === t.key ? "border-accent text-text" : "border-transparent text-muted hover:text-text"
            }`}
          >
            {t.label}
            {t.count ? <span className="ml-1.5 text-xs text-muted">{t.count}</span> : null}
          </button>
        ))}
      </nav>

      {error ? <p className="px-3 py-2 text-sm text-danger">{error}</p> : null}

      <div className="min-h-0 flex-1 overflow-y-auto">
        {tab === "direct" ? (
          dmRooms.length === 0 ? (
            <div className="p-6">
              <EmptyState
                icon={<MessageCircle size={32} />}
                title="No conversations"
                message="Open someone's profile and tap Message to start one."
              />
            </div>
          ) : (
            <ul>
              {dmRooms.map((room) => (
                <DmRow key={room.room_id} room={room}>
                  {/* D-295 — pin and mute are per-member filing, like archive beside them. Both are
                      optimistic: the row updates immediately and the server call is forward-only, so
                      a failure self-corrects on the next read rather than needing a rollback. */}
                  <button
                    onClick={() => void pin(room, !room.pinned)}
                    className="shrink-0 text-xs text-muted hover:text-text"
                    aria-label={`${room.pinned ? "Unpin" : "Pin"} conversation with ${room.other_name}`}
                  >
                    {room.pinned ? "Unpin" : "Pin"}
                  </button>
                  <button
                    onClick={() => void mute(room, !room.notifications_muted)}
                    className="shrink-0 text-xs text-muted hover:text-text"
                    aria-label={`${room.notifications_muted ? "Unmute" : "Mute"} conversation with ${room.other_name}`}
                  >
                    {room.notifications_muted ? "Unmute" : "Mute"}
                  </button>
                  <button
                    onClick={() => archive(room, true)}
                    className="shrink-0 text-xs text-muted hover:text-text"
                    aria-label={`Archive conversation with ${room.other_name}`}
                  >
                    Archive
                  </button>
                </DmRow>
              ))}
            </ul>
          )
        ) : null}

        {tab === "events" ? eventList : null}

        {tab === "requests" ? (
          requestRooms.length === 0 ? (
            <div className="p-6">
              <EmptyState
                icon={<Mail size={32} />}
                title="No message requests"
                message="Messages from people you're not connected to wait here first."
              />
            </div>
          ) : (
            <ul>
              {requestRooms.map((room) => (
                <DmRow key={room.room_id} room={room}>
                  <span className="flex shrink-0 gap-2">
                    <Button onClick={() => respond(room, true)}>Accept</Button>
                    <Button variant="secondary" onClick={() => respond(room, false)}>Decline</Button>
                  </span>
                </DmRow>
              ))}
            </ul>
          )
        ) : null}

        {tab === "archived" ? (
          archivedRooms.length === 0 ? (
            <div className="p-6">
              <EmptyState icon={<Archive size={32} />} title="Nothing archived" message="Archived conversations appear here." />
            </div>
          ) : (
            <ul>
              {archivedRooms.map((room) => (
                <DmRow key={room.room_id} room={room}>
                  <button
                    onClick={() => archive(room, false)}
                    className="shrink-0 text-xs text-muted hover:text-text"
                  >
                    Unarchive
                  </button>
                </DmRow>
              ))}
            </ul>
          )
        ) : null}
      </div>
    </div>
  );
}
