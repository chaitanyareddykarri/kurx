"use server";

import { revalidatePath } from "next/cache";
import { requireSession } from "@/lib/session";
import { DETECTION_UNAVAILABLE } from "@/lib/certificate-detection";
import { apiErrorCode, apiErrorMessage, apiErrorStatus } from "@/lib/api";
import {
  archiveTemplate, createEventTemplate, presignTemplateBackground, replaceTemplateFields,
  approveBatch, cancelBatch, createBatch, getBatch, getDeliveries, getLineage, previewBatch,
  copyTemplateToEvent, copyTemplateToLibrary, createAccessLink, detectTemplateText, getArtworkColour, getArtworkMap,
  listAccessLinks,
  reissueCertificate, revokeAccessLink,
  revokeBatch,
  revokeCertificate, sendBatch,
  previewParticipants, setTemplateBackground, updateTemplate,
  type ArtworkMap, type CertificateFieldInput, type TextDetection
} from "@/lib/certificate-api";

/**
 * Certificate module writes (D-344).
 *
 * Every action returns `{ ok }` or `{ error }` rather than throwing: these run behind buttons inside an
 * editor, and an unhandled throw replaces the whole page with an error boundary — losing the layout
 * someone has been positioning for the last twenty minutes over one failed save.
 *
 * Authorization is never re-decided here. The server owns it, and a template belonging to someone else
 * comes back 404; this layer only turns that into a sentence.
 */

function fail(err: unknown) {
  if (apiErrorStatus(err) === 403) {
    return { error: "You need Manager access on this event to change its certificate designs." };
  }
  if (apiErrorStatus(err) === 404) {
    return { error: "That design no longer exists, or it belongs to someone else." };
  }
  return { error: apiErrorMessage(err) };
}

function refresh(eventId?: string | null) {
  if (eventId) revalidatePath(`/host/events/${eventId}/certificates`);
}

export async function createTemplateAction(eventId: string, name: string, pageSize?: string) {
  const session = await requireSession();
  try {
    const template = await createEventTemplate(session.accessToken, eventId, { name, pageSize });
    refresh(eventId);
    return { ok: true as const, template };
  } catch (err) {
    return fail(err);
  }
}

export async function updateTemplateAction(
  templateId: string,
  body: { name?: string; pageSize?: string; status?: string },
  eventId?: string | null
) {
  const session = await requireSession();
  try {
    const template = await updateTemplate(session.accessToken, templateId, body);
    refresh(eventId);
    return { ok: true as const, template };
  } catch (err) {
    return fail(err);
  }
}

/**
 * Step 1 of an artwork upload. A server action because the access token is an httpOnly cookie the
 * browser cannot read; the browser then does step 2 (PUT the bytes) with the returned URL, so no image
 * ever passes through this server.
 */
export async function presignBackgroundAction(templateId: string, contentType: string, maxBytes: number) {
  const session = await requireSession();
  try {
    return { ok: true as const, ...(await presignTemplateBackground(session.accessToken, templateId, contentType, maxBytes)) };
  } catch (err) {
    return fail(err);
  }
}

export async function setBackgroundAction(
  templateId: string,
  body: { storageKey: string; contentType: string; widthPx: number; heightPx: number },
  eventId?: string | null
) {
  const session = await requireSession();
  try {
    const template = await setTemplateBackground(session.accessToken, templateId, body);
    refresh(eventId);
    return { ok: true as const, template };
  } catch (err) {
    return fail(err);
  }
}

export async function saveFieldsAction(
  templateId: string, fields: CertificateFieldInput[], eventId?: string | null
) {
  const session = await requireSession();
  try {
    const template = await replaceTemplateFields(session.accessToken, templateId, fields);
    refresh(eventId);
    return { ok: true as const, template };
  } catch (err) {
    return fail(err);
  }
}

export async function archiveTemplateAction(templateId: string, eventId?: string | null) {
  const session = await requireSession();
  try {
    await archiveTemplate(session.accessToken, templateId);
    refresh(eventId);
    return { ok: true as const };
  } catch (err) {
    return fail(err);
  }
}

/** Reads an uploaded participant list (D-344, Phase 6). The file goes through the Next server rather
 *  than to storage, because it is parsed and thrown away — nothing is persisted until the organiser has
 *  confirmed the mapping. */
export async function previewParticipantsAction(eventId: string, file: File) {
  const session = await requireSession();
  try {
    return { ok: true as const, preview: await previewParticipants(session.accessToken, eventId, file) };
  } catch (err) {
    const message = {
      empty_file: "That file is empty.",
      file_too_large: "That file is over 10 MB.",
      unsupported_file_type: "Upload a .csv or .xlsx file.",
      no_rows: "That file has a header row but no participants under it.",
      invalid_spreadsheet: "That file could not be read as a spreadsheet.",
    }[apiErrorCode(err) ?? ""];
    return { ok: false as const, error: message ?? fail(err).error };
  }
}

