"use client";

import { useEffect } from "react";
import { useFormState, useFormStatus } from "react-dom";
import { AlertTriangle } from "lucide-react";
import { Button, Card, Field, Input, Select, useToast } from "@kurx/ui";
import { recordSignalAction } from "@/lib/admin-actions";

// Subjects a fraud signal can attach to (backend polymorphic subject_type).
const SUBJECT_TYPES = ["UserIdentity", "Organization", "Membership", "Event"];

function Submit() {
  const { pending } = useFormStatus();
  return (
    <Button type="submit" disabled={pending}>
      {pending ? "Recording…" : "Record signal"}
    </Button>
  );
}

/** Record a manual fraud/risk signal (M13). Signals accumulate into a subject's risk score; at ≥100 the
 *  subject loses paid-organizing capability (read live). There is no list endpoint yet, so this is
 *  record-only — the running score is echoed back on submit. */
export function FraudSignalForm() {
  const [state, action] = useFormState(recordSignalAction, null);
  const toast = useToast();

  useEffect(() => {
    if (state && "ok" in state) {
      toast(`Signal recorded. Risk score is now ${(state as { riskScore?: number }).riskScore ?? "—"}.`, "success");
    } else if (state && "error" in state) {
      toast(String(state.error), "error");
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [state]);

  return (
    <Card>
      <div className="flex items-start gap-3">
        <AlertTriangle size={18} className="mt-0.5 shrink-0 text-accent-text" />
        <div>
          <h2 className="font-semibold text-text">Record a risk signal</h2>
          <p className="mt-1 text-sm text-muted">
            Signals accumulate into the subject&apos;s risk score. At 100+ the subject loses paid-organizing
            capability, checked live on every request.
          </p>
        </div>
      </div>

      <form action={action} className="mt-5 space-y-4">
        <div className="grid gap-4 sm:grid-cols-2">
          <Field label="Subject type" htmlFor="fs-subject-type">
            <Select id="fs-subject-type" name="subjectType" defaultValue="UserIdentity">
              {SUBJECT_TYPES.map((t) => <option key={t} value={t}>{t}</option>)}
            </Select>
          </Field>
          <Field label="Subject ID" htmlFor="fs-subject-id" helper="The user / org / event UUID">
            <Input id="fs-subject-id" name="subjectId" required placeholder="00000000-0000-0000-0000-000000000000" className="font-mono" />
          </Field>
        </div>

        <div className="grid gap-4 sm:grid-cols-[1fr_8rem]">
          <Field label="Kind" htmlFor="fs-kind" helper="e.g. manual, velocity, duplicate_account">
            <Input id="fs-kind" name="kind" required placeholder="manual" />
          </Field>
          <Field label="Score" htmlFor="fs-score">
            <Input id="fs-score" name="score" type="number" defaultValue={50} />
          </Field>
        </div>

        <Field label="Value" htmlFor="fs-value" helper="Optional context, e.g. shared device id">
          <Input id="fs-value" name="value" placeholder="Context" />
        </Field>

        <Submit />
      </form>
    </Card>
  );
}
