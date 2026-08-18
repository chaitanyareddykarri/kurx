import { ChatRoomView } from "@/components/chat/chat-room-view";
import { listMyChats } from "@/lib/chat-api";
import { requireSession } from "@/lib/session";
import { section } from "@/lib/api";

// Event chat as a workspace tab. Reuses the full chat room view — messages, attachments, presence, and the
// host moderation workflow (pin / mute / ban / delete) are already capability-gated inside it.
export default async function EventChatPage({ params }: { params: { id: string } }) {
  const session = await requireSession();
  // "This event has no chat room" and "we could not fetch your rooms" are different answers, and the
  // fallback gave the first for both (D-235).
  const roomsResult = await section(listMyChats(session.accessToken));
  const rooms = roomsResult.state === "ok" ? roomsResult.data : [];
  const roomsFailed = roomsResult.state !== "ok";
  const room = rooms.find((r) => r.eventId === params.id);

  if (roomsFailed) {
    return (
      <p role="status" className="rounded-lg border border-dashed border-border p-6 text-sm text-muted">
        Your chat rooms couldn&apos;t be loaded, so this is not a statement that the event has no room
        yet. Try refreshing.
      </p>
    );
  }

  if (!room) {
    return (
      <p className="rounded-lg border border-border p-6 text-sm text-muted">
        The event chat room opens once the event is published and attendees join. You&apos;ll moderate it here.
      </p>
    );
  }

  return (
    <div className="h-[calc(100dvh-16rem)] min-h-[26rem] overflow-hidden rounded-lg border border-border">
      <ChatRoomView roomId={room.roomId} title={room.eventTitle} currentUserId={session.me.id} />
    </div>
  );
}
