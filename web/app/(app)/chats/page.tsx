import { MessagesSquare } from "lucide-react";
import { EmptyState } from "@kurx/ui";
import { ChatSearch } from "@/components/chat/chat-search";
import { RoomList } from "@/components/chat/room-list";
import { MessagesTabs } from "@/components/chat/messages-tabs";
import { listMyChats } from "@/lib/chat-api";
import { listDms, listDmRequests } from "@/lib/account-api";
import { requireSession } from "@/lib/session";

export const metadata = { title: "Messages" };

/// The Messages area: direct conversations, event chats, requests and the archive (D-264).
///
/// On desktop this is the left pane of the two-pane layout; with nothing selected it fills the width.
/// Every list is independent, so one failing (an event chat outage, say) does not blank the others.
export default async function ChatsPage() {
  const session = await requireSession();

  const [eventRooms, direct, requests, archived] = await Promise.all([
    listMyChats(session.accessToken).catch(() => []),
    listDms(session.accessToken).catch(() => []),
    listDmRequests(session.accessToken).catch(() => []),
    listDms(session.accessToken, true).catch(() => [])
  ]);

  const eventList =
    eventRooms.length === 0 ? (
      <div className="p-6">
        <EmptyState
          icon={<MessagesSquare size={32} />}
          title="No event chats"
          message="Chat rooms for events you attend appear here."
        />
      </div>
    ) : (
      <RoomList rooms={eventRooms} />
    );

  return (
    <div className="grid h-[calc(100dvh-10rem)] min-h-0 grid-cols-1 overflow-hidden rounded-lg border border-border md:grid-cols-[20rem_1fr]">
      <div className="flex min-h-0 flex-col border-r border-border">
        {/* D-295 — search spans every room, so it sits above the tabs rather than inside one. */}
        <ChatSearch />
        <MessagesTabs direct={direct} requests={requests} archived={archived} eventList={eventList} />
      </div>
      <div className="hidden items-center justify-center p-8 text-sm text-muted md:flex">
        Select a conversation to start reading.
      </div>
    </div>
  );
}
