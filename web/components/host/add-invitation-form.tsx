"use client";

import { useState } from "react";
import { useFormState, useFormStatus } from "react-dom";
import { addInvitationAction } from "@/lib/invitation-actions";
import { Button, controlClass, PhoneField } from "@kurx/ui";

const inputClass = controlClass;
const labelClass = "text-sm font-medium text-text";

function SubmitButton({ disabled }: { disabled?: boolean }) {
  const { pending } = useFormStatus();
  return <Button type="submit" disabled={pending || disabled}>{pending ? "Adding…" : "Add guest"}</Button>;
}

export function AddInvitationForm({ eventId }: { eventId: string }) {
  const [state, formAction] = useFormState(addInvitationAction.bind(null, eventId), null);
  const [phone, setPhone] = useState("");
  const [phoneValid, setPhoneValid] = useState(false);

  return (
    <form action={formAction} className="space-y-4">
      <div className="grid gap-4 sm:grid-cols-3">
        <div>
          <label className={labelClass} htmlFor="i-name">Name</label>
          <input id="i-name" name="name" maxLength={150} className={`mt-1 ${inputClass}`} />
        </div>
        <div>
          <label className={labelClass} htmlFor="i-email">Email</label>
          <input id="i-email" name="email" type="email" className={`mt-1 ${inputClass}`} />
        </div>
        <div>
          {/* Country picker, not a bare text box: this value reaches InvitationService.NormalizePhone,
              which reads digits with no '+' in the legacy region — so an overseas guest entered in their
              own national format was invited, and WhatsApped, at an unrelated Indian number. Phone stays
              optional (a guest can be invited by email alone), so the field only blocks submission once
              something has actually been typed into it. */}
          <PhoneField
            id="i-phone"
            label="Phone"
            value={phone}
            onChange={(e164, valid) => { setPhone(e164); setPhoneValid(valid); }}
          />
          <input type="hidden" name="phone" value={phone} />
        </div>
      </div>
      <div className="sm:w-48">
        <label className={labelClass} htmlFor="i-channel">Channel</label>
        <select id="i-channel" name="channel" defaultValue="Email" className={`mt-1 ${inputClass}`}>
          <option value="Email">Email</option>
          <option value="WhatsApp">WhatsApp</option>
          <option value="Both">Both</option>
        </select>
      </div>
      <div className="flex items-center gap-3">
        <SubmitButton disabled={phone !== "" && !phoneValid} />
        {state && "ok" in state ? <span className="text-sm text-success">Added.</span> : null}
        {state && "error" in state ? <span className="text-sm text-danger">{String(state.error)}</span> : null}
      </div>
      <p className="text-xs text-muted">Add guests, then &ldquo;Send pending&rdquo; to deliver invites (email/WhatsApp delivery is a dev stub until providers are wired).</p>
    </form>
  );
}
