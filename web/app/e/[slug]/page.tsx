import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { Calendar, CheckCircle2, MapPin, Share2, Star, Ticket } from "lucide-react";
import { DownloadApp } from "@/components/marketing/download-app";
import { Badge, Breadcrumbs, Card, LinkButton } from "@kurx/ui";
import { EventCard } from "@/components/events/event-card";
import { SaveButton } from "@/components/events/save-button";
import { ReviewForm } from "@/components/events/review-form";
import {
  GettingThereSection, ParticipationSection, RulesSection, TermsSection,
  priceLabel, teamPriceRows, teamSizeLabel
} from "@/components/events/event-detail-sections";
import { getPublicEvent, getRelatedEvents, listEventReviews, listPublicTicketTypes, listSavedEvents, type EventReview } from "@/lib/api";
import { formatCurrency } from "@/lib/formatters";
import { currentSession } from "@/lib/session";
import { createMetadata, jsonLd } from "@/lib/site";

/**
 * The API returns the raw lifecycle string. Mapping it once here keeps the page
 * from printing "published" at a reader and from colouring every status green —
 * both of which it did before.
 */
const STATUS: Record<string, { label: string; tone: "success" | "muted" | "danger" | "warning" | "teal" }> = {
  published: { label: "Registration open", tone: "success" },
  closed: { label: "Registration closed", tone: "muted" },
  cancelled: { label: "Cancelled", tone: "danger" },
  postponed: { label: "Postponed", tone: "warning" },
  draft: { label: "Draft", tone: "muted" },
  under_review: { label: "Under review", tone: "teal" },
  pending_review: { label: "Under review", tone: "teal" }
};
const statusLabel = (s: string) => STATUS[s]?.label ?? s;
const statusTone = (s: string) => STATUS[s]?.tone ?? "muted";

type Props = { params: { slug: string } };

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const event = await getPublicEvent(params.slug).catch(() => null);
  if (!event) return createMetadata({ title: "Event not found", path: `/e/${params.slug}` });
  return createMetadata({ title: event.title, path: `/e/${params.slug}` });
}

