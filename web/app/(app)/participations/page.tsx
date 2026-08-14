import Link from "next/link";
import { EmptyState, Badge } from "@kurx/ui";
import { listMyParticipations, type Participation } from "@/lib/api";
import { requireSession } from "@/lib/session";

export const metadata = { title: "My participations" };

/**
 * The caller's programme roles across every event — speaker, judge, mentor, volunteer (D-305).
 *
 * `listMyParticipations` already existed in `api.ts` with **no page calling it**: a wrapper written and then
 * orphaned, which is why the audit counted it as a dead client method. Flutter has had this screen for
 * months.
 *
 * Distinct from a ticket. A participation is a role in the programme (D-272 gives it `Participant`
 * authority); a ticket is admission. Both put an event in your Workspace, for different reasons.
 *
 * `ParticipantEndpoints.ToJson` carries **no event title**, so rows link by id rather than inventing a
 * name the API did not return.
 */
export default async function ParticipationsPage() {
  const session = await requireSession();
  const rows = await listMyParticipations(session.accessToken).catch(() => [] as Participation[]);

  if (rows.length === 0) {
    return (
      <div className="mx-auto max-w-2xl py-12">
        <EmptyState
          icon="record_voice_over"
          title="No participations"
          message="Roles you accept in an event's programme — speaking, judging, mentoring, volunteering — appear here."
        />
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-2xl">
      <h1 className="text-h1 text-text">My participations</h1>
      <ul className="mt-4 space-y-3">
        {rows.map((p) => (
          <li key={p.id} className="rounded-lg border border-border bg-surface p-4">
            <div className="flex flex-wrap items-baseline justify-between gap-2">
              <span className="text-body font-semibold capitalize text-text">
                {p.custom_label || p.role_slug.replace(/[-_]/g, " ")}
              </span>
              <Badge tone={p.state === "accepted" ? "teal" : "warning"}>{p.state}</Badge>
            </div>
            {p.role_class ? <p className="mt-1 text-caption capitalize text-muted">{p.role_class}</p> : null}
            <Link
              href={`/workspace/${p.event_id}`}
              className="mt-2 inline-block text-caption text-accent-text hover:underline"
            >
              Open in Workspace
            </Link>
          </li>
        ))}
      </ul>
    </div>
  );
}
