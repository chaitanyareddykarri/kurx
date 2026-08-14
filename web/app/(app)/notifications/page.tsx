import Link from "next/link";
import { listNotifications } from "@/lib/api";
import { getNotificationPreferences } from "@/lib/account-api";
import { requireSession } from "@/lib/session";
import { NotificationsFeed } from "@/components/notifications/notifications-feed";
import { NotificationPreferences } from "@/components/settings/notification-preferences";
import { Card } from "@/components/ui/card";

export const metadata = { title: "Notifications · Kurx" };

/**
 * Notifications — inbox and preferences, together.
 *
 * Preferences used to live at `/settings/notifications`, three sections away from the bell that
 * delivers what they govern. They are the same subject: one is what was sent, the other is what may
 * be sent. Splitting them meant a person who wanted to stop a notification had to leave the screen
 * showing it and hunt through Settings.
 *
 * Both render on one page rather than behind a client-side tab: each is a server read, the page is
 * short, and a tab would trade a scroll for a round trip.
 */
export default async function NotificationsPage({
  searchParams
}: {
  searchParams: { view?: string };
}) {
  const session = await requireSession();
  const showPreferences = searchParams.view === "preferences";

  // Each read degrades on its own. A preferences outage must not hide the inbox, and vice versa —
  // they are independent answers to independent questions.
  const [inbox, prefs] = await Promise.all([
    listNotifications(session.accessToken).catch(() => ({
      items: [] as Awaited<ReturnType<typeof listNotifications>>["items"],
      unread_count: 0
    })),
    getNotificationPreferences(session.accessToken).catch(() => null)
  ]);

  return (
    <div className="space-y-lg">
      <nav aria-label="Notification views" className="flex gap-1 border-b border-border">
        <Link
          href="/notifications"
          aria-current={showPreferences ? undefined : "page"}
          className={`inline-flex min-h-11 items-center border-b-2 px-4 text-body font-medium transition duration-fast ${
            showPreferences ? "border-transparent text-muted hover:text-text" : "border-accent text-text"
          }`}
        >
          Inbox
          {inbox.unread_count > 0 ? (
            <span className="ml-2 rounded-pill bg-accent px-1.5 text-micro font-semibold text-on-accent">
              {inbox.unread_count}
            </span>
          ) : null}
        </Link>
        <Link
          href="/notifications?view=preferences"
          aria-current={showPreferences ? "page" : undefined}
          className={`inline-flex min-h-11 items-center border-b-2 px-4 text-body font-medium transition duration-fast ${
            showPreferences ? "border-accent text-text" : "border-transparent text-muted hover:text-text"
          }`}
        >
          Preferences
        </Link>
      </nav>

      {showPreferences ? (
        prefs ? (
          <div className="space-y-md">
            <p className="text-sm text-muted">
              What you get told about, and how. WhatsApp is off by default — it costs money per
              message and reaches a surface people treat as personal.
            </p>
            <NotificationPreferences initial={prefs.categories} />
          </div>
        ) : (
          <Card>
            <p className="text-sm text-muted">
              Preferences couldn&apos;t be loaded. Your existing settings are unchanged — try again in
              a moment.
            </p>
          </Card>
        )
      ) : (
        <NotificationsFeed title="Notifications" items={inbox.items} unreadCount={inbox.unread_count} />
      )}
    </div>
  );
}
