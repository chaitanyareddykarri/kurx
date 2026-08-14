import Link from "next/link";
import { CalendarX, Search, SlidersHorizontal, X } from "lucide-react";
import { getTranslations } from "next-intl/server";
import { EventCard } from "@/components/events/event-card";
import { KindChips } from "@/components/events/kind-chips";
import { Alert, Button, EmptyState, Field, Input, LinkButton, Select } from "@kurx/ui";
import { ChipLink } from "@/components/ui/chip";
import { ChipRail } from "@/components/ui/chip-rail";
import { FilterSection } from "@/components/ui/filter-section";
import {
  apiErrorMessage,
  getFeaturedEvents,
  getLatestEvents,
  getRecommendedEvents,
  getTrendingEvents,
  getUpcomingEvents,
  listCategories,
  listKinds,
  searchEvents
} from "@/lib/api";
import { requireSession } from "@/lib/session";
import type { Category, EventSearchResult, EventSummary, Kind } from "@/lib/api";

const PAGE_SIZE = 12;
/** One row of three at every breakpoint the results grid uses, so a rail never renders a ragged tail. */
const RAIL_SIZE = 3;
const FILTER_KEYS = ["q", "categoryId", "city", "dateFrom", "dateTo", "sort", "kind", "mode", "price"] as const;
type FilterKey = (typeof FILTER_KEYS)[number];

type SearchParams = Record<string, string | string[] | undefined>;

function one(params: SearchParams, key: string): string {
  const v = params[key];
  return (Array.isArray(v) ? v[0] : v) ?? "";
}

/** Builds a `/discover` URL from the current filters with the given overrides applied (undefined removes
 * the key). The single link-building path for pagination, chip toggles, and per-filter removal — the URL
 * stays the whole state, so every one of these is a plain server-rendered `<Link>`, no client JS. */
function hrefWith(params: SearchParams, overrides: Partial<Record<FilterKey | "page", string | undefined>>): string {
  const qs = new URLSearchParams();
  for (const key of FILTER_KEYS) {
    const value = key in overrides ? overrides[key] : one(params, key);
    if (value) qs.set(key, value);
  }
  const page = "page" in overrides ? overrides.page : one(params, "page");
  if (page && page !== "1") qs.set("page", page);
  const s = qs.toString();
  return s ? `/discover?${s}` : "/discover";
}

/** One curated row. Renders nothing at all when the rail is empty — a heading over a blank strip reads
 * as a loading failure, and five of them read as an outage. Five near-identical sections is past the
 * point where repeating the markup is the cheaper option. */
function EventRail({ title, subtitle, events }: { title: string; subtitle: string; events: EventSummary[] }) {
  if (events.length === 0) return null;
  return (
    <section className="space-y-3">
      <div>
        <h2 className="text-h2">{title}</h2>
        <p className="text-body text-muted">{subtitle}</p>
      </div>
      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
        {events.map((event) => (
          <EventCard key={event.id} event={event} />
        ))}
      </div>
    </section>
  );
}

/** Removes one or more filter keys, resetting to page 1 — the target of every active-filter chip's "×". */
function removeFilterHref(params: SearchParams, keys: FilterKey[]): string {
  const overrides: Partial<Record<FilterKey | "page", string | undefined>> = { page: undefined };
  for (const key of keys) overrides[key] = undefined;
  return hrefWith(params, overrides);
}

