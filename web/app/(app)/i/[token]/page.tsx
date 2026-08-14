import Link from "next/link";
import { CalendarClock, MapPin, Ticket } from "lucide-react";
import { getPublicInviteLink } from "@/lib/api";
import { formatDate } from "@/lib/formatters";
import { currentSession } from "@/lib/session";
import { Card } from "@kurx/ui";
import { RedeemInviteLink } from "@/components/invitations/redeem-invite-link";

/**
 * D-266 M6 (D9 Method B) — the invite-link landing page.
 *
 * This is the URL organisers copy out of the invite-link manager. **Deliberately renders for signed-out
 * visitors**: Method B exists to reach people who are not on Kurx yet, so the pre-flight
 * (`GET /v1/public/invite-links/{token}`) is anonymous and only the redeem itself requires an account.
 *
 * A link grants **permission to register and nothing more** — the copy says so, because a link that looks
 * like a ticket is the failure mode that turns an invitation into a support conversation.
 */
export default async function InviteLinkPage({ params }: { params: { token: string } }) {
  const session = await currentSession();

  let link: Awaited<ReturnType<typeof getPublicInviteLink>> | null = null;
  let notFound = false;
  try {
    link = await getPublicInviteLink(params.token);
  } catch {
    // The pre-flight 404s for an unknown token. Anything else (network, 500) lands here too — both mean
    // "we can't tell you anything about this link", which is the same message to the holder.
    notFound = true;
  }

  if (notFound || !link) {
    return (
      <Shell>
        <h1 className="text-lg font-semibold text-text">This invite link isn&apos;t valid</h1>
        <p className="mt-2 text-sm text-muted">
          The link may have been mistyped, or the organiser may have removed it. Ask whoever shared it for a
          new one.
        </p>
      </Shell>
    );
  }

  return (
    <Shell>
      <p className="text-xs font-semibold uppercase tracking-wide text-accent-text">You&apos;ve been invited</p>
      <h1 className="mt-1 text-lg font-semibold text-text">{link.event_title}</h1>

      <p className="mt-2 flex flex-wrap items-center gap-x-3 gap-y-1 text-xs text-muted">
        <span className="inline-flex items-center gap-1">
          <CalendarClock size={12} aria-hidden /> {formatDate(link.starts_at, "en-IN", { day: "numeric", month: "short", year: "numeric" })}
        </span>
        {link.venue_name || link.city ? (
          <span className="inline-flex items-center gap-1">
            <MapPin size={12} /> {[link.venue_name, link.city].filter(Boolean).join(", ")}
          </span>
        ) : null}
        {link.seats_remaining !== null ? (
          <span className="inline-flex items-center gap-1">
            <Ticket size={12} /> {link.seats_remaining} place{link.seats_remaining === 1 ? "" : "s"} left
          </span>
        ) : null}
      </p>

      {!link.is_usable ? (
        <p role="alert" className="mt-4 rounded-md border border-warning/40 bg-warning/10 px-3 py-2 text-sm text-text">
          {UNUSABLE_COPY[link.reason ?? ""] ?? "This link can no longer be used."}
        </p>
      ) : !session ? (
        <div className="mt-4 space-y-3">
          <p className="text-sm text-muted">
            Sign in to accept this invitation. You&apos;ll come straight back here.
          </p>
          <Link href={`/#login?next=${encodeURIComponent(`/i/${params.token}`)}`}
            className="inline-flex h-10 items-center rounded-md bg-accent px-4 text-sm font-semibold text-white">
            Sign in to continue
          </Link>
        </div>
      ) : (
        <RedeemInviteLink
          token={params.token}
          requiresPasscode={link.requires_passcode}
          eventSlug={link.event_slug}
        />
      )}

      <p className="mt-5 border-t border-border pt-4 text-xs text-muted">
        Accepting an invite link lets you register for this event. It doesn&apos;t reserve a place or issue a
        ticket — if the event charges, you still pay when you register.
      </p>
    </Shell>
  );
}

/// Presentation for the server's refusal codes. Unmapped codes fall through to a generic line rather than
/// leaking a raw identifier to someone who is not a developer.
const UNUSABLE_COPY: Record<string, string> = {
  invite_link_expired: "This invite link has expired. Ask the organiser for a new one.",
  invite_link_revoked: "The organiser has turned this link off.",
  invite_link_exhausted: "Every place on this link has been taken.",
};

function Shell({ children }: { children: React.ReactNode }) {
  return (
    <div className="mx-auto w-full max-w-lg p-4">
      <Card>{children}</Card>
    </div>
  );
}
