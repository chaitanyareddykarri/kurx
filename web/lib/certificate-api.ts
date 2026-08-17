import { z } from "zod";
import { api, authHeaders } from "@/lib/api";

/**
 * The certificate module's API client (D-355).
 *
 * A sibling of `lib/api.ts` rather than a second axios instance — one client, one auth convention.
 *
 * Geometry is percentages of the page throughout, matching the server exactly. A design laid out in a
 * browser viewport has to render identically onto A4 at 300dpi, and the only way that survives is for
 * nothing on either side to carry a resolution.
 */

export const CERTIFICATE_FIELD_KINDS = ["text", "dynamicfield", "image", "qrcode"] as const;
export type CertificateFieldKind = (typeof CERTIFICATE_FIELD_KINDS)[number];

export const certificateFieldSchema = z.object({
  id: z.string(),
  kind: z.enum(CERTIFICATE_FIELD_KINDS),
  field_key: z.string().nullable().optional(),
  label: z.string().nullable().optional(),
  static_text: z.string().nullable().optional(),
  x: z.number(),
  y: z.number(),
  width: z.number(),
  height: z.number(),
  rotation: z.number(),
  z_order: z.number(),
  is_required: z.boolean(),
  /// Covers text printed into the uploaded artwork. See the editor for why this is not "editing".
  is_masking: z.boolean(),
  /// A handle on text the artwork already prints: nothing drawn, nothing covered, until the creator
  /// changes the words. Covering unchanged text erases the design's texture for no gain.
  mirrors_artwork: z.boolean().optional().default(false),
  background_color: z.string().nullable().optional(),
  font_family: z.string().nullable().optional(),
  font_size_pt: z.number().nullable().optional(),
  font_weight: z.string().nullable().optional(),
  font_style: z.string().nullable().optional(),
  underline: z.boolean().optional(),
  line_height: z.number().nullable().optional(),
  letter_spacing: z.number().nullable().optional(),
  color: z.string().nullable().optional(),
  horizontal_alignment: z.string(),
  vertical_alignment: z.string()
});
export type CertificateField = z.infer<typeof certificateFieldSchema>;

export const certificateTemplateSchema = z.object({
  id: z.string(),
  event_id: z.string().nullable().optional(),
  owner_user_id: z.string(),
  name: z.string(),
  // A slug from the server's catalogue (D-361) — seven papers × two orientations, plus `custom`.
  // A plain string rather than an enum: the catalogue lives on the server, and pinning the list here
  // would recreate the duplicate table the decision removed.
  page_size: z.string(),
  /// The page in millimetres — authoritative. The canvas derives its aspect from these.
  page_width_mm: z.number(),
  page_height_mm: z.number(),
  status: z.enum(["draft", "ready", "archived"]),
  version: z.number(),
  background_storage_key: z.string().nullable().optional(),
  /// Presigned. A storage key is not fetchable (D-302), so without this the editor cannot draw the
  /// design it is placing fields onto.
  background_url: z.string().nullable().optional(),
  background_width_px: z.number().nullable().optional(),
  background_height_px: z.number().nullable().optional(),
  /// True once certificates exist. Further edits then produce a NEW version rather than changing what
  /// was already issued — the editor says so rather than letting it be a surprise.
  has_issued_certificates: z.boolean(),
  fields: z.array(certificateFieldSchema),
  created_at: z.string(),
  updated_at: z.string().nullable().optional()
});
export type CertificateTemplate = z.infer<typeof certificateTemplateSchema>;

/** The shape sent when saving the canvas. Ids are not round-tripped: the canvas is the truth and the
 *  server replaces the whole set, so nothing outside the template references a field row. */
export type CertificateFieldInput = {
  kind: CertificateFieldKind;
  fieldKey?: string | null;
  label?: string | null;
  staticText?: string | null;
  x: number;
  y: number;
  width: number;
  height: number;
  rotation?: number;
  zOrder?: number;
  isRequired?: boolean;
  isMasking?: boolean;
  backgroundColor?: string | null;
  fontFamily?: string | null;
  fontSizePt?: number | null;
  fontWeight?: string | null;
  color?: string | null;
  horizontalAlignment?: string | null;
  verticalAlignment?: string | null;
};

