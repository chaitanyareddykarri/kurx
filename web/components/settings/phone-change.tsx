"use client";

import { useState } from "react";
import { useFormState, useFormStatus } from "react-dom";
import { Button } from "@/components/ui/button";
import { PhoneField } from "@kurx/ui";
import { requestPhoneOtpAction, changePhoneAction } from "@/lib/settings-actions";

// `border-strong` identifies the control (WCAG 1.4.11); `border` is decorative at 1.35:1 (D-288).
const inputCls = "h-10 w-full rounded-md border border-border-strong bg-background px-3 text-sm text-text";

function Submit({ label, disabled }: { label: string; disabled?: boolean }) {
  const { pending } = useFormStatus();
  return (
    <Button type="submit" disabled={pending || disabled}>
      {pending ? "…" : label}
    </Button>
  );
}

export function PhoneChange() {
  const [otpState, requestAction] = useFormState(requestPhoneOtpAction, null);
  const [changeState, changeAction] = useFormState(changePhoneAction, null);
  const [phone, setPhone] = useState("");
  const [phoneValid, setPhoneValid] = useState(false);
  const sentTo = otpState && "phone" in otpState ? otpState.phone : "";

  return (
    <div className="space-y-3">
      <form action={requestAction} className="space-y-2">
        {/* The controlled E.164 value rides the form as a hidden field so the server action gets it. */}
        <input type="hidden" name="phone" value={phone} />
        <PhoneField
          label="New phone number"
          value={phone}
          onChange={(e164, valid) => {
            setPhone(e164);
            setPhoneValid(valid);
          }}
        />
        <Submit label="Send code" disabled={!phoneValid} />
      </form>
      {otpState && "error" in otpState && otpState.error ? <p className="text-sm text-danger">{otpState.error}</p> : null}

      {sentTo ? (
        <form action={changeAction} className="flex flex-wrap items-end gap-2">
          <input type="hidden" name="phone" value={sentTo} />
          <div>
            <label className="text-xs text-muted" htmlFor="phone-code">
              Code sent to {sentTo}
            </label>
            <input id="phone-code" name="code" inputMode="numeric" className={`mt-1 ${inputCls} w-32`} />
          </div>
          <Submit label="Change phone" />
        </form>
      ) : null}
      {changeState && "error" in changeState && changeState.error ? (
        <p className="text-sm text-danger">{changeState.error}</p>
      ) : null}
      {changeState && "ok" in changeState ? <p className="text-sm text-accent">Phone updated.</p> : null}
    </div>
  );
}
