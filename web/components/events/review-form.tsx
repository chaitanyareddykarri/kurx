"use client";

import { useFormState, useFormStatus } from "react-dom";
import { submitReviewAction } from "@/lib/social-actions";
import { Button } from "@/components/ui/button";

const inputClass = "h-10 w-full rounded-md border border-border bg-background px-3 text-sm text-text";

function SubmitButton() {
  const { pending } = useFormStatus();
  return (
    <Button type="submit" variant="secondary" disabled={pending} className="shrink-0">
      {pending ? "Posting…" : "Post review"}
    </Button>
  );
}

export function ReviewForm({ eventId, slug }: { eventId: string; slug: string }) {
  const [state, formAction] = useFormState(submitReviewAction.bind(null, eventId, slug), null);

  return (
    <form action={formAction} className="mt-4 space-y-3 rounded-md border border-border bg-surface p-4">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-end">
        <div>
          <label className="text-sm font-medium text-text" htmlFor="r-rating">Rating</label>
          <select id="r-rating" name="rating" defaultValue="5" className={`mt-1 ${inputClass}`}>
            {[5, 4, 3, 2, 1].map((n) => <option key={n} value={n}>{"★".repeat(n)} ({n})</option>)}
          </select>
        </div>
        <div className="flex-1">
          <label className="text-sm font-medium text-text" htmlFor="r-title">Title (optional)</label>
          <input id="r-title" name="title" maxLength={120} className={`mt-1 ${inputClass}`} />
        </div>
      </div>
      <div>
        <label className="text-sm font-medium text-text" htmlFor="r-body">Your review (optional)</label>
        <textarea id="r-body" name="body" rows={3} maxLength={2000}
          className="mt-1 w-full rounded-md border border-border-strong bg-background px-3 py-2 text-sm text-text" />
      </div>
      <div className="flex items-center justify-between">
        <label className="flex items-center gap-2 text-sm text-muted">
          <input type="checkbox" name="anonymous" /> Post anonymously
        </label>
        <SubmitButton />
      </div>
      {state && "error" in state ? (
        <p className="text-sm text-danger">{String(state.error)}</p>
      ) : state && "ok" in state ? (
        <p className="text-sm text-success">Thanks — your review is live.</p>
      ) : null}
    </form>
  );
}
