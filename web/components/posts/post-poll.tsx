"use client";

import { useState } from "react";
import { votePollAction } from "@/lib/posts-actions";
import type { PostPoll as Poll } from "@/lib/posts-api";

/// A poll on a post. Results are revealed once the viewer has voted or the poll has closed —
/// showing them beforehand would bias the vote, which is the whole reason the backend returns
/// `voted_by_me` per option rather than letting the client decide.
export function PostPoll({ postId, poll }: { postId: string; poll: Poll }) {
  const [current, setCurrent] = useState(poll);
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const hasVoted = current.options.some((o) => o.voted_by_me);
  const showResults = hasVoted || current.is_closed;
  const canVote = !current.is_closed && (!hasVoted || current.allow_multiple);

  async function vote(optionId: string) {
    if (!canVote || pending) return;
    setPending(true);
    setError(null);
    const selected = current.allow_multiple
      ? [...current.options.filter((o) => o.voted_by_me).map((o) => o.id), optionId]
      : [optionId];
    const result = await votePollAction(postId, selected);
    setPending(false);
    if ("error" in result) setError(result.error);
    else setCurrent(result);
  }

  return (
    <div className="mt-3 rounded-md border border-border bg-background p-3">
      <p className="text-sm font-medium text-text">{current.question}</p>
      {current.allow_multiple ? (
        <p className="mt-0.5 text-xs text-muted">Select as many as you like</p>
      ) : null}

      <ul className="mt-3 space-y-2">
        {current.options.map((o) => {
          const pct = current.total_votes > 0 ? Math.round((o.vote_count / current.total_votes) * 100) : 0;
          return (
            <li key={o.id}>
              <button
                type="button"
                onClick={() => vote(o.id)}
                disabled={!canVote || pending}
                aria-pressed={o.voted_by_me}
                className={`relative w-full overflow-hidden rounded-md border px-3 py-2 text-left text-sm transition
                  ${o.voted_by_me ? "border-accent text-text" : "border-border text-muted"}
                  ${canVote ? "hover:border-accent hover:text-text" : "cursor-default"}`}
              >
                {showResults ? (
                  <span
                    aria-hidden
                    className="absolute inset-y-0 left-0 bg-accent/15"
                    style={{ width: `${pct}%` }}
                  />
                ) : null}
                <span className="relative flex items-center justify-between gap-3">
                  <span className="truncate">
                    {o.voted_by_me ? "✓ " : ""}
                    {o.text}
                  </span>
                  {showResults ? <span className="shrink-0 tabular-nums text-xs text-muted">{pct}%</span> : null}
                </span>
              </button>
            </li>
          );
        })}
      </ul>

      <p className="mt-2 text-xs text-muted">
        {current.total_votes} {current.total_votes === 1 ? "vote" : "votes"}
        {current.is_closed ? " · closed" : current.closes_at ? ` · closes ${new Date(current.closes_at).toLocaleDateString()}` : ""}
      </p>
      {error ? <p className="mt-1 text-xs text-danger">{error}</p> : null}
    </div>
  );
}
