import axios from "axios";
import { problemMessage } from "@kurx/ui";
import { cache } from "react";
import { z } from "zod";
import { siteConfig } from "@/lib/site";

export const api = axios.create({
  baseURL: siteConfig.apiBaseUrl,
  headers: { "Content-Type": "application/json" }
});

/// Backend errors are RFC7807 ProblemDetails whose `error` field is a machine-readable CODE, not a
/// sentence (docs/api/README.md). Returning it verbatim showed users the literal string
/// `invalid_code` when they mistyped an OTP; `problemMessage` maps it to the same words mobile has
/// always used for the same code.
export function apiErrorMessage(err: unknown): string {
  if (axios.isAxiosError(err)) {
    const data = err.response?.data as { error?: string; detail?: string } | undefined;
    return problemMessage({ code: data?.error, detail: data?.detail, status: err.response?.status });
  }
  return err instanceof Error ? err.message : "Something went wrong.";
}

/// HTTP status of a failed API call, or undefined if not an HTTP error. Used to tell a 403
/// (e.g. not a platform reviewer) apart from a generic failure.
export function apiErrorStatus(err: unknown): number | undefined {
  return axios.isAxiosError(err) ? err.response?.status : undefined;
}

/// The machine-readable `error` code itself, for the cases where a caller needs to say something more
/// specific than `problemMessage` can — "that file is over 10 MB" rather than a generic bad-request
/// sentence. Prefer `apiErrorMessage`; reach for this only when the extra words genuinely help.
export function apiErrorCode(err: unknown): string | undefined {
  return axios.isAxiosError(err)
    ? (err.response?.data as { error?: string } | undefined)?.error
    : undefined;
}

export const tokenResponseSchema = z.object({
  access_token: z.string(),
  access_expires_at: z.string(),
  refresh_token: z.string(),
  refresh_expires_at: z.string(),
  user_id: z.string(),
  is_new_user: z.boolean()
});

export type TokenResponse = z.infer<typeof tokenResponseSchema>;

export const SECTION_TIERS = ["public", "connections", "event_participants", "only_me"] as const;
export type SectionTier = (typeof SECTION_TIERS)[number];

/** The sections the privacy screen edits, in render order (D-221). */
/// Must match the backend `ProfileSection` enum exactly (D-229/H3). A section missing here is a
/// section the user cannot govern — `metrics` and `contributions` were published with no control at
/// all until this list was completed. A backend test asserts the two stay in step.
export const PROFILE_SECTIONS = [
  "profile", "events", "attended", "certificates", "achievements", "organizations", "timeline",
  "network", "metrics", "contributions"
] as const;
export type ProfileSectionKey = (typeof PROFILE_SECTIONS)[number];

export const privacyFlagsSchema = z.object({
  profile_public: z.boolean(),
  show_attended: z.boolean(),
  show_certificates: z.boolean(),
  show_allies: z.boolean(),
  // Four-tier per-section visibility (D-221). Optional so a client pinned to an older backend still
  // parses; `.catchall` keeps unknown sections from failing the parse when the server adds one.
  sections: z.record(z.string()).nullable().optional()
});
export type PrivacyFlags = z.infer<typeof privacyFlagsSchema>;

export const meSchema = z.object({
  id: z.string(),
  phone: z.string(),
  name: z.string(),
  username: z.string().nullable().optional(),
  email: z.string().nullable().optional(),
  /// Proven by an emailed one-time code, not merely present. Optional so a client pinned to an
  /// older backend still parses.
  email_verified: z.boolean().optional().default(false),
  // The caller's own editable display fields (D-219). Optional so a client pinned to an older backend
  // still parses; the settings form prefills from these instead of from the public profile, which is
  // what previously wiped them whenever that public read failed.
  headline: z.string().nullable().optional(),
  bio: z.string().nullable().optional(),
  education_json: z.string().nullable().optional(),
  skills: z.array(z.string()).nullable().optional(),
  // Phase 2 (About). Null = not stated, never "none" — the section is omitted rather than
  // rendering an empty list, which would assert an absence the person never declared.
  languages: z.array(z.string()).nullable().optional(),
  interests: z.array(z.string()).nullable().optional(),
  links_json: z.string().nullable().optional(),
  avatar_key: z.string().nullable().optional(),
  cover_key: z.string().nullable().optional(),
  /// Presigned, fetchable companions to the two keys above (D-302). The KEY is what a save round-trips;
  /// the URL is the only one of the pair a client can render — a bare key resolves against the web
  /// origin and 404s, which is why every avatar showed initials.
  avatar_url: z.string().nullable().optional(),
  cover_url: z.string().nullable().optional(),
  privacy: privacyFlagsSchema.optional(),
  /// Self-declared date of birth, date-only `YYYY-MM-DD` (D-311).
  date_of_birth: z.string().nullable().optional(),
  /// When the account was created — full UTC ISO-8601, the owner's own view of it. Optional so a
  /// client parsed against an older backend still validates.
  created_at: z.string().optional(),
  needs_onboarding: z.boolean(),
  is_platform_reviewer: z.boolean().optional().default(false),
  // Backend /v1/me returns live platform roles (D-040); parsed here for the dev banner (D-125).
  platform_roles: z.array(z.string()).optional(),
  /// Raw KYC state (M3) — the aggregate level/status, not the masked components.
  identity: z.object({ level: z.string(), status: z.string() }).optional(),
  /// Derived user trust (M7). Read here rather than from an org's capability contract because paid
  /// hosting is a property of the *person*, and the create-event wizard now runs before any
  /// organization is chosen (D-267).
  ///
  /// **This is nested on the wire and used to be declared flat.** `/v1/me` has always answered
  /// `trust: { can_organize_paid, … }`, while this schema declared a top-level `can_organize_paid`
  /// with `.optional().default(false)` — so the key was never present, zod filled the default, and
  /// `me.can_organize_paid` was **false for every user on every request**, including fully verified
  /// organisers. `/host/events/new` gates its paid step on exactly that value, so paid hosting read
  /// as unavailable to everyone. Same failure class as D-245 and D-292: a hand-maintained client
  /// model disagreeing with the wire, silently, because the default was a plausible value.
  trust: z.object({
    level: z.string(),
    can_organize_free: z.boolean(),
    can_organize_paid: z.boolean(),
    can_receive_payout: z.boolean(),
    identity_verified: z.boolean(),
    bank_verified: z.boolean(),
    /// D-307 — may this person create a PUBLIC event, free or paid. Distinct from `can_organize_paid`
    /// (which answers "may they take money") even though the predicates are identical today.
    /// Optional-and-defaulted so a response from a backend predating it still parses, and defaulted to
    /// **false** because the closed position is the safe one for a gate.
    can_create_public_event: z.boolean().optional().default(false),
    /// Constant true server-side: a Private event can never be Listed, take payment, or reach a
    /// discovery surface. Defaulted true so an older backend does not accidentally block Private.
    can_create_private_event: z.boolean().optional().default(true),
    /// D-353/D-352 — whether a PUBLIC event must name a verified organization. True in Production,
    /// always; false only under the dev bypass, where no admin-approved organization exists to name.
    /// Defaulted **true**, the closed position: a gate that stops demanding a requirement because a
    /// field went missing is the wrong failure.
    requires_representation: z.boolean().optional().default(true)
  }).optional()
}).transform((me) => ({
  ...me,
  /// Flattened for the existing call sites, which read `me.can_organize_paid`. Derived from the
  /// nested object rather than re-declared, so the two can never disagree again.
  can_organize_paid: me.trust?.can_organize_paid ?? false,
  can_receive_payout: me.trust?.can_receive_payout ?? false,
  identity_verified: me.trust?.identity_verified ?? false,
  bank_verified: me.trust?.bank_verified ?? false,
  /// Closed position on absence: a missing capability must never read as permission.
  can_create_public_event: me.trust?.can_create_public_event ?? false,
  can_create_private_event: me.trust?.can_create_private_event ?? true,
  requires_representation: me.trust?.requires_representation ?? true
}));

export type Me = z.infer<typeof meSchema>;

/// An organization the caller may represent (`GET /v1/me/representations`, D-268). Representing
/// yourself is not in this list — it is not an organization.
export const representationSchema = z.object({
  organization_id: z.string(),
  name: z.string(),
  slug: z.string(),
  logo_key: z.string().nullable(),
  /// The caller's authority to act for this organization — not a role over events.
  authority: z.string(),
  /// The ORGANIZATION's registry status (D-350), distinct from `authority`. A staged representation
  /// request is a real PendingReview row in this list; only a verified organization may be represented
  /// by a paid event, and the server refuses the rest at submit-for-review.
  is_verified: z.boolean(),
  /// D-352 — the CAPABILITY the client gates on; `is_verified` is the FACT it displays. They differ only
  /// under the dev bypass, which opens the gate without forging the status. Defaulted so an older server
  /// that omits it falls back to the fact, which is the closed position in Production.
  can_back_paid_event: z.boolean().optional()
});

export type Representation = z.infer<typeof representationSchema>;

export async function requestOtp(phone: string) {
  await api.post("/v1/auth/otp/request", { phone });
}

export async function verifyOtp(phone: string, code: string) {
  const { data } = await api.post("/v1/auth/otp/verify", { phone, code });
  return tokenResponseSchema.parse(data);
}

export async function refreshToken(refreshTokenValue: string) {
  const { data } = await api.post("/v1/auth/refresh", { refreshToken: refreshTokenValue });
  return tokenResponseSchema.parse(data);
}

export async function logout(refreshTokenValue: string) {
  await api.post("/v1/auth/logout", { refreshToken: refreshTokenValue });
}

export async function getMe(accessToken: string) {
  const { data } = await api.get("/v1/me", {
    headers: { Authorization: `Bearer ${accessToken}` }
  });
  return meSchema.parse(data);
}

export const usernameAvailabilitySchema = z.object({
  username: z.string(),
  status: z.enum(["available", "unavailable", "reserved", "invalid"])
});
export type UsernameAvailability = z.infer<typeof usernameAvailabilitySchema>;

export async function checkUsernameAvailability(username: string) {
  const { data } = await api.get("/v1/usernames/availability", { params: { username } });
  return usernameAvailabilitySchema.parse(data);
}

export async function updateProfile(
  accessToken: string,
  body: {
    name?: string; username?: string; headline?: string; bio?: string; skills?: string[];
    languages?: string[]; interests?: string[]; educationJson?: string;
    linksJson?: string; avatarKey?: string; coverKey?: string;
    /// `YYYY-MM-DD`, date-only (D-311). Required to finish onboarding — omitting it leaves
    /// `complete_profile` outstanding, which loops the registration wizard back to this step.
    dateOfBirth?: string;
  }
) {
  const { data } = await api.patch("/v1/me/profile", body, {
    headers: { Authorization: `Bearer ${accessToken}` }
  });
  return data as { id: string; name: string; username: string | null };
}

/// Partial update of the caller's visibility settings (D-219, extended per-section in D-221).
/// Omitted flags and sections are left unchanged; a named section overrides the boolean covering it.
export async function updatePrivacy(
  accessToken: string,
  body: Partial<Record<"profilePublic" | "showAttended" | "showCertificates" | "showAllies", boolean>> & {
    sections?: Record<string, SectionTier>;
  }
) {
  const { data } = await api.patch("/v1/me/privacy", body, {
    headers: { Authorization: `Bearer ${accessToken}` }
  });
  return privacyFlagsSchema.parse(data);
}

export const presignedUploadSchema = z.object({
  key: z.string(),
  url: z.string(),
  headers: z.record(z.string()).nullable().optional()
});

/// Step 1 of the two-step profile-image upload (D-219): presign, PUT the bytes to `url`, then persist
/// the returned `key` via updateProfile. Mirrors how event media confirms a key after upload.
export async function presignProfileImage(
  accessToken: string,
  body: { slot: "avatar" | "cover"; contentType: string; maxBytes: number }
) {
  const { data } = await api.post("/v1/me/profile-image/presign", body, {
    headers: { Authorization: `Bearer ${accessToken}` }
  });
  return presignedUploadSchema.parse(data);
}

/// Hide or show a single connection without turning off show_allies entirely (D-219).
export async function setAllyVisibility(accessToken: string, connectionId: string, visibility: "public" | "hidden") {
  const { data } = await api.patch(`/v1/allies/${connectionId}/visibility`, { visibility }, {
    headers: { Authorization: `Bearer ${accessToken}` }
  });
  return data as { id: string; visibility: string };
}

// ── Registration ceremony (Phase 2D) ────────────────────────────────────────
// Registration begins with the phone-OTP login above (which mints the session); these authenticated
// calls then drive the remaining steps. `remaining` is an ordered subset of the step keys below.

export const registrationStatusSchema = z.object({
  has_password: z.boolean(),
  email: z.string().nullable(),
  email_verified: z.boolean(),
  phone: z.string().nullable(),
  phone_verified: z.boolean(),
  has_trusted_device: z.boolean(),
  needs_onboarding: z.boolean(),
  // "verify_email" | "create_password" | "complete_profile" | "enroll_device", in ceremony order.
  remaining: z.array(z.string())
});
export type RegistrationStatus = z.infer<typeof registrationStatusSchema>;

export async function getRegistrationStatus(accessToken: string) {
  const { data } = await api.get("/v1/auth/registration/status", {
    headers: { Authorization: `Bearer ${accessToken}` }
  });
  return registrationStatusSchema.parse(data);
}

/** Sends a verification code to `email`. Rejects a malformed address or one owned by another account. */
export async function startEmailVerification(accessToken: string, email: string) {
  await api.post(
    "/v1/auth/email/verify/start",
    { email },
    { headers: { Authorization: `Bearer ${accessToken}` } }
  );
}

/** Verifies the emailed code; on success the backend sets and marks the email verified. */
export async function completeEmailVerification(accessToken: string, email: string, code: string) {
  await api.post(
    "/v1/auth/email/verify/complete",
    { email, code },
    { headers: { Authorization: `Bearer ${accessToken}` } }
  );
}

