import { Card } from "@kurx/ui";
import { requireStaffSession } from "@/lib/session";
import { PageHeader } from "@/components/layout/page-header";
import { listBlacklist, apiErrorMessage, apiErrorStatus } from "@/lib/api";
import { BlacklistManager } from "@/components/admin/blacklist-manager";

// Fraud blocklist (M13, D-052) — VerificationReviewer gated. The trust layer reads it live, so a block
// takes effect on the subject's next request (they lose paid-organizing capability).
export default async function BlacklistPage() {
  const session = await requireStaffSession();

  try {
    const entries = await listBlacklist(session.accessToken);
    return (
      <div className="space-y-6">
        <PageHeader
          kicker="Trust & Safety"
          title="Blacklist"
          description="Hard-block a phone, email, organization name, device, or document hash. Blocked subjects immediately lose paid-organizing capability."
        />
        <BlacklistManager entries={entries} />
      </div>
    );
  } catch (err) {
    const forbidden = apiErrorStatus(err) === 403;
    return (
      <div className="space-y-4">
        <h1 className="text-2xl font-semibold text-text">Blacklist</h1>
        <Card>
          {forbidden ? (
            <p className="text-sm text-muted">You don&apos;t have Verification Reviewer access.</p>
          ) : (
            <p className="text-sm text-muted">
              Couldn&apos;t load the blocklist: <span className="text-text">{apiErrorMessage(err)}</span>
            </p>
          )}
        </Card>
      </div>
    );
  }
}
