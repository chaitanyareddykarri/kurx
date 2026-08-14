import { redirect } from "next/navigation";

// Duplicated `/notifications` exactly — same feed, same endpoint, same component — under a second
// address nothing linked to. Notifications are one surface, reached from the header bell; a second
// copy under /host was a module owning another module's responsibility.
//
// Redirects rather than deletes, matching every other retired /host address, so an existing
// bookmark still lands somewhere real.
export default function HostNotificationsRedirect() {
  redirect("/notifications");
}