// Phone change (E.164): request an OTP to the new number, then confirm it with the code.
export async function requestPhoneOtp(phone: string) {
  await api.post("/v1/auth/otp/request", { phone });
}
export async function changePhone(accessToken: string, phone: string, code: string) {
  await api.post("/v1/me/phone/verify", { phone, code }, { headers: { Authorization: `Bearer ${accessToken}` } });
}

/// The organizations the caller may represent (D-268). Named for what it serves — the Representing step
/// and the Representing surfaces — not for the table behind it. It was `GET /v1/orgs`, which read as
/// organization management and returned the self-representation row alongside real institutions.
export async function listMyRepresentations(accessToken: string) {
  const { data } = await api.get("/v1/me/representations", {
    headers: { Authorization: `Bearer ${accessToken}` }
  });
  return z.array(representationSchema).parse(data);
}

/** Exported for the sibling API modules (chat, posts, account) rather than each standing up a second
 *  axios instance — one client, one auth convention. */
export function authHeaders(accessToken: string) {
  return { headers: { Authorization: `Bearer ${accessToken}` } };
}

// ── Group registration (OrderEndpoints /v1/groups): the caller's group memberships, join-by-code,
// and per-member payment readiness (a member without a ticket_id hasn't paid yet). ──
export const groupMemberSchema = z.object({
  id: z.string(),
  user_id: z.string().nullable(),
  name: z.string(),
  phone: z.string(),
  ticket_id: z.string().nullable(),
  answers_json: z.string().nullable(),
  joined_at: z.string().nullable(),
  username: z.string().nullable(),
  avatar_key: z.string().nullable(),
  avatar_url: z.string().nullable().optional(),
});

export const groupSchema = z.object({
  id: z.string(),
  event_id: z.string(),
  ticket_type_id: z.string(),
  group_number: z.number(),
  display_name: z.string().nullable(),
  join_code: z.string(),
  leader_user_id: z.string(),
  capacity: z.number(),
  members: z.array(groupMemberSchema)
});
export type Group = z.infer<typeof groupSchema>;

export async function myGroups(accessToken: string) {
  const { data } = await api.get("/v1/groups", authHeaders(accessToken));
  return z.array(groupSchema).parse(data);
}

export async function getGroup(accessToken: string, groupId: string) {
  const { data } = await api.get(`/v1/groups/${groupId}`, authHeaders(accessToken));
  return groupSchema.parse(data);
}

export async function joinGroup(accessToken: string, body: { joinCode: string; displayName?: string }) {
  await api.post("/v1/groups/join", { joinCode: body.joinCode, displayName: body.displayName }, authHeaders(accessToken));
}

// ── In-app notifications (MeNotificationEndpoints, D-064). ──
export const notificationSchema = z.object({
  id: z.string(),
  kind: z.string(),
  title: z.string(),
  body: z.string(),
  data_json: z.string().nullable(),
  read_at: z.string().nullable(),
  created_at: z.string()
});
export const notificationsResponseSchema = z.object({
  items: z.array(notificationSchema),
  unread_count: z.number()
});
export type Notification = z.infer<typeof notificationSchema>;

export async function listNotifications(accessToken: string) {
  const { data } = await api.get("/v1/me/notifications", authHeaders(accessToken));
  return notificationsResponseSchema.parse(data);
}

export async function markNotificationRead(accessToken: string, id: string) {
  await api.post(`/v1/me/notifications/${id}/read`, {}, authHeaders(accessToken));
}

export async function markAllNotificationsRead(accessToken: string) {
  await api.post("/v1/me/notifications/read-all", {}, authHeaders(accessToken));
}

export const categorySchema = z.object({
  id: z.string(),
  parent_id: z.string().nullable(),
  level: z.string(),
  name: z.string(),
  slug: z.string(),
  sort: z.number(),
  is_visible: z.boolean(),
  /// "Public" | "Private" on a Type node; absent on Audience/Category nodes and on Types no admin has
  /// classified. **Null means Public** — the same fallback `ResolveArchetypeAsync` applies server-side.
  /// The Create-Event gate filters Types by this; it never overrides the derivation (D-266 M1 / D-305).
  /// Optional so a response from a backend predating it still parses (the D-292 lesson).
  product_class: z.string().nullable().optional()
});
export type Category = z.infer<typeof categorySchema>;

/// Category/subcategory-specific field metadata (backs the metadata-driven Create-Event form).
/// `payload` is a JSON string: {registration_mode, group_min/max, pricing_unit, fields:[...]}.
export const fieldPresetSchema = z.object({
  id: z.string(),
  slug: z.string(),
  name: z.string(),
  payload: z.string()
});
export type FieldPreset = z.infer<typeof fieldPresetSchema>;

export const eventVenueSchema = z.object({
  venue_id: z.string().nullable(),
  name: z.string(),
  address: z.string(),
  city: z.string(),
  lat: z.number().nullable(),
  lng: z.number().nullable(),
  google_maps_url: z.string().nullable()
});

export const eventMediaSchema = z.object({
  id: z.string(),
  kind: z.string(),
  key: z.string(),
  caption: z.string(),
  sort: z.number(),
  /// Presigned GET. `key` is a storage key and is NOT fetchable — never use it as a src (D-302).
  url: z.string().nullable().optional()
});
export type EventMediaView = z.infer<typeof eventMediaSchema>;

export const eventDetailSchema = z.object({
  id: z.string(),
  // The organization this event REPRESENTS — never its owner, which is the user in created_by (D-268).
  // `org_id` is the deprecated D-273a alias, kept optional so this schema parses both the old and new
  // server shapes during the rollout. Read representing_org_id.
  representing_org_id: z.string(),
  org_id: z.string().optional(),
  parent_event_id: z.string().nullable(),
  title: z.string(),
  slug: z.string(),
  subtitle: z.string(),
  description: z.string(),
  category_id: z.string(),
  type_id: z.string().nullable(),
  audience_level_id: z.string().nullable(),
  template_id: z.string().nullable(),
  tags: z.array(z.string()),
  venue: eventVenueSchema,
  starts_at: z.string(),
  ends_at: z.string(),
  timezone: z.string(),
  capacity: z.number().nullable(),
  visibility: z.string(),
  status: z.string(),
  language: z.string(),
  contact_email: z.string(),
  contact_phone: z.string(),
  website: z.string(),
  social_links_json: z.string().nullable(),
  banner_key: z.string().nullable(),
  is_featured: z.boolean(),
  view_count: z.number(),
  media: z.array(eventMediaSchema),
  created_at: z.string(),
  published_at: z.string().nullable(),
  updated_at: z.string(),
  // D-302 — all optional so a response predating them still parses (the D-292 lesson).
  banner_url: z.string().nullable().optional(),
  event_mode: z.string().nullable().optional(),
  online_url: z.string().nullable().optional(),
  settlement_currency: z.string().nullable().optional(),
  /// The organization the host represents, named server-side. Null = self-represented (D-268), in which
  /// case there is no institution to show and the creator stands alone.
  representing: z
    .object({
      org_id: z.string(),
      name: z.string(),
      slug: z.string(),
      logo_key: z.string().nullable().optional(),
      logo_url: z.string().nullable().optional(),
      is_verified: z.boolean()
    })
    .nullable()
    .optional(),
  content: z
    .object({
      tagline: z.string().nullable().optional(),
      short_description: z.string().nullable().optional(),
      rules: z.string().nullable().optional(),
      faq_json: z.string().nullable().optional(),
      logo_url: z.string().nullable().optional(),
      thumbnail_url: z.string().nullable().optional(),
      promo_video_url: z.string().nullable().optional()
    })
    .nullable()
    .optional(),
  legal: z
    .object({
      terms_url: z.string().nullable().optional(),
      code_of_conduct: z.string().nullable().optional(),
      refund_policy: z.string().nullable().optional(),
      cancellation_policy: z.string().nullable().optional()
    })
    .nullable()
    .optional(),
  schedule: z
    .object({
      registration_opens_at: z.string().nullable().optional(),
      registration_closes_at: z.string().nullable().optional()
    })
    .nullable()
    .optional(),
  eligibility: z
    .object({
      min_age: z.number().nullable().optional(),
      max_age: z.number().nullable().optional(),
      gender_restriction: z.string().nullable().optional()
    })
    .nullable()
    .optional()
});
export type EventDetail = z.infer<typeof eventDetailSchema>;

/// The PUBLIC on-sale ticket listing an attendee sees — distinct from the org-scoped management route
/// above. It had no web caller at all, which is why the booking form invented its own ticket types.
export const publicTicketTypeSchema = z.object({
  id: z.string(),
  name: z.string(),
  description: z.string().nullable().optional(),
  price_paise: z.number(),
  currency: z.string().nullable().optional(),
  quantity: z.number().nullable().optional(),
  sold: z.number().nullable().optional(),
  per_user_limit: z.number().nullable().optional(),
  registration_mode: z.string().nullable().optional(),
  group_min: z.number().nullable().optional(),
  group_max: z.number().nullable().optional(),
  is_competition: z.boolean().nullable().optional()
});
export type PublicTicketType = z.infer<typeof publicTicketTypeSchema>;

export async function listPublicTicketTypes(eventId: string) {
  const { data } = await api.get(`/v1/events/${eventId}/ticket-types`);
  return z.array(publicTicketTypeSchema).parse(data);
}

export const createdOrderSchema = z.object({
  id: z.string(),
  status: z.string(),
  amount_paise: z.number(),
  currency: z.string().nullable().optional(),
  /// Present only for a PAID order — the gateway order the client must pay against. A checkout opened
  /// without this is not a payment, which is exactly what the previous booking form did.
  razorpay_order_id: z.string().nullable().optional(),
  tickets: z.array(z.object({ id: z.string(), code: z.string(), state: z.string() })).default([])
});
export type CreatedOrder = z.infer<typeof createdOrderSchema>;

/// Creates the REAL order. `idempotencyKey` is per-caller (D-049 §17.1): a double-submitted form returns
/// the original order rather than minting a second one.
export async function createEventOrder(
  accessToken: string,
  eventId: string,
  body: { ticketTypeId: string; quantity?: number; groupSize?: number; displayName?: string; answers?: Record<string, unknown>; idempotencyKey?: string }
) {
  const { data } = await api.post(`/v1/events/${eventId}/orders`, body, authHeaders(accessToken));
  return createdOrderSchema.parse(data);
}

export const eventSummarySchema = z.object({
  id: z.string(),
  representing_org_id: z.string(),
  org_id: z.string().optional(),          // DEPRECATED (D-273a) — see eventDetailSchema
  parent_event_id: z.string().nullable(),
  title: z.string(),
  slug: z.string(),
  subtitle: z.string(),
  banner_key: z.string().nullable(),
  starts_at: z.string(),
  ends_at: z.string(),
  status: z.string(),
  visibility: z.string(),
  venue_name: z.string(),
  city: z.string(),
  // D-302. All optional so a response from a backend that predates them still parses — the D-292 lesson,
  // where a required field the API stopped sending made every parse throw and rendered an empty page.
  // `banner_url` is presigned; `banner_key` beside it is NOT fetchable and must never be used as a src.
  banner_url: z.string().nullable().optional(),
  event_mode: z.string().nullable().optional(),
  category_name: z.string().nullable().optional(),
  // null means no ticket type exists yet — render "Registration not open", never "Free".
  price_from_paise: z.number().nullable().optional(),
  currency: z.string().nullable().optional(),
  is_featured: z.boolean().nullable().optional()
});

/// Enriched org-scoped row for the organizer events-management table (distinct from the public summary).
export const orgEventRowSchema = z.object({
  id: z.string(),
  title: z.string(),
  slug: z.string(),
  status: z.string(),
  visibility: z.string(),
  category_name: z.string().nullable(),
  venue_name: z.string(),
  city: z.string(),
  capacity: z.number().nullable(),
  is_paid: z.boolean(),
  tickets_sold: z.number(),
  checked_in: z.number(),
  revenue_paise: z.number(),
  starts_at: z.string(),
  updated_at: z.string()
});
export type OrgEventRow = z.infer<typeof orgEventRowSchema>;

/// One of the caller's own events (`GET /v1/me/events`, D-267). The representing organization travels on
/// the row so a card can say "representing X" — it is never a grouping key: Workspace lists events.
/// Who an event represents (D-268). `kind: "personal"` means the owner represents themselves and carries
/// no organization at all. Representation never denotes ownership — the owner is always the user.
export const eventRepresentationSchema = z.object({
  kind: z.enum(["personal", "organization"]),
  organization_id: z.string().nullable(),
  organization_name: z.string().nullable(),
  verified: z.boolean().default(false)
});
export type EventRepresentation = z.infer<typeof eventRepresentationSchema>;

export const myEventRowSchema = orgEventRowSchema.extend({
  representation: eventRepresentationSchema
});
export type MyEventRow = z.infer<typeof myEventRowSchema>;
export type EventSummary = z.infer<typeof eventSummarySchema>;

export const listCategories = cache(async () => {
  const { data } = await api.get("/v1/categories", { params: { level: "Category" } });
  return z.array(categorySchema).parse(data);
});

/// V3 §15 (Phase 16) Kind registry — public, like /v1/categories. Powers the Kind filter/quick-browse rail.
export const kindSchema = z.object({
  slug: z.string(),
  name: z.string(),
  group_slug: z.string(),
  group_name: z.string(),
  sort: z.number()
});
export type Kind = z.infer<typeof kindSchema>;

export const listKinds = cache(async () => {
  const { data } = await api.get("/v1/kinds");
  return z.array(kindSchema).parse(data);
});

export async function listSubcategories() {
  const { data } = await api.get("/v1/categories", { params: { level: "Type" } });
  return z.array(categorySchema).parse(data);
}

export async function listFieldPresets(slug?: string) {
  const { data } = await api.get("/v1/field-presets", { params: slug ? { slug } : {} });
  return z.array(fieldPresetSchema).parse(data);
}

