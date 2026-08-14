import { redirect } from "next/navigation";

// Preferences moved to the notifications screen, beside the notifications they govern — see
// `app/(app)/notifications/page.tsx`. Redirects rather than deletes so an existing bookmark, and the
// deep links carried by already-sent notification emails, still land somewhere real.
export default function NotificationSettingsRedirect() {
  redirect("/notifications?view=preferences");
}
