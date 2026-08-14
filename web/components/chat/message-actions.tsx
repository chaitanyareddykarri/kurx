"use client";

import { useEffect, useState } from "react";
import { Dialog } from "@kurx/ui";
import {
  banMemberAction,
  deleteMessageAction,
  editMessageAction,
  fetchMyChatsAction,
  forwardMessageAction,
  hideMessageAction,
  muteMemberAction,
  pinMessageAction,
  reportMessageAction,
  setModeratorAction,
  toggleReactionAction
} from "@/lib/chat-actions";
import { PIN_DURATIONS } from "@/lib/chat-api";
import type { MyChat } from "@/lib/chat-api";
import type { ChatRoom } from "@/lib/chat-api";
import type { LocalChatMessage } from "@/lib/chat-merge";

/// Per-message actions, including the host moderation workflow.
///
/// Every option is gated on the **server-computed capabilities** — never on `myRole` or the post
/// policy. The backend re-checks host role on each of these calls regardless, so this only decides
/// what to show, never what is allowed.
/// D-295 - the six offered on the quick row. Deliberately a short fixed set: a full picker is a
/// different component, and these cover the overwhelming majority of real reactions.
const QUICK_REACTIONS = ["👍", "❤️", "😂", "🎉", "👏", "🙏"];

export function MessageActions({
  message,
  room,
  isMine,
  onClose,
  onDone,
  onReply
}: {
  message: LocalChatMessage;
  room: ChatRoom;
  isMine: boolean;
  onClose: () => void;
  onDone: () => void;
  /// Hands the message back to the composer to quote. Purely client-side until send: the server has
  /// accepted `replyToMessageId` since chat shipped, and nothing ever offered it.
  onReply: (message: LocalChatMessage) => void;
}) {
  const [busy, setBusy] = useState(false);
  const [failure, setFailure] = useState<string>();
  // D-293: null = the action list; a string = the edit composer, seeded with the current body.
  const [draft, setDraft] = useState<string | null>(null);
  // D-295 - the forward picker. Rooms load on demand, not with the dialog: most opens are a delete
  // or a report, and fetching a room list for those would be a wasted round trip.
  const [forwarding, setForwarding] = useState(false);
  const [rooms, setRooms] = useState<MyChat[]>([]);

  // Esc closes — expected on desktop, and the only way out for keyboard users.
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") onClose();
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [onClose]);

  const run = async (fn: () => Promise<void>) => {
    setBusy(true);
    setFailure(undefined);
    try {
      await fn();
      onDone();
    } catch {
      setFailure("That action could not be completed.");
      setBusy(false);
    }
  };

  const canModerate = room.capabilities.canModerate;
  const canDelete = canModerate || (isMine && room.capabilities.canDelete);
  const canPin = room.capabilities.canPin;
  const senderId = message.senderId ?? undefined;
  // D-293. Only your own, only while the room accepts posts — an edit is speech, so it is gated on
  // the same right as sending. There is deliberately no host path: a host removes a message, never
  // rewrites one. The server enforces all of this; this only decides what to show.
  // `status === "sent"` matters: an unconfirmed or failed message has no server id yet, so there
  // is nothing for a PATCH to address.
  const canEdit =
    isMine && !message.isDeleted && room.capabilities.canPost && message.status === "sent";

  return (
    <Dialog open onClose={onClose} title="Message actions">
      <div className="space-y-3">
        {draft === null ? (
          <p className="line-clamp-3 rounded-md bg-elevated p-3 text-sm text-muted">{message.body}</p>
        ) : (
          <div className="space-y-2">
            <label htmlFor="edit-message" className="sr-only">
              Edit message
            </label>
            <textarea
              id="edit-message"
              autoFocus
              rows={3}
              value={draft}
              maxLength={1000}
              onChange={(e) => setDraft(e.target.value)}
              className="w-full rounded-md border border-border bg-bg p-3 text-sm text-text"
            />
            <div className="flex gap-2">
              <button
                type="button"
                disabled={busy || draft.trim() === message.body.trim() || draft.trim().length === 0}
                onClick={() => void run(async () => void (await editMessageAction(message.id, draft)))}
                className="rounded-md bg-accent px-3 py-2 text-sm text-on-accent disabled:opacity-50"
              >
                Save
              </button>
              <button
                type="button"
                disabled={busy}
                onClick={() => setDraft(null)}
                className="rounded-md border border-border px-3 py-2 text-sm disabled:opacity-50"
              >
                Cancel
              </button>
            </div>
          </div>
        )}

        {/* D-295 - quick reactions. One call; the server decides on or off, so a second tap removes
            rather than stacking. */}
        {draft === null && !forwarding && (
          <div className="flex gap-1.5">
            {QUICK_REACTIONS.map((emoji) => (
              <button
                key={emoji}
                type="button"
                disabled={busy}
                aria-label={`React ${emoji}`}
                onClick={() => void run(async () => void (await toggleReactionAction(message.id, emoji)))}
                className="rounded-md border border-border px-2.5 py-1.5 text-base hover:bg-elevated disabled:opacity-50"
              >
                <span aria-hidden>{emoji}</span>
              </button>
            ))}
          </div>
        )}

        {forwarding && (
          <div className="space-y-2">
            <p className="text-sm text-muted">Forward to</p>
            {rooms.filter((r) => r.roomId !== room.roomId).length === 0 ? (
              <p className="text-sm text-muted">No other conversations to forward to.</p>
            ) : (
              <ul className="max-h-56 divide-y divide-border overflow-y-auto rounded-md border border-border">
                {rooms
                  .filter((r) => r.roomId !== room.roomId)
                  .map((r) => (
                    <li key={r.roomId}>
                      <button
                        type="button"
                        disabled={busy}
                        onClick={() =>
                          void run(async () => void (await forwardMessageAction(message.id, r.roomId)))
                        }
                        className="w-full px-3 py-2 text-left text-sm hover:bg-elevated disabled:opacity-50"
                      >
                        {r.eventTitle}
                      </button>
                    </li>
                  ))}
              </ul>
            )}
            <button
              type="button"
              onClick={() => setForwarding(false)}
              className="rounded-md border border-border px-3 py-2 text-sm"
            >
              Cancel
            </button>
          </div>
        )}

        <div className={draft === null && !forwarding ? "flex flex-col gap-2" : "hidden"}>
          <button
            type="button"
            disabled={busy}
            onClick={() =>
              void (async () => {
                setForwarding(true);
                try {
                  setRooms(await fetchMyChatsAction());
                } catch {
                  setRooms([]);
                }
              })()
            }
            className="rounded-md border border-border px-3 py-2 text-left text-sm hover:bg-elevated disabled:opacity-50"
          >
            Forward message
          </button>

          {/* Reply. Gated on CanPost rather than a permission of its own — a reply is an ordinary
              message that happens to quote another, so anyone who may post may send one. Closes the
              sheet and hands the message to the composer; nothing is sent until the user sends. */}
          {room.capabilities.canPost && !message.isDeleted && (
            <button
              type="button"
              disabled={busy}
              onClick={() => {
                onReply(message);
                onClose();
              }}
              className="rounded-md border border-border px-3 py-2 text-left text-sm hover:bg-elevated disabled:opacity-50"
            >
              Reply
            </button>
          )}

          {canEdit && (
            <button
              type="button"
              disabled={busy}
              onClick={() => setDraft(message.body)}
              className="rounded-md border border-border px-3 py-2 text-left text-sm hover:bg-elevated disabled:opacity-50"
            >
              Edit message
            </button>
          )}

          {/* Distinct from "Delete message", which removes it for the whole room. This one is
              available to anyone in the room, including for other people's messages. */}
          <button
            type="button"
            disabled={busy}
            onClick={() => void run(() => hideMessageAction(message.id))}
            className="rounded-md border border-border px-3 py-2 text-left text-sm hover:bg-elevated disabled:opacity-50"
          >
            Delete for me
          </button>

          {/* D-296 — pinning asks for how long; unpinning is one click. A pin with no end date is the
              one a host never comes back to clear, so the duration is chosen up front, never implied. */}
          {canPin &&
            (message.isPinned ? (
              <button
                type="button"
                disabled={busy}
                onClick={() => void run(() => pinMessageAction(message.id, false))}
                className="rounded-md border border-border px-3 py-2 text-left text-sm hover:bg-elevated disabled:opacity-50"
              >
                Unpin message
              </button>
            ) : (
              <div className="rounded-md border border-border px-3 py-2">
                <p className="text-sm">Pin message for</p>
                <div className="mt-2 flex gap-2">
                  {PIN_DURATIONS.map((d) => (
                    <button
                      key={d.hours}
                      type="button"
                      disabled={busy}
                      onClick={() => void run(() => pinMessageAction(message.id, true, d.hours))}
                      className="rounded-md border border-border px-2 py-1 text-xs hover:bg-elevated disabled:opacity-50"
                    >
                      {d.label}
                    </button>
                  ))}
                </div>
              </div>
            ))}

          {canDelete && (
            <button
              type="button"
              disabled={busy}
              onClick={() => void run(() => deleteMessageAction(message.id))}
              className="rounded-md border border-border px-3 py-2 text-left text-sm hover:bg-elevated disabled:opacity-50"
            >
              Delete for everyone
            </button>
          )}

          {!isMine && (
            <button
              type="button"
              disabled={busy}
              onClick={() => void run(() => reportMessageAction(message.id, "Inappropriate"))}
              className="rounded-md border border-border px-3 py-2 text-left text-sm hover:bg-elevated disabled:opacity-50"
            >
              Report message
            </button>
          )}

          {/* Moderating the author, not the message — hosts do this from the message they saw. */}
          {canModerate && !isMine && senderId && (
            <>
              <button
                type="button"
                disabled={busy}
                onClick={() => void run(() => muteMemberAction(room.roomId, senderId, 60))}
                className="rounded-md border border-border px-3 py-2 text-left text-sm hover:bg-elevated disabled:opacity-50"
              >
                Mute author for 1 hour
              </button>
              <button
                type="button"
                disabled={busy}
                onClick={() => void run(() => banMemberAction(room.roomId, senderId))}
                className="rounded-md border border-danger/40 px-3 py-2 text-left text-sm text-danger hover:bg-danger/10 disabled:opacity-50"
              >
                Remove author from chat
              </button>
            </>
          )}

          {/* D-301 — promotion is HOST-only, so it is gated on `canManageModerators` and never on
              `canModerate`. A Moderator sees the two moderation buttons above and not these; the server
              refuses either direction from a Moderator regardless, so this only decides what to show. */}
          {room.capabilities.canManageModerators && !isMine && senderId && (
            <>
              <button
                type="button"
                disabled={busy}
                onClick={() => void run(() => setModeratorAction(room.roomId, senderId, true))}
                className="rounded-md border border-border px-3 py-2 text-left text-sm hover:bg-elevated disabled:opacity-50"
              >
                Make chat moderator
              </button>
              <button
                type="button"
                disabled={busy}
                onClick={() => void run(() => setModeratorAction(room.roomId, senderId, false))}
                className="rounded-md border border-border px-3 py-2 text-left text-sm hover:bg-elevated disabled:opacity-50"
              >
                Remove moderator role
              </button>
            </>
          )}
        </div>

        {failure && (
          <p role="alert" className="text-sm text-danger">
            {failure}
          </p>
        )}
      </div>
    </Dialog>
  );
}