/// Every event the caller hosts, across every organization they represent, in one flat list (D-267).
/// This replaced "list my orgs, then list each org's events" — the reason Workspace used to be an
/// organization browser before it was an event list.
export async function listMyEvents(accessToken: string, page = 1, pageSize = 50) {
  const { data } = await api.get("/v1/me/events", { ...authHeaders(accessToken), params: { page, pageSize } });
  return z.object({ items: z.array(myEventRowSchema), total: z.number() }).parse(data);
}

/// `RefundEndpoints.ToViewJson` (D-199).
export const refundSchema = z.object({
  id: z.string(),
  order_id: z.string(),
  event_id: z.string().nullable().optional(),
  org_id: z.string().nullable().optional(),
  amount_paise: z.number().default(0),
  currency: z.string().default("INR"),
  reason: z.string().nullable().optional(),
  status: z.string().default("pending"),
  razorpay_refund_id: z.string().nullable().optional(),
  created_at: z.string().nullable().optional()
});
export type Refund = z.infer<typeof refundSchema>;

/// The caller's own refunds, across every event.
export async function listMyRefunds(accessToken: string) {
  const { data } = await api.get("/v1/refunds", authHeaders(accessToken));
  return z.array(refundSchema).parse(data);
}

/// `EventRegistrationEndpoints.ToRegistrationJson` — the registration shadow of an Order/Ticket.
export const eventRegistrationSchema = z.object({
  id: z.string(),
  event_id: z.string(),
  ticket_type_id: z.string().nullable().optional(),
  subject_type: z.string().default("Person"),
  subject_id: z.string().nullable().optional(),
  order_id: z.string().nullable().optional(),
  state: z.string().default("pending"),
  admission_count: z.number().default(0),
  created_at: z.string().nullable().optional()
});
export type EventRegistration = z.infer<typeof eventRegistrationSchema>;

export async function listEventRegistrations(accessToken: string, orgId: string, eventId: string) {
  const { data } = await api.get(
    `/v1/orgs/${orgId}/events/${eventId}/registrations`,
    authHeaders(accessToken)
  );
  return z.array(eventRegistrationSchema).parse(data);
}

/// `ParticipantEndpoints.ToJson` — the caller's roles across every event. Carries no event title;
/// the Workspace hub shows the role and links by id rather than inventing a name it wasn't given.
export const participationSchema = z.object({
  id: z.string(),
  event_id: z.string(),
  role_slug: z.string(),
  role_class: z.string().nullable().optional(),
  custom_label: z.string().nullable().optional(),
  state: z.string().default("invited"),
  created_at: z.string().nullable().optional()
});
export type Participation = z.infer<typeof participationSchema>;

export async function listMyParticipations(accessToken: string) {
  const { data } = await api.get("/v1/me/participations", authHeaders(accessToken));
  return z.array(participationSchema).parse(data);
}

/// An event addressed by its own id (D-267). No organization in the path: the caller's authority over the
/// event authorizes it, which is also what lets a page resolve `representing_org_id` from the event
/// instead of from a selected-organization cookie.
export async function getEvent(accessToken: string, eventId: string) {
  const { data } = await api.get(`/v1/events/${eventId}`, authHeaders(accessToken));
  return eventDetailSchema.parse(data);
}

/// Create Event needs no organization to reach it (D-267). `representingOrgId` is chosen inside the
/// form; omitting it hosts the event personally.
export async function createEvent(accessToken: string, body: Record<string, unknown>) {
  const { data } = await api.post("/v1/events", body, authHeaders(accessToken));
  return eventDetailSchema.parse(data);
}

export async function updateOrgEvent(accessToken: string, orgId: string, eventId: string, body: Record<string, unknown>) {
  const { data } = await api.patch(`/v1/orgs/${orgId}/events/${eventId}`, body, authHeaders(accessToken));
  return eventDetailSchema.parse(data);
}

export async function transitionOrgEvent(accessToken: string, orgId: string, eventId: string, action: string) {
  const { data } = await api.post(`/v1/orgs/${orgId}/events/${eventId}/transition`, { action }, authHeaders(accessToken));
  return eventDetailSchema.parse(data);
}

export async function deleteOrgEvent(accessToken: string, orgId: string, eventId: string) {
  await api.delete(`/v1/orgs/${orgId}/events/${eventId}`, authHeaders(accessToken));
}

export async function cloneOrgEvent(accessToken: string, orgId: string, eventId: string) {
  const { data } = await api.post(`/v1/orgs/${orgId}/events/${eventId}/clone`, {}, authHeaders(accessToken));
  return eventDetailSchema.parse(data);
}

export const gateScanSchema = z.object({
  admitted: z.boolean(),
  is_duplicate: z.boolean(),
  message: z.string().optional(),
  first_checked_in_at: z.string().nullish(),
  first_checked_in_by: z.string().nullish()
});

export async function scanGate(accessToken: string, eventId: string, ticketCode: string) {
  const { data } = await api.post(`/v1/gate/${eventId}/scan`, { ticketCode }, authHeaders(accessToken));
  return gateScanSchema.parse(data);
}

// ── Event analytics (detailed) — sales/attendance/revenue/tickets/export ─────
//
// snake_case, like the rest of this file. These four types live in `Kurx.Application.Abstractions`
// and are returned straight out of `Results.Ok`, so `SnakeCaseResponseConverter` (D-259 addendum)
// renames every property on the way out — the converter matches by namespace, not by a name suffix.
// They were written camelCase and threw on every real parse, which took the whole analytics page
// down through its `Promise.all` (D-289). The committed contract in `docs/api/openapi.json` is the
// check: `AttendanceMetricView` publishes `attendance_rate`, `checked_in`, `total_tickets`.
export const salesPointSchema = z.object({ date: z.string(), ticket_count: z.number(), revenue_paise: z.number() });
export async function getEventSales(accessToken: string, orgId: string, eventId: string) {
  const { data } = await api.get(`/v1/orgs/${orgId}/events/${eventId}/analytics/sales`, authHeaders(accessToken));
  return z.array(salesPointSchema).parse(data);
}
export const attendanceSchema = z.object({ total_tickets: z.number(), checked_in: z.number(), attendance_rate: z.number() });
export async function getEventAttendance(accessToken: string, orgId: string, eventId: string) {
  const { data } = await api.get(`/v1/orgs/${orgId}/events/${eventId}/analytics/attendance`, authHeaders(accessToken));
  return attendanceSchema.parse(data);
}
export const ticketTypeRevenueSchema = z.object({ ticket_type_id: z.string(), name: z.string(), quantity_sold: z.number(), revenue_paise: z.number() });
export async function getEventRevenue(accessToken: string, orgId: string, eventId: string) {
  const { data } = await api.get(`/v1/orgs/${orgId}/events/${eventId}/analytics/revenue`, authHeaders(accessToken));
  return z.array(ticketTypeRevenueSchema).parse(data);
}
export const ticketAnalyticsSchema = z.object({ capacity: z.number(), sold: z.number(), remaining: z.number() });
export async function getEventTickets(accessToken: string, orgId: string, eventId: string) {
  const { data } = await api.get(`/v1/orgs/${orgId}/events/${eventId}/analytics/tickets`, authHeaders(accessToken));
  return ticketAnalyticsSchema.parse(data);
}
export async function exportEventAnalyticsCsv(accessToken: string, orgId: string, eventId: string) {
  const { data } = await api.get(`/v1/orgs/${orgId}/events/${eventId}/analytics/export`, { ...authHeaders(accessToken), responseType: "text" });
  return data as string;
}

// ── Sessions edit / delete / reorder ────────────────────────────────────────
export async function updateEventSession(accessToken: string, orgId: string, eventId: string, sessionId: string, body: Record<string, unknown>) {
  await api.patch(`/v1/orgs/${orgId}/events/${eventId}/sessions/${sessionId}`, body, authHeaders(accessToken));
}
export async function deleteEventSession(accessToken: string, orgId: string, eventId: string, sessionId: string) {
  await api.delete(`/v1/orgs/${orgId}/events/${eventId}/sessions/${sessionId}`, authHeaders(accessToken));
}
export async function reorderEventSessions(accessToken: string, orgId: string, eventId: string, sessionIds: string[]) {
  await api.post(`/v1/orgs/${orgId}/events/${eventId}/sessions/reorder`, { sessionIds }, authHeaders(accessToken));
}

// ── Speakers / Sponsors edit + unassign-from-event ──────────────────────────
export async function updateOrgSpeaker(accessToken: string, orgId: string, speakerId: string, body: Record<string, unknown>) {
  await api.patch(`/v1/orgs/${orgId}/speakers/${speakerId}`, body, authHeaders(accessToken));
}
export async function removeEventSpeaker(accessToken: string, orgId: string, eventId: string, speakerId: string) {
  await api.delete(`/v1/orgs/${orgId}/events/${eventId}/speakers/${speakerId}`, authHeaders(accessToken));
}
export async function updateOrgSponsor(accessToken: string, orgId: string, sponsorId: string, body: Record<string, unknown>) {
  await api.patch(`/v1/orgs/${orgId}/sponsors/${sponsorId}`, body, authHeaders(accessToken));
}
export async function removeEventSponsor(accessToken: string, orgId: string, eventId: string, sponsorId: string) {
  await api.delete(`/v1/orgs/${orgId}/events/${eventId}/sponsors/${sponsorId}`, authHeaders(accessToken));
}

// ── Media delete ────────────────────────────────────────────────────────────
export async function deleteEventMedia(accessToken: string, orgId: string, eventId: string, mediaId: string) {
  await api.delete(`/v1/orgs/${orgId}/events/${eventId}/media/${mediaId}`, authHeaders(accessToken));
}

// ── Form field edit ─────────────────────────────────────────────────────────
export async function updateTicketTypeField(accessToken: string, orgId: string, eventId: string, ticketTypeId: string, fieldId: string, body: Record<string, unknown>) {
  await api.patch(`/v1/orgs/${orgId}/events/${eventId}/ticket-types/${ticketTypeId}/fields/${fieldId}`, body, authHeaders(accessToken));
}

// ── Announcement edit ───────────────────────────────────────────────────────
export async function updateAnnouncement(accessToken: string, announcementId: string, body: Record<string, unknown>) {
  await api.patch(`/v1/announcements/${announcementId}`, body, authHeaders(accessToken));
}

// ── Invitations resend + CSV import ─────────────────────────────────────────
export async function resendInvitation(accessToken: string, invitationId: string) {
  await api.post(`/v1/invitations/${invitationId}/resend`, {}, authHeaders(accessToken));
}
export async function importInvitationsCsv(accessToken: string, eventId: string, file: File) {
  const fd = new FormData();
  fd.append("file", file);
  const { data } = await api.post(`/v1/events/${eventId}/invitations/import`, fd, {
    headers: { Authorization: `Bearer ${accessToken}` }
  });
  return data as { imported?: number };
}

// ── Venue edit / delete / images ────────────────────────────────────────────
export async function updateOrgVenue(accessToken: string, orgId: string, venueId: string, body: Record<string, unknown>) {
  await api.patch(`/v1/orgs/${orgId}/venues/${venueId}`, body, authHeaders(accessToken));
}
export async function deleteOrgVenue(accessToken: string, orgId: string, venueId: string) {
  await api.delete(`/v1/orgs/${orgId}/venues/${venueId}`, authHeaders(accessToken));
}
export async function addVenueImage(accessToken: string, orgId: string, venueId: string, key: string) {
  await api.post(`/v1/orgs/${orgId}/venues/${venueId}/images`, { key }, authHeaders(accessToken));
}
export async function deleteVenueImage(accessToken: string, orgId: string, venueId: string, imageId: string) {
  await api.delete(`/v1/orgs/${orgId}/venues/${venueId}/images/${imageId}`, authHeaders(accessToken));
}

export const venueSchema = z.object({
  id: z.string(),
  org_id: z.string(),
  name: z.string(),
  address: z.string(),
  city: z.string(),
  lat: z.number().nullable(),
  lng: z.number().nullable(),
  google_maps_url: z.string().nullable(),
  capacity: z.number().nullable(),
  has_parking: z.boolean(),
  is_accessible: z.boolean(),
  notes: z.string(),
  image_keys: z.array(z.string())
});
export type Venue = z.infer<typeof venueSchema>;

export async function listOrgVenues(accessToken: string, orgId: string) {
  const { data } = await api.get(`/v1/orgs/${orgId}/venues`, authHeaders(accessToken));
  return z.array(venueSchema).parse(data);
}

export async function createOrgVenue(accessToken: string, orgId: string, body: Record<string, unknown>) {
  const { data } = await api.post(`/v1/orgs/${orgId}/venues`, body, authHeaders(accessToken));
  return venueSchema.parse(data);
}

export const speakerSchema = z.object({
  id: z.string(),
  org_id: z.string(),
  name: z.string(),
  bio: z.string(),
  photo_key: z.string().nullable(),
  company: z.string(),
  role: z.string(),
  social_links_json: z.string().nullable(),
  // Null for the common case (curated content, no account) — a linked speaker can Connect/View Profile.
  user_id: z.string().nullable(),
  username: z.string().nullable(),
  avatar_key: z.string().nullable(),
  avatar_url: z.string().nullable().optional(),
});
export type Speaker = z.infer<typeof speakerSchema>;

export async function listOrgSpeakers(accessToken: string, orgId: string) {
  const { data } = await api.get(`/v1/orgs/${orgId}/speakers`, authHeaders(accessToken));
  return z.array(speakerSchema).parse(data);
}

export async function createOrgSpeaker(accessToken: string, orgId: string, body: Record<string, unknown>) {
  const { data } = await api.post(`/v1/orgs/${orgId}/speakers`, body, authHeaders(accessToken));
  return speakerSchema.parse(data);
}

export async function listEventSpeakers(accessToken: string, orgId: string, eventId: string) {
  const { data } = await api.get(`/v1/orgs/${orgId}/events/${eventId}/speakers`, authHeaders(accessToken));
  return z.array(speakerSchema).parse(data);
}