/** Certificate run actions (D-344, Phase 7). Each returns `{ ok }` or `{ error }` rather than throwing:
 *  these run behind buttons in a multi-step flow, and an unhandled throw would replace the page with an
 *  error boundary, losing an upload and a mapping someone has just finished confirming. */
export async function createBatchAction(
  eventId: string, body: { name: string; templateId: string; file: File; mapping: Record<string, string> }
) {
  const session = await requireSession();
  try {
    const batch = await createBatch(session.accessToken, eventId, body);
    revalidatePath(`/host/events/${eventId}/certificates`);
    return { ok: true as const, batch };
  } catch (err) {
    return { ok: false as const, error: batchErrorMessage(err) };
  }
}

export async function previewBatchAction(batchId: string) {
  const session = await requireSession();
  try {
    return { ok: true as const, batch: await previewBatch(session.accessToken, batchId) };
  } catch (err) {
    return { ok: false as const, error: batchErrorMessage(err) };
  }
}

export async function approveBatchAction(eventId: string, batchId: string) {
  const session = await requireSession();
  try {
    const batch = await approveBatch(session.accessToken, batchId);
    revalidatePath(`/host/events/${eventId}/certificates`);
    return { ok: true as const, batch };
  } catch (err) {
    return { ok: false as const, error: batchErrorMessage(err) };
  }
}

export async function cancelBatchAction(eventId: string, batchId: string) {
  const session = await requireSession();
  try {
    const batch = await cancelBatch(session.accessToken, batchId);
    revalidatePath(`/host/events/${eventId}/certificates`);
    return { ok: true as const, batch };
  } catch (err) {
    return { ok: false as const, error: batchErrorMessage(err) };
  }
}

/** Polls a run in progress. Read-only, so it never revalidates — a poll that busts the route cache every
 *  two seconds would re-render the whole page for a number that changed. */
export async function getBatchAction(batchId: string) {
  const session = await requireSession();
  try {
    return { ok: true as const, batch: await getBatch(session.accessToken, batchId) };
  } catch (err) {
    return { ok: false as const, error: batchErrorMessage(err) };
  }
}

function batchErrorMessage(err: unknown) {
  const message = {
    participant_name_not_mapped: "Choose which column holds the participant's name.",
    required_field_not_mapped: "Every required field on the design needs a column behind it.",
    rows_missing_participant_name: "Some rows have no name in them. Fill those in and upload again.",
    template_not_found: "That design no longer exists, or it belongs to another event.",
    background_required: "That design has no artwork yet.",
    preview_required: "Generate and check the sample certificates before approving.",
    batch_not_previewable: "This run has already been approved.",
    batch_not_cancellable: "This run has already finished.",
    invalid_batch_name: "Give this run a name between 1 and 120 characters.",
    file_too_large: "That file is over 10 MB.",
    unsupported_file_type: "Upload a .csv or .xlsx file.",
    no_rows: "That file has a header row but no participants under it.",
    invalid_spreadsheet: "That file could not be read as a spreadsheet.",
  }[apiErrorCode(err) ?? ""];
  return message ?? fail(err).error;
}

/** Queues a run's certificates for emailing (D-344, Phase 8). */
export async function sendBatchAction(eventId: string, batchId: string) {
  const session = await requireSession();
  try {
    const summary = await sendBatch(session.accessToken, batchId);
    revalidatePath(`/host/events/${eventId}/certificates`);
    return { ok: true as const, summary };
  } catch (err) {
    return {
      ok: false as const,
      error: apiErrorCode(err) === "nothing_to_send"
        ? "This run hasn't generated any certificates yet."
        : fail(err).error,
    };
  }
}

/** Polls the sending progress. Read-only, so it never revalidates. */
export async function getDeliveriesAction(batchId: string) {
  const session = await requireSession();
  try {
    return { ok: true as const, summary: await getDeliveries(session.accessToken, batchId) };
  } catch (err) {
    return { ok: false as const, error: fail(err).error };
  }
}

/** Certificate corrections and withdrawals (D-344, Phase 9). */
export async function revokeCertificateAction(eventId: string, certificateRowId: string, reason: string) {
  const session = await requireSession();
  try {
    const certificate = await revokeCertificate(session.accessToken, certificateRowId, reason);
    revalidatePath(`/host/events/${eventId}/certificates`);
    return { ok: true as const, certificate };
  } catch (err) {
    return { ok: false as const, error: revocationErrorMessage(err) };
  }
}

export async function revokeBatchAction(eventId: string, batchId: string, reason: string) {
  const session = await requireSession();
  try {
    const result = await revokeBatch(session.accessToken, batchId, reason);
    revalidatePath(`/host/events/${eventId}/certificates`);
    return { ok: true as const, result };
  } catch (err) {
    return { ok: false as const, error: revocationErrorMessage(err) };
  }
}