export default async function EventPage({ params }: Props) {
  const event = await getPublicEvent(params.slug).catch(() => null);
  if (!event) notFound();

  const related = await getRelatedEvents(params.slug, 3).catch(() => []);
  const ticketTypes = await listPublicTicketTypes(event.id).catch(() => []);
  // Cheapest ticket a buyer could actually take. null = no ticket type published, which is NOT free.
  const fromPaise = ticketTypes.length > 0 ? Math.min(...ticketTypes.map((t) => t.price_paise)) : null;
  const gallery = (event.media ?? []).filter((m) => m.url);
  const reviews = await listEventReviews(event.id).catch(() => null);
  const venueLine = [event.venue.name, event.venue.city].filter(Boolean).join(", ");

  const session = await currentSession();
  const initialSaved = session
    ? await listSavedEvents(session.accessToken, 100).then((s) => s.some((e) => e.id === event.id)).catch(() => false)
    : false;

  return (
    <>
      <main className="container-shell py-10 pb-28 lg:pb-10">
        <script type="application/ld+json" dangerouslySetInnerHTML={jsonLd("Event", {
          name: event.title, startDate: event.starts_at,
          location: venueLine,
        })} />
        <Breadcrumbs
          className="mb-lg"
          items={[{ label: "Discover", href: "/discover" }, { label: event.title }]}
        />
        <div className="rounded-lg border border-border bg-surface p-6">
          {/*
            The banner. This box reserved banner-sized space and rendered nothing for the life of the
            product, because the API returned `banner_key` (a storage key, not fetchable) and never a
            URL. `banner_url` is presigned server-side (D-302).
          */}
          {event.banner_url ? (
            <div className="mb-6 aspect-[3/1] w-full overflow-hidden rounded-lg bg-elevated">
              {/* eslint-disable-next-line @next/next/no-img-element */}
              <img src={event.banner_url} alt="" aria-hidden className="h-full w-full object-cover" />
            </div>
          ) : null}
          <div className="min-h-56 rounded-lg border border-border bg-elevated p-6">
            {/*
              One status, once, in human words. The page previously printed the
              raw string here ("published") AND repeated it in the sidebar under
              a heading, hardcoded to `text-success` regardless of value — so a
              CANCELLED event announced itself in green as though nothing were
              wrong.
            */}
            <Badge tone={statusTone(event.status)}>{statusLabel(event.status)}</Badge>
            <h1 className="mt-4 max-w-3xl text-display">{event.title}</h1>
            {event.subtitle && <p className="mt-2 text-body-lg text-muted">{event.subtitle}</p>}
            <div className="mt-5 flex flex-wrap gap-4 text-body text-muted">
              <span className="flex items-center gap-2">
                <Calendar size={16} aria-hidden="true" />
                {new Date(event.starts_at).toLocaleString("en-IN", { dateStyle: "full", timeStyle: "short" })}
              </span>
              {venueLine && (
                <span className="flex items-center gap-2">
                  <MapPin size={16} aria-hidden="true" /> {venueLine}
                </span>
              )}
              {event.event_mode && event.event_mode.toLowerCase() !== "offline" ? (
                <span className="flex items-center gap-2 capitalize">{event.event_mode}</span>
              ) : null}
              {fromPaise !== null ? (
                <span className="flex items-center gap-2">
                  <Ticket size={16} aria-hidden="true" />
                  {fromPaise === 0 ? "Free" : `From ${formatCurrency(fromPaise, "en-IN", event.settlement_currency ?? "INR")}`}
                </span>
              ) : null}
            </div>
            {/*
              Who the host is representing. The API returned only `representing_org_id` — a bare Guid —
              so no surface could name the institution behind an event, which is the single fact an
              attendee uses to decide whether to trust it (D-302). Null means self-represented.
            */}
            {event.representing ? (
              <p className="mt-4 text-body text-muted">
                Hosted on behalf of{" "}
                <Link href={`/o/${event.representing.slug}`} className="text-accent-text hover:underline">
                  {event.representing.name}
                </Link>
                {event.representing.is_verified ? (
                  <span className="ml-2 inline-flex align-middle">
                    <Badge tone="teal" icon={<CheckCircle2 size={12} aria-hidden="true" />}>Verified</Badge>
                  </span>
                ) : null}
              </p>
            ) : null}
          </div>
          <div className="mt-6 grid gap-6 lg:grid-cols-[1fr_320px]">
            <div className="space-y-6">
              {event.description && <Section title="About" items={[event.description]} />}

              {/* The media gallery. Stored, returned, and never rendered until D-302. */}
              {gallery.length > 0 && (
                <section>
                  <h2 className="text-h2">Gallery</h2>
                  <div className="mt-3 grid gap-3 sm:grid-cols-2 md:grid-cols-3">
                    {gallery.map((m) => (
                      <div key={m.id} className="aspect-[3/2] overflow-hidden rounded-lg bg-elevated">
                        {/* eslint-disable-next-line @next/next/no-img-element */}
                        <img src={m.url!} alt={m.caption || ""} loading="lazy" className="h-full w-full object-cover" />
                      </div>
                    ))}
                  </div>
                </section>
              )}

              {/* Ticket types and prices. You previously pressed "Book ticket" without ever being shown
                  what a ticket cost. */}
              {/*
                D-265's field groups, rendered for the first time. They were validated by the wizard,
                stored, returned by this very response, and displayed nowhere — so an event could carry
                an 18+ rule, a registration deadline and a binding consent statement that the person
                deciding whether to register never saw. Each section returns null when the organiser set
                nothing, so a simple event stays a short page.
              */}
              <ParticipationSection event={event} />
              <RulesSection event={event} />

              {/* Ticket types and prices. You previously pressed "Book ticket" without ever being shown
                  what a ticket cost. */}
              {ticketTypes.length > 0 && (
                <section>
                  <h2 className="text-h2">Tickets</h2>
                  <ul className="mt-3 space-y-2">
                    {ticketTypes.map((t) => {
                      const left = t.quantity != null && t.sold != null ? Math.max(t.quantity - t.sold, 0) : null;
                      return (
                        <li key={t.id} className="flex flex-wrap items-baseline justify-between gap-2 rounded-md border border-border p-3">
                          <span className="text-label text-text">{t.name}</span>
                          {/* D-357 — the unit, always. "₹2,000" alone cannot tell a registrant whether
                              they are buying a team entry or one seat of four. */}
                          <span className="text-label text-text">
                            {priceLabel(t.price_paise, t.pricing_unit,
                              (p) => formatCurrency(p, "en-IN", t.currency ?? "INR"), t.price_tiers)}
                          </span>
                          <span className="w-full text-caption text-muted">
                            {[
                              teamSizeLabel(t.registration_mode, t.group_min, t.group_max),
                              left === 0
                                ? "Sold out"
                                : left != null
                                  ? `${left} ${t.registration_mode === "Group" ? "team slots" : "left"}`
                                  : "Available"
                            ].filter(Boolean).join(" · ")}
                          </span>
                          {/* D-366 — the whole table, not a single figure. A registrant deciding whether
                              to enter with three people or five needs both numbers in front of them, and
                              "₹300" on an event that also charges ₹400 is a price they cannot act on. */}
                          {teamPriceRows(t.price_tiers, (p) => formatCurrency(p, "en-IN", t.currency ?? "INR")) ? (
                            <div className="w-full overflow-x-auto">
                            <table className="w-full text-caption text-muted">
                              <caption className="sr-only">Price by team size for {t.name}</caption>
                              <tbody>
                                {teamPriceRows(t.price_tiers, (p) => formatCurrency(p, "en-IN", t.currency ?? "INR"))!.map((row) => (
                                  <tr key={row.size}>
                                    <td className="py-0.5 pr-3">{row.size}</td>
                                    <td className="py-0.5 text-text">{row.price}</td>
                                  </tr>
                                ))}
                              </tbody>
                            </table>
                            </div>
                          ) : null}
                        </li>
                      );
                    })}
                  </ul>
                </section>
              )}
              <GettingThereSection event={event} />
              <TermsSection event={event} />
              {reviews && (
                <ReviewsSection
                  eventId={event.id}
                  slug={event.slug}
                  average={reviews.summary.average}
                  count={reviews.summary.count}
                  items={reviews.items}
                  canReview={Boolean(session)}
                />
              )}
              {related.length > 0 && (
                <section>
                  <h2 className="text-h2">Related events</h2>
                  <div className="mt-3 grid gap-4 md:grid-cols-2">
                    {related.map((r) => <EventCard key={r.slug} event={r} />)}
                  </div>
                </section>
              )}
            </div>
            <aside className="space-y-4 lg:sticky lg:top-20 lg:self-start">
              <Card>
                <h2 className="text-h3">Registration</h2>
                {/* "Book ticket" promised a checkout the web client does not have — the page it
                    led to opened Razorpay with no order and reported success regardless (REG-009).
                    The label now matches where the button actually goes. */}
                <LinkButton href={`/book/${event.slug}`} className="mt-4 w-full">
                  <Ticket size={16} aria-hidden="true" /> Book in the app
                </LinkButton>
                <div className="mt-3">
                  {session ? (
                    <SaveButton eventId={event.id} initialSaved={initialSaved} />
                  ) : (
                    <LinkButton href="/?login=required#login" variant="secondary" className="w-full">
                      Save event
                    </LinkButton>
                  )}
                </div>
              </Card>
              {/*
                A "Event QR code" card sat here rendering a lucide glyph, not a
                code — `qrcode.react` is a declared dependency used nowhere in
                web, so this was a placeholder that promised a scannable code and
                delivered an icon. Removed rather than implemented: whether it
                was meant to encode the share URL or a check-in code is a product
                question, and the deep-link button below already covers the
                former. Recorded as REG-005.
              */}
              <LinkButton href={`kurx://event/${event.slug}`} variant="secondary" className="w-full">
                <Share2 size={16} aria-hidden="true" /> Continue in app
              </LinkButton>
            </aside>
          </div>
        </div>
      </main>
      {/*
        The booking action lived only in a sidebar that stacks BELOW the
        description, reviews and related events on a phone — so the conversion
        control on the conversion page was several screens of scrolling away.
        This is the Event/Conference pattern's sticky register CTA
        (`docs/ui-ux/visual-identity.md` §11A), shown only where the sidebar is
        not visible. `pb-24` on <main> reserves its height so it never covers
        the last row of content.
      */}
      <div className="fixed inset-x-0 bottom-0 z-sticky border-t border-border bg-background/95 p-4 pb-[max(1rem,env(safe-area-inset-bottom))] backdrop-blur lg:hidden">
        <LinkButton href={`/book/${event.slug}`} className="w-full">
          <Ticket size={16} aria-hidden="true" /> Book in the app
        </LinkButton>
      </div>
      <DownloadApp deepLink={`kurx://event/${event.slug}`} />
    </>
  );
}

