import { CalendarCheck, CalendarClock, ExternalLink, History } from "lucide-react";
import { Card, EmptyState } from "@kurx/ui";
import { requireStaffSession } from "@/lib/session";
import { PageHeader } from "@/components/layout/page-header";
import {
  listAdminEvents, getReviewCounts, getReviewHistory, getReviewChecklist, getEventAuthorization,
  apiErrorMessage, apiErrorStatus,
} from "@/lib/api";
import { ReviewActions } from "@/components/admin/review-actions";
import { ReviewChecklistPanel } from "@/components/admin/review-checklist";
import { AuthorizationPanel } from "@/components/admin/authorization-panel";
import { FinancialReviewPanel } from "@/components/admin/financial-review-panel";

// D-266 M4 — the review console. Queue counts come from the backend
// (GET /v1/admin/events/review-counts); they are never derived here, so the tabs cannot disagree with the
// list they head. Every action routes through the org-scoped transition endpoint via ReviewActions —
// there is no admin-specific workflow route, and no workflow logic in this file.

const TABS = [
  { key: "PendingReview", label: "Pending review" },
  { key: "UnderReview", label: "Under review" },
  { key: "ChangesRequested", label: "Changes requested" },
  { key: "Approved", label: "Approved" },
  { key: "Rejected", label: "Rejected" },
] as const;

type TabKey = (typeof TABS)[number]["key"];

function countFor(counts: Awaited<ReturnType<typeof getReviewCounts>>, key: TabKey): number {
  switch (key) {
    case "PendingReview": return counts.pending_review;
    case "UnderReview": return counts.under_review;
    case "ChangesRequested": return counts.changes_requested;
    case "Approved": return counts.approved;
    case "Rejected": return counts.rejected;
  }
}

