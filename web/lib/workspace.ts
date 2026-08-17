/// User Workspace state rules (D-305).
///
/// Extracted from `app/(app)/workspace/page.tsx` so they can be tested: that page is an async server
/// component and cannot be rendered in vitest, which left the one rule that decides *whether a host
/// workspace opens* with no coverage at all.
///
/// **These must stay in step with the Flutter hub's identically-named sets.** Two clients disagreeing
/// about when a host workspace opens is the same defect twice: one lets an organiser into a management
/// surface for an event that has not been approved, the other tells them to wait when it has.

/// Statuses where the **Event Host Workspace** is genuinely open. Everything before approval shows its
/// state instead of being presented as a working workspace (product flow: "Admin Review → Approved →
/// Host Workspace Opens").
///
/// D-377 — `approved` belongs here, and its absence contradicted the sentence directly above. Approval
/// is the moment the workspace opens: the event is reviewed, permitted, and waiting on its host to
/// publish it. Without it the row drew an hourglass reading "Opens after approval" on an event that WAS
/// approved, and the one action the host now had — Publish — sat behind a link the page would not offer.
export const OPEN_HOST_STATES = new Set([
  "approved", "published", "scheduled", "live", "completed", "closed"
]);

/// D-266 M4: the states where the event is with the platform and the host is waiting on a decision.
/// `changesrequested`/`rejected` are deliberately excluded — those are decided outcomes the host has to
/// act on, not a pending queue. Same pair the admin review queue lists.
export const AWAITING_REVIEW_STATES = new Set(["pendingreview", "underreview"]);

/// Whether opening this event leads to its Event Host Workspace, or to a status card explaining why not.
export function hostWorkspaceOpen(status: string): boolean {
  return OPEN_HOST_STATES.has(status.toLowerCase());
}

/// Whether this event is sitting with the platform awaiting a decision.
export function awaitingReview(status: string): boolean {
  return AWAITING_REVIEW_STATES.has(status.toLowerCase());
}
