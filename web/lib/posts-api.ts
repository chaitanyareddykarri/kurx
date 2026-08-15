import { z } from "zod";
import { api } from "@/lib/api";

/// Typed access to the frozen Posts contract (D-262). Mirrors `lib/api.ts`: one function per
/// endpoint, zod-validated, accessToken passed in — never read from a module-level singleton.
///
/// Keys are **snake_case**: every response DTO lives in `Kurx.Application.Abstractions`, which is the
/// namespace `SnakeCaseResponseConverter` keys off (D-259). Request bodies stay camelCase — the
/// converter's Read path is delegated, so the platform contract is camelCase in, snake_case out.

function authHeaders(accessToken: string) {
  return { headers: { Authorization: `Bearer ${accessToken}` } };
}

export const POST_KINDS = ["text", "images", "video", "document", "poll", "event", "share"] as const;
export type PostKind = (typeof POST_KINDS)[number];

/// Mirrors the backend `PostVisibility` enum. Order is the render order of the composer's picker,
/// widest audience first — which is also the order the labels below assume.
export const POST_VISIBILITIES = ["public", "connections", "event_participants", "only_me"] as const;
export type PostVisibility = (typeof POST_VISIBILITIES)[number];

export const VISIBILITY_LABEL: Record<PostVisibility, string> = {
  public: "Anyone",
  connections: "Allies only",
  event_participants: "Event participants",
  only_me: "Only me"
};

export const postAuthorSchema = z.object({
  id: z.string(),
  name: z.string(),
  username: z.string().nullable().optional(),
  avatar_key: z.string().nullable().optional(),
  /// Presigned companion (D-302) — what renders; the key alone is not fetchable.
  avatar_url: z.string().nullable().optional(),
  is_verified: z.boolean().default(false)
});
export type PostAuthor = z.infer<typeof postAuthorSchema>;

export const postMediaSchema = z.object({
  id: z.string(),
  kind: z.string(),
  url: z.string(),
  content_type: z.string(),
  size_bytes: z.number(),
  width: z.number().nullable().optional(),
  height: z.number().nullable().optional(),
  duration_seconds: z.number().nullable().optional(),
  sort: z.number().default(0)
});
export type PostMedia = z.infer<typeof postMediaSchema>;

export const postPollOptionSchema = z.object({
  id: z.string(),
  text: z.string(),
  sort: z.number().default(0),
  vote_count: z.number().default(0),
  voted_by_me: z.boolean().default(false)
});
export type PostPollOption = z.infer<typeof postPollOptionSchema>;

export const postPollSchema = z.object({
  id: z.string(),
  question: z.string(),
  allow_multiple: z.boolean().default(false),
  closes_at: z.string().nullable().optional(),
  is_closed: z.boolean().default(false),
  total_votes: z.number().default(0),
  options: z.array(postPollOptionSchema).default([])
});
export type PostPoll = z.infer<typeof postPollSchema>;

export const postEventRefSchema = z.object({
  id: z.string(),
  title: z.string(),
  slug: z.string(),
  banner_key: z.string().nullable().optional(),
  /// Presigned; `banner_key` beside it is not fetchable (D-302).
  banner_url: z.string().nullable().optional(),
  starts_at: z.string(),
  city: z.string().nullable().optional()
});
export type PostEventRef = z.infer<typeof postEventRefSchema>;

/// `shared_post` is typed as the base shape rather than recursively as `Post`: the backend never
/// nests more than one level (a share of a share attaches to the original, `cannot_share_a_share`),
/// so a recursive zod type would model a state the contract forbids.
const postBaseSchema = z.object({
  id: z.string(),
  author: postAuthorSchema,
  kind: z.string(),
  body: z.string().default(""),
  visibility: z.string(),
  media: z.array(postMediaSchema).default([]),
  poll: postPollSchema.nullable().optional(),
  event: postEventRefSchema.nullable().optional(),
  hashtags: z.array(z.string()).default([]),
  mentions: z.array(postAuthorSchema).default([]),
  like_count: z.number().default(0),
  comment_count: z.number().default(0),
  share_count: z.number().default(0),
  liked_by_me: z.boolean().default(false),
  saved_by_me: z.boolean().default(false),
  can_edit: z.boolean().default(false),
  can_delete: z.boolean().default(false),
  created_at: z.string(),
  edited_at: z.string().nullable().optional()
});

