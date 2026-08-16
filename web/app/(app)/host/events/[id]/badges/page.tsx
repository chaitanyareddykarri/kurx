import { requireEventOrg } from "@/lib/event-org";
import { can } from "@/lib/capabilities";
import { section } from "@/lib/api";
import { listBadgeRecipients, listBadgeSizes } from "@/lib/badge-api";
import { BadgeExport } from "@/components/host/badges/badge-export";
import { Card, PermissionDeniedState } from "@kurx/ui";

/**
 * Event Dashboard → Badges (D-362).
 *
 * Organizer-only, and that is the product decision rather than a permission detail: badges are
 * pre-printed onto lanyards here and handed out. There is no holder-facing badge view anywhere in the
 * product, so nothing links here from a user's own dashboard.
 */
export default async function EventBadgesPage({ params }: { params: { id: string } }) {
  const { session, caps } = await requireEventOrg(params.id);
  // The server gates on EventPermission.ManageContent (Manager level) inside IdCardService, and this is
  // the same Manager-level capability the host nav uses — so what reveals the page and what the page
  // allows agree. A badge is an entry credential; Staff-level view access is deliberately not enough.
  const canManage = can(caps, "events", "update");

  if (!canManage) {
    return (
      <PermissionDeniedState message="Only an event manager can print badges — a badge is an entry credential." />
    );
  }

  // Through `section` so an API outage renders "couldn't load" rather than an empty roster. An empty
  // roster is a specific claim ("nobody to badge yet") and must not be what a failed request looks
  // like (D-235).
  const [sizes, recipients] = await Promise.all([
    section(listBadgeSizes(session.accessToken, params.id)),
    section(listBadgeRecipients(session.accessToken, params.id))
  ]);

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-h2 text-text">Badges</h1>
        <p className="mt-1 max-w-2xl text-sm text-muted">
          Print lanyard badges for everyone at this event. Attendee badges carry their existing ticket QR,
          so they scan at the gate exactly as a phone does; staff badges carry a signed staff pass.
        </p>
      </div>

      {sizes.state !== "ok" || recipients.state !== "ok" ? (
        <Card>
          <p role="status" className="text-sm text-muted">
            The badge roster couldn&apos;t be loaded. That is a failure to read, not an empty event —
            nobody has been removed. Refresh to try again.
          </p>
        </Card>
      ) : (
        <BadgeExport
          eventId={params.id}
          accessToken={session.accessToken}
          sizes={sizes.data}
          recipients={recipients.data}
        />
      )}
    </div>
  );
}
