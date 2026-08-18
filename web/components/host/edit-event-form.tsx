"use client";

import { useState } from "react";
import { useFormState, useFormStatus } from "react-dom";
import { updateEventAction, requestEventChangesAction } from "@/lib/event-actions";
import { toLocalInput, withUtcTimes } from "@/lib/event-wizard";
import { Alert, Button, Spinner, controlClass } from "@kurx/ui";
import type { Category, EventDetail } from "@/lib/api";

const inputClass = controlClass;
const textareaClass = controlClass;

/// D-388 — the same form serves two different acts, and the button has to say which one.
type FormMode = "direct" | "proposal";

function SaveButton({ mode }: { mode: FormMode }) {
  const { pending } = useFormStatus();
  const idle = mode === "proposal" ? "Submit changes for approval" : "Save changes";
  const busy = mode === "proposal" ? "Submitting…" : "Saving…";
  return (
    <Button type="submit" disabled={pending}>
      {pending ? <Spinner size={16} decorative /> : null}
      {pending ? busy : idle}
    </Button>
  );
}

/**
 * D-388 — `mode` decides which server action the form posts to, and it is a PROP rather than something
 * this component derives from `event.status`.
 *
 * The page already has to decide whether to render a read-only view instead of this form at all, and that
 * decision is the same rule. Deriving it twice — once to choose the view, once to choose the action — is
 * how a page ends up showing "Save changes" on a form whose save the server will refuse.
 */
