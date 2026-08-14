"use server";

import { revalidatePath } from "next/cache";
import { requireSession } from "@/lib/session";
import { updateIdCard, generateIdCard, apiErrorMessage } from "@/lib/api";

/** What the editor is allowed to send. Named after the server's holder input rather than after the
 * form, so the two stay visibly the same shape: if the server ever narrows what a holder may change,
 * the mismatch shows up here rather than as a 403 the user has to interpret. */
export type IdCardEditValues = {
  template?: string;
  layoutJson?: string;
  bloodGroup?: string;
  address?: string;
  emergencyContactName?: string;
  emergencyContactPhone?: string;
};

export type ActionResult = { ok: true } | { ok: false; error: string };

/** Saves the holder-editable fields. Deliberately takes a typed object rather than FormData: the
 * editor is a client component holding live state for the preview, so there is no form post to
 * intercept, and passing the object keeps the "nothing asserted can be sent" property checkable by
 * reading one type. */
export async function saveIdCardAction(cardId: string, values: IdCardEditValues): Promise<ActionResult> {
  const session = await requireSession();
  try {
    await updateIdCard(session.accessToken, cardId, values);
    revalidatePath(`/id-cards/${cardId}`);
    revalidatePath("/id-cards");
    return { ok: true };
  } catch (e) {
    return { ok: false, error: apiErrorMessage(e) };
  }
}

/** Renders the PDF and PNG. Separate from saving because generating is what makes a card real, and a
 * holder editing their address should not silently reissue the printed document. */
export async function generateIdCardAction(cardId: string): Promise<ActionResult> {
  const session = await requireSession();
  try {
    await generateIdCard(session.accessToken, cardId);
    revalidatePath(`/id-cards/${cardId}`);
    revalidatePath("/id-cards");
    return { ok: true };
  } catch (e) {
    return { ok: false, error: apiErrorMessage(e) };
  }
}