export default async function EventReviewConsolePage({
  searchParams,
}: { searchParams?: { status?: string } }) {
  const session = await requireStaffSession();
  const active: TabKey =
    (TABS.find((t) => t.key === searchParams?.status)?.key ?? "PendingReview");

  try {
    const [counts, list] = await Promise.all([
      getReviewCounts(session.accessToken),
      listAdminEvents(session.accessToken, { status: active, limit: 50 }),
    ]);

    // History is fetched per event so a reviewer sees prior decisions without opening a second page.
    // Reads GET /v1/admin/events/{id}/review-history — the VerificationReview store, not a local copy.
    const histories = await Promise.all(
      list.items.map((e) =>
        getReviewHistory(session.accessToken, e.event_id).catch(() => []),
      ),
    );

    // D-266 M7: the checklist that gates Approve, and the institutional authorization it usually turns on.
    // Both are fetched per row so a reviewer works one screen rather than clicking through. Failures are
    // swallowed to null/empty deliberately — a queue that renders is more useful than one that 500s
    // because a single event's side-panel could not load.
    const checklists = await Promise.all(
      list.items.map((e) => getReviewChecklist(session.accessToken, e.event_id).catch(() => null)),
    );
    const authorizations = await Promise.all(
      list.items.map((e) => getEventAuthorization(session.accessToken, e.event_id).catch(() => null)),
    );

    return (
      <div className="space-y-6">
        <PageHeader
          kicker="Trust & Safety"
          title="Event review"
          description="Public events pass through review before they can publish. Claim an item to work on it, then approve, request changes, or reject with a reason."
        />

        <div className="flex flex-wrap gap-2">
          {TABS.map((t) => (
            <a key={t.key} href={`/events/review?status=${t.key}`}
              className={`h-8 rounded-md border px-3 text-xs font-semibold leading-8 ${
                t.key === active
                  ? "border-accent/40 bg-accent/10 text-accent-text"
                  : "border-border text-muted hover:bg-elevated"}`}>
              {t.label} ({countFor(counts, t.key)})
            </a>
          ))}
          {counts.legacy_in_review > 0 ? (
            // Pre-M4 events still sitting in the retired state. Surfaced rather than hidden so they are
            // not silently stranded while the legacy status is being retired.
            <a href="/events/pending"
              className="h-8 rounded-md border border-warning/40 px-3 text-xs font-semibold leading-8 text-warning hover:bg-warning/10">
              Legacy queue ({counts.legacy_in_review})
            </a>
          ) : null}
        </div>

        {list.items.length === 0 ? (
          <Card>
            <EmptyState icon={<CalendarCheck size={22} />} title={`Nothing in ${TABS.find((t) => t.key === active)!.label.toLowerCase()}`}
              message="Events appear here as organisers submit them and reviewers work through the queue." />
          </Card>
        ) : (
          <div className="space-y-3">
            {list.items.map((e, i) => (
              <Card key={e.event_id}>
                <div className="flex items-start gap-3">
                  <span className="mt-0.5 grid h-9 w-9 shrink-0 place-items-center rounded-md border border-border bg-elevated text-muted">
                    <CalendarClock size={16} />
                  </span>
                  <div className="min-w-0 flex-1">
                    <div className="flex flex-wrap items-center gap-2">
                      <h3 className="font-semibold text-text">{e.title}</h3>
                      <span className="rounded border border-border px-1.5 py-0.5 text-[10px] font-semibold text-muted">
                        {e.status}
                      </span>
                    </div>
                    <p className="mt-1 text-xs text-muted">
                      {e.org_name} · starts {new Date(e.starts_at).toLocaleDateString("en-IN")}
                    </p>

                    {/* A reviewer must never be asked to approve an event they cannot inspect. The queue
                        card carries three fields; everything a review is actually ABOUT — description,
                        venue, schedule, ticket types, pricing, media, organizer — lives in the existing
                        9-tab admin event workspace, so this deep-links there rather than duplicating it.
                        `tab=all` is deliberate: the workspace only opens an event present in the current
                        tab's result set, and `all` is the one tab with no status filter, so it works for
                        every review state. */}
                    <a href={`/events?tab=all&event=${e.event_id}`}
                      className="mt-2 inline-flex h-8 items-center gap-1 rounded-md border border-border px-3 text-xs font-semibold text-text hover:bg-elevated">
                      <ExternalLink size={12} /> Open event
                    </a>

                    {histories[i].length > 0 ? (
                      <details className="mt-2">
                        <summary className="flex cursor-pointer items-center gap-1 text-xs text-muted">
                          <History size={12} /> Review history ({histories[i].length})
                        </summary>
                        <ul className="mt-2 space-y-1.5 border-l border-border pl-3">
                          {histories[i].map((h) => (
                            <li key={h.id} className="text-xs">
                              <span className="font-semibold text-text">{h.decision}</span>
                              {h.reason_code ? <span className="text-muted"> · {h.reason_code}</span> : null}
                              <span className="text-muted">
                                {" "}· {h.reviewer_name ?? "unknown reviewer"} ·{" "}
                                {new Date(h.created_at).toLocaleDateString("en-IN")}
                              </span>
                              {h.notes ? <p className="mt-0.5 text-muted">{h.notes}</p> : null}
                            </li>
                          ))}
                        </ul>
                      </details>
                    ) : null}

                    {/* D-266 M7 — shown only while a reviewer actually holds the item. Before a claim
                        there is nothing to work through, and after a decision the ticks are history. */}
                    {e.status.toLowerCase() === "underreview" ? (
                      <>
                        <AuthorizationPanel eventId={e.event_id} authorization={authorizations[i]} />
                        {/* Shown only when the POLICY ENGINE says this event needs it — never inferred
                            here from "is it paid", which would be the console re-deriving a server rule
                            and getting it wrong (a concert takes more money and needs no such review). */}
                        {checklists[i]?.items.some((it) => it.key === "financial_review_required") ? (
                          <FinancialReviewPanel eventId={e.event_id} />
                        ) : null}
                        {checklists[i] ? (
                          <ReviewChecklistPanel eventId={e.event_id} initial={checklists[i]!} />
                        ) : null}
                      </>
                    ) : null}

                    <ReviewActions orgId={e.representing_org_id} eventId={e.event_id} status={e.status} />
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
        <h1 className="text-2xl font-semibold text-text">Event review</h1>
        <Card>
          {forbidden ? (
            <p className="text-sm text-muted">You don&apos;t have Verification Reviewer access.</p>
          ) : (
            <p className="text-sm text-muted">
              Couldn&apos;t load the review queue: <span className="text-text">{apiErrorMessage(err)}</span>
            </p>
          )}
        </Card>
      </div>
    );
  }
}
