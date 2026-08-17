import { notFound } from "next/navigation";
import { PostDetail } from "@/components/posts/post-detail";
import { getPost, getComments } from "@/lib/posts-api";
import { requireSession } from "@/lib/session";

export const metadata = { title: "Post" };

/// Single post + its comments. A post the viewer may not see 404s here rather than 403ing — the
/// backend enforces that (D-018) and this simply mirrors it.
export default async function PostDetailPage({ params }: { params: { postId: string } }) {
  const session = await requireSession();

  const post = await getPost(session.accessToken, params.postId).catch(() => null);
  if (!post) notFound();

  const comments = await getComments(session.accessToken, params.postId).catch(() => ({
    items: [],
    next_cursor: null
  }));

  return (
    <PostDetail
      post={post}
      initialComments={comments}
      viewer={{ name: session.me.name, avatarUrl: session.me.avatar_url }}
    />
  );
}
