import { redirect } from "next/navigation";
import { Card } from "@kurx/ui";
import { requireStaffSession } from "@/lib/session";
import { PageHeader } from "@/components/layout/page-header";
import { listAdminCategories, apiErrorMessage, apiErrorStatus, type AdminCategory } from "@/lib/api";
import { TaxonomyWorkspace } from "@/components/admin/taxonomy/taxonomy-workspace";

// D-188 (Platform Taxonomy Management). Replaces developer-seeded-only taxonomy with a fully
// admin-manageable module — see docs/DECISIONS.md D-188. This screen stays alongside the legacy
// /categories screen until parity is verified (decision 9); both work today.
export default async function EventTaxonomyPage() {
  const session = await requireStaffSession();
  if (!session.roles.includes("SuperAdmin")) redirect("/");

  try {
    const categories = await listAdminCategories(session.accessToken);
    const stats = computeStats(categories);

    return (
      <div className="space-y-6">
        <PageHeader
          kicker="Platform"
          title="Event Taxonomy"
          description="Audiences, categories, and event types — fully admin-managed. New databases bootstrap from a fixed starter set; every change after that happens here, not in code."
        />
        <StatsPanel stats={stats} />
        <TaxonomyWorkspace categories={categories} />
      </div>
    );
  } catch (err) {
    const forbidden = apiErrorStatus(err) === 403;
    return (
      <div className="space-y-4">
        <h1 className="text-2xl font-semibold text-text">Event Taxonomy</h1>
        <Card>
          {forbidden ? (
            <p className="text-sm text-muted">You don&apos;t have access to the taxonomy.</p>
          ) : (
            <p className="text-sm text-muted">
              Couldn&apos;t load the taxonomy: <span className="text-text">{apiErrorMessage(err)}</span>
            </p>
          )}
        </Card>
      </div>
    );
  }
}

function computeStats(categories: AdminCategory[]) {
  const byLevel = (level: string) => categories.filter((c) => c.level === level);
  const used = categories.filter((c) => c.usage_count > 0);
  const mostUsed = [...categories].sort((a, b) => b.usage_count - a.usage_count).slice(0, 3);
  return {
    totalAudiences: byLevel("audience").length,
    totalCategories: byLevel("category").length,
    totalTypes: byLevel("type").length,
    active: categories.filter((c) => c.status === "active").length,
    disabled: categories.filter((c) => c.status === "disabled").length,
    archived: categories.filter((c) => c.status === "archived").length,
    unused: categories.length - used.length,
    mostUsed
  };
}

function StatsPanel({ stats }: { stats: ReturnType<typeof computeStats> }) {
  const tiles: { label: string; value: number | string }[] = [
    { label: "Audiences", value: stats.totalAudiences },
    { label: "Categories", value: stats.totalCategories },
    { label: "Event Types", value: stats.totalTypes },
    { label: "Active", value: stats.active },
    { label: "Disabled", value: stats.disabled },
    { label: "Archived", value: stats.archived },
    { label: "Unused", value: stats.unused }
  ];
  return (
    <div className="grid grid-cols-2 gap-3 sm:grid-cols-4 lg:grid-cols-7">
      {tiles.map((t) => (
        <div key={t.label} className="rounded-lg border border-border bg-surface p-3">
          <p className="text-xs text-muted">{t.label}</p>
          <p className="mt-1 text-xl font-semibold text-text">{t.value}</p>
        </div>
      ))}
      {stats.mostUsed.length > 0 && stats.mostUsed[0].usage_count > 0 ? (
        <div className="col-span-2 rounded-lg border border-border bg-surface p-3 sm:col-span-4 lg:col-span-7">
          <p className="text-xs text-muted">Most used</p>
          <p className="mt-1 truncate text-sm text-text">
            {stats.mostUsed.filter((c) => c.usage_count > 0).map((c) => `${c.name} (${c.usage_count})`).join(" · ")}
          </p>
        </div>
      ) : null}
    </div>
  );
}