export async function assignEventSpeaker(accessToken: string, orgId: string, eventId: string, speakerId: string) {
  await api.post(`/v1/orgs/${orgId}/events/${eventId}/speakers`, { speakerId }, authHeaders(accessToken));
}

export const sponsorSchema = z.object({
  id: z.string(),
  org_id: z.string(),
  name: z.string(),
  logo_key: z.string().nullable(),
  website: z.string(),
  tier: z.string(),
  priority: z.number()
});
export type Sponsor = z.infer<typeof sponsorSchema>;

export async function listOrgSponsors(accessToken: string, orgId: string) {
  const { data } = await api.get(`/v1/orgs/${orgId}/sponsors`, authHeaders(accessToken));
  return z.array(sponsorSchema).parse(data);
}

export async function createOrgSponsor(accessToken: string, orgId: string, body: Record<string, unknown>) {
  const { data } = await api.post(`/v1/orgs/${orgId}/sponsors`, body, authHeaders(accessToken));
  return sponsorSchema.parse(data);
}

export async function listEventSponsors(accessToken: string, orgId: string, eventId: string) {
  const { data } = await api.get(`/v1/orgs/${orgId}/events/${eventId}/sponsors`, authHeaders(accessToken));
  return z.array(sponsorSchema).parse(data);
}

export async function assignEventSponsor(accessToken: string, orgId: string, eventId: string, sponsorId: string) {
  await api.post(`/v1/orgs/${orgId}/events/${eventId}/sponsors`, { sponsorId }, authHeaders(accessToken));
}

export const sessionSchema = z.object({
  id: z.string(),
  event_id: z.string(),
  title: z.string(),
  description: z.string(),
  kind: z.string(),
  starts_at: z.string(),
  ends_at: z.string(),
  sort: z.number(),
  speaker_ids: z.array(z.string())
});
export type EventSession = z.infer<typeof sessionSchema>;

export async function listEventSessions(accessToken: string, orgId: string, eventId: string) {
  const { data } = await api.get(`/v1/orgs/${orgId}/events/${eventId}/sessions`, authHeaders(accessToken));
  return z.array(sessionSchema).parse(data);
}

export async function createEventSession(accessToken: string, orgId: string, eventId: string, body: Record<string, unknown>) {
  const { data } = await api.post(`/v1/orgs/${orgId}/events/${eventId}/sessions`, body, authHeaders(accessToken));
  return sessionSchema.parse(data);
}

export async function presignEventMedia(accessToken: string, orgId: string, eventId: string, contentType: string, maxBytes: number) {
  const { data } = await api.post(`/v1/orgs/${orgId}/events/${eventId}/media/presign`,
    { contentType, maxBytes }, authHeaders(accessToken));
  return z.object({ key: z.string(), url: z.string(), headers: z.record(z.string()) }).parse(data);
}

export async function attachEventMedia(accessToken: string, orgId: string, eventId: string, kind: string, key: string, caption?: string) {
  await api.post(`/v1/orgs/${orgId}/events/${eventId}/media`, { kind, key, caption }, authHeaders(accessToken));
}

// ── Public API (no auth required) ───────────────────────────────────────────

export async function getFeaturedEvents(limit = 3) {
  const { data } = await api.get("/v1/events/featured", { params: { limit } });
  return z.array(eventSummarySchema).parse(data);
}

export async function getLatestEvents(limit = 9) {
  const { data } = await api.get("/v1/events/latest", { params: { limit } });
  return z.array(eventSummarySchema).parse(data);
}

export async function getUpcomingEvents(limit = 9) {
  const { data } = await api.get("/v1/events/upcoming", { params: { limit } });
  return z.array(eventSummarySchema).parse(data);
}

export async function getTrendingEvents(limit = 9) {
  const { data } = await api.get("/v1/events/trending", { params: { limit } });
  return z.array(eventSummarySchema).parse(data);
}

export const eventSearchResultSchema = z.object({
  items: z.array(eventSummarySchema),
  total: z.number()
});
export type EventSearchResult = z.infer<typeof eventSearchResultSchema>;

export type EventSearchParams = {
  q?: string;
  categoryId?: string;
  orgId?: string;
  city?: string;
  dateFrom?: string;
  dateTo?: string;
  sort?: string;
  page?: number;
  pageSize?: number;
  // V3 §15 (Phase 16) — additive discovery filters (D-184); all optional, omitting keeps prior behaviour.
  kind?: string;
  mode?: string;
  price?: string;
};

/// Event search is GET /v1/events (not /v1/events/search — that route does not exist).
export async function searchEvents(params: EventSearchParams = {}) {
  const { data } = await api.get("/v1/events", { params });
  return eventSearchResultSchema.parse(data);
}

export async function getPublicEvent(slug: string) {
  const { data } = await api.get(`/v1/events/${slug}`);
  return eventDetailSchema.parse(data);
}

export async function getRelatedEvents(slug: string, limit = 3) {
  const { data } = await api.get(`/v1/events/${slug}/related`, { params: { limit } });
  return z.array(eventSummarySchema).parse(data);
}

/// V3 §15 (Phase 16) eligibility-aware "events you can attend" feed (D-184). Auth required.
export async function getRecommendedEvents(accessToken: string, limit = 9) {
  const { data } = await api.get("/v1/events/for-you", { params: { limit }, ...authHeaders(accessToken) });
  return z.array(eventSummarySchema).parse(data);
}

export const profileOrgSchema = z.object({
  org_id: z.string(),
  org_name: z.string(),
  org_slug: z.string(),
  logo_key: z.string().nullable(),
  roles: z.array(z.string()),
  is_verified: z.boolean(),
  joined_at: z.string(),
  valid_until: z.string().nullable(),
  org_events_conducted: z.number(),
  org_certificates_count: z.number(),
  org_achievements_count: z.number(),
});
export type ProfileOrg = z.infer<typeof profileOrgSchema>;

export const achievementCardSchema = z.object({
  name: z.string(),
  description: z.string().nullable(),
  icon_key: z.string().nullable(),
  earned_at: z.string(),
  source: z.enum(["certificate", "badge"]),
  event_title: z.string().nullable(),
  event_slug: z.string().nullable(),
  org_name: z.string().nullable(),
});
export type AchievementCard = z.infer<typeof achievementCardSchema>;

export const publicProfileSchema = z.object({
  id: z.string(),
  name: z.string(),
  /// Month precision only (`"2026-08"`) — the public profile says how long someone has been here,
  /// never the instant they signed up. The owner's exact `created_at` lives on `/v1/me`.
  joined_at: z.string().nullable().optional(),
  username: z.string().nullable(),
  headline: z.string().nullable(),
  bio: z.string().nullable(),
  summary: z.string(),
  avatar_key: z.string().nullable(),
  cover_key: z.string().nullable(),
  /// Presigned companions (D-302) — see the note on `meSchema`. Optional so a client built against a
  /// backend that predates them still parses.
  avatar_url: z.string().nullable().optional(),
  cover_url: z.string().nullable().optional(),
  college: z.object({
    institute: z.string().nullable(),
    degree: z.string().nullable(),
    branch: z.string().nullable(),
  }).nullable(),
  // `links_json`, not `links`: the wire key is derived from PublicProfileView.LinksJson. It was
  // declared as `links` here and on Flutter, so it parsed as null on every profile and the social
  // links silently never rendered for anyone — the fourth instance of the D-245/D-292 family, and the
  // first one a contract-map entry now pins.
  links_json: z.string().nullable(),
  // `users.Skills` is a nullable array column passed straight through, so the wire shape for "added
  // none" is null. Parsed as a plain array it threw for every such user, turning the whole profile
  // page into a 500 rather than one empty section. Normalised to `[]` here so callers hold a
  // `string[]` and the empty case renders as absence — which is what it means (D-289).
  skills: z.array(z.string()).nullable().transform((v) => v ?? []),
  // Phase 2 (About). Optional so a client pinned to an older backend still parses; nullable
  // because "not stated" and "none" are different answers.
  languages: z.array(z.string()).nullable().optional(),
  interests: z.array(z.string()).nullable().optional(),
  // Every count nullable: null = hidden from this viewer, never zero (D-229/H2).
  stats: z.object({
    events_conducted: z.number().nullable(),
    events_attended: z.number().nullable(),
    certificates_count: z.number().nullable(),
    participations: z.number().nullable(),
    achievements: z.number().nullable(),
    ally_count: z.number().nullable(),
  }),
  verification: z.object({
    identity_verified: z.boolean(),
    verified_member: z.boolean(),
    organizer: z.boolean(),
    verified_certificates: z.number().nullable(),
    years_on_platform: z.number(),
    // D-221 additions — optional so a client pinned to an older backend still parses.
    phone_verified: z.boolean().optional(),
    email_verified: z.boolean().optional(),
    speaker_verified: z.boolean().optional(),
    community_verified: z.boolean().optional(),
  }),
  event_dna: z.array(z.object({ kind: z.string(), count: z.number() })),
  achievements: z.array(achievementCardSchema),
  identity_labels: z.array(z.string()),
  /// The derived headline (D-225) — the short form of `identity_labels`. Distinct from `headline`,
  /// which is the user's own self-declared line: one is proof, the other is a claim.
  derived_headline: z.string().optional().default(""),
  /// Per-field provenance (D-221): "verified" | "self_declared" | "derived". Drives the badge, so a
  /// client never has to infer a field's category from its name.
  _meta: z.record(z.string()).optional(),
  organizations: z.array(profileOrgSchema),
});
export type PublicProfile = z.infer<typeof publicProfileSchema>;

export const publicCertificateCardSchema = z.object({
  id: z.string(),
  event_title: z.string(),
  issued_at: z.string(),
  verify_code: z.string(),
  is_achievement: z.boolean(),
});
export type PublicCertificateCard = z.infer<typeof publicCertificateCardSchema>;

export const publicEventCardSchema = z.object({
  id: z.string(),
  title: z.string(),
  slug: z.string(),
  banner_key: z.string().nullable(),
  starts_at: z.string(),
  city: z.string(),
  org_name: z.string(),
  roles: z.array(z.string()),
  visibility: z.string(),
  certificate_verify_code: z.string().nullable(),
  is_achievement: z.boolean(),
});
export type PublicEventCard = z.infer<typeof publicEventCardSchema>;

export const timelineEntrySchema = z.object({
  kind: z.enum(["org_joined", "org_verified", "participation", "achievement", "certificate", "organized", "attended"]),
  title: z.string(),
  slug: z.string().nullable(),
  banner_key: z.string().nullable(),
  roles: z.array(z.string()),
  org_name: z.string().nullable(),
  city: z.string().nullable(),
  occurred_at: z.string(),
  verify_code: z.string().nullable(),
  is_first_event: z.boolean(),
});
export type TimelineEntry = z.infer<typeof timelineEntrySchema>;

export const allyProfileCardSchema = z.object({
  user_id: z.string(),
  name: z.string(),
  username: z.string().nullable(),
  avatar_key: z.string().nullable(),
  avatar_url: z.string().nullable().optional(),
  mutual_event_count: z.number(),
});
export type AllyProfileCard = z.infer<typeof allyProfileCardSchema>;

export const allyConnectionSchema = z.object({
  id: z.string(),
  other_user_id: z.string(),
  other_name: z.string(),
  other_username: z.string().nullable(),
  other_avatar_key: z.string().nullable(),
  other_avatar_url: z.string().nullable().optional(),
  status: z.enum(["Pending", "Accepted", "Declined", "Revoked"]),
  visibility: z.enum(["Public", "Hidden"]),
  requested_at: z.string(),
  responded_at: z.string().nullable(),
  first_shared_event_id: z.string().nullable(),
  first_shared_event_title: z.string().nullable(),
  first_shared_event_slug: z.string().nullable(),
});
export type AllyConnection = z.infer<typeof allyConnectionSchema>;

/// Viewer-aware read headers (D-229/C1). The public profile endpoints are anonymous-allowed, but a
/// signed-in viewer may be entitled to more under the Connections / EventParticipants tiers — so the
/// session token is attached whenever we have one. Omitting it (which this client did until D-229)
/// silently reduced every signed-in web viewer to the anonymous view, making two of the four privacy
/// tiers non-functional on web while working correctly on Flutter.
function viewerHeaders(accessToken?: string) {
  return accessToken ? { headers: { Authorization: `Bearer ${accessToken}` } } : {};
}

/// Wrapped in React `cache()` for the same reason `listCategories`/`listKinds` are: `generateMetadata`
/// and the page body both need the profile, so an uncached call fetched the most expensive read on the
/// page twice on every single profile view. `cache()` keys on the arguments, so the viewer-specific
/// variants (token present vs absent) stay correctly separate rather than sharing one entry.
export const getPublicProfile = cache(async (username: string, accessToken?: string) => {
  const { data } = await api.get(`/v1/public/users/${username}`, viewerHeaders(accessToken));
  return publicProfileSchema.parse(data);
});

export async function getPublicProfileCertificates(username: string, accessToken?: string) {
  const { data } = await api.get(`/v1/public/users/${username}/certificates`, viewerHeaders(accessToken));
  return z.array(publicCertificateCardSchema).parse(data);
}

export async function getPublicProfileEvents(username: string, type: "conducted" | "attended" = "conducted", accessToken?: string) {
  const { data } = await api.get(`/v1/public/users/${username}/events`, { params: { type }, ...viewerHeaders(accessToken) });
  return z.array(publicEventCardSchema).parse(data);
}

export async function getPublicProfileTimeline(username: string, page = 1, pageSize = 20, accessToken?: string) {
  const { data } = await api.get(`/v1/public/users/${username}/timeline`, { params: { page, pageSize }, ...viewerHeaders(accessToken) });
  return z.array(timelineEntrySchema).parse(data);
}

export async function getPublicProfileAllies(username: string, accessToken?: string) {
  const { data } = await api.get(`/v1/public/users/${username}/allies`, viewerHeaders(accessToken));
  return z.array(allyProfileCardSchema).parse(data);
}

