import { Search } from "lucide-react";
import { Button, Card, Input } from "@kurx/ui";
import { requireStaffSession } from "@/lib/session";
import { PageHeader } from "@/components/layout/page-header";
import { listUsers, apiErrorMessage, apiErrorStatus } from "@/lib/api";
import { UsersManager } from "@/components/admin/users-manager";

// User administration & moderation (D-060). Search users; suspend / ban / reinstate. Gated by the backend
// Moderation policy (SuperAdmin / VerificationReviewer / Support); non-moderators get a 403, surfaced below.
// Empty search shows the currently-moderated accounts rather than the whole user table.
export default async function UsersPage({ searchParams }: { searchParams: { q?: string } }) {
  const session = await requireStaffSession();
  const q = (searchParams.q ?? "").trim();

  try {
    const users = await listUsers(session.accessToken, q || undefined);
    return (
      <div className="space-y-6">
        <PageHeader
          kicker="People & Orgs"
          title="Users"
          description="Suspend or ban abusive accounts — both block login immediately and are reversible. Empty search lists the currently-moderated accounts."
        />
        <form method="get" className="flex max-w-md items-center gap-2">
          <div className="relative flex-1">
            <Search size={15} className="pointer-events-none absolute left-2.5 top-2.5 text-muted" />
            <Input name="q" defaultValue={q} placeholder="Search phone / name / username" className="pl-8" />
          </div>
          <Button type="submit" variant="secondary">Search</Button>
        </form>

        <UsersManager users={users} query={q} />
      </div>
    );
  } catch (err) {
    const forbidden = apiErrorStatus(err) === 403;
    return (
      <div className="space-y-4">
        <h1 className="text-2xl font-semibold text-text">Users</h1>
        <Card>
          {forbidden ? (
            <p className="text-sm text-muted">You don&apos;t have moderation access.</p>
          ) : (
            <p className="text-sm text-muted">
              Couldn&apos;t load users: <span className="text-text">{apiErrorMessage(err)}</span>
            </p>
          )}
        </Card>
      </div>
    );
  }
}
