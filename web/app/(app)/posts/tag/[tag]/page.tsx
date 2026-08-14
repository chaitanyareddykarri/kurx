import { PostFeed } from "@/components/posts/post-feed";
import { getHashtagPosts } from "@/lib/posts-api";
import { requireSession } from "@/lib/session";

export function generateMetadata({ params }: { params: { tag: string } }) {
  return { title: `#${params.tag}` };
}

export default async function HashtagPage({ params }: { params: { tag: string } }) {
  const session = await requireSession();
  const tag = decodeURIComponent(params.tag).toLowerCase();

  const initial = await getHashtagPosts(session.accessToken, tag).catch(() => ({
    items: [],
    next_cursor: null
  }));

  return (
    <div className="mx-auto max-w-2xl">
      <h1 className="mb-4 text-2xl font-semibold text-text">#{tag}</h1>
      <PostFeed
        source={{ kind: "hashtag", tag }}
        initial={initial}
        emptyTitle={`No posts tagged #${tag}`}
        emptyMessage="Be the first to use this tag."
      />
    </div>
  );
}
