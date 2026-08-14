"use server";

import { revalidatePath } from "next/cache";
import { requireSession } from "@/lib/session";
import { submitMembershipClaim, presignClaimDoc, apiErrorMessage } from "@/lib/api";

function str(formData: FormData, key: string): string | undefined {
  const v = formData.get(key);
  return typeof v === "string" && v.length > 0 ? v : undefined;
}

// A claim must carry proof — the backend rejects an evidence-less claim (invalid_evidence). Upload the
// file via the claim-scoped presign (D-055 G4), then submit the claim referencing its storage key.
export async function submitMembershipClaimAction(orgId: string, _: unknown, formData: FormData) {
  const session = await requireSession();
  const file = formData.get("proof");
  if (!(file instanceof File) || file.size === 0) {
    return { error: "Attach a document proving your affiliation (ID card, letter, etc.)." };
  }
  try {
    const presigned = await presignClaimDoc(
      session.accessToken, orgId, file.type || "application/octet-stream", file.size
    );
    await fetch(presigned.url, { method: "PUT", headers: presigned.headers, body: await file.arrayBuffer() });
    await submitMembershipClaim(session.accessToken, orgId, {
      claimedRole: str(formData, "claimedRole") ?? "Employee",
      documents: [{ docType: "membership_proof", storageKey: presigned.key }]
    });
  } catch (err) {
    return { error: apiErrorMessage(err) };
  }
  revalidatePath("/host/representing");
  return { ok: true };
}
