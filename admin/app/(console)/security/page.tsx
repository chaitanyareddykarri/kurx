import { requireStaffSession } from "@/lib/session";
import { PageHeader } from "@/components/layout/page-header";
import {
  listDevices,
  listSessions,
  listTrustedBrowsers,
  recoveryCodesRemaining,
  securityActivity,
  securityOverview,
  section,
} from "@/lib/api";
import { StaffSecurityCenter } from "@/components/auth/security-center";

export const metadata = { title: "Security · Kurx admin" };

/**
 * Staff Security Center (Phase 2E). Reads run here server-side (the token never reaches the client);
 * the client component performs writes through server actions and re-fetches via router.refresh().
 */
export default async function SecurityPage() {
  const session = await requireStaffSession();
  const t = session.accessToken;
  /*
   * Sessions, devices and trusted browsers are load-bearing here in a way the rest of this page is
   * not: an empty list on a security screen reads as "nothing else is signed in as you", which is a
   * statement about the account's safety. Falling back to `[]` made an outage indistinguishable from
   * that, on the one screen an operator opens *because* they suspect something (D-235).
   *
   * The others degrade honestly — a missing overview or activity feed shows less, but claims nothing.
   */
  const [overview, sessionsR, devicesR, browsersR, recoveryRemaining, activity] = await Promise.all([
    securityOverview(t).catch(() => null),
    section(listSessions(t)),
    section(listDevices(t)),
    section(listTrustedBrowsers(t)),
    recoveryCodesRemaining(t).catch(() => null),
    securityActivity(t, 25).catch(() => [])
  ]);

  const inventoryFailed =
    sessionsR.state !== "ok" || devicesR.state !== "ok" || browsersR.state !== "ok";
  const sessions = sessionsR.state === "ok" ? sessionsR.data : [];
  const devices = devicesR.state === "ok" ? devicesR.data : [];
  const browsers = browsersR.state === "ok" ? browsersR.data : [];

  return (
    <div className="mx-auto max-w-3xl space-y-6">
      <PageHeader kicker="Account" title="Security" description="Your staff account's sign-in factors, sessions, and recent activity." />
      {inventoryFailed ? (
        <p role="status" className="rounded-lg border border-dashed border-warning/50 bg-warning/10 p-4 text-sm text-text">
          Your sessions, devices or trusted browsers couldn&apos;t be loaded. What is listed below may
          be incomplete — this is <strong>not</strong> confirmation that nothing else is signed in.
          Refresh before acting on it.
        </p>
      ) : null}
      <StaffSecurityCenter
        overview={overview}
        sessions={sessions}
        devices={devices}
        browsers={browsers}
        recoveryRemaining={recoveryRemaining}
        activity={activity}
      />
    </div>
  );
}
