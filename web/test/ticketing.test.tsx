import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";

import { EmptyState } from "@kurx/ui";
import { orderStatusOf } from "@/lib/order-status";
import { eventStatusOf } from "@/lib/event-status";
import { TicketQr } from "@/components/tickets/ticket-qr";

/**
 * Guards for Phase 19 — ticketing and registration.
 *
 * The money path is where a lie costs the most: an outage that reads as "you have no tickets", a
 * status the buyer cannot parse, or a decorative glyph standing in for the code that opens the gate.
 */

describe("orderStatusOf — a machine status is not a sentence", () => {
  it("turns the backend's values into words", () => {
    // `capitalize` on the raw value produced "Awaitingpayment" on the buyer's own tickets page.
    expect(orderStatusOf("AwaitingPayment")).toEqual({ label: "Awaiting payment", tone: "warning" });
    expect(orderStatusOf("paid")).toEqual({ label: "Paid", tone: "success" });
    expect(orderStatusOf("CANCELLED")).toEqual({ label: "Cancelled", tone: "danger" });
    expect(orderStatusOf("awaiting_payment")).toEqual({ label: "Awaiting payment", tone: "warning" });
  });

  it("never carries the state in tone alone", () => {
    // Every mapping must have a label; a colour-only status fails WCAG 1.4.1 and is invisible to a
    // screen reader.
    for (const raw of ["paid", "pending", "cancelled", "refunded", "failed", "expired"]) {
      expect(orderStatusOf(raw).label).toBeTruthy();
    }
  });

  it("shows an unknown status as-is rather than guessing at it", () => {
    // Mapping an unrecognised value onto "Paid" would be a claim about somebody's money.
    expect(orderStatusOf("chargeback_opened")).toEqual({ label: "chargeback_opened", tone: "muted" });
  });
});

describe("EmptyState — a Material icon name is not an icon", () => {
  it("drops a bare string rather than printing the identifier", () => {
    // Seven web call sites passed names like "receipt_long" from a font this project does not load,
    // and each rendered the identifier as visible text above the heading.
    render(<EmptyState icon="receipt_long" title="No refunds" />);
    expect(screen.queryByText("receipt_long")).not.toBeInTheDocument();
    expect(screen.getByText("No refunds")).toBeInTheDocument();
  });

  it("still renders a real icon node", () => {
    render(<EmptyState icon={<svg data-testid="glyph" />} title="No refunds" />);
    expect(screen.getByTestId("glyph")).toBeInTheDocument();
  });
});

describe("TicketQr — the code that opens the gate", () => {
  it("loads the real server-rendered QR, not a decorative glyph", () => {
    render(<TicketQr code="11111111-2222-3333-4444-555555555555" eventTitle="Hack Day" />);
    const img = screen.getByRole("img", { name: /Entry QR code for Hack Day/ });
    // Through the server route: the upstream PNG endpoint needs a bearer token an <img> cannot send.
    expect(img).toHaveAttribute("src", "/api/ticket-qr/11111111-2222-3333-4444-555555555555");
  });
});

describe("eventStatusOf — a host should not read a database value", () => {
  it("turns lifecycle statuses into words", () => {
    // `/workspace` printed the raw value into a sentence: "Opens after approval · pendingreview".
    expect(eventStatusOf("PendingReview")).toEqual({ label: "Pending review", tone: "warning" });
    expect(eventStatusOf("changes_requested")).toEqual({ label: "Changes requested", tone: "danger" });
    expect(eventStatusOf("live")).toEqual({ label: "Live", tone: "accent" });
  });

  it("shows an unknown status as-is rather than guessing at it", () => {
    expect(eventStatusOf("quarantined")).toEqual({ label: "quarantined", tone: "muted" });
  });

  it("covers every status the workspace filters on", () => {
    // The view filters and the labels read the same normalised values; a status the filters know
    // about but the labels do not would reach the host raw.
    for (const s of ["draft", "pendingreview", "underreview", "published", "scheduled", "live", "completed", "closed", "archived", "cancelled"]) {
      expect(eventStatusOf(s).label).not.toBe(s);
    }
  });
});
