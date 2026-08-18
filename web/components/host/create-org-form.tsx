"use client";

import { useEffect, useRef } from "react";
import { useFormState, useFormStatus } from "react-dom";
import { registerRepresentationInlineAction, submitRepresentationRequestAction } from "@/lib/org-actions";
// The vocabulary from the leaf module and the type as a type-only import: both erase to nothing at
// runtime, so this client component never pulls `lib/api`'s axios client and React `cache` calls in.
import { organizationTypes } from "@/lib/org-types";
import type { Representation } from "@/lib/api";
import { Button, Field, Input, Select, Spinner } from "@kurx/ui";

function SubmitButton({ label }: { label: string }) {
  const { pending } = useFormStatus();
  return (
    <Button type="submit" disabled={pending}>
      {pending ? <Spinner size={16} decorative /> : null}
      {pending ? "Submitting…" : label}
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
export function CreateOrgForm({ returnTo, onRegistered }: {
  /// D-382 — where to land after the request is filed, when the caller arrived from an event. Without it
  /// they were dropped on the account-level representation list and had to rediscover the event they
  /// were in the middle of. Validated server-side in `submitRepresentationRequestAction`, never trusted
  /// as a redirect target just because it reached the form.
  returnTo?: string;
  /// Inline mode: hand the staged organization back instead of navigating anywhere.
  ///
  /// Create Event's Representing step mounts this form *in place*, so the redirect the standalone page
  /// needs is the one thing the wizard cannot survive — leaving it discards every unsaved answer. One
  /// component either way, because two copies of "what registering an institution asks for" is exactly
  /// how the two drift.
  onRegistered?: (rep: Representation) => void;
} = {}) {
  const inline = onRegistered !== undefined;
  const [state, formAction] = useFormState(
    inline
      ? (async (_: unknown, fd: FormData) => registerRepresentationInlineAction(fd))
      : submitRepresentationRequestAction,
    null);
  const error = state && "error" in state ? String(state.error) : undefined;

  // Fires once per staged organization: `state` is the action's return value, so re-renders that do not
  // re-run the action see the same object and must not re-announce it.
  //
  // The shape is CHECKED, not assumed. "Not an error" is not the same as "a representation": anything
  // else coming back — a bare `{ ok: true }`, a future field rename — would otherwise be handed to the
  // caller as an organization and select an `undefined` id, leaving the step looking answered when
  // nothing was registered. An id is the one thing that makes this a representation.
  const announced = useRef<unknown>(null);
  useEffect(() => {
    if (!state || "error" in state || announced.current === state) return;
    if (typeof (state as Representation).organization_id !== "string") return;
    announced.current = state;
    onRegistered?.(state as Representation);
  }, [state, onRegistered]);

  return (
    <form action={formAction} className="space-y-4">
      {returnTo ? <input type="hidden" name="returnTo" value={returnTo} /> : null}
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
        {/* "Organization email domain", not "Official email domain": this form now renders on the same
            step as the authorization, which asks for the signatory's "Official email". Two fields whose
            labels share a prefix is an ambiguity for anyone navigating by label — and for the tests
            that do the same thing. */}
        <Field label="Organization email domain" helper="Optional.">
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
      {/* Inline, the button is one of several on the step, so it says what it does to THIS step rather
          than naming the whole workflow. */}
      <SubmitButton label={inline ? "Save organization" : "Register for verification"} />
    </form>
  );
}
