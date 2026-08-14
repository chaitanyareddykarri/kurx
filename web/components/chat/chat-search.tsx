"use client";

import Link from "next/link";
import { Search, X } from "lucide-react";
import { useEffect, useRef, useState } from "react";
import { searchMessagesAction } from "@/lib/chat-actions";
import type { ChatSearchHit } from "@/lib/chat-api";

/// Message search (D-295). Sits above the conversation list and searches every room the reader is in;
/// pass `roomId` to scope it to one.
///
/// Debounced rather than search-as-you-type-per-keystroke: the server runs a full-text query per call,
/// and a minimum length plus a 250ms pause keeps a typed word to one query instead of five. The
/// minimum is the server's own, which rejects anything shorter with `query_too_short` — checking it
/// here too means the reader gets a hint instead of an error. Keep the two numbers equal: a stricter
/// client silently refuses a search the server would have answered.
const MIN_QUERY = 2;

export function ChatSearch({ roomId, placeholder }: { roomId?: string; placeholder?: string }) {
  const [q, setQ] = useState("");
  const [hits, setHits] = useState<ChatSearchHit[] | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Guards against an out-of-order response overwriting a newer one: a slow query for "ab" must not
  // replace the results for "abcd" the reader is already looking at.
  const latest = useRef(0);

  useEffect(() => {
    const term = q.trim();
    if (term.length < MIN_QUERY) {
      setHits(null);
      setError(null);
      return;
    }
    const seq = ++latest.current;
    setBusy(true);
    const timer = setTimeout(() => {
      searchMessagesAction(term, roomId)
        .then((result) => {
          if (seq !== latest.current) return;
          setHits(result);
          setError(null);
        })
        .catch(() => {
          if (seq !== latest.current) return;
          setHits(null);
          setError("Search is unavailable right now.");
        })
        .finally(() => {
          if (seq === latest.current) setBusy(false);
        });
    }, 250);
    return () => clearTimeout(timer);
  }, [q, roomId]);

  return (
    <div className="border-b border-border">
      <div className="relative px-3 py-2">
        <Search
          size={14}
          aria-hidden
          className="pointer-events-none absolute left-6 top-1/2 -translate-y-1/2 text-muted"
        />
        <input
          type="search"
          value={q}
          onChange={(e) => setQ(e.target.value)}
          placeholder={placeholder ?? "Search messages"}
          aria-label="Search messages"
          className="w-full rounded-md border border-border bg-surface py-2 pl-8 pr-8 text-sm"
        />
        {q && (
          <button
            type="button"
            onClick={() => setQ("")}
            aria-label="Clear search"
            className="absolute right-6 top-1/2 -translate-y-1/2 text-muted hover:text-text"
          >
            <X size={14} aria-hidden />
          </button>
        )}
      </div>

      {/* aria-live so a screen reader hears the count change without moving focus off the input. */}
      <div aria-live="polite" className="empty:hidden">
        {error && <p className="px-3 pb-2 text-xs text-danger">{error}</p>}
        {!error && hits && hits.length === 0 && !busy && (
          <p className="px-3 pb-2 text-xs text-muted">No messages match “{q.trim()}”.</p>
        )}
        {!error && hits && hits.length > 0 && (
          <ul className="max-h-64 overflow-y-auto">
            {hits.map((hit) => (
              <li key={hit.messageId} className="border-t border-border first:border-t-0">
                <Link
                  href={`/chats/${hit.roomId}`}
                  className="block px-3 py-2 text-sm hover:bg-elevated"
                >
                  <span className="flex items-baseline justify-between gap-2">
                    <span className="truncate text-xs text-muted">{hit.roomLabel}</span>
                    <span className="shrink-0 text-xs text-muted">
                      {new Date(hit.createdAt).toLocaleDateString()}
                    </span>
                  </span>
                  <span className="mt-0.5 block truncate">
                    <span className="text-muted">{hit.senderName ?? "System"}: </span>
                    {hit.body}
                  </span>
                </Link>
              </li>
            ))}
          </ul>
        )}
      </div>
    </div>
  );
}
