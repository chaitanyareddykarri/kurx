import NextLink from "next/link";
import { ReactNode } from "react";

import { Badge } from "./badge";

/**
 * Content components: the card family, metadata rows, stats, timelines and media
 * frames. Data-shape agnostic on purpose — web, admin and the Flutter port all
 * describe an event differently, so these take primitives rather than a DTO.
 */

/** A plain surface. Radius `lg` is the ticket corner (visual-identity P5). */
export function Card({ children, className = "" }: { children: ReactNode; className?: string }) {
  return <div className={`rounded-lg border border-border bg-surface p-5 shadow-sm ${className}`}>{children}</div>;
}

/**
 * A card whose whole area is a link.
 *
 * The link is one element with a stretched overlay rather than a wrapper around
 * everything, which matters for three reasons: the card is a **single tab stop**
 * instead of one per link inside it; the accessible name is the title alone
 * rather than every word in the card read aloud; and text inside stays
 * selectable. Nested controls (a save button, a menu) opt out with
 * `relative z-raised`, which `CardAction` does.
 */
export function LinkCard({
  href,
  label,
  children,
  className = ""
}: {
  href: string;
  /** The accessible name — normally the title text. */
  label: string;
  children: ReactNode;
  className?: string;
}) {
  return (
    <div
      className={`group relative rounded-lg border border-border bg-surface shadow-sm transition duration-fast ease-kurx focus-within:border-accent hover:border-accent/70 ${className}`}
    >
      <NextLink
        href={href}
        aria-label={label}
        className="absolute inset-0 z-base rounded-lg focus:outline-none focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
      />
      {children}
    </div>
  );
}

/** Wraps a control inside a `LinkCard` so it stays clickable above the overlay. */
export function CardAction({ children, className = "" }: { children: ReactNode; className?: string }) {
  return <div className={`relative z-raised ${className}`}>{children}</div>;
}

/**
 * An icon + text metadata line (date, venue, capacity).
 *
 * The icon is always decorative: it repeats what the text says, and announcing
 * "calendar, 14 March" adds nothing over "14 March".
 */
export function MetaRow({
  icon,
  children,
  className = ""
}: {
  icon?: ReactNode;
  children: ReactNode;
  className?: string;
}) {
  return (
    <p className={`flex items-center gap-sm text-caption text-muted ${className}`}>
      {icon ? (
        <span aria-hidden="true" className="shrink-0">
          {icon}
        </span>
      ) : null}
      <span className="min-w-0 truncate">{children}</span>
    </p>
  );
}

/**
 * A fixed-ratio media container.
 *
 * The ratio is reserved before the image loads, so nothing below it jumps when
 * it arrives — cumulative layout shift is a Phase 45 gate, and the cheapest place
 * to prevent it is here rather than per call site.
 */
export function MediaFrame({
  ratio = "16/9",
  children,
  className = ""
}: {
  ratio?: "16/9" | "3/2" | "1/1";
  children: ReactNode;
  className?: string;
}) {
  const aspect = { "16/9": "aspect-video", "3/2": "aspect-[3/2]", "1/1": "aspect-square" }[ratio];
  return (
    <div className={`${aspect} w-full overflow-hidden rounded-lg bg-elevated ${className}`}>{children}</div>
  );
}

export function Stat({
  label,
  value,
  trend,
  tone = "neutral"
}: {
  label: string;
  value: string;
  trend?: string;
  tone?: "neutral" | "up" | "down";
}) {
  const trendTone = { neutral: "text-muted", up: "text-success", down: "text-danger" }[tone];
  return (
    <div className="rounded-lg border border-border bg-surface p-4">
      <p className="text-caption text-muted">{label}</p>
      <div className="mt-2 flex items-end justify-between gap-3">
        <strong className="text-h2 text-text">{value}</strong>
        {trend ? (
          <span className={`text-caption ${trendTone}`}>
            {/* Direction in words as well as colour and arrow glyph. */}
            {tone !== "neutral" ? <span className="sr-only">{tone === "up" ? "up " : "down "}</span> : null}
            {trend}
          </span>
        ) : null}
      </div>
    </div>
  );
}

export type TimelineEntry = {
  id: string;
  title: string;
  detail?: string;
  timestamp: string;
  /** `done` uses teal — an attestation that something happened (D-286). */
  state?: "done" | "current" | "pending";
};

