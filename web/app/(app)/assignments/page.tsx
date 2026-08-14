import { ClipboardCheck } from "lucide-react";
import { EmptyState } from "@kurx/ui";
import { AssignmentRow } from "@/components/assignments/assignment-row";
import { listMyAssignments } from "@/lib/api";
import { requireSession } from "@/lib/session";

export const metadata = { title: "Assignments" };

/**
 * The invitee's side of event staffing (D-319).
 *
 * `GET /v1/me/assignments`, `accept` and `decline` shipped with D-064 and had no caller on any client
 * for the whole time since: an organiser could invite someone, but the invitee had no screen, and the
 * API lets nobody else answer on their behalf. The §14.2 go-live gate requires an accepted assignment,
 * so the missing screen is what kept events out of Live.
 */
export default async function AssignmentsPage() {
  const session = await requireSession();
  const assignments = await listMyAssignments(session.accessToken).catch(() => []);

  const pending = assignments.filter((a) => a.status === "invited");
  const accepted = assignments.filter((a) => a.status !== "invited");

  return (
    <div className="space-y-8">
      <div>
        <h1 className="text-3xl font-semibold">Assignments</h1>
        <p className="mt-2 text-sm text-muted">
          Roles you&rsquo;ve been invited to take on at an event. Accepting adds you to the event team and its chat.
        </p>
      </div>

      {assignments.length === 0 ? (
        <EmptyState
          icon={<ClipboardCheck aria-hidden />}
          title="No assignments yet"
          message="When an organizer adds you to an event team, the invitation lands here for you to accept or decline."
        />
      ) : (
        <>
          {pending.length > 0 ? (
            <section className="space-y-3">
              <h2 className="text-lg font-medium">Waiting on you</h2>
              <ul className="space-y-3">
                {pending.map((a) => <AssignmentRow key={a.id} assignment={a} />)}
              </ul>
            </section>
          ) : null}

          {accepted.length > 0 ? (
            <section className="space-y-3">
              <h2 className="text-lg font-medium">Accepted</h2>
              <ul className="space-y-3">
                {accepted.map((a) => <AssignmentRow key={a.id} assignment={a} />)}
              </ul>
            </section>
          ) : null}
        </>
      )}
    </div>
  );
}
