import { PostFeed } from "@/components/posts/post-feed";
import { getEventPosts } from "@/lib/posts-api";
import { requireSession } from "@/lib/session";

export const metadata = { title: "Event posts" };

export default async function EventPostsPage({ params }: { params: { eventId: string } }) {
  const session = await requireSession();

  const initial = await getEventPosts(session.accessToken, params.eventId).catch(() => ({
    items: [],
    next_cursor: null
  }));

  return (
    <div className="mx-auto max-w-2xl">
      <h1 className="mb-4 text-2xl font-semibold text-text">Event posts</h1>
      <PostFeed
        source={{ kind: "event", eventId: params.eventId }}
        initial={initial}
        emptyTitle="No posts for this event yet"
        emptyMessage="Participants and organizers can post here."
      />
    </div>
  );
}