export async function listEventTemplates(accessToken: string, eventId: string) {
  const { data } = await api.get(`/v1/events/${eventId}/certificate-templates`, authHeaders(accessToken));
  return z.array(certificateTemplateSchema).parse(data);
}

export async function listLibraryTemplates(accessToken: string) {
  const { data } = await api.get(`/v1/me/certificate-templates`, authHeaders(accessToken));
  return z.array(certificateTemplateSchema).parse(data);
}

export async function getTemplate(accessToken: string, templateId: string) {
  const { data } = await api.get(`/v1/certificate-templates/${templateId}`, authHeaders(accessToken));
  return certificateTemplateSchema.parse(data);
}

export async function createEventTemplate(
  accessToken: string, eventId: string, body: { name: string; pageSize?: string; pageWidthMm?: number; pageHeightMm?: number }
) {
  const { data } = await api.post(`/v1/events/${eventId}/certificate-templates`, body, authHeaders(accessToken));
  return certificateTemplateSchema.parse(data);
}

export async function updateTemplate(
  accessToken: string, templateId: string, body: { name?: string; pageSize?: string; status?: string; pageWidthMm?: number; pageHeightMm?: number }
) {
  const { data } = await api.patch(`/v1/certificate-templates/${templateId}`, body, authHeaders(accessToken));
  return certificateTemplateSchema.parse(data);
}

export const presignedUploadSchema = z.object({
  key: z.string(),
  url: z.string(),
  headers: z.record(z.string()).nullable().optional()
});

/** Step 1 of an artwork upload. The bytes go browser → storage directly; nothing passes through the
 *  Next server. The minted key is under this template's own prefix, which is what the server validates
 *  it against on the way back in. */
export async function presignTemplateBackground(
  accessToken: string, templateId: string, contentType: string, maxBytes: number
) {
  const { data } = await api.post(
    `/v1/certificate-templates/${templateId}/background/presign`,
    { contentType, maxBytes },
    authHeaders(accessToken)
  );
  return presignedUploadSchema.parse(data);
}

/** Step 2, once the PUT succeeded. Separate because nothing may be recorded until the bytes actually
 *  landed — a template pointing at a key that was never written renders as a blank page. */
export async function setTemplateBackground(
  accessToken: string,
  templateId: string,
  body: { storageKey: string; contentType: string; widthPx: number; heightPx: number }
) {
  const { data } = await api.put(
    `/v1/certificate-templates/${templateId}/background`, body, authHeaders(accessToken));
  return certificateTemplateSchema.parse(data);
}

export async function replaceTemplateFields(
  accessToken: string, templateId: string, fields: CertificateFieldInput[]
) {
  const { data } = await api.put(
    `/v1/certificate-templates/${templateId}/fields`, { fields }, authHeaders(accessToken));
  return certificateTemplateSchema.parse(data);
}

export async function archiveTemplate(accessToken: string, templateId: string) {
  await api.delete(`/v1/certificate-templates/${templateId}`, authHeaders(accessToken));
}

/** Headers for a multipart upload.
 *
 *  The explicit `Content-Type: undefined` is load-bearing, not tidiness. The shared axios instance sets a
 *  default of `application/json`, and axios treats that as an instruction: given FormData and a JSON
 *  content type it silently serialises the form to JSON instead — the body becomes `{"file":{}}` and the
 *  file's bytes are dropped without an error anywhere. Clearing the header lets axios generate the
 *  multipart boundary itself, which is the only way the file actually leaves the process.
 */
function uploadHeaders(accessToken: string) {
  return { headers: { Authorization: `Bearer ${accessToken}`, "Content-Type": undefined } };
}

/** A participant list, read but not yet turned into anything (D-355, Phase 6). */
export const spreadsheetPreviewSchema = z.object({
  columns: z.array(z.string()),
  sampleRows: z.array(z.array(z.string())),
  totalRows: z.number(),
  truncated: z.boolean(),
  suggestedMapping: z.record(z.string()),
});
export type SpreadsheetPreview = z.infer<typeof spreadsheetPreviewSchema>;

