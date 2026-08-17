"use client";

import { useEffect, useRef, useState } from "react";
import { useFormState, useFormStatus } from "react-dom";
import { useToast } from "@kurx/ui";
import { ConfirmDecisionButton } from "@/components/admin/confirm-decision-button";
import { reviewEventAction } from "@/lib/admin-actions";

/** The reason vocabulary is the backend's closed enum. Labels are presentation only — the value sent is
 *  always the enum name, so a copy change here can never alter what is recorded. */
const REASONS = [
  ["Incomplete", "Incomplete — details too thin to evaluate"],
  ["ProhibitedContent", "Prohibited content — breaches policy"],
  ["UnverifiedOrganiser", "Unverified organiser"],
  ["MisrepresentedAffiliation", "Misrepresented affiliation"],
  ["InvalidCommerce", "Invalid pricing or refund terms"],
  ["Duplicate", "Duplicate of an existing event"],
  ["Other", "Other — explain in notes"],
] as const;

function Submit({ label, tone }: { label: string; tone: "approve" | "reject" | "neutral" }) {
  const { pending } = useFormStatus();
  const cls =
    tone === "approve" ? "border-success/40 text-success hover:bg-success/10"
    : tone === "reject" ? "border-danger/40 text-danger hover:bg-danger/10"
    : "border-border text-text hover:bg-elevated";
  return (
    <button type="submit" disabled={pending}
      className={`h-8 rounded-md border px-3 text-xs font-semibold disabled:opacity-50 ${cls}`}>
      {pending ? "…" : label}
    </button>
  );
}

/** Which actions the current status permits. Mirrors EventStatusWorkflow so the console does not offer a
 *  button the backend will refuse — but the backend remains the authority: an invalid transition still
 *  returns 409 and is surfaced, rather than being silently prevented here.
 *
 *  Matched lowercased: the admin events API serialises status as the lowercased enum name
 *  (`AdminEventEndpoints` → `e.Status.ToLowerInvariant()`), NOT the PascalCase member name the tab
 *  query-string uses. Comparing against PascalCase here matched nothing in every state, so the console
 *  rendered "No review actions available" for the whole queue. */
function actionsFor(status: string): string[] {
  switch (status.toLowerCase()) {
    case "pendingreview": return ["claim_review", "withdraw"];
    case "underreview": return ["approve_review", "request_changes", "reject_review", "release_review"];
    case "changesrequested": return ["submit_for_review"];
    // `withdraw` (D-363 §1) because approving in error had no undo: once here the console offered only
    // "Publish", so a reviewer who cleared the wrong event could do nothing but publish it or ask the
    // organiser to withdraw it for them. It returns the event to Draft, where it can be reworked.
    // Requires the SuperAdmin claim — `withdraw` is not a reviewer-scoped action, so a reviewer without
    // it is refused, exactly as on the `pendingreview` button beside it.
    case "approved": return ["publish_approved", "withdraw"];
    case "rejected": return ["submit_for_review"];
    case "draft": return ["submit_for_review"];
    default: return [];
  }
}

/** Where each decision sends the event. Mirrors EventStatusWorkflow — the server is the authority; this
 *  only tells the reviewer where to look for what they just did. */
const DESTINATION: Record<string, string> = {
  claim_review: "Under review", release_review: "Pending review",
  approve_review: "Approved", reject_review: "Rejected",
  request_changes: "Changes requested", withdraw: "Draft",
  submit_for_review: "Pending review", publish_approved: "Published",
  publish: "Published", reject: "Draft",
};

const LABELS: Record<string, string> = {
  claim_review: "Claim", release_review: "Release", approve_review: "Approve",
  reject_review: "Reject", request_changes: "Request changes", withdraw: "Withdraw",
  submit_for_review: "Submit for review", publish_approved: "Publish",
  publish: "Approve & publish", reject: "Reject",
};

