import { PostFeed } from "@/components/posts/post-feed";
import { getUserPosts } from "@/lib/posts-api";
import { requireSession } from "@/lib/session";

export function generateMetadata({ params }: { params: { username: string } }) {
  return { title: `@${params.username} · Posts` };
}

export default async function UserPostsPage({ params }: { params: { username: string } }) {
  const session = await requireSession();
  const username = params.username.toLowerCase();
  const initial = await getUserPosts(session.accessToken, username).catch(() => ({
    items: [],
    next_cursor: null
  }));

  return (
    <div className="mx-auto max-w-2xl">
      <h1 className="mb-4 text-2xl font-semibold text-text">@{username}</h1>
      <PostFeed
        source={{ kind: "user", username }}
        initial={initial}
        emptyTitle="No posts"
        emptyMessage="This person hasn't posted anything you can see."
      />
    </div>
  );
}
