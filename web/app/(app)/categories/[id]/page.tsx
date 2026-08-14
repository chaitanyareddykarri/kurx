import { notFound } from "next/navigation";
import { CalendarX } from "lucide-react";
import { getTranslations } from "next-intl/server";
import { EmptyState, LinkButton } from "@kurx/ui";
import { EventCard } from "@/components/events/event-card";
import { listCategories, searchEvents } from "@/lib/api";
import { requireSession } from "@/lib/session";

/// Category browse. Deliberately thin: it resolves the category, shows its first page of events, and
/// hands every further interaction to `/discover?categoryId=…`, which already owns pagination and the
/// other five facets. A second filter UI here would be a copy that drifts.
///
/// Depth: `listCategories()` asks for `level=Category` only, so a subcategory id resolves to nothing and
/// 404s rather than rendering an empty page under a blank title.

const PAGE_SIZE = 12;

export async function generateMetadata({ params }: { params: { id: string } }) {
  const category = (await listCategories().catch(() => [])).find((c) => c.id === params.id);
  return { title: category ? `${category.name} events` : "Category" };
}

export default async function CategoryBrowsePage({ params }: { params: { id: string } }) {
  const t = await getTranslations("discover");
  await requireSession();

  const category = (await listCategories().catch(() => [])).find((c) => c.id === params.id);
  if (!category) notFound();

  const results = await searchEvents({ categoryId: category.id, page: 1, pageSize: PAGE_SIZE })
    .catch(() => ({ items: [], total: 0 }));

  const discoverHref = `/discover?categoryId=${encodeURIComponent(category.id)}`;

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-h1">{category.name}</h1>
        <p className="mt-2 text-body-lg text-muted">{t("resultCount", { count: results.total })}</p>
      </div>

      {results.items.length === 0 ? (
        <EmptyState
          icon={<CalendarX size={40} aria-hidden="true" />}
          title={t("emptyTitle")}
          message={t("emptyMessage")}
          action={
            <LinkButton href="/discover" variant="secondary">
              {t("title")}
            </LinkButton>
          }
        />
      ) : (
        <>
          <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
            {results.items.map((event) => (
              <EventCard key={event.id} event={event} />
            ))}
          </div>
          {results.total > results.items.length && (
            <LinkButton href={discoverHref} variant="secondary">
              {t("title")}
            </LinkButton>
          )}
        </>
      )}
    </div>
  );
}
