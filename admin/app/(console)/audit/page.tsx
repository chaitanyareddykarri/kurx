import { Search, ScrollText } from "lucide-react";
import { Button, Card, EmptyState, Input } from "@kurx/ui";
import { requireStaffSession } from "@/lib/session";
import { PageHeader } from "@/components/layout/page-header";
import { listAudit, apiErrorMessage, apiErrorStatus } from "@/lib/api";
import { AuditLogTable } from "@/components/admin/audit-log-table";

// Audit-log viewer (D-062). Read-only over the audit_log the platform already writes (logins, event
// lifecycle, moderation, user suspensions, …). Gated by the backend Audit policy (SuperAdmin / Auditor).
export default async function AuditPage({ searchParams }: { searchParams: { action?: string; entity?: string } }) {
  const session = await requireStaffSession();
  const action = (searchParams.action ?? "").trim();
  const entity = (searchParams.entity ?? "").trim();

  try {
    const rows = await listAudit(session.accessToken, { action: action || undefined, entity: entity || undefined });
    return (
      <div className="space-y-6">
        <PageHeader
          kicker="System"
          title={`Audit log (${rows.length})`}
          description="Every recorded platform action — who, what, when. Newest first."
        />
        <form method="get" className="flex flex-wrap items-center gap-2">
          <div className="relative">
            <Search size={15} className="pointer-events-none absolute left-2.5 top-2.5 text-muted" />
            <Input name="action" defaultValue={action} placeholder="Filter action (e.g. user.ban)" className="w-56 pl-8" />
          </div>
          <Input name="entity" defaultValue={entity} placeholder="Entity (e.g. users)" className="w-40" />
          <Button type="submit" variant="secondary">Filter</Button>
        </form>

        <AuditLogTable
          rows={rows}
          emptyState={<EmptyState icon={<ScrollText size={22} />} title="No matching audit entries" message="Try clearing the action or entity filter." />}
        />
      </div>
    );
  } catch (err) {
    const forbidden = apiErrorStatus(err) === 403;
    return (
      <div className="space-y-4">
        <h1 className="text-2xl font-semibold text-text">Audit log</h1>
        <Card>
          {forbidden ? (
            <p className="text-sm text-muted">You don&apos;t have audit access.</p>
          ) : (
            <p className="text-sm text-muted">Couldn&apos;t load the audit log: <span className="text-text">{apiErrorMessage(err)}</span></p>
          )}
        </Card>
      </div>
    );
  }
}
