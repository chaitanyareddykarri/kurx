import { EmptyState } from "@kurx/ui";
import { myPoints, myBadges, type PointsSummary, type Badge as BadgeDto } from "@/lib/api";
import { requireSession } from "@/lib/session";
import { formatDate } from "@/lib/formatters";

export const metadata = { title: "Points & badges" };

/**
 * Points and badges (D-305).
 *
 * `GET /v1/me/points` and `GET /v1/me/badges` shipped with the gamification module and web called
 * **neither**. Both are read-only; points are awarded by the server and a badge is earned, never
 * claimed here.
 *
 * A badge's `icon_key` is a storage key and is **not** rendered as an image — nothing presigns it
 * (D-302), so drawing it would produce a broken image on every badge. Name and type carry the meaning.
 */
export default async function PointsPage() {
  const session = await requireSession();
  const [points, badges] = await Promise.all([
    myPoints(session.accessToken).catch(() => ({ total_points: 0, history: [] } as PointsSummary)),
    myBadges(session.accessToken).catch(() => [] as BadgeDto[])
  ]);

  return (
    <div className="mx-auto max-w-2xl space-y-8">
      <section>
        <h1 className="text-h1 text-text">Points &amp; badges</h1>
        <p className="mt-2 text-display text-accent-text">{points.total_points.toLocaleString("en-IN")}</p>
        <p className="text-caption text-muted">points earned</p>
      </section>

      <section>
        <h2 className="text-h2 text-text">Badges</h2>
        {badges.length === 0 ? (
          <p className="mt-2 text-body text-muted">No badges yet — they arrive as you take part in events.</p>
        ) : (
          <ul className="mt-3 grid gap-2 sm:grid-cols-2">
            {badges.map((b) => (
              <li key={b.id} className="rounded-lg border border-border bg-surface p-4">
                <p className="text-body font-semibold text-text">{b.name}</p>
                <p className="mt-0.5 text-caption text-muted">{b.description}</p>
                <p className="mt-1 text-caption text-muted">
                  Earned {formatDate(b.earned_at, "en-IN", { day: "numeric", month: "short", year: "numeric" })}
                </p>
              </li>
            ))}
          </ul>
        )}
      </section>

      <section>
        <h2 className="text-h2 text-text">History</h2>
        {points.history.length === 0 ? (
          <div className="mt-3">
            <EmptyState icon="workspace_premium" title="No points yet" message="Register for an event to start earning." />
          </div>
        ) : (
          <ul className="mt-3 space-y-2">
            {points.history.map((h) => (
              <li key={h.id} className="flex items-baseline justify-between gap-3 rounded-md border border-border p-3">
                <span className="min-w-0">
                  <span className="block text-body text-text">{h.reason || h.source}</span>
                  <span className="block text-caption text-muted">
                    {formatDate(h.created_at, "en-IN", { day: "numeric", month: "short", year: "numeric" })}
                  </span>
                </span>
                {/* Points can be deducted (a refund reverses an award), so the sign is shown rather than assumed. */}
                <span className={`text-label ${h.points < 0 ? "text-danger" : "text-accent-text"}`}>
                  {h.points > 0 ? `+${h.points}` : h.points}
                </span>
              </li>
            ))}
          </ul>
        )}
      </section>
    </div>
  );
}
