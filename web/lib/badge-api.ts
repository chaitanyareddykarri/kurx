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

export const badgeRecipientSchema = z.object({
  user_id: z.string(),
  name: z.string(),
  kind: z.enum(["attendee", "staff"]),
  subtitle: z.string().nullable(),
  access_level: z.string().nullable(),
  // Whether a photo will print. The QR payload is deliberately NOT served — it is a working credential,
  // and a console listing everyone's scannable code would hand out badges as JSON.
  has_photo: z.boolean()
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
