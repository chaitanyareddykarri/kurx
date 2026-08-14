import { CheckCircle2, Clock, Hourglass, XCircle } from "lucide-react";
import { EmptyState } from "@kurx/ui";
import { listEventRegistrations, type EventRegistration } from "@/lib/api";
import { requireEventOrg } from "@/lib/event-org";

export const metadata = { title: "Registrations" };

/// Registrations for one event.
///
/// Deliberately separate from Attendees: an attendee is an *issued admission*, a registration is the
/// act that may still be pending, waitlisted or cancelled. Collapsing them would hide exactly the
/// rows an organiser running manual approval needs to see.
///
/// Read-only for now — the backend exposes the list but no approve/reject transition on a
/// registration, so a button here would have nothing to call. Mirrors the Flutter page.
export default async function RegistrationsPage({ params }: { params: { id: string } }) {
  const { session, orgId } = await requireEventOrg(params.id);

  const registrations = await listEventRegistrations(session.accessToken, orgId, params.id).catch(
    () => [] as EventRegistration[]
  );

  return (
    <div className="mx-auto max-w-3xl">
      <h1 className="text-2xl font-semibold text-text">Registrations</h1>
      <p className="mt-1 text-sm text-muted">
        Every registration, including those still pending or waitlisted. Attendees lists issued
        admissions only.
      </p>

      {registrations.length === 0 ? (
        <div className="mt-6">
          <EmptyState
            icon="how_to_reg"
            title="No registrations yet"
            message="Registrations for this event appear here."
          />
        </div>
      ) : (
        <ul className="mt-4 space-y-2">
          {registrations.map((r) => {
            const { Icon, tone } = statusOf(r.state);
            return (
              <li
                key={r.id}
                className="flex items-center gap-3 rounded-lg border border-border bg-surface p-3"
              >
                <Icon size={18} className={tone} />
                <div className="min-w-0 flex-1">
                  <p className="text-sm font-medium text-text">
                    {r.subject_type === "Team" ? "Team registration" : "Individual"}
                  </p>
                  <p className="mt-0.5 text-xs text-muted">
                    {[
                      r.state,
                      `${r.admission_count} ${r.admission_count === 1 ? "admission" : "admissions"}`,
                      r.created_at ? new Date(r.created_at).toLocaleDateString() : null
                    ]
                      .filter(Boolean)
                      .join(" · ")}
                  </p>
                </div>
              </li>
            );
          })}
        </ul>
      )}
    </div>
  );
}

function statusOf(state: string) {
  switch (state.toLowerCase()) {
    case "confirmed":
      return { Icon: CheckCircle2, tone: "text-accent-text" };
    case "cancelled":
      return { Icon: XCircle, tone: "text-danger" };
    case "waitlisted":
      return { Icon: Hourglass, tone: "text-accent-text" };
    default:
      return { Icon: Clock, tone: "text-muted" };
  }
}