// ── Verified sources wired in D-222 + the Journey (D-223) ───────────────────

export const journeyNodeSchema = z.object({
  tier: z.string(),
  first_attained_at: z.string(),
  occurrences: z.number(),
  source: z.string(),
  evidence: z.object({
    kind: z.string(),
    event_title: z.string().nullable(),
    event_slug: z.string().nullable(),
    detail: z.string().nullable(),
    org_name: z.string().nullable()
  })
});
export type JourneyNode = z.infer<typeof journeyNodeSchema>;

// `org_name` is nullable on all three: a self-represented event has no organization to name (D-268),
// and the server sends null rather than the host's own name. Requiring it here is the D-245 failure —
// one null and zod throws, taking the whole verified block down instead of one label.
export const competitionResultSchema = z.object({
  event_id: z.string(), event_title: z.string(), event_slug: z.string(),
  banner_key: z.string().nullable(), stage_name: z.string(),
  rank: z.number(), score: z.number().nullable(),
  occurred_at: z.string(), org_name: z.string().nullable()
});
export type CompetitionResult = z.infer<typeof competitionResultSchema>;

export const speakerSessionSchema = z.object({
  event_id: z.string(), event_title: z.string(), event_slug: z.string(),
  banner_key: z.string().nullable(), session_title: z.string(),
  starts_at: z.string(), ends_at: z.string(), org_name: z.string().nullable()
});
export type SpeakerSession = z.infer<typeof speakerSessionSchema>;

export const profileAssignmentSchema = z.object({
  event_id: z.string(), event_title: z.string(), event_slug: z.string(),
  banner_key: z.string().nullable(), role: z.string(), status: z.string(),
  completed_at: z.string().nullable(), starts_at: z.string(), org_name: z.string().nullable()
});
export type ProfileAssignment = z.infer<typeof profileAssignmentSchema>;

export const eventDnaTagSchema = z.object({ kind: z.string(), count: z.number() });

/// Profile metrics (D-225). **A null member means hidden from this viewer, never zero** — render "—"
/// for null. Showing 0 would turn a privacy choice into a statement about the person.
export const profileMetricsSchema = z.object({
  events_organized: z.number().nullable(),
  events_participated: z.number().nullable(),
  events_attended: z.number().nullable(),
  completion_rate: z.number().nullable(),
  assignments_accepted: z.number().nullable(),
  assignments_completed: z.number().nullable(),
  competitions_entered: z.number().nullable(),
  competitions_won: z.number().nullable(),
  speaker_sessions: z.number().nullable(),
  certificates: z.number().nullable(),
  achievement_certificates: z.number().nullable(),
  organizations: z.number().nullable(),
  verified_organizations: z.number().nullable(),
  ally_count: z.number().nullable(),
  cities: z.array(z.string()),
  event_dna: z.array(eventDnaTagSchema)
});
export type ProfileMetrics = z.infer<typeof profileMetricsSchema>;

/// Experience (D-225). The band and its counts always travel together — a band alone is an
/// unfalsifiable judgement, and the counts are what make it checkable.
export const experienceSummarySchema = z.object({
  band: z.string(),
  distinct_events: z.number(),
  events_organized: z.number(),
  events_participated: z.number(),
  events_attended: z.number(),
  leadership_events: z.number(),
  organizations: z.number(),
  verified_organizations: z.number(),
  assignments_completed: z.number(),
  speaker_sessions: z.number(),
  competitions_won: z.number(),
  years_active: z.number(),
  first_activity_at: z.string().nullable()
});
export type ExperienceSummary = z.infer<typeof experienceSummarySchema>;

export const contributionDaySchema = z.object({
  date: z.string(),
  count: z.number(),
  level: z.number()
});

/// The contributions heatmap (D-228). Only days *with* activity are returned — the client fills the
/// gaps when laying out the grid, so a year of zeroes never crosses the wire.
export const contributionsSchema = z.object({
  from: z.string(),
  to: z.string(),
  total: z.number(),
  days: z.array(contributionDaySchema)
});
export type Contributions = z.infer<typeof contributionsSchema>;

export async function getPublicProfileContributions(username: string, months = 12, accessToken?: string) {
  const { data } = await api.get(`/v1/public/users/${username}/contributions`, { params: { months }, ...viewerHeaders(accessToken) });
  return contributionsSchema.parse(data);
}

/// The resume is a PDF download, not JSON — the URL is handed to the browser rather than fetched,
/// so the file lands through the normal download path with the server's filename.
export function publicProfileResumeUrl(username: string) {
  return `${siteConfig.apiBaseUrl}/v1/public/users/${username}/resume`;
}

export async function getPublicProfileMetrics(username: string, accessToken?: string) {
  const { data } = await api.get(`/v1/public/users/${username}/metrics`, viewerHeaders(accessToken));
  return profileMetricsSchema.parse(data);
}

export async function getPublicProfileExperience(username: string, accessToken?: string) {
  const { data } = await api.get(`/v1/public/users/${username}/experience`, viewerHeaders(accessToken));
  return experienceSummarySchema.parse(data);
}

/// The Journey is unpaginated by design — at most one node per tier.
export async function getPublicProfileJourney(username: string, accessToken?: string) {
  const { data } = await api.get(`/v1/public/users/${username}/journey`, viewerHeaders(accessToken));
  return z.array(journeyNodeSchema).parse(data);
}

export async function getPublicProfileCompetitions(username: string, accessToken?: string) {
  const { data } = await api.get(`/v1/public/users/${username}/competitions`, viewerHeaders(accessToken));
  return z.array(competitionResultSchema).parse(data);
}

export async function getPublicProfileSessions(username: string, accessToken?: string) {
  const { data } = await api.get(`/v1/public/users/${username}/sessions`, viewerHeaders(accessToken));
  return z.array(speakerSessionSchema).parse(data);
}

export async function getPublicProfileAssignments(username: string, accessToken?: string) {
  const { data } = await api.get(`/v1/public/users/${username}/assignments`, viewerHeaders(accessToken));
  return z.array(profileAssignmentSchema).parse(data);
}

// --- Allies (D-201): mutual, explicitly-consented professional connections ---

export async function requestAlly(accessToken: string, targetUserId: string) {
  const { data } = await api.post("/v1/allies/requests", { targetUserId }, authHeaders(accessToken));
  return allyConnectionSchema.parse(data);
}
export async function acceptAllyRequest(accessToken: string, connectionId: string) {
  const { data } = await api.post(`/v1/allies/requests/${connectionId}/accept`, {}, authHeaders(accessToken));
  return allyConnectionSchema.parse(data);
}
export async function declineAllyRequest(accessToken: string, connectionId: string) {
  const { data } = await api.post(`/v1/allies/requests/${connectionId}/decline`, {}, authHeaders(accessToken));
  return allyConnectionSchema.parse(data);
}
export async function revokeAlly(accessToken: string, connectionId: string) {
  await api.delete(`/v1/allies/${connectionId}`, authHeaders(accessToken));
}
export async function listIncomingAllyRequests(accessToken: string) {
  const { data } = await api.get("/v1/allies/requests/incoming", authHeaders(accessToken));
  return z.array(allyConnectionSchema).parse(data);
}
export async function listOutgoingAllyRequests(accessToken: string) {
  const { data } = await api.get("/v1/allies/requests/outgoing", authHeaders(accessToken));
  return z.array(allyConnectionSchema).parse(data);
}
export async function listMyAllies(accessToken: string) {
  const { data } = await api.get("/v1/me/allies", authHeaders(accessToken));
  return z.array(allyConnectionSchema).parse(data);
}

export type AllyRelationStatus = "none" | "pending_outgoing" | "pending_incoming" | "accepted";

/** One call for a whole page of user ids — the batch primitive every person-list surface uses
 * instead of a request per card (no N+1). Returns "none" for any id the caller has no row for. */
export async function getAllyStatusBatch(accessToken: string, userIds: string[]): Promise<Record<string, AllyRelationStatus>> {
  if (userIds.length === 0) return {};
  const { data } = await api.post("/v1/me/allies/status-batch", { userIds }, authHeaders(accessToken));
  return data as Record<string, AllyRelationStatus>;
}

export const sharedEventSchema = z.object({ id: z.string(), title: z.string(), slug: z.string(), starts_at: z.string() });
export const sharedOrgSchema = z.object({ id: z.string(), name: z.string(), slug: z.string() });
/// Why two people know each other (D-226). Directional and derived — `label` is already written from
/// the viewer's perspective by the server, so the client renders it verbatim rather than re-wording it.
export const profileRelationshipSchema = z.object({
  type: z.string(),
  label: z.string(),
  count: z.number(),
  context: z.string().nullable()
});
export type ProfileRelationship = z.infer<typeof profileRelationshipSchema>;

export const mutualDetailSchema = z.object({
  shared_events: z.array(sharedEventSchema),
  shared_orgs: z.array(sharedOrgSchema),
  // Optional so a client pinned to a pre-D-226 backend still parses.
  relationships: z.array(profileRelationshipSchema).optional().default([])
});
export type MutualDetail = z.infer<typeof mutualDetailSchema>;

export async function getAllyMutualDetail(accessToken: string, otherUserId: string) {
  const { data } = await api.get(`/v1/me/allies/mutual/${otherUserId}`, authHeaders(accessToken));
  return mutualDetailSchema.parse(data);
}

export const allySuggestionSchema = z.object({
  user_id: z.string(), name: z.string(), username: z.string().nullable(), avatar_key: z.string().nullable(),
  avatar_url: z.string().nullable().optional(),
  shared_event_count: z.number(), shared_org_count: z.number(), reason: z.string(),
});
export type AllySuggestion = z.infer<typeof allySuggestionSchema>;

export async function getAllySuggestions(accessToken: string, limit = 20) {
  const { data } = await api.get("/v1/me/allies/suggestions", { ...authHeaders(accessToken), params: { limit } });
  return z.array(allySuggestionSchema).parse(data);
}

// --- People search (D-20x): foundation for finding anyone with a public profile ---

export const publicUserSearchResultSchema = z.object({
  id: z.string(), name: z.string(), username: z.string(), avatar_key: z.string().nullable(), headline: z.string().nullable(),
  avatar_url: z.string().nullable().optional(),
});
export type PublicUserSearchResult = z.infer<typeof publicUserSearchResultSchema>;

export async function searchPublicUsers(q: string, page = 1, pageSize = 20) {
  const { data } = await api.get("/v1/public/users", { params: { q, page, pageSize } });
  return z.array(publicUserSearchResultSchema).parse(data);
}

export const publicOrgSchema = z.object({
  id: z.string(),
  name: z.string(),
  slug: z.string(),
  logo_key: z.string().nullable(),
  bio: z.string().nullable(),
  tier: z.number(),
  events_count: z.number(),
  members_count: z.number(),
});
export type PublicOrg = z.infer<typeof publicOrgSchema>;

export async function getPublicOrg(slug: string) {
  const { data } = await api.get(`/v1/public/orgs/${slug}`);
  return publicOrgSchema.parse(data);
}

// --- Attendee engagement (D-064): save events, follow orgs, reviews ---

export async function saveEvent(accessToken: string, eventId: string) {
  await api.post(`/v1/events/${eventId}/save`, {}, authHeaders(accessToken));
}
export async function unsaveEvent(accessToken: string, eventId: string) {
  await api.delete(`/v1/events/${eventId}/save`, authHeaders(accessToken));
}
export async function listSavedEvents(accessToken: string, limit = 50) {
  const { data } = await api.get("/v1/me/saved", { ...authHeaders(accessToken), params: { limit } });
  return z.array(eventSummarySchema).parse(data);
}

export async function followOrg(accessToken: string, orgId: string) {
  await api.post(`/v1/orgs/${orgId}/follow`, {}, authHeaders(accessToken));
}
export async function unfollowOrg(accessToken: string, orgId: string) {
  await api.delete(`/v1/orgs/${orgId}/follow`, authHeaders(accessToken));
}
export async function listFollowing(accessToken: string, limit = 100) {
  const { data } = await api.get("/v1/me/following", { ...authHeaders(accessToken), params: { limit } });
  return z.array(z.object({
    org_id: z.string(), name: z.string(), slug: z.string(), logo_key: z.string().nullable(),
  })).parse(data);
}

export const eventReviewSchema = z.object({
  id: z.string(),
  event_id: z.string(),
  rating: z.number(),
  title: z.string().nullable(),
  body: z.string().nullable(),
  is_anonymous: z.boolean(),
  is_verified: z.boolean(),
  author_name: z.string().nullable(),
  author_username: z.string().nullable(),
  author_avatar_key: z.string().nullable(),
  author_avatar_url: z.string().nullable().optional(),
  created_at: z.string(),
});
export type EventReview = z.infer<typeof eventReviewSchema>;

export const eventReviewsSchema = z.object({
  items: z.array(eventReviewSchema),
  summary: z.object({ average: z.number(), count: z.number() }),
  total: z.number(),
});

export async function listEventReviews(eventId: string, page = 1, pageSize = 20) {
  const { data } = await api.get(`/v1/events/${eventId}/reviews`, { params: { page, pageSize } });
  return eventReviewsSchema.parse(data);
}
export async function upsertEventReview(accessToken: string, eventId: string,
  input: { rating: number; title?: string; body?: string; isAnonymous: boolean }) {
  const { data } = await api.post(`/v1/events/${eventId}/reviews`,
    { rating: input.rating, title: input.title ?? null, body: input.body ?? null, isAnonymous: input.isAnonymous },
    authHeaders(accessToken));
  return eventReviewSchema.parse(data);
}
export async function deleteMyReview(accessToken: string, eventId: string) {
  await api.delete(`/v1/events/${eventId}/reviews/mine`, authHeaders(accessToken));
}

