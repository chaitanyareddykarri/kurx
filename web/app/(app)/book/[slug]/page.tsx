import { notFound } from "next/navigation";
import Link from "next/link";
import { Calendar, MapPin } from "lucide-react";
import { BookingForm } from "@/components/events/booking-form";
import { getPublicEvent, listPublicTicketTypes } from "@/lib/api";
import { requireSession } from "@/lib/session";

/**
 * Booking (D-302). This page previously showed three decorative cards describing what booking *would*
 * do — one of them read "Razorpay Checkout.js loads at payment time" at the attendee — above a form that
 * never contacted the backend. The cards are gone: they were copy, not controls.
 */
export default async function BookTicketPage({ params }: { params: { slug: string } }) {
  await requireSession();

  const event = await getPublicEvent(params.slug).catch(() => null);
  if (!event) notFound();

  const ticketTypes = await listPublicTicketTypes(event.id).catch(() => []);
  const where = [event.venue.name, event.venue.city].filter(Boolean).join(", ");

  return (
    <div className="mx-auto max-w-3xl space-y-6">
      <div>
        <Link href={`/e/${event.slug}`} className="text-caption text-accent-text hover:underline">
          ← Back to event
        </Link>
        <h1 className="mt-2 text-h1">{event.title}</h1>
        <div className="mt-2 flex flex-wrap gap-4 text-body text-muted">
          <span className="flex items-center gap-2">
            <Calendar size={16} aria-hidden="true" />
            {new Date(event.starts_at).toLocaleString("en-IN", { dateStyle: "medium", timeStyle: "short" })}
          </span>
          {where ? (
            <span className="flex items-center gap-2">
              <MapPin size={16} aria-hidden="true" /> {where}
            </span>
          ) : null}
        </div>
      </div>

      <BookingForm eventId={event.id} ticketTypes={ticketTypes} />
    </div>
  );
}
