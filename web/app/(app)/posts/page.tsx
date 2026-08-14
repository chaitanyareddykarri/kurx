import { PostsHome } from "@/components/posts/posts-home";
import { getFeed, getTrendingHashtags, searchPosts } from "@/lib/posts-api";
import { requireSession } from "@/lib/session";

export const metadata = { title: "Posts" };

/// The feed, or search results when `?q=` is present. First page renders on the server either way, so
/// the timeline is readable before any client JS runs; "load more" and every interaction after that
/// are client-side.
export default async function PostsPage({
  searchParams
}: {
  searchParams?: Record<string, string | string[] | undefined>;
}) {
  const session = await requireSession();
  const raw = searchParams?.q;
  const query = (Array.isArray(raw) ? raw[0] : raw)?.trim() || undefined;

  const [page, trending] = await Promise.all([
    (query
      ? searchPosts(session.accessToken, query)
      : getFeed(session.accessToken)
    ).catch(() => ({ items: [], next_cursor: null })),
    getTrendingHashtags(session.accessToken).catch(() => [])
  ]);

  return (
    <PostsHome
      author={{ name: session.me.name, avatarKey: session.me.avatar_key }}
      initial={page}
      trending={trending}
      query={query}
    />
  );
}