/** Reads an uploaded participant list and returns its columns, a few real rows and a SUGGESTED mapping.
 *
 *  Multipart rather than JSON: a 10MB spreadsheet becomes 13MB of base64 that has to be held as a string
 *  before it can be decoded, and the browser already has an encoding for exactly this. Nothing is
 *  generated or stored by this call — it is the read that lets the organiser check the mapping first. */
export async function previewParticipants(accessToken: string, eventId: string, file: File) {
  const body = new FormData();
  body.append("file", file);
  const { data } = await api.post(
    `/v1/events/${eventId}/certificate-participants/preview`, body, uploadHeaders(accessToken));
  return spreadsheetPreviewSchema.parse(data);
}

/** A generation run (D-355, Phase 7). */
export const certificateBatchSchema = z.object({
  id: z.string(),
  event_id: z.string(),
  template_id: z.string(),
  template_version: z.number(),
  name: z.string(),
  status: z.string(),
  source_file_name: z.string().nullable(),
  row_count: z.number(),
  issued_count: z.number(),
  preview_count: z.number(),
  approved_at: z.string().nullable(),
  created_at: z.string(),
  preview_urls: z.array(z.string()),
  failed_rows: z.array(z.number()),
});
export type CertificateBatch = z.infer<typeof certificateBatchSchema>;

export async function listBatches(accessToken: string, eventId: string) {
  const { data } = await api.get(
    `/v1/events/${eventId}/certificate-batches`, authHeaders(accessToken));
  return z.array(certificateBatchSchema).parse(data);
}

export async function getBatch(accessToken: string, batchId: string) {
  const { data } = await api.get(`/v1/certificate-batches/${batchId}`, authHeaders(accessToken));
  return certificateBatchSchema.parse(data);
}

/** Creates the run. The mapping travels as JSON alongside the file, because the file has to be multipart
 *  and a 10MB spreadsheet base64'd into a JSON body is 13MB held as a string. */
export async function createBatch(
  accessToken: string, eventId: string,
  body: { name: string; templateId: string; file: File; mapping: Record<string, string> }
) {
  const form = new FormData();
  form.append("file", body.file);
  const { data } = await api.post(
    `/v1/events/${eventId}/certificate-batches`
      + `?name=${encodeURIComponent(body.name)}&templateId=${body.templateId}`
      + `&mapping=${encodeURIComponent(JSON.stringify(body.mapping))}`,
    form, uploadHeaders(accessToken));
  return certificateBatchSchema.parse(data);
}

export async function previewBatch(accessToken: string, batchId: string) {
  const { data } = await api.post(
    `/v1/certificate-batches/${batchId}/preview`, {}, authHeaders(accessToken));
  return certificateBatchSchema.parse(data);
}

/** The irreversible one. Everything before it is a parsed file and three sample renders. */
export async function approveBatch(accessToken: string, batchId: string) {
  const { data } = await api.post(
    `/v1/certificate-batches/${batchId}/approve`, {}, authHeaders(accessToken));
  return certificateBatchSchema.parse(data);
}

export async function cancelBatch(accessToken: string, batchId: string) {
  const { data } = await api.post(
    `/v1/certificate-batches/${batchId}/cancel`, {}, authHeaders(accessToken));
  return certificateBatchSchema.parse(data);
}

/** What happened to a run's sends (D-355, Phase 8). */
export const deliverySummarySchema = z.object({
  batch_id: z.string(),
  total: z.number(),
  pending: z.number(),
  sent: z.number(),
  failed: z.number(),
  no_destination: z.number(),
  recent: z.array(z.object({
    id: z.string(),
    certificate_id: z.string(),
    channel: z.string(),
    destination: z.string().nullable(),
    status: z.string(),
    error: z.string().nullable(),
    attempt_count: z.number(),
    sent_at: z.string().nullable(),
    created_at: z.string(),
  })),
});
export type DeliverySummary = z.infer<typeof deliverySummarySchema>;

/** Queues the sends. Returns immediately — the emails go out in the background, which is why the response
 *  is a summary of what was queued rather than of what was delivered. */
export async function sendBatch(accessToken: string, batchId: string) {
  const { data } = await api.post(
    `/v1/certificate-batches/${batchId}/send`, {}, authHeaders(accessToken));
  return deliverySummarySchema.parse(data);
}

