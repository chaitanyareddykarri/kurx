import { requireStaffSession } from "@/lib/session";
import { PageHeader } from "@/components/layout/page-header";
import { FraudSignalForm } from "@/components/admin/fraud-signal-form";

// Risk / fraud signals (M13, D-052) — record a manual risk signal against any subject. There is no list
// endpoint yet (POST-only), so this is record-only; a risk-signals feed + score aggregation is a Tier-C
// follow-up (see admin/ADMIN_AUDIT_AND_ROADMAP.md, T-2). Backend gates recording on VerificationReviewer.
export default async function RiskPage() {
  await requireStaffSession();

  return (
    <div className="space-y-6">
      <PageHeader
        kicker="Trust & Safety"
        title="Risk signals"
        description="A signals feed is a later addition — there is no list endpoint yet, so this is record-only."
      />
      <FraudSignalForm />
    </div>
  );
}
