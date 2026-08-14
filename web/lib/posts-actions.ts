"use server";

import { revalidatePath } from "next/cache";
import { requireSession } from "@/lib/session";
import { apiErrorMessage } from "@/lib/api";
import {
  getFeed, getMyPosts, getSavedPosts, getUserPosts, getEventPosts, getHashtagPosts, searchPosts,
  getComments, createPost, updatePost, deletePost, setPostLike, setPostSaved,
  sharePost, votePoll, addComment, deleteComment, setCommentLike,
  presignPostMedia, confirmPostMedia, reportContent,
  POST_BODY_MAX, COMMENT_BODY_MAX,
  type Post, type PostPage, type PostComment, type PostCommentPage,
  type PostLikeResult, type PostPoll, type PostKind, type PostVisibility,
} from "@/lib/posts-api";

/// Server actions over the frozen Posts contract (D-262).
///
/// Toggles (like/save) return the server's resulting state rather than an assumed flip, matching
/// `social-actions.ts` — the count has to come from the server anyway, since other people are
/// liking the same post concurrently and the backend mutates the counter in SQL (D-240).

export type ActionError = { error: string };

function isError<T>(r: T | ActionError): r is ActionError {
  return typeof r === "object" && r !== null && "error" in r;
}

// ── Feed reads (used by "load more", which runs on the client) ────────────────

export async function loadFeedAction(cursor?: string): Promise<PostPage> {
  const session = await requireSession();
  return getFeed(session.accessToken, cursor);
}

export async function loadMyPostsAction(cursor?: string): Promise<PostPage> {
  const session = await requireSession();
  return getMyPosts(session.accessToken, cursor);
}

export async function loadSavedPostsAction(cursor?: string): Promise<PostPage> {
  const session = await requireSession();
  return getSavedPosts(session.accessToken, cursor);
}

export async function loadUserPostsAction(username: string, cursor?: string): Promise<PostPage> {
  const session = await requireSession();
  return getUserPosts(session.accessToken, username, cursor);
}

export async function loadEventPostsAction(eventId: string, cursor?: string): Promise<PostPage> {
  const session = await requireSession();
  return getEventPosts(session.accessToken, eventId, cursor);
}

export async function loadHashtagPostsAction(tag: string, cursor?: string): Promise<PostPage> {
  const session = await requireSession();
  return getHashtagPosts(session.accessToken, tag, cursor);
}

export async function loadSearchPostsAction(q: string, cursor?: string): Promise<PostPage> {
  const session = await requireSession();
  return searchPosts(session.accessToken, q, cursor);
}

export async function loadCommentsAction(postId: string, cursor?: string): Promise<PostCommentPage> {
  const session = await requireSession();
  return getComments(session.accessToken, postId, cursor);
}

// ── Composer ─────────────────────────────────────────────────────────────────

export async function createPostAction(formData: FormData): Promise<Post | ActionError> {
  const session = await requireSession();

  const body = String(formData.get("body") ?? "").trim();
  const kind = String(formData.get("kind") ?? "text") as PostKind;
  const visibility = String(formData.get("visibility") ?? "public") as PostVisibility;
  const eventId = String(formData.get("eventId") ?? "") || undefined;
  const mediaIds = formData.getAll("mediaIds").map(String).filter(Boolean);

  // Poll options arrive as repeated fields so the form stays a plain <form> with no JSON blob.
  const pollQuestion = String(formData.get("pollQuestion") ?? "").trim();
  const pollOptions = formData.getAll("pollOption").map((v) => String(v).trim()).filter(Boolean);
  const poll = kind === "poll"
    ? {
        question: pollQuestion,
        allowMultiple: formData.get("pollAllowMultiple") === "on",
        closesAt: String(formData.get("pollClosesAt") ?? "") || undefined,
        options: pollOptions,
      }
    : undefined;

  // Validate only what the user can still fix here; everything else is the server's call.
  if (!body && mediaIds.length === 0 && !poll) return { error: "body_required" };
  if (body.length > POST_BODY_MAX) return { error: "body_too_long" };
  if (poll && poll.options.length < 2) return { error: "poll_needs_two_options" };

  try {
    const post = await createPost(session.accessToken, {
      body, kind, visibility, mediaIds, eventId, poll,
    });
    revalidatePath("/posts");
    return post;
  } catch (err) {
    return { error: apiErrorMessage(err) };
  }
}

