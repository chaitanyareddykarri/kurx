"use server";

import { revalidatePath } from "next/cache";
import { requireSession } from "@/lib/session";
import { presignRepresentationDoc, submitRepresentationRequest, searchOrganizations, updateOrg, apiErrorMessage } from "@/lib/api";

// Register a not-yet-verified institution as a representation request (event-first, D-074/D-075): stages a
// hidden placeholder org (PendingReview) with the caller as a *pending* Representative, which an admin
// approves into the registry. Two steps, because the proof upload has to happen in the browser — see below.

/**
 * Presign the proof of affiliation. **The upload itself is NOT done here.**
 *
 * The presigned URL is browser-facing (`API_BASE`, e.g. `http://localhost:5080/...`). A server action
 * runs inside the Next.js container, where that host does not exist — `fetch` there dies with
 * `TypeError: fetch failed` (ECONNREFUSED), which is what "fetch failed" on the letterhead upload was.
 * The caller PUTs from the browser, exactly as `AuthorizationForm` and the create-event wizard already
 * do for the authorization letter, and passes the returned `key` to `fileRepresentationRequestAction`.
 */
export async function presignRepresentationDocAction(contentType: string, sizeBytes: number) {
  const session = await requireSession();
  try {
    return await presignRepresentationDoc(session.accessToken, contentType || "application/octet-stream", sizeBytes);
  } catch (err) {
    return { error: apiErrorMessage(err) };
  }
}

/// Stage the institution against an already-uploaded proof. Returns the organization shaped as a
/// `Representation`, because that is what the Representing step's picker reads.
export async function fileRepresentationRequestAction(input: {
  name: string; type?: string; primaryDomain?: string; storageKey: string;
}) {
  const session = await requireSession();
  const name = input.name.trim();
  if (!name) return { error: "Organization name is required." };
  if (!input.storageKey) return { error: "Proof of affiliation is required to register an organization." };

  try {
    const org = await submitRepresentationRequest(session.accessToken, {
      name,
      type: input.type || undefined,
      primaryDomain: input.primaryDomain || undefined,
      documents: [{ docType: "letterhead", storageKey: input.storageKey }]
    });
    revalidatePath("/host/representing");
    // A freshly staged org is PendingReview by construction: draftable, never paid-capable.
    return {
      organization_id: org.id,
      name: org.name,
      slug: org.slug,
      logo_key: org.logo_key,
      authority: org.role,
      is_verified: false,
      can_back_paid_event: false
    };
  } catch (err) {
    return { error: apiErrorMessage(err) };
  }
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
