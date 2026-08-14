"use client";

import { useEffect, useState } from "react";
import { useFormState, useFormStatus } from "react-dom";
import { Banknote } from "lucide-react";
import { useToast } from "@kurx/ui";
import { recordFinancialReviewAction } from "@/lib/admin-actions";

function Submit({ label, tone }: { label: string; tone: "approve" | "reject" }) {
  const { pending } = useFormStatus();
  return (
    <button type="submit" disabled={pending}
      className={`h-8 rounded-md border px-3 text-xs font-semibold disabled:opacity-50 ${
        tone === "approve" ? "border-success/40 text-success hover:bg-success/10"
                           : "border-danger/40 text-danger hover:bg-danger/10"}`}>
      {pending ? "…" : label}
    </button>
  );
}

/**
 * D-266 M7 — FinanceOps' verdict on a fundraising event's money path (D12 §6, A11).
 *
 * **Rendered only for events whose archetype requires it**, which the backend signals through the
 * `financial_review_required` blocker rather than this component inferring it from "is it paid" — a
 * ticketed concert takes more money and needs no such review.
 *
 * A non-FinanceOps reviewer sees the panel and gets a 403 on submit; the server is the authority, and
 * hiding the control would leave a reviewer unable to tell "not my job" from "nothing to do".
 */
export function FinancialReviewPanel({ eventId }: { eventId: string }) {
  const [state, formAction] = useFormState(recordFinancialReviewAction.bind(null, eventId), null);
  const [failing, setFailing] = useState(false);
  const toast = useToast();

  useEffect(() => {
    if (state && "error" in state) toast(String(state.error), "error");
    if (state && "ok" in state) { toast("Financial review recorded.", "success"); setFailing(false); }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [state]);

  return (
    <div className="mt-3 rounded-md border border-border bg-elevated p-3">
      <h4 className="flex items-center gap-1.5 text-xs font-semibold text-text">
        <Banknote size={13} /> Financial review
      </h4>
      <p className="mt-1 text-xs text-muted">
        This event solicits money for a cause. FinanceOps clears the money path before it can go live.
      </p>

      <div className="mt-3 flex flex-wrap items-center gap-2">
        <form action={formAction} className="inline">
          <input type="hidden" name="passed" value="true" />
          <Submit label="Clear money path" tone="approve" />
        </form>
        <button type="button" onClick={() => setFailing((v) => !v)}
          className="h-8 rounded-md border border-danger/40 px-3 text-xs font-semibold text-danger hover:bg-danger/10">
          Refuse
        </button>
      </div>

      {failing ? (
        <form action={formAction} className="mt-2 space-y-2 rounded-md border border-border bg-surface p-3">
          <input type="hidden" name="passed" value="false" />
          <label className="block text-xs text-muted">
            Why <span className="text-danger">*</span>
            <textarea name="notes" rows={3} required
              placeholder="What the organiser has to correct about the money path."
              className="mt-1 w-full rounded-md border border-border-strong bg-elevated px-2 py-1 text-xs text-text" />
          </label>
          <div className="flex gap-2">
            <Submit label="Confirm refusal" tone="reject" />
            <button type="button" onClick={() => setFailing(false)}
              className="h-8 rounded-md border border-border px-3 text-xs text-muted hover:bg-elevated">
              Cancel
            </button>
          </div>
        </form>
      ) : null}
    </div>
  );
}
