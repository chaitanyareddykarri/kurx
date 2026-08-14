import { requireStaffSession } from "@/lib/session";
import { PageHeader } from "@/components/layout/page-header";
import { PasswordManager } from "@/components/auth/password-manager";

export const metadata = { title: "Account · Kurx admin" };

/** Staff account settings. Password management only for now (D-129) — no self-registration here. */
export default async function AccountPage() {
  await requireStaffSession();

  return (
    <div className="mx-auto max-w-lg space-y-6">
      <PageHeader kicker="Account" title="Account" description="Manage the password you use to sign in to the console." />
      <div className="rounded-lg border border-border bg-surface p-6 shadow-github">
        <PasswordManager />
      </div>
    </div>
  );
}
