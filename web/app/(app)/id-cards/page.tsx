import { IdCard as IdCardIcon } from "lucide-react";
import { Badge, EmptyState, LinkButton } from "@kurx/ui";
import { listMyIdCards } from "@/lib/api";
import { requireSession } from "@/lib/session";

export const metadata = { title: "ID Cards" };

export default async function IdCardsPage() {
  const session = await requireSession();

  // A failure here is a temporary read problem, not "you have no cards" — the two must never render
  // the same, because the second is a statement about the user's college that we would be inventing.
  let cards;
  try {
    cards = await listMyIdCards(session.accessToken);
  } catch {
    return (
      <Shell>
        <div role="status" className="rounded-lg border border-dashed border-border bg-surface p-6 text-center">
          <p className="text-body text-text">Your ID cards couldn&apos;t be loaded.</p>
          <p className="mt-1 text-sm text-muted">This is a temporary problem on our side. Try refreshing.</p>
        </div>
      </Shell>
    );
  }

  if (cards.length === 0) {
    return (
      <Shell>
        <EmptyState
          icon={<IdCardIcon size={32} />}
          title="No ID cards yet"
          message="ID cards are issued by verified event creators to verified attendees or members of their events. You can't create one for yourself, which helps make it proof of participation. Ask the event creator or organizer to issue yours."
        />
      </Shell>
    );
  }

  return (
    <Shell>
      <ul className="grid gap-3 sm:grid-cols-2">
        {cards.map((c) => (
          <li key={c.id} className="rounded-lg border border-border bg-surface p-4">
            <div className="flex items-start justify-between gap-3">
              <div className="min-w-0">
                <p className="truncate text-body font-medium text-text">{c.org_name}</p>
                <p className="mt-0.5 text-sm text-muted">
                  {c.card_number}
                  {c.student_id ? ` · ${c.student_id}` : ""}
                </p>
              </div>
              <Badge tone={c.status === "active" ? "success" : c.status === "revoked" ? "danger" : "neutral"}>
                {c.status}
              </Badge>
            </div>
            <p className="mt-2 text-sm text-muted">
              {c.valid_until ? `Valid until ${c.valid_until}` : "No expiry"}
              {c.generated_at ? "" : " · not generated yet"}
            </p>
            <div className="mt-3">
              <LinkButton href={`/id-cards/${c.id}`} variant="secondary">
                {c.is_revoked ? "View" : "Edit card"}
              </LinkButton>
            </div>
          </li>
        ))}
      </ul>
    </Shell>
  );
}

function Shell({ children }: { children: React.ReactNode }) {
  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-3xl font-semibold text-text">ID Cards</h1>
        <p className="mt-2 text-sm text-muted">
          Cards issued to you by an organization. Edit how yours looks, then generate it to download or print.
        </p>
      </div>
      {children}
    </div>
  );
}