export async function reissueCertificateAction(
  eventId: string, certificateRowId: string,
  body: { recipientName?: string; values?: Record<string, string>; reason: string }
) {
  const session = await requireSession();
  try {
    const certificate = await reissueCertificate(session.accessToken, certificateRowId, body);
    revalidatePath(`/host/events/${eventId}/certificates`);
    return { ok: true as const, certificate };
  } catch (err) {
    return { ok: false as const, error: revocationErrorMessage(err) };
  }
}

export async function getLineageAction(certificateRowId: string) {
  const session = await requireSession();
  try {
    return { ok: true as const, lineage: await getLineage(session.accessToken, certificateRowId) };
  } catch (err) {
    return { ok: false as const, error: revocationErrorMessage(err) };
  }
}

function revocationErrorMessage(err: unknown) {
  const message = {
    invalid_reason: "Say why — the reason is shown to anyone who checks this certificate.",
    already_revoked: "This certificate has already been withdrawn.",
    certificate_revoked: "This certificate was withdrawn, so it can't be corrected. Issue a new one instead.",
    certificate_superseded:
      "This one has already been replaced. Correct the replacement rather than this copy.",
    recipient_not_found: "The recipient this certificate was issued to no longer exists.",
    background_required: "The design this certificate used no longer has its artwork.",
  }[apiErrorCode(err) ?? ""];
  return message ?? fail(err).error;
}

/** Capability links for participants without accounts (D-344, Phase 10). */
export async function createAccessLinkAction(recipientId: string) {
  const session = await requireSession();
  try {
    return { ok: true as const, link: await createAccessLink(session.accessToken, recipientId) };
  } catch (err) {
    return { ok: false as const, error: fail(err).error };
  }
}

export async function listAccessLinksAction(recipientId: string) {
  const session = await requireSession();
  try {
    return { ok: true as const, links: await listAccessLinks(session.accessToken, recipientId) };
  } catch (err) {
    return { ok: false as const, error: fail(err).error };
  }
}

export async function revokeAccessLinkAction(recipientId: string, linkId: string) {
  const session = await requireSession();
  try {
    await revokeAccessLink(session.accessToken, linkId);
    return { ok: true as const };
  } catch (err) {
    return { ok: false as const, error: fail(err).error };
  }
}

/** Asks whether this deployment can find text on the artwork (D-344, Phase 12).
 *
 *  Returns "unavailable" rather than an error for every failure, including a network one. Detection is an
 *  assist that no build currently provides; an editor must never be blocked, or even interrupted, by the
 *  absence of something optional. */
export async function detectTemplateTextAction(templateId: string): Promise<TextDetection> {
  try {
    const session = await requireSession();
    return await detectTemplateText(session.accessToken, templateId);
  } catch {
    return DETECTION_UNAVAILABLE;
  }
}

/** Design reuse (D-344, Phase 13). */
export async function copyToLibraryAction(eventId: string, templateId: string, name?: string) {
  const session = await requireSession();
  try {
    const template = await copyTemplateToLibrary(session.accessToken, templateId, name);
    revalidatePath(`/host/events/${eventId}/certificates`);
    return { ok: true as const, template };
  } catch (err) {
    return { ok: false as const, error: reuseErrorMessage(err) };
  }
}

export async function copyToEventAction(eventId: string, templateId: string, name?: string) {
  const session = await requireSession();
  try {
    const template = await copyTemplateToEvent(session.accessToken, templateId, eventId, name);
    revalidatePath(`/host/events/${eventId}/certificates`);
    return { ok: true as const, template };
  } catch (err) {
    return { ok: false as const, error: reuseErrorMessage(err) };
  }
}

function reuseErrorMessage(err: unknown) {
  const message = {
    invalid_name: "Give the copy a name.",
    artwork_unavailable:
      "This design's artwork could not be read, so copying it would produce a blank certificate.",
  }[apiErrorCode(err) ?? ""];
  return message ?? fail(err).error;
}

/** Where the design already has something printed (D-344). Returns an unanalysed map on any failure —
 *  a warning that cannot be computed must never render as "your design is clear here". */
export async function getArtworkMapAction(templateId: string): Promise<ArtworkMap> {
  try {
    const session = await requireSession();
    return await getArtworkMap(session.accessToken, templateId);
  } catch {
    return { columns: 0, rows: 0, cells: "", analysed: false };
  }
}

/** The artwork's colour behind a region. Null when it cannot be sampled — no colour is better than a
 *  wrong one, because a guessed patch is a visible smear on somebody's certificate. */
export async function getArtworkColourAction(
  templateId: string, region: { x: number; y: number; width: number; height: number }
): Promise<string | null> {
  try {
    const session = await requireSession();
    return await getArtworkColour(session.accessToken, templateId, region);
  } catch {
    return null;
  }
}