export const certificateVerifySchema = z.object({
  verify_code: z.string(),
  status: z.string(),
  is_revoked: z.boolean().optional(),
  issued_to: z.string(),
  event_title: z.string(),
  event_slug: z.string(),
  organizer: z.string(),
  org_slug: z.string(),
  issued_at: z.string(),
  // The API returns a presigned download URL (pdf_url), not the storage key.
  pdf_url: z.string().nullable(),
});
export type CertificateVerify = z.infer<typeof certificateVerifySchema>;

export async function verifyCertificate(code: string) {
  const { data } = await api.get(`/v1/certificates/${code}`);
  return certificateVerifySchema.parse(data);
}

// GET /v1/orders returns ORDERS (ToOrderJson), not a flat ticket projection. This schema previously
// required `order_id`, which that payload has never contained — so zod threw on every non-empty
// response and the caller's `.catch(() => [])` turned it into "No tickets yet". Buyers with tickets
// saw an empty page and nothing was logged. Kept in lockstep with ToOrderJson in OrderEndpoints.cs.
export const myOrderTicketSchema = z.object({
  id: z.string(),
  code: z.string(),
  state: z.string(),
  checked_in_at: z.string().nullable().optional(),
  created_at: z.string(),
});

export const myOrderSchema = z.object({
  id: z.string(),
  event_id: z.string(),
  ticket_type_id: z.string(),
  status: z.string(),
  amount_paise: z.number(),
  currency: z.string(),
  created_at: z.string(),
  tickets: z.array(myOrderTicketSchema).default([]),
  // Denormalised by the list endpoint so a card can render without a second round trip.
  event_title: z.string().nullable().optional(),
  event_slug: z.string().nullable().optional(),
  ticket_type: z.string().nullable().optional(),
  group_id: z.string().nullable().optional(),
  join_code: z.string().nullable().optional(),
});
export type MyOrder = z.infer<typeof myOrderSchema>;

export async function myTickets(accessToken: string, page = 0, pageSize = 200) {
  const { data } = await api.get("/v1/orders", {
    ...authHeaders(accessToken),
    params: { page, pageSize }
  });
  return z.array(myOrderSchema).parse(data);
}

// ── Trust, verification & organizer readiness (M3–M8) ────────────────────────
// Typed clients only — all business rules stay in the backend. Response shapes are
// verified against the endpoint handlers (IdentityEndpoints, OrgEndpoints,
// EventEndpoints, MembershipClaimEndpoints, WalletEndpoints); requests are camelCase
// (case-insensitive-bound), responses snake_case, matching the rest of this file.

// Person identity verification (M3, D-042) — masked last-4 only.
export const identityStatusSchema = z.object({
  level: z.string(),
  status: z.string(),
  govt_id_kind: z.string().nullable(),
  govt_id_last4: z.string().nullable(),
  pan_last4: z.string().nullable(),
  bank_last4: z.string().nullable(),
  // Per-component state. The aggregate `status` cannot answer "is my PAN verified" — every
  // submission wrote it, so one failed bank check reported the whole identity rejected.
  // Optional so a client pinned to an older backend still parses.
  govt_id_status: z.string().optional().default("NotStarted"),
  pan_status: z.string().optional().default("NotStarted"),
  bank_status: z.string().optional().default("NotStarted"),
  penny_drop_status: z.string().optional().default("NotStarted"),
  bank_name_match: z.string().optional().default("NotChecked"),
  bank_verified_at: z.string().nullable().optional(),
  reviewed_at: z.string().nullable(),
  expires_at: z.string().nullable(),
  updated_at: z.string().nullable()
});
export type IdentityStatus = z.infer<typeof identityStatusSchema>;

export const identityHistoryEntrySchema = z.object({
  component: z.string(),
  decision: z.string(),
  reviewer_id: z.string().nullable(),
  reason_code: z.string().nullable(),
  notes: z.string().nullable(),
  created_at: z.string()
});
export type IdentityHistoryEntry = z.infer<typeof identityHistoryEntrySchema>;

/// The caller's own verification history. Caller-scoped server-side — the subject is resolved from
/// the token, never from a parameter, because a rejection is a private fact about a person.
export async function getMyIdentityHistory(accessToken: string, limit = 50) {
  const { data } = await api.get("/v1/me/identity/history", {
    params: { limit },
    headers: { Authorization: `Bearer ${accessToken}` }
  });
  return z.array(identityHistoryEntrySchema).parse(data);
}

export async function getMyIdentity(accessToken: string) {
  const { data } = await api.get("/v1/me/identity", authHeaders(accessToken));
  return identityStatusSchema.parse(data);
}

export async function submitIdentityGovernmentId(accessToken: string, body: { kind: string; idNumber: string; name: string }) {
  const { data } = await api.post("/v1/me/identity/government-id", body, authHeaders(accessToken));
  return identityStatusSchema.parse(data);
}

export async function submitIdentityPan(accessToken: string, body: { pan: string; name: string }) {
  const { data } = await api.post("/v1/me/identity/pan", body, authHeaders(accessToken));
  return identityStatusSchema.parse(data);
}

export async function submitIdentityBank(accessToken: string, body: { accountNumber: string; ifsc: string; holderName: string }) {
  const { data } = await api.post("/v1/me/identity/bank", body, authHeaders(accessToken));
  return identityStatusSchema.parse(data);
}

// Trust capability matrix in an org's context (M7, D-046) — live per request.
export const orgCapabilitiesSchema = z.object({
  can_represent_org: z.boolean(),
  is_org_verified_rep: z.boolean(),
  is_org_verified: z.boolean()
});
export type OrgCapabilities = z.infer<typeof orgCapabilitiesSchema>;

export async function getMyOrgCapabilities(accessToken: string, orgId: string) {
  const { data } = await api.get(`/v1/orgs/${orgId}/my-capabilities`, authHeaders(accessToken));
  return orgCapabilitiesSchema.parse(data);
}

// Live event payment-readiness gate (M8, D-047).
export const paymentReadinessSchema = z.object({
  is_paid: z.boolean(),
  payments_enabled: z.boolean(),
  blocking_reasons: z.array(z.string())
});
export type PaymentReadiness = z.infer<typeof paymentReadinessSchema>;

export async function getEventPaymentReadiness(accessToken: string, orgId: string, eventId: string) {
  const { data } = await api.get(`/v1/orgs/${orgId}/events/${eventId}/payment-readiness`, authHeaders(accessToken));
  return paymentReadinessSchema.parse(data);
}

// Organization registry fuzzy search (M4, D-043) — dedup before creating an org.
export const orgSearchResultSchema = z.object({
  id: z.string(),
  name: z.string(),
  slug: z.string(),
  logo_key: z.string().nullable(),
  type: z.string(),
  primary_domain: z.string().nullable(),
  verification_status: z.string(),
  score: z.number(),
  match: z.string()
});
export type OrgSearchResult = z.infer<typeof orgSearchResultSchema>;

export async function searchOrganizations(accessToken: string, q: string, limit = 10) {
  const { data } = await api.get("/v1/orgs/search", { ...authHeaders(accessToken), params: { q, limit } });
  return z.array(orgSearchResultSchema).parse(data);
}

// Membership-affiliation claims (M6, D-045).
export const membershipClaimSchema = z.object({
  id: z.string(),
  org_id: z.string(),
  org_name: z.string(),
  org_slug: z.string(),
  claimed_role: z.string(),
  status: z.string(),
  fast_track: z.boolean(),
  valid_until: z.string().nullable(),
  reviewed_at: z.string().nullable(),
  notes: z.string().nullable(),
  created_at: z.string()
});
export type MembershipClaim = z.infer<typeof membershipClaimSchema>;

export async function getMyMembershipClaims(accessToken: string) {
  const { data } = await api.get("/v1/me/membership-claims", authHeaders(accessToken));
  return z.array(membershipClaimSchema).parse(data);
}

export async function submitMembershipClaim(
  accessToken: string,
  orgId: string,
  body: { claimedRole: string; validUntil?: string; documents?: { docType: string; storageKey: string }[] }
) {
  const { data } = await api.post(`/v1/orgs/${orgId}/membership-claims`, body, authHeaders(accessToken));
  return membershipClaimSchema.parse(data);
}

// Organization wallet + ledger (D-028) — read-only. Withdrawal is a money-moving
// action gated on the real Route payout integration (P0, see docs/roadmap/README.md).
// snake_case, matching every other response on the platform (D-259 addendum).
//
// History worth keeping, because it explains why this file was wrong twice: these two endpoints
// forward the Application records (WalletView / LedgerEntryView) straight out of `Results.Ok`, and
// the API had no JSON naming policy — so they alone answered in camelCase while 65 of 75 endpoint
// files answered in snake_case. This schema declared snake_case against that camelCase reality, so
// `.parse()` threw a ZodError on EVERY call and the finance workspace could not render. The server now
// applies the platform convention to those records too, so snake_case here is finally the truth
// rather than a correct-looking guess.
export const walletSchema = z.object({
  org_id: z.string(),
  collected_paise: z.number(),
  available_paise: z.number(),
  advanced_paise: z.number(),
  reserved_paise: z.number(),
  settled_paise: z.number(),
  lifetime_earned_paise: z.number(),
  lifetime_withdrawn_paise: z.number(),
  updated_at: z.string(),
  // V3 §9.1 / D-257 — the org's settlement currency. Always sent; never assume INR at the call site.
  currency: z.string()
});
export type Wallet = z.infer<typeof walletSchema>;

export async function getOrgWallet(accessToken: string, orgId: string) {
  const { data } = await api.get(`/v1/orgs/${orgId}/wallet`, authHeaders(accessToken));
  return walletSchema.parse(data);
}

export const ledgerEntrySchema = z.object({
  id: z.string(),
  amount_paise: z.number(),
  state: z.string(),
  ref_type: z.string(),
  ref_id: z.string(),
  event_id: z.string(),
  currency: z.string(),
  created_at: z.string()
});
export type LedgerEntry = z.infer<typeof ledgerEntrySchema>;

export async function getOrgWalletLedger(accessToken: string, orgId: string, page = 1, pageSize = 20) {
  const { data } = await api.get(`/v1/orgs/${orgId}/wallet/ledger`, { ...authHeaders(accessToken), params: { page, pageSize } });
  return z.array(ledgerEntrySchema).parse(data);
}

// The organization member roster and its invitation flow had exactly one consumer — the org-level
// Team page — which D-267 removed: an organization roster is an organization-admin screen, and event
// collaboration already lives per event at /host/events/{id}/team. The backend endpoints remain for
// the admin console; no client wrapper exists here because nothing calls one.

// Ticket types for an event (org-facing — all types, any sale window) (D-020).
export const ticketTypeSchema = z.object({
  id: z.string(),
  event_id: z.string(),
  name: z.string(),
  price_paise: z.number(),
  pricing_unit: z.string(),
  registration_mode: z.string(),
  group_min: z.number().nullable(),
  group_max: z.number().nullable(),
  quantity: z.number(),
  sold: z.number(),
  available: z.number(),
  sale_starts: z.string(),
  sale_ends: z.string(),
  per_user_limit: z.number(),
  is_all_access: z.boolean(),
  is_competition: z.boolean()
});
export type TicketType = z.infer<typeof ticketTypeSchema>;

export async function listOrgTicketTypes(accessToken: string, orgId: string, eventId: string) {
  const { data } = await api.get(`/v1/orgs/${orgId}/events/${eventId}/ticket-types`, authHeaders(accessToken));
  return z.array(ticketTypeSchema).parse(data);
}

// Aggregate analytics summary for one event (D-053). Read-only, Owner/Manager.
export const eventAnalyticsSchema = z.object({
  event_id: z.string(),
  view_count: z.number(),
  ticket_types: z.number(),
  tickets_issued: z.number(),
  checked_in: z.number(),
  orders_paid: z.number(),
  gross_paise: z.number()
});
export type EventAnalytics = z.infer<typeof eventAnalyticsSchema>;

export async function getEventAnalytics(accessToken: string, orgId: string, eventId: string) {
  const { data } = await api.get(`/v1/orgs/${orgId}/events/${eventId}/analytics`, authHeaders(accessToken));
  return eventAnalyticsSchema.parse(data);
}

// Ticket-type + form-field mutations (D-020). Owner/Manager/Admin.
export async function createTicketType(accessToken: string, orgId: string, eventId: string, body: Record<string, unknown>) {
  const { data } = await api.post(`/v1/orgs/${orgId}/events/${eventId}/ticket-types`, body, authHeaders(accessToken));
  return ticketTypeSchema.parse(data);
}

export async function updateTicketType(accessToken: string, orgId: string, eventId: string, ticketTypeId: string, body: Record<string, unknown>) {
  const { data } = await api.patch(`/v1/orgs/${orgId}/events/${eventId}/ticket-types/${ticketTypeId}`, body, authHeaders(accessToken));
  return ticketTypeSchema.parse(data);
}

export async function deleteTicketType(accessToken: string, orgId: string, eventId: string, ticketTypeId: string) {
  await api.delete(`/v1/orgs/${orgId}/events/${eventId}/ticket-types/${ticketTypeId}`, authHeaders(accessToken));
}

export const formFieldSchema = z.object({
  id: z.string(),
  ticket_type_id: z.string(),
  key: z.string(),
  label: z.string(),
  type: z.string(),
  scope: z.string(),
  required: z.boolean(),
  options_json: z.string().nullable(),
  sort: z.number()
});
export type FormField = z.infer<typeof formFieldSchema>;

export async function listTicketTypeFields(accessToken: string, orgId: string, eventId: string, ticketTypeId: string) {
  const { data } = await api.get(`/v1/orgs/${orgId}/events/${eventId}/ticket-types/${ticketTypeId}/fields`, authHeaders(accessToken));
  return z.array(formFieldSchema).parse(data);
}

export async function addTicketTypeField(accessToken: string, orgId: string, eventId: string, ticketTypeId: string, body: Record<string, unknown>) {
  const { data } = await api.post(`/v1/orgs/${orgId}/events/${eventId}/ticket-types/${ticketTypeId}/fields`, body, authHeaders(accessToken));
  return formFieldSchema.parse(data);
}

