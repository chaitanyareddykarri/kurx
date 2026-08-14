"use client";

import { useFormState, useFormStatus } from "react-dom";
import { createTicketTypeAction } from "@/lib/ticket-actions";
import { withUtcTimes } from "@/lib/event-wizard";
import { Button, controlClass } from "@kurx/ui";

const inputClass = controlClass;
const labelClass = "text-sm font-medium text-text";

function SubmitButton() {
  const { pending } = useFormStatus();
  return <Button type="submit" disabled={pending}>{pending ? "Adding…" : "Add ticket type"}</Button>;
}

export function CreateTicketTypeForm({ orgId, eventId }: { orgId: string; eventId: string }) {
  const [state, formAction] = useFormState(createTicketTypeAction.bind(null, orgId, eventId), null);

  return (
    <form action={(fd) => formAction(withUtcTimes(fd, ["saleStarts", "saleEnds"]))} className="space-y-4">
      <div>
        <label className={labelClass} htmlFor="tt-name">Name</label>
        <input id="tt-name" name="name" required minLength={1} maxLength={150} className={`mt-1 ${inputClass}`} />
      </div>
      <div className="grid gap-4 sm:grid-cols-3">
        <div>
          <label className={labelClass} htmlFor="tt-price">Price (₹)</label>
          <input id="tt-price" name="price" type="number" min={0} step="1" defaultValue={0} className={`mt-1 ${inputClass}`} />
        </div>
        <div>
          <label className={labelClass} htmlFor="tt-quantity">Quantity</label>
          <input id="tt-quantity" name="quantity" type="number" min={1} required className={`mt-1 ${inputClass}`} />
        </div>
        <div>
          <label className={labelClass} htmlFor="tt-perUserLimit">Per-user limit</label>
          <input id="tt-perUserLimit" name="perUserLimit" type="number" min={1} defaultValue={5} className={`mt-1 ${inputClass}`} />
        </div>
      </div>
      <div className="grid gap-4 sm:grid-cols-2">
        <div>
          <label className={labelClass} htmlFor="tt-saleStarts">Sale starts</label>
          <input id="tt-saleStarts" name="saleStarts" type="datetime-local" required className={`mt-1 ${inputClass}`} />
        </div>
        <div>
          <label className={labelClass} htmlFor="tt-saleEnds">Sale ends</label>
          <input id="tt-saleEnds" name="saleEnds" type="datetime-local" required className={`mt-1 ${inputClass}`} />
        </div>
      </div>
      <div className="grid gap-4 sm:grid-cols-2">
        <div>
          <label className={labelClass} htmlFor="tt-pricingUnit">Pricing unit</label>
          <select id="tt-pricingUnit" name="pricingUnit" defaultValue="PerTicket" className={`mt-1 ${inputClass}`}>
            <option value="PerTicket">Per ticket</option>
            <option value="PerGroup">Per group</option>
          </select>
        </div>
        <div>
          <label className={labelClass} htmlFor="tt-registrationMode">Registration mode</label>
          <select id="tt-registrationMode" name="registrationMode" defaultValue="Individual" className={`mt-1 ${inputClass}`}>
            <option value="Individual">Individual</option>
            <option value="Group">Group</option>
          </select>
        </div>
      </div>
      <div className="grid gap-4 sm:grid-cols-2">
        <div>
          <label className={labelClass} htmlFor="tt-groupMin">Group min (Group mode)</label>
          <input id="tt-groupMin" name="groupMin" type="number" min={1} className={`mt-1 ${inputClass}`} />
        </div>
        <div>
          <label className={labelClass} htmlFor="tt-groupMax">Group max (Group mode)</label>
          <input id="tt-groupMax" name="groupMax" type="number" min={1} className={`mt-1 ${inputClass}`} />
        </div>
      </div>
      <div className="flex flex-wrap gap-6">
        <label className="flex items-center gap-2 text-sm text-text">
          <input type="checkbox" name="isCompetition" className="h-4 w-4" /> Competition (account required)
        </label>
        <label className="flex items-center gap-2 text-sm text-text">
          <input type="checkbox" name="isAllAccess" className="h-4 w-4" /> All-access pass
        </label>
      </div>
      {state && "error" in state ? <p className="text-sm text-danger">{String(state.error)}</p> : null}
      <SubmitButton />
    </form>
  );
}
