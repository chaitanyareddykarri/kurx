import { EmptyState, Badge } from "@kurx/ui";
import { listMyOrgInvitations, type OrgInvitation } from "@/lib/api";
import { requireSession } from "@/lib/session";
import { formatDate } from "@/lib/formatters";

export const metadata = { title: "Organization invitations" };

/**
 * Invitations to represent an organization (D-305).
 *
 * `GET /v1/me/org-invitations` had no web caller. Flutter reaches it and labels it a read-only inbox
 * for a precise reason, repeated here: **accept and decline are token-keyed**
 * (`POST /v1/org-invitations/{token}/accept`), and the token only ever arrives in the invite message.
 * This page never holds one, so it shows what is pending and deliberately offers no buttons — an
 * Accept control here would have nothing to send and would fail every time it was pressed.
 */
export default async function OrgInvitationsPage() {
  const session = await requireSession();
  const invitations = await listMyOrgInvitations(session.accessToken).catch(() => [] as OrgInvitation[]);

  if (invitations.length === 0) {
    return (
      <div className="mx-auto max-w-2xl py-12">
        <EmptyState
          icon="mark_email_unread"
          title="No organization invitations"
          message="When an organization invites you to represent it, the invitation appears here."
        />
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-2xl">
      <h1 className="text-h1 text-text">Organization invitations</h1>
      <p className="mt-1 text-body text-muted">
        Open the invite link you were sent to accept or decline — the link carries the token that authorises it.
      </p>
      <ul className="mt-4 space-y-3">
        {invitations.map((i) => (
          <li key={i.id} className="rounded-lg border border-border bg-surface p-4">
            <div className="flex flex-wrap items-baseline justify-between gap-2">
              <span className="text-body font-semibold text-text">{i.org_name}</span>
              <Badge tone={i.status === "pending" ? "warning" : "neutral"}>{i.status}</Badge>
            </div>
            <p className="mt-1 text-caption capitalize text-muted">
              Role: {i.role}
              {i.expires_at
                ? ` · expires ${formatDate(i.expires_at, "en-IN", { day: "numeric", month: "short", year: "numeric" })}`
                : ""}
            </p>
          </li>
        ))}
      </ul>
    </div>
  );
}
