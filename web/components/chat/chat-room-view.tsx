"use client";

import { Lock, MessagesSquare, Paperclip, Pin, Radio, Send, UploadCloud, Video } from "lucide-react";
import { useCallback, useEffect, useMemo, useRef, useState, useTransition } from "react";
import { EmptyState, ErrorState, ListSkeleton } from "@kurx/ui";
import { MessageBubble } from "@/components/chat/message-bubble";
import { MessageActions } from "@/components/chat/message-actions";
import { AttachmentView } from "@/components/chat/attachment-view";
import { RoomMediaButton } from "@/components/chat/room-media";
import { VoiceRecorder } from "@/components/chat/voice-recorder";
import { useAttachmentUploads } from "@/lib/use-attachment-uploads";
import { canSend, pendingAsAttachment } from "@/lib/chat-upload";
import { OnlineCount, TypingBanner } from "@/components/chat/presence";
import { cannotPostReason, type LocalChatMessage } from "@/lib/chat-merge";
import {
  activeTypists,
  highestDeliveredByOthers,
  highestReadByOthers,
  isOnline,
  onlineCount
} from "@/lib/chat-presence";
import { useChatRoom } from "@/lib/use-chat-room";
import { respondToDmRequestAction } from "@/lib/account-actions";
import { markDeliveredAction, toggleReactionAction } from "@/lib/chat-actions";