export async function deleteTicketTypeField(accessToken: string, orgId: string, eventId: string, ticketTypeId: string, fieldId: string) {
  await api.delete(`/v1/orgs/${orgId}/events/${eventId}/ticket-types/${ticketTypeId}/fields/${fieldId}`, authHeaders(accessToken));
}

// Event staff assignments (D-064 A4).
export const eventAssignmentSchema = z.object({
  id: z.string(),
  event_id: z.string(),
  org_id: z.string(),
  user_id: z.string(),
  role: z.string(),
  custom_role: z.string().nullable(),
  status: z.string(),
  show_on_profile: z.boolean(),
  notes: z.string().nullable(),
  created_at: z.string(),
  assignee_name: z.string(),
  assignee_username: z.string().nullable(),
  assignee_avatar_key: z.string().nullable(),
  assignee_avatar_url: z.string().nullable().optional(),
  // D-319 — event context. The host's team list already knows the event; the invitee's own list is the
  // one that needs it, and both read this schema. `representing_org_name` is null for a self-represented
  // event, which carries no organization identity (D-268) — render nothing, never a fallback label.
  event_title: z.string(),
  event_slug: z.string().nullable(),
  event_starts_at: z.string(),
  representing_org_name: z.string().nullable(),
});
export type EventAssignment = z.infer<typeof eventAssignmentSchema>;

export async function listEventAssignments(accessToken: string, orgId: string, eventId: string) {
  const { data } = await api.get(`/v1/orgs/${orgId}/events/${eventId}/assignments`, authHeaders(accessToken));
  return z.array(eventAssignmentSchema).parse(data);
}

export async function assignToEvent(accessToken: string, orgId: string, eventId: string,
  body: { phone: string; role: string; customRole?: string; notes?: string }) {
  const { data } = await api.post(`/v1/orgs/${orgId}/events/${eventId}/assignments`, body, authHeaders(accessToken));
  return eventAssignmentSchema.parse(data);
}

export async function removeEventAssignment(accessToken: string, orgId: string, eventId: string, id: string) {
  await api.delete(`/v1/orgs/${orgId}/events/${eventId}/assignments/${id}`, authHeaders(accessToken));
}

/// The invitee's own view of every assignment naming them — invited and already accepted (D-319). This
/// is the only list that shows an invite to the one person who is allowed to answer it.
export async function listMyAssignments(accessToken: string) {
  const { data } = await api.get(`/v1/me/assignments`, authHeaders(accessToken));
  return z.array(eventAssignmentSchema).parse(data);
}

export async function respondToAssignment(accessToken: string, id: string, accept: boolean) {
  const { data } = await api.post(`/v1/assignments/${id}/${accept ? "accept" : "decline"}`, {}, authHeaders(accessToken));
  return eventAssignmentSchema.parse(data);
}

// Organization detail + profile edit (D-015 / D-043).
export const orgDetailSchema = z.object({
  id: z.string(),
  name: z.string(),
  slug: z.string(),
  logo_key: z.string().nullable(),
  bio: z.string().nullable(),
  links_json: z.string().nullable(),
  payout_account_status: z.string(),
  bank_last4: z.string().nullable(),
  tier: z.number(),
  role: z.string(),
  type: z.string(),
  primary_domain: z.string().nullable(),
  verification_status: z.string()
});
export type OrgDetail = z.infer<typeof orgDetailSchema>;

export async function getOrg(accessToken: string, orgId: string) {
  const { data } = await api.get(`/v1/orgs/${orgId}`, authHeaders(accessToken));
  return orgDetailSchema.parse(data);
}

export async function updateOrg(accessToken: string, orgId: string, body: Record<string, unknown>) {
  const { data } = await api.patch(`/v1/orgs/${orgId}`, body, authHeaders(accessToken));
  return orgDetailSchema.parse(data);
}

// Organization types (D-043). Backend OrganizationType, parsed case-insensitively; unknown → Other.
export const organizationTypes = [
  "college", "school", "university", "company", "startup",
  "ngo", "club", "community", "government", "other"
] as const;

// No client creates an organization. Institutions go through `submitRepresentationRequest` (an admin
// approves them), and representing yourself creates nothing at all — it is `Representing = Personal`,
// resolved server-side (D-268). This wrapper existed only for the retired "create a personal org" step.

// Presign representation-request evidence (D-076): the institution doesn't exist yet, so this is user-scoped
// (no orgId), unlike presignOrgDoc/presignClaimDoc.
export async function presignRepresentationDoc(accessToken: string, contentType: string, maxBytes: number) {
  const { data } = await api.post("/v1/orgs/representation-requests/media/presign",
    { contentType, maxBytes }, authHeaders(accessToken));
  return z.object({ key: z.string(), url: z.string(), headers: z.record(z.string()) }).parse(data);
}

// Register a NOT-yet-verified institution (event-first, D-074/D-075): stages a hidden placeholder org an
// admin approves. Returns the placeholder (verification_status "pendingreview", role "representative").
export async function submitRepresentationRequest(
  accessToken: string,
  body: { name: string; type?: string; legalName?: string; primaryDomain?: string; documents: { docType: string; storageKey: string }[] }
) {
  const { data } = await api.post("/v1/orgs/representation-requests", body, authHeaders(accessToken));
  return orgDetailSchema.parse(data);
}

// Org-level credential upload (verification letterhead, D-055) — Owner/Manager only. Mirrors presignEventMedia.
export async function presignOrgDoc(accessToken: string, orgId: string, contentType: string, maxBytes: number) {
  const { data } = await api.post(`/v1/orgs/${orgId}/media/presign`,
    { contentType, maxBytes }, authHeaders(accessToken));
  return z.object({ key: z.string(), url: z.string(), headers: z.record(z.string()) }).parse(data);
}

// Membership-claim evidence upload (D-055 G4) — any authenticated user, for their own claim to an existing org.
export async function presignClaimDoc(accessToken: string, orgId: string, contentType: string, maxBytes: number) {
  const { data } = await api.post(`/v1/orgs/${orgId}/membership-claims/media/presign`,
    { contentType, maxBytes }, authHeaders(accessToken));
  return z.object({ key: z.string(), url: z.string(), headers: z.record(z.string()) }).parse(data);
}

// Submit the org for verification with uploaded evidence (M5, D-044).
export async function submitOrgVerification(
  accessToken: string,
  orgId: string,
  documents: { docType: string; storageKey: string }[]
) {
  const { data } = await api.post(`/v1/orgs/${orgId}/verification/submit`, { documents }, authHeaders(accessToken));
  return z.object({ org_id: z.string(), status: z.string() }).passthrough().parse(data);
}

// ── Admin verification console (M5/M6/M12) ──────────────────────────────────
// Gated by the live VerificationReviewer role: the backend 403s non-reviewers, so the UI just
// surfaces that 403 rather than reading an admin flag off /v1/me (which has none).

// D-195: org verification review, membership claim review, org suspend/blacklist/merge, and the fraud
// blocklist were all ported to the dedicated admin/ console (admin/lib/api.ts) — this file's copies were
// removed after the migration's redirects were verified live. See docs/DECISIONS.md D-195.

// Event announcements (Phase B) — organizer → attendee broadcasts. Event-scoped.
export const announcementSchema = z.object({
  id: z.string(),
  event_id: z.string(),
  title: z.string(),
  body: z.string(),
  audience: z.string(),
  include_child_events: z.boolean(),
  channels: z.array(z.string()),
  status: z.string(),
  scheduled_at: z.string().nullable(),
  total_recipients: z.number(),
  sent_push: z.number(),
  sent_email: z.number(),
  sent_whatsapp: z.number(),
  failed_count: z.number(),
  sent_at: z.string().nullable(),
  created_at: z.string()
});
export type Announcement = z.infer<typeof announcementSchema>;

export async function listAnnouncements(accessToken: string, eventId: string) {
  const { data } = await api.get(`/v1/events/${eventId}/announcements`, authHeaders(accessToken));
  return z.array(announcementSchema).parse(data);
}

export async function createAnnouncement(accessToken: string, eventId: string, body: Record<string, unknown>) {
  const { data } = await api.post(`/v1/events/${eventId}/announcements`, body, authHeaders(accessToken));
  return announcementSchema.parse(data);
}

export async function cancelAnnouncement(accessToken: string, announcementId: string) {
  await api.delete(`/v1/announcements/${announcementId}`, authHeaders(accessToken));
}

// Event invitations (Phase B / D-036) — guest list + RSVP. Event-scoped.
export const invitationSchema = z.object({
  id: z.string(),
  event_id: z.string(),
  name: z.string(),
  email: z.string().nullable(),
  phone: z.string().nullable(),
  channel: z.string(),
  invite_token: z.string(),
  send_status: z.string(),
  rsvp_status: z.string(),
  status: z.string(),
  send_count: z.number(),
  sent_at: z.string().nullable(),
  responded_at: z.string().nullable(),
  created_at: z.string(),
  group_id: z.string().nullable()
});
export type Invitation = z.infer<typeof invitationSchema>;

export const invitationListSchema = z.object({
  items: z.array(invitationSchema),
  total: z.number(),
  funnel: z.object({
    invited: z.number(),
    sent: z.number(),
    accepted: z.number(),
    declined: z.number(),
    registered: z.number()
  })
});

export async function listInvitations(accessToken: string, eventId: string) {
  const { data } = await api.get(`/v1/events/${eventId}/invitations`, authHeaders(accessToken));
  return invitationListSchema.parse(data);
}

export async function addInvitation(accessToken: string, eventId: string, body: Record<string, unknown>) {
  const { data } = await api.post(`/v1/events/${eventId}/invitations`, body, authHeaders(accessToken));
  return invitationSchema.parse(data);
}

export async function sendInvitations(accessToken: string, eventId: string) {
  const { data } = await api.post(`/v1/events/${eventId}/invitations/send`, { invitationIds: null }, authHeaders(accessToken));
  return data as { queued: number };
}

export async function revokeInvitation(accessToken: string, invitationId: string) {
  await api.delete(`/v1/invitations/${invitationId}`, authHeaders(accessToken));
}

// Certificates (D-035) — bulk generate for an event's eligible tickets. Owner/Manager/Admin.
export async function generateCertificates(accessToken: string, eventId: string) {
  const { data } = await api.post(`/v1/events/${eventId}/certificates/generate`, {}, authHeaders(accessToken));
  return data as { generated: number };
}

// Certificate roster for an event (D-064) — list who has one + revoke. Owner/Manager/Admin.
export const eventCertificateSchema = z.object({
  id: z.string(),
  event_id: z.string(),
  verify_code: z.string(),
  user_id: z.string().nullable().optional(),
  holder_name: z.string().nullable(),
  kind: z.string(),
  status: z.string(),
  is_revoked: z.boolean(),
  revoked_reason: z.string().nullable(),
  issued_at: z.string()
});
export type EventCertificate = z.infer<typeof eventCertificateSchema>;

export async function listEventCertificates(accessToken: string, eventId: string) {
  const { data } = await api.get(`/v1/events/${eventId}/certificates`, authHeaders(accessToken));
  return z.array(eventCertificateSchema).parse(data);
}

export async function revokeCertificate(accessToken: string, certificateId: string, reason: string) {
  await api.post(`/v1/certificates/${certificateId}/revoke`, { reason }, authHeaders(accessToken));
}

// Attendee roster CSV export (D-054) — Owner/Manager/Staff. Returns text/csv with every custom-field answer.
export async function exportAttendeesCsv(accessToken: string, orgId: string, eventId: string) {
  const { data } = await api.get(`/v1/orgs/${orgId}/events/${eventId}/attendees/export`, {
    ...authHeaders(accessToken),
    responseType: "text"
  });
  return data as string;
}

// Attendee roster (D-054) — Owner/Manager/Staff. Returns attendee PII.
//
// snake_case, like the rest of this file. This endpoint forwards `AttendeeRow` records straight
// through `Results.Ok` with no manual projection, which is exactly the case the D-259 addendum was
// written for: `SnakeCaseResponseConverter` matches on the `Kurx.Application.Abstractions`
// namespace, so those records are renamed on the way out and the camelCase this schema used to
// declare no longer exists on the wire.
//
// The camelCase was correct when it was written and stopped being correct when D-259 landed. It
// survived because the break is invisible until an event has its first attendee: an empty roster
// parses clean through `z.array(...).parse([])`, so the page renders "0 attendees" and looks
// healthy right up to the moment it matters (D-289).
export const attendeeSchema = z.object({
  ticket_id: z.string(),
  code: z.string(),
  state: z.string(),
  checked_in_at: z.string().nullable(),
  buyer_name: z.string(),
  buyer_phone: z.string(),
  ticket_type_id: z.string(),
  ticket_type_name: z.string(),
  group_id: z.string().nullable(),
  group_number: z.number().nullable(),
  group_display_name: z.string().nullable(),
  answers: z.record(z.string().nullable()),
  buyer_user_id: z.string().nullable(),
  buyer_username: z.string().nullable(),
  buyer_avatar_key: z.string().nullable(),
  buyer_avatar_url: z.string().nullable().optional(),
});
export type Attendee = z.infer<typeof attendeeSchema>;

export const attendeeListSchema = z.object({ items: z.array(attendeeSchema), total: z.number() });

export async function listAttendees(accessToken: string, orgId: string, eventId: string) {
  const { data } = await api.get(`/v1/orgs/${orgId}/events/${eventId}/attendees`, authHeaders(accessToken));
  return attendeeListSchema.parse(data);
}

// ── Section outcomes: privacy is not an outage (D-235) ──────────────────────

/**
 * The outcome of fetching one profile section.
 *
 * Every section fetch on the profile page used to be `.catch(() => [])`, which collapsed six very
 * different situations into one empty list: a section the viewer may not see, a section that is
 * genuinely empty, a backend that is down, a network that dropped, a 500, and a bad response body.
 * The page then rendered all of them identically — as "this person has nothing here". A reader could
 * not tell a privacy choice from an outage, and neither could we.
 *
 * `hidden` is a *successful* answer to the question "may I see this": the section renders nothing, no
 * error, because nothing is wrong. `unavailable` means we do not know, and the UI must say so rather
 * than assert an absence it cannot support.
 */
