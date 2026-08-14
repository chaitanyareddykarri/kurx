"use client";

import { useFormState, useFormStatus } from "react-dom";
import { Card } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { submitGovtIdAction, submitPanAction, submitBankAction } from "@/lib/settings-actions";

type Identity = {
  level: string;
  status: string;
  govt_id_kind: string | null;
  govt_id_last4: string | null;
  pan_last4: string | null;
  bank_last4: string | null;
} | null;

// `border-strong` identifies the control (WCAG 1.4.11); `border` is decorative at 1.35:1 (D-288).
const inputCls = "h-10 w-full rounded-md border border-border-strong bg-background px-3 text-sm text-text";
const wideCls = "sm:col-span-2 h-10 rounded-md border border-border-strong bg-background px-3 text-sm text-text";

function Submit({ label }: { label: string }) {
  const { pending } = useFormStatus();
  return <Button type="submit" disabled={pending}>{pending ? "Submitting…" : label}</Button>;
}

function Row({ label, value, ok }: { label: string; value: string; ok: boolean }) {
  return (
    <div className="flex items-center justify-between gap-3 text-sm">
      <span className="text-muted">{label}</span>
      <span className={ok ? "font-medium text-success" : "text-muted"}>{value}</span>
    </div>
  );
}

export function IdentitySection({ identity }: { identity: Identity }) {
  const [govt, govtAction] = useFormState(submitGovtIdAction, null);
  const [pan, panAction] = useFormState(submitPanAction, null);
  const [bank, bankAction] = useFormState(submitBankAction, null);

  return (
    <div className="space-y-6">
      <Card>
        <h2 className="font-semibold">Status</h2>
        <div className="mt-3 space-y-2">
          <Row label="Verification level" value={identity?.level ?? "—"} ok={!!identity && identity.level !== "L0"} />
          <Row label="Status" value={identity?.status ?? "none"} ok={identity?.status === "verified"} />
          <Row label="Government ID" value={identity?.govt_id_last4 ? `${identity.govt_id_kind ?? "id"} ••${identity.govt_id_last4}` : "not submitted"} ok={!!identity?.govt_id_last4} />
          <Row label="PAN" value={identity?.pan_last4 ? `••${identity.pan_last4}` : "not submitted"} ok={!!identity?.pan_last4} />
          <Row label="Bank" value={identity?.bank_last4 ? `••${identity.bank_last4}` : "not submitted"} ok={!!identity?.bank_last4} />
        </div>
        <p className="mt-3 text-xs text-muted">Only masked last-4 values are ever stored or shown. Submissions are attempt-capped.</p>
      </Card>

      <Card>
        <h2 className="font-semibold">Government ID</h2>
        <form action={govtAction} className="mt-3 grid gap-2 sm:grid-cols-2">
          <select name="kind" className={inputCls}>
            <option value="aadhaar">Aadhaar</option>
            <option value="passport">Passport</option>
            <option value="voter">Voter ID</option>
            <option value="dl">Driving licence</option>
          </select>
          <input name="name" placeholder="Name on ID" className={inputCls} />
          <input name="idNumber" placeholder="ID number" className={wideCls} />
          <div className="sm:col-span-2"><Submit label="Submit government ID" /></div>
        </form>
        {govt && "error" in govt && govt.error ? <p className="mt-2 text-sm text-danger">{govt.error}</p> : null}
        {govt && "ok" in govt ? <p className="mt-2 text-sm text-accent">Submitted for verification.</p> : null}
      </Card>

      <Card>
        <h2 className="font-semibold">PAN</h2>
        <form action={panAction} className="mt-3 grid gap-2 sm:grid-cols-2">
          <input name="pan" placeholder="PAN" className={inputCls} />
          <input name="name" placeholder="Name on PAN" className={inputCls} />
          <div className="sm:col-span-2"><Submit label="Submit PAN" /></div>
        </form>
        {pan && "error" in pan && pan.error ? <p className="mt-2 text-sm text-danger">{pan.error}</p> : null}
        {pan && "ok" in pan ? <p className="mt-2 text-sm text-accent">Submitted for verification.</p> : null}
      </Card>

      <Card>
        <h2 className="font-semibold">Bank account</h2>
        <form action={bankAction} className="mt-3 grid gap-2 sm:grid-cols-2">
          <input name="holderName" placeholder="Account holder name" className={inputCls} />
          <input name="ifsc" placeholder="IFSC" className={inputCls} />
          <input name="accountNumber" placeholder="Account number" className={wideCls} />
          <div className="sm:col-span-2"><Submit label="Submit bank account" /></div>
        </form>
        {bank && "error" in bank && bank.error ? <p className="mt-2 text-sm text-danger">{bank.error}</p> : null}
        {bank && "ok" in bank ? <p className="mt-2 text-sm text-accent">Submitted for verification.</p> : null}
      </Card>
    </div>
  );
}