function Stars({ rating }: { rating: number }) {
  return (
    <span className="inline-flex items-center gap-0.5 text-accent-text" role="img" aria-label={`${rating} out of 5`}>
      {[1, 2, 3, 4, 5].map((n) => (
        <Star
          key={n}
          size={14}
          aria-hidden="true"
          className={n <= Math.round(rating) ? "fill-current" : "text-border-strong"}
        />
      ))}
    </span>
  );
}

function ReviewsSection({ eventId, slug, average, count, items, canReview }: {
  eventId: string; slug: string; average: number; count: number; items: EventReview[]; canReview: boolean;
}) {
  return (
    <section>
      <div className="flex items-center gap-3">
        <h2 className="text-h2">Reviews</h2>
        {count > 0 && (
          <span className="flex items-center gap-2 text-sm text-muted">
            <Stars rating={average} /> {average.toFixed(1)} · {count} review{count !== 1 ? "s" : ""}
          </span>
        )}
      </div>
      {canReview
        ? <ReviewForm eventId={eventId} slug={slug} />
        : <p className="mt-2 text-sm text-muted"><a href="/?login=required#login" className="text-accent">Sign in</a> to review this event.</p>}
      {items.length === 0 ? (
        <p className="mt-4 text-sm text-muted">No reviews yet — be the first.</p>
      ) : (
        <ul className="mt-4 space-y-3">
          {items.map((r) => (
            <li key={r.id} className="rounded-md border border-border bg-surface p-4">
              <div className="flex flex-wrap items-center gap-2">
                <Stars rating={r.rating} />
                {r.title && <strong className="text-sm">{r.title}</strong>}
                {r.is_verified && (
                  <span className="inline-flex items-center gap-1 text-xs text-success"><CheckCircle2 size={12} /> Verified attendee</span>
                )}
              </div>
              {r.body && <p className="mt-2 text-sm text-muted">{r.body}</p>}
              <p className="mt-2 text-xs text-muted">
                {r.is_anonymous ? (
                  "Anonymous"
                ) : r.author_username ? (
                  <a href={`/u/${r.author_username}`} className="font-medium text-text hover:text-accent">
                    {r.author_name ?? "Attendee"}
                  </a>
                ) : (
                  r.author_name ?? "Attendee"
                )}{" "}
                · {new Date(r.created_at).toLocaleDateString("en-IN", { dateStyle: "medium" })}
              </p>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}

function Section({ title, items }: { title: string; items: string[] }) {
  return (
    <section>
      <h2 className="text-xl font-semibold">{title}</h2>
      <div className="mt-3 grid gap-2">
        {items.map((item) => <div key={item} className="rounded-md border border-border bg-surface px-3 py-2 text-sm text-muted">{item}</div>)}
      </div>
    </section>
  );
}
