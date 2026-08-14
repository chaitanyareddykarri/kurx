import { Building2, FileText, ShieldCheck, UserCheck } from "lucide-react";
import { Badge, Card, EmptyState, SectionHeader } from "@kurx/ui";
import { requireStaffSession } from "@/lib/session";
import { PageHeader } from "@/components/layout/page-header";
import {
  listPendingOrgVerifications, listPendingMembershipClaims, getVerificationHistory,
  apiErrorMessage, apiErrorStatus,
  getVerificationDocViewUrl
} from "@/lib/api";
import { ReviewForm } from "@/components/admin/review-form";

// Verification queue (M5/M6/M12) — org verifications + membership claims in one place. Gated by the
// backend's VerificationReviewer role (non-reviewers get a 403, surfaced below). The reviewer confirms
// each affiliation out of band (call/email), then records the decision.
export default async function VerificationPage() {
  const session = await requireStaffSession();

  try {
    const [orgs, claims] = await Promise.all([
      listPendingOrgVerifications(session.accessToken),
      listPendingMembershipClaims(session.accessToken)
    ]);
    // Evidence docs per org (best-effort — a failed history load just hides that org's links).
    const histories = await Promise.all(
      orgs.map((o) => getVerificationHistory(session.accessToken, "organization", o.org_id).catch(() => null))
    );
    const docsByOrg = new Map(orgs.map((o, i) => [o.org_id, histories[i]?.documents ?? []]));

    // Each document opened at `/verification/doc/{id}` — a route that does not exist, so every one
    // of these was a 404 on a screen whose whole purpose is reading the documents. The real target
    // is the signed URL from `GET /v1/admin/verification-documents/{id}/view`, resolved here rather
    // than in the browser because the call needs the staff token.
    //
    // D-235: a document whose URL will not resolve is rendered as unlinked text, never as a link
    // that goes nowhere — on a verification queue, a reviewer who clicks and lands on an error page
    // learns nothing about whether the document exists.
    const allDocs = [...docsByOrg.values()].flat();
    const resolved = await Promise.all(
      allDocs.map((d) => getVerificationDocViewUrl(session.accessToken, d.id).catch(() => null))
    );
    const docUrl = new Map(allDocs.map((d, i) => [d.id, resolved[i]]));

    return (
      <div className="space-y-8">
        <PageHeader
          kicker="Trust & Safety"
          title="Verification queue"
          description="Confirm each affiliation out of band (call/email the institution), then record the decision."
        />

        <section className="space-y-3">
          <SectionHeader title="Organizations" subtitle={`${orgs.length} awaiting review`} />
          {orgs.length === 0 ? (
            <Card>
              <EmptyState icon={<ShieldCheck size={22} />} title="No organizations awaiting review" message="New submissions will appear here." />
            </Card>
          ) : (
            <div className="space-y-3">
              {orgs.map((o) => (
                <Card key={o.org_id}>
                  <div className="flex items-start gap-3">
                    <span className="mt-0.5 grid h-9 w-9 shrink-0 place-items-center rounded-md border border-border bg-elevated text-muted">
                      <Building2 size={16} />
                    </span>
                    <div className="min-w-0 flex-1">
                      <h3 className="font-semibold text-text">{o.name}</h3>
                      <div className="mt-1 flex flex-wrap items-center gap-1.5 text-xs text-muted">
                        <Badge tone="neutral">{o.type}</Badge>
                        {o.primary_domain ? <span>{o.primary_domain}</span> : null}
                        <span>· {o.document_count} document{o.document_count === 1 ? "" : "s"}</span>
                        <span>· submitted {new Date(o.submitted_at).toLocaleDateString("en-IN")}</span>
                      </div>
                      {(docsByOrg.get(o.org_id) ?? []).length > 0 ? (
                        <div className="mt-3 flex flex-wrap gap-2">
                          {(docsByOrg.get(o.org_id) ?? []).map((d) => (
                            docUrl.get(d.id) ? (
                            <a
                              key={d.id}
                              href={docUrl.get(d.id) as string}
                              target="_blank"
                              rel="noopener noreferrer"
                              className="inline-flex items-center gap-1.5 rounded-md border border-border px-2 py-1 text-xs font-medium text-accent-text hover:bg-elevated"
                            >
                              <FileText size={12} />
                              View {d.doc_type.replace(/_/g, " ")}
                            </a>
                          ) : (
                            <span
                              key={d.id}
                              className="inline-flex items-center gap-1.5 rounded-md border border-dashed border-border px-2 py-1 text-xs font-medium text-muted"
                              title="This document could not be opened. Refresh before deciding."
                            >
                              <FileText size={12} />
                              {d.doc_type} — unavailable
                            </span>
                            )
                          ))}
                        </div>
                      ) : null}
                      <ReviewForm kind="org" id={o.org_id} />
                    </div>
                  </div>
                </Card>
              ))}
            </div>
          )}
        </section>

        <section className="space-y-3">
          <SectionHeader title="Membership claims" subtitle={`${claims.length} awaiting review`} />
          {claims.length === 0 ? (
            <Card>
              <EmptyState icon={<UserCheck size={22} />} title="No membership claims awaiting review" message="New claims will appear here." />
            </Card>
          ) : (
            <div className="space-y-3">
              {claims.map((c) => (
                <Card key={c.id}>
                  <h3 className="font-semibold text-text">
                    {c.user_name}{c.username ? ` (@${c.username})` : ""}
                  </h3>
                  <p className="mt-1 text-xs text-muted">
                    Claims <span className="text-text">{c.claimed_role}</span> at{" "}
                    <span className="text-text">{c.org_name}</span>
                    {c.fast_track ? <Badge tone="accent">fast-track</Badge> : null}
                    {" "}· submitted {new Date(c.created_at).toLocaleDateString("en-IN")}
                  </p>
                  <ReviewForm kind="claim" id={c.id} />
                </Card>
              ))}
            </div>
          )}
        </section>
      </div>
    );
  } catch (err) {
    const forbidden = apiErrorStatus(err) === 403;
    return (
      <div className="space-y-4">
        <h1 className="text-2xl font-semibold text-text">Verification queue</h1>
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
