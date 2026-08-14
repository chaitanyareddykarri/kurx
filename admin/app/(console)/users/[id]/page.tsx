import Link from "next/link";
import { ArrowLeft, ScrollText } from "lucide-react";
import { Avatar, Card, EmptyState } from "@kurx/ui";
import { requireStaffSession } from "@/lib/session";
import { hasRole } from "@/lib/roles";
import { PageHeader } from "@/components/layout/page-header";
import { listAudit, listUsers, apiErrorMessage, apiErrorStatus, type AuditEntry } from "@/lib/api";
import { UsersManager } from "@/components/admin/users-manager";
import { UserAuditTable } from "@/components/admin/user-audit-table";

// There is no GET /v1/admin/users/{id} — the admin surface exposes a searchable list and the
// moderation actions, nothing more. This page composes the two endpoints that do exist: the list
// filtered to one account, plus that account's audit trail (the audit endpoint filters by actor).
// A user's devices, sessions and passkeys are deliberately absent: those endpoints are self-scoped,
// so no admin-readable version exists yet.
export default async function UserDetailPage({
  params,
  searchParams
}: {
  params: { id: string };
  searchParams: { q?: string };
}) {
  const session = await requireStaffSession();
  // The list endpoint has no by-id lookup, so the caller carries the search term that found the row.
  const q = (searchParams.q ?? "").trim();

  try {
    const users = await listUsers(session.accessToken, q || undefined);
    const user = users.find((u) => u.id === params.id) ?? null;

    // The audit log is a separate policy (SuperAdmin / ReadOnlyAuditor); a moderator without it
    // still gets the account itself rather than a hard error.
    const canAudit = hasRole(session.roles, ["SuperAdmin", "ReadOnlyAuditor"]);
    let audit: AuditEntry[] = [];
    let auditError: string | null = null;
    if (canAudit) {
      try {
        audit = await listAudit(session.accessToken, { actor: params.id });
      } catch (err) {
        auditError = apiErrorMessage(err);
      }
    }

    return (
      <div className="space-y-6">
        <nav aria-label="Breadcrumb">
          <Link
            href={q ? `/users?q=${encodeURIComponent(q)}` : "/users"}
            className="inline-flex items-center gap-1.5 text-sm text-muted hover:text-text"
          >
            <ArrowLeft size={15} /> Users
          </Link>
        </nav>

        <PageHeader
          kicker="People & Orgs"
          title={
            <span className="flex items-center gap-3">
              <Avatar name={user?.name || "User"} size={36} />
              {user?.name || "User"}
            </span>
          }
          description={<span className="font-mono text-xs">{params.id}</span>}
        />

        {user ? (
          <UsersManager users={[user]} query="" linkToDetail={false} />
        ) : (
          <Card>
            <EmptyState
              icon={<ScrollText size={20} />}
              title="Account not in the current result set"
              message={
                q
                  ? `No account with this id matched “${q}”. The admin API has no by-id lookup, so open the user from a search that returns them.`
                  : "Open this user from the Users list so the search term that found them is carried through — the admin API has no by-id lookup."
              }
            />
          </Card>
        )}

        <section className="space-y-2">
          <h2 className="text-sm font-semibold text-text">Audit trail</h2>
          {!canAudit ? (
            <Card>
              <p className="text-sm text-muted">
                Viewing the audit log needs the Super Admin or Auditor role.
              </p>
            </Card>
          ) : auditError ? (
            <Card>
              <p className="text-sm text-muted">
                Couldn&apos;t load the audit trail: <span className="text-text">{auditError}</span>
              </p>
            </Card>
          ) : audit.length === 0 ? (
            <Card>
              <EmptyState
                icon={<ScrollText size={20} />}
                title="No audited actions"
                message="This account has not performed any action that writes to the audit log."
              />
            </Card>
          ) : (
            <UserAuditTable rows={audit} caption="Audit entries where this user was the actor" />
          )}
        </section>
      </div>
    );
  } catch (err) {
    const forbidden = apiErrorStatus(err) === 403;
    return (
      <div className="space-y-4">
        <h1 className="text-2xl font-semibold text-text">User</h1>
        <Card>
          {forbidden ? (
            <p className="text-sm text-muted">You don&apos;t have moderation access.</p>
          ) : (
            <p className="text-sm text-muted">
              Couldn&apos;t load this user: <span className="text-text">{apiErrorMessage(err)}</span>
            </p>
          )}
        </Card>
      </div>
    );
  }
}
