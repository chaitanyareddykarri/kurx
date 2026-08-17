"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { PostCard } from "@/components/posts/post-card";
import { CommentThread } from "@/components/posts/comment-thread";
import { ShareDialog } from "@/components/posts/share-dialog";
import { applyLike, applySaved } from "@/lib/posts-feed";
import { togglePostLikeAction, togglePostSavedAction, deletePostAction } from "@/lib/posts-actions";
import type { Post, PostCommentPage } from "@/lib/posts-api";

export function PostDetail({
  post: initial,
  initialComments,
  viewer
}: {
  post: Post;
  initialComments: PostCommentPage;
  viewer: { name: string; avatarUrl?: string | null };
}) {
  const router = useRouter();
  const [post, setPost] = useState(initial);
  const [sharing, setSharing] = useState<Post | null>(null);

  // The card's handlers are list-shaped; a detail page is just a list of one.
  const one = (next: Post[]) => next[0] ?? post;

  return (
    <div className="mx-auto max-w-2xl">
      <PostCard
        post={post}
        onLike={async (p) => {
          const result = await togglePostLikeAction(p.id, p.liked_by_me);
          setPost((cur) => one(applyLike([cur], p.id, result)));
        }}
        onSave={async (p) => {
          const saved = await togglePostSavedAction(p.id, p.saved_by_me);
          setPost((cur) => one(applySaved([cur], p.id, saved)));
        }}
        onDelete={async (p) => {
          await deletePostAction(p.id);
          router.push("/posts");
        }}
        onShare={setSharing}
      />

      <CommentThread
        postId={post.id}
        initial={initialComments}
        viewer={viewer}
        onCountChange={(delta) =>
          setPost((cur) => ({ ...cur, comment_count: Math.max(0, cur.comment_count + delta) }))
        }
      />

      {sharing ? (
        <ShareDialog post={sharing} onClose={() => setSharing(null)} onShared={() => setSharing(null)} />
      ) : null}
    </div>
  );
}
