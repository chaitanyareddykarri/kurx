/**
 * Order status → the words a buyer reads, and the tone that marks it.
 *
 * The screens printed `order.status` straight through a `capitalize` class, so the backend's machine
 * values reached buyers verbatim: "Awaitingpayment", "Paid", "Cancelled". Tone never carries the
 * meaning alone — the label always says it in words, so the state survives greyscale and a screen
 * reader.
 *
 * Matching is case-insensitive and punctuation-insensitive because the value is a plain string on
 * the wire, and an unrecognised one is shown as-is rather than mapped to a guess: inventing "Paid"
 * for a status we do not know would be a claim about somebody's money.
 */
export type OrderStatusTone = "success" | "warning" | "danger" | "muted" | "accent";

const LABELS: Record<string, { label: string; tone: OrderStatusTone }> = {
  paid: { label: "Paid", tone: "success" },
  completed: { label: "Completed", tone: "success" },
  confirmed: { label: "Confirmed", tone: "success" },
  free: { label: "Free", tone: "success" },
  pending: { label: "Pending", tone: "warning" },
  awaitingpayment: { label: "Awaiting payment", tone: "warning" },
  processing: { label: "Processing", tone: "warning" },
  cancelled: { label: "Cancelled", tone: "danger" },
  canceled: { label: "Cancelled", tone: "danger" },
  failed: { label: "Failed", tone: "danger" },
  refunded: { label: "Refunded", tone: "muted" },
  expired: { label: "Expired", tone: "muted" },
};

export function orderStatusOf(status: string): { label: string; tone: OrderStatusTone } {
  const key = status.toLowerCase().replace(/[\s_-]/g, "");
  return LABELS[key] ?? { label: status, tone: "muted" };
}
