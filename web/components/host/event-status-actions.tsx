"use client";

import { useState, useTransition } from "react";
import { useRouter } from "next/navigation";
import { Button, ConfirmDialog } from "@kurx/ui";
import { transitionEventAction, deleteDraftEventAction } from "@/lib/event-actions";

/// Next signals a redirect by throwing an error carrying a `NEXT_REDIRECT` digest. There is no public
/// helper for this in the app-router client runtime, so the digest is matched directly.
function isRedirectError(err: unknown): boolean {
  return typeof (err as { digest?: unknown } | null)?.digest === "string"
    && ((err as { digest: string }).digest.startsWith("NEXT_REDIRECT"));
}

const actionsByStatus: Record<string, { action: string; label: string }[]> = {
  draft: [
    { action: "submit_review", label: "Submit for Review" },
    { action: "publish", label: "Publish" }
  ],
  // D-266 M4: the host's own actions only. `publish`/`reject` used to sit on the review state here, but
  // those are reviewer transitions (IEventAuthority refuses them for a host) and the admin review console
  // owns them now. Once a reviewer has claimed the event there is nothing for the host to do.
  pendingreview: [{ action: "withdraw", label: "Withdraw from Review" }],
  underreview: [],
  changesrequested: [{ action: "submit_for_review", label: "Resubmit for Review" }],
  rejected: [{ action: "submit_for_review", label: "Resubmit for Review" }],
  approved: [{ action: "publish_approved", label: "Publish" }],
  // D-319 — web offered 7 of the 11 lifecycle actions mobile has had all along, and the four it omitted
  // are the ones that carry an event through to its end: an event could be published here but never
  // opened, taken live or completed. `schedule`/`open_registration`/`go_live`/`complete` are the §14.2
  // gated steps; a refused one reports its gate (`no_pass`, `no_staff_assigned`, …) through `run` below.
  scheduled: [
    { action: "open_registration", label: "Open registration" },
    { action: "unpublish", label: "Unpublish" }
  ],
  published: [
    { action: "go_live", label: "Go live" },
    { action: "complete", label: "Complete" },
    { action: "unpublish", label: "Unpublish" },
    { action: "close", label: "Close" }
  ],
  live: [{ action: "complete", label: "Complete" }],
  completed: [{ action: "archive", label: "Archive" }],
  closed: [{ action: "archive", label: "Archive" }],
  cancelled: [{ action: "archive", label: "Archive" }],
  archived: []
};

/**
 * Transitions with no way back, which therefore ask first. `cancel` is terminal by D-101 — a cancelled
 * event is never resurrected, it is cloned — so it is the one lifecycle action that must not fire on a
 * single click. It is offered from every state the workflow table allows it from.
 */
const CANCELLABLE = new Set(["draft", "pendingreview", "underreview", "published", "scheduled", "live"]);

export function EventStatusActions({ orgId, eventId, status }: { orgId: string; eventId: string; status: string }) {
  const [isPending, startTransition] = useTransition();
  const [error, setError] = useState<string | null>(null);
  const [confirmingDelete, setConfirmingDelete] = useState(false);
  const [confirmingCancel, setConfirmingCancel] = useState(false);
  const router = useRouter();
  const key = status.toLowerCase();
  const actions = actionsByStatus[key] ?? [];

  function run(action: string) {
    startTransition(async () => {
      setError(null);
      const result = await transitionEventAction(orgId, eventId, action);
      // A refused transition used to be silent — the button did nothing and said nothing. D-266 M5's
      // publish gates made that far more likely, so the reason is shown instead of discarded.
      if (result && "error" in result) { setError(result.error ?? "That action was refused."); return; }
      router.refresh();
    });
  }

  /*
   * Deleting the whole draft event fired on one click, and the call was unguarded — a refusal or a
   * dropped connection left the button looking like it had worked, on the single most destructive
   * action in the host surface. Every sibling transition above already surfaces its refusal; this one
   * did not, and it is the one that cannot be undone.
   */
  function remove() {
    startTransition(async () => {
      try {
        await deleteDraftEventAction(orgId, eventId);
      } catch (err) {
        // `deleteDraftEventAction` ends in `redirect("/workspace")`, and Next implements redirect by
        // THROWING. Catching indiscriminately here would swallow the success path and report a
        // failure on every successful delete — so the redirect signal is re-thrown untouched and only
        // a real error is surfaced.
        if (isRedirectError(err)) throw err;
        setError("That draft could not be deleted. Nothing has changed.");
      }
    });
  }

  return (
    <div className="space-y-2">
      <div className="flex flex-wrap items-center gap-2">
        {actions.map((a) => (
          <Button key={a.action} variant="secondary" disabled={isPending} onClick={() => run(a.action)}>
            {a.label}
          </Button>
        ))}
        {CANCELLABLE.has(key) ? (
          <Button variant="ghost" disabled={isPending} onClick={() => setConfirmingCancel(true)}>Cancel event</Button>
        ) : null}
        {key === "draft" ? (
          <Button variant="ghost" disabled={isPending} onClick={() => setConfirmingDelete(true)}>Delete draft</Button>
        ) : null}
      </div>
      <ConfirmDialog
        open={confirmingCancel}
        title="Cancel this event?"
        description="Cancelling is permanent — a cancelled event cannot be published again, and anyone already registered will need to be refunded. If you want to run it later, clone it instead."
        confirmLabel="Cancel event"
        tone="danger"
        onConfirm={() => { setConfirmingCancel(false); run("cancel"); }}
        onClose={() => setConfirmingCancel(false)}
      />
      <ConfirmDialog
        open={confirmingDelete}
        title="Delete this draft event?"
        description="Everything set up on it — schedule, ticket types, registration form — goes with it. This cannot be undone."
        confirmLabel="Delete draft"
        tone="danger"
        onConfirm={remove}
        onClose={() => setConfirmingDelete(false)}
      />
      {error ? (
        <p role="alert" className="rounded-md border border-danger/40 bg-danger/10 px-3 py-2 text-sm text-danger">
          {error}
        </p>
      ) : null}
    </div>
  );
}
