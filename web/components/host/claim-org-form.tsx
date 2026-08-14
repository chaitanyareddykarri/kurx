"use client";

import { useFormState, useFormStatus } from "react-dom";
import { submitMembershipClaimAction } from "@/lib/membership-actions";
import { Button, Spinner } from "@kurx/ui";

const ROLES = ["Employee", "Student", "Faculty", "Coordinator", "EventLead", "Founder", "Director", "Volunteer", "Other"];

function SubmitButton() {
  const { pending } = useFormStatus();
  return <Button type="submit" variant="secondary" disabled={pending} className="shrink-0">{pending ? <Spinner size={16} label="Requesting access" /> : "Request access"}</Button>;
}

export function ClaimOrgForm({ orgId }: { orgId: string }) {
  const [state, formAction] = useFormState(submitMembershipClaimAction.bind(null, orgId), null);

  if (state && "ok" in state) {
    return <p className="mt-3 text-xs text-success">Claim submitted — track it below.</p>;
  }

  return (
    <div className="mt-3">
      <form action={formAction} className="space-y-2">
        <div className="flex items-center gap-2">
          <select
            name="claimedRole"
            defaultValue="Employee"
            aria-label="Your role at this organization"
            className="h-8 rounded-md border border-border-strong bg-background px-2 text-xs text-text"
          >
            {ROLES.map((r) => <option key={r} value={r}>{r}</option>)}
          </select>
          <SubmitButton />
        </div>
        <input
          type="file"
          name="proof"
          required
          accept="application/pdf,image/*"
          aria-label="Proof of affiliation"
          className="block w-full text-xs text-muted file:mr-2 file:rounded file:border file:border-border-strong file:bg-surface file:px-2 file:py-1 file:text-xs file:text-text"
        />
        <p className="text-[11px] text-muted">Attach an ID card, offer letter, or similar. An admin verifies it before approving.</p>
      </form>
      {state && "error" in state ? <p className="mt-2 text-xs text-danger">{String(state.error)}</p> : null}
    </div>
  );
}
