"use client";

import { useEffect, useRef, useState } from "react";
import { useFormState, useFormStatus } from "react-dom";
import { Megaphone, TriangleAlert } from "lucide-react";
import { Button, Card, ConfirmDialog, Field, Input, Textarea, useToast } from "@kurx/ui";
import { broadcastAction } from "@/lib/admin-actions";

const TITLE_MAX = 200;
const MESSAGE_MAX = 2000;

/** Inside the form so useFormStatus can report the in-flight fan-out, which is slow by design. */
function SendButton({ onOpen, disabled }: { onOpen: () => void; disabled: boolean }) {
  const { pending } = useFormStatus();
  return (
    <Button type="button" onClick={onOpen} disabled={disabled || pending}>
      <Megaphone size={15} />
      {pending ? "Sending…" : "Send to everyone"}
    </Button>
  );
}

export function BroadcastForm() {
  const [state, action] = useFormState(broadcastAction, null);
  const [confirming, setConfirming] = useState(false);
  const formRef = useRef<HTMLFormElement>(null);
  const [title, setTitle] = useState("");
  const [message, setMessage] = useState("");
  const toast = useToast();

  const ready = title.trim().length > 0 && message.trim().length > 0;

  useEffect(() => {
    if (state && "ok" in state) toast(`Sent to ${(state as { sent?: number }).sent ?? 0} users.`, "success");
    else if (state && "error" in state) toast(String(state.error), "error");
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [state]);

  return (
    <>
      <Card>
        <form ref={formRef} action={action} className="space-y-4">
          <Field label="Title" htmlFor="bc-title" helper={`${title.length}/${TITLE_MAX}`}>
            <Input
              id="bc-title"
              name="title"
              required
              maxLength={TITLE_MAX}
              value={title}
              onChange={(e) => setTitle(e.target.value)}
            />
          </Field>

          <Field label="Message" htmlFor="bc-message" helper={`${message.length}/${MESSAGE_MAX}`}>
            <Textarea
              id="bc-message"
              name="message"
              required
              rows={5}
              maxLength={MESSAGE_MAX}
              value={message}
              onChange={(e) => setMessage(e.target.value)}
            />
          </Field>

          <Field label="Data JSON" htmlFor="bc-data" helper="Optional deep-link payload">
            <Input id="bc-data" name="dataJson" placeholder='{"route":"/discover"}' className="font-mono text-xs" />
          </Field>

          <div className="flex items-start gap-2 rounded-md border border-danger/30 bg-danger/5 p-3">
            <TriangleAlert size={16} className="mt-0.5 shrink-0 text-danger" />
            <p className="text-xs text-muted">
              This reaches <strong className="text-text">every registered user</strong>. It cannot be recalled,
              edited, or targeted at a subset.
            </p>
          </div>

          {/* Submitting is gated behind the confirmation dialog, which calls requestSubmit(). */}
          <SendButton onOpen={() => setConfirming(true)} disabled={!ready} />
        </form>
      </Card>

      <ConfirmDialog
        open={confirming}
        onClose={() => setConfirming(false)}
        onConfirm={() => formRef.current?.requestSubmit()}
        title="Send this to every user?"
        description={
          <div className="space-y-2">
            <p>
              “{title}” will be delivered to every registered account immediately. This cannot be undone.
            </p>
            <p className="text-muted">The request stays open until the fan-out finishes.</p>
          </div>
        }
        confirmLabel="Send broadcast"
        tone="danger"
      />
    </>
  );
}
