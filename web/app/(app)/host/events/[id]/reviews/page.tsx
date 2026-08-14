import { requireEventOrg } from "@/lib/event-org";
import { listEventReviews } from "@/lib/api";
import { Card, Stat } from "@kurx/ui";
import { formatDate } from "@/lib/formatters";

export default async function EventReviewsPage({ params }: { params: { id: string } }) {
  const { orgId } = await requireEventOrg(params.id);

  const { items, summary } = await listEventReviews(params.id, 1, 50);

  return (
    <div className="space-y-4">
      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
        <Stat label="Average rating" value={summary.count > 0 ? `${summary.average.toFixed(1)} / 5` : "—"} />
        <Stat label="Reviews" value={String(summary.count)} />
      </div>
      {items.length === 0 ? (
        <Card><p className="text-sm text-muted">No reviews yet. Attendees can review after the event.</p></Card>
      ) : (
        <div className="space-y-3">
          {items.map((r) => (
            <Card key={r.id}>
              <div className="flex flex-wrap items-center justify-between gap-2">
                <p className="font-semibold">
                  {/* The stars were the only statement of the score, and a screen reader reads them
                      as "black star black star black star". The number is the accessible answer; the
                      glyphs are the picture of it. */}
                  <span role="img" aria-label={`${r.rating} out of 5`}>
                    <span aria-hidden className="text-accent-text">{"★".repeat(r.rating)}</span>
                    <span aria-hidden className="text-muted">{"★".repeat(5 - r.rating)}</span>
                  </span>
                  {r.title ? <span className="ml-2 text-text">{r.title}</span> : null}
                </p>
                <p className="text-xs text-muted">
                  {r.is_anonymous ? (
                    "Anonymous"
                  ) : r.author_username ? (
                    <a href={`/u/${r.author_username}`} className="font-medium text-text hover:text-accent-text">
                      {r.author_name ?? "Attendee"}
                    </a>
                  ) : (
                    r.author_name ?? "Attendee"
                  )}
                  {r.is_verified ? " · verified attendee" : ""} · {formatDate(r.created_at, "en-IN", { day: "numeric", month: "short", year: "numeric" })}
                </p>
              </div>
              {r.body ? <p className="mt-2 whitespace-pre-wrap text-sm text-text">{r.body}</p> : null}
            </Card>
          ))}
        </div>
      )}
    </div>
  );
}
