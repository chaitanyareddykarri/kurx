import { PostFeed } from "@/components/posts/post-feed";
import { getSavedPosts } from "@/lib/posts-api";
import { requireSession } from "@/lib/session";

export const metadata = { title: "Saved posts" };

export default async function SavedPostsPage() {
  const session = await requireSession();
  const initial = await getSavedPosts(session.accessToken).catch(() => ({ items: [], next_cursor: null }));

  return (
    <div className="mx-auto max-w-2xl">
      <h1 className="mb-4 text-2xl font-semibold text-text">Saved posts</h1>
      <PostFeed
        source={{ kind: "saved" }}
        initial={initial}
        emptyTitle="Nothing saved yet"
        emptyMessage="Tap the bookmark on a post to keep it here."
      />
    </div>
  );
}
