import { Card } from "@kurx/ui";
import { ProvenanceBadge } from "@/components/profile/provenance-badge";
import type { Contributions } from "@/lib/api";

/**
 * The contributions heatmap (D-228) — daily activity density over a rolling window.
 *
 * The server returns only days that *have* activity, and each carries a 0–4 intensity level so the
 * client never re-decides the scale. Empty cells are filled here.
 *
 * Colour is not the only encoding: every cell carries a title with the date and count, so the grid
 * is readable without relying on hue discrimination.
 */
const LEVEL_CLASS = [
  "bg-elevated",
  "bg-accent/25",
  "bg-accent/45",
  "bg-accent/70",
  "bg-accent"
] as const;

function startOfWeek(d: Date) {
  const copy = new Date(d);
  copy.setUTCDate(copy.getUTCDate() - copy.getUTCDay());
  copy.setUTCHours(0, 0, 0, 0);
  return copy;
}

function isoDay(d: Date) {
  return d.toISOString().slice(0, 10);
}

export function ContributionsHeatmap({
  contributions,
  weeks = 52
}: {
  contributions: Contributions | null;
  weeks?: number;
}) {
  // No data at all means nothing to show — an empty grid would imply "inactive", which is a claim,
  // whereas absence is honest about a person who simply has no verified activity yet.
  if (!contributions || contributions.total === 0) return null;

  const byDay = new Map(contributions.days.map((d) => [d.date, d]));
  const end = startOfWeek(new Date(`${contributions.to}T00:00:00Z`));
  const columns: { date: string; count: number; level: number }[][] = [];

  for (let w = weeks - 1; w >= 0; w--) {
    const weekStart = new Date(end);
    weekStart.setUTCDate(weekStart.getUTCDate() - w * 7);
    const column = [];
    for (let d = 0; d < 7; d++) {
      const cell = new Date(weekStart);
      cell.setUTCDate(cell.getUTCDate() + d);
      const key = isoDay(cell);
      const hit = byDay.get(key);
      column.push({ date: key, count: hit?.count ?? 0, level: hit?.level ?? 0 });
    }
    columns.push(column);
  }

  return (
    <Card>
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h2 className="font-semibold text-text">Contributions</h2>
        <ProvenanceBadge source="derived" />
      </div>
      <p className="mt-1 text-xs text-muted">
        {contributions.total} contribution{contributions.total === 1 ? "" : "s"} in the last year —
        events run, taken part in, spoken at, and recognised.
      </p>

      {/* Wide content scrolls inside its own container; the page never scrolls horizontally. */}
      <div className="mt-4 overflow-x-auto pb-1">
        <div className="flex gap-[3px]" role="img" aria-label={`${contributions.total} contributions in the last year`}>
          {columns.map((column) => (
            <div key={column[0].date} className="flex flex-col gap-[3px]">
              {column.map((cell) => (
                <span
                  key={cell.date}
                  title={`${cell.count} on ${cell.date}`}
                  className={`h-[10px] w-[10px] rounded-[2px] ${LEVEL_CLASS[cell.level] ?? LEVEL_CLASS[0]}`}
                />
              ))}
            </div>
          ))}
        </div>
      </div>

      <div className="mt-2 flex items-center justify-end gap-1 text-[11px] text-muted">
        <span>Less</span>
        {LEVEL_CLASS.map((cls, i) => (
          <span key={i} className={`h-[10px] w-[10px] rounded-[2px] ${cls}`} />
        ))}
        <span>More</span>
      </div>
    </Card>
  );
}