export default async function DiscoverPage({ searchParams }: { searchParams: SearchParams }) {
  const t = await getTranslations("discover");
  const session = await requireSession();
  const page = Math.max(Number(one(searchParams, "page")) || 1, 1);

  const filters = {
    q: one(searchParams, "q") || undefined,
    categoryId: one(searchParams, "categoryId") || undefined,
    city: one(searchParams, "city") || undefined,
    dateFrom: one(searchParams, "dateFrom") || undefined,
    dateTo: one(searchParams, "dateTo") || undefined,
    sort: one(searchParams, "sort") || undefined,
    kind: one(searchParams, "kind") || undefined,
    mode: one(searchParams, "mode") || undefined,
    price: one(searchParams, "price") || undefined
  };

  let results: EventSearchResult | null = null;
  let categories: Category[] = [];
  let kinds: Kind[] = [];
  let error: string | null = null;

  try {
    [results, categories, kinds] = await Promise.all([
      searchEvents({ ...filters, page, pageSize: PAGE_SIZE }),
      listCategories().catch(() => [] as Category[]),
      listKinds().catch(() => [] as Kind[])
    ]);
  } catch (err) {
    error = apiErrorMessage(err);
  }

  const totalPages = results ? Math.max(Math.ceil(results.total / PAGE_SIZE), 1) : 1;
  const hasFilters = Object.values(filters).some(Boolean);

  // The curated rails are the *unfiltered* landing state. Once someone has typed a query or picked a
  // facet they have told us what they want, and five rails of things they did not ask for push the
  // answer below the fold — so they are skipped entirely rather than rendered and ignored, which also
  // saves five API calls on every paginated search.
  //
  // Each `.catch(() => [])` is per-rail on purpose: one empty rail disappears, it does not take the
  // page down with it. `/for-you` is the only authenticated one and the only one that can 401.
  const [featured, trending, upcoming, latest, recommended] = hasFilters
    ? [[], [], [], [], []]
    : await Promise.all([
        getFeaturedEvents(RAIL_SIZE).catch(() => [] as EventSummary[]),
        getTrendingEvents(RAIL_SIZE).catch(() => [] as EventSummary[]),
        getUpcomingEvents(RAIL_SIZE).catch(() => [] as EventSummary[]),
        getLatestEvents(RAIL_SIZE).catch(() => [] as EventSummary[]),
        getRecommendedEvents(session.accessToken, RAIL_SIZE).catch(() => [] as EventSummary[])
      ]);

  const modeOptions: { value?: string; label: string }[] = [
    { value: undefined, label: t("allModes") },
    { value: "offline", label: t("modeOffline") },
    { value: "online", label: t("modeOnline") },
    { value: "hybrid", label: t("modeHybrid") }
  ];
  const priceOptions: { value?: string; label: string }[] = [
    { value: undefined, label: t("allPrices") },
    { value: "free", label: t("priceFree") },
    { value: "paid", label: t("pricePaid") }
  ];

  const categoryName = (id: string) => categories.find((c) => c.id === id)?.name ?? id;
  const kindName = (slug: string) => kinds.find((k) => k.slug === slug)?.name ?? slug;
  // Each chip carries the facet name as well as the value. A bare "Bengaluru"
  // out of context is guesswork, and five identical "×" controls in a row are
  // indistinguishable to a screen reader — so `facet` also names the remove link.
  const activeFilterChips: { key: FilterKey | "date"; facet: string; label: string; removeKeys: FilterKey[] }[] = [
    ...(filters.q ? [{ key: "q" as const, facet: t("keyword"), label: `"${filters.q}"`, removeKeys: ["q" as const] }] : []),
    ...(filters.categoryId
      ? [{ key: "categoryId" as const, facet: t("category"), label: categoryName(filters.categoryId), removeKeys: ["categoryId" as const] }]
      : []),
    ...(filters.kind ? [{ key: "kind" as const, facet: t("kind"), label: kindName(filters.kind), removeKeys: ["kind" as const] }] : []),
    ...(filters.city ? [{ key: "city" as const, facet: t("city"), label: filters.city, removeKeys: ["city" as const] }] : []),
    ...(filters.mode
      ? [{ key: "mode" as const, facet: t("mode"), label: modeOptions.find((m) => m.value === filters.mode)?.label ?? filters.mode, removeKeys: ["mode" as const] }]
      : []),
    ...(filters.price
      ? [{ key: "price" as const, facet: t("price"), label: priceOptions.find((p) => p.value === filters.price)?.label ?? filters.price, removeKeys: ["price" as const] }]
      : []),
    ...(filters.dateFrom || filters.dateTo
      ? [{ key: "date" as const, facet: t("dateFrom"), label: [filters.dateFrom, filters.dateTo].filter(Boolean).join(" – "), removeKeys: ["dateFrom" as const, "dateTo" as const] }]
      : [])
  ];

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-h1">{t("title")}</h1>
        <p className="mt-2 text-body-lg text-muted">{t("subtitle")}</p>
      </div>

      {/* Home's search covers three entity types, and until now only this one was reachable from here.
          People and Posts each already own a working search on their own surface, so these are links to
          those rather than a fourth combined results page nobody would maintain. */}
      {/* Every chip block below carries a VISIBLE heading, not just an aria-label. Three rails of
          identically-styled pills stacked with nothing between them read as one 34-item wall: nothing
          told a sighted user where "Event type" ended and "Category" began, and the two do different
          things (a kind toggles a filter, a category navigates away). The heading style and the
          heading-hugs-its-chips spacing are the same ones "Active filters" below already uses —
          proximity is what does the grouping, so the gap inside a block must stay smaller than the
          `space-y-6` gap between them. */}
      <section className="space-y-2">
        <h2 className="text-micro uppercase tracking-wide text-muted">{t("searchElsewhere")}</h2>
        <nav className="flex flex-wrap gap-2" aria-label={t("searchElsewhere")}>
          <ChipLink href="/discover" selected>
            {t("searchEvents")}
          </ChipLink>
          <ChipLink href="/allies">{t("searchPeople")}</ChipLink>
          <ChipLink href={filters.q ? `/posts?q=${encodeURIComponent(filters.q)}` : "/posts"}>
            {t("searchPosts")}
          </ChipLink>
        </nav>
      </section>

      {/* Both groups ship CLOSED. Together they are 34 options, which on arrival pushed the results —
          the thing the page is for — below two screens of pills. The rails themselves stay server
          components passed as children, so only the open/shut toggle costs client JS. */}
      {(kinds.length > 0 || categories.length > 0) && (
        <div className="rounded-lg border border-border bg-surface px-4">
          {kinds.length > 0 && (
            <FilterSection
              title={t("kind")}
              selectedLabel={filters.kind ? kindName(filters.kind) : undefined}
            >
              <KindChips
                kinds={kinds}
                activeSlug={filters.kind}
                hrefFor={(slug) => hrefWith(searchParams, { kind: slug, page: undefined })}
                ariaLabel={t("kind")}
              />
            </FilterSection>
          )}

          {/* Category browse. These link OUT to /categories/{id} rather than setting the categoryId
              facet like the Kind chips do: a category is a place you go, a kind is a filter you toggle,
              and the browse page was previously reachable from nowhere on this surface. The header still
              reflects `categoryId` when the filter form has set one, so a shut group never hides an
              active filter. */}
          {categories.length > 0 && (
            <FilterSection
              title={t("category")}
              selectedLabel={filters.categoryId ? categoryName(filters.categoryId) : undefined}
            >
              <ChipRail
                ariaLabel={t("category")}
                items={categories.map((c) => ({ key: c.id, label: c.name, href: `/categories/${c.id}` }))}
              />
            </FilterSection>
          )}
        </div>
      )}

      {activeFilterChips.length > 0 && (
        <div className="flex flex-wrap items-center gap-2">
          <h2 className="text-micro uppercase tracking-wide text-muted">{t("activeFilters")}</h2>
          {activeFilterChips.map((chip) => (
            <ChipLink
              key={chip.key}
              href={removeFilterHref(searchParams, chip.removeKeys)}
              selected
              aria-label={`Remove filter ${chip.facet}: ${chip.label}`}
              trailing={<X size={14} aria-hidden="true" />}
            >
              <span className="text-muted">{chip.facet}:</span> {chip.label}
            </ChipLink>
          ))}
          <Link
            href="/discover"
            className="inline-flex min-h-11 items-center rounded-md px-2 text-caption text-accent-text hover:underline"
          >
            {t("clear")}
          </Link>
        </div>
      )}

      <div className="grid gap-6 lg:grid-cols-[260px_1fr]">
        {/* Native GET form: the URL is the state, so filters survive reload and share. Mode/Price are
            plain chip links outside the form (immediate navigation, one param at a time) — hidden inputs
            carry their current value through so submitting the form never clobbers them. */}
        {/*
          `<details>` rather than a JS drawer: this page's whole architecture is
          URL-as-state with no client JS, and native details is keyboard-operable
          and announced as expanded/collapsed for free.

          It collapses only below `lg`. Previously the panel always stacked ABOVE
          the results, so a phone user scrolled past eight filter fields before
          reaching a single event — the filters were pushing the content they
          exist to refine off the screen.

          The `filter-panel` class in globals.css forces it open on desktop and
          hides the summary there.
        */}
        <details className="filter-panel h-fit rounded-lg border border-border bg-surface" open={hasFilters}>
          <summary className="flex min-h-11 cursor-pointer items-center gap-sm px-4 py-3 text-label text-text lg:hidden">
            <SlidersHorizontal size={16} aria-hidden="true" />
            {t("filters")}
            {activeFilterChips.length > 0 ? (
              <span className="rounded-pill bg-accent px-2 py-0.5 text-micro text-on-accent">
                {activeFilterChips.length}
              </span>
            ) : null}
          </summary>
          <form method="GET" action="/discover" className="space-y-4 p-4 pt-0 lg:pt-4">
          <h2 className="sr-only">{t("filters")}</h2>
          <input type="hidden" name="mode" value={filters.mode ?? ""} />
          <input type="hidden" name="price" value={filters.price ?? ""} />

          <Field label={t("keyword")} htmlFor="q">
            <Input id="q" name="q" defaultValue={filters.q} placeholder={t("keywordPlaceholder")} />
          </Field>

          <Field label={t("category")} htmlFor="categoryId">
            <Select id="categoryId" name="categoryId" defaultValue={filters.categoryId ?? ""}>
              <option value="">{t("allCategories")}</option>
              {categories.map((c) => (
                <option key={c.id} value={c.id}>{c.name}</option>
              ))}
            </Select>
          </Field>

          <Field label={t("kind")} htmlFor="kind">
            <Select id="kind" name="kind" defaultValue={filters.kind ?? ""}>
              <option value="">{t("allKinds")}</option>
              {kinds.map((k) => (
                <option key={k.slug} value={k.slug}>{k.name}</option>
              ))}
            </Select>
          </Field>

          <Field label={t("city")} htmlFor="city">
            <Input id="city" name="city" defaultValue={filters.city} placeholder={t("cityPlaceholder")} />
          </Field>

          <Field label={t("dateFrom")} htmlFor="dateFrom">
            <Input id="dateFrom" name="dateFrom" type="date" defaultValue={filters.dateFrom} />
          </Field>

          <Field label={t("dateTo")} htmlFor="dateTo">
            <Input id="dateTo" name="dateTo" type="date" defaultValue={filters.dateTo} />
          </Field>

          <Field label={t("sort")} htmlFor="sort">
            <Select id="sort" name="sort" defaultValue={filters.sort ?? "date_asc"}>
              <option value="date_asc">{t("sortDateAsc")}</option>
              <option value="date_desc">{t("sortDateDesc")}</option>
              <option value="newest">{t("sortNewest")}</option>
              <option value="popular">{t("sortPopular")}</option>
            </Select>
          </Field>

          <Field label={t("mode")} htmlFor="mode-chips">
            <div id="mode-chips" className="flex flex-wrap gap-2">
              {modeOptions.map((opt) => (
                <ChipLink
                  key={opt.label}
                  href={hrefWith(searchParams, { mode: opt.value, page: undefined })}
                  selected={filters.mode === opt.value}
                >
                  {opt.label}
                </ChipLink>
              ))}
            </div>
          </Field>

          <Field label={t("price")} htmlFor="price-chips">
            <div id="price-chips" className="flex flex-wrap gap-2">
              {priceOptions.map((opt) => (
                <ChipLink
                  key={opt.label}
                  href={hrefWith(searchParams, { price: opt.value, page: undefined })}
                  selected={filters.price === opt.value}
                >
                  {opt.label}
                </ChipLink>
              ))}
            </div>
          </Field>

          <div className="flex gap-2 pt-1">
            {/* Was a hand-rolled 40px button with a hardcoded `text-black` on the
                ember fill — right by accident, and outside the token system. */}
            <Button type="submit" className="flex-1">
              <Search size={16} aria-hidden="true" /> {t("apply")}
            </Button>
            {hasFilters && (
              <LinkButton href="/discover" variant="secondary">
                {t("clear")}
              </LinkButton>
            )}
          </div>
          </form>
        </details>

        <div className="space-y-8">
          <EventRail title={t("featured")} subtitle={t("featuredSubtitle")} events={featured} />
          <EventRail title={t("trending")} subtitle={t("trendingSubtitle")} events={trending} />
          <EventRail title={t("upcoming")} subtitle={t("upcomingSubtitle")} events={upcoming} />
          <EventRail title={t("latest")} subtitle={t("latestSubtitle")} events={latest} />
          <EventRail title={t("recommended")} subtitle={t("recommendedSubtitle")} events={recommended} />

          {error ? (
            <Alert
              tone="danger"
              live
              title={t("emptyTitle")}
              action={
                <LinkButton href="/discover" variant="secondary" size="sm">
                  {t("clear")}
                </LinkButton>
              }
            >
              {error}
            </Alert>
          ) : results && results.items.length === 0 ? (
            <EmptyState
              icon={<CalendarX size={40} aria-hidden="true" />}
              title={t("emptyTitle")}
              message={hasFilters ? t("emptyFiltered") : t("emptyMessage")}
              // "No results" with no way out is a dead end; when filters caused
              // it, the remedy is one link away and should be offered.
              action={
                hasFilters ? (
                  <LinkButton href="/discover" variant="secondary">
                    {t("clear")}
                  </LinkButton>
                ) : undefined
              }
            />
          ) : results ? (
            <>
              <h2 className="text-body text-muted">{t("resultCount", { count: results.total })}</h2>
              <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
                {results.items.map((event) => (
                  <EventCard key={event.id} event={event} />
                ))}
              </div>

              {totalPages > 1 && (
                <nav className="flex items-center justify-between border-t border-border pt-4" aria-label={t("pagination")}>
                  {page > 1 ? (
                    <Link
                      href={hrefWith(searchParams, { page: String(page - 1) })}
                      className="inline-flex min-h-11 items-center rounded-md px-2 text-body text-accent-text hover:underline"
                    >
                      {t("previous")}
                    </Link>
                  ) : (
                    <span className="px-2 text-body text-muted opacity-50">{t("previous")}</span>
                  )}
                  <span className="text-body text-muted">{t("pageOf", { page, totalPages })}</span>
                  {page < totalPages ? (
                    <Link
                      href={hrefWith(searchParams, { page: String(page + 1) })}
                      className="inline-flex min-h-11 items-center rounded-md px-2 text-body text-accent-text hover:underline"
                    >
                      {t("next")}
                    </Link>
                  ) : (
                    <span className="px-2 text-body text-muted opacity-50">{t("next")}</span>
                  )}
                </nav>
              )}
            </>
          ) : null}
        </div>
      </div>
    </div>
  );
}
