"use client";

import { useEffect, useState } from "react";
import { useFormState, useFormStatus } from "react-dom";
import { Ban, Trash2 } from "lucide-react";
import { Badge, Button, Card, DataTable, EmptyState, Field, Input, PhoneField, Select, useToast, type Column } from "@kurx/ui";
import { addBlacklistAction, removeBlacklistAction } from "@/lib/admin-actions";
import type { BlacklistEntry } from "@/lib/api";

// Only Phone/Email/OrgName are typed by hand; Device/DocHash are opaque ids kept for completeness.
const KINDS = ["Phone", "Email", "OrgName", "Device", "DocHash"];

function BlockSubmit({ disabled }: { disabled?: boolean }) {
  const { pending } = useFormStatus();
  return (
    <Button type="submit" disabled={pending || disabled} className="sm:mb-0">
      <Ban size={15} /> {pending ? "Blocking…" : "Block"}
    </Button>
  );
}

function AddForm() {
  const [state, action] = useFormState(addBlacklistAction, null);
  const toast = useToast();
  const [kind, setKind] = useState("Email");
  const [phone, setPhone] = useState("");
  const [phoneValid, setPhoneValid] = useState(false);

  useEffect(() => {
    if (state && "ok" in state) toast("Added to blocklist.", "success");
    else if (state && "error" in state) toast(String(state.error), "error");
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [state]);

  return (
    <Card>
      <h2 className="font-semibold text-text">Block a phone, email, org name, device or document hash</h2>
      <form action={action} className="mt-3 grid gap-3 sm:grid-cols-[10rem_1fr_1fr_auto] sm:items-end">
        <Field label="Kind" htmlFor="bl-kind">
          <Select id="bl-kind" name="kind" value={kind} onChange={(e) => setKind(e.target.value)}>
            {KINDS.map((k) => <option key={k} value={k}>{k}</option>)}
          </Select>
        </Field>
        {/* A blocked phone is normalized by the same NormalizePhone the login path uses, which reads
            digits with no '+' in the legacy region. Typing a Singapore number in its national form
            therefore blocked an unrelated Indian number and left the intended one free to sign up. */}
        {kind === "Phone" ? (
          <div>
            <PhoneField
              id="bl-value"
              label="Value"
              value={phone}
              onChange={(e164, valid) => { setPhone(e164); setPhoneValid(valid); }}
            />
            <input type="hidden" name="value" value={phone} />
          </div>
        ) : (
          <Field label="Value" htmlFor="bl-value">
            <Input id="bl-value" name="value" required placeholder="e.g. spammer@example.com" />
          </Field>
        )}
        <Field label="Reason" htmlFor="bl-reason" helper="Optional">
          <Input id="bl-reason" name="reason" placeholder="e.g. chargeback fraud" />
        </Field>
        <BlockSubmit disabled={kind === "Phone" && !phoneValid} />
      </form>
    </Card>
  );
}

function RemoveButton({ id, value }: { id: string; value: string }) {
  const [state, action] = useFormState(removeBlacklistAction.bind(null, id), null);
  const toast = useToast();

  useEffect(() => {
    if (state && "ok" in state) toast(`Removed "${value}" from the blocklist.`, "success");
    else if (state && "error" in state) toast(String(state.error), "error");
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [state]);

  return (
    <form action={action} className="flex justify-end">
      <button
        type="submit"
        aria-label={`Remove ${value}`}
        title="Remove"
        className="grid h-8 w-8 place-items-center rounded-md border border-border text-muted hover:bg-danger/10 hover:text-danger"
      >
        <Trash2 size={14} />
      </button>
    </form>
  );
}

export function BlacklistManager({ entries }: { entries: BlacklistEntry[] }) {
  const columns: Column<BlacklistEntry>[] = [
    { key: "kind", header: "Kind", render: (e) => <Badge tone="neutral">{e.kind}</Badge> },
    { key: "value", header: "Value", render: (e) => <span className="font-mono text-xs text-text">{e.value}</span> },
    { key: "reason", header: "Reason", render: (e) => <span className="text-muted">{e.reason ?? "—"}</span> },
    { key: "created_at", header: "Added", render: (e) => <span className="text-xs text-muted">{new Date(e.created_at).toLocaleDateString("en-IN")}</span> },
    { key: "actions", header: "", srHeader: "Row actions", align: "right", width: "4rem", render: (e) => <RemoveButton id={e.id} value={e.value} /> }
  ];

  return (
    <div className="space-y-6">
      <AddForm />
      <DataTable
        columns={columns}
        data={entries}
        keyField={(e) => e.id}
        caption="Blocklist"
        emptyState={<EmptyState icon={<Ban size={22} />} title="The blocklist is empty" message="Blocked subjects lose paid-organizing capability immediately." />}
      />
    </div>
  );
}
