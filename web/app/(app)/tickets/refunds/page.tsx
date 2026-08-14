import { CheckCircle2, Clock, ReceiptText, RefreshCw, XCircle } from "lucide-react";
import { EmptyState } from "@kurx/ui";
import { listMyRefunds, section, type Refund } from "@/lib/api";
import { formatCurrency } from "@/lib/formatters";
import { requireSession } from "@/lib/session";

export const metadata = { title: "Refunds" };

/// The caller's refunds across every event (D-199).
///
/// Read-only by design: a refund is issued against an *order* by the org's Owner/Finance — this page
/// is where a buyer watches one land, not where they start one. Mirrors the Flutter page.
export default async function RefundsPage() {
  const session = await requireSession();
  // An outage must not read as "you have no refunds" — on the money path that is the difference
  // between "we could not reach the server" and "the money you are waiting for does not exist"
  // (D-235).
  const result = await section(listMyRefunds(session.accessToken));

  if (result.state !== "ok") {
    return (
      <div className="mx-auto max-w-2xl py-12">
        <div role="status" className="rounded-lg border border-dashed border-border bg-surface p-6 text-center">
          <p className="text-body text-text">Your refunds couldn&apos;t be loaded.</p>
          <p className="mt-1 text-sm text-muted">
            This is a temporary problem on our side, not a change to your refunds. Try refreshing.
          </p>
        </div>
      </div>
    );
  }

  const refunds: Refund[] = result.data;

  if (refunds.length === 0) {
    return (
      <div className="mx-auto max-w-2xl py-12">
        {/* `icon` is a ReactNode, so the Material Symbols *name* passed here rendered as the
            literal text "receipt_long" above the heading. */}
        <EmptyState icon={<ReceiptText size={32} />} title="No refunds" message="Refunds on your orders appear here." />
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-2xl">
      <h1 className="mb-4 text-2xl font-semibold text-text">Refunds</h1>
      <ul className="space-y-3">
        {refunds.map((r) => {
          const { Icon, tone, label } = statusOf(r.status);
          return (
            <li key={r.id} className="flex items-center gap-3 rounded-lg border border-border bg-surface p-4">
              <Icon size={18} aria-hidden className={`shrink-0 ${tone}`} />
              <div className="min-w-0 flex-1">
                <p className="text-sm font-semibold text-text">
                  {formatCurrency(r.amount_paise, "en-IN", r.currency)}
                </p>
                <p className="mt-0.5 truncate text-xs text-muted">
                  {[label, r.reason || null, r.created_at ? new Date(r.created_at).toLocaleDateString() : null]
                    .filter(Boolean)
                    .join(" · ")}
                </p>
              </div>
            </li>
          );
        })}
      </ul>
    </div>
  );
}

function statusOf(status: string) {
  switch (status) {
    case "succeeded":
      return { Icon: CheckCircle2, tone: "text-success", label: "Refunded" };
    case "failed":
      return { Icon: XCircle, tone: "text-danger", label: "Failed" };
    case "processing":
      return { Icon: RefreshCw, tone: "text-warning", label: "Processing" };
    default:
      return { Icon: Clock, tone: "text-muted", label: "Pending" };
  }
}
