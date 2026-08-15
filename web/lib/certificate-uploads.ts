import {
  certificateBatchSchema, spreadsheetPreviewSchema,
  type CertificateBatch, type SpreadsheetPreview
} from "@/lib/certificate-api";

/**
 * Uploads that carry a file (D-344, Phases 6 and 7).
 *
 * These go to route handlers under `/api/…` rather than through Server Actions. Actions were tried first
 * and failed silently — no entry in the API log, none in the Next log — because the failure happened at
 * the action boundary, before any code we wrote. A plain multipart `fetch` removes that boundary: the
 * request either reaches the handler or fails with a status you can read.
 *
 * Every function here returns a discriminated result rather than throwing, so a caller cannot forget to
 * handle failure, and never leaves a spinner running because an exception escaped.
 */

type Result<T> = { ok: true; value: T } | { ok: false; error: string };

/** Turns whatever came back into a sentence, preferring the API's own machine-readable code. */
function messageFor(status: number, payload: unknown): string {
  const code = typeof payload === "object" && payload !== null && "error" in payload
    ? String((payload as { error?: unknown }).error ?? "")
    : "";

  const known: Record<string, string> = {
    unauthenticated: "Your session has expired. Sign in again.",
    empty_file: "That file is empty.",
    file_too_large: "That file is over 10 MB.",
    unsupported_file_type: "Upload a .csv or .xlsx file.",
    no_rows: "That file has a header row but no participants under it.",
    invalid_spreadsheet: "That file could not be read as a spreadsheet.",
    forbidden: "You need Manager access on this event.",
    not_found: "That event no longer exists, or it belongs to someone else.",
    participant_name_not_mapped: "Choose which column holds the participant's name.",
    required_field_not_mapped: "Every required field on the design needs a column behind it.",
    rows_missing_participant_name: "Some rows have no name in them. Fill those in and upload again.",
    template_not_found: "That design no longer exists, or it belongs to another event.",
    background_required: "That design has no artwork yet.",
    invalid_batch_name: "Give this run a name.",
  };

  if (known[code]) return known[code];
  if (status === 413) return "That file is over 10 MB.";
  // Deliberately includes the status: an unrecognised failure that names its own code is one somebody can
  // act on, where "something went wrong" is not.
  return `The upload failed (HTTP ${status}${code ? ` — ${code}` : ""}). Try again.`;
}

async function post<T>(url: string, body: FormData, parse: (raw: unknown) => T): Promise<Result<T>> {
  let response: Response;
  try {
    // No Content-Type header set by hand: the browser must add it, because only it knows the multipart
    // boundary it generated.
    response = await fetch(url, { method: "POST", body });
  } catch {
    return { ok: false, error: "Could not reach the server. Check your connection and try again." };
  }

  const text = await response.text();
  let payload: unknown = null;
  try { payload = JSON.parse(text); } catch { /* handled below */ }

  if (!response.ok) return { ok: false, error: messageFor(response.status, payload) };

  try {
    return { ok: true, value: parse(payload) };
  } catch {
    // The request succeeded but the shape is not what this build expects — usually a stale tab against a
    // newer server. Saying so beats rendering half a screen.
    return { ok: false, error: "The server sent something this page did not understand. Reload and retry." };
  }
}

export function uploadParticipantList(
  eventId: string, file: File
): Promise<Result<SpreadsheetPreview>> {
  const body = new FormData();
  body.append("file", file);
  return post(
    `/api/events/${eventId}/certificate-participants/preview`,
    body,
    (raw) => spreadsheetPreviewSchema.parse(raw));
}

export function createCertificateBatch(
  eventId: string,
  input: { name: string; templateId: string; file: File; mapping: Record<string, string> }
): Promise<Result<CertificateBatch>> {
  const body = new FormData();
  body.append("file", input.file);
  const query = new URLSearchParams({
    name: input.name,
    templateId: input.templateId,
    mapping: JSON.stringify(input.mapping),
  });
  return post(
    `/api/events/${eventId}/certificate-batches?${query}`,
    body,
    (raw) => certificateBatchSchema.parse(raw));
}