export async function getDeliveries(accessToken: string, batchId: string) {
  const { data } = await api.get(
    `/v1/certificate-batches/${batchId}/deliveries`, authHeaders(accessToken));
  return deliverySummarySchema.parse(data);
}

/** One link in a certificate's chain (D-355, Phase 9). */
export const certificateLineageSchema = z.object({
  id: z.string(),
  certificate_id: z.string(),
  recipient_name: z.string(),
  status: z.string(),
  issued_at: z.string(),
  supersedes: z.string().nullable(),
  superseded_by: z.string().nullable(),
  revocation_reason: z.string().nullable(),
  revoked_at: z.string().nullable(),
});
export type CertificateLineage = z.infer<typeof certificateLineageSchema>;

/** Withdraws a certificate. Permanent, and the reason is published on the verification page. */
export async function revokeCertificate(accessToken: string, certificateRowId: string, reason: string) {
  const { data } = await api.post(
    `/v1/certificates/${certificateRowId}/revoke`, { reason }, authHeaders(accessToken));
  return certificateLineageSchema.parse(data);
}

export async function revokeBatch(accessToken: string, batchId: string, reason: string) {
  const { data } = await api.post(
    `/v1/certificate-batches/${batchId}/revoke`, { reason }, authHeaders(accessToken));
  return z.object({ revoked: z.number(), skipped: z.number() }).parse(data);
}

/** Issues a corrected replacement. Omitted fields carry over from the certificate being replaced. */
export async function reissueCertificate(
  accessToken: string, certificateRowId: string,
  body: { recipientName?: string; values?: Record<string, string>; reason: string }
) {
  const { data } = await api.post(
    `/v1/certificates/${certificateRowId}/reissue`, body, authHeaders(accessToken));
  return certificateLineageSchema.parse(data);
}

export async function getLineage(accessToken: string, certificateRowId: string) {
  const { data } = await api.get(
    `/v1/certificates/${certificateRowId}/lineage`, authHeaders(accessToken));
  return z.array(certificateLineageSchema).parse(data);
}

/** What a participant sees of their own certificate (D-355, Phase 10). */
export const participantCertificateSchema = z.object({
  certificate_id: z.string(),
  event_title: z.string(),
  status: z.string(),
  issued_at: z.string(),
  verification_url: z.string(),
  download_pdf_url: z.string().nullable(),
  download_png_url: z.string().nullable(),
  replaced_by: z.string().nullable(),
  revocation_reason: z.string().nullable(),
});
export const participantCertificatesSchema = z.object({
  recipient_name: z.string(),
  certificates: z.array(participantCertificateSchema),
});
export type ParticipantCertificates = z.infer<typeof participantCertificatesSchema>;

export const accessLinkSchema = z.object({
  id: z.string(),
  recipient_id: z.string(),
  url: z.string().nullable(),
  revoked: z.boolean(),
  created_at: z.string(),
  last_accessed_at: z.string().nullable(),
  revoked_at: z.string().nullable(),
});
export type AccessLink = z.infer<typeof accessLinkSchema>;

/** Resolves a capability link. No token needed beyond the one in the path — holding it IS the
 *  authorisation, which is the point: the person this reaches has no account. */
export async function resolveCertificateAccess(token: string) {
  const { data } = await api.get(`/v1/certificate-access/${encodeURIComponent(token)}`);
  return participantCertificatesSchema.parse(data);
}

export async function listMyCertificates(accessToken: string) {
  const { data } = await api.get("/v1/me/certificates", authHeaders(accessToken));
  return participantCertificatesSchema.parse(data);
}

/** Mints a link. The `url` on the response is the only time the token exists outside the recipient's
 *  hands — it is not stored and cannot be shown again. */
export async function createAccessLink(accessToken: string, recipientId: string) {
  const { data } = await api.post(
    `/v1/certificate-recipients/${recipientId}/access-link`, {}, authHeaders(accessToken));
  return accessLinkSchema.parse(data);
}

export async function listAccessLinks(accessToken: string, recipientId: string) {
  const { data } = await api.get(
    `/v1/certificate-recipients/${recipientId}/access-links`, authHeaders(accessToken));
  return z.array(accessLinkSchema).parse(data);
}

export async function revokeAccessLink(accessToken: string, linkId: string) {
  await api.delete(`/v1/certificate-access-links/${linkId}`, authHeaders(accessToken));
}

