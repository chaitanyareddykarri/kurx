"use client";

import { AlertCircle, Check, CheckCheck, Clock, Pin } from "lucide-react";
import { memo } from "react";
import { AttachmentView } from "@/components/chat/attachment-view";
import { PresenceAvatar } from "@/components/chat/presence";
import type { LocalChatMessage } from "@/lib/chat-merge";

/// The one genuinely new chat component. Everything else on these screens reuses the design system
/// (Avatar, Button, Card, Badge, EmptyState, ErrorState, ListSkeleton) — a message bubble has no
/// equivalent in `@kurx/ui` or `components/ui`, and bending Card into one would be worse.

// An explicit locale, like every other formatted value in the app. `undefined` resolves to the
// server's locale on the server and the browser's on the client, which is a hydration mismatch
// waiting for a user whose two disagree.
function timeLabel(iso: string) {
  return new Date(iso).toLocaleTimeString("en-IN", { hour: "2-digit", minute: "2-digit" });
}

function MessageBubbleImpl({
  message,
  isMine,
  showSender,
  senderOnline = false,
  presenceKnown = false,
  read = false,
  delivered = false,
  onRetry,
  onDiscard,
  onOpenActions,
  onReact
}: {
  message: LocalChatMessage;
  isMine: boolean;
  /// False when the previous message came from the same sender — avatar and name are drawn once per
  /// run rather than on every line.
  showSender: boolean;
  /// Presence arrives as two plain booleans rather than the presence state itself: that is what lets
  /// the memo below skip every bubble a given presence event does not actually change.
  senderOnline?: boolean;
  presenceKnown?: boolean;
  /// True once some other member's read pointer has reached this message (D-114). Absent means
  /// "not read" — a bubble rendered without presence context is a valid state, not an error.
  read?: boolean;
  /// D-295 — true once some other member's DEVICE acknowledged receiving it. Strictly weaker than
  /// `read`: delivered says it arrived, read says someone looked.
  delivered?: boolean;
  onRetry?: () => void;
  onDiscard?: () => void;
  onOpenActions?: () => void;
  /// D-295 — toggles one emoji. Must be a stable reference from the parent, or the memo below stops
  /// skipping bubbles that a given re-render does not actually change.
  onReact?: (emoji: string) => void;
}) {
  if (message.kind === "System") {
    return (
      <div className="my-3 flex justify-center" role="status">
        <span className="rounded-full bg-elevated px-3 py-1 text-xs text-muted">{message.body}</span>
      </div>
    );
  }

  if (message.isDeleted) {
    return (
      <div className={`mb-2 flex ${isMine ? "justify-end" : "justify-start"}`}>
        <span className="rounded-lg border border-border px-3 py-2 text-sm italic text-muted">
          Message deleted
        </span>
      </div>
    );
  }

  const failed = message.status === "failed";
  const pending = message.status === "pending";

  return (
    <div className={`group mb-2 flex gap-2 ${isMine ? "justify-end" : "justify-start"}`}>
      {!isMine && (
        <div className="w-8 shrink-0">
          {showSender && (
            <PresenceAvatar
              name={message.senderName ?? "?"}
              size={32}
              online={senderOnline}
              presenceKnown={presenceKnown}
            />
          )}
        </div>
      )}

      <div className={`flex max-w-[min(42rem,75%)] flex-col ${isMine ? "items-end" : "items-start"}`}>
        {showSender && !isMine && (
          <span className="mb-0.5 flex items-center gap-1.5 px-1 text-xs font-semibold text-muted">
            {message.senderName ?? "Unknown"}
            {/* D-301 — Host and Moderator are different authorities, so they read differently. The label
                is the badge: a colour-only distinction would say nothing to a screen reader and nothing
                to anyone who cannot separate the two hues. */}
            {message.senderRole === "Host" && (
              <span className="text-[10px] font-bold uppercase tracking-wide text-accent-text">Host</span>
            )}
            {message.senderRole === "Moderator" && (
              <span className="text-[10px] font-bold uppercase tracking-wide text-muted">Mod</span>
            )}
          </span>
        )}

        <div
          // Right-click opens the same actions as the keyboard/menu path, so a desktop user gets
          // the affordance they expect without a long-press.
          onContextMenu={(e) => {
            if (!onOpenActions) return;
            e.preventDefault();
            onOpenActions();
          }}
          className={[
            "rounded-2xl px-3 py-2 text-sm leading-relaxed",
            isMine ? "bg-accent text-on-accent" : "bg-surface text-text",
            isMine ? "rounded-br-sm" : "rounded-bl-sm",
            failed ? "ring-1 ring-danger" : ""
          ].join(" ")}
        >
          {/* Files first, then any caption: the attachment is usually the point of the message,
              and the text reads as a caption beneath it. */}
          {message.attachments.map((a) => (
            <AttachmentView key={a.id} attachment={a} />
          ))}
          {message.body && <p className="whitespace-pre-wrap break-words">{message.body}</p>}

          {/* D-295 — the link card. `host` is server-derived and is the one part a sender cannot
              fake, so it is rendered prominently: the title may misdescribe the page, the host
              always tells the reader where the tap actually goes. */}
          {!message.isDeleted && message.linkPreview && (
            <a
              href={message.linkPreview.url}
              target="_blank"
              rel="noopener noreferrer nofollow"
              className="mt-1.5 block overflow-hidden rounded-md border border-border bg-bg/40"
            >
              {message.linkPreview.imageUrl && (
                // eslint-disable-next-line @next/next/no-img-element -- remote, arbitrary host: next/image would need every sender's domain allow-listed.
                <img
                  src={message.linkPreview.imageUrl}
                  alt="" aria-hidden
                  className="h-28 w-full object-cover"
                  loading="lazy"
                />
              )}
              <span className="block px-2.5 py-2">
                <span className="block text-[11px] uppercase tracking-wide text-muted">
                  {message.linkPreview.host}
                </span>
                {message.linkPreview.title && (
                  <span className="mt-0.5 block truncate text-sm font-medium">
                    {message.linkPreview.title}
                  </span>
                )}
                {message.linkPreview.description && (
                  <span className="mt-0.5 line-clamp-2 block text-xs text-muted">
                    {message.linkPreview.description}
                  </span>
                )}
              </span>
            </a>
          )}

          {/* Reactions (D-295). Aggregated per emoji by the server; `mine` drives the toggled ring so
              the state survives greyscale rather than relying on colour alone. */}
          {!message.isDeleted && message.reactions && message.reactions.length > 0 && (
            <span className="mt-1.5 flex flex-wrap gap-1">
              {message.reactions.map((r) => (
                <button
                  key={r.emoji}
                  type="button"
                  onClick={() => onReact?.(r.emoji)}
                  aria-label={`${r.emoji} ${r.count}${r.mine ? ", you reacted" : ""}`}
                  aria-pressed={r.mine}
                  className={`inline-flex items-center gap-1 rounded-pill border px-2 py-0.5 text-xs ${
                    r.mine ? "border-accent bg-accent/10" : "border-border bg-bg/40"
                  }`}
                >
                  <span aria-hidden>{r.emoji}</span>
                  <span>{r.count}</span>
                </button>
              ))}
            </span>
          )}

          <span
            className={`mt-1 flex items-center gap-1 text-[10px] ${isMine ? "text-on-accent/70" : "text-muted"}`}
          >
            {message.isPinned && <Pin size={10} role="img" aria-label="Pinned" />}
            {/* Sent time, never the edit time (D-293): the message did not move, so showing the edit
                time here would misdate it in the conversation. The marker below says it changed. */}
            <time dateTime={message.createdAt}>{timeLabel(message.createdAt)}</time>
            {message.editedAt && <span title={`Edited ${timeLabel(message.editedAt)}`}>· edited</span>}
            {isMine && pending && <Clock size={10} role="img" aria-label="Sending" />}
            {isMine && failed && <AlertCircle size={10} role="img" className="text-danger" aria-label="Not sent" />}
            {/* Three states, and the glyph changes with each — not just the colour. The state has to
                survive greyscale and colour-blindness, so sent is one tick and both delivered and
                read are two, separated by weight and by the label a screen reader announces.
                `role="img"` is load-bearing: `aria-label` on an <svg> without it is not exposed by
                several screen readers, so every delivery state here was silent to them. */}
            {isMine && !pending && !failed && read && (
              <CheckCheck size={10} role="img" className="text-info" aria-label="Read" />
            )}
            {isMine && !pending && !failed && !read && delivered && (
              <CheckCheck size={10} role="img" aria-label="Delivered" />
            )}
            {isMine && !pending && !failed && !read && !delivered && (
              <Check size={10} role="img" aria-label="Sent" />
            )}
          </span>
        </div>

        {failed && (
          <span className="mt-0.5 flex items-center gap-2 text-xs">
            <span className="text-danger">Not sent</span>
            <button
              type="button"
              onClick={onRetry}
              className="inline-flex min-h-11 items-center rounded-md px-2 font-semibold text-accent-text underline-offset-2 hover:underline focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
            >
              Retry
            </button>
            <button
              type="button"
              onClick={onDiscard}
              className="inline-flex min-h-11 items-center rounded-md px-2 text-muted underline-offset-2 hover:underline focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
            >
              Discard
            </button>
          </span>
        )}
      </div>
    </div>
  );
}

/// Memoised so a presence or typing update — which re-renders the room — repaints only the bubbles
/// whose own props actually changed, rather than the whole list. Every prop is a primitive or a
/// stable reference, so the default shallow comparison is enough.
export const MessageBubble = memo(MessageBubbleImpl);
