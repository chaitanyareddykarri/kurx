import Link from "next/link";
import { Card } from "@kurx/ui";
import type { JourneyNode } from "@/lib/api";

// Tier → display label + accent. Order here is presentational weight only; the rail itself renders in
// the order the API returns, which is strictly chronological by first attainment (D-223). A canonical
// ladder would assert a progression the data does not support — people organize before volunteering.
const TIERS: Record<string, { label: string; blurb: string }> = {
  attendee: { label: "Attendee", blurb: "First verified check-in" },
  participant: { label: "Participant", blurb: "First event taken part in" },
  volunteer: { label: "Volunteer", blurb: "First time serving an event" },
  team_lead: { label: "Team Lead", blurb: "First time leading a team" },
  competition_winner: { label: "Competition Winner", blurb: "First published first place" },
  speaker: { label: "Speaker", blurb: "First talk given" },
  judge: { label: "Judge", blurb: "First time judging" },
  mentor: { label: "Mentor", blurb: "First time mentoring" },
  organizer: { label: "Organizer", blurb: "First event run" },
  host: { label: "Host", blurb: "First event hosted" },
  verified_member: { label: "Verified Member", blurb: "First verified affiliation" }
};

function formatDate(iso: string) {
  return new Date(iso).toLocaleDateString(undefined, { month: "short", year: "numeric" });
}

/** Renders the evidence for one node as a link when the row points at a public event. */
function Evidence({ node }: { node: JourneyNode }) {
  const { event_title, event_slug, detail, org_name } = node.evidence;
  const label = detail ?? event_title ?? org_name;
  if (!label) return null;
  const body = <span className="text-muted">{label}</span>;
  return (
    <p className="mt-0.5 text-xs">
      {event_slug ? (
        <Link href={`/e/${event_slug}`} className="text-muted underline-offset-2 hover:text-text hover:underline">
          {label}
        </Link>
      ) : (
        body
      )}
    </p>
  );
}

export function JourneyRail({ nodes }: { nodes: JourneyNode[] }) {
  // An empty journey is the honest output for someone with no verified activity yet — rendering an
  // aspirational placeholder ladder would be exactly the fabricated progress this system avoids.
  if (nodes.length === 0) return null;

  return (
    <Card>
      <h2 className="font-semibold text-text">Professional Journey</h2>
      <p className="mt-1 text-xs text-muted">
        The first time each milestone was reached, in order. Every step links to the record that proves it.
      </p>
      <ol className="mt-4 space-y-0">
        {nodes.map((node, i) => {
          const tier = TIERS[node.tier] ?? { label: node.tier, blurb: "" };
          const last = i === nodes.length - 1;
          return (
            <li key={node.tier} className="relative flex gap-3 pb-5 last:pb-0">
              {/* The rail: a line behind every node but the last, so the sequence reads as one path. */}
              {!last && <span aria-hidden className="absolute left-[7px] top-4 h-full w-px bg-border" />}
              <span aria-hidden className="relative mt-1.5 h-3.5 w-3.5 shrink-0 rounded-full border-2 border-accent bg-surface" />
              <div className="min-w-0">
                <div className="flex flex-wrap items-baseline gap-x-2">
                  <span className="font-medium text-text">{tier.label}</span>
                  <span className="text-xs text-muted">{formatDate(node.first_attained_at)}</span>
                  {node.occurrences > 1 && (
                    <span className="rounded-full border border-border px-1.5 text-[11px] text-muted">
                      ×{node.occurrences}
                    </span>
                  )}
                </div>
                {tier.blurb && <p className="text-xs text-muted">{tier.blurb}</p>}
                <Evidence node={node} />
              </div>
            </li>
          );
        })}
      </ol>
    </Card>
  );
}
