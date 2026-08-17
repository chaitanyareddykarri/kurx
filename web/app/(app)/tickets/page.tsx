import { Ticket } from "lucide-react";
import { Badge, Card, EmptyState, LinkButton } from "@kurx/ui";
import { myTickets, section } from "@/lib/api";
import { formatCurrency, formatDate } from "@/lib/formatters";
import { requireSession } from "@/lib/session";
import { TicketQr } from "@/components/tickets/ticket-qr";
import { orderStatusOf } from "@/lib/order-status";

export const metadata = { title: "My Tickets" };

export default async function TicketsPage() {
  const session = await requireSession();

  /*
   * `.catch(() => [])` is the D-235 defect, and this is the worst place in the product to have it:
   * a 500, a timeout or a dropped connection rendered "No tickets yet. Book an event to see your
   * tickets here." — telling somebody who has paid, and may be standing at a gate, that they own
   * nothing. An outage is not evidence about a purchase.
   */
  const result = await section(myTickets(session.accessToken));

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-4">
        <h1 className="text-3xl font-semibold text-text">My Tickets</h1>
        <div className="flex flex-wrap gap-2">
          <LinkButton href="/tickets/refunds" variant="secondary">Refunds</LinkButton>
        </div>
      </div>

      {result.state !== "ok" ? (
        <div role="status" className="rounded-lg border border-dashed border-border bg-surface p-6 text-center">
          <p className="text-body text-text">Your tickets couldn&apos;t be loaded.</p>
          <p className="mt-1 text-sm text-muted">
            This is a temporary problem on our side, not a change to your orders. Try refreshing.
          </p>
        </div>
      ) : result.data.length === 0 ? (
        <EmptyState
          icon={<Ticket size={32} />}
          title="No tickets yet"
          message="Tickets you book appear here, each with the code you show at the gate."
          action={<LinkButton href="/discover">Find an event</LinkButton>}
        />
      ) : (
        <div className="grid gap-4 md:grid-cols-2">
          {result.data.map((order) => {
            const status = orderStatusOf(order.status);
            return (
              <Card key={order.id} className="space-y-3">
                <div className="flex flex-wrap items-start justify-between gap-2">
                  <h2 className="font-semibold text-text">{order.event_title ?? "Event"}</h2>
                  {/* The raw machine status was printed through `capitalize`, so a buyer read
                      "Awaitingpayment". Statuses are mapped to words, with a tone that does not
                      carry the meaning on its own. */}
                  <Badge tone={status.tone}>{status.label}</Badge>
                </div>

                <p className="text-sm text-muted">
                  {order.ticket_type ?? "Standard"}
                  {" · "}
                  {/* The order's own settlement currency (V3 §9.1), not a hardcoded ₹. */}
                  {order.amount_paise > 0 ? formatCurrency(order.amount_paise, "en-IN", order.currency) : "Free"}
                  {" · "}
                  {formatDate(order.created_at, "en-IN", { day: "numeric", month: "short", year: "numeric" })}
                </p>

                {/*
                  Each ticket now shows its own real, scannable code.

                  What was here was a 48px lucide `QrCode` glyph — decoration, not a code — drawn
                  once per *order* while the actual `code` on every ticket in `order.tickets` was
                  never rendered at all. A buyer with three tickets saw one picture of a QR and could
                  not produce a single one of them at the gate.

                  `GET /v1/tickets/{code}/qr.png` exists for exactly this; its own comment in
                  `CertificateEndpoints.cs` says it "replaces the decorative/mock QR the web and
                  mobile clients render". Flutter adopted it. Web never did.
                */}
                {order.tickets.length > 0 ? (
                  <ul className="grid gap-3 sm:grid-cols-2">
                    {order.tickets.map((ticket) => (
                      <li key={ticket.id} className="rounded-md border border-border bg-background p-3">
                        <TicketQr code={ticket.code} eventTitle={order.event_title ?? "this event"} />
                        <p className="mt-2 break-all text-center font-mono text-[11px] text-muted">{ticket.code}</p>
                        {ticket.checked_in_at ? (
                          <p className="mt-1 text-center text-caption text-success">
                            Checked in {formatDate(ticket.checked_in_at, "en-IN", { day: "numeric", month: "short" })}
                          </p>
                        ) : null}
                      </li>
                    ))}
                  </ul>
                ) : (
                  <p className="text-sm text-muted">
                    No tickets have been issued on this order yet.
                  </p>
                )}
              </Card>
            );
          })}
        </div>
      )}
    </div>
  );
}
