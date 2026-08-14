import { redirect } from "next/navigation";
import { requireStaffSession } from "@/lib/session";
import { PageHeader } from "@/components/layout/page-header";
import { BroadcastForm } from "@/components/admin/broadcast-form";

// Platform-wide announcement (KurxAdmin). The backend fans out to every user synchronously on the
// request thread, so this is slow on a large user base and cannot be recalled once sent — hence the
// typed confirmation rather than a plain submit.
export default async function BroadcastPage() {
  const session = await requireStaffSession();
  // SuperAdmin only (backend policy is KurxAdmin). Same bounce as staff/page.tsx — no reason to show
  // anyone else a working-looking form that fans out to every user.
  if (!session.roles.includes("SuperAdmin")) redirect("/");

  return (
    <div className="space-y-6">
      <PageHeader
        kicker="System"
        title="Broadcast"
        description="Send an in-app notification to every registered user. There is no recall and no audience filter — the backend has no targeting parameters."
      />

      <BroadcastForm />
    </div>
  );
}
