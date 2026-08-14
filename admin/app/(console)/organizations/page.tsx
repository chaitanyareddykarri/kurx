import { Card } from "@kurx/ui";
import { requireStaffSession } from "@/lib/session";
import { hasRole } from "@/lib/roles";
import { PageHeader } from "@/components/layout/page-header";
import { OrgFilterBar } from "@/components/admin/org-workspace/org-filter-bar";
import { OrgWorkspace } from "@/components/admin/org-workspace/org-workspace";
import {
  apiErrorMessage,
  apiErrorStatus,
  getAdminOrgDetail,
  getFraudScore,
  getOrgWalletAsAdmin,
  listAdminOrgs,
  listOrgEventsAsAdmin,
  type AdminOrgDetail,
  type OrgEventRow,
  type WalletAdmin
} from "@/lib/api";

type SearchParams = { q?: string; status?: string; type?: string; page?: string; org?: string };

// D-194: platform-wide org list & detail — closes the standing gap in admin/STATUS.md §5.4 ("Organization
// list & detail — BLOCKED, no admin-scoped org read"). The detail Sheet composes already-built admin reads
// (D-186's listOrgEventsAsAdmin/getOrgWalletAsAdmin/getFraudScore, previously only reachable from the event
// workspace's Organizer tab) rather than a second data path.
export default async function OrganizationsPage({ searchParams }: { searchParams: SearchParams }) {
  const session = await requireStaffSession();
  const canModerate = hasRole(session.roles, ["VerificationReviewer"]);
  const page = Math.max(Number(searchParams.page) || 1, 1);

  try {
    const { items: orgs, total } = await listAdminOrgs(session.accessToken, {
      q: searchParams.q, status: searchParams.status, type: searchParams.type, page, limit: 50
    });

    const openOrg = searchParams.org ? orgs.find((o) => o.org_id === searchParams.org) ?? null : null;
    let detail: AdminOrgDetail | null = null;
    let detailError: string | null = null;
    let wallet: WalletAdmin | null = null;
    let riskScore: number | null = null;
    let events: OrgEventRow[] = [];

    if (openOrg) {
      const [detailR, walletR, riskR, eventsR] = await Promise.allSettled([
        getAdminOrgDetail(session.accessToken, openOrg.org_id),
        getOrgWalletAsAdmin(session.accessToken, openOrg.org_id),
        getFraudScore(session.accessToken, "Organization", openOrg.org_id),
        listOrgEventsAsAdmin(session.accessToken, openOrg.org_id)
      ]);
      if (detailR.status === "fulfilled") detail = detailR.value; else detailError = apiErrorMessage(detailR.reason);
      if (walletR.status === "fulfilled") wallet = walletR.value;
      if (riskR.status === "fulfilled") riskScore = riskR.value.risk_score;
      if (eventsR.status === "fulfilled") events = eventsR.value.items;
    }

    return (
      <div className="space-y-6">
        <PageHeader
          kicker="People & Orgs"
          title="Organizations"
          description="Every organization on the platform — search, open one to see its wallet, risk score, and events, and moderate it directly."
        />
        <OrgFilterBar values={{ q: searchParams.q, status: searchParams.status, type: searchParams.type }} />
        <OrgWorkspace
          orgs={orgs}
          total={total}
          page={page}
          openOrg={openOrg}
          detail={detail}
          detailError={detailError}
          wallet={wallet}
          riskScore={riskScore}
          events={events}
          canModerate={canModerate}
        />
      </div>
    );
  } catch (err) {
    const forbidden = apiErrorStatus(err) === 403;
    return (
      <div className="space-y-4">
        <PageHeader kicker="People & Orgs" title="Organizations" description="" />
        <Card>
          {forbidden ? (
            <p className="text-sm text-muted">You don&apos;t have Verification Reviewer access.</p>
          ) : (
            <p className="text-sm text-muted">
              Couldn&apos;t load organizations: <span className="text-text">{apiErrorMessage(err)}</span>
            </p>
          )}
        </Card>
      </div>
    );
  }
}
