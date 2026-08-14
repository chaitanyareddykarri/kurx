import { redirect } from "next/navigation";

// This was an event picker that drilled into each event's workspace — which is precisely what
// /workspace is. The announcements themselves always lived on the event's own tab, so this page
// owned no content of its own and duplicated Workspace's list.
//
// Redirects rather than deletes, matching every other retired /host address.
export default function HostAnnouncementsRedirect() {
  redirect("/workspace");
}