export async function updatePostAction(
  postId: string,
  input: { body?: string; visibility?: PostVisibility }
): Promise<Post | ActionError> {
  const session = await requireSession();
  try {
    const post = await updatePost(session.accessToken, postId, input);
    revalidatePath("/posts");
    revalidatePath(`/posts/${postId}`);
    return post;
  } catch (err) {
    return { error: apiErrorMessage(err) };
  }
}

export async function deletePostAction(postId: string): Promise<void> {
  const session = await requireSession();
  await deletePost(session.accessToken, postId);
  revalidatePath("/posts");
}

// ── Engagement ───────────────────────────────────────────────────────────────

export async function togglePostLikeAction(postId: string, currentlyLiked: boolean): Promise<PostLikeResult> {
  const session = await requireSession();
  return setPostLike(session.accessToken, postId, !currentlyLiked);
}

export async function togglePostSavedAction(postId: string, currentlySaved: boolean): Promise<boolean> {
  const session = await requireSession();
  await setPostSaved(session.accessToken, postId, !currentlySaved);
  return !currentlySaved;
}

export async function sharePostAction(postId: string, body: string): Promise<Post | ActionError> {
  const session = await requireSession();
  try {
    const post = await sharePost(session.accessToken, postId, body.trim());
    revalidatePath("/posts");
    return post;
  } catch (err) {
    return { error: apiErrorMessage(err) };
  }
}

export async function votePollAction(postId: string, optionIds: string[]): Promise<PostPoll | ActionError> {
  const session = await requireSession();
  try {
    return await votePoll(session.accessToken, postId, optionIds);
  } catch (err) {
    return { error: apiErrorMessage(err) };
  }
}

// ── Comments ─────────────────────────────────────────────────────────────────

export async function addCommentAction(
  postId: string,
  body: string,
  parentCommentId?: string
): Promise<PostComment | ActionError> {
  const session = await requireSession();
  const trimmed = body.trim();
  if (!trimmed) return { error: "body_required" };
  if (trimmed.length > COMMENT_BODY_MAX) return { error: "body_too_long" };
  try {
    const comment = await addComment(session.accessToken, postId, trimmed, parentCommentId);
    revalidatePath(`/posts/${postId}`);
    return comment;
  } catch (err) {
    return { error: apiErrorMessage(err) };
  }
}

export async function deleteCommentAction(commentId: string, postId: string): Promise<void> {
  const session = await requireSession();
  await deleteComment(session.accessToken, commentId);
  revalidatePath(`/posts/${postId}`);
}

export async function toggleCommentLikeAction(
  commentId: string,
  currentlyLiked: boolean
): Promise<PostLikeResult> {
  const session = await requireSession();
  return setCommentLike(session.accessToken, commentId, !currentlyLiked);
}

export async function reportContentAction(
  entityType: "post" | "post_comment",
  entityId: string,
  reason: string,
  details?: string
): Promise<void | ActionError> {
  const session = await requireSession();
  try {
    await reportContent(session.accessToken, entityType, entityId, reason, details);
  } catch (err) {
    return { error: apiErrorMessage(err) };
  }
}

// ── Media ────────────────────────────────────────────────────────────────────

/// Two-step upload, same shape as chat attachments (D-110): presign → client PUTs the bytes
/// straight to storage → confirm. The file never passes through this server.
export async function presignPostMediaAction(file: {
  fileName: string;
  contentType: string;
  sizeBytes: number;
}) {
  const session = await requireSession();
  return presignPostMedia(session.accessToken, file);
}

export async function confirmPostMediaAction(mediaId: string, storageKey: string) {
  const session = await requireSession();
  return confirmPostMedia(session.accessToken, mediaId, storageKey);
}

export { isError };
