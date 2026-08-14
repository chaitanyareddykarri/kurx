import Link from "next/link";
import { Card, Badge } from "@kurx/ui";
import { Trophy, Mic } from "lucide-react";
import type { CompetitionResult, SpeakerSession } from "@/lib/api";

function formatDate(iso: string) {
  return new Date(iso).toLocaleDateString(undefined, { month: "short", year: "numeric" });
}

// Placement wording matches the backend's: beyond third the rank is stated plainly rather than
// dressed up, because "Finalist" would be a claim the stored rank does not make.
function rankLabel(rank: number) {
  if (rank === 1) return "Winner";
  if (rank === 2) return "Runner-up";
  if (rank === 3) return "Third place";
  return `#${rank}`;
}

/**
 * Competition results and speaking history (D-222). Both are verified rows that existed in the
 * backend from the start and had never appeared on a profile. Each section renders only when it has
 * real content — an empty section is omitted rather than shown as a zero state.
 */
export function VerifiedSections({
  competitions,
  sessions
}: {
  competitions: CompetitionResult[];
  sessions: SpeakerSession[];
}) {
  if (competitions.length === 0 && sessions.length === 0) return null;

  return (
    <div className="mt-4 grid gap-4 lg:grid-cols-2">
      {competitions.length > 0 && (
        <Card>
          <h2 className="flex items-center gap-2 font-semibold text-text">
            <Trophy size={16} aria-hidden /> Competition results
          </h2>
          <p className="mt-1 text-xs text-muted">Published results only — provisional and disputed placements never appear.</p>
          <ul className="mt-3 space-y-3">
            {competitions.map((c) => (
              <li key={`${c.event_id}-${c.stage_name}`} className="flex items-start justify-between gap-3">
                <div className="min-w-0">
                  <Link href={`/e/${c.event_slug}`} className="font-medium text-text hover:underline">
                    {c.event_title}
                  </Link>
                  <p className="text-xs text-muted">
                    {[c.stage_name, c.org_name, formatDate(c.occurred_at)].filter(Boolean).join(" · ")}
                  </p>
                </div>
                <Badge tone={c.rank === 1 ? "accent" : undefined}>{rankLabel(c.rank)}</Badge>
              </li>
            ))}
          </ul>
        </Card>
      )}

      {sessions.length > 0 && (
        <Card>
          <h2 className="flex items-center gap-2 font-semibold text-text">
            <Mic size={16} aria-hidden /> Speaking
          </h2>
          <p className="mt-1 text-xs text-muted">Sessions an organizer linked to this account.</p>
          <ul className="mt-3 space-y-3">
            {sessions.map((s) => (
              <li key={`${s.event_id}-${s.session_title}`} className="min-w-0">
                <p className="font-medium text-text">{s.session_title}</p>
                <p className="text-xs text-muted">
                  <Link href={`/e/${s.event_slug}`} className="hover:underline">{s.event_title}</Link>
                  {/* A self-represented event has no org name; the separator goes with it, not a gap. */}
                  {[s.org_name, formatDate(s.starts_at)].filter(Boolean).map((t) => ` · ${t}`).join("")}
                </p>
              </li>
            ))}
          </ul>
        </Card>
      )}
    </div>
  );
}
