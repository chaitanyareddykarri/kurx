import { EmptyState } from "@kurx/ui";
import { listMyWaitlist, type WaitlistEntry } from "@/lib/api";
import { requireSession } from "@/lib/session";
import { formatDate } from "@/lib/formatters";

export const metadata = { title: "My waitlist" };

/**
 * The caller's waitlist entries (D-305).
 *
 * `GET /v1/me/waitlist` has existed since the waitlist shipped and web had **no caller for it** — the
 * Flutter app has shown this for months. Read-only by design: joining is done from a sold-out ticket
 * type, and an offer is claimed from the notification that carries it.
 *
 * The endpoint returns no event title (it joins none), so rows identify the event by id rather than
 * inventing a name the API did not give.
 */
export default async function WaitlistPage() {
  const session = await requireSession();
  const entries = await listMyWaitlist(session.accessToken).catch(() => [] as WaitlistEntry[]);

  if (entries.length === 0) {
    return (
      <div className="mx-auto max-w-2xl py-12">
        <EmptyState
          icon="hourglass_empty"
          title="Nothing on your waitlist"
          message="When a ticket type is sold out you can join its waitlist, and it will appear here."
        />
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-2xl">
      <h1 className="mb-4 text-h1 text-text">My waitlist</h1>
      <ul className="space-y-3">
        {entries.map((w) => (
          <li key={w.id} className="rounded-lg border border-border bg-surface p-4">
            <div className="flex items-baseline justify-between gap-3">
              <span className="text-body text-text">Position {w.position}</span>
              <span className="text-caption capitalize text-muted">{w.status}</span>
            </div>
            <p className="mt-1 text-caption text-muted">
              Joined {formatDate(w.created_at, "en-IN", { day: "numeric", month: "short", year: "numeric" })}
              {/* An offer that has expired is why a waitlist row stops moving; showing the deadline is the
                  difference between "still waiting" and "you missed it". */}
              {w.offer_expires_at
                ? ` · offer expires ${formatDate(w.offer_expires_at, "en-IN", { day: "numeric", month: "short", hour: "numeric", minute: "2-digit" })}`
                : ""}
            </p>
          </li>
        ))}
      </ul>
    </div>
  );
}
