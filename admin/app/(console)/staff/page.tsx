import { redirect } from "next/navigation";
import { requireStaffSession } from "@/lib/session";
import { PageHeader } from "@/components/layout/page-header";
import { listStaff } from "@/lib/api";
import { StaffManager } from "@/components/staff/staff-manager";

// Staff & role management (D-056). SuperAdmin only — non-SuperAdmin staff are bounced to the dashboard
// (the backend also gates every /v1/admin/staff route on the live SuperAdmin policy).
export default async function StaffPage() {
  const session = await requireStaffSession();
  if (!session.roles.includes("SuperAdmin")) redirect("/");

  const staff = await listStaff(session.accessToken);

  return (
    <div className="space-y-6">
      <PageHeader
        kicker="System"
        title="Staff & Roles"
        description="Grant and revoke platform roles. Authority is read live per request, so a change takes effect on the member's next request. Making someone staff means granting them a role — they must have signed in to Kurx at least once."
      />
      <StaffManager initialStaff={staff} currentUserId={session.me.id} />
    </div>
  );
}
