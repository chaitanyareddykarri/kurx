"use client";

import { useState } from "react";
import Link from "next/link";
import { Heart, Trash2 } from "lucide-react";
import { Avatar } from "@/components/ui/avatar";
import { PostBody } from "@/components/posts/post-body";
import { formatRelativeTime } from "@/lib/formatters";
import {
  addCommentAction, deleteCommentAction, toggleCommentLikeAction, loadCommentsAction
} from "@/lib/posts-actions";
import { COMMENT_BODY_MAX, type PostComment, type PostCommentPage } from "@/lib/posts-api";

/// Comments, one level of reply deep. A reply to a reply attaches to the same parent — the backend
/// enforces that, and flattening here keeps the thread readable instead of stair-stepping off-screen.
export function CommentThread({
  postId,
  initial,
  viewer,
  onCountChange
}: {
  postId: string;
  initial: PostCommentPage;
  viewer: { name: string; avatarKey?: string | null };
  onCountChange?: (delta: number) => void;
}) {
  const [items, setItems] = useState<PostComment[]>(initial.items);
  const [cursor, setCursor] = useState<string | null>(initial.next_cursor ?? null);
  const [replyTo, setReplyTo] = useState<PostComment | null>(null);
  const [body, setBody] = useState("");
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const roots = items.filter((c) => !c.parent_comment_id);
  const repliesOf = (id: string) => items.filter((c) => c.parent_comment_id === id);

  async function submit() {
    if (!body.trim() || pending) return;
    setPending(true);
    setError(null);
    const result = await addCommentAction(postId, body, replyTo?.parent_comment_id ?? replyTo?.id);
    setPending(false);
    if ("error" in result) {
      setError(result.error);
      return;
    }
    setItems((cur) => [...cur, result]);
    setBody("");
    setReplyTo(null);
    onCountChange?.(1);
  }

  async function remove(comment: PostComment) {
    const snapshot = items;
    // A root going away takes its replies with it, matching the server's cascade.
    setItems((cur) => cur.filter((c) => c.id !== comment.id && c.parent_comment_id !== comment.id));
    onCountChange?.(-1);
    try {
      await deleteCommentAction(comment.id, postId);
    } catch {
      setItems(snapshot);
      onCountChange?.(1);
    }
  }

  async function like(comment: PostComment) {
    const result = await toggleCommentLikeAction(comment.id, comment.liked_by_me);
    setItems((cur) =>
      cur.map((c) => (c.id === comment.id ? { ...c, liked_by_me: result.liked, like_count: result.like_count } : c))
    );
  }

  async function loadMore() {
    if (!cursor) return;
    const page = await loadCommentsAction(postId, cursor);
    const seen = new Set(items.map((c) => c.id));
    setItems((cur) => [...cur, ...page.items.filter((c) => !seen.has(c.id))]);
    setCursor(page.next_cursor ?? null);
  }

  return (
    <section className="mt-4 rounded-lg border border-border bg-surface p-4">
      <h2 className="text-sm font-semibold text-text">Comments</h2>

      <div className="mt-3 flex gap-2">
        <Avatar name={viewer.name} src={viewer.avatarKey ?? undefined} size={32} />
        <div className="min-w-0 flex-1">
          {replyTo ? (
            <p className="mb-1 text-xs text-muted">
              Replying to {replyTo.author.name}{" "}
              <button type="button" className="text-accent hover:underline" onClick={() => setReplyTo(null)}>
                cancel
              </button>
            </p>
          ) : null}
          <label htmlFor="comment-body" className="sr-only">
            Add a comment
          </label>
          <textarea
            id="comment-body"
            value={body}
            onChange={(e) => setBody(e.target.value)}
            rows={2}
            maxLength={COMMENT_BODY_MAX}
            placeholder="Add a comment…"
            className="w-full rounded-md border border-border-strong bg-background px-3 py-2 text-sm text-text"
          />
          <div className="mt-1.5 flex items-center justify-end gap-2">
            <span className="text-xs tabular-nums text-muted">
              {body.length}/{COMMENT_BODY_MAX}
            </span>
            <button
              type="button"
              onClick={submit}
              disabled={pending || !body.trim()}
              className="rounded-md bg-accent px-3 py-1.5 text-sm font-medium text-on-accent disabled:opacity-50"
            >
              {pending ? "Posting…" : "Comment"}
            </button>
          </div>
          {error ? <p className="text-xs text-danger">{error}</p> : null}
        </div>
      </div>

      <ul className="mt-4 space-y-4">
        {roots.map((c) => (
          <li key={c.id}>
            <Comment comment={c} onLike={like} onDelete={remove} onReply={setReplyTo} />
            {repliesOf(c.id).length > 0 ? (
              <ul className="mt-3 space-y-3 border-l border-border pl-4">
                {repliesOf(c.id).map((r) => (
                  <li key={r.id}>
                    <Comment comment={r} onLike={like} onDelete={remove} onReply={setReplyTo} />
                  </li>
                ))}
              </ul>
            ) : null}
          </li>
        ))}
      </ul>

      {roots.length === 0 ? <p className="mt-4 text-sm text-muted">No comments yet.</p> : null}

      {cursor ? (
        <button
          type="button"
          onClick={loadMore}
          className="mt-3 w-full rounded-md border border-border py-1.5 text-xs text-muted hover:bg-elevated hover:text-text"
        >
          Load more comments
        </button>
      ) : null}
    </section>
  );
}

function Comment({
  comment,
  onLike,
  onDelete,
  onReply
}: {
  comment: PostComment;
  onLike: (c: PostComment) => void;
  onDelete: (c: PostComment) => void;
  onReply: (c: PostComment) => void;
}) {
  const handle = comment.author.username;
  return (
    <div className="flex gap-2">
      <Avatar name={comment.author.name} src={comment.author.avatar_key ?? undefined} size={32} />
      <div className="min-w-0 flex-1">
        <div className="rounded-md bg-background px-3 py-2">
          <Link
            href={handle ? `/u/${handle}` : "#"}
            className="text-xs font-semibold text-text hover:underline"
          >
            {comment.author.name}
          </Link>
          <PostBody body={comment.body} className="mt-0.5 whitespace-pre-wrap break-words text-sm text-text" />
        </div>
        <div className="mt-1 flex items-center gap-3 px-1 text-xs text-muted">
          <span>{formatRelativeTime(comment.created_at)}</span>
          <button
            type="button"
            aria-label={comment.liked_by_me ? "Unlike comment" : "Like comment"}
            onClick={() => onLike(comment)}
            className={`flex items-center gap-1 hover:text-text ${comment.liked_by_me ? "text-accent" : ""}`}
          >
            <Heart size={12} className={comment.liked_by_me ? "fill-accent" : ""} />
            {comment.like_count > 0 ? comment.like_count : null}
          </button>
          <button type="button" onClick={() => onReply(comment)} className="hover:text-text">
            Reply
          </button>
          {comment.can_delete ? (
            <button
              type="button"
              aria-label="Delete comment"
              onClick={() => onDelete(comment)}
              className="flex items-center gap-1 hover:text-danger"
            >
              <Trash2 size={12} />
            </button>
          ) : null}
        </div>
      </div>
    </div>
  );
}
