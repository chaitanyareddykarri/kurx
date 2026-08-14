import Link from "next/link";
import { CalendarClock, MapPin } from "lucide-react";
import { requireSession } from "@/lib/session";
import { listMyInvitations, section } from "@/lib/api";
import { formatDate } from "@/lib/formatters";
import { Card } from "@kurx/ui";
import { InvitationActions } from "@/components/invitations/invitation-actions";

/**
 * D-266 M6 (D9 Method A) — the invitee's inbox.
 *
 * **Accepting grants permission to register. It is not a ticket and not a booking.** The copy here says so
 * in as many words, because the single most damaging misreading of an invitation is "I'm going" — an
 * invited guest of a paid event still pays, and no ticket exists until an order settles.
 *
 * Without this screen Method A was unreachable: the backend delivered an in-app notification and there was
 * nowhere to act on it.
 */
export default async function InvitationsPage() {
  const session = await requireSession();
  // An outage rendered the same empty state as "you have no invitations", so an invite a host is
  // waiting on the user to accept simply appeared not to exist (D-235).
  const result = await section(listMyInvitations(session.accessToken));
  const invitations = result.state === "ok" ? result.data : [];
  const failed = result.state !== "ok";

  const pending = invitations.filter((i) => i.rsvp_status === "None");
  const settled = invitations.filter((i) => i.rsvp_status !== "None");

  return (
    <div className="mx-auto w-full max-w-3xl space-y-6 p-4">
      <header>
        <h1 className="text-xl font-semibold text-text">Invitations</h1>
        <p className="mt-1 text-sm text-muted">
          Accepting an invitation lets you register for the event. It doesn&apos;t book a place or issue a
          ticket — if the event charges, you still pay when you register.
        </p>
      </header>

      {failed ? (
        <Card>
          <p role="status" className="text-sm text-text">
            Your invitations couldn&apos;t be loaded.{" "}
            <span className="text-muted">
              This is a temporary problem on our side, not a change to your invitations. Try refreshing.
            </span>
          </p>
        </Card>
      ) : invitations.length === 0 ? (
        <Card>
          <p className="text-sm text-muted">
            No invitations yet. When an organiser invites you, it shows up here.
          </p>
        </Card>
      ) : null}

      {pending.length > 0 ? (
        <section className="space-y-3">
          <h2 className="text-sm font-semibold text-text">Waiting for your answer</h2>
          {pending.map((i) => <InvitationRow key={i.id} invitation={i} showActions />)}
        </section>
      ) : null}

      {settled.length > 0 ? (
        <section className="space-y-3">
          <h2 className="text-sm font-semibold text-text">Answered</h2>
          {settled.map((i) => <InvitationRow key={i.id} invitation={i} />)}
        </section>
      ) : null}
    </div>
  );
}

function InvitationRow({
  invitation: i,
  showActions = false,
}: {
  invitation: Awaited<ReturnType<typeof listMyInvitations>>[number];
  showActions?: boolean;
}) {
  const accepted = i.rsvp_status === "Accepted";
  return (
    <Card>
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0">
          <Link href={`/e/${i.event_slug}`} className="font-semibold text-text hover:underline">
            {i.event_title}
          </Link>
          <p className="mt-1 flex flex-wrap items-center gap-x-3 gap-y-1 text-xs text-muted">
            <span className="inline-flex items-center gap-1">
              <CalendarClock size={12} aria-hidden /> {formatDate(i.starts_at, "en-IN", { day: "numeric", month: "short", year: "numeric" })}
            </span>
            {i.venue_name || i.city ? (
              <span className="inline-flex items-center gap-1">
                <MapPin size={12} /> {[i.venue_name, i.city].filter(Boolean).join(", ")}
              </span>
            ) : null}
            <span>invited by {i.invited_by_name}</span>
          </p>
        </div>

        {showActions ? (
          <InvitationActions invitationId={i.id} />
        ) : (
          <span className={`text-xs font-semibold ${accepted ? "text-success" : "text-muted"}`}>
            {accepted ? "Accepted" : "Declined"}
          </span>
        )}
      </div>

      {accepted ? (
        <p className="mt-3 text-xs text-muted">
          You can register for this event.{" "}
          <Link href={`/e/${i.event_slug}`} className="text-accent-text underline-offset-2 hover:underline focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent">
            Go to the event
          </Link>{" "}
          to complete registration.
        </p>
      ) : null}
    </Card>
  );
}