/// The room pane. Desktop-first: wide message column, keyboard-driven composer, right-click
/// moderation. Behaviour is identical to Flutter — same endpoints, ordering, optimistic flow and
/// capability checks — but the layout is built for a mouse and a large screen rather than ported.
export function ChatRoomView({
  roomId,
  title,
  currentUserId
}: {
  roomId: string;
  title: string;
  currentUserId?: string;
}) {
  const {
    room, messages, loading, loadingOlder, hasMoreOlder, live, error, presence, notifyTyping,
    loadOlder, send, retry, discard, refresh
  } = useChatRoom(roomId, currentUserId);

  // Derived once per render and handed down as plain values, so no component re-derives presence
  // and no component keeps its own copy.
  const typists = useMemo(() => activeTypists(presence, currentUserId), [presence, currentUserId]);
  const readUpTo = useMemo(
    () => highestReadByOthers(presence, currentUserId),
    [presence, currentUserId]
  );
  const deliveredUpTo = useMemo(
    () => highestDeliveredByOthers(presence, currentUserId),
    [presence, currentUserId]
  );
  const others = onlineCount(presence, currentUserId);

  const uploads = useAttachmentUploads(room?.roomId);
  const [draft, setDraft] = useState("");
  const [dragging, setDragging] = useState(false);
  const dragDepth = useRef(0);
  const fileInputRef = useRef<HTMLInputElement>(null);
  const videoInputRef = useRef<HTMLInputElement>(null);
  const [actionsFor, setActionsFor] = useState<LocalChatMessage | undefined>();
  /// D-295/D-301 — the message the composer is quoting, or undefined. Client-side only until send:
  /// `replyToMessageId` has been accepted by the server and carried by this client's API layer since
  /// chat shipped, and no UI ever set it.
  const [replyingTo, setReplyingTo] = useState<LocalChatMessage | undefined>();
  const scrollRef = useRef<HTMLDivElement>(null);
  const bottomRef = useRef<HTMLDivElement>(null);
  const wasAtBottom = useRef(true);

  // Only auto-scroll when the reader was already at the bottom — yanking someone out of history
  // they are reading is the classic chat bug.
  useEffect(() => {
    if (wasAtBottom.current) bottomRef.current?.scrollIntoView({ block: "end" });
  }, [messages]);

  const onScroll = useCallback(() => {
    const el = scrollRef.current;
    if (!el) return;
    wasAtBottom.current = el.scrollHeight - el.scrollTop - el.clientHeight < 80;
    if (el.scrollTop < 200) void loadOlder();
  }, [loadOlder]);

  const submit = useCallback(async () => {
    // Refuses while anything is still uploading: a message must never become permanent referencing
    // an attachment the server has not confirmed.
    if (!canSend(draft, uploads.uploads)) return;

    const attachmentIds = uploads.uploads
      .filter((u) => u.status === "done" && u.attachmentId)
      .map((u) => u.attachmentId as string);

    const text = draft.trim();
    const replyTo = replyingTo?.id;
    setDraft("");
    // Cleared before the await, not after: the reply is captured above, and leaving the banner up
    // during the round trip invites a second send that quotes the same message twice.
    setReplyingTo(undefined);
    wasAtBottom.current = true;
    await send(text, attachmentIds, replyTo);
    // Reconciled into the sent message — the pending strip is done with them.
    uploads.clear();
  }, [draft, replyingTo, send, uploads]);

  /// Every source — picker, drop, paste — funnels through this, so there is one pipeline and one
  /// set of rules rather than three.
  const acceptFiles = useCallback(
    (files: FileList | null | undefined) => {
      if (!files || files.length === 0) return;
      if (!room?.capabilities.canUpload) return;
      uploads.add(Array.from(files));
    },
    [room?.capabilities.canUpload, uploads]
  );

  // Drag depth counter: dragenter/dragleave fire for every child element, so a naive boolean
  // flickers the overlay as the pointer crosses inner nodes.
  const onDragEnter = useCallback((e: React.DragEvent) => {
    if (!e.dataTransfer?.types?.includes("Files")) return;
    e.preventDefault();
    dragDepth.current += 1;
    setDragging(true);
  }, []);

  const onDragLeave = useCallback((e: React.DragEvent) => {
    if (!e.dataTransfer?.types?.includes("Files")) return;
    e.preventDefault();
    dragDepth.current = Math.max(0, dragDepth.current - 1);
    if (dragDepth.current === 0) setDragging(false);
  }, []);

  const onDrop = useCallback(
    (e: React.DragEvent) => {
      e.preventDefault();
      dragDepth.current = 0;
      setDragging(false);
      acceptFiles(e.dataTransfer?.files);
    },
    [acceptFiles]
  );

  /// Clipboard paste. Needs no backend change — a pasted screenshot arrives as a File on the
  /// ClipboardEvent and goes through the same pipeline as any other source.
  const onPaste = useCallback(
    (e: React.ClipboardEvent) => {
      const files = Array.from(e.clipboardData?.files ?? []);
      if (files.length === 0) return;
      e.preventDefault();
      if (!room?.capabilities.canUpload) return;
      uploads.add(files);
    },
    [room?.capabilities.canUpload, uploads]
  );

  // D-295 — acknowledge DELIVERY of the newest message this device holds. Deliberately not the read
  // pointer: arriving is not reading, and conflating them would mark every message read the instant
  // it landed, whether or not anyone looked. Server-side it is forward-only, so a repeat is a no-op.
  const newestId = messages.length > 0 ? messages[messages.length - 1]?.id : undefined;
  const ackedRef = useRef<string | undefined>();
  useEffect(() => {
    if (!newestId || newestId.startsWith("local-") || ackedRef.current === newestId) return;
    ackedRef.current = newestId;
    void markDeliveredAction(roomId, newestId).catch(() => {
      // Best effort. The pointer only moves forward, so a later successful call supersedes this one.
    });
  }, [roomId, newestId]);

  // D-295 — one stable handler for every bubble. Created per render inside the map instead, each
  // bubble would receive a new function identity and the memo on MessageBubble would never skip one.
  // The server returns the whole summary, so a refresh is how the row reconciles.
  const react = useCallback(
    async (messageId: string, emoji: string) => {
      try {
        await toggleReactionAction(messageId, emoji);
        await refresh();
      } catch {
        // A failed reaction is not worth interrupting the conversation for; the next read corrects it.
      }
    },
    [refresh]
  );

  const blockedReason = room ? cannotPostReason(room) : null;
  const canPost = room?.capabilities.canPost ?? false;

  // D-292 — a message request awaiting THIS reader's decision. The sender's own pending request is
  // not one of these: only the recipient decides, so they get the ordinary composer and no banner.
  const awaitingMyDecision =
    room?.dmRequestState === "pending" && room.dmInitiatedBy !== currentUserId;

  return (
    <section
      className="relative flex h-full min-h-0 flex-col"
      aria-label={`Chat for ${title}`}
      onDragEnter={onDragEnter}
      onDragOver={(e) => {
        if (e.dataTransfer?.types?.includes("Files")) e.preventDefault();
      }}
      onDragLeave={onDragLeave}
      onDrop={onDrop}
    >
      {dragging && room?.capabilities.canUpload && (
        <div
          role="presentation"
          className="pointer-events-none absolute inset-0 z-20 flex items-center justify-center border-2 border-dashed border-accent bg-bg/85"
        >
          <span className="flex flex-col items-center gap-2 text-sm font-semibold text-accent">
            <UploadCloud size={28} aria-hidden />
            Drop files to attach
          </span>
        </div>
      )}
      <header className="flex items-center justify-between gap-4 border-b border-border px-5 py-3">
        <div className="min-w-0">
          <h2 className="truncate text-lg font-semibold">{title}</h2>
          <p className="text-xs text-muted">
            {room?.status === "Locked" ? "Read-only" : "Event chat"}
            {room?.myRole === "Host" && " · You are a host"}
          </p>
        </div>
        <span className="flex shrink-0 items-center gap-2">
          <RoomMediaButton roomId={roomId} />
          <OnlineCount count={others} />
          {live && (
            <span className="inline-flex shrink-0 items-center gap-1.5 rounded-full border border-success/30 px-2.5 py-1 text-xs text-success">
              <Radio size={12} aria-hidden /> Live
            </span>
          )}
        </span>
      </header>

      {room?.status === "Locked" && (
        <p className="flex items-center gap-2 border-b border-border bg-elevated px-5 py-2 text-xs text-muted">
          <Lock size={12} aria-hidden /> This chat is read-only.
        </p>
      )}

      {/* D-296 — the pinned strip. A pin whose only trace is an icon on the original bubble surfaces
          nothing: the point of pinning is that the message is readable without scrolling for it. The
          server already excludes expired pins, so anything here is still in force. */}
      {room && room.pinnedMessages.length > 0 && (
        <div className="border-b border-border bg-elevated px-5 py-2">
          {room.pinnedMessages.map((m) => (
            <p key={m.id} className="flex items-baseline gap-2 text-xs">
              <Pin size={11} aria-hidden className="shrink-0 translate-y-0.5 text-muted" />
              <span className="min-w-0 flex-1 truncate">
                <span className="text-muted">{m.senderName ?? "System"}: </span>
                {m.body}
              </span>
              {m.pinnedUntil && (
                <span className="shrink-0 text-muted">until {formatPinExpiry(m.pinnedUntil)}</span>
              )}
            </p>
          ))}
        </div>
      )}

      <div
        ref={scrollRef}
        onScroll={onScroll}
        className="min-h-0 flex-1 overflow-y-auto px-5 py-4"
        role="log"
        aria-live="polite"
        aria-label="Messages"
      >
        {loading && messages.length === 0 && <ListSkeleton rows={6} />}

        {!loading && Boolean(error) && messages.length === 0 && (
          <ErrorState message="This chat could not be loaded." onRetry={() => void refresh()} />
        )}

        {!loading && !error && messages.length === 0 && (
          <EmptyState
            icon={<MessagesSquare size={32} />}
            title="No messages yet"
            message="Say hello to everyone attending this event."
          />
        )}

        {loadingOlder && <p className="pb-3 text-center text-xs text-muted">Loading earlier messages…</p>}
        {!hasMoreOlder && messages.length > 0 && (
          <p className="pb-3 text-center text-xs text-muted">Start of the conversation</p>
        )}

        {messages.map((m, i) => {
          const previous = i > 0 ? messages[i - 1] : undefined;
          const isMine =
            (currentUserId != null && m.senderId === currentUserId) || m.status !== "sent";
          return (
            <MessageBubble
              key={m.id}
              message={m}
              isMine={isMine}
              showSender={
                !previous || previous.senderId !== m.senderId || previous.kind === "System"
              }
              senderOnline={isOnline(presence, m.senderId)}
              presenceKnown={presence.enabled && m.senderId != null}
              // UUIDv7 ids sort by time, which is the order the read pointer advances in.
              read={readUpTo !== undefined && m.id <= readUpTo}
              delivered={deliveredUpTo !== undefined && m.id <= deliveredUpTo}
              onRetry={m.clientMessageId ? () => void retry(m.clientMessageId!) : undefined}
              onDiscard={m.clientMessageId ? () => discard(m.clientMessageId!) : undefined}
              onOpenActions={m.status === "sent" && m.kind !== "System" ? () => setActionsFor(m) : undefined}
              onReact={
                m.status === "sent" && m.kind !== "System"
                  ? (emoji: string) => void react(m.id, emoji)
                  : undefined
              }
            />
          );
        })}
        <div ref={bottomRef} />
      </div>

      <TypingBanner typists={typists} />

      {/* D-292: a message request, answered in the conversation rather than only from the inbox.
          The composer stays below it on purpose — replying IS accepting (the rule every messenger
          uses, and what SendMessageAsync now enforces), so this exists to make Decline reachable
          and to say plainly what state the conversation is in. */}
      {awaitingMyDecision && room && (
        <DmRequestBanner roomId={room.roomId} onAnswered={refresh} />
      )}

      {canPost ? (
        <form
          className="border-t border-border px-5 py-3"
          onSubmit={(e) => {
            e.preventDefault();
            void submit();
          }}
        >
          {/* The quoted message, above the composer and dismissible. Sits here rather than inside the
              textarea so it survives the draft being cleared and is obvious before sending. */}
          {replyingTo && (
            <div className="mb-2 flex items-start gap-2 rounded-md border-l-2 border-accent bg-elevated px-3 py-2">
              <span className="min-w-0 flex-1">
                <span className="block text-xs font-semibold text-accent">
                  Replying to {replyingTo.senderName ?? "a message"}
                </span>
                <span className="block truncate text-xs text-muted">
                  {replyingTo.body || "Attachment"}
                </span>
              </span>
              <button
                type="button"
                onClick={() => setReplyingTo(undefined)}
                aria-label="Cancel reply"
                className="shrink-0 text-xs text-muted hover:text-text"
              >
                Cancel
              </button>
            </div>
          )}

          {/* Pending uploads sit above the composer, not in the message list: no permanent message
              exists until every upload has confirmed. */}
          {uploads.uploads.length > 0 && (
            <ul className="mb-2 flex flex-col gap-1" aria-label="Attachments to send">
              {uploads.uploads.map((u) => (
                <li key={u.localId}>
                  <AttachmentView
                    attachment={pendingAsAttachment(u)}
                    pending={u}
                    onCancel={() => uploads.cancel(u.localId)}
                    onRetry={() => uploads.retry(u.localId)}
                    onRemove={() => uploads.remove(u.localId)}
                  />
                </li>
              ))}
            </ul>
          )}

          <div className="flex items-end gap-2">
          {room?.capabilities.canUpload && (
            <>
              <input
                ref={fileInputRef}
                type="file"
                multiple
                className="sr-only"
                aria-hidden="true"
                tabIndex={-1}
                onChange={(e) => {
                  acceptFiles(e.target.files);
                  // Reset so choosing the same file twice still fires a change event.
                  e.target.value = "";
                }}
              />
              <button
                type="button"
                onClick={() => fileInputRef.current?.click()}
                aria-label="Attach files"
                title="Attach files"
                className="inline-flex h-10 w-10 shrink-0 items-center justify-center rounded-md border border-border text-muted hover:bg-elevated hover:text-text"
              >
                <Paperclip size={16} aria-hidden />
              </button>
              {/* D-298 — a recording is a file like any other: same presign, same magic-byte check,
                  same scanner. Nothing is trusted because our own recorder produced it. */}
              <VoiceRecorder onRecorded={(file) => uploads.add([file])} />

              {/* D-298 video capture. `capture` is the native platform feature: on a phone browser it
                  opens the camera directly, on desktop the attribute is ignored and this is a video
                  file picker. A custom MediaRecorder camera UI would add a preview surface, a device
                  chooser and a permissions flow to win only the desktop case, where people attach an
                  existing file anyway. ponytail: native capture; build a recorder if desktop capture
                  is ever actually asked for. */}
              <input
                ref={videoInputRef}
                type="file"
                accept="video/*"
                capture="environment"
                className="sr-only"
                aria-hidden="true"
                tabIndex={-1}
                onChange={(e) => {
                  acceptFiles(e.target.files);
                  e.target.value = "";
                }}
              />
              <button
                type="button"
                onClick={() => videoInputRef.current?.click()}
                aria-label="Record a video"
                title="Record a video"
                className="inline-flex h-10 w-10 shrink-0 items-center justify-center rounded-md border border-border text-muted hover:bg-elevated hover:text-text"
              >
                <Video size={16} aria-hidden />
              </button>
            </>
          )}
          <label htmlFor="chat-composer" className="sr-only">
            Message
          </label>
          <textarea
            id="chat-composer"
            value={draft}
            onChange={(e) => {
              setDraft(e.target.value);
              notifyTyping(e.target.value);
            }}
            // Enter sends, Shift+Enter newlines — the desktop convention. Mirrors the server's
            // 1000-char ceiling so the refusal is visible before a round trip.
            onKeyDown={(e) => {
              if (e.key === "Enter" && !e.shiftKey) {
                e.preventDefault();
                void submit();
              }
            }}
            onPaste={onPaste}
            rows={1}
            maxLength={1000}
            placeholder="Type a message…  (Enter to send, Shift+Enter for a new line)"
            className="max-h-40 min-h-[2.5rem] flex-1 resize-y rounded-md border border-border bg-surface px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-accent"
          />
          <button
            type="submit"
            disabled={!canSend(draft, uploads.uploads)}
            aria-label="Send message"
            className="inline-flex h-10 w-10 shrink-0 items-center justify-center rounded-md bg-accent text-on-accent disabled:opacity-50"
          >
            <Send size={16} aria-hidden />
          </button>
          </div>
        </form>
      ) : (
        <p className="border-t border-border px-5 py-4 text-center text-sm text-muted">
          {blockedReason ?? "You cannot post in this chat."}
        </p>
      )}

      {actionsFor && room && (
        <MessageActions
          message={actionsFor}
          room={room}
          isMine={currentUserId != null && actionsFor.senderId === currentUserId}
          onClose={() => setActionsFor(undefined)}
          onDone={() => {
            setActionsFor(undefined);
            void refresh();
          }}
          onReply={setReplyingTo}
        />
      )}
    </section>
  );
}


