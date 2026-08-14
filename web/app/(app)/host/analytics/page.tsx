import Link from "next/link";
import { requireSession } from "@/lib/session";
import { listMyEvents } from "@/lib/api";
import { Card, LinkButton } from "@kurx/ui";

// Reports hub: analytics are per-event, so this lists events and drills into each event's Analytics tab.
export default async function ReportsHubPage() {
  const session = await requireSession();
  // The caller's own events, across every organization they represent (D-267).
  const { items } = await listMyEvents(session.accessToken);

  return (
    <div className="space-y-4">
      <div>
        <h1 className="text-2xl font-semibold">Reports</h1>
        <p className="mt-1 text-sm text-muted">Pick an event to see its live analytics.</p>
      </div>
      {items.length === 0 ? (
        <Card>
          {/* Points at Profile, where creation is entered (D-305), rather than deep-linking past the
              eligibility gate straight into the form. */}
          <p className="text-sm text-muted">No events yet.</p>
          <LinkButton href="/profile" className="mt-3">Create one from your profile</LinkButton>
        </Card>
      ) : (
        <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
          {items.map((e) => (
            <Link key={e.id} href={`/host/events/${e.id}/analytics`} className="rounded-lg border border-border bg-surface p-4 transition hover:border-accent/70">
              <strong className="block">{e.title}</strong>
              <span className="mt-1 block text-xs text-muted">Analytics</span>
            </Link>
          ))}
        </div>
      )}
    </div>
  );
}
