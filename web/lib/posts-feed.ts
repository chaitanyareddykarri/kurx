import type { Post, PostPage, PostLikeResult } from "@/lib/posts-api";

/// Feed state transitions, kept out of the component so they are testable without a DOM.

export type MergedFeed = { items: Post[]; nextCursor: string | null };

/// Appends a page, dropping ids already held.
///
/// The dedupe is not defensive padding: the feed is keyset-paged, so a post created between two
/// fetches shifts the window and the boundary row legitimately arrives in both pages. Without this,
/// React renders duplicate keys and the count visibly jumps.
export function mergePostPage(current: Post[], page: PostPage): MergedFeed {
  const seen = new Set(current.map((p) => p.id));
  const added = page.items.filter((p) => !seen.has(p.id));
  return { items: [...current, ...added], nextCursor: page.next_cursor ?? null };
}

/// Writes a like result onto one post. The count comes from the server rather than being
/// incremented locally — other people are liking the same post, and the backend mutates the counter
/// in SQL (D-240), so the returned value is the only trustworthy one.
export function applyLike(items: Post[], postId: string, result: PostLikeResult): Post[] {
  if (!items.some((p) => p.id === postId)) return items;
  return items.map((p) =>
    p.id === postId ? { ...p, liked_by_me: result.liked, like_count: result.like_count } : p
  );
}

export function applySaved(items: Post[], postId: string, saved: boolean): Post[] {
  return items.map((p) => (p.id === postId ? { ...p, saved_by_me: saved } : p));
}

export function removePost(items: Post[], postId: string): Post[] {
  return items.filter((p) => p.id !== postId);
}

export function prependPost(items: Post[], post: Post): Post[] {
  return [post, ...items.filter((p) => p.id !== post.id)];
}
