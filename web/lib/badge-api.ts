import { z } from "zod";
import { api, authHeaders } from "@/lib/api";

/**
 * Event badges (D-362) — organizer-only.
 *
 * There is deliberately no "my badge" call here and no holder-facing route to call. Badges are
 * pre-printed onto lanyards by whoever runs the event; a holder never fetches their own. Adding one
 * would be a product change, not a convenience.
 */

export const badgeSizeSchema = z.object({
  key: z.string(),
  label: z.string(),
  width_mm: z.number(),
  height_mm: z.number(),
  landscape: z.boolean()
});

export type BadgeSize = z.infer<typeof badgeSizeSchema>;

export const issuedCardSchema = z.object({
  id: z.string(),
  card_number: z.string(),
  verify_code: z.string(),
  status: z.string(),
  is_revoked: z.boolean(),
  generated_at: z.string().nullable()
});

export type IssuedCard = z.infer<typeof issuedCardSchema>;

export const badgeRecipientSchema = z.object({
  user_id: z.string(),
  name: z.string(),
  kind: z.enum(["attendee", "staff"]),
  subtitle: z.string().nullable(),
  access_level: z.string().nullable(),
  // Whether a photo will print. The QR payload is deliberately NOT served — it is a working credential,
  // and a console listing everyone's scannable code would hand out badges as JSON.
  has_photo: z.boolean(),
  // Null means no id_cards row exists yet — "not issued", which is a different state from issued-and-revoked.
  card: issuedCardSchema.nullable().optional()
});

export type BadgeRecipient = z.infer<typeof badgeRecipientSchema>;

export async function listBadgeSizes(accessToken: string, eventId: string) {
  const { data } = await api.get(`/v1/events/${eventId}/badges/sizes`, authHeaders(accessToken));
  return z.array(badgeSizeSchema).parse(data);
}

export async function listBadgeRecipients(accessToken: string, eventId: string) {
  const { data } = await api.get(`/v1/events/${eventId}/badges/recipients`, authHeaders(accessToken));
  return z.array(badgeRecipientSchema).parse(data);
}

/**
 * The event's editable ID card design (D-362).
 *
 * **camelCase in, snake_case out.** `IdCardTemplateSpec` is declared in `Kurx.Application.Abstractions`,
 * so the API's SnakeCaseResponseConverter rewrites its properties on the way out — including nested
 * records, which is the part most easily missed (`font_size_pt`, `z_order`). Requests bind camelCase.
 * The wire shape is therefore parsed explicitly and mapped once, rather than letting a camelCase schema
 * silently fail to match every response.
 */
export const BADGE_FIELD_KEYS = [
  "photo", "logo", "qr", "holder_name", "subtitle",
  "access_level", "event_name", "event_date", "card_number", "accent_band"
] as const;

export type BadgeFieldKey = (typeof BADGE_FIELD_KEYS)[number];

const badgeFieldWireSchema = z.object({
  key: z.string(),
  x: z.number(),
  y: z.number(),
  width: z.number(),
  height: z.number(),
  font_size_pt: z.number().nullable(),
  color: z.string().nullable(),
  align: z.string(),
  weight: z.string().nullable(),
  z_order: z.number(),
  enabled: z.boolean()
});

const idCardTemplateWireSchema = z.object({
  accent_color: z.string().nullable(),
  text_color: z.string().nullable(),
  logo_key: z.string().nullable(),
  background_key: z.string().nullable(),
  size_key: z.string(),
  fields: z.array(badgeFieldWireSchema).nullable()
});

export type BadgeField = {
  key: BadgeFieldKey;
  x: number;
  y: number;
  width: number;
  height: number;
  fontSizePt: number | null;
  color: string | null;
  align: string;
  weight: string | null;
  zOrder: number;
  enabled: boolean;
};

export type IdCardTemplate = {
  accentColor: string | null;
  textColor: string | null;
  logoKey: string | null;
  backgroundKey: string | null;
  sizeKey: string;
  fields: BadgeField[] | null;
};

function fromWire(raw: unknown): IdCardTemplate {
  const w = idCardTemplateWireSchema.parse(raw);
  return {
    accentColor: w.accent_color,
    textColor: w.text_color,
    logoKey: w.logo_key,
    backgroundKey: w.background_key,
    sizeKey: w.size_key,
    fields:
      w.fields?.map((f) => ({
        key: f.key as BadgeFieldKey,
        x: f.x,
        y: f.y,
        width: f.width,
        height: f.height,
        fontSizePt: f.font_size_pt,
        color: f.color,
        align: f.align,
        weight: f.weight,
        zOrder: f.z_order,
        enabled: f.enabled
      })) ?? null
  };
}

export async function getIdCardTemplate(accessToken: string, eventId: string) {
  const { data } = await api.get(`/v1/events/${eventId}/badges/template`, authHeaders(accessToken));
  return fromWire(data);
}

/**
 * The built-in placements for a size (D-385).
 *
 * `getIdCardTemplate` answers `fields: null` for an event whose design has never been saved. That means
 * "the server will use its built-in layout" — it does **not** mean the card is empty. An editor that
 * renders it as no fields shows a blank card the server would never print, and the first checkbox tick
 * then sends a one-field layout that *replaces* the whole default. So the defaults are fetched and
 * seeded rather than guessed.
 */
