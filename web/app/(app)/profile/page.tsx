import Link from "next/link";
import {
  Award, Bell, Bookmark, Building2, CalendarPlus, Handshake, Hourglass, IdCard, LayoutDashboard,
  MailOpen, MessagesSquare, Mic, Settings, ShieldCheck, Ticket, Trophy, UserRound, Users
} from "lucide-react";
import type { LucideIcon } from "lucide-react";
import { Avatar } from "@kurx/ui";
import { Card } from "@/components/ui/card";
import { getMe } from "@/lib/api";
import { requireSession } from "@/lib/session";

export const metadata = { title: "Profile" };

/**
 * The Profile hub (D-305).
 *
 * This route used to `redirect("/settings")` — it had been a byte-for-byte duplicate of Settings
 * (D-212). That left web with **no profile hub at all**, while Flutter's `profile_page.dart` has had a
 * nineteen-item one for months: the header "Profile" link went to a settings form. D-305 makes Profile
 * the home of **Create Event**, which needs somewhere to live, so the hub becomes real and the two
 * clients finally describe the same product.
 *
 * Every tile points at a route that **exists**. Flutter's Points, Orders, Participations, Waitlist,
 * Devices, Org invitations and Help have no web equivalent yet and are deliberately absent rather than
 * present-and-dead — a tile that 404s is worse than a tile that is missing.
 */

type Tile = { href: string; label: string; icon: LucideIcon; hint?: string };

/** Your event life. Workspace is the activity hub — it is not a dashboard and not the creation door. */
const ACTIVITY: Tile[] = [
  { href: "/workspace", label: "Workspace", icon: LayoutDashboard, hint: "Events you host or take part in" },
  { href: "/tickets", label: "My tickets", icon: Ticket },
  { href: "/participations", label: "My participations", icon: Mic, hint: "Speaking, judging, mentoring, volunteering" },
  { href: "/invitations", label: "Invitations", icon: MailOpen },
  { href: "/waitlist", label: "My waitlist", icon: Hourglass },
  { href: "/saved", label: "Saved events", icon: Bookmark }
];

/** People and conversations. */
const SOCIAL: Tile[] = [
  { href: "/allies", label: "My allies", icon: Handshake },
  { href: "/groups", label: "Groups", icon: Users },
  { href: "/chats", label: "Messages", icon: MessagesSquare },
  { href: "/notifications", label: "Notifications", icon: Bell },
  { href: "/points", label: "Points & badges", icon: Trophy }
];

/** Identity, authority and account. `Representing` is authority to act for an organization — it is not
 *  an account setting, which is why it sits here beside verification rather than inside Settings. */
const ACCOUNT: Tile[] = [
  { href: "/settings/identity", label: "Verification", icon: IdCard, hint: "Identity, PAN and bank status" },
  { href: "/settings/representing", label: "Representing", icon: ShieldCheck, hint: "Organizations you may act for" },
  { href: "/org-invitations", label: "Organization invitations", icon: Building2 },
  { href: "/settings/security", label: "Security", icon: ShieldCheck },
  { href: "/settings", label: "Settings", icon: Settings }
];

function TileGrid({ title, tiles }: { title: string; tiles: Tile[] }) {
  return (
    <section>
      <h2 className="text-label text-muted">{title}</h2>
      <div className="mt-2 grid gap-2 sm:grid-cols-2">
        {tiles.map(({ href, label, icon: Icon, hint }) => (
          <Link
            key={href}
            href={href}
            className="flex min-h-11 items-start gap-3 rounded-lg border border-border bg-surface p-4 transition duration-fast hover:border-accent"
          >
            <Icon size={18} className="mt-0.5 shrink-0 text-muted" aria-hidden="true" />
            <span className="min-w-0">
              <span className="block text-body text-text">{label}</span>
              {hint ? <span className="mt-0.5 block text-caption text-muted">{hint}</span> : null}
            </span>
          </Link>
        ))}
      </div>
    </section>
  );
}

export default async function ProfileHubPage() {
  const session = await requireSession();
  const me = await getMe(session.accessToken);
  const displayName = me.name?.trim() || "Your profile";

  return (
    <div className="mx-auto max-w-3xl space-y-6">
      <Card>
        <div className="flex items-center gap-4">
          {/*
            `avatar_url` is the presigned companion to `avatar_key` — the key alone is a STORAGE KEY and
            resolves against this origin, which is what made every avatar fall back to initials. The
            fallback still covers an account with no picture, because a null key yields a null URL.
          */}
          <Avatar name={displayName} src={me.avatar_url ?? undefined} size={64} />
          <div className="min-w-0">
            <h1 className="truncate text-h2 text-text">{displayName}</h1>
            {me.username ? (
              <Link href={`/u/${me.username}`} className="text-body text-accent-text hover:underline">
                @{me.username}
              </Link>
            ) : (
              <Link href="/settings" className="text-body text-accent-text hover:underline">
                Claim your username
              </Link>
            )}
          </div>
          <span className="flex-1" />
          <Link
            href="/settings"
            className="inline-flex min-h-11 items-center gap-2 rounded-md border border-border px-3 text-body text-text transition duration-fast hover:border-accent"
          >
            <UserRound size={16} aria-hidden="true" /> Edit
          </Link>
        </div>
      </Card>

      {/*
        Create Event is the primary action on Profile (D-305) — it is no longer reached from Workspace
        or from the primary nav. The link opens the eligibility gate, not the form: the gate reuses the
        caller's existing verification state and asks Public or Private before any event detail is typed.
      */}
      <Link
        href="/host/events/new"
        className="flex min-h-11 items-center gap-3 rounded-lg border border-accent bg-accent/10 p-4 transition duration-fast hover:bg-accent/15"
      >
        <CalendarPlus size={20} className="shrink-0 text-accent-text" aria-hidden="true" />
        <span className="min-w-0">
          <span className="block text-body font-semibold text-text">Create event</span>
          <span className="mt-0.5 block text-caption text-muted">
            Host something on Kurx — free or ticketed
          </span>
        </span>
      </Link>

      <TileGrid title="Your events" tiles={ACTIVITY} />
      <TileGrid title="People" tiles={SOCIAL} />
      <TileGrid title="Account" tiles={ACCOUNT} />
    </div>
  );
}
