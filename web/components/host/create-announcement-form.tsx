"use client";

import { useFormState, useFormStatus } from "react-dom";
import { createAnnouncementAction } from "@/lib/announcement-actions";
import { withUtcTimes } from "@/lib/event-wizard";
import { Button, controlClass } from "@kurx/ui";

const inputClass = controlClass;
const textareaClass = controlClass;
const labelClass = "text-sm font-medium text-text";

function SubmitButton() {
  const { pending } = useFormStatus();
  return <Button type="submit" disabled={pending}>{pending ? "Sending…" : "Send announcement"}</Button>;
}

export function CreateAnnouncementForm({ eventId }: { eventId: string }) {
  const [state, formAction] = useFormState(createAnnouncementAction.bind(null, eventId), null);

  return (
    <form action={(fd) => formAction(withUtcTimes(fd, ["scheduledAt"]))} className="space-y-4">
      <div>
        <label className={labelClass} htmlFor="a-title">Title</label>
        <input id="a-title" name="title" required minLength={1} maxLength={200} className={`mt-1 ${inputClass}`} />
      </div>
      <div>
        <label className={labelClass} htmlFor="a-body">Message</label>
        <textarea id="a-body" name="body" required rows={4} className={`mt-1 ${textareaClass}`} />
      </div>
      <div className="grid gap-4 sm:grid-cols-2">
        <div>
          <label className={labelClass} htmlFor="a-audience">Audience</label>
          <select id="a-audience" name="audience" defaultValue="AllTicketHolders" className={`mt-1 ${inputClass}`}>
            <option value="AllTicketHolders">All ticket holders</option>
            <option value="CheckedIn">Checked in</option>
            <option value="NotCheckedIn">Not checked in</option>
          </select>
        </div>
        <div>
          <label className={labelClass} htmlFor="a-scheduledAt">Schedule (optional)</label>
          <input id="a-scheduledAt" name="scheduledAt" type="datetime-local" className={`mt-1 ${inputClass}`} />
        </div>
      </div>
      <fieldset>
        <legend className={labelClass}>Channels</legend>
        <div className="mt-2 flex flex-wrap gap-4">
          {[["push", "Push"], ["email", "Email"], ["whatsapp", "WhatsApp"]].map(([v, label]) => (
            <label key={v} className="flex items-center gap-2 text-sm text-text">
              <input type="checkbox" name="channels" value={v} defaultChecked={v === "push"} className="h-4 w-4" /> {label}
            </label>
          ))}
        </div>
      </fieldset>
      <label className="flex items-center gap-2 text-sm text-text">
        <input type="checkbox" name="includeChildEvents" className="h-4 w-4" /> Include sub-events
      </label>
      <div className="flex items-center gap-3">
        <SubmitButton />
        {state && "ok" in state ? <span className="text-sm text-success">Queued.</span> : null}
        {state && "error" in state ? <span className="text-sm text-danger">{String(state.error)}</span> : null}
      </div>
      <p className="text-xs text-muted">Delivery providers (email/WhatsApp/push) are dev stubs today — recipients are counted, but real messages don&apos;t send until providers are wired.</p>
    </form>
  );
}
