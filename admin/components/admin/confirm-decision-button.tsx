"use client";

import { useRef, useState } from "react";
import { useFormStatus } from "react-dom";
import { ConfirmDialog } from "@kurx/ui";

/**
 * A submit button for a moderation decision, with the confirmation in front of it.
 *
 * Approving publishes an event to everyone; rejecting takes it away from an organiser who has
 * usually spent days on it. Both fired on a single click, from buttons sitting next to each other,
 * with nothing in between — on the console where the stakes are highest in the product.
 *
 * The form still submits natively: confirming calls `requestSubmit()` on the owning form, so the
 * server action, its validation and its revalidation are untouched. This adds a gate; it does not
 * take over the submission.
 *
 * `useFormStatus` reports the state of the owning form, not of this button — which is the scope that
 * matters here, because Approve and Reject sit in the *same* form on `event-review-form.tsx`. Without
 * it, Reject stayed live while an Approve was in flight against the same event.
 *
 * Deliberately not applied to `reject_review` / `request_changes` in `review-actions.tsx`: those
 * already open a reason form the reviewer must fill in, which is a stronger gate than a dialog.
 */
export function ConfirmDecisionButton({
  label,
  title,
  description,
  confirmLabel,
  tone,
  name,
  value,
}: {
  label: string;
  title: string;
  description: string;
  confirmLabel: string;
  tone: "approve" | "reject";
  /** Forwarded to the hidden field so the server action sees the same payload it did before. */
  name?: string;
  value?: string;
}) {
  const [open, setOpen] = useState(false);
  const anchor = useRef<HTMLSpanElement>(null);
  const { pending } = useFormStatus();

  const cls =
    tone === "approve"
      ? "border-success/40 text-success hover:bg-success/10"
      : "border-danger/40 text-danger hover:bg-danger/10";

  return (
    <span ref={anchor} className="inline-flex">
      {name ? <input type="hidden" name={name} value={value} /> : null}
      <button
        type="button"
        disabled={pending}
        onClick={() => setOpen(true)}
        className={`inline-flex min-h-11 items-center rounded-md border px-3 text-xs font-semibold transition duration-fast focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent disabled:cursor-not-allowed disabled:opacity-60 ${cls}`}
      >
        {pending ? `${label}\u2026` : label}
      </button>
      <ConfirmDialog
        open={open}
        title={title}
        description={description}
        confirmLabel={confirmLabel}
        tone={tone === "reject" ? "danger" : "default"}
        onConfirm={() => anchor.current?.closest("form")?.requestSubmit()}
        onClose={() => setOpen(false)}
      />
    </span>
  );
}