/** Certificate activity for an event (D-355, Phase 11). */
export const certificateDashboardSchema = z.object({
  templates: z.number(),
  batches: z.number(),
  batches_in_progress: z.number(),
  live: z.number(),
  revoked: z.number(),
  superseded: z.number(),
  sent: z.number(),
  pending_delivery: z.number(),
  failed_delivery: z.number(),
  no_destination: z.number(),
  verifications: z.number(),
  views: z.number(),
  recent_verifications: z.array(z.object({ day: z.string(), count: z.number() })),
});
export type CertificateDashboard = z.infer<typeof certificateDashboardSchema>;

export async function getCertificateDashboard(accessToken: string, eventId: string) {
  const { data } = await api.get(
    `/v1/events/${eventId}/certificate-dashboard`, authHeaders(accessToken));
  return certificateDashboardSchema.parse(data);
}

/** The OCR extension point (D-355, Phase 12). The schema lives in `certificate-detection.ts` so the
 *  contract can be tested without this module's API client; re-exported here so callers have one import. */
export { textDetectionSchema, type TextDetection } from "@/lib/certificate-detection";
import { textDetectionSchema as detectionSchema } from "@/lib/certificate-detection";

export async function detectTemplateText(accessToken: string, templateId: string) {
  const { data } = await api.post(
    `/v1/certificate-templates/${templateId}/detect-text`, {}, authHeaders(accessToken));
  return detectionSchema.parse(data);
}

/** Saves a copy of a design into the caller's reusable library (D-355, Phase 13). A copy, never a
 *  reference — the two designs share nothing afterwards. */
export async function copyTemplateToLibrary(
  accessToken: string, templateId: string, name?: string
) {
  const { data } = await api.post(
    `/v1/certificate-templates/${templateId}/copy-to-library`, { name }, authHeaders(accessToken));
  return certificateTemplateSchema.parse(data);
}

export async function copyTemplateToEvent(
  accessToken: string, templateId: string, eventId: string, name?: string
) {
  const { data } = await api.post(
    `/v1/certificate-templates/${templateId}/copy-to-event/${eventId}`, { name },
    authHeaders(accessToken));
  return certificateTemplateSchema.parse(data);
}

/** Where the design already has something printed (D-355). Fetched once when the editor opens. */
export { artworkMapSchema, type ArtworkMap } from "@/lib/certificate-artwork-map";
import { artworkMapSchema as artworkSchema } from "@/lib/certificate-artwork-map";

export async function getArtworkMap(accessToken: string, templateId: string) {
  const { data } = await api.get(
    `/v1/certificate-templates/${templateId}/artwork-map`, authHeaders(accessToken));
  return artworkSchema.parse(data);
}

/** The artwork's own colour behind a region — what a field needs to cover printed text without leaving
 *  a visible patch (D-355). */
export async function getArtworkColour(
  accessToken: string, templateId: string,
  region: { x: number; y: number; width: number; height: number }
) {
  const query = new URLSearchParams({
    x: String(region.x), y: String(region.y),
    width: String(region.width), height: String(region.height),
  });
  const { data } = await api.get(
    `/v1/certificate-templates/${templateId}/artwork-colour?${query}`, authHeaders(accessToken));
  return z.object({ colour: z.string() }).parse(data).colour;
}

export const pageSizePresetSchema = z.object({
  slug: z.string(),
  family: z.string(),
  label: z.string(),
  landscape: z.boolean(),
  width_mm: z.number(),
  height_mm: z.number()
});
export type PageSizePreset = z.infer<typeof pageSizePresetSchema>;

export const pageSizeCatalogueSchema = z.object({
  presets: z.array(pageSizePresetSchema),
  custom: z.object({ min_mm: z.number(), max_mm: z.number() })
});
export type PageSizeCatalogue = z.infer<typeof pageSizeCatalogueSchema>;

/** The page-size catalogue (D-361). Fetched rather than hardcoded so the dimensions have one home. */
export async function listPageSizes(accessToken: string) {
  const { data } = await api.get("/v1/certificate-page-sizes", authHeaders(accessToken));
  return pageSizeCatalogueSchema.parse(data);
}
