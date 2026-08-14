"use client";

import { useState, useTransition } from "react";
import { ShieldAlert, ListChecks } from "lucide-react";
import { setReviewChecklistItemAction } from "@/lib/admin-actions";
import type { ReviewChecklist } from "@/lib/api";

/** Reviewer-facing copy for the backend's requirement codes.
 *
 *  **Presentation only.** The item list itself is whatever the backend's policy engine returns — this map
 *  never decides which items exist, so a rule added server-side appears here immediately (as its raw code
 *  until copy is written for it) rather than being silently dropped by a console that didn't know it. */
const LABELS: Record<string, string> = {
  event_authorization_required:
    "Institutional authorization approved — the organization this event represents has consented in writing",
  representation_required:
    "Event represents an organization — this type cannot be run in a personal capacity",
  private_product_cannot_be_listed: "Private event is not listed in discovery",
  private_product_cannot_take_payment: "Private event takes no payment",
  registration_policy_not_allowed_for_type: "Registration policy is permitted for this event type",
  invitation_list_required: "Invitation list is in place",
  invited_registrants_still_pay: "Invited guests are still charged — no free bypass",
  approval_reviewer_required: "An approval reviewer is assigned",
  eligibility_criteria_required: "Eligibility criteria are defined",
};

export function ReviewChecklistPanel({ eventId, initial }: { eventId: string; initial: ReviewChecklist }) {
  const [checklist, setChecklist] = useState(initial);
  const [error, setError] = useState<string | null>(null);
  const [isPending, startTransition] = useTransition();

  function toggle(key: string, checked: boolean) {
    startTransition(async () => {
      setError(null);
      const res = await setReviewChecklistItemAction(eventId, key, checked);
      if ("error" in res) { setError(String(res.error)); return; }
      setChecklist(res.checklist);
    });
  }

  if (checklist.items.length === 0) {
    return (
      <p className="mt-3 flex items-center gap-1.5 text-xs text-muted">
        <ListChecks size={12} /> No outstanding requirements for this event.
      </p>
    );
  }

  return (
    <div className="mt-3 rounded-md border border-border bg-elevated p-3">
      <div className="flex items-center justify-between gap-2">
        <h4 className="flex items-center gap-1.5 text-xs font-semibold text-text">
          <ListChecks size={13} /> Reviewer checklist
        </h4>
        <span className={`text-[10px] font-semibold ${checklist.is_complete ? "text-success" : "text-warning"}`}>
          {checklist.items.filter((i) => i.checked).length}/{checklist.items.length}
          {checklist.is_complete ? " · complete" : " · approve is blocked"}
        </span>
      </div>

      <ul className="mt-2 space-y-1.5">
        {checklist.items.map((item) => (
          <li key={item.key}>
            <label className="flex items-start gap-2 text-xs text-text">
              <input
                type="checkbox"
                checked={item.checked}
                disabled={isPending}
                onChange={(e) => toggle(item.key, e.target.checked)}
                className="mt-0.5 h-3.5 w-3.5 shrink-0 accent-accent"
              />
              <span className={item.checked ? "text-muted line-through" : undefined}>
                {LABELS[item.key] ?? item.key}
                {item.blocking ? (
                  // A blocking item refuses the publish on its own; the rest only need confirming. Worth
                  // distinguishing so a reviewer knows which ticks are acknowledgements and which are gates.
                  <span className="ml-1.5 inline-flex items-center gap-0.5 align-middle text-[10px] font-semibold text-danger">
                    <ShieldAlert size={10} /> blocks publish
                  </span>
                ) : null}
              </span>
            </label>
          </li>
        ))}
      </ul>

      {error ? <p className="mt-2 text-xs text-danger">{error}</p> : null}
    </div>
  );
}