export async function getBadgeFieldDefaults(
  accessToken: string,
  eventId: string,
  sizeKey: string,
  kind: "attendee" | "staff"
): Promise<BadgeField[]> {
  const { data } = await api.get(`/v1/events/${eventId}/badges/template/defaults`, {
    ...authHeaders(accessToken),
    params: { size: sizeKey, kind }
  });
  return z.array(badgeFieldWireSchema).parse(data).map((f) => ({
    key: f.key as BadgeFieldKey,
    x: f.x,
    y: f.y,
    width: f.width,
    height: f.height,
    fontSizePt: f.font_size_pt,
    color: f.color,
    align: f.align,
    weight: f.weight,
    zOrder: f.z_order,
    enabled: f.enabled
  }));
}

export async function saveIdCardTemplate(accessToken: string, eventId: string, spec: IdCardTemplate) {
  // Sent camelCase — request binding is camelCase, only responses are rewritten.
  const { data } = await api.put(`/v1/events/${eventId}/badges/template`, spec, authHeaders(accessToken));
  return fromWire(data);
}

/**
 * Renders the spec as given — unsaved — through the same renderer that prints the real card, so the
 * preview and the printed badge cannot drift apart.
 */
export async function previewIdCardTemplate(
  accessToken: string,
  eventId: string,
  spec: IdCardTemplate,
  kind: "attendee" | "staff"
): Promise<Blob> {
  const { data } = await api.post(
    `/v1/events/${eventId}/badges/template/preview`,
    { spec, kind },
    { ...authHeaders(accessToken), responseType: "blob" }
  );
  return data as Blob;
}

/** Presigned upload for the card's artwork or logo. Returns the key to store on the design. */
export async function presignIdCardAsset(
  accessToken: string,
  eventId: string,
  contentType: string,
  purpose: "background" | "logo"
) {
  const { data } = await api.post(
    `/v1/events/${eventId}/badges/template/asset/presign`,
    { contentType, purpose },
    authHeaders(accessToken)
  );
  return z.object({ key: z.string(), url: z.string(), headers: z.record(z.string()) }).parse(data);
}

/** A readable URL for an uploaded asset, so the canvas can show the artwork under the fields. */
export async function idCardAssetUrl(accessToken: string, eventId: string, key: string) {
  const { data } = await api.get(`/v1/events/${eventId}/badges/template/asset-url`, {
    ...authHeaders(accessToken),
    params: { key }
  });
  return z.object({ url: z.string() }).parse(data).url;
}

/**
 * Issues real ID cards — creates the id_cards rows, allocates card numbers and verify codes, and stores
 * the rendered artefacts. Idempotent per (event, holder): re-running keeps an existing card's number.
 */
export async function generateIdCards(
  accessToken: string,
  eventId: string,
  body: { sizeKey: string; kinds: ("attendee" | "staff")[]; userIds?: string[] }
) {
  const { data } = await api.post(`/v1/events/${eventId}/badges/generate`, body, authHeaders(accessToken));
  return z.object({ issued: z.number(), regenerated: z.number() }).parse(data);
}

/**
 * The print run. Returns the PDF bytes rather than a URL: the endpoint is authenticated, so a bare
 * href would 401 — the browser does not attach the bearer token to a navigation.
 */
export async function downloadBadgeSheet(
  accessToken: string,
  eventId: string,
  body: { sizeKey: string; kinds: ("attendee" | "staff")[]; userIds?: string[] }
): Promise<Blob> {
  const { data } = await api.post(`/v1/events/${eventId}/badges/sheet`, body, {
    ...authHeaders(accessToken),
    responseType: "blob"
  });
  return data as Blob;
}

/**
 * Revokes an issued badge (D-386).
 *
 * **Marks the card, does not close a door.** The verification lookup stops reporting it current, but an
 * attendee's entry credential is their ticket and a staff member's is their assignment — stopping someone
 * entering means voiding one of those. The UI says so at the point of the click, because the opposite
 * assumption is the dangerous one.
 */
export async function revokeBadge(
  accessToken: string,
  eventId: string,
  recipientUserId: string,
  reason?: string
) {
  const { data } = await api.post(
    `/v1/events/${eventId}/badges/${recipientUserId}/revoke`,
    { reason: reason ?? null },
    authHeaders(accessToken)
  );
  return issuedCardSchema.parse(data);
}

/**
 * One person's badge as its own PDF (D-385).
 *
 * The route has existed since D-362 but nothing called it, so a reprint for the one person whose
 * lanyard was lost meant regenerating the entire sheet. Blob rather than an href for the same reason as
 * the sheet: the endpoint is authenticated and a navigation carries no bearer token.
 */
export async function downloadOneBadge(
  accessToken: string,
  eventId: string,
  recipientUserId: string,
  sizeKey: string
): Promise<Blob> {
  const { data } = await api.get(`/v1/events/${eventId}/badges/${recipientUserId}.pdf`, {
    ...authHeaders(accessToken),
    params: { size: sizeKey },
    responseType: "blob"
  });
  return data as Blob;
}
