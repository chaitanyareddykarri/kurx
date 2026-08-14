"use client";

import { useRef, useState } from "react";
import { useFormStatus } from "react-dom";
import { Button, ConfirmDialog } from "@kurx/ui";

/**
 * A submit button for a destructive server action, with the confirmation step in front of it.
 *
 * The host surface deletes things through plain `<form action={serverAction}>` submits — a ticket
 * type, a registration-form field — and every one of them fired on a single click, irreversibly,
 * with nothing in between. `ConfirmDialog` exists for exactly this and had one call site in the whole
 * of web.
 *
 * The form still submits natively: the dialog's confirm calls `requestSubmit()` on the owning form,
 * so the server action, its progressive enhancement and its revalidation are all untouched. This adds
 * a gate, it does not take over the submission.
 *
 * Because the submit is native there is no client-side promise to hang a spinner on, so until Phase 42
 * the button looked identical before and during the delete. A confirmed delete on a slow connection
 * showed nothing at all, and the obvious response to that is to click again — which reopens the dialog
 * and fires a second one. `useFormStatus` reports the owning form's state, which is what makes this a
 * fix rather than a guess about how long the action takes.
 */
export function ConfirmSubmitButton({
  label,
  title,
  description,
  confirmLabel = "Delete",
  className = "",
}: {
  /** The visible button label, and the accessible name unless `title` narrows it. */
  label: string;
  /** Names what is being destroyed — "Delete the Early Bird ticket type?", not "Are you sure?". */
  title: string;
  description: string;
  confirmLabel?: string;
  className?: string;
}) {
  const [open, setOpen] = useState(false);
  const anchor = useRef<HTMLSpanElement>(null);
  const { pending } = useFormStatus();

  return (
    <span ref={anchor} className={className}>
      <Button type="button" variant="ghost" disabled={pending} onClick={() => setOpen(true)}>
        {pending ? `${label}\u2026` : label}
      </Button>
      <ConfirmDialog
        open={open}
        title={title}
        description={description}
        confirmLabel={confirmLabel}
        tone="danger"
        onConfirm={() => {
          // `requestSubmit` rather than `submit`: it runs validation and fires the submit event, which
          // is what React's server-action binding listens for. `submit()` would bypass both.
          anchor.current?.closest("form")?.requestSubmit();
        }}
        onClose={() => setOpen(false)}
      />
    </span>
  );
}
