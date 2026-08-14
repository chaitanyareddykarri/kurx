"use client";

import { useState } from "react";
import { updatePostAction } from "@/lib/posts-actions";
import {
  POST_BODY_MAX, POST_VISIBILITIES, VISIBILITY_LABEL,
  type Post, type PostVisibility
} from "@/lib/posts-api";

/// Edit a post's body and audience — the only two things the contract lets `PATCH` change. Media,
/// poll, event attachment and kind are immutable after creation, so nothing else is offered.
/// Saving stamps `edited_at`, which every card renders: an edit is visible, never silent.
export function EditPostDialog({
  post,
  onClose,
  onSaved
}: {
  post: Post;
  onClose: () => void;
  onSaved: (post: Post) => void;
}) {
  const [body, setBody] = useState(post.body);
  const [visibility, setVisibility] = useState(post.visibility as PostVisibility);
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const dirty = body.trim() !== post.body.trim() || visibility !== post.visibility;

  async function save() {
    setPending(true);
    setError(null);
    const result = await updatePostAction(post.id, { body: body.trim(), visibility });
    setPending(false);
    if ("error" in result) setError(result.error);
    else onSaved(result);
  }

  return (
    <div
      className="fixed inset-0 z-50 grid place-items-center bg-black/50 p-4"
      role="dialog"
      aria-modal="true"
      aria-label="Edit post"
      onClick={onClose}
    >
      <div
        className="w-full max-w-lg rounded-lg border border-border bg-surface p-4"
        onClick={(e) => e.stopPropagation()}
      >
        <h2 className="text-sm font-semibold text-text">Edit post</h2>

        <label htmlFor="edit-body" className="sr-only">
          Post text
        </label>
        <textarea
          id="edit-body"
          value={body}
          onChange={(e) => setBody(e.target.value)}
          rows={6}
          maxLength={POST_BODY_MAX}
          className="mt-3 w-full rounded-md border border-border-strong bg-background px-3 py-2 text-sm text-text"
        />

        <label htmlFor="edit-visibility" className="mt-3 block text-xs text-muted">
          Who can see this
        </label>
        <select
          id="edit-visibility"
          value={visibility}
          onChange={(e) => setVisibility(e.target.value as PostVisibility)}
          className="mt-1 w-full rounded-md border border-border-strong bg-background px-2 py-1.5 text-sm text-text"
        >
          {POST_VISIBILITIES.filter((v) => v !== "event_participants" || post.event).map((v) => (
            <option key={v} value={v}>
              {VISIBILITY_LABEL[v]}
            </option>
          ))}
        </select>

        {post.media.length > 0 || post.poll ? (
          <p className="mt-2 text-xs text-muted">
            {post.poll ? "The poll" : "Attachments"} cannot be changed after posting.
          </p>
        ) : null}

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
            onClick={save}
            disabled={!dirty || pending}
            className="rounded-md bg-accent px-3 py-1.5 text-sm font-medium text-on-accent disabled:opacity-50"
          >
            {pending ? "Saving…" : "Save changes"}
          </button>
        </div>
      </div>
    </div>
  );
}
