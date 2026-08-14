import Link from "next/link";
import { BarChart3, CalendarPlus, Hourglass, LayoutDashboard, Users } from "lucide-react";
import { Badge, Card, LinkButton, Stat } from "@kurx/ui";
import { requireSession } from "@/lib/session";
import { listMyEvents, listMyParticipations, section, type MyEventRow } from "@/lib/api";
import { awaitingReview, hostWorkspaceOpen } from "@/lib/workspace";
import { eventStatusOf } from "@/lib/event-status";
import { formatDate } from "@/lib/formatters";

export const metadata = { title: "Workspace" };

/// Statuses where the host workspace is genuinely open. Everything before approval shows its state
/// instead of being presented as a working workspace. The rules live in `lib/workspace.ts` so they can
/// be tested — this file is an async server component and cannot be rendered in vitest.

/// Views over the caller's own events. These are STATUS FILTERS on one list, not separate containers —
/// the whole point of D-267 is that a person's events are one flat set that belongs to them. Organizations
/// are never a view here; an event names the org it represents on its own row.
const VIEWS = [
  { key: "hosted", label: "Hosted Events", match: () => true },
  { key: "drafts", label: "Drafts", match: (s: string) => s === "draft" },
  { key: "pending", label: "Pending Approval", match: (s: string) => awaitingReview(s) },
  { key: "archived", label: "Archived", match: (s: string) => s === "archived" || s === "cancelled" }
] as const;

type ViewKey = (typeof VIEWS)[number]["key"];

const PAGE_SIZE = 50;