export const postSchema = postBaseSchema.extend({
  shared_post: postBaseSchema.nullable().optional()
});
export type Post = z.infer<typeof postSchema>;
export type SharedPost = z.infer<typeof postBaseSchema>;

export const postPageSchema = z.object({
  items: z.array(postSchema).default([]),
  next_cursor: z.string().nullable().optional()
});
export type PostPage = z.infer<typeof postPageSchema>;

export const postCommentSchema = z.object({
  id: z.string(),
  post_id: z.string(),
  author: postAuthorSchema,
  body: z.string(),
  parent_comment_id: z.string().nullable().optional(),
  like_count: z.number().default(0),
  liked_by_me: z.boolean().default(false),
  can_delete: z.boolean().default(false),
  created_at: z.string()
});
export type PostComment = z.infer<typeof postCommentSchema>;

export const postCommentPageSchema = z.object({
  items: z.array(postCommentSchema).default([]),
  next_cursor: z.string().nullable().optional()
});
export type PostCommentPage = z.infer<typeof postCommentPageSchema>;

export const postLikeResultSchema = z.object({
  liked: z.boolean(),
  like_count: z.number()
});
export type PostLikeResult = z.infer<typeof postLikeResultSchema>;

export const postMediaPresignSchema = z.object({
  media_id: z.string(),
  upload_url: z.string(),
  storage_key: z.string()
});
export type PostMediaPresign = z.infer<typeof postMediaPresignSchema>;

export const trendingHashtagSchema = z.object({
  tag: z.string(),
  post_count: z.number()
});
export type TrendingHashtag = z.infer<typeof trendingHashtagSchema>;

// ── Reads ────────────────────────────────────────────────────────────────────

/// Cursors are opaque tokens (same rule as chat, D-104) — passed straight back, never parsed.
async function getPage(accessToken: string, url: string, cursor?: string, limit = 20): Promise<PostPage> {
  const { data } = await api.get(url, {
    ...authHeaders(accessToken),
    params: { cursor, limit }
  });
  return postPageSchema.parse(data);
}

export function getFeed(accessToken: string, cursor?: string, limit?: number) {
  return getPage(accessToken, "/v1/feed", cursor, limit);
}

export function getMyPosts(accessToken: string, cursor?: string, limit?: number) {
  return getPage(accessToken, "/v1/me/posts", cursor, limit);
}

export function getSavedPosts(accessToken: string, cursor?: string, limit?: number) {
  return getPage(accessToken, "/v1/me/posts/saved", cursor, limit);
}

export function getUserPosts(accessToken: string, username: string, cursor?: string, limit?: number) {
  return getPage(accessToken, `/v1/public/users/${encodeURIComponent(username)}/posts`, cursor, limit);
}

export function getEventPosts(accessToken: string, eventId: string, cursor?: string, limit?: number) {
  return getPage(accessToken, `/v1/events/${eventId}/posts`, cursor, limit);
}

export function getHashtagPosts(accessToken: string, tag: string, cursor?: string, limit?: number) {
  return getPage(accessToken, `/v1/hashtags/${encodeURIComponent(tag)}/posts`, cursor, limit);
}

/// Free-text search over post bodies, scoped server-side to what the caller may already see. A blank
/// `q` comes back empty from the API rather than as the whole table, so this needs no guard of its own.
export async function searchPosts(accessToken: string, q: string, cursor?: string, limit = 20): Promise<PostPage> {
  const { data } = await api.get("/v1/posts/search", {
    ...authHeaders(accessToken),
    params: { q, cursor, limit }
  });
  return postPageSchema.parse(data);
}

export async function getPost(accessToken: string, postId: string): Promise<Post> {
  const { data } = await api.get(`/v1/posts/${postId}`, authHeaders(accessToken));
  return postSchema.parse(data);
}

export async function getTrendingHashtags(accessToken: string, limit = 10): Promise<TrendingHashtag[]> {
  const { data } = await api.get("/v1/hashtags/trending", { ...authHeaders(accessToken), params: { limit } });
  return z.array(trendingHashtagSchema).parse(data);
}

export async function getComments(
  accessToken: string,
  postId: string,
  cursor?: string,
  limit = 20
): Promise<PostCommentPage> {
  const { data } = await api.get(`/v1/posts/${postId}/comments`, {
    ...authHeaders(accessToken),
    params: { cursor, limit }
  });
  return postCommentPageSchema.parse(data);
}

// ── Writes ───────────────────────────────────────────────────────────────────

