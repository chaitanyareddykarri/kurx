import { Card } from "@kurx/ui";
import { ProvenanceBadge } from "@/components/profile/provenance-badge";
import type { ExperienceSummary, ProfileMetrics } from "@/lib/api";

/**
 * Metrics and Experience (D-225).
 *
 * Two rules this component exists to honour:
 *
 * 1. **Null means hidden, never zero.** A hidden section renders "—". Printing 0 would turn the
 *    owner's privacy choice into a statement about them ("organized 0 events").
 * 2. **The Experience band never appears alone.** A band on its own is an unfalsifiable judgement;
 *    shown beside the counts that produced it, a reader can check it and disagree.
 */

function formatCount(value: number | null): string {
  return value === null ? "—" : value.toLocaleString();
}

function Metric({ label, value, hint }: { label: string; value: string; hint?: string }) {
  return (
    <div className="rounded-md border border-border bg-background px-3 py-2">
      <div className="text-lg font-semibold text-text">{value}</div>
      <div className="text-xs text-muted">{label}</div>
      {hint && <div className="text-[11px] text-muted">{hint}</div>}
    </div>
  );
}

/**
 * Event DNA — a distribution across event kinds, drawn as proportional bars.
 *
 * Deliberately bars rather than the `sparkline` primitive: a sparkline reads as a time series, and
 * this is a categorical breakdown. Using it here would imply a trend the data does not describe.
 */
function EventDna({ dna }: { dna: ProfileMetrics["event_dna"] }) {
  if (dna.length === 0) return null;
  const max = Math.max(...dna.map((d) => d.count));

  return (
    <div className="mt-5">
      <div className="flex items-center gap-2">
        <h3 className="text-sm font-semibold text-text">Event DNA</h3>
        <ProvenanceBadge source="derived" />
      </div>
      <p className="mt-0.5 text-xs text-muted">The kinds of event this person shows up for.</p>
      <ul className="mt-3 space-y-1.5">
        {dna.map((tag) => (
          <li key={tag.kind} className="flex items-center gap-3">
            <span className="w-28 shrink-0 truncate text-xs capitalize text-muted">
              {tag.kind.replace(/[-_]/g, " ")}
            </span>
            <span className="h-2 flex-1 overflow-hidden rounded-full bg-elevated">
              <span
                className="block h-full rounded-full bg-accent"
                style={{ width: `${Math.round((tag.count / max) * 100)}%` }}
              />
            </span>
            <span className="w-6 shrink-0 text-right text-xs tabular-nums text-muted">{tag.count}</span>
          </li>
        ))}
      </ul>
    </div>
  );
}

export function MetricsPanel({
  metrics,
  experience
}: {
  metrics: ProfileMetrics | null;
  experience: ExperienceSummary | null;
}) {
  // Both sections are independently gated server-side; if neither is visible to this viewer, the
  // whole panel is omitted rather than rendered as an empty shell.
  if (!metrics && !experience) return null;

  return (
    <Card>
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h2 className="font-semibold text-text">Activity</h2>
        <ProvenanceBadge source="verified" />
      </div>

      {experience && (
        <div className="mt-3 rounded-md border border-border bg-elevated px-3 py-2">
          <div className="flex flex-wrap items-baseline gap-2">
            <span className="text-base font-semibold text-text">{experience.band}</span>
            <ProvenanceBadge source="derived" />
          </div>
          {/* The counts behind the band — always shown with it, never instead of it. */}
          <p className="mt-1 text-xs text-muted">
            {experience.distinct_events} event{experience.distinct_events === 1 ? "" : "s"}
            {experience.leadership_events > 0 && ` · ${experience.leadership_events} led`}
            {experience.organizations > 0 && ` · ${experience.organizations} organization${experience.organizations === 1 ? "" : "s"}`}
            {experience.years_active > 0 && ` · ${experience.years_active} year${experience.years_active === 1 ? "" : "s"} active`}
          </p>
        </div>
      )}

      {metrics && (
        <>
          <div className="mt-4 grid grid-cols-2 gap-2 sm:grid-cols-3">
            <Metric label="Organized" value={formatCount(metrics.events_organized)} />
            <Metric label="Participated" value={formatCount(metrics.events_participated)} />
            <Metric label="Attended" value={formatCount(metrics.events_attended)} />
            {(metrics.speaker_sessions ?? 0) > 0 && (
              <Metric label="Talks given" value={formatCount(metrics.speaker_sessions)} />
            )}
            {(metrics.competitions_entered ?? 0) > 0 && (
              <Metric
                label="Competitions"
                value={formatCount(metrics.competitions_entered)}
                hint={(metrics.competitions_won ?? 0) > 0 ? `${metrics.competitions_won} won` : undefined}
              />
            )}
            {(metrics.assignments_accepted ?? 0) > 0 && (
              <Metric
                label="Assignments"
                value={formatCount(metrics.assignments_accepted)}
                hint={`${metrics.assignments_completed} completed`}
              />
            )}
            <Metric label="Certificates" value={formatCount(metrics.certificates)} />
            <Metric
              label="Organizations"
              value={formatCount(metrics.organizations)}
              hint={(metrics.verified_organizations ?? 0) > 0 ? `${metrics.verified_organizations} verified` : undefined}
            />
            {metrics.completion_rate !== null && (
              <Metric label="Completion" value={`${Math.round(metrics.completion_rate * 100)}%`} />
            )}
          </div>

          {metrics.cities.length > 0 && (
            <p className="mt-3 text-xs text-muted">
              Active in {metrics.cities.slice(0, 4).join(", ")}
              {metrics.cities.length > 4 && ` +${metrics.cities.length - 4} more`}
            </p>
          )}

          <EventDna dna={metrics.event_dna} />
        </>
      )}
    </Card>
  );
}
