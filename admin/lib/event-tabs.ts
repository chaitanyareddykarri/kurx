// Shared between the server page (admin/app/(console)/events/page.tsx) and the client workspace
// components — kept in its own pure module so importing it from a client component never risks
// pulling in the page's server-only session code.
export const TABS = [
  { id: "upcoming", label: "Upcoming" },
  { id: "live", label: "Live" },
  { id: "completed", label: "Completed" },
  { id: "draft", label: "Draft" },
  { id: "review", label: "Pending Review" },
  { id: "cancelled", label: "Cancelled" },
  { id: "archived", label: "Archived" },
  { id: "all", label: "All Events" }
] as const;
export type TabId = (typeof TABS)[number]["id"];

// D-266 M4 retired `inreview`. The backend status filter takes ONE state and silently ignores one it
// cannot parse — `inreview` here made this tab list every event instead of none, which is why it went
// unnoticed. The full queue (PendingReview + UnderReview + outcomes) lives on /events/review; this tab
// is the workspace's plain status filter.
export const TAB_STATUS: Record<TabId, string | undefined> = {
  upcoming: "published", live: "live", completed: "completed", draft: "draft",
  review: "pendingreview", cancelled: "cancelled", archived: "archived", all: undefined
};

export function parseTab(raw?: string): TabId {
  return (TABS.find((t) => t.id === raw)?.id ?? "all") as TabId;
}
