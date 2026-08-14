import { requireSession } from "@/lib/session";
import { getMyIdentity } from "@/lib/api";
import { IdentitySection } from "@/components/settings/identity-section";

export default async function IdentitySettingsPage() {
  const session = await requireSession();
  const identity = await getMyIdentity(session.accessToken).catch(() => null);

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-semibold">Identity verification</h1>
      <IdentitySection identity={identity} />
    </div>
  );
}
