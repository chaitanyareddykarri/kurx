"use client";

import { useCallback, useState } from "react";
import { Newspaper } from "lucide-react";
import { Button, EmptyState, Spinner, useToast } from "@kurx/ui";
import { apiErrorMessage } from "@/lib/api";
import { PostCard } from "@/components/posts/post-card";
import { ShareDialog } from "@/components/posts/share-dialog";
import { mergePostPage, applyLike, applySaved, removePost, prependPost } from "@/lib/posts-feed";
import {
  togglePostLikeAction, togglePostSavedAction, deletePostAction, loadFeedAction,
  loadMyPostsAction, loadSavedPostsAction, loadUserPostsAction, loadEventPostsAction,
  loadHashtagPostsAction, loadSearchPostsAction
} from "@/lib/posts-actions";
import type { Post, PostPage } from "@/lib/posts-api";

export type FeedSource =
  | { kind: "feed" }
  | { kind: "mine" }
  | { kind: "saved" }
  | { kind: "user"; username: string }
  | { kind: "event"; eventId: string }
  | { kind: "hashtag"; tag: string }
  | { kind: "search"; q: string };

function loaderFor(source: FeedSource): (cursor?: string) => Promise<PostPage> {
  switch (source.kind) {
    case "mine": return (c) => loadMyPostsAction(c);
    case "saved": return (c) => loadSavedPostsAction(c);
    case "user": return (c) => loadUserPostsAction(source.username, c);
    case "event": return (c) => loadEventPostsAction(source.eventId, c);
    case "hashtag": return (c) => loadHashtagPostsAction(source.tag, c);
    case "search": return (c) => loadSearchPostsAction(source.q, c);
    default: return (c) => loadFeedAction(c);
  }
}

/// Owns feed state so every post's like/save/delete lands in one place. The first page is rendered
/// on the server and handed in — only "load more" round-trips, so the feed is useful before any
/// client JS has run.
export function PostFeed({
  source,
  initial,
  emptyTitle = "Nothing here yet",
  emptyMessage = "Posts will show up here."
}: {
  source: FeedSource;
  initial: PostPage;
  emptyTitle?: string;
  emptyMessage?: string;
}) {
  const [items, setItems] = useState<Post[]>(initial.items);
  const [cursor, setCursor] = useState<string | null>(initial.next_cursor ?? null);
  const [loading, setLoading] = useState(false);
  const [sharing, setSharing] = useState<Post | null>(null);
  const [announcement, setAnnouncement] = useState("");
  const toast = useToast();

  const load = loaderFor(source);

  const loadMore = useCallback(async () => {
    if (!cursor || loading) return;
    setLoading(true);
    try {
      const page = await load(cursor);
      const merged = mergePostPage(items, page);
      const added = merged.items.length - items.length;
      setItems(merged.items);
      setCursor(merged.nextCursor);
      // Clicking "Load more" moved nothing a screen reader could perceive: the posts arrived below
      // the button and were never announced, so the control read as doing nothing.
      setAnnouncement(added > 0 ? `${added} more ${added === 1 ? "post" : "posts"} loaded.` : "No more posts.");
    } catch (err) {
      // A failed page silently re-enabled the button and left the feed as it was.
      toast(apiErrorMessage(err), "error");
    } finally {
      setLoading(false);
    }
  }, [cursor, loading, items, load, toast]);

  const onLike = useCallback(async (post: Post) => {
    // Optimistic flip, then overwrite with the server's authoritative count.
    setItems((cur) => applyLike(cur, post.id, { liked: !post.liked_by_me, like_count: post.like_count + (post.liked_by_me ? -1 : 1) }));
    try {
      const result = await togglePostLikeAction(post.id, post.liked_by_me);
      setItems((cur) => applyLike(cur, post.id, result));
    } catch (err) {
      // The revert was the entire failure report: the heart flipped back and nothing was said, so a
      // like that never landed was indistinguishable from a double-tap. This is the silent-revert
      // shape Phase 18 found on the account toggles, on the most-used control in the product.
      setItems((cur) => applyLike(cur, post.id, { liked: post.liked_by_me, like_count: post.like_count }));
      toast(apiErrorMessage(err), "error");
    }
  }, [toast]);

  const onSave = useCallback(async (post: Post) => {
    setItems((cur) => applySaved(cur, post.id, !post.saved_by_me));
    try {
      const saved = await togglePostSavedAction(post.id, post.saved_by_me);
      setItems((cur) => applySaved(cur, post.id, saved));
    } catch (err) {
      setItems((cur) => applySaved(cur, post.id, post.saved_by_me));
      toast(apiErrorMessage(err), "error");
    }
  }, [toast]);

  const onDelete = useCallback(async (post: Post) => {
    const snapshot = post;
    setItems((cur) => removePost(cur, post.id));
    try {
      await deletePostAction(post.id);
    } catch (err) {
      // A delete that failed put the post back at the TOP of the feed rather than where it was, so
      // the reappearance looked like a new post. Saying so is the least this can do.
      setItems((cur) => prependPost(cur, snapshot));
      toast(`${apiErrorMessage(err)} The post was not deleted.`, "error");
    }
  }, [toast]);

  if (items.length === 0) {
    return <EmptyState icon={<Newspaper size={32} />} title={emptyTitle} message={emptyMessage} />;
  }

  return (
    <>
      <div className="space-y-3">
        {items.map((p) => (
          <PostCard
            key={p.id}
            post={p}
            onLike={onLike}
            onSave={onSave}
            onDelete={onDelete}
            onShare={setSharing}
          />
        ))}
      </div>

      {/* Present before the first message, or the mutation is not observed and nothing is read. */}
      <p role="status" aria-live="polite" className="sr-only">{announcement}</p>

      {cursor ? (
        <Button variant="secondary" onClick={loadMore} disabled={loading} className="mt-4 w-full">
          {loading ? <Spinner size={16} decorative /> : null}
          {loading ? "Loading…" : "Load more"}
        </Button>
      ) : null}

      {sharing ? (
        <ShareDialog
          post={sharing}
          onClose={() => setSharing(null)}
          onShared={(created) => {
            setSharing(null);
            // Only the home feed is guaranteed to contain the viewer's own posts.
            if (source.kind === "feed" || source.kind === "mine") setItems((cur) => prependPost(cur, created));
          }}
        />
      ) : null}
    </>
  );
}
