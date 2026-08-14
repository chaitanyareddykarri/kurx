import { ReactNode } from "react";
import { Avatar } from "./avatar";
import { Badge } from "./badge";

export interface UserCardProps {
  name: string;
  username?: string | null;
  avatarSrc?: string | null;
  verified?: boolean;
  subtitle?: string;
  href?: string;
  action?: ReactNode;
  className?: string;
}

/** A compact person identity — the shared primitive for allies, search results, attendee/member
 * lists, and any other place a user is rendered as a row/card. `action` sits outside the `href`
 * link (never nested inside it) so a "Connect"/"Accept" button never ends up inside an `<a>`. */
export function UserCard({ name, username, avatarSrc, verified, subtitle, href, action, className = "" }: UserCardProps) {
  const identity = (
    <div className="flex min-w-0 items-center gap-3">
      <Avatar name={name} src={avatarSrc ?? undefined} size={44} />
      <div className="min-w-0 flex-1">
        <div className="flex items-center gap-1.5">
          <p className="truncate font-semibold text-text">{name}</p>
          {verified && <Badge tone="accent">Verified</Badge>}
        </div>
        {username && <p className="truncate text-sm text-muted">@{username}</p>}
        {subtitle && <p className="truncate text-xs text-muted">{subtitle}</p>}
      </div>
    </div>
  );

  return (
    <div className={`flex items-center gap-3 rounded-lg border border-border bg-surface p-4 ${className}`}>
      {href ? (
        <a href={href} className="min-w-0 flex-1 rounded-md transition hover:opacity-80 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent">
          {identity}
        </a>
      ) : (
        <div className="min-w-0 flex-1">{identity}</div>
      )}
      {action && <div className="shrink-0">{action}</div>}
    </div>
  );
}
