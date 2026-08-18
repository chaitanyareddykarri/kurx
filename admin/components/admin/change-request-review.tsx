"use client";

import { useEffect, useRef, useState } from "react";
import { useFormState, useFormStatus } from "react-dom";
import { useToast } from "@kurx/ui";
import { decideChangeRequestAction } from "@/lib/admin-actions";
import type { EventChangeRequest } from "@/lib/api";

/*
 * D-388 — CURRENT vs PROPOSED, and the two buttons that decide it.
 *
 * The comparison is rendered from the server's `changes[]` verbatim. Nothing is derived here: the point
 * of a review screen is that the reviewer and the apply path agree about what is being changed, and a
 * console that re-formats those values is a second opinion on that question.
 */

/** The same closed vocabulary the event review uses. Labels are presentation; the value posted is the
 *  enum name, so changing the copy can never change what is recorded. */
const REASONS = [
  ["Incomplete", "Incomplete — can't evaluate the change"],
  ["ProhibitedContent", "Prohibited content"],
  ["UnverifiedOrganiser", "Unverified organiser"],
  ["MisrepresentedAffiliation", "Misrepresented affiliation"],
  ["InvalidCommerce", "Invalid pricing or refund terms"],
  ["Duplicate", "Duplicate of an existing event"],
  ["Other", "Other — explain in notes"],
] as const;

function Submit({ label, tone, disabled }: { label: string; tone: "approve" | "reject"; disabled?: boolean }) {
  const { pending } = useFormStatus();
  const cls = tone === "approve"
    ? "border-success/40 text-success hover:bg-success/10"
    : "border-danger/40 text-danger hover:bg-danger/10";
  return (
    <button type="submit" disabled={pending || disabled}
      className={`h-8 rounded-md border px-3 text-xs font-semibold disabled:opacity-50 ${cls}`}>
      {pending ? "…" : label}
    </button>
  );
}

/** One field's before and after. Both values are shown even when one is empty — "was blank" is a change
 *  a reviewer needs to see, and rendering nothing for it would hide it. */
function ChangeRow({ label, current, proposed }: { label: string; current: string | null; proposed: string | null }) {
  return (
    <div className="grid gap-1 border-b border-border py-2 last:border-0 sm:grid-cols-[9rem_1fr_1fr]">
      <div className="text-[11px] font-semibold uppercase tracking-wide text-muted">{label}</div>
      <div className="text-xs text-muted">
        <span className="mr-1 text-[10px] uppercase">Current</span>
        <span className="line-through">{current ?? "— empty —"}</span>
      </div>
      <div className="text-xs text-text">
        <span className="mr-1 text-[10px] uppercase text-muted">Proposed</span>
        <span className="font-semibold">{proposed ?? "— empty —"}</span>
      </div>
    </div>
  );
}

export function ChangeRequestReview({ request }: { request: EventChangeRequest }) {
  const toast = useToast();
  const [state, formAction] = useFormState(
    decideChangeRequestAction.bind(null, request.representing_org_id, request.event_id, request.id), null);
  const [rejecting, setRejecting] = useState(false);

  // Same guard as review-actions.tsx: StrictMode re-runs this effect in development, and one decision
  // announced twice reads as two decisions.
  const toasted = useRef<unknown>(null);
  useEffect(() => {
    if (!state || toasted.current === state) return;
    toasted.current = state;
    if ("error" in state) toast(String(state.error), "error");
    if ("ok" in state) {
      toast(state.approved
        ? "Approved — the changes are live on the event now."
        : "Rejected — the event is unchanged.", "success");
      setRejecting(false);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [state]);

  return (
    <div className="mt-3 rounded-md border border-border p-3">
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <h4 className="text-sm font-semibold text-text">Proposed changes</h4>
        <span className="text-[11px] text-muted">
          v{request.base_version} → v{request.base_version + 1} · requested{" "}
          {new Date(request.created_at).toLocaleString("en-IN")}
          {request.requested_by_name ? ` by ${request.requested_by_name}` : ""}
        </span>
      </div>

      {/*
        A stale proposal cannot be approved — the server refuses it with `version_conflict` to protect the
        newer approved values. Said here, with the button disabled, rather than letting the reviewer press
        Approve and read a refusal: an action that can only fail should not be offered.
      */}
      {request.stale ? (
        <p className="mt-2 rounded-md border border-warning/40 bg-warning/5 p-2 text-xs text-warning">
          This event has changed since the host proposed these (it was on v{request.base_version}, it is on
          v{request.current_version} now). Approving would overwrite the newer values, so it is refused —
          reject this and ask the host to propose again from the current details.
        </p>
      ) : null}

      {request.reason ? (
        <p className="mt-2 rounded-md border border-border bg-elevated p-2 text-xs text-muted">
          <span className="font-semibold text-text">Host&apos;s note: </span>{request.reason}
        </p>
      ) : null}

      <div className="mt-3">
        {request.changes.length === 0 ? (
          // Every proposed value now equals the live one — someone else already made this change. Named
          // rather than rendered as an empty box, so the reviewer knows there is nothing to decide.
          <p className="text-xs text-muted">
            Nothing differs from the live event any more. This request has been overtaken; reject it.
          </p>
        ) : (
          request.changes.map((c) => (
            <ChangeRow key={c.field} label={c.label} current={c.current} proposed={c.proposed} />
          ))
        )}
      </div>

      <form action={formAction} className="mt-3 space-y-2">
        {rejecting ? (
          <>
            <label className="block text-[11px] font-semibold uppercase tracking-wide text-muted" htmlFor={`reason-${request.id}`}>
              Reason
            </label>
            <select id={`reason-${request.id}`} name="reasonCode" required
              className="h-8 w-full rounded-md border border-border bg-surface px-2 text-xs text-text">
              {REASONS.map(([value, label]) => <option key={value} value={value}>{label}</option>)}
            </select>
            <textarea name="notes" rows={2} maxLength={2000}
              placeholder="What the host needs to change (shown to them)."
              className="w-full rounded-md border border-border bg-surface p-2 text-xs text-text" />
          </>
        ) : null}

        <div className="flex flex-wrap items-center gap-2">
          {rejecting ? (
            <>
              <input type="hidden" name="decision" value="reject" />
              <Submit label="Confirm rejection" tone="reject" />
              <button type="button" onClick={() => setRejecting(false)}
                className="h-8 rounded-md border border-border px-3 text-xs font-semibold text-muted hover:bg-elevated">
                Cancel
              </button>
            </>
          ) : (
            <>
              <input type="hidden" name="decision" value="approve" />
              <Submit label="Approve changes" tone="approve" disabled={request.stale || request.changes.length === 0} />
              <button type="button" onClick={() => setRejecting(true)}
                className="h-8 rounded-md border border-danger/40 px-3 text-xs font-semibold text-danger hover:bg-danger/10">
                Reject…
              </button>
            </>
          )}
        </div>
      </form>
    </div>
  );
}