/// D-292 — Accept / Decline for a message request, shown inside the conversation.
///
/// Accept is not strictly required to talk (a reply accepts on its own), but Decline is reachable
/// nowhere else once the reader has opened the room, and a request that cannot be refused is not a
/// request. `onAnswered` re-reads the room so the banner and the capability flags update together.
/// D-296 — "until Fri 14:00" for a pin inside the week, a date beyond it. Absolute rather than "in 3
/// days" so a host reading it twice a day is not told two different things about the same instant.
function formatPinExpiry(iso: string): string {
  const at = new Date(iso);
  const withinWeek = at.getTime() - Date.now() < 7 * 24 * 60 * 60 * 1000;
  return at.toLocaleString(undefined, {
    weekday: withinWeek ? "short" : undefined,
    day: withinWeek ? undefined : "numeric",
    month: withinWeek ? undefined : "short",
    hour: "numeric",
    minute: "2-digit"
  });
}

function DmRequestBanner({ roomId, onAnswered }: { roomId: string; onAnswered: () => void }) {
  const [pending, startTransition] = useTransition();
  const [error, setError] = useState<string | undefined>();

  const answer = (accept: boolean) =>
    startTransition(async () => {
      setError(undefined);
      // ActionResult is a union, narrowed on the error arm — the same shape messages-tabs uses.
      const result = await respondToDmRequestAction(roomId, accept);
      if ("error" in result) setError(result.error);
      else onAnswered();
    });

  return (
    <div className="border-t border-border bg-surface px-5 py-3">
      <p className="text-sm text-text">
        This is a message request. Replying accepts it.
      </p>
      <div className="mt-2 flex items-center gap-2">
        <button
          type="button"
          disabled={pending}
          onClick={() => answer(true)}
          className="rounded-md bg-accent px-3 py-1.5 text-sm text-on-accent disabled:opacity-50"
        >
          Accept
        </button>
        <button
          type="button"
          disabled={pending}
          onClick={() => answer(false)}
          className="rounded-md border border-border px-3 py-1.5 text-sm text-text disabled:opacity-50"
        >
          Decline
        </button>
      </div>
      {error && (
        <p role="alert" className="mt-2 text-sm text-danger">
          {error}
        </p>
      )}
    </div>
  );
}