export function ReviewActions({ orgId, eventId, status }: { orgId: string; eventId: string; status: string }) {
  const [state, formAction] = useFormState(reviewEventAction.bind(null, orgId, eventId), null);
  const [open, setOpen] = useState<string | null>(null);
  const toast = useToast();

  // See event-review-form.tsx: StrictMode re-runs this effect in development, and without the guard
  // one decision produced two toasts. Applies to the success path too — "Moved to Published." twice
  // reads as two publishes.
  const toasted = useRef<unknown>(null);
  useEffect(() => {
    if (!state || toasted.current === state) return;
    toasted.current = state;
    if ("error" in state) toast(String(state.error), "error");
    if ("ok" in state) {
      // Name the destination. Previously this said only "Decision recorded" and the row vanished from
      // the tab — the reviewer's own change became invisible to them, with no way to tell an approval
      // from a rejection after the fact except by hunting the other tabs.
      const moved = DESTINATION[state.action ?? ""];
      toast(moved ? `Moved to ${moved}.` : "Decision recorded.", "success");
      setOpen(null);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [state]);

  const available = actionsFor(status);
  if (available.length === 0) {
    return <p className="mt-3 text-xs text-muted">No review actions available in {status}.</p>;
  }

  // reject_review needs a structured reason; request_changes needs notes. Both are ENFORCED by the
  // backend — these forms exist so a reviewer is not made to guess, not to be the validation.
  const needsForm = open === "reject_review" || open === "request_changes";

  return (
    <div className="mt-3 space-y-2">
      <div className="flex flex-wrap items-center gap-2">
        {available.map((a) =>
          a === "reject_review" || a === "request_changes" ? (
            <button key={a} type="button" onClick={() => setOpen(open === a ? null : a)}
              className={`h-8 rounded-md border px-3 text-xs font-semibold ${
                a === "reject_review" ? "border-danger/40 text-danger hover:bg-danger/10"
                                      : "border-border text-text hover:bg-elevated"}`}>
              {LABELS[a] ?? a}
            </button>
          ) : (
            <form key={a} action={formAction} className="inline">
              {/* `reject_review` and `request_changes` are handled above: each opens a reason form
                  the reviewer must fill in, which is a stronger gate than a dialog. These are the
                  ones that fired straight through — including publishing an approved event. */}
              {a === "approve_review" || a === "publish_approved" || a === "publish" || a === "reject" ? (
                <ConfirmDecisionButton
                  name="action"
                  value={a}
                  label={LABELS[a] ?? a}
                  tone={a === "reject" ? "reject" : "approve"}
                  title={`${LABELS[a] ?? a}?`}
                  description={
                    a === "reject"
                      ? "The organiser is told it was rejected and it cannot be published without a fresh review."
                      : a === "approve_review"
                        ? "The event clears review. It still needs publishing before anyone can see it."
                        : "The event becomes visible to everyone and can start taking registrations immediately."
                  }
                  confirmLabel={LABELS[a] ?? a}
                />
              ) : (
                <>
                  <input type="hidden" name="action" value={a} />
                  <Submit label={LABELS[a] ?? a} tone="neutral" />
                </>
              )}
            </form>
          ),
        )}
      </div>

      {needsForm ? (
        <form action={formAction} className="space-y-2 rounded-md border border-border bg-elevated p-3">
          <input type="hidden" name="action" value={open!} />
          {open === "reject_review" ? (
            <label className="block text-xs text-muted">
              Reason
              <select name="reasonCode" required defaultValue=""
                className="mt-1 h-8 w-full rounded-md border border-border-strong bg-surface px-2 text-xs text-text">
                <option value="" disabled>Select a reason…</option>
                {REASONS.map(([v, l]) => <option key={v} value={v}>{l}</option>)}
              </select>
            </label>
          ) : null}
          <label className="block text-xs text-muted">
            Notes {open === "request_changes" ? <span className="text-danger">*</span> : "(optional)"}
            <textarea name="notes" rows={3} required={open === "request_changes"}
              placeholder={open === "request_changes"
                ? "What the organiser needs to change before resubmitting."
                : "Context for the audit trail. Never shown verbatim to attendees."}
              className="mt-1 w-full rounded-md border border-border-strong bg-surface px-2 py-1 text-xs text-text" />
          </label>
          <div className="flex gap-2">
            <Submit label={open === "reject_review" ? "Confirm reject" : "Send request"}
              tone={open === "reject_review" ? "reject" : "neutral"} />
            <button type="button" onClick={() => setOpen(null)}
              className="h-8 rounded-md border border-border px-3 text-xs text-muted hover:bg-elevated">
              Cancel
            </button>
          </div>
        </form>
      ) : null}

      {state && "error" in state ? <p className="text-xs text-danger">{String(state.error)}</p> : null}
    </div>
  );
}