export default async function WorkspacePage({
  searchParams
}: {
  searchParams: { view?: string };
}) {
  const session = await requireSession();
  const view = (VIEWS.find((v) => v.key === searchParams.view)?.key ?? "hosted") as ViewKey;

  // One call for every event this person hosts, whoever they represent (D-267).
  //
  // `.catch(() => [])` here meant an outage rendered "No events yet. Create one." to a host who has
  // run twenty (D-235). Both reads are classified now, and a failure says so instead of asserting an
  // absence it cannot support.
  const [eventsResult, participationsResult] = await Promise.all([
    section(listMyEvents(session.accessToken, 1, PAGE_SIZE)),
    section(listMyParticipations(session.accessToken))
  ]);

  const hostedFailed = eventsResult.state !== "ok";
  const hosted: MyEventRow[] = eventsResult.state === "ok" ? eventsResult.data.items : [];
  // The canonical count, never `hosted.length` (D-231): the list is one page of at most 50, so
  // measuring the array under-reported anybody past that and called it their total.
  const hostedTotal = eventsResult.state === "ok" ? eventsResult.data.total : null;
  const truncated = hostedTotal !== null && hostedTotal > hosted.length;

  const participationsFailed = participationsResult.state !== "ok";
  // `invited`/`declined`/`removed` are not participation — only an accepted role unlocks a workspace.
  const participating = (participationsResult.state === "ok" ? participationsResult.data : [])
    .filter((p) => p.state === "accepted");

  const matcher = VIEWS.find((v) => v.key === view)!.match;
  const shown = hosted.filter((e) => matcher(e.status.toLowerCase()));

  return (
    <div className="mx-auto max-w-3xl space-y-8">
      <section className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <h1 className="text-2xl font-semibold text-text">Workspace</h1>
          <p className="mt-1 text-sm text-muted">
            Every event you run or take part in. Host workspaces open once an event is approved.
          </p>
        </div>
        {/*
          Create event, restored here by D-333, which reverses D-305's navigation half only.
          D-305 removed this button because a primary creation action made Workspace read as an
          organizer dashboard; the surface rule it protects is unchanged and still binding — Workspace
          lists participated and approved-created events and gains no organizer tooling from this link.

          What is NOT reverted: the destination. This opens `/host/events/new`, the D-305 eligibility
          gate — verification and the Public/Private choice still happen BEFORE any event detail is
          typed. The door moved back; what is behind it did not. Profile keeps its entry point too, so
          this is a second door, not a relocation.
        */}
        <LinkButton href="/host/events/new">
          <CalendarPlus size={16} aria-hidden="true" />
          Create event
        </LinkButton>
      </section>

      <section aria-label="Overview" className="grid gap-3 grid-cols-2 sm:grid-cols-4">
        {/* A count we could not fetch is not a count of zero — it renders an em dash rather than
            telling somebody they host nothing because a request failed. */}
        <Stat label="Hosting" value={hostedFailed ? "—" : String(hostedTotal ?? hosted.length)} />
        <Stat label="Drafts" value={hostedFailed ? "—" : String(countIn(hosted, "drafts"))} />
        <Stat label="Pending approval" value={hostedFailed ? "—" : String(countIn(hosted, "pending"))} />
        <Stat label="Participating" value={participationsFailed ? "—" : String(participating.length)} />
      </section>

      <section>
        <nav className="flex gap-1 overflow-x-auto border-b border-border" aria-label="Event views">
          {VIEWS.map((v) => (
            <Link
              key={v.key}
              href={v.key === "hosted" ? "/workspace" : `/workspace?view=${v.key}`}
              // The current view was marked by colour and a border alone, with nothing programmatic —
              // a screen-reader user could not tell which of the four they were looking at.
              aria-current={v.key === view ? "page" : undefined}
              className={`inline-flex min-h-11 items-center whitespace-nowrap border-b-2 px-4 text-sm font-medium transition duration-fast focus-visible:outline focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-accent ${
                v.key === view ? "border-accent text-text" : "border-transparent text-muted hover:text-text"
              }`}
            >
              {v.label}
            </Link>
          ))}
        </nav>

        {hostedFailed ? (
          <div role="status" className="mt-4 rounded-lg border border-dashed border-border bg-surface p-6 text-center">
            <p className="text-body text-text">Your events couldn&apos;t be loaded.</p>
            <p className="mt-1 text-sm text-muted">
              This is a temporary problem on our side, not a change to your events. Try refreshing.
            </p>
          </div>
        ) : shown.length === 0 ? (
          <p className="mt-4 text-sm text-muted">
            {view === "hosted" ? (
              <>
                {/* Contextual, not a primary door: it points at Profile, where creation now lives
                    (D-305), rather than deep-linking past the gate into the form. */}
                No events yet. Start one from{" "}
                <Link
                  href="/profile"
                  className="text-accent-text underline-offset-2 hover:underline focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
                >
                  your profile
                </Link>
                .
              </>
            ) : (
              "Nothing here."
            )}
          </p>
        ) : (
          <>
            <ul className="mt-4 space-y-2">
              {shown.map((e) => {
                const open = hostWorkspaceOpen(e.status);
                const status = eventStatusOf(e.status);
                return (
                  <li key={e.id}>
                    <Link
                      href={`/host/events/${e.id}`}
                      className="flex items-center gap-3 rounded-lg border border-border bg-surface p-3 transition duration-fast hover:border-accent focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
                    >
                      {open ? (
                        <LayoutDashboard size={18} aria-hidden className="shrink-0 text-accent-text" />
                      ) : (
                        <Hourglass size={18} aria-hidden className="shrink-0 text-muted" />
                      )}
                      <span className="min-w-0 flex-1">
                        <span className="block truncate text-sm font-medium text-text">{e.title}</span>
                        <span className="block truncate text-xs text-muted">
                          {/* The row carried a date, a city and a venue and showed none of them; the
                              only detail was a raw status, so "pendingreview" reached the host. */}
                          {[
                            open ? "Host workspace" : "Opens after approval",
                            formatDate(e.starts_at, "en-IN", { day: "numeric", month: "short", year: "numeric" }),
                            e.city || null,
                            // Representation is a property of the event, shown here — never a folder
                            // you had to open to find it. A personal event represents nobody.
                            e.representation.kind === "organization"
                              ? `representing ${e.representation.organization_name}`
                              : null
                          ].filter(Boolean).join(" · ")}
                        </span>
                      </span>
                      <Badge tone={status.tone}>{status.label}</Badge>
                    </Link>
                  </li>
                );
              })}
            </ul>
            {truncated && view === "hosted" ? (
              // Silently showing 50 of 80 would let a host conclude the rest are gone.
              <p className="mt-3 text-caption text-muted">
                Showing the {hosted.length} most recently updated of {hostedTotal}.
              </p>
            ) : null}
          </>
        )}
      </section>

      <section>
        <h2 className="text-sm font-semibold text-text">Participating Events</h2>
        {participationsFailed ? (
          <p role="status" className="mt-2 text-sm text-muted">
            Your participations couldn&apos;t be loaded. Try refreshing.
          </p>
        ) : participating.length === 0 ? (
          <p className="mt-2 text-sm text-muted">Register for an event and its workspace appears here.</p>
        ) : (
          <ul className="mt-3 space-y-2">
            {participating.map((p) => (
              <li key={p.id}>
                <Link
                  href={`/workspace/${p.event_id}`}
                  className="flex items-center gap-3 rounded-lg border border-border bg-surface p-3 transition duration-fast hover:border-accent focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
                >
                  <Users size={18} aria-hidden className="shrink-0 text-accent-text" />
                  <span className="min-w-0 flex-1">
                    {/* `GET /v1/me/participations` returns no event title — only an id and a role —
                        so the row is titled by role. `/v1/orders` denormalises `event_title` for
                        exactly this reason; until this endpoint does the same, a second read per row
                        is the only alternative and is not worth an N+1 on every workspace load. */}
                    <span className="block truncate text-sm font-medium text-text">
                      {p.custom_label ?? roleLabel(p.role_slug)}
                    </span>
                    <span className="block text-xs text-muted">Participant workspace</span>
                  </span>
                </Link>
              </li>
            ))}
          </ul>
        )}
      </section>

      <section>
        {/*
          A "Templates" tile linked to `/host/templates`, which has no page — only
          `/host/templates/{certificates,editor,invites}` exist, and all three are `WorkflowPage`
          placeholders. The link 404'd on click. Removed rather than repointed: sending a host to a
          placeholder is not better than not offering the tile, and building the index is Phase 21's
          call. This closes REG-003.
        */}
        <Card>
          <Link
            href="/host/analytics"
            className="flex items-center gap-3 rounded-md transition duration-fast hover:text-accent-text focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
          >
            <BarChart3 size={18} aria-hidden className="text-accent-text" />
            <span className="text-sm font-medium text-text">Analytics</span>
          </Link>
        </Card>
      </section>
    </div>
  );
}

function countIn(events: MyEventRow[], key: "drafts" | "pending"): number {
  const match = VIEWS.find((v) => v.key === key)!.match;
  return events.filter((e) => match(e.status.toLowerCase())).length;
}

function roleLabel(slug: string) {
  const spaced = slug.replace(/_/g, " ");
  return spaced ? spaced[0].toUpperCase() + spaced.slice(1) : "Participant";
}
