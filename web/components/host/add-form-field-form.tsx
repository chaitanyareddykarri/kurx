"use client";

import { useFormState, useFormStatus } from "react-dom";
import { addFieldAction } from "@/lib/ticket-actions";
import { Button, controlClass } from "@kurx/ui";

const inputClass = controlClass;
const labelClass = "text-sm font-medium text-text";

function SubmitButton() {
  const { pending } = useFormStatus();
  return <Button type="submit" variant="secondary" disabled={pending}>{pending ? "Adding…" : "Add field"}</Button>;
}

export function AddFormFieldForm({ orgId, eventId, ticketTypeId }: { orgId: string; eventId: string; ticketTypeId: string }) {
  const [state, formAction] = useFormState(addFieldAction.bind(null, orgId, eventId, ticketTypeId), null);

  return (
    <form action={formAction} className="space-y-4">
      <div className="grid gap-4 sm:grid-cols-2">
        <div>
          <label className={labelClass} htmlFor="ff-key">Key (snake_case)</label>
          <input id="ff-key" name="key" required minLength={1} maxLength={100} placeholder="tshirt_size" className={`mt-1 ${inputClass}`} />
        </div>
        <div>
          <label className={labelClass} htmlFor="ff-label">Label</label>
          <input id="ff-label" name="label" required minLength={1} maxLength={200} placeholder="T-shirt size" className={`mt-1 ${inputClass}`} />
        </div>
      </div>
      <div className="grid gap-4 sm:grid-cols-3">
        <div>
          <label className={labelClass} htmlFor="ff-type">Type</label>
          <select id="ff-type" name="type" defaultValue="Text" className={`mt-1 ${inputClass}`}>
            {["Text", "Number", "Select", "Checkbox", "Date", "File"].map((t) => <option key={t} value={t}>{t}</option>)}
          </select>
        </div>
        <div>
          <label className={labelClass} htmlFor="ff-scope">Scope</label>
          <select id="ff-scope" name="scope" defaultValue="PerRegistration" className={`mt-1 ${inputClass}`}>
            <option value="PerRegistration">Per registration</option>
            <option value="PerParticipant">Per participant</option>
          </select>
        </div>
        <div>
          <label className={labelClass} htmlFor="ff-sort">Sort</label>
          <input id="ff-sort" name="sort" type="number" min={0} className={`mt-1 ${inputClass}`} />
        </div>
      </div>
      <div>
        <label className={labelClass} htmlFor="ff-options">Options JSON (for Select fields)</label>
        <input id="ff-options" name="optionsJson" placeholder='["S","M","L"]' className={`mt-1 ${inputClass}`} />
      </div>
      <label className="flex items-center gap-2 text-sm text-text">
        <input type="checkbox" name="required" className="h-4 w-4" /> Required
      </label>
      {state && "error" in state ? <p className="text-sm text-danger">{String(state.error)}</p> : null}
      <SubmitButton />
    </form>
  );
}
