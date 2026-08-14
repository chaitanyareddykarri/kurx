"use client";

import { useFormState, useFormStatus } from "react-dom";
import { updateTicketTypeAction } from "@/lib/ticket-actions";
import { toLocalInput, withUtcTimes } from "@/lib/event-wizard";
import { Button, controlClass } from "@kurx/ui";
import type { TicketType } from "@/lib/api";

const inputClass = controlClass;
const labelClass = "text-sm font-medium text-text";

function SubmitButton() {
  const { pending } = useFormStatus();
  return <Button type="submit" disabled={pending}>{pending ? "Saving…" : "Save ticket type"}</Button>;
}

export function EditTicketTypeForm({ orgId, eventId, ticketType }: { orgId: string; eventId: string; ticketType: TicketType }) {
  const t = ticketType;
  const [state, formAction] = useFormState(updateTicketTypeAction.bind(null, orgId, eventId, t.id), null);

  return (
    <form action={(fd) => formAction(withUtcTimes(fd, ["saleStarts", "saleEnds"]))} className="space-y-4">
      <div>
        <label className={labelClass} htmlFor={`e-name-${t.id}`}>Name</label>
        <input id={`e-name-${t.id}`} name="name" defaultValue={t.name} required minLength={1} maxLength={150} className={`mt-1 ${inputClass}`} />
      </div>
      <div className="grid gap-4 sm:grid-cols-3">
        <div>
          <label className={labelClass} htmlFor={`e-price-${t.id}`}>Price (₹)</label>
          <input id={`e-price-${t.id}`} name="price" type="number" min={0} step="1" defaultValue={t.price_paise / 100} className={`mt-1 ${inputClass}`} />
        </div>
        <div>
          <label className={labelClass} htmlFor={`e-quantity-${t.id}`}>Quantity</label>
          <input id={`e-quantity-${t.id}`} name="quantity" type="number" min={t.sold || 1} defaultValue={t.quantity} required className={`mt-1 ${inputClass}`} />
        </div>
        <div>
          <label className={labelClass} htmlFor={`e-perUserLimit-${t.id}`}>Per-user limit</label>
          <input id={`e-perUserLimit-${t.id}`} name="perUserLimit" type="number" min={1} defaultValue={t.per_user_limit} className={`mt-1 ${inputClass}`} />
        </div>
      </div>
      <div className="grid gap-4 sm:grid-cols-2">
        <div>
          <label className={labelClass} htmlFor={`e-saleStarts-${t.id}`}>Sale starts</label>
          <input id={`e-saleStarts-${t.id}`} name="saleStarts" type="datetime-local" defaultValue={toLocalInput(t.sale_starts)} required className={`mt-1 ${inputClass}`} />
        </div>
        <div>
          <label className={labelClass} htmlFor={`e-saleEnds-${t.id}`}>Sale ends</label>
          <input id={`e-saleEnds-${t.id}`} name="saleEnds" type="datetime-local" defaultValue={toLocalInput(t.sale_ends)} required className={`mt-1 ${inputClass}`} />
        </div>
      </div>
      <div className="grid gap-4 sm:grid-cols-2">
        <div>
          <label className={labelClass} htmlFor={`e-pricingUnit-${t.id}`}>Pricing unit</label>
          <select id={`e-pricingUnit-${t.id}`} name="pricingUnit" defaultValue={t.pricing_unit} className={`mt-1 ${inputClass}`}>
            <option value="PerTicket">Per ticket</option>
            <option value="PerGroup">Per group</option>
          </select>
        </div>
        <div>
          <label className={labelClass} htmlFor={`e-registrationMode-${t.id}`}>Registration mode</label>
          <select id={`e-registrationMode-${t.id}`} name="registrationMode" defaultValue={t.registration_mode} className={`mt-1 ${inputClass}`}>
            <option value="Individual">Individual</option>
            <option value="Group">Group</option>
          </select>
        </div>
      </div>
      <div className="grid gap-4 sm:grid-cols-2">
        <div>
          <label className={labelClass} htmlFor={`e-groupMin-${t.id}`}>Group min</label>
          <input id={`e-groupMin-${t.id}`} name="groupMin" type="number" min={1} defaultValue={t.group_min ?? ""} className={`mt-1 ${inputClass}`} />
        </div>
        <div>
          <label className={labelClass} htmlFor={`e-groupMax-${t.id}`}>Group max</label>
          <input id={`e-groupMax-${t.id}`} name="groupMax" type="number" min={1} defaultValue={t.group_max ?? ""} className={`mt-1 ${inputClass}`} />
        </div>
      </div>
      <div className="flex flex-wrap gap-6">
        <label className="flex items-center gap-2 text-sm text-text">
          <input type="checkbox" name="isCompetition" defaultChecked={t.is_competition} className="h-4 w-4" /> Competition
        </label>
        <label className="flex items-center gap-2 text-sm text-text">
          <input type="checkbox" name="isAllAccess" defaultChecked={t.is_all_access} className="h-4 w-4" /> All-access pass
        </label>
      </div>
      <div className="flex items-center gap-3">
        <SubmitButton />
        {state && "ok" in state ? <span className="text-sm text-success">Saved.</span> : null}
        {state && "error" in state ? <span className="text-sm text-danger">{String(state.error)}</span> : null}
      </div>
    </form>
  );
}
