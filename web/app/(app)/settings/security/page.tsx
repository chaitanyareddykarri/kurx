import { Card } from "@/components/ui/card";
import { SecurityCenter } from "@/components/auth/security-center";
import { requireSession } from "@/lib/session";

export const metadata = { title: "Security · Kurx" };

/**
 * Security Center (Phase 2E). The access token is passed to the client component because every one of
 * these endpoints is user-scoped and read live per request — nothing here is cached or derived from a
 * claim in the token.
 */
export default async function SecurityPage() {
  const session = await requireSession();

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-3xl font-semibold">Security</h1>
        <p className="text-sm text-muted">
          Manage how you sign in, your trusted devices and browsers, and where you&apos;re signed in.
        </p>
      </div>

      <Card>
        <SecurityCenter accessToken={session.accessToken} />
      </Card>
    </div>
  );
}
