"use client";

import { useFormState, useFormStatus } from "react-dom";
import { submitRepresentationRequestAction } from "@/lib/org-actions";
import { organizationTypes } from "@/lib/api";
import { Button, Field, Input, Select, Spinner } from "@kurx/ui";

function SubmitButton() {
  const { pending } = useFormStatus();
  return (
    <Button type="submit" disabled={pending}>
      {pending ? <Spinner size={16} decorative /> : null}
      {pending ? "Submitting…" : "Register for verification"}
    </Button>
  );
}

/**
 * Register a not-yet-listed institution (event-first, D-074/D-075): name + type + required proof.
 * Submits a representation request — a hidden PendingReview org an admin approves before it joins the
 * registry; the caller becomes a *pending* Representative (never Owner) and can draft (not publish) an
 * event meanwhile.
 *
 * **Rebuilt onto `Field` in Phase 21.** Phase 7 closed audit S1-1 by making `Field` wire
 * `aria-describedby`, `aria-invalid` and `aria-required` onto its child, and reached 86 call sites —
 * none of them on the host surface, which hand-rolled 160 controls across 21 files and used `Field`
 * exactly zero times.
 *
 * The cost here was concrete. Both explanatory paragraphs — the one telling somebody what counts as
 * an organization, and the one telling them what document to upload — were `<p>` siblings that no
 * control referenced, so a screen-reader user focused on either field was told nothing about what to
 * put in it. The submission error was a bare red `<p>`: colour only, and never announced.
 */
export function CreateOrgForm() {
  const [state, formAction] = useFormState(submitRepresentationRequestAction, null);
  const error = state && "error" in state ? String(state.error) : undefined;

  return (
    <form action={formAction} className="space-y-4">
      <Field
        label="Organization name"
        required
        helper="The college, company, club, or community you represent. An admin verifies it before it joins the registry."
      >
        <Input id="org-name" name="name" required placeholder="e.g. NSRIT College" />
      </Field>

      <div className="grid gap-4 sm:grid-cols-2">
        <Field label="Type">
          <Select id="org-type" name="type" defaultValue="college" className="capitalize">
            {organizationTypes.map((t) => <option key={t} value={t}>{t}</option>)}
          </Select>
        </Field>
        <Field label="Official email domain" helper="Optional.">
          <Input id="org-domain" name="primaryDomain" placeholder="nsrit.edu.in" />
        </Field>
      </div>

      <Field
        label="Proof of affiliation"
        required
        helper="A letterhead, official document or authorization proof showing you represent this organization. An admin reviews it; the organization joins the registry and you become a Verified Representative on approval."
      >
        {/* A file input keeps its own control chrome — the design system has no file variant, and
            styling one to look like a text field would misrepresent what it does. */}
        <input
          id="org-letterhead"
          name="letterhead"
          type="file"
          required
          accept="application/pdf,image/*"
          className="block w-full text-sm text-muted file:mr-3 file:min-h-11 file:rounded-md file:border file:border-border-strong file:bg-surface file:px-3 file:text-sm file:text-text"
        />
      </Field>

      {/* Was a bare red paragraph: colour-only, and silent when it appeared. */}
      {error ? (
        <p role="alert" className="text-sm text-danger">{error}</p>
      ) : null}
      <SubmitButton />
    </form>
  );
}