export function EditEventForm({
  orgId,
  event,
  categories,
  subcategories = [],
  mode = "direct"
}: {
  orgId: string;
  event: EventDetail;
  categories: Category[];
  subcategories?: Category[];
  mode?: FormMode;
}) {
  const action = mode === "proposal" ? requestEventChangesAction : updateEventAction;
  const [state, formAction] = useFormState(action.bind(null, orgId, event.id), null);
  const [categoryId, setCategoryId] = useState(event.category_id);
  const subs = subcategories.filter((s) => s.parent_id === categoryId);

  return (
    <form action={(formData) => formAction(withUtcTimes(formData))} className="space-y-4">
      {/* D-388 — stated before the first field, not next to the button. A host who reads it only on the
          way out has already spent ten minutes believing they were editing the live event. */}
      {mode === "proposal" ? (
        <Alert tone="info" title="These changes go to a reviewer">
          Your event stays exactly as it is now. Nothing here reaches the public listing, your attendees
          or your ticket page until an admin approves it.
        </Alert>
      ) : null}
      {/* D-363 §4 — this form posts the whole record on every save (title, dates, venue, capacity all
          go in the payload), so ANY save from here reads as a material edit and returns an approved
          event to the queue. Finding that out afterwards, from a status badge, is the wrong way. */}
      {mode === "direct" && event.status === "approved" ? (
        <Alert tone="warning" title="This event is approved">
          Saving a change here returns it to review — a reviewer approved what it says now. Publish it
          first if you are ready to go live.
        </Alert>
      ) : null}
      <div>
        <label className="text-sm font-medium text-text" htmlFor="title">Title</label>
        <input id="title" name="title" defaultValue={event.title} minLength={2} maxLength={200} className={`mt-1 ${inputClass}`} />
      </div>
      <div>
        <label className="text-sm font-medium text-text" htmlFor="subtitle">Subtitle</label>
        <input id="subtitle" name="subtitle" defaultValue={event.subtitle} maxLength={200} className={`mt-1 ${inputClass}`} />
      </div>
      <div>
        <label className="text-sm font-medium text-text" htmlFor="description">Description</label>
        <textarea id="description" name="description" rows={4} defaultValue={event.description} className={`mt-1 ${textareaClass}`} />
      </div>
      <div className="grid gap-4 sm:grid-cols-2">
        <div>
          <label className="text-sm font-medium text-text" htmlFor="categoryId">Category</label>
          <select id="categoryId" name="categoryId" value={categoryId} onChange={(e) => setCategoryId(e.target.value)} className={`mt-1 ${inputClass}`}>
            {categories.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
          </select>
        </div>
        <div>
          <label className="text-sm font-medium text-text" htmlFor="typeId">Type</label>
          <select id="typeId" name="typeId" key={categoryId} defaultValue={event.type_id ?? ""} className={`mt-1 ${inputClass}`}>
            <option value="">— none —</option>
            {subs.map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}
          </select>
        </div>
      </div>
      <div className="grid gap-4 sm:grid-cols-2">
        <div>
          <label className="text-sm font-medium text-text" htmlFor="startsAt">Starts at</label>
          <input id="startsAt" name="startsAt" type="datetime-local" defaultValue={toLocalInput(event.starts_at)} className={`mt-1 ${inputClass}`} />
        </div>
        <div>
          <label className="text-sm font-medium text-text" htmlFor="endsAt">Ends at</label>
          <input id="endsAt" name="endsAt" type="datetime-local" defaultValue={toLocalInput(event.ends_at)} className={`mt-1 ${inputClass}`} />
        </div>
      </div>
      <div className="grid gap-4 sm:grid-cols-2">
        <div>
          <label className="text-sm font-medium text-text" htmlFor="venueName">Venue name</label>
          <input id="venueName" name="venueName" defaultValue={event.venue.name} className={`mt-1 ${inputClass}`} />
        </div>
        <div>
          <label className="text-sm font-medium text-text" htmlFor="city">City</label>
          <input id="city" name="city" defaultValue={event.venue.city} className={`mt-1 ${inputClass}`} />
        </div>
      </div>
      <div>
        <label className="text-sm font-medium text-text" htmlFor="venueAddress">Venue address</label>
        <input id="venueAddress" name="venueAddress" defaultValue={event.venue.address} className={`mt-1 ${inputClass}`} />
      </div>
      <div className="grid gap-4 sm:grid-cols-2">
        <div>
          <label className="text-sm font-medium text-text" htmlFor="capacity">Capacity</label>
          <input id="capacity" name="capacity" type="number" min={1} defaultValue={event.capacity ?? ""} className={`mt-1 ${inputClass}`} />
        </div>
        <div>
          <label className="text-sm font-medium text-text" htmlFor="visibility">Visibility</label>
          <select id="visibility" name="visibility" defaultValue={event.visibility} className={`mt-1 ${inputClass}`}>
            {/* D-266: the three values EventVisibility actually has. `Private` was offered here until
                now and had been dead since migration RetireLegacyPrivateVisibility — the server's
                Enum.TryParse rejects it, so choosing it silently changed nothing. `InviteOnly` was
                missing entirely, which meant an existing event could never be switched to invite-only
                through the UI and M6's invitation features were unreachable for it. */}
            <option value="Listed">Listed</option>
            <option value="Unlisted">Unlisted</option>
            <option value="InviteOnly">Invite only</option>
          </select>
        </div>
      </div>
      <div className="grid gap-4 sm:grid-cols-2">
        <div>
          <label className="text-sm font-medium text-text" htmlFor="contactEmail">Contact email</label>
          <input id="contactEmail" name="contactEmail" type="email" defaultValue={event.contact_email} className={`mt-1 ${inputClass}`} />
        </div>
        <div>
          <label className="text-sm font-medium text-text" htmlFor="contactPhone">Contact phone</label>
          <input id="contactPhone" name="contactPhone" defaultValue={event.contact_phone} className={`mt-1 ${inputClass}`} />
        </div>
      </div>
      <div>
        <label className="text-sm font-medium text-text" htmlFor="website">Website</label>
        <input id="website" name="website" defaultValue={event.website} className={`mt-1 ${inputClass}`} />
      </div>
      {/*
        Both outcomes were invisible. The action returns `{ ok: true }` on success and this form
        never rendered it, so saving fourteen fields confirmed nothing at all — and the error branch
        was dead, because the action threw rather than returning (fixed in `event-actions.ts`).

        Both live above the button rather than below it: after a form this long the submit control is
        where the eye already is, and a message under it is off-screen on a phone.
      */}
      {mode === "proposal" ? (
        <div>
          <label className="text-sm font-medium text-text" htmlFor="reason">
            Why are you making these changes? <span className="text-muted">(optional)</span>
          </label>
          <textarea id="reason" name="reason" rows={2} maxLength={1000} className={`mt-1 ${textareaClass}`}
            placeholder="e.g. The venue moved us to the main auditorium." />
          <p className="mt-1 text-xs text-muted">A reviewer reads this next to the changes.</p>
        </div>
      ) : null}
      {state && "error" in state ? (
        <p role="alert" className="rounded-md border border-danger/40 bg-danger/10 px-3 py-2 text-sm text-danger">
          {String(state.error)}
        </p>
      ) : null}
      {state && "ok" in state ? (
        <p role="status" className="rounded-md border border-success/40 bg-success/10 px-3 py-2 text-sm text-success">
          {mode === "proposal" ? "Submitted for approval. Your event is unchanged until a reviewer approves." : "Changes saved."}
        </p>
      ) : null}
      <SaveButton mode={mode} />
    </form>
  );
}
