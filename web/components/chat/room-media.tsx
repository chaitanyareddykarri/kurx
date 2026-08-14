"use client";

import { Images } from "lucide-react";
import { useEffect, useState } from "react";
import { Dialog } from "@kurx/ui";
import { roomMediaAction } from "@/lib/chat-actions";
import type { ChatAttachment } from "@/lib/chat-api";
import { isImage, readableSize } from "@/lib/chat-upload";

/// Shared media for a room (D-295): everything ever attached, newest first, without scrolling the
/// history for it.
///
/// Loaded when the panel opens rather than with the room — most readers never open it, and the query
/// spans the room's entire history. Images render as thumbnails, everything else as a named row, which
/// is what makes the panel useful for finding the PDF someone posted last week.
export function RoomMediaButton({ roomId }: { roomId: string }) {
  const [open, setOpen] = useState(false);

  return (
    <>
      <button
        type="button"
        onClick={() => setOpen(true)}
        aria-label="Shared media"
        title="Shared media"
        className="rounded-md border border-border px-2 py-1 text-xs text-muted hover:bg-elevated"
      >
        <Images size={14} aria-hidden />
      </button>
      {open && <RoomMediaDialog roomId={roomId} onClose={() => setOpen(false)} />}
    </>
  );
}

function RoomMediaDialog({ roomId, onClose }: { roomId: string; onClose: () => void }) {
  const [items, setItems] = useState<ChatAttachment[] | null>(null);
  const [error, setError] = useState(false);

  useEffect(() => {
    let cancelled = false;
    roomMediaAction(roomId)
      .then((result) => !cancelled && setItems(result))
      .catch(() => !cancelled && setError(true));
    return () => {
      cancelled = true;
    };
  }, [roomId]);

  return (
    <Dialog open onClose={onClose} title="Shared media">
      {error && <p className="text-sm text-danger">Shared media could not be loaded.</p>}
      {!error && items === null && <p className="text-sm text-muted">Loading…</p>}
      {!error && items?.length === 0 && (
        <p className="text-sm text-muted">Nothing has been shared in this conversation yet.</p>
      )}
      {items && items.length > 0 && (
        <div className="max-h-[60vh] overflow-y-auto">
          <ul className="grid grid-cols-3 gap-2 sm:grid-cols-4">
            {items.filter((a) => isImage(a.contentType)).map((a) => (
              <li key={a.id}>
                <span className="block aspect-square overflow-hidden rounded-md border border-border bg-surface">
                  {/* eslint-disable-next-line @next/next/no-img-element -- signed, expiring URL */}
                  <img src={a.url} alt={a.fileName} className="h-full w-full object-cover" />
                </span>
              </li>
            ))}
          </ul>
          <ul className="mt-3 space-y-1">
            {items.filter((a) => !isImage(a.contentType)).map((a) => (
              <li key={a.id}>
                <a
                  href={a.url}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="flex items-baseline justify-between gap-2 rounded-md border border-border px-2 py-1.5 text-xs hover:bg-elevated"
                >
                  <span className="truncate">{a.fileName}</span>
                  <span className="shrink-0 text-muted">{readableSize(a.sizeBytes)}</span>
                </a>
              </li>
            ))}
          </ul>
        </div>
      )}
    </Dialog>
  );
}