/**
 * A vertical timeline (event schedule, review history, audit trail).
 *
 * An ordered list, so position and count are structural. State is carried in
 * visually-hidden text as well as the marker's colour.
 */
export function Timeline({ entries, className = "" }: { entries: TimelineEntry[]; className?: string }) {
  return (
    <ol className={`space-y-lg ${className}`}>
      {entries.map((e) => {
        const state = e.state ?? "pending";
        const marker = {
          done: "border-teal bg-teal",
          current: "border-accent bg-accent",
          pending: "border-border-strong bg-surface"
        }[state];
        return (
          <li key={e.id} className="relative flex gap-md pl-1">
            <span aria-hidden="true" className={`mt-1.5 h-2.5 w-2.5 shrink-0 rounded-full border-2 ${marker}`} />
            <div className="min-w-0 flex-1">
              <p className="text-label text-text">
                {e.title}
                <span className="sr-only"> — {state}</span>
              </p>
              {e.detail ? <p className="mt-0.5 text-caption text-muted">{e.detail}</p> : null}
              <p className="mt-0.5 text-micro text-muted">{e.timestamp}</p>
            </div>
          </li>
        );
      })}
    </ol>
  );
}

export type EventCardStatus = "available" | "closed" | "cancelled" | "postponed" | "draft" | "review";

const STATUS: Record<EventCardStatus, { label: string; tone: "success" | "muted" | "danger" | "warning" | "teal" }> = {
  available: { label: "Available", tone: "success" },
  closed: { label: "Closed", tone: "muted" },
  cancelled: { label: "Cancelled", tone: "danger" },
  postponed: { label: "Postponed", tone: "warning" },
  draft: { label: "Draft", tone: "muted" },
  review: { label: "Under review", tone: "teal" }
};

/**
 * The event card.
 *
 * One status, shown once. The previous web card rendered the raw status string
 * in a pill at the top *and* a mapped human label at the bottom, so a cancelled
 * event read "cancelled … Cancelled" and a published one read
 * "published … Available" — two different vocabularies for the same fact. It
 * also ended with a "View details" `<span>` styled to look like a link but not
 * focusable or clickable, which is the kind of thing that trains people to
 * distrust an interface.
 */
export function EventCard({
  href,
  title,
  subtitle,
  status,
  when,
  where,
  meta,
  media,
  action,
  className = ""
}: {
  href: string;
  title: string;
  subtitle?: string | null;
  status: EventCardStatus;
  /** Pre-formatted by the caller — formatting is locale policy, not a card's job. */
  when: string;
  where?: string | null;
  /** A third meta line — category, price, anything the caller has already joined into one string. */
  meta?: string | null;
  media?: ReactNode;
  /** A save/bookmark control; wrapped so it stays clickable above the card link. */
  action?: ReactNode;
  className?: string;
}) {
  const { label, tone } = STATUS[status];
  return (
    <LinkCard href={href} label={title} className={className}>
      {media ? <MediaFrame ratio="3/2">{media}</MediaFrame> : null}
      <div className="p-5">
        <div className="flex items-start justify-between gap-md">
          <Badge tone={tone}>{label}</Badge>
          {action ? <CardAction>{action}</CardAction> : null}
        </div>
        <h3 className="mt-md text-h3 text-text">{title}</h3>
        {subtitle ? <p className="mt-1 text-body text-muted">{subtitle}</p> : null}
        <div className="mt-md space-y-1">
          <MetaRow>{when}</MetaRow>
          {where ? <MetaRow>{where}</MetaRow> : null}
          {meta ? <MetaRow>{meta}</MetaRow> : null}
        </div>
      </div>
    </LinkCard>
  );
}

/** A category / taxonomy tile. */
export function CategoryCard({
  href,
  label,
  count,
  icon,
  className = ""
}: {
  href: string;
  label: string;
  count?: number;
  icon?: ReactNode;
  className?: string;
}) {
  return (
    <LinkCard
      href={href}
      // The count is in the name so it is announced, not left as orphaned text.
      label={count === undefined ? label : `${label}, ${count} events`}
      className={className}
    >
      <div className="flex items-center gap-md p-4">
        {icon ? (
          <span aria-hidden="true" className="shrink-0 text-accent-text">
            {icon}
          </span>
        ) : null}
        <span className="min-w-0 flex-1">
          <span className="block truncate text-label text-text">{label}</span>
          {count !== undefined ? (
            <span aria-hidden="true" className="block text-caption text-muted">
              {count} {count === 1 ? "event" : "events"}
            </span>
          ) : null}
        </span>
      </div>
    </LinkCard>
  );
}

