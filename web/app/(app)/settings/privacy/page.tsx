import { Card } from "@/components/ui/card";
import { requireSession } from "@/lib/session";
import { PrivacyForm } from "@/components/profile/privacy-form";

export const metadata = { title: "Privacy · Kurx" };

/**
 * Privacy — who may see each section of the profile.
 *
 * This page previously rendered **blocked accounts** under the name "Privacy", while these controls
 * lived unlabelled at the bottom of the settings index, duplicated by the profile editor above them.
 * Both halves of that mix-up are now fixed: blocking is `/settings/blocked`, and visibility lives
 * here, once.
 */
export default async function PrivacySettingsPage() {
  const session = await requireSession();

  return (
    <div className="space-y-lg">
      <div>
        <h1 className="text-2xl font-semibold text-text">Privacy</h1>
        <p className="mt-1 text-sm text-muted">
          Controls what appears on your public profile. Everything shown there is built from verified
          activity — these switches decide which parts are visible, never what they say.
        </p>
      </div>
      <Card>
        <PrivacyForm
          initial={session.me.privacy ?? {
            profile_public: true,
            show_attended: false,
            show_certificates: true,
            show_allies: true,
            sections: null
          }}
        />
      </Card>
    </div>
  );
}
