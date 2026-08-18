"use client";

import Link from "next/link";
import { useTransition } from "react";
import { Bookmark, Heart, MessageCircle, Repeat2 } from "lucide-react";
import { PostActionsMenu } from "@/components/posts/post-actions-menu";
import { Avatar, Badge } from "@kurx/ui";
import { PostBody } from "@/components/posts/post-body";
import { PostPoll } from "@/components/posts/post-poll";
import { PostMediaGrid } from "@/components/posts/post-media-grid";
import { formatDate, formatRelativeTime } from "@/lib/formatters";
import { VISIBILITY_LABEL, type Post, type PostVisibility, type SharedPost } from "@/lib/posts-api";

/// One post in a feed. Like/save are optimistic and then reconciled with the server's own count —
/// the backend mutates those counters in SQL (D-240), so a locally incremented number is a guess,
/// not the truth. On failure the previous state is restored rather than left wrong.
export function PostCard({
  post,
  onLike,
  onSave,
  onDelete,
  onShare,
  onUpdated,
  compact = false
}: {
  post: Post;
  onLike: (post: Post) => Promise<void>;
  onSave: (post: Post) => Promise<void>;
  onDelete?: (post: Post) => Promise<void>;
  onShare?: (post: Post) => void;
  onUpdated?: (post: Post) => void;
  compact?: boolean;
}) {
  const [pending, startTransition] = useTransition();

  return (
    <article className="rounded-lg border border-border bg-surface p-4">
      <Header post={post} />

      <div className="mt-3">
        <PostBody body={post.body} />
      </div>

      {post.media.length > 0 ? <PostMediaGrid media={post.media} /> : null}
      {post.poll ? <PostPoll postId={post.id} poll={post.poll} /> : null}
      {post.event ? <EventAttachment event={post.event} /> : null}
      {post.shared_post ? <SharedPostCard post={post.shared_post} /> : null}

      <div className="mt-3 flex flex-wrap items-center gap-0.5 border-t border-border pt-2 lg:gap-1">
        <Action
          label={post.liked_by_me ? "Unlike" : "Like"}
          icon={<Heart size={16} aria-hidden className={post.liked_by_me ? "fill-accent-text text-accent-text" : ""} />}
          count={post.like_count}
          active={post.liked_by_me}
          pressed={post.liked_by_me}
          disabled={pending}
          onClick={() => startTransition(() => void onLike(post))}
        />
        <Action
          label="Comments"
          icon={<MessageCircle size={16} aria-hidden />}
          count={post.comment_count}
          href={`/posts/${post.id}`}
        />
        <Action
          label="Share"
          icon={<Repeat2 size={16} aria-hidden />}
          count={post.share_count}
          disabled={!onShare}
          onClick={() => onShare?.(post)}
        />
        <span className="flex-1" />
        <Action
          label={post.saved_by_me ? "Unsave" : "Save"}
          icon={<Bookmark size={16} aria-hidden className={post.saved_by_me ? "fill-accent-text text-accent-text" : ""} />}
          active={post.saved_by_me}
          pressed={post.saved_by_me}
          disabled={pending}
          onClick={() => startTransition(() => void onSave(post))}
        />
        <PostActionsMenu post={post} onDelete={onDelete} onUpdated={onUpdated} />
      </div>

      {/* The visibility was announced twice: once here in `sr-only` text and again in the header
          line above, which states it visibly. One is enough. */}
    </article>
  );
}