/**
 * A ticket in the wallet.
 *
 * The perforation notch is drawn with two absolutely-positioned circles rather
 * than a background image, so it recolours with the theme.
 */
export function TicketCard({
  href,
  eventTitle,
  when,
  where,
  ticketType,
  code,
  status = "valid",
  className = ""
}: {
  href: string;
  eventTitle: string;
  when: string;
  where?: string | null;
  ticketType: string;
  /** Shown as the human-readable reference; never the raw HMAC payload. */
  code?: string;
  status?: "valid" | "used" | "refunded" | "cancelled";
  className?: string;
}) {
  const tone = { valid: "success", used: "muted", refunded: "warning", cancelled: "danger" } as const;
  const label = { valid: "Valid", used: "Checked in", refunded: "Refunded", cancelled: "Cancelled" }[status];

  return (
    <LinkCard href={href} label={`${eventTitle} — ${ticketType}, ${label}`} className={`overflow-hidden ${className}`}>
      <div className="p-5">
        <div className="flex items-start justify-between gap-md">
          <h3 className="min-w-0 flex-1 text-h3 text-text">{eventTitle}</h3>
          <Badge tone={tone[status]}>{label}</Badge>
        </div>
        <div className="mt-md space-y-1">
          <MetaRow>{when}</MetaRow>
          {where ? <MetaRow>{where}</MetaRow> : null}
        </div>
      </div>
      <div className="relative border-t border-dashed border-border-strong">
        <span
          aria-hidden="true"
          className="absolute -left-2 -top-2 h-4 w-4 rounded-full bg-background"
        />
        <span
          aria-hidden="true"
          className="absolute -right-2 -top-2 h-4 w-4 rounded-full bg-background"
        />
        <div className="flex items-center justify-between gap-md px-5 py-3">
          <span className="text-caption text-muted">{ticketType}</span>
          {code ? <span className="font-mono text-caption text-text">{code}</span> : null}
        </div>
      </div>
    </LinkCard>
  );
}

/**
 * Deterministic placeholder art for an event with no uploaded banner (D-302).
 *
 * **This is a port of Flutter's `EventVisuals`, not a new design.** Mobile has drawn seeded gradient
 * hero art since D-286, while web rendered no image frame at all — so the same event was a picture on
 * one client and a wall of text on the other. Since almost no event has a banner, that divergence *was*
 * the visual difference between the two products.
 *
 * The gradient list, the hash and the seed (the slug) are copied exactly so a given event resolves to
 * the same colours on both. Every stop clears 3:1 against white because a white glyph sits on top
 * (WCAG 1.4.11) — the ember and gold entries are deliberately a shade below their token values for
 * that reason, and changing one side without the other silently breaks the pairing.
 */
const EVENT_GRADIENTS: [string, string][] = [
  ["#EF6F21", "#B5300E"], // ember → deep rust
  ["#B54C0D", "#6B2D0A"], // rust → cocoa
  ["#B88F14", "#B5540D"], // gold → ember
  ["#D64550", "#8C1F35"], // rose → wine
  ["#309E88", "#14503F"], // teal → forest
  ["#EF6F21", "#8C1F35"], // ember → wine
  ["#C77B3A", "#5B3A1E"], // amber → bark
  ["#1F6B5C", "#3D2E22"]  // deep teal → warm dark
];

/** Must stay identical to `EventVisuals.gradientFor` — same multiplier, same mask, same modulo. */
function gradientFor(seed: string): [string, string] {
  let hash = 0;
  for (const ch of seed) hash = (hash * 31 + ch.charCodeAt(0)) & 0x7fffffff;
  return EVENT_GRADIENTS[hash % EVENT_GRADIENTS.length]!;
}

export function EventPlaceholder({ seed, className = "" }: { seed: string; className?: string }) {
  const [from, to] = gradientFor(seed);
  return (
    <div
      className={`h-full w-full ${className}`}
      style={{ backgroundImage: `linear-gradient(to bottom right, ${from}, ${to})` }}
      // Decorative: it encodes no information a sighted user gets and the card's title is already the
      // accessible name, so announcing it would only add noise to a screen reader.
      aria-hidden="true"
    />
  );
}
