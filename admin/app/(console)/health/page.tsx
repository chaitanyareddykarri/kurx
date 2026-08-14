import { redirect } from "next/navigation";
import { Activity, CircleAlert, CircleCheck, CircleX } from "lucide-react";
import { Badge, Card, SectionHeader, StatCard } from "@kurx/ui";
import { requireStaffSession } from "@/lib/session";
import { PageHeader } from "@/components/layout/page-header";
import { getHealth, apiErrorMessage, type Health } from "@/lib/api";

// Live backend health (ASP.NET health report). Rendered per request — an aggregate cached for even a
// few seconds would report a dependency as up after it went down, which defeats the point.
export const dynamic = "force-dynamic";

const TONE: Record<string, "success" | "danger" | "muted"> = {
  Healthy: "success",
  Degraded: "muted",
  Unhealthy: "danger"
};

function StatusIcon({ status }: { status: string }) {
  if (status === "Healthy") return <CircleCheck size={18} className="text-success" />;
  if (status === "Unhealthy") return <CircleX size={18} className="text-danger" />;
  return <CircleAlert size={18} className="text-muted" />;
}

export default async function HealthPage() {
  const session = await requireStaffSession();
  // SuperAdmin only, matching the declared nav IA. Same bounce as staff/page.tsx.
  if (!session.roles.includes("SuperAdmin")) redirect("/");

  let health: Health | null = null;
  let error: string | null = null;
  try {
    health = await getHealth();
  } catch (err) {
    // Only a transport failure lands here — a 503 health report is parsed normally, since an
    // unhealthy answer is still an answer.
    error = apiErrorMessage(err);
  }

  return (
    <div className="space-y-6">
      <PageHeader
        kicker="System"
        title="Health"
        description="Live dependency checks reported by the API. Refresh the page to re-run them."
      />

      {error ? (
        <Card>
          <p className="text-sm text-danger">The API did not respond.</p>
          <p className="mt-1 text-sm text-muted">{error}</p>
        </Card>
      ) : health ? (
        <>
          <div className="grid gap-3 sm:grid-cols-2">
            <StatCard
              label="Overall status"
              value={
                <span className="flex items-center gap-2">
                  <StatusIcon status={health.status} />
                  {health.status}
                </span>
              }
              icon={<Activity size={16} />}
            />
            <StatCard label="Total check duration" value={`${health.totalDurationMs.toFixed(1)} ms`} />
          </div>

          <div className="space-y-2">
            <SectionHeader title="Dependencies" />
            {health.checks.length === 0 ? (
              <Card>
                <p className="text-sm text-muted">The API reports no individual dependency checks.</p>
              </Card>
            ) : (
              health.checks.map((c) => (
                <Card key={c.name}>
                  <div className="flex flex-wrap items-center justify-between gap-3">
                    <div className="flex items-center gap-3">
                      <StatusIcon status={c.status} />
                      <div>
                        <p className="font-medium capitalize text-text">{c.name}</p>
                        {c.description ? <p className="text-xs text-muted">{c.description}</p> : null}
                      </div>
                    </div>
                    <div className="flex items-center gap-3">
                      <span className="text-xs text-muted">{c.durationMs.toFixed(1)} ms</span>
                      <Badge tone={TONE[c.status] ?? "neutral"}>{c.status}</Badge>
                    </div>
                  </div>
                </Card>
              ))
            )}
          </div>
        </>
      ) : null}
    </div>
  );
}
