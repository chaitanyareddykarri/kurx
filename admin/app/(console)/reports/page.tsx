import { Card } from "@kurx/ui";
import { requireStaffSession } from "@/lib/session";
import { PageHeader } from "@/components/layout/page-header";
import { listReports, apiErrorMessage, apiErrorStatus } from "@/lib/api";
import { ReportsManager } from "@/components/admin/reports-manager";

// Reports & moderation (C-5, D-059) — the open-report triage queue. Gated by the backend's Moderation
// policy (SuperAdmin / VerificationReviewer / Support); non-moderators get a 403, surfaced below.
export default async function ReportsPage() {
  const session = await requireStaffSession();

  try {
    const reports = await listReports(session.accessToken, "open");
    return (
      <div className="space-y-6">
        <PageHeader
          kicker="Trust & Safety"
          title={`Reports & moderation (${reports.length})`}
          description="User-filed reports awaiting triage. Resolve (action taken) or dismiss (no action). Both are final and audit-logged."
        />
        <ReportsManager reports={reports} />
      </div>
    );
  } catch (err) {
    const forbidden = apiErrorStatus(err) === 403;
    return (
      <div className="space-y-4">
        <h1 className="text-2xl font-semibold text-text">Reports &amp; moderation</h1>
        <Card>
          {forbidden ? (
            <p className="text-sm text-muted">You don&apos;t have moderation access.</p>
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
