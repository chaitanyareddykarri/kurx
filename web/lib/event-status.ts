/**
 * Event lifecycle status → the words a host reads.
 *
 * `/workspace` printed the backend's value straight into a sentence, so a host waiting on a decision
 * read "Opens after approval · pendingreview". The same values drive the workspace's view filters,
 * which already normalise with `.toLowerCase()` — this is that normalisation given a label.
 *
 * An unrecognised status is shown as-is rather than mapped to a guess: telling a host their event is
 * "Published" because we did not recognise the value would be worse than showing them the raw word.
 */
export type EventStatusTone = "success" | "warning" | "danger" | "muted" | "accent";

const LABELS: Record<string, { label: string; tone: EventStatusTone }> = {
  draft: { label: "Draft", tone: "muted" },
  pendingreview: { label: "Pending review", tone: "warning" },
  underreview: { label: "Under review", tone: "warning" },
  changesrequested: { label: "Changes requested", tone: "danger" },
  rejected: { label: "Rejected", tone: "danger" },
  approved: { label: "Approved", tone: "success" },
  published: { label: "Published", tone: "success" },
  scheduled: { label: "Scheduled", tone: "success" },
  live: { label: "Live", tone: "accent" },
  completed: { label: "Completed", tone: "muted" },
  closed: { label: "Closed", tone: "muted" },
  archived: { label: "Archived", tone: "muted" },
  cancelled: { label: "Cancelled", tone: "danger" },
  canceled: { label: "Cancelled", tone: "danger" },
};

export function eventStatusOf(status: string): { label: string; tone: EventStatusTone } {
  const key = status.toLowerCase().replace(/[\s_-]/g, "");
  return LABELS[key] ?? { label: status, tone: "muted" };
}
