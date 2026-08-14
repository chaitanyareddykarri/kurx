import { Card } from "@kurx/ui";
import { CreateOrgForm } from "@/components/host/create-org-form";

// Request to represent a not-yet-registered organization (event-first, D-074/D-075). Submitting stages a
// hidden org an admin verifies; the caller becomes a Verified Representative on approval — never an owner,
// because organizations have no account.
export default function RequestRepresentationPage() {
  return (
    <div className="space-y-4">
      <div>
        <h1 className="text-3xl font-semibold">Request to represent an organization</h1>
        <p className="mt-1 text-sm text-muted">
          A college, company, club, or community not yet on Kurx. This submits a representation request an admin
          verifies before it joins the registry — you become a Verified Representative on approval.
        </p>
      </div>
      <Card>
        <CreateOrgForm />
      </Card>
    </div>
  );
}
