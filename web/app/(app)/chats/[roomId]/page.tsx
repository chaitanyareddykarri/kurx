import { notFound, redirect } from "next/navigation";
import { ChatRoomView } from "@/components/chat/chat-room-view";
import { RoomList } from "@/components/chat/room-list";
import { listMyChats } from "@/lib/chat-api";
import { listDms, listDmRequests } from "@/lib/account-api";
import { requireSession } from "@/lib/session";

/// One conversation, keyed by **roomId** (D-292).
///
/// This used to be keyed by eventId, which made every direct-message row a dead link: the Messages
/// inbox links a DM to `/chats/{room_id}`, nothing in the event-chat list ever matched it, and the
/// page answered `notFound()`. A DM has no event, so eventId cannot be the navigation key —
/// `ChatRoom.Id` is the one identifier both kinds share, and `GET /v1/chat/rooms/{roomId}` is what
/// makes it resolvable.
///
/// This URL is the deep-link target: the in-app notifications feed and any shared link land here.
export default async function ChatRoomPage({ params }: { params: { roomId: string } }) {
  const session = await requireSession();

  // Both sources, because "which conversation is this" spans them. Archived is included: opening an
  // archived conversation from a link must work, exactly as it does in the inbox.
  const [eventRooms, direct, requests, archived] = await Promise.all([
    listMyChats(session.accessToken).catch(() => []),
    listDms(session.accessToken).catch(() => []),
    listDmRequests(session.accessToken).catch(() => []),
    listDms(session.accessToken, true).catch(() => [])
  ]);

  const eventRoom = eventRooms.find((r) => r.roomId === params.roomId);
  const dm = [...direct, ...requests, ...archived].find((r) => r.room_id === params.roomId);

  // Backward compatibility: `/chats/{eventId}` was the old shape and is still in notification deep
  // links and anything a user bookmarked. Resolve it to the room and redirect rather than 404 —
  // a dead link is a worse answer than a moved one.
  if (!eventRoom && !dm) {
    const legacy = eventRooms.find((r) => r.eventId === params.roomId);
    if (legacy) redirect(`/chats/${legacy.roomId}`);
    // Membership is the server's call; a room the caller is not in is not theirs to see (D-018).
    notFound();
  }

  // An event room is titled by its event; a direct room by the person on the other side.
  const title = eventRoom?.eventTitle ?? dm?.other_name ?? "Conversation";

  return (
    <div className="grid h-[calc(100vh-10rem)] min-h-0 grid-cols-1 overflow-hidden rounded-lg border border-border md:grid-cols-[20rem_1fr]">
      {/* Sidebar is desktop-only; on small screens the room owns the viewport. */}
      <div className="hidden min-h-0 border-r border-border md:block">
        <RoomList rooms={eventRooms} activeRoomId={params.roomId} />
      </div>
      <ChatRoomView roomId={params.roomId} title={title} currentUserId={session.me.id} />
    </div>
  );
}
