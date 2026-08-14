"use client";

import { ReactNode, useState } from "react";
import { Dialog } from "./dialog";
import { Button } from "./button";
import { Spinner } from "./spinner";

type ConfirmDialogProps = {
  open: boolean;
  onClose: () => void;
  /** May be async; the confirm button shows a spinner until it resolves. */
  onConfirm: () => void | Promise<void>;
  title: string;
  description?: ReactNode;
  confirmLabel?: string;
  cancelLabel?: string;
  /** `danger` for destructive/irreversible actions (suspend, blacklist, refund). */
  tone?: "default" | "danger";
};

/** The mandatory gate before any destructive admin action. Handles the async
 *  confirm lifecycle (pending, error surfaced by caller via toast) so screens
 *  never fire a suspend/blacklist/refund straight off a click. */
export function ConfirmDialog({
  open,
  onClose,
  onConfirm,
  title,
  description,
  confirmLabel = "Confirm",
  cancelLabel = "Cancel",
  tone = "default"
}: ConfirmDialogProps) {
  const [pending, setPending] = useState(false);

  async function handleConfirm() {
    if (pending) return;
    setPending(true);
    try {
      await onConfirm();
      onClose();
    } finally {
      setPending(false);
    }
  }

  return (
    <Dialog
      open={open}
      onClose={pending ? () => undefined : onClose}
      title={title}
      footer={
        <>
          <Button variant="ghost" onClick={onClose} disabled={pending}>
            {cancelLabel}
          </Button>
          {/*
            The destructive treatment used to be hand-rolled here as
            `bg-danger text-white`. White on the dark-theme danger token is
            3.29:1 — below AA — so the most consequential button in the console
            failed in one theme. The shared `danger` variant uses
            `text-background`, which holds in both (5.11 / 5.51).
          */}
          <Button
            variant={tone === "danger" ? "danger" : "secondary"}
            onClick={handleConfirm}
            disabled={pending}
          >
            {pending ? <Spinner size={15} decorative /> : null}
            {confirmLabel}
          </Button>
        </>
      }
    >
      {description ?? "This action cannot be undone."}
    </Dialog>
  );
}
