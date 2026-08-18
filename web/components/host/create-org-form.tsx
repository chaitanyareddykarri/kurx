"use client";

import { useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { fileRepresentationRequestAction, presignRepresentationDocAction } from "@/lib/org-actions";
// The vocabulary from the leaf module and the type as a type-only import: both erase to nothing at
// runtime, so this client component never pulls `lib/api`'s axios client and React `cache` calls in.
import { organizationTypes } from "@/lib/org-types";
import { safeReturnTo } from "@/lib/safe-return-to";
import type { Representation } from "@/lib/api";
import { Button, Field, Input, Select, Spinner } from "@kurx/ui";

/**
 * Register a not-yet-listed institution (event-first, D-074/D-075): name + type + required proof.
 * Submits a representation request — a hidden PendingReview org an admin approves before it joins the
 * registry; the caller becomes a *pending* Representative (never Owner) and can draft (not publish) an
 * event meanwhile.
 *
 * **The proof is uploaded from the BROWSER, in three steps.** This form used to be a plain server
 * action that presigned, PUT the bytes and staged the org all server-side — and the PUT could never
 * work: the presigned URL is browser-facing (`http://localhost:5080/...`), while a server action runs
 * inside the Next.js container where that host is ECONNREFUSED. Node reports it as a bare
 * `TypeError: fetch failed`, which is exactly what uploading a letterhead did. `AuthorizationForm` and
 * the create-event wizard have always done presign → browser PUT → submit for the authorization letter;
 * this now matches them.
 *
 * **Rebuilt onto `Field` in Phase 21** — `Field` wires `aria-describedby`, `aria-invalid` and
 * `aria-required` onto its child. Both explanatory paragraphs used to be `<p>` siblings no control
 * referenced, so a screen-reader user focused on either field was told nothing about what to put in it.
 */
export function CreateOrgForm({ returnTo, onRegistered }: {
  /// D-382 — where to land after the request is filed, when the caller arrived from an event. Without it
  /// they were dropped on the account-level representation list and had to rediscover the event they
  /// were in the middle of. Re-checked with `safeReturnTo` before navigating: it reaches this component
  /// from a URL query parameter, so it is untrusted at the point of use, not just at the point of read.
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
  const router = useRouter();
  const formRef = useRef<HTMLFormElement>(null);
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<string | undefined>();

  async function onSubmit(e: React.FormEvent<HTMLFormElement>) {
    e.preventDefault();
    const fd = new FormData(e.currentTarget);
    const name = String(fd.get("name") ?? "").trim();
    const file = fd.get("letterhead");

    if (!name) return setError("Organization name is required.");
    if (!(file instanceof File) || file.size === 0) {
      return setError("Proof of affiliation is required to register an organization.");
    }

    setPending(true);
    setError(undefined);
    try {
      const presigned = await presignRepresentationDocAction(
        file.type || "application/octet-stream", file.size);
      if ("error" in presigned) throw new Error(presigned.error);

      // The PUT that has to happen here rather than on the server — see the note above the component.
      const put = await fetch(presigned.url, {
        method: "PUT", headers: presigned.headers, body: file
      });
      if (!put.ok) throw new Error("The proof could not be uploaded. Try again.");

      const filed = await fileRepresentationRequestAction({
        name,
        type: String(fd.get("type") ?? "") || undefined,
        primaryDomain: String(fd.get("primaryDomain") ?? "") || undefined,
        storageKey: presigned.key
      });
      if ("error" in filed) throw new Error(filed.error);

      if (inline) {
        formRef.current?.reset();
        onRegistered?.(filed as Representation);
      } else {
        router.push(safeReturnTo(returnTo) ?? "/host/representing");
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : "The request could not be submitted.");
    } finally {
      setPending(false);
    }
  }

  return (
    <form ref={formRef} onSubmit={onSubmit} className="space-y-4">
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
      <Button type="submit" disabled={pending}>
        {pending ? <Spinner size={16} decorative /> : null}
        {pending ? "Submitting…" : inline ? "Save organization" : "Register for verification"}
      </Button>
    </form>
  );
}
