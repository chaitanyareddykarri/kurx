import Link from "next/link";
import { Card } from "@/components/ui/card";
import type { Notification } from "@/lib/api";
import { MarkAllReadButton, MarkReadButton } from "@/components/notifications/notification-buttons";
import { AllyNotificationActions } from "@/components/notifications/ally-notification-actions";

/// Deep-link target for a notification, or null when it does not point anywhere.
///
/// The generic convention (D-20x): any `data_json` may carry a `route` (an in-app path) — every new
/// notification kind should populate it instead of inventing another one-off payload shape. Chat
/// notifications (D-107) predate this convention and still carry their own
/// `{ notificationType: "chat", eventId, roomId, messageId }` shape, kept working as a fallback.
///
/// Tolerant by design: unparseable or unknown payloads simply yield no link rather than throwing.
function deepLink(dataJson: string | null): string | null {
  if (!dataJson) return null;
  try {
    const data = JSON.parse(dataJson) as Record<string, unknown>;
    if (typeof data.route === "string" && data.route.startsWith("/")) return data.route;
    if (data.notificationType !== "chat") return null;
    const eventId = data.eventId;
    return typeof eventId === "string" && eventId.length > 0 ? `/chats/${eventId}` : null;
  } catch {
    return null;
  }
}

/// The ally connectionId a "New Ally Request" notification carries, so its card can render inline
/// Accept/Decline without a lookup. Null for every other kind.
function allyConnectionId(kind: string, dataJson: string | null): string | null {
  if (kind !== "ally.requested" || !dataJson) return null;
  try {
    const data = JSON.parse(dataJson) as Record<string, unknown>;
    return typeof data.connectionId === "string" ? data.connectionId : null;
  } catch {
    return null;
  }
}

// Presentational feed shared by the user (/notifications) and host (/host/notifications) pages —
// both render the same per-user notification list from /v1/me/notifications.
export function NotificationsFeed({
  title,
  items,
  unreadCount
}: {
  title: string;
  items: Notification[];
  unreadCount: number;
}) {
  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between gap-4">
        <div>
          <h1 className="text-3xl font-semibold">{title}</h1>
          <p className="mt-2 text-sm text-muted">
            {unreadCount > 0 ? `${unreadCount} unread` : "You're all caught up."}
          </p>
        </div>
        {items.length > 0 ? <MarkAllReadButton /> : null}
      </div>

      {items.length === 0 ? (
        <p className="text-muted">No notifications yet. Ticket, certificate, and payment updates will show up here.</p>
      ) : (
        <div className="space-y-3">
          {items.map((n) => {
            const connectionId = allyConnectionId(n.kind, n.data_json);
            return (
              <Card key={n.id} className={n.read_at ? undefined : "border-accent"}>
                <div className="flex items-start justify-between gap-3">
                  <div>
                    {deepLink(n.data_json) ? (
                      <Link href={deepLink(n.data_json)!} className="font-semibold hover:underline">
                        {n.title}
                      </Link>
                    ) : (
                      <h2 className="font-semibold">{n.title}</h2>
                    )}
                    {n.body ? <p className="mt-1 text-sm text-muted">{n.body}</p> : null}
                    <p className="mt-1 text-xs text-muted">
                      {new Date(n.created_at).toLocaleString("en-IN", { dateStyle: "medium", timeStyle: "short" })}
                    </p>
                    {connectionId && <AllyNotificationActions connectionId={connectionId} />}
                  </div>
                  {n.read_at ? <span className="shrink-0 text-xs text-muted">Read</span> : <MarkReadButton id={n.id} />}
                </div>
              </Card>
            );
          })}
        </div>
      )}
    </div>
  );
}
