"use client";

import { useEffect } from "react";
import { useFormState, useFormStatus } from "react-dom";
import { useToast } from "@kurx/ui";
import { reviewOrgAction, reviewClaimAction } from "@/lib/admin-actions";

function DecisionButton({ decision, label, tone }: { decision: string; label: string; tone: "approve" | "reject" | "neutral" }) {
  const { pending } = useFormStatus();
  const cls =
    tone === "approve" ? "border-success/40 text-success hover:bg-success/10"
      : tone === "reject" ? "border-danger/40 text-danger hover:bg-danger/10"
        : "border-border text-muted hover:bg-surface";
  return (
    <button
      type="submit"
      name="decision"
      value={decision}
      disabled={pending}
      className={`h-8 rounded-md border px-3 text-xs font-semibold disabled:opacity-50 ${cls}`}
    >
      {pending ? "…" : label}
    </button>
  );
}

/** Reviewer decision for an org verification or a membership claim (D-055). Confirm the affiliation out
 *  of band (call/email) first, then record the decision here with optional notes. */
export function ReviewForm({ kind, id }: { kind: "org" | "claim"; id: string }) {
  const action = kind === "org" ? reviewOrgAction : reviewClaimAction;
  const [state, formAction] = useFormState(action.bind(null, id), null);
  const toast = useToast();

  useEffect(() => {
    if (state && "error" in state) toast(String(state.error), "error");
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [state]);

  if (state && "ok" in state) {
    return <p className="mt-3 text-xs font-medium text-success">Decision recorded.</p>;
  }

  return (
    <form action={formAction} className="mt-3 space-y-2">
      <textarea
        name="notes"
        rows={2}
        placeholder="Notes (e.g. spoke to registrar on +91… — confirmed)"
        className="w-full rounded-md border border-border-strong bg-background px-2 py-1.5 text-xs text-text placeholder:text-muted focus:border-accent focus:outline-none"
      />
      <div className="flex flex-wrap items-center gap-2">
        <DecisionButton decision="approve" label="Approve" tone="approve" />
        {kind === "org" ? <DecisionButton decision="request_changes" label="Request changes" tone="neutral" /> : null}
        <DecisionButton decision="reject" label="Reject" tone="reject" />
      </div>
      {state && "error" in state ? <p className="text-xs text-danger">{String(state.error)}</p> : null}
    </form>
  );
}
