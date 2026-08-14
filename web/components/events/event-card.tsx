import { EventCard as UiEventCard, EventPlaceholder, type EventCardStatus } from "@kurx/ui";
import { formatCurrency } from "@/lib/formatters";
import type { EventSummary } from "@/lib/api";

/**
 * Web's event card — the shared `@kurx/ui` card plus this app's data shape.
 *
 * The mapping below is the whole of what is web-specific. Everything visual
 * moved to the design system so the same card can serve discovery, search
 * results and the host workspace without three variants drifting apart.
 *
 * The previous version rendered the raw status string in a pill *and* a mapped
 * human label at the bottom, so a cancelled event read "cancelled … Cancelled"
 * and a published one read "published … Available" — two vocabularies for one
 * fact. It also ended in a "View details" `<span>` that looked like a link but
 * was neither focusable nor clickable.
 *
 * The bookmark button is **removed rather than reconnected**. It carried an
 * `aria-label` and a hover colour but no `onClick` at all, and `EventSummary`
 * has no saved field for it to reflect — so it was a dead control advertising
 * itself as interactive, on the most-rendered card in the product. Wiring one up
 * here would be inventing product behaviour; `SaveButton` exists and needs an
 * `initialSaved` the list endpoint does not return. Logged as REG-004 for the
 * discovery phase to resolve properly.
 */
const STATUS: Record<string, EventCardStatus> = {
  published: "available",
  closed: "closed",
  cancelled: "cancelled",
  postponed: "postponed",
  draft: "draft",
  under_review: "review",
  pending_review: "review"
};

/** Formatted here, not in the design system: locale is this app's policy. */
function formatWhen(startsAt: string): string {
  return new Date(startsAt).toLocaleString("en-IN", { dateStyle: "medium", timeStyle: "short" });
}

/** Mode reads as a place when it isn't a physical one — "Online" beside a venue line says more than a
 * mode chip would. Offline adds nothing a venue line doesn't already say, so it is omitted. */
const MODE_LABEL: Record<string, string> = { online: "Online", hybrid: "Hybrid · in person + online" };

/**
 * The "from" price. `null` is NOT free — it means the host has published no ticket type yet, and calling
 * that Free would advertise a registration that cannot happen. `0` is genuinely free.
 */
function priceLabel(event: EventSummary): string | null {
  const paise = event.price_from_paise;
  if (paise === null || paise === undefined) return null;
  if (paise === 0) return "Free";
  return `From ${formatCurrency(paise, "en-IN", event.currency ?? "INR")}`;
}

export function EventCard({ event }: { event: EventSummary }) {
  const mode = MODE_LABEL[(event.event_mode ?? "").toLowerCase()];
  const where = [event.venue_name, event.city].filter(Boolean).join(", ");
  const price = priceLabel(event);

  return (
    <UiEventCard
      href={`/e/${event.slug}`}
      title={event.title}
      subtitle={event.subtitle}
      status={STATUS[event.status] ?? "closed"}
      when={formatWhen(event.starts_at)}
      // Mode replaces the venue line for an online event, which has no venue to name, and annotates it
      // for a hybrid one. Offline falls through to the venue alone.
      where={mode ? (where ? `${mode} · ${where}` : mode) : where || null}
      meta={[event.category_name, price].filter(Boolean).join(" · ") || null}
      media={
        // `banner_url` is the PRESIGNED url. `banner_key` is a storage key and is not fetchable — using
        // it as a src is why event imagery never appeared anywhere (D-302).
        //
        // No banner is the COMMON case, not the exception — until now nothing could upload one — so the
        // fallback is the same seeded gradient Flutter has drawn since D-286, never an empty frame. A
        // card with no image where the mobile app shows art is the two products disagreeing about what
        // an event looks like.
        event.banner_url ? (
          // eslint-disable-next-line @next/next/no-img-element
          <img src={event.banner_url} alt="" aria-hidden loading="lazy" className="h-full w-full object-cover" />
        ) : (
          <EventPlaceholder seed={event.slug} />
        )
      }
    />
  );
}