export type SectionResult<T> =
  | { state: "ok"; data: T }
  | { state: "hidden" }
  | { state: "unavailable" };

/**
 * Runs a section fetch and classifies the outcome. 403 is the backend's "not entitled" (D-221); 404 on
 * a *section* means the profile itself is hidden or gone, which the page has already handled at the
 * root, so it is treated as hidden rather than as a failure. Everything else — 5xx, a timeout, DNS,
 * a schema mismatch — is `unavailable`, because none of them are evidence about the person.
 */
export async function section<T>(promise: Promise<T>): Promise<SectionResult<T>> {
  try {
    return { state: "ok", data: await promise };
  } catch (err) {
    const status = apiErrorStatus(err);
    if (status === 403 || status === 404) return { state: "hidden" };
    return { state: "unavailable" };
  }
}

/** The data if the section resolved, else `fallback` — for sections with no distinct error UI yet. */
export function sectionData<T>(result: SectionResult<T>, fallback: T): T {
  return result.state === "ok" ? result.data : fallback;
}

// ── D-266 M8 · the engines the wizard renders from ──────────────────────────────────────────
//
// **The client decides nothing about event behaviour.** Capabilities say what an event supports, the
// policy engine says what it is allowed to do, and both are read from the backend on every render. A
// hardcoded rule in `web/` is a Phase 1 failure by definition (D-266 §9), because it becomes a second
// source of truth that drifts the moment the matrix or a taxonomy row changes.

export const resolvedCapabilitySchema = z.object({
  slug: z.string(),
  name: z.string(),
  group_slug: z.string().nullable(),
  /** `required` | `on` | `off` | `locked`. **`locked` means the archetype forbids it** — never render a
   *  toggle for it; `off` means available and unchosen. Conflating the two is what the retired Kind model
   *  could not avoid. */
  state: z.string(),
  workspace_tab: z.string().nullable(),
});
export type ResolvedCapability = z.infer<typeof resolvedCapabilitySchema>;

export const policyRequirementsSchema = z.object({
  event_id: z.string(),
  product: z.string(),
  selected_policy: z.string(),
  /** The registration policies this event type permits. The wizard renders exactly these — never a
   *  hardcoded list, which would offer a policy the server refuses. */
  allowed_policies: z.array(z.string()),
  registration_gates: z.array(z.string()),
  identity_requirement: z.string(),
  registration_requirements: z.array(z.string()),
  /** Empty means publishable as far as policy is concerned. */
  publish_blockers: z.array(z.string()),
  reviewer_checklist: z.array(z.string()),
  is_valid: z.boolean(),
});
export type PolicyRequirements = z.infer<typeof policyRequirementsSchema>;

/** An event's effective capabilities — archetype matrix × product × mode × dependency DAG, resolved server-side. */
export async function getEventCapabilities(accessToken: string, orgId: string, eventId: string): Promise<ResolvedCapability[]> {
  const { data } = await api.get(`/v1/orgs/${orgId}/events/${eventId}/capabilities`, authHeaders(accessToken));
  return z.array(resolvedCapabilitySchema).parse(data);
}

/** What an archetype supports, with no event yet — what the wizard needs while the user is still choosing. */
export async function getArchetypeCapabilities(slug: string, mode?: string): Promise<ResolvedCapability[]> {
  const { data } = await api.get(`/v1/archetypes/${slug}/capabilities`, { params: mode ? { mode } : undefined });
  return z.array(resolvedCapabilitySchema).parse(data);
}

export async function getPolicyRequirements(accessToken: string, orgId: string, eventId: string): Promise<PolicyRequirements> {
  const { data } = await api.get(`/v1/orgs/${orgId}/events/${eventId}/policy-requirements`, authHeaders(accessToken));
  return policyRequirementsSchema.parse(data);
}

// ── D-266 M5/M8 · institutional authorization ───────────────────────────────────────────────

export const eventAuthorizationSchema = z.object({
  event_id: z.string(),
  head_name: z.string(),
  head_designation: z.string(),
  official_email: z.string(),
  official_phone: z.string().nullable(),
  representative_role: z.string(),
  representative_role_other: z.string().nullable(),
  representative_user_id: z.string().nullable(),
  representative_username: z.string().nullable(),
  reviewer_name: z.string().nullable(),
  letterhead_url: z.string().nullable(),
  signature_url: z.string().nullable(),
  supporting_document_urls: z.array(z.string()),
  status: z.string(),
  reason_code: z.string().nullable(),
  notes: z.string().nullable(),
  created_at: z.string(),
  updated_at: z.string(),
});
export type EventAuthorizationView = z.infer<typeof eventAuthorizationSchema>;

/** Null when nothing is filed — the backend answers 204, which is a state and not an error. */
export async function getEventAuthorization(accessToken: string, eventId: string): Promise<EventAuthorizationView | null> {
  const { data, status } = await api.get(`/v1/events/${eventId}/authorization`, authHeaders(accessToken));
  return status === 204 || !data ? null : eventAuthorizationSchema.parse(data);
}

export async function submitEventAuthorization(
  accessToken: string, eventId: string,
  body: {
    headName: string; headDesignation: string; officialEmail: string; officialPhone: string;
    representativeRole: string; representativeRoleOther?: string; representativeUserId?: string;
    // Omitted on a re-file means "keep the document on file" — the client never holds the stored key.
    letterheadDocumentKey?: string; signatureDocumentKey?: string; supportingDocumentKeys?: string[];
  },
) {
  const { data } = await api.post(`/v1/events/${eventId}/authorization`, body, authHeaders(accessToken));
  return eventAuthorizationSchema.parse(data);
}

/** The closed role vocabulary, from the server that validates it — never a client-side copy. A hardcoded
 *  list is how a role gets offered here and refused by the API. */
export async function getRepresentativeRoles(accessToken: string) {
  const { data } = await api.get("/v1/events/authorization/roles", authHeaders(accessToken));
  return z.array(z.string()).parse(data);
}

/** Presigned PUT for one authorization document. The key comes back with the URL and is what the submit
 *  call carries — bytes never travel through the API. */
export async function presignAuthorizationDocument(
  accessToken: string, eventId: string, contentType: string, maxBytes: number,
) {
  const { data } = await api.post(`/v1/events/${eventId}/authorization/presign`,
    { contentType, maxBytes }, authHeaders(accessToken));
  return z.object({ key: z.string(), url: z.string(), headers: z.record(z.string()) }).parse(data);
}

// ── D-266 M6/M8 · invite links (D9 Method B) ────────────────────────────────────────────────

export const inviteLinkSchema = z.object({
  id: z.string(),
  event_id: z.string(),
  token: z.string(),
  max_seats: z.number().nullable(),
  used_count: z.number(),
  single_use: z.boolean(),
  expires_at: z.string().nullable(),
  /** Whether a passcode is set. The passcode itself is hashed server-side and never returned. */
  requires_passcode: z.boolean(),
  status: z.string(),
  created_at: z.string(),
});
export type InviteLink = z.infer<typeof inviteLinkSchema>;

export async function listInviteLinks(accessToken: string, eventId: string): Promise<InviteLink[]> {
  const { data } = await api.get(`/v1/events/${eventId}/invite-links`, authHeaders(accessToken));
  return z.array(inviteLinkSchema).parse(data);
}

export async function createInviteLink(
  accessToken: string, eventId: string,
  body: { maxSeats?: number | null; singleUse?: boolean; expiresAt?: string | null; passcode?: string | null },
): Promise<InviteLink> {
  const { data } = await api.post(`/v1/events/${eventId}/invite-links`, body, authHeaders(accessToken));
  return inviteLinkSchema.parse(data);
}

export async function revokeInviteLink(accessToken: string, linkId: string) {
  await api.delete(`/v1/invite-links/${linkId}`, authHeaders(accessToken));
}

// ── D-266 M6 · the invitee's side (D9 Method A) ─────────────────────────────────────────────

export const myInvitationSchema = z.object({
  id: z.string(),
  event_id: z.string(),
  event_title: z.string(),
  event_slug: z.string(),
  banner_key: z.string().nullable(),
  starts_at: z.string(),
  venue_name: z.string().nullable(),
  city: z.string().nullable(),
  invited_by_name: z.string(),
  /** `None` | `Accepted` | `Declined`. */
  rsvp_status: z.string(),
  created_at: z.string(),
});
export type MyInvitation = z.infer<typeof myInvitationSchema>;

export async function listMyInvitations(accessToken: string, pendingOnly = false): Promise<MyInvitation[]> {
  const { data } = await api.get("/v1/me/invitations",
    { ...authHeaders(accessToken), params: { pendingOnly } });
  return z.array(myInvitationSchema).parse(data);
}

export async function respondToInvitation(accessToken: string, invitationId: string, accept: boolean) {
  const { data } = await api.post(`/v1/invitations/${invitationId}/${accept ? "accept" : "decline"}`,
    {}, authHeaders(accessToken));
  return myInvitationSchema.parse(data);
}

/** Username lookup for the invite picker — the same index as the public profile search, so a user who
 *  has not made their username discoverable is invited by email or phone instead. */
export async function searchUsersForInvite(accessToken: string, q: string) {
  const { data } = await api.get("/v1/users/search",
    { ...authHeaders(accessToken), params: { q, pageSize: 8 } });
  return z.array(z.object({
    id: z.string(), name: z.string(), username: z.string(), avatar_key: z.string().nullable(),
    avatar_url: z.string().nullable().optional(),
  })).parse(data);
}

// ── D-266 M6 · redeeming a link (D9 Method B) ───────────────────────────────────────────────

export const publicInviteLinkSchema = z.object({
  event_title: z.string(),
  event_slug: z.string(),
  starts_at: z.string(),
  venue_name: z.string().nullable(),
  city: z.string().nullable(),
  requires_passcode: z.boolean(),
  is_usable: z.boolean(),
  /** `invite_link_revoked` | `invite_link_expired` | `invite_link_exhausted`, or null when usable. */
  reason: z.string().nullable(),
  seats_remaining: z.number().nullable(),
});
export type PublicInviteLink = z.infer<typeof publicInviteLinkSchema>;

/** Unauthenticated pre-flight — the holder may not have an account yet, which is the case Method B exists for. */
export async function getPublicInviteLink(token: string): Promise<PublicInviteLink> {
  const { data } = await api.get(`/v1/public/invite-links/${token}`);
  return publicInviteLinkSchema.parse(data);
}

export async function redeemInviteLink(accessToken: string, token: string, passcode?: string) {
  await api.post(`/v1/invite-links/${token}/redeem`, { passcode: passcode ?? null }, authHeaders(accessToken));
}

// ── Profile hub surfaces (D-305) ────────────────────────────────────────────
// Endpoints that existed on the backend with no web caller at all. Flutter's Profile has reached these
// for months; web's Profile was a redirect into Settings, so the data had nowhere to land. Shapes are
// read off the endpoint handlers, not guessed — the D-292/D-245 lesson is that a hand-written client
// model which disagrees with the wire fails silently inside a `.catch`.

/// `WaitlistEndpoints.ToJson`. Carries no event title — the endpoint does not join one — so callers
/// link by id rather than inventing a name.
export const waitlistEntrySchema = z.object({
  id: z.string(),
  event_id: z.string(),
  ticket_type_id: z.string(),
  position: z.number(),
  status: z.string(),
  offer_expires_at: z.string().nullable().optional(),
  created_at: z.string()
});
export type WaitlistEntry = z.infer<typeof waitlistEntrySchema>;

export async function listMyWaitlist(accessToken: string) {
  const { data } = await api.get("/v1/me/waitlist", authHeaders(accessToken));
  return z.array(waitlistEntrySchema).parse(data);
}

/// `OrgInvitationEndpoints.ToJson`. Accept/decline are **token-keyed**, and the token only ever arrives
/// in the invite message — so this list is a read-only inbox showing what is pending, exactly as the
/// Flutter screen notes. Do not add buttons here that have no token to send.
export const orgInvitationSchema = z.object({
  id: z.string(),
  org_id: z.string(),
  org_name: z.string(),
  invited_phone: z.string().nullable().optional(),
  role: z.string(),
  status: z.string(),
  expires_at: z.string().nullable().optional(),
  created_at: z.string()
});
export type OrgInvitation = z.infer<typeof orgInvitationSchema>;

export async function listMyOrgInvitations(accessToken: string) {
  const { data } = await api.get("/v1/me/org-invitations", authHeaders(accessToken));
  return z.array(orgInvitationSchema).parse(data);
}

/// `GET /v1/me/points` → `PointsSummary`. `total_points` and `created_at` carry explicit
/// `JsonPropertyName` attributes server-side; the rest follow the response convention.
export const pointsEntrySchema = z.object({
  id: z.string(),
  source: z.string(),
  points: z.number(),
  reason: z.string().nullable().optional(),
  created_at: z.string()
});
export const pointsSummarySchema = z.object({
  total_points: z.number(),
  history: z.array(pointsEntrySchema).default([])
});
export type PointsSummary = z.infer<typeof pointsSummarySchema>;

export async function myPoints(accessToken: string) {
  const { data } = await api.get("/v1/me/points", authHeaders(accessToken));
  return pointsSummarySchema.parse(data);
}

/// `GET /v1/me/badges` → `BadgeView[]`. `icon_key` is a STORAGE KEY and is not fetchable — it is
/// deliberately not rendered as an image (D-302); the badge shows its name and type.
export const badgeSchema = z.object({
  id: z.string(),
  name: z.string(),
  type: z.string(),
  description: z.string(),
  icon_key: z.string().nullable().optional(),
  earned_at: z.string()
});
export type Badge = z.infer<typeof badgeSchema>;

export async function myBadges(accessToken: string) {
  const { data } = await api.get("/v1/me/badges", authHeaders(accessToken));
  return z.array(badgeSchema).parse(data);
}
