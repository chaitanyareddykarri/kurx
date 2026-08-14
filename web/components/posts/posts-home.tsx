"use client";

import { useState } from "react";
import Link from "next/link";
import { PostComposer } from "@/components/posts/post-composer";
import { PostFeed } from "@/components/posts/post-feed";
import type { Post, PostPage, TrendingHashtag } from "@/lib/posts-api";

/// Feed + composer. The composer's result is prepended into the same list the feed owns, so a new
/// post appears without a round-trip — `key` on the feed forces it to re-seed from the updated page.
export function PostsHome({
  author,
  initial,
  trending,
  query
}: {
  author: { name: string; avatarKey?: string | null };
  initial: PostPage;
  trending: TrendingHashtag[];
  /// Present when the page was opened as a search (`/posts?q=…`). The results replace the feed rather
  /// than sitting beside it — two stacked lists of posts with different rules is not readable.
  query?: string;
}) {
  const [page, setPage] = useState(initial);
  const [seed, setSeed] = useState(0);
  const searching = Boolean(query);

  function onCreated(post: Post) {
    setPage((cur) => ({ ...cur, items: [post, ...cur.items.filter((p) => p.id !== post.id)] }));
    setSeed((s) => s + 1);
  }

  return (
    <div className="mx-auto grid max-w-5xl gap-6 lg:grid-cols-[1fr_16rem]">
      <div className="min-w-0">
        <h1 className="mb-4 text-2xl font-semibold text-text">Posts</h1>

        {/* A plain GET form: the URL carries the query, so a search survives reload, back, and sharing,
            and the first page of results is server-rendered like every other listing here. */}
        <form method="GET" action="/posts" role="search" className="mb-4 flex gap-2">
          <input
            type="search"
            name="q"
            defaultValue={query ?? ""}
            placeholder="Search posts"
            aria-label="Search posts"
            className="min-h-11 flex-1 rounded-md border border-border bg-surface px-3 text-sm text-text placeholder:text-muted"
          />
          <button
            type="submit"
            className="min-h-11 rounded-md border border-border px-4 text-sm text-text hover:bg-elevated"
          >
            Search
          </button>
          {searching ? (
            <Link
              href="/posts"
              className="inline-flex min-h-11 items-center rounded-md px-3 text-sm text-accent hover:underline"
            >
              Clear
            </Link>
          ) : null}
        </form>

        {searching ? null : <PostComposer author={author} onCreated={onCreated} />}
        <div className="mt-4">
          <PostFeed
            key={`${searching ? `q:${query}` : "feed"}:${seed}`}
            source={searching ? { kind: "search", q: query! } : { kind: "feed" }}
            initial={page}
            emptyTitle={searching ? "No posts match that search" : "Your feed is quiet"}
            emptyMessage={
              searching
                ? "Try a different word, or browse a hashtag from the Trending list."
                : "Connect with people and follow organizations to see their posts here."
            }
          />
        </div>
      </div>

      <aside className="hidden lg:block">
        <div className="sticky top-20 space-y-4">
        <div className="rounded-lg border border-border bg-surface p-4">
          <h2 className="text-sm font-semibold text-text">Yours</h2>
          <ul className="mt-2 space-y-1.5">
            <li>
              <Link href="/posts/mine" className="block text-sm text-accent hover:underline">
                My posts
              </Link>
            </li>
            <li>
              <Link href="/posts/saved" className="block text-sm text-accent hover:underline">
                Saved posts
              </Link>
            </li>
          </ul>
        </div>
        <div className="rounded-lg border border-border bg-surface p-4">
          <h2 className="text-sm font-semibold text-text">Trending</h2>
          {trending.length === 0 ? (
            <p className="mt-2 text-xs text-muted">Nothing trending yet.</p>
          ) : (
            <ul className="mt-2 space-y-1.5">
              {trending.map((t) => (
                <li key={t.tag}>
                  <Link href={`/posts/tag/${t.tag}`} className="block text-sm text-accent hover:underline">
                    #{t.tag}
                    <span className="ml-1.5 text-xs text-muted">{t.post_count}</span>
                  </Link>
                </li>
              ))}
            </ul>
          )}
        </div>
        </div>
      </aside>
    </div>
  );
}
