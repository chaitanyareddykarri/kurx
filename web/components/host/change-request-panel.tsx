"use client";

import { useState, useTransition } from "react";
import { useRouter } from "next/navigation";
import { Alert, Badge, Button, Spinner } from "@kurx/ui";
import { withdrawEventChangesAction } from "@/lib/event-actions";
import type { EventChangeRequest } from "@/lib/api";

/*
 * D-388 — what the host sees instead of a Save button once their event is live.
 *
 * The pending state is shown BEFORE the form, not after it, and the form is not rendered at all while a
 * request is open. Leaving an editable form beside a pending proposal would invite a second edit that
 * silently replaces the first — the host would have no way to know which values a reviewer was looking
 * at, and neither would the reviewer.
 */

/** The diff, as the host wrote it and the reviewer will read it. Same shape on both surfaces on purpose. */
export function ChangeList({ changes }: { changes: EventChangeRequest["changes"] }) {
  if (changes.length === 0) {
    return <p className="text-sm text-muted">No field differences.</p>;
  }
  return (
    <dl className="divide-y divide-border rounded-md border border-border">
      {changes.map((c) => (
        <div key={c.field} className="grid gap-1 p-3 sm:grid-cols-[10rem_1fr]">
          <dt className="text-xs font-semibold uppercase tracking-wide text-muted">{c.label}</dt>
          <dd className="space-y-1 text-sm">
            {/* Current struck through rather than merely labelled: at a glance the reviewer needs to see
                which value is going away, and a colour alone does not survive a greyscale print or a
                colour-vision difference. */}
            <p className="text-muted">
              <span className="mr-2 text-xs uppercase">Current</span>
              <span className="line-through">{c.current ?? "— empty —"}</span>
            </p>
            <p className="text-text">
              <span className="mr-2 text-xs uppercase text-muted">Proposed</span>
              <span className="font-medium">{c.proposed ?? "— empty —"}</span>
            </p>
          </dd>
        </div>
      ))}
    </dl>
  );
}

/// Badge's own tone vocabulary — it has no `info`, and `accent` is the "in progress, nothing wrong"
/// tone. Withdrawn is `muted` rather than `danger`: the host chose it, it is not a refusal.
const STATUS_TONE: Record<string, "accent" | "success" | "danger" | "muted"> = {
  pending: "accent",
  approved: "success",
  rejected: "danger",
  withdrawn: "muted"
};

export function PendingChangeRequest({
  orgId,
  eventId,
  request
}: {
  orgId: string;
  eventId: string;
  request: EventChangeRequest;
}) {
  const router = useRouter();
  const [pending, start] = useTransition();
  const [error, setError] = useState<string | null>(null);

  function withdraw() {
    setError(null);
    start(async () => {
      const result = await withdrawEventChangesAction(orgId, eventId, request.id);
      if (result && "error" in result && result.error) setError(String(result.error));
      else router.refresh();
    });
  }

  return (
    <div className="space-y-4">
      <Alert tone="info" title="Changes pending approval">
        Your event is unchanged and still live as it was. A reviewer is looking at the changes below;
        they take effect only once approved.
      </Alert>

      {/* The staleness the server will refuse on, surfaced BEFORE the reviewer refuses it. Without this
          the host learns their proposal is unapplicable only after someone tried to approve it. */}
      {request.stale ? (
        <Alert tone="warning" title="This event changed after you proposed these">
          It was on version {request.base_version} when you submitted and is on {request.current_version}{" "}
          now, so a reviewer can&apos;t apply these values without overwriting the newer ones. Withdraw
          and propose again from the current details.
        </Alert>
      ) : null}

      <div className="flex flex-wrap items-center gap-2 text-xs text-muted">
        <Badge tone={STATUS_TONE[request.status] ?? "neutral"}>{request.status}</Badge>
        <span>Submitted {new Date(request.created_at).toLocaleString("en-IN")}</span>
        {request.requested_by_name ? <span>· by {request.requested_by_name}</span> : null}
      </div>

      {request.reason ? (
        <p className="rounded-md border border-border bg-elevated px-3 py-2 text-sm text-muted">
          <span className="font-semibold text-text">Your note: </span>
          {request.reason}
        </p>
      ) : null}

      <ChangeList changes={request.changes} />

      {error ? (
        <p role="alert" className="rounded-md border border-danger/40 bg-danger/10 px-3 py-2 text-sm text-danger">
          {error}
        </p>
      ) : null}

      <Button type="button" variant="secondary" onClick={withdraw} disabled={pending}>
        {pending ? <Spinner size={16} decorative /> : null}
        {pending ? "Withdrawing…" : "Withdraw and edit again"}
      </Button>
    </div>
  );
}

/// A decided request, kept visible so the host can read WHY. A rejection with no reason on screen is a
/// dead end — the host has to guess what to change.
export function DecidedChangeRequest({ request }: { request: EventChangeRequest }) {
  const decided = request.status === "approved" ? "Approved" : request.status === "rejected" ? "Rejected" : "Withdrawn";
  return (
    <details className="rounded-md border border-border p-3">
      <summary className="flex cursor-pointer flex-wrap items-center gap-2 text-sm">
        <Badge tone={STATUS_TONE[request.status] ?? "neutral"}>{decided}</Badge>
        <span className="text-muted">
          {request.changes.length} field{request.changes.length === 1 ? "" : "s"} ·{" "}
          {new Date(request.reviewed_at ?? request.updated_at).toLocaleDateString("en-IN")}
          {request.reviewed_by_name ? ` · ${request.reviewed_by_name}` : ""}
        </span>
      </summary>
      <div className="mt-3 space-y-3">
        {request.review_reason_code || request.review_notes ? (
          <div className="rounded-md border border-border bg-elevated px-3 py-2 text-sm">
            {request.review_reason_code ? (
              <p className="font-semibold text-text">Reason: {request.review_reason_code}</p>
            ) : null}
            {request.review_notes ? <p className="mt-1 text-muted">{request.review_notes}</p> : null}
          </div>
        ) : null}
        <ChangeList changes={request.changes} />
      </div>
    </details>
  );
}
