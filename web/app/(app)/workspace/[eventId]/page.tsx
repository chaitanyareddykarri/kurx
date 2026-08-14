import Link from "next/link";
import { Award, MessagesSquare, Newspaper } from "lucide-react";
import { requireSession } from "@/lib/session";

export const metadata = { title: "Participant workspace" };

/// The **participant workspace** for one event — what opens once a registration is confirmed.
///
/// A launcher, not a re-implementation: every destination is a page this app already ships.
///
/// This surface is deliberately thinner than the Flutter one, because web genuinely has fewer
/// participant screens. A tile is only listed if it resolves for an *attendee*:
///
/// * **Results / Leaderboard** — web has no competition or leaderboard page at all (Flutter does).
///   Pointing at `/host/events/[id]/analytics` would send an attendee at an organiser-only route.
/// * **Feedback** — reviews live on `/e/[slug]`, and a slug cannot be derived from an event id
///   without another read; linking `/e/[eventId]` would simply 404.
/// * **Tasks, Resources/Files, Attendance** — no endpoint exists at all (the `submissions`
///   capability slug is declared in the registry with nothing implementing it).
///
/// Each of those is a tile the moment its page exists — an absent tile beats a dead one.
export default async function ParticipantWorkspacePage({ params }: { params: { eventId: string } }) {
  await requireSession();
  const { eventId } = params;

  const tiles = [
    { href: `/chats/${eventId}`, label: "Event chat", icon: MessagesSquare },
    { href: `/posts/event/${eventId}`, label: "Event posts", icon: Newspaper },
    { href: "/certificates", label: "Certificates", icon: Award }
  ];

  return (
    <div className="mx-auto max-w-3xl">
      <h1 className="text-2xl font-semibold text-text">Participant workspace</h1>
      <div className="mt-4 grid grid-cols-2 gap-3 sm:grid-cols-3">
        {tiles.map((t) => {
          const Icon = t.icon;
          return (
            <Link
              key={t.label}
              href={t.href}
              className="rounded-lg border border-border bg-surface p-4 transition duration-fast hover:border-accent focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
            >
              <Icon size={18} aria-hidden className="text-accent-text" />
              <span className="mt-2 block text-sm font-medium text-text">{t.label}</span>
            </Link>
          );
        })}
      </div>
    </div>
  );
}
