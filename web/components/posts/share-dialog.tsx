"use client";

import { useState } from "react";
import { sharePostAction } from "@/lib/posts-actions";
import { POST_BODY_MAX, type Post } from "@/lib/posts-api";

/// Reshare with an optional comment. The dialog never nests a share inside a share — if the post
/// being shared is itself a share, the backend reattaches to the original (`cannot_share_a_share`),
/// so what the user previews here is what they get.
export function ShareDialog({
  post,
  onClose,
  onShared
}: {
  post: Post;
  onClose: () => void;
  onShared: (created: Post) => void;
}) {
  const [body, setBody] = useState("");
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function submit() {
    setPending(true);
    setError(null);
    const result = await sharePostAction(post.id, body);
    setPending(false);
    if ("error" in result) setError(result.error);
    else onShared(result);
  }

  return (
    <div
      className="fixed inset-0 z-50 grid place-items-center bg-black/50 p-4"
      role="dialog"
      aria-modal="true"
      aria-label="Share post"
      onClick={onClose}
    >
      <div
        className="w-full max-w-lg rounded-lg border border-border bg-surface p-4"
        onClick={(e) => e.stopPropagation()}
      >
        <h2 className="text-sm font-semibold text-text">Share this post</h2>

        <textarea
          value={body}
          onChange={(e) => setBody(e.target.value)}
          rows={3}
          maxLength={POST_BODY_MAX}
          placeholder="Add a comment (optional)"
          className="mt-3 w-full rounded-md border border-border-strong bg-background px-3 py-2 text-sm text-text"
        />

        <div className="mt-2 rounded-md border border-border bg-background p-3">
          <p className="text-xs font-medium text-text">{post.author.name}</p>
          <p className="mt-1 line-clamp-3 whitespace-pre-wrap text-xs text-muted">{post.body}</p>
        </div>

        {error ? <p className="mt-2 text-xs text-danger">{error}</p> : null}

        <div className="mt-3 flex justify-end gap-2">
          <button
            type="button"
            onClick={onClose}
            className="rounded-md px-3 py-1.5 text-sm text-muted hover:bg-elevated hover:text-text"
          >
            Cancel
          </button>
          <button
            type="button"
            onClick={submit}
            disabled={pending}
            className="rounded-md bg-accent px-3 py-1.5 text-sm font-medium text-on-accent disabled:opacity-50"
          >
            {pending ? "Sharing…" : "Share"}
          </button>
        </div>
      </div>
    </div>
  );
}