export type CreatePostInput = {
  body: string;
  kind: PostKind;
  visibility: PostVisibility;
  mediaIds?: string[];
  eventId?: string;
  sharedPostId?: string;
  poll?: { question: string; allowMultiple: boolean; closesAt?: string; options: string[] };
};

export async function createPost(accessToken: string, input: CreatePostInput): Promise<Post> {
  const { data } = await api.post("/v1/posts", input, authHeaders(accessToken));
  return postSchema.parse(data);
}

export async function updatePost(
  accessToken: string,
  postId: string,
  input: { body?: string; visibility?: PostVisibility }
): Promise<Post> {
  const { data } = await api.patch(`/v1/posts/${postId}`, input, authHeaders(accessToken));
  return postSchema.parse(data);
}

export async function deletePost(accessToken: string, postId: string): Promise<void> {
  await api.delete(`/v1/posts/${postId}`, authHeaders(accessToken));
}

export async function setPostLike(accessToken: string, postId: string, liked: boolean): Promise<PostLikeResult> {
  const url = `/v1/posts/${postId}/like`;
  const { data } = liked
    ? await api.post(url, {}, authHeaders(accessToken))
    : await api.delete(url, authHeaders(accessToken));
  return postLikeResultSchema.parse(data);
}

export async function setPostSaved(accessToken: string, postId: string, saved: boolean): Promise<void> {
  const url = `/v1/posts/${postId}/save`;
  if (saved) await api.post(url, {}, authHeaders(accessToken));
  else await api.delete(url, authHeaders(accessToken));
}

export async function sharePost(accessToken: string, postId: string, body: string): Promise<Post> {
  const { data } = await api.post(
    "/v1/posts",
    { body, kind: "share", visibility: "public", sharedPostId: postId },
    authHeaders(accessToken)
  );
  return postSchema.parse(data);
}

export async function votePoll(accessToken: string, postId: string, optionIds: string[]): Promise<PostPoll> {
  const { data } = await api.post(`/v1/posts/${postId}/poll/vote`, { optionIds }, authHeaders(accessToken));
  return postPollSchema.parse(data);
}

export async function addComment(
  accessToken: string,
  postId: string,
  body: string,
  parentCommentId?: string
): Promise<PostComment> {
  const { data } = await api.post(
    `/v1/posts/${postId}/comments`,
    { body, parentCommentId },
    authHeaders(accessToken)
  );
  return postCommentSchema.parse(data);
}

export async function deleteComment(accessToken: string, commentId: string): Promise<void> {
  await api.delete(`/v1/comments/${commentId}`, authHeaders(accessToken));
}

export async function setCommentLike(
  accessToken: string,
  commentId: string,
  liked: boolean
): Promise<PostLikeResult> {
  const url = `/v1/comments/${commentId}/like`;
  const { data } = liked
    ? await api.post(url, {}, authHeaders(accessToken))
    : await api.delete(url, authHeaders(accessToken));
  return postLikeResultSchema.parse(data);
}

/// Content reports reuse the platform endpoint (D-059) rather than a posts-specific one —
/// `entityType` is `post` or `post_comment`. One moderation queue, not two.
export async function reportContent(
  accessToken: string,
  entityType: "post" | "post_comment",
  entityId: string,
  reason: string,
  details?: string
): Promise<void> {
  await api.post("/v1/reports", { entityType, entityId, reason, details }, authHeaders(accessToken));
}

// ── Media upload ─────────────────────────────────────────────────────────────

export async function presignPostMedia(
  accessToken: string,
  file: { fileName: string; contentType: string; sizeBytes: number }
): Promise<PostMediaPresign> {
  const { data } = await api.post("/v1/posts/media/presign", file, authHeaders(accessToken));
  return postMediaPresignSchema.parse(data);
}

export async function confirmPostMedia(
  accessToken: string,
  mediaId: string,
  storageKey: string
): Promise<PostMedia> {
  const { data } = await api.post(
    "/v1/posts/media/confirm",
    { mediaId, storageKey },
    authHeaders(accessToken)
  );
  return postMediaSchema.parse(data);
}

/// Media caps are enforced server-side (`too_many_media`); these mirror them so the composer can
/// disable its own picker instead of letting the user upload into a rejection.
export const MEDIA_CAPS = { images: 10, video: 1, document: 5 } as const;

/// Body cap mirrors the backend's 3000-char limit (`body_too_long`).
export const POST_BODY_MAX = 3000;
export const COMMENT_BODY_MAX = 1000;
