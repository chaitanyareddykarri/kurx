import { PostFeed } from "@/components/posts/post-feed";
import { getMyPosts } from "@/lib/posts-api";
import { requireSession } from "@/lib/session";

export const metadata = { title: "My posts" };

export default async function MyPostsPage() {
  const session = await requireSession();
  const initial = await getMyPosts(session.accessToken).catch(() => ({ items: [], next_cursor: null }));

  return (
    <div className="mx-auto max-w-2xl">
      <h1 className="mb-4 text-2xl font-semibold text-text">My posts</h1>
      <PostFeed
        source={{ kind: "mine" }}
        initial={initial}
        emptyTitle="You haven't posted yet"
        emptyMessage="Anything you post shows up here."
      />
    </div>
  );
}
