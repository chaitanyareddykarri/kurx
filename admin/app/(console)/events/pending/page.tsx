import { CalendarCheck, CalendarClock } from "lucide-react";
import { Badge, Card, EmptyState } from "@kurx/ui";
import { requireStaffSession } from "@/lib/session";
import { PageHeader } from "@/components/layout/page-header";
import { listPendingEvents, apiErrorMessage, apiErrorStatus } from "@/lib/api";
import { EventReviewForm } from "@/components/admin/event-review-form";

// Event approval queue (M8, D-057). Paid events can't self-publish — they enter the review queue and a platform
// reviewer approves (publish) or rejects here. The trust gate (org verified + organizer paid-capable) is
// re-checked live by the transition, so a stale queue can't push an unqualified event live.
export default async function EventApprovalPage() {
  const session = await requireStaffSession();

  try {
    const events = await listPendingEvents(session.accessToken);

    return (
      <div className="space-y-6">
        <PageHeader
          kicker="Trust & Safety"
          title={`Event approval (${events.length})`}
          description={
            <>
              Paid events awaiting review. Confirm the organizer&apos;s affiliation is verified (see the{" "}
              <a href="/verification" className="text-accent-text">Verification queue</a>), then approve or reject.
            </>
          }
        />

        {events.length === 0 ? (
          <Card>
            <EmptyState icon={<CalendarCheck size={22} />} title="No events awaiting review" message="Paid events enter this queue when an organizer submits them for approval." />
          </Card>
        ) : (
          <div className="space-y-3">
            {events.map((e) => (
              <Card key={e.event_id}>
                <div className="flex items-start gap-3">
                  <span className="mt-0.5 grid h-9 w-9 shrink-0 place-items-center rounded-md border border-border bg-elevated text-muted">
                    <CalendarClock size={16} />
                  </span>
                  <div className="min-w-0 flex-1">
                    <div className="flex flex-wrap items-center gap-2">
                      <h3 className="font-semibold text-text">{e.title}</h3>
                      {/* D-266 M4 — one reviewer holds an item at a time. Shown here because a rule the
                          reviewer only discovers by being refused at the decision is a worse rule: by then
                          they have already worked the checklist. */}
                      {e.review_claimed_by ? (
                        <Badge tone={e.review_claimed_by === session.me.id ? "success" : "muted"}>
                          {e.review_claimed_by === session.me.id
                            ? "You’re reviewing this"
                            : `Held by ${e.review_claimed_by_name?.trim() || "another reviewer"}`}
                        </Badge>
                      ) : null}
                    </div>
                    <p className="mt-1 text-xs text-muted">
                      {e.org_name} · starts {new Date(e.starts_at).toLocaleDateString("en-IN")} · submitted{" "}
                      {new Date(e.created_at).toLocaleDateString("en-IN")}
                      {e.review_claimed_at
                        ? ` · claimed ${new Date(e.review_claimed_at).toLocaleString("en-IN")}`
                        : null}
                    </p>
                    <EventReviewForm orgId={e.representing_org_id} eventId={e.event_id} />
                  </div>
                </div>
              </Card>
            ))}
          </div>
        )}
      </div>
    );
  } catch (err) {
    const forbidden = apiErrorStatus(err) === 403;
    return (
      <div className="space-y-4">
        <h1 className="text-2xl font-semibold text-text">Event approval</h1>
        <Card>
          {forbidden ? (
            <p className="text-sm text-muted">You don&apos;t have Verification Reviewer access.</p>
          ) : (
            <p className="text-sm text-muted">
              Couldn&apos;t load the queue: <span className="text-text">{apiErrorMessage(err)}</span>
            </p>
          )}
        </Card>
      </div>
    );
  }
}
