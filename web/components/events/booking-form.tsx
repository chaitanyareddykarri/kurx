"use client";

import { useState, useTransition } from "react";
import { useRouter } from "next/navigation";
import { CreditCard, Ticket } from "lucide-react";
import { Alert, Button } from "@kurx/ui";
import { createBookingAction } from "@/lib/booking-actions";
import { formatCurrency } from "@/lib/formatters";
import { priceLabel } from "@/components/events/event-detail-sections";
import type { PublicTicketType } from "@/lib/api";

/**
 * Booking, against the real backend (D-302).
 *
 * What this replaces collected a name and an email, hardcoded two ticket types that had nothing to do
 * with the event, and then opened Razorpay with no order and no amount — falling through to `/tickets`
 * when the script was absent. It reported success for a registration that never happened.
 *
 * Ticket types now come from `GET /v1/events/{id}/ticket-types` and booking goes through
 * `POST /v1/events/{id}/orders`. The gateway is only ever opened against an order the SERVER created,
 * because an amount decided by the browser is not a price.
 */

function soldOut(t: PublicTicketType): boolean {
  return t.quantity != null && t.sold != null && t.sold >= t.quantity;
}

function remaining(t: PublicTicketType): number | null {
  return t.quantity != null && t.sold != null ? Math.max(t.quantity - t.sold, 0) : null;
}

export function BookingForm({ eventId, ticketTypes }: { eventId: string; ticketTypes: PublicTicketType[] }) {
  const router = useRouter();
  const available = ticketTypes.filter((t) => !soldOut(t));
  const [selected, setSelected] = useState<string | null>(available[0]?.id ?? null);
  const [error, setError] = useState<string | null>(null);
  const [pending, start] = useTransition();

  if (ticketTypes.length === 0) {
    return (
      <Alert tone="info" title="Registration isn't open">
        The host has not published any ticket types for this event yet.
      </Alert>
    );
  }

  const chosen = ticketTypes.find((t) => t.id === selected) ?? null;

  function book() {
    if (!chosen) return;
    setError(null);
    start(async () => {
      const result = await createBookingAction(eventId, chosen!.id);
      if (!result.ok) {
        setError(result.error);
        return;
      }
      // A free ticket is issued immediately and the order comes back already Paid — there is nothing to
      // pay, so sending someone to a gateway would be theatre. A paid order carries a gateway order id;
      // until the Razorpay adapter is real (D-049 drives MockPaymentGateway) the honest thing is to say
      // so rather than open a checkout that cannot settle.
      if (result.status.toLowerCase() === "paid") {
        router.push("/tickets");
        return;
      }
      setError(
        "Your order was created, but online payment isn't live yet on this environment. " +
          "It's saved under My Tickets as unpaid."
      );
    });
  }

  return (
    <div className="space-y-4 rounded-lg border border-border bg-surface p-5">
      <fieldset>
        <legend className="text-label text-text">Choose a ticket</legend>
        <div className="mt-3 space-y-2">
          {ticketTypes.map((t) => {
            const out = soldOut(t);
            const left = remaining(t);
            return (
              <label
                key={t.id}
                className={`flex items-start gap-3 rounded-md border p-3 ${
                  out ? "cursor-not-allowed border-border opacity-60" : "cursor-pointer border-border hover:border-accent"
                } ${selected === t.id ? "border-accent" : ""}`}
              >
                <input
                  type="radio"
                  name="ticketType"
                  value={t.id}
                  disabled={out}
                  checked={selected === t.id}
                  onChange={() => setSelected(t.id)}
                  className="mt-1"
                />
                <span className="min-w-0 flex-1">
                  <span className="flex flex-wrap items-baseline justify-between gap-2">
                    <span className="text-label text-text">{t.name}</span>
                    {/* D-366 — a banded ticket has no single price, so printing `price_paise` here would
                        quote the cheapest band as if it were THE price. `priceLabel` says what the
                        number is: a range, charged per team, resolved by size at checkout. */}
                    <span className="text-label text-text">
                      {t.price_paise === 0 && !t.price_tiers?.length
                        ? "Free"
                        : priceLabel(t.price_paise, t.pricing_unit,
                            (p) => formatCurrency(p, "en-IN", t.currency ?? "INR"), t.price_tiers)}
                    </span>
                  </span>
                  {t.description ? <span className="mt-1 block text-caption text-muted">{t.description}</span> : null}
                  <span className="mt-1 block text-caption text-muted">
                    {out ? "Sold out" : left != null ? `${left} left` : "Available"}
                    {t.per_user_limit ? ` · max ${t.per_user_limit} per person` : ""}
                  </span>
                </span>
              </label>
            );
          })}
        </div>
      </fieldset>

      {error ? (
        <Alert tone="danger" live title="Couldn't complete that">
          {error}
        </Alert>
      ) : null}

      <Button type="button" onClick={book} disabled={!chosen || pending} className="w-full">
        {chosen && chosen.price_paise > 0 ? (
          <>
            <CreditCard size={16} aria-hidden="true" /> {pending ? "Creating order…" : "Continue to payment"}
          </>
        ) : (
          <>
            <Ticket size={16} aria-hidden="true" /> {pending ? "Registering…" : "Register free"}
          </>
        )}
      </Button>
    </div>
  );
}