function Header({ post }: { post: Post | SharedPost }) {
  const handle = post.author.username;
  /*
   * An author with no username has no public profile to open. Both the avatar and the name rendered
   * `href="#"` — two focusable, link-announced controls per post that navigate nowhere, on the
   * most-rendered component in the product (the REG-004 pattern). They are plain content now.
   */
  const avatar = <Avatar name={post.author.name} src={post.author.avatar_url ?? undefined} size={40} />;
  return (
    <div className="flex items-start gap-3">
      {handle ? (
        <Link
          href={`/u/${handle}`}
          aria-label={post.author.name}
          className="rounded-full focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
        >
          {avatar}
        </Link>
      ) : (
        avatar
      )}
      <div className="min-w-0 flex-1">
        <div className="flex items-center gap-1.5">
          {handle ? (
            <Link
              href={`/u/${handle}`}
              className="truncate text-sm font-semibold text-text underline-offset-2 hover:underline focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
            >
              {post.author.name}
            </Link>
          ) : (
            <span className="truncate text-sm font-semibold text-text">{post.author.name}</span>
          )}
          {/* Was a literal "✓" in a <span> carrying `aria-label`. A span has no role, so the label
              is not exposed at all by several screen readers, and the glyph alone reads as "check
              mark" or is dropped. `text-accent` was also 2.80:1 on light. */}
          {post.author.is_verified ? <Badge tone="accent">Verified</Badge> : null}
        </div>
        <p className="truncate text-xs text-muted">
          {handle ? `@${handle} · ` : ""}
          {formatRelativeTime(post.created_at)}
          {post.edited_at ? " · edited" : ""}
          {" · "}
          {VISIBILITY_LABEL[post.visibility as PostVisibility] ?? post.visibility}
        </p>
      </div>
    </div>
  );
}

function EventAttachment({ event }: { event: NonNullable<Post["event"]> }) {
  return (
    <Link
      href={`/e/${event.slug}`}
      className="mt-3 flex gap-3 overflow-hidden rounded-md border border-border bg-background transition hover:border-accent"
    >
      {event.banner_url ? (
        // banner_URL, never banner_key — a storage key is not an image source (D-302).
        // eslint-disable-next-line @next/next/no-img-element -- provider-swappable storage host
        <img src={event.banner_url} alt={`Banner for ${event.title}`} className="h-20 w-28 shrink-0 object-cover" />
      ) : null}
      <div className="min-w-0 py-2 pr-3">
        <p className="truncate text-sm font-medium text-text">{event.title}</p>
        <p className="mt-0.5 truncate text-xs text-muted">
          {formatDate(event.starts_at, "en-IN", { day: "numeric", month: "short", year: "numeric" })}
          {event.city ? ` · ${event.city}` : ""}
        </p>
      </div>
    </Link>
  );
}

/// A shared post renders read-only and never recursively — the backend guarantees exactly one level
/// (`cannot_share_a_share`), so there is no nested share to handle here.
function SharedPostCard({ post }: { post: SharedPost }) {
  return (
    <div className="mt-3 rounded-md border border-border bg-background p-3">
      <Header post={post} />
      <div className="mt-2">
        <PostBody body={post.body} className="line-clamp-4 whitespace-pre-wrap break-words text-sm text-muted" />
      </div>
      {post.media.length > 0 ? <PostMediaGrid media={post.media} compact /> : null}
    </div>
  );
}

function Action({
  label,
  icon,
  count,
  active,
  pressed,
  disabled,
  onClick,
  href
}: {
  label: string;
  icon: React.ReactNode;
  count?: number;
  active?: boolean;
  /** Set on toggles only; a plain action button must not claim a pressed state. */
  pressed?: boolean;
  disabled?: boolean;
  onClick?: () => void;
  href?: string;
}) {
  const cls = `inline-flex min-h-11 items-center gap-1.5 rounded-md px-1.5 text-xs transition duration-fast lg:px-2.5
    focus-visible:outline focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-accent
    ${active ? "text-accent-text" : "text-muted"} ${disabled ? "opacity-50" : "hover:bg-elevated hover:text-text"}`;

  /*
   * The count goes into the accessible name.
   *
   * `aria-label` REPLACES an element's content, so `aria-label="Like"` around a visible "42" meant
   * the number was announced to nobody — every like, comment, share and save count on every post was
   * silent, while being the main thing those controls communicate.
   */
  const name = count !== undefined && count > 0 ? `${label}, ${count}` : label;

  const inner = (
    <>
      {icon}
      {count !== undefined && count > 0 ? <span aria-hidden className="tabular-nums">{count}</span> : null}
    </>
  );

  if (href) {
    return (
      <Link href={href} aria-label={name} className={cls}>
        {inner}
      </Link>
    );
  }
  return (
    <button
      type="button"
      aria-label={name}
      // Like and Save are toggles. Their state was carried by fill and hue alone, which is not a
      // state a screen reader can perceive — `aria-pressed` is what makes "already liked" audible.
      aria-pressed={pressed}
      className={cls}
      disabled={disabled}
      onClick={onClick}
    >
      {inner}
    </button>
  );
}
