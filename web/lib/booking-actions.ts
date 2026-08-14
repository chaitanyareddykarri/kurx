"use server";

import { revalidatePath } from "next/cache";
import { requireSession } from "@/lib/session";
import { apiErrorMessage, createEventOrder } from "@/lib/api";

/**
 * Attendee booking (D-302).
 *
 * The form this replaces never called the backend at all: it collected a name and an email, opened
 * Razorpay client-side with **no order id and no amount**, and — if the Razorpay script had not loaded —
 * simply navigated to `/tickets`. So it reported success for a registration that had never happened, and
 * no order, ticket or payment existed anywhere. That is the worst shape a bug can take: a green path
 * over nothing.
 *
 * Registration is a server action rather than a client fetch so the access token stays in the httpOnly
 * cookie, matching every other write in this app.
 */

export type BookingResult =
  | { ok: true; orderId: string; status: string; amountPaise: number; razorpayOrderId: string | null }
  | { ok: false; error: string };

/** Refusals the API can answer with, in words a person can act on. Anything unmapped falls through to
 * the raw code rather than a generic apology, so an unknown refusal is still diagnosable. */
const BOOKING_ERRORS: Record<string, string> = {
  not_eligible: "You don't meet this event's eligibility rules, so registration isn't open to you.",
  sold_out: "This ticket type is sold out.",
  inventory_exhausted: "This ticket type just sold out.",
  per_user_limit_reached: "You've already booked the maximum number of tickets for this type.",
  registration_closed: "Registration for this event has closed.",
  event_not_published: "This event isn't open for registration yet.",
  competition_requires_invitation: "This is an invite-only competition — ask the organiser for an invitation.",
  paid_group_not_supported_yet: "Group booking for paid tickets isn't available yet.",
  payment_not_supported_yet: "Paid registration isn't available for this event yet."
};

export async function createBookingAction(
  eventId: string,
  ticketTypeId: string,
  opts: { groupSize?: number; displayName?: string } = {}
): Promise<BookingResult> {
  const session = await requireSession();
  try {
    const order = await createEventOrder(session.accessToken, eventId, {
      ticketTypeId,
      groupSize: opts.groupSize,
      displayName: opts.displayName,
      // Per-caller idempotency (D-049 §17.1). A double-click, or a retry after a dropped response,
      // returns the ORIGINAL order instead of charging twice.
      idempotencyKey: `${session.me.id}:${eventId}:${ticketTypeId}`
    });
    revalidatePath("/tickets");
    return {
      ok: true,
      orderId: order.id,
      status: order.status,
      amountPaise: order.amount_paise,
      razorpayOrderId: order.razorpay_order_id ?? null
    };
  } catch (err) {
    const code = apiErrorMessage(err);
    return { ok: false, error: BOOKING_ERRORS[code] ?? code };
  }
}
