import { ShieldAlert } from "lucide-react";
import { SignOutButton } from "@/app/forbidden/sign-out-button";

// Reached when a signed-in account has no platform role (not Kurx staff).
export default function ForbiddenPage() {
  return (
    <div className="grid min-h-screen place-items-center bg-background p-4">
      <div className="w-full max-w-sm rounded-lg border border-border bg-surface p-6 text-center shadow-github">
        <ShieldAlert className="mx-auto text-danger" size={40} />
        <h1 className="mt-3 text-lg font-semibold text-text">No admin access</h1>
        <p className="mt-1 text-sm text-muted">
          This account isn&apos;t provisioned for the Kurx admin console. Ask a Super Admin to grant a platform role.
        </p>
        <div className="mt-4">
          <SignOutButton />
        </div>
      </div>
    </div>
  );
}
