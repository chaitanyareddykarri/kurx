"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";
import { requireSession } from "@/lib/session";
import { presignRepresentationDoc, submitRepresentationRequest, searchOrganizations, updateOrg, apiErrorMessage } from "@/lib/api";

function str(formData: FormData, key: string): string | undefined {
  const v = formData.get(key);
  return typeof v === "string" && v.length > 0 ? v : undefined;
}

// Register a not-yet-verified institution as a representation request (event-first, D-074/D-075). The proof
// is uploaded via the user-scoped representation presign (the org doesn't exist yet), then the request stages
// a hidden placeholder org (PendingReview) with the caller as a *pending* Representative. The event
// publishes once an admin approves.
//
// D-267: this no longer sets an "active organization" cookie and no longer bounces the caller into event
// creation. The new representation simply joins the list the Representing step offers, so registering an
// institution and creating an event are independent acts rather than one funnel.
export async function submitRepresentationRequestAction(_: unknown, formData: FormData) {
  const filed = await fileRepresentationRequest(formData);
  if ("error" in filed) return filed;

  revalidatePath("/host/representing");
  // D-382 — back to the event the caller came from, when they came from one. `safeReturnTo` is what
  // keeps a form field from becoming an open redirect: only an in-app host path is ever followed.
  redirect(safeReturnTo(str(formData, "returnTo")) ?? "/host/representing");
}

/**
 * The same request, filed WITHOUT navigating: returns the staged organization so the caller can carry
 * on where it stands.
 *
 * Create Event's Representing step registers an institution inline, so a redirect is the one thing it
 * must not do — leaving the wizard is what discards ten steps of unsaved answers. The action above
 * still redirects because the standalone page has nowhere else to go; both file the identical request
 * through `fileRepresentationRequest`, so there is one code path and one set of rules.
 */
export async function registerRepresentationInlineAction(formData: FormData) {
  const filed = await fileRepresentationRequest(formData);
  if ("error" in filed) return filed;

  revalidatePath("/host/representing");
  // Shaped as a `Representation` — the wizard's picker reads that, not `OrgDetail`, and converting here
  // keeps the client from knowing two shapes for one thing. A freshly staged org is PendingReview by
  // construction, so both flags are false: draftable, not publishable, never paid-capable.
  const org = filed.org;
  return {
    organization_id: org.id,
    name: org.name,
    slug: org.slug,
    logo_key: org.logo_key,
    authority: org.role,
    is_verified: false,
    can_back_paid_event: false
  };
}

/// Presign → PUT → stage. Shared so the inline and standalone callers cannot drift apart on what a
/// representation request requires.
async function fileRepresentationRequest(formData: FormData) {
  const session = await requireSession();
  const name = str(formData, "name");
  if (!name) return { error: "Organization name is required." };

  const file = formData.get("letterhead");
  if (!(file instanceof File) || file.size === 0) {
    return { error: "Proof of affiliation is required to register an organization." };
  }

  try {
    const presigned = await presignRepresentationDoc(
      session.accessToken, file.type || "application/octet-stream", file.size
    );
    await fetch(presigned.url, { method: "PUT", headers: presigned.headers, body: await file.arrayBuffer() });
    const org = await submitRepresentationRequest(session.accessToken, {
      name,
      type: str(formData, "type"),
      primaryDomain: str(formData, "primaryDomain"),
      documents: [{ docType: "letterhead", storageKey: presigned.key }]
    });
    return { org };
  } catch (err) {
    return { error: apiErrorMessage(err) };
  }
}

/// A `returnTo` is a redirect target supplied by the browser, so it is treated as untrusted input.
/// Only a same-app path under `/host/` is followed, which is also why the test is not the usual
/// `startsWith("/")`: that admits protocol-relative `//evil.example` as a "path".
function safeReturnTo(value: string | undefined): string | undefined {
  return value?.startsWith("/host/") ? value : undefined;
}

// Registry search (D-074) for the "who are you representing?" step — returns the VERIFIED institutions a
// user can claim before registering a new one. Server-side so the session token stays httpOnly.
export async function searchOrgsAction(q: string) {
  const session = await requireSession();
  if (q.trim().length < 2) return [];
  try {
    const results = await searchOrganizations(session.accessToken, q.trim());
    return results.map((r) => ({ id: r.id, name: r.name, type: r.type, primaryDomain: r.primary_domain ?? null }));
  } catch {
    return [];
  }
}

// "Just me" is no longer a step the caller has to complete before the create-event form will open —
// Personal is a choice inside the form, and the backend resolves (or mints) the personal org itself
// when an event arrives with no representing organization (D-267).
