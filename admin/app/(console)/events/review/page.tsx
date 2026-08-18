import { CalendarCheck, CalendarClock, ExternalLink, History } from "lucide-react";
import { Card, EmptyState } from "@kurx/ui";
import { requireStaffSession } from "@/lib/session";
import { PageHeader } from "@/components/layout/page-header";
import {
  listAdminEvents, getReviewCounts, getReviewHistory, getReviewChecklist, getEventAuthorization,
  getEventForReview, getEventTicketTypesForReview, apiErrorMessage, apiErrorStatus,
} from "@/lib/api";
import { ReviewActions } from "@/components/admin/review-actions";
import { ReviewChecklistPanel } from "@/components/admin/review-checklist";
import { AuthorizationPanel } from "@/components/admin/authorization-panel";
import { FinancialReviewPanel } from "@/components/admin/financial-review-panel";
import { ReviewDossier } from "@/components/admin/review-dossier";

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

    /*
     * The event itself — the thing being reviewed.
     *
     * `listAdminEvents` returns a 29-field LIST row with no description, no rules, no terms, no consent
     * text, no eligibility and no registration windows. Every one of those is collected by the Create
     * Event wizard and stored, and none of them reached a reviewer: the card carried three fields and
     * deep-linked to the event workspace, whose Overview tab renders the same list row.
     *
     * `GET /v1/events/{id}` already returns all of it and `CanViewAsync` already admits both
     * `kurx_admin` and `VerificationReviewer` to an event in any status (D-191) — so this is a missing
     * mapping in the console, not a missing capability in the API. Same swallow-to-null as the panels
     * above: a queue that renders without one dossier beats a 500 over the whole page.
     */
    const dossiers = await Promise.all(
      list.items.map((e) => getEventForReview(session.accessToken, e.event_id).catch(() => null)),
    );
    // D-372 — the registration options and the UNIT their prices are charged in, so "₹2,000" is never
    // shown to a reviewer without saying whether it buys a team or a seat.
    const ticketSets = await Promise.all(
      list.items.map((e) => getEventTicketTypesForReview(session.accessToken, e.event_id).catch(() => [])),
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
                      {/* D-381 — a legacy self-representation row is NAMED as legacy, never printed as
                          if it were an institution: that would show a reviewer a person's name under
                          "representing organization", the one thing they must not be shown. */}
                      {e.org_is_personal ? "Legacy personal representation" : e.org_name} · starts{" "}
                      {new Date(e.starts_at).toLocaleDateString("en-IN")}
                    </p>
                    {/*
                      The ORGANIZATION's own registry standing, beside the event awaiting a decision.

                      An event can now reach this queue while the institution behind it is still
                      PendingReview — a representation registered during event creation stages the
                      organization and lets the draft proceed, because creation and publication are
                      different gates. Approving such an event is legitimate and does NOT make it
                      publishable: `pending_org_verification` still refuses the transition until the
                      organization is verified. Without this line a reviewer approves, the organiser
                      cannot publish, and neither of them can see why.
                    */}
                    {e.org_verification !== "Verified" ? (
                      <p className="mt-1 inline-flex items-center gap-1 rounded border border-warning/40 bg-warning/5 px-1.5 py-0.5 text-[10px] font-semibold text-warning">
                        Organization: {e.org_verification} — this event can be approved, but it can&apos;t
                        publish until the organization is verified
                      </p>
                    ) : null}

                    {/* A reviewer must never be asked to approve an event they cannot inspect. This used
                        to deep-link to the 9-tab event workspace INSTEAD of showing the event — but that
                        workspace's Overview tab renders the same 29-field list row this card came from,
                        so the description and every D-265 field group appeared on neither screen. The
                        dossier below is that content; the link stays for the operational tabs
                        (registrations, tickets, finance, moderation, media, timeline) it genuinely owns.
                        `tab=all` is deliberate: the workspace only opens an event present in the current
                        tab's result set, and `all` is the one tab with no status filter. */}
                    <a href={`/events?tab=all&event=${e.event_id}`}
                      className="mt-2 inline-flex h-8 items-center gap-1 rounded-md border border-border px-3 text-xs font-semibold text-text hover:bg-elevated">
                      <ExternalLink size={12} /> Registrations, finance & moderation
                    </a>

                    {dossiers[i] ? (
                      <ReviewDossier event={dossiers[i]!} tickets={ticketSets[i]} />
                    ) : (
                      // Named rather than blank: a reviewer must know they are looking at less than the
                      // whole event, not silently approve on a card.
                      <p className="mt-3 rounded-md border border-warning/40 bg-warning/5 p-2 text-xs text-warning">
                        This event&apos;s details could not be loaded. Do not approve without opening it.
                      </p>
                    )}

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

                    {/*
                      D-382 — the authorization is EVIDENCE, and evidence is readable before the claim.

                      This whole block was gated on `underreview`, which put the event's details in the
                      queue and the one document the decision turns on behind a claim: deciding whether
                      to pick an item up meant deciding blind, and finding out whether an authorization
                      had even been filed meant taking the event into your own name first. The panel
                      renders for every row now, `readOnly` until the item is held — reading is not
                      deciding, and the verdict buttons still require the claim.

                      D-266 M7 stands for the two panels below it: a checklist is work-in-progress, not
                      evidence, and there is nothing to tick on an event nobody is working.
                    */}
                    <AuthorizationPanel
                      eventId={e.event_id}
                      authorization={authorizations[i]}
                      readOnly={e.status.toLowerCase() !== "underreview"}
                    />
                    {e.status.toLowerCase() === "underreview" ? (
                      <>
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
