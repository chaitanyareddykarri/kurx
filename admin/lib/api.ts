import axios from "axios";
import { problemMessage } from "@kurx/ui";
import { cache } from "react";
import { z } from "zod";
import { siteConfig } from "@/lib/site";

// Reuses the same backend as web/mobile — no separate admin API surface for auth.
export const api = axios.create({
  baseURL: siteConfig.apiBaseUrl,
  headers: { "Content-Type": "application/json" }
});

/** Backend errors are RFC7807 ProblemDetails with an "error" code (docs/api/README.md). */
export function apiErrorMessage(err: unknown): string {
  if (axios.isAxiosError(err)) {
    // The `error` field is a machine-readable CODE, not a sentence (docs/api/README.md); returning
    // it verbatim put strings like `forbidden` in front of a moderator. Same words as mobile.
    const data = err.response?.data as { error?: string; detail?: string } | undefined;
    return problemMessage({ code: data?.error, detail: data?.detail, status: err.response?.status });
  }
  return err instanceof Error ? err.message : "Something went wrong.";
}

/** HTTP status of a failed call — used to tell 401/403 apart from a generic failure. */
export function apiErrorStatus(err: unknown): number | undefined {
  return axios.isAxiosError(err) ? err.response?.status : undefined;
}

// ── Section outcomes: an outage is not an answer (D-235) ────────────────────

/**
 * The outcome of one read, kept distinct from its data.
 *
 * Web has had this since Phase 15 and admin never got it, so admin kept writing `.catch(() => [])` —
 * which collapses "you may not see this", "there is genuinely nothing", "the backend is down" and
 * "the response did not parse" into one empty list. On a console that is worse than on web: an empty
 * list here reads as *no reports against this event*, or *nothing else is signed in as you*, and an
 * operator acts on it.
 *
 * `hidden` is a successful answer to "may I see this". `unavailable` means we do not know, and the
 * screen must say so rather than assert an absence it cannot support.
 */
export type SectionResult<T> = { state: "ok"; data: T } | { state: "hidden" } | { state: "unavailable" };

export async function section<T>(promise: Promise<T>): Promise<SectionResult<T>> {
  try {
    return { state: "ok", data: await promise };
  } catch (err) {
    const status = apiErrorStatus(err);
    if (status === 403 || status === 404) return { state: "hidden" };
    return { state: "unavailable" };
  }
}

/** The data if the read resolved, else `fallback` — for reads with no distinct error UI yet. */
export function sectionData<T>(result: SectionResult<T>, fallback: T): T {
  return result.state === "ok" ? result.data : fallback;
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

// NOTE: /v1/me exposes only `is_platform_reviewer` today. Full RBAC for the console
// needs the backend to return `platform_roles: string[]` here (see lib/roles.ts and
// the Phase-2 backend requirements). The optional field below is future-proofing —
// it is parsed if present, ignored if absent.
export const meSchema = z.object({
  id: z.string(),
  phone: z.string(),
  name: z.string(),
  username: z.string().nullable().optional(),
  email: z.string().nullable().optional(),
  needs_onboarding: z.boolean(),
  is_platform_reviewer: z.boolean().optional().default(false),
  platform_roles: z.array(z.string()).optional()
});
export type Me = z.infer<typeof meSchema>;

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
  const { data } = await api.get("/v1/me", { headers: { Authorization: `Bearer ${accessToken}` } });
  return meSchema.parse(data);
}

// ── Staff & roles (admin console M12 / D-056), SuperAdmin only ──────────────
function auth(accessToken: string) {
  return { headers: { Authorization: `Bearer ${accessToken}` } };
}

export const staffMemberSchema = z.object({
  user_id: z.string(),
  name: z.string(),
  phone: z.string(),
  username: z.string().nullable().optional(),
  roles: z.array(z.string())
});
export type StaffMember = z.infer<typeof staffMemberSchema>;

export async function listStaff(accessToken: string, params?: { q?: string; role?: string }) {
  const { data } = await api.get("/v1/admin/staff", { ...auth(accessToken), params });
  return z.array(staffMemberSchema).parse(data);
}

export async function grantStaff(accessToken: string, body: { phone: string; role: string }) {
  const { data } = await api.post("/v1/admin/staff/grant", body, auth(accessToken));
  return staffMemberSchema.parse(data);
}

export async function revokeStaffRole(accessToken: string, userId: string, role: string) {
  await api.delete(`/v1/admin/staff/${userId}/roles/${role}`, auth(accessToken));
}

// ── Verification console (M5/M6/M12), VerificationReviewer only ──────────────
// The backend 403s non-reviewers; pages surface that 403 rather than pre-checking a flag.
export const pendingOrgSchema = z.object({
  org_id: z.string(),
  name: z.string(),
  slug: z.string(),
  type: z.string(),
  primary_domain: z.string().nullable(),
  document_count: z.number(),
  submitted_at: z.string()
});
export type PendingOrg = z.infer<typeof pendingOrgSchema>;

export async function listPendingOrgVerifications(accessToken: string, limit = 50) {
  const { data } = await api.get("/v1/admin/orgs/pending", { ...auth(accessToken), params: { limit } });
  return z.array(pendingOrgSchema).parse(data);
}

// decision: "approve" | "reject" | "request_changes"
export async function reviewOrgVerification(
  accessToken: string,
  orgId: string,
  body: { decision: string; reasonCode?: string; notes?: string }
) {
  const { data } = await api.post(`/v1/admin/orgs/${orgId}/verification/review`, body, auth(accessToken));
  return z.object({ org_id: z.string(), status: z.string() }).passthrough().parse(data);
}

export const verificationDocSchema = z.object({
  id: z.string(),
  doc_type: z.string(),
  status: z.string(),
  created_at: z.string()
});

export async function getVerificationHistory(accessToken: string, subjectType: string, subjectId: string) {
  const { data } = await api.get(`/v1/admin/verifications/${subjectType}/${subjectId}/history`, auth(accessToken));
  return z.object({
    subject_type: z.string(),
    subject_id: z.string(),
    reviews: z.array(z.unknown()),
    documents: z.array(verificationDocSchema)
  }).parse(data);
}

export async function getVerificationDocViewUrl(accessToken: string, documentId: string) {
  const { data } = await api.get(`/v1/admin/verification-documents/${documentId}/view`, auth(accessToken));
  return z.object({ url: z.string() }).parse(data).url;
}

export const pendingClaimSchema = z.object({
  id: z.string(),
  user_id: z.string(),
  user_name: z.string(),
  username: z.string().nullable(),
  org_id: z.string(),
  org_name: z.string(),
  claimed_role: z.string(),
  status: z.string(),
  fast_track: z.boolean(),
  created_at: z.string()
});
export type PendingClaim = z.infer<typeof pendingClaimSchema>;

export async function listPendingMembershipClaims(accessToken: string, limit = 50) {
  const { data } = await api.get("/v1/admin/membership-claims/pending", { ...auth(accessToken), params: { limit } });
  return z.array(pendingClaimSchema).parse(data);
}

// decision: "approve" | "reject"
export async function reviewMembershipClaim(
  accessToken: string,
  claimId: string,
  body: { decision: string; reasonCode?: string; notes?: string }
) {
  const { data } = await api.post(`/v1/admin/membership-claims/${claimId}/review`, body, auth(accessToken));
  return z.object({ id: z.string(), status: z.string() }).passthrough().parse(data);
}

// ── Org lifecycle admin (M12): suspend / blacklist / merge ───────────────────
const orgStatusSchema = z.object({ org_id: z.string(), status: z.string() }).passthrough();

export async function suspendOrg(accessToken: string, orgId: string, reason?: string) {
  const { data } = await api.post(`/v1/admin/orgs/${orgId}/verification/suspend`, { reason }, auth(accessToken));
  return orgStatusSchema.parse(data);
}

export async function blacklistOrg(accessToken: string, orgId: string, reason?: string) {
  const { data } = await api.post(`/v1/admin/orgs/${orgId}/verification/blacklist`, { reason }, auth(accessToken));
  return orgStatusSchema.parse(data);
}

export async function mergeOrgs(accessToken: string, duplicateOrgId: string, canonicalOrgId: string) {
  const { data } = await api.post("/v1/admin/orgs/merge", { duplicateOrgId, canonicalOrgId }, auth(accessToken));
  return orgStatusSchema.parse(data);
}

// ── Org list & detail (D-194): platform-wide, admin-scoped — no admin org read existed before this
// (admin/STATUS.md §5.4). Detail composes with the already-built listOrgEventsAsAdmin/getOrgWalletAsAdmin/
// getFraudScore below (built by D-186 for the event workspace's Organizer tab, reused here as-is).
export const adminOrgSchema = z.object({
  org_id: z.string(), name: z.string(), slug: z.string(), logo_key: z.string().nullable(),
  type: z.string(), verification_status: z.string(), primary_domain: z.string().nullable(),
  // Admin-only (D-268): marks a self-representation persistence row rather than an institution.
  // Staff reviewing the registry must tell the two apart; no user-facing surface sees this flag.
  is_personal: z.boolean(), member_count: z.number(), event_count: z.number(), created_at: z.string()
});
export type AdminOrg = z.infer<typeof adminOrgSchema>;

export type AdminOrgListParams = { q?: string; status?: string; type?: string; limit?: number; page?: number };

export async function listAdminOrgs(accessToken: string, params: AdminOrgListParams = {}) {
  const { data } = await api.get("/v1/admin/orgs", { ...auth(accessToken), params });
  return z.object({ items: z.array(adminOrgSchema), total: z.number() }).parse(data);
}

export const adminOrgDetailSchema = z.object({
  org_id: z.string(), name: z.string(), slug: z.string(), logo_key: z.string().nullable(),
  bio: z.string().nullable(), links_json: z.string().nullable(), payout_account_status: z.string(),
  bank_last4: z.string().nullable(), tier: z.number(), type: z.string(), primary_domain: z.string().nullable(),
  verification_status: z.string()
});
export type AdminOrgDetail = z.infer<typeof adminOrgDetailSchema>;

export async function getAdminOrgDetail(accessToken: string, orgId: string) {
  const { data } = await api.get(`/v1/admin/orgs/${orgId}`, auth(accessToken));
  return adminOrgDetailSchema.parse(data);
}

// ── Fraud (M13, D-052): hard blocklist + polymorphic risk signals ────────────
export const blacklistEntrySchema = z.object({
  id: z.string(),
  kind: z.string(),
  value: z.string(),
  reason: z.string().nullable(),
  created_at: z.string()
});
export type BlacklistEntry = z.infer<typeof blacklistEntrySchema>;

export async function listBlacklist(accessToken: string) {
  const { data } = await api.get("/v1/admin/blacklist", auth(accessToken));
  return z.array(blacklistEntrySchema).parse(data);
}

export async function addBlacklist(accessToken: string, body: { kind: string; value: string; reason?: string }) {
  const { data } = await api.post("/v1/admin/blacklist", body, auth(accessToken));
  return blacklistEntrySchema.parse(data);
}

export async function removeBlacklist(accessToken: string, id: string) {
  await api.delete(`/v1/admin/blacklist/${id}`, auth(accessToken));
}

// Record a polymorphic risk signal (POST-only; there is no list endpoint yet). Returns the new risk score.
export async function recordFraudSignal(
  accessToken: string,
  body: { subjectType: string; subjectId: string; kind: string; value?: string; score: number }
) {
  const { data } = await api.post("/v1/admin/fraud-signals", body, auth(accessToken));
  return z.object({ risk_score: z.number() }).parse(data);
}

// ── Event approval queue (M8, D-057), VerificationReviewer only ──────────────
export const pendingEventSchema = z.object({
  event_id: z.string(),
  // The organisation the event REPRESENTS, not its owner (D-268/D-273a). Required, unlike the web and
  // mobile schemas: the console is deployed together with the API it reads, so it never has to tolerate
  // a pre-rename server. The deprecated `org_id` is simply not modelled here.
  representing_org_id: z.string(),
  org_name: z.string(),
  title: z.string(),
  slug: z.string(),
  starts_at: z.string(),
  created_at: z.string(),
  // D-266 M7 — enrichment, so a reviewer can triage the queue without a call per row. Optional in the
  // schema only so the console still parses a payload from a server deployed before M7; every field is
  // sent by the current API. There is deliberately no `meeting_password`: the server's DTO has no such
  // field, and modelling one here would invite someone to expect it.
  status: z.string().optional(),
  product: z.string().optional(),
  archetype_slug: z.string().nullable().optional(),
  is_paid: z.boolean().optional(),
  authorization_status: z.string().nullable().optional(),
  city: z.string().nullable().optional(),
  // D-266 M4 — who holds this item. One reviewer at a time: shown so the queue reads as taken rather than
  // inviting a second reviewer to start work they will be refused at the decision.
  review_claimed_by: z.string().nullable().optional(),
  review_claimed_by_name: z.string().nullable().optional(),
  review_claimed_at: z.string().nullable().optional()
});
export type PendingEvent = z.infer<typeof pendingEventSchema>;

// ── D-266 M7 · financial review (D12 §6, A11 Fundraising) ───────────────────────────────────
// FinanceOps only — clearing a money path is a different competence from reviewing content, which is
// why this is not on the reviewer-gated group.

export async function recordFinancialReview(
  accessToken: string, eventId: string, passed: boolean, notes?: string,
) {
  await api.post(`/v1/admin/events/${eventId}/financial-review`, { passed, notes }, auth(accessToken));
}

export async function listPendingEvents(accessToken: string, limit = 50) {
  const { data } = await api.get("/v1/admin/events/pending", { ...auth(accessToken), params: { limit } });
  return z.array(pendingEventSchema).parse(data);
}

// Approve = "publish", reject = "reject". Reuses the org-scoped transition, which the backend already
// lets a reviewer drive on any org's event (M8) — no dedicated admin action endpoint needed.

// ── D-266 M4 · review lifecycle ───────────────────────────────────────────────
// The reviewer actions (claim_review / release_review / request_changes / approve_review /
// reject_review) go through this same transition endpoint. There is deliberately no admin-specific
// action route: the workflow lives in the backend, and duplicating it here is how the two drift.

/** Which reviewer decisions require what. Mirrors EventStatusWorkflow, but the backend is the
 *  authority — it rejects a missing reason or notes regardless of what the client sends. */
export type ReviewAction =
  | "submit_for_review" | "withdraw"
  | "claim_review" | "release_review"
  | "request_changes" | "approve_review" | "reject_review"
  | "publish_approved";

/** Closed vocabulary from Kurx.Domain.Enums.EventReviewReason. */
export const reviewReasonSchema = z.enum([
  "Incomplete", "ProhibitedContent", "UnverifiedOrganiser",
  "MisrepresentedAffiliation", "InvalidCommerce", "Duplicate", "Other",
]);
export type ReviewReason = z.infer<typeof reviewReasonSchema>;

export interface TransitionEventRequest {
  action: string;
  /** Required by the backend for reject_review; an EventReviewReason name. */
  reasonCode?: ReviewReason;
  /** Required by the backend for request_changes. Admin-only; never shown verbatim to attendees. */
  notes?: string;
}

export const reviewCountsSchema = z.object({
  pending_review: z.number(),
  under_review: z.number(),
  changes_requested: z.number(),
  approved: z.number(),
  rejected: z.number(),
  /** Pre-M4 InReview bucket, present until the legacy state is retired. */
  legacy_in_review: z.number(),
});
export type ReviewCounts = z.infer<typeof reviewCountsSchema>;
/** Alias kept for call sites that read these as queue tab counts. */
export type ReviewQueueCounts = ReviewCounts;

export const reviewHistoryItemSchema = z.object({
  id: z.string(),
  decision: z.string(),
  reviewer_id: z.string().nullable(),
  /** Null when the reviewer account no longer exists — the decision is still retained. */
  reviewer_name: z.string().nullable(),
  reason_code: z.string().nullable(),
  notes: z.string().nullable(),
  created_at: z.string(),
});
export type ReviewHistoryItem = z.infer<typeof reviewHistoryItemSchema>;
export type ReviewHistory = ReviewHistoryItem[];

export async function getReviewCounts(accessToken: string): Promise<ReviewCounts> {
  const { data } = await api.get("/v1/admin/events/review-counts", auth(accessToken));
  return reviewCountsSchema.parse(data);
}

export async function getReviewHistory(accessToken: string, eventId: string): Promise<ReviewHistory> {
  const { data } = await api.get(`/v1/admin/events/${eventId}/review-history`, auth(accessToken));
  return z.array(reviewHistoryItemSchema).parse(data);
}

// ── D-266 M7 · reviewer checklist ───────────────────────────────────────────────────────────
// The ITEMS come from the backend's policy engine on every read — they are never cached or
// reconstructed here. A hardcoded list in the console would be a second source of truth and would go
// stale the moment a rule changed, letting a reviewer tick a complete-looking list the publish refuses.

export const reviewChecklistItemSchema = z.object({
  key: z.string(),
  checked: z.boolean(),
  checked_at: z.string().nullable(),
  /** True when this item would refuse the publish on its own, not merely require confirmation. */
  blocking: z.boolean(),
});
export const reviewChecklistSchema = z.object({
  event_id: z.string(),
  items: z.array(reviewChecklistItemSchema),
  is_complete: z.boolean(),
});
export type ReviewChecklist = z.infer<typeof reviewChecklistSchema>;

export async function getReviewChecklist(accessToken: string, eventId: string): Promise<ReviewChecklist> {
  const { data } = await api.get(`/v1/admin/events/${eventId}/review-checklist`, auth(accessToken));
  return reviewChecklistSchema.parse(data);
}

export async function setReviewChecklistItem(
  accessToken: string, eventId: string, itemKey: string, checked: boolean,
): Promise<ReviewChecklist> {
  const { data } = await api.put(`/v1/admin/events/${eventId}/review-checklist`,
    { itemKey, checked }, auth(accessToken));
  return reviewChecklistSchema.parse(data);
}

// ── D-266 M5/M7 · institutional authorization ───────────────────────────────────────────────
// Documents arrive as short-lived presigned URLs, never storage keys.

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
  reviewer_id: z.string().nullable(),
  reviewed_at: z.string().nullable(),
  reason_code: z.string().nullable(),
  notes: z.string().nullable(),
  created_at: z.string(),
  updated_at: z.string(),
});
export type EventAuthorization = z.infer<typeof eventAuthorizationSchema>;

/** Null when nothing has been filed — the backend answers 204, which is a state rather than an error. */
export async function getEventAuthorization(accessToken: string, eventId: string): Promise<EventAuthorization | null> {
  const { data, status } = await api.get(`/v1/admin/events/${eventId}/authorization`, auth(accessToken));
  return status === 204 || !data ? null : eventAuthorizationSchema.parse(data);
}

export async function reviewEventAuthorization(
  accessToken: string, eventId: string, decision: "approve" | "reject" | "request_changes",
  reasonCode?: string, notes?: string,
) {
  const { data } = await api.post(`/v1/admin/events/${eventId}/authorization/review`,
    { decision, reasonCode, notes }, auth(accessToken));
  return eventAuthorizationSchema.parse(data);
}

/** `reasonCode`/`notes` are optional and trailing, so every existing caller compiles unchanged.
 *  Omitted keys are dropped rather than sent as null: the backend treats absent and null alike, but
 *  sending nulls would make request logs read as if a reviewer had explicitly cleared them. */
export async function transitionEvent(
  accessToken: string, orgId: string, eventId: string, action: string,
  reasonCode?: ReviewReason, notes?: string,
) {
  const body: TransitionEventRequest = { action };
  if (reasonCode !== undefined) body.reasonCode = reasonCode;
  if (notes !== undefined) body.notes = notes;
  await api.post(`/v1/orgs/${orgId}/events/${eventId}/transition`, body, auth(accessToken));
}

// ── Global event management (D-061/D-186), VerificationReviewer ──────────────
export const adminEventSchema = z.object({
  event_id: z.string(),
  representing_org_id: z.string(),      // see pendingEventSchema
  org_name: z.string(),
  title: z.string(),
  slug: z.string(),
  status: z.string(),
  is_featured: z.boolean(),
  starts_at: z.string(),
  created_at: z.string(),
  category: z.string().nullable(),
  subcategory: z.string().nullable(),
  visibility: z.string(),
  city: z.string(),
  venue_name: z.string(),
  capacity: z.number().nullable(),
  ends_at: z.string(),
  is_paid: z.boolean(),
  org_verification: z.string(),
  updated_at: z.string(),
  is_suspended: z.boolean(),
  suspended_reason: z.string().nullable(),
  is_hidden: z.boolean(),
  hidden_reason: z.string().nullable(),
  banner_key: z.string().nullable(),
  tickets_sold: z.number(),
  checked_in: z.number(),
  registrations_count: z.number(),
  revenue_paise: z.number(),
  currency: z.string()
});
export type AdminEvent = z.infer<typeof adminEventSchema>;

/*
 * The full event, as a reviewer needs to read it.
 *
 * `adminEventSchema` above is a LIST row — 29 fields, and not one of them is the event's description.
 * A reviewer deciding whether a public event may carry Kurx's name into discovery was shown a title, a
 * category, a city, a venue name, a capacity and a date. Everything a review is actually about — the
 * description, the rules, the terms and consent text, the eligibility gates, the registration windows,
 * the online/hybrid join details — was collected by the Create Event wizard, stored, and then read by
 * nobody before approval.
 *
 * No new endpoint and no new backend DTO: `GET /v1/events/{id}` already returns all of it, and
 * `CanViewAsync` already admits `kurx_admin` **and** `VerificationReviewer` to an event in any status
 * (D-191). The mapping simply stopped at the console. This is that missing leg.
 *
 * Deliberately `.passthrough()`-free and `.nullable()`-heavy: every group is optional on the wire
 * (D-265 made them additive), so a strict schema here would 500 the review queue for an event created
 * before they existed.
 */
const reviewGroupSchemas = {
  content: z.object({
    tagline: z.string().nullable(), short_description: z.string().nullable(),
    rules: z.string().nullable(), faq_json: z.string().nullable(),
    logo_url: z.string().nullable().optional(), thumbnail_url: z.string().nullable().optional(),
    promo_video_url: z.string().nullable().optional()
  }).partial().nullable().optional(),
  legal: z.object({
    terms_url: z.string().nullable(), terms_text: z.string().nullable(),
    code_of_conduct: z.string().nullable(), refund_policy: z.string().nullable(),
    cancellation_policy: z.string().nullable(), requires_consent: z.boolean(),
    consent_text: z.string().nullable()
  }).partial().nullable().optional(),
  schedule: z.object({
    registration_opens_at: z.string().nullable(), registration_closes_at: z.string().nullable(),
    checkin_opens_at: z.string().nullable(), checkin_closes_at: z.string().nullable(),
    result_date: z.string().nullable(), certificate_release_at: z.string().nullable(),
    auto_close: z.boolean()
  }).partial().nullable().optional(),
  location_detail: z.object({
    building: z.string().nullable(), floor: z.string().nullable(), room: z.string().nullable(),
    google_maps_url: z.string().nullable(), meeting_platform: z.string().nullable()
    // `meeting_password` is deliberately NOT read: it is shown only to confirmed registrants, and a
    // reviewer has no decision that depends on it.
  }).partial().nullable().optional(),
  eligibility: z.object({
    min_age: z.number().nullable(), max_age: z.number().nullable(),
    gender_restriction: z.string().nullable(), max_teams: z.number().nullable()
  }).partial().nullable().optional(),
  representing: z.object({
    org_id: z.string(), name: z.string(), slug: z.string(),
    logo_key: z.string().nullable(), is_verified: z.boolean()
  }).partial().nullable().optional()
};

export const reviewEventSchema = z.object({
  id: z.string(),
  title: z.string(),
  slug: z.string(),
  subtitle: z.string(),
  description: z.string(),
  status: z.string(),
  visibility: z.string(),
  starts_at: z.string(),
  ends_at: z.string(),
  timezone: z.string(),
  capacity: z.number().nullable(),
  event_mode: z.string(),
  online_url: z.string().nullable(),
  contact_email: z.string(),
  contact_phone: z.string(),
  website: z.string(),
  language: z.string(),
  settlement_currency: z.string(),
  venue: z.object({
    name: z.string().nullable(), address: z.string().nullable(), city: z.string().nullable()
  }).partial().nullable().optional(),
  banner_url: z.string().nullable().optional(),
  ...reviewGroupSchemas
});
export type ReviewEvent = z.infer<typeof reviewEventSchema>;

/** The reviewer's read of one event. Same route every organiser uses — the server decides who may see it. */
export async function getEventForReview(accessToken: string, eventId: string) {
  const { data } = await api.get(`/v1/events/${eventId}`, auth(accessToken));
  return reviewEventSchema.parse(data);
}

/*
 * The event's registration options, as a reviewer must read them (D-357).
 *
 * "Price ₹2,000, Quantity 50" is ambiguous: ₹2,000 for a team or for one of its members, and 50 teams
 * or 50 people. `pricing_unit`, `registration_mode` and the group bounds are what disambiguate it, and
 * they have been on this public route since D-020.
 */
export const reviewTicketTypeSchema = z.object({
  id: z.string(),
  name: z.string(),
  price_paise: z.number(),
  currency: z.string().nullable().optional(),
  pricing_unit: z.string().nullable().optional(),
  registration_mode: z.string().nullable().optional(),
  group_min: z.number().nullable().optional(),
  group_max: z.number().nullable().optional(),
  /// D-366 — the price bands of a team ticket. A reviewer approving a price has to see the price they
  /// are approving: on a banded ticket `price_paise` is only the cheapest band, so showing it alone
  /// would put a reviewer's name against a fee structure they never read.
  price_tiers: z.array(z.object({
    min_size: z.number(),
    max_size: z.number(),
    price_paise: z.number()
  })).nullable().optional(),
  quantity: z.number().nullable().optional(),
  sold: z.number().nullable().optional(),
  is_competition: z.boolean().nullable().optional()
});
export type ReviewTicketType = z.infer<typeof reviewTicketTypeSchema>;

export async function getEventTicketTypesForReview(accessToken: string, eventId: string) {
  const { data } = await api.get(`/v1/events/${eventId}/ticket-types`, auth(accessToken));
  return z.array(reviewTicketTypeSchema).parse(data);
}

/** Every axis the Event Management workspace's list/filter bar can send — all optional, mirrors
 *  `AdminEventListFilter` on the backend 1:1 so a new filter never needs a translation layer here. */
export type AdminEventListParams = {
  q?: string; status?: string; categoryId?: string; city?: string; visibility?: string;
  isPaid?: boolean; verifiedOnly?: boolean; dateFrom?: string; dateTo?: string;
  revenueMin?: number; revenueMax?: number; registrationsMin?: number; registrationsMax?: number;
  sort?: string; limit?: number; page?: number;
};

// D-187: the backend now returns {items, total} with real Page-based pagination — previously this endpoint
// hard-capped at 100 rows with no way to reach row 101, silently hiding every older event past that point on
// any platform with more than 100 events. `total` is what the pager renders against.
export const adminEventListResultSchema = z.object({ items: z.array(adminEventSchema), total: z.number() });
export async function listAdminEvents(accessToken: string, params?: AdminEventListParams) {
  const { data } = await api.get("/v1/admin/events", { ...auth(accessToken), params });
  return adminEventListResultSchema.parse(data);
}

export async function setEventFeatured(accessToken: string, eventId: string, featured: boolean) {
  const { data } = await api.post(`/v1/admin/events/${eventId}/${featured ? "feature" : "unfeature"}`, {}, auth(accessToken));
  return adminEventSchema.parse(data);
}

// D-186: moderation overrides — orthogonal to the content-lifecycle status. All return the updated row.
export async function suspendEvent(accessToken: string, eventId: string, reason?: string) {
  const { data } = await api.post(`/v1/admin/events/${eventId}/suspend`, { reason }, auth(accessToken));
  return adminEventSchema.parse(data);
}
export async function unsuspendEvent(accessToken: string, eventId: string) {
  const { data } = await api.post(`/v1/admin/events/${eventId}/unsuspend`, {}, auth(accessToken));
  return adminEventSchema.parse(data);
}
export async function hideEvent(accessToken: string, eventId: string, reason?: string) {
  const { data } = await api.post(`/v1/admin/events/${eventId}/hide`, { reason }, auth(accessToken));
  return adminEventSchema.parse(data);
}
export async function unhideEvent(accessToken: string, eventId: string) {
  const { data } = await api.post(`/v1/admin/events/${eventId}/unhide`, {}, auth(accessToken));
  return adminEventSchema.parse(data);
}

// D-186: organizer messaging — composition on the backend (NotifyAsync + audit), no new entity here either.
export async function messageOrganizer(accessToken: string, eventId: string, message: string) {
  await api.post(`/v1/admin/events/${eventId}/message`, { message }, auth(accessToken));
}
export async function warnOrganizer(accessToken: string, eventId: string, message: string) {
  await api.post(`/v1/admin/events/${eventId}/warn`, { message }, auth(accessToken));
}

// D-186: the event content-lifecycle actions (approve/reject/publish/unpublish/close/archive/cancel) are
// NOT admin-prefixed — they reuse `transitionEvent` above (the same org-scoped route every organizer uses,
// which already honours VerificationReviewer/kurx_admin per AdminEventEndpoints.cs's header comment). No
// admin-side duplicate of that call. Same for delete — reuses IEventService.DeleteDraftAsync, which the
// admin session's kurx_admin claim already bypasses org-membership on; Draft-only by design (D-186 §3:
// nothing has happened yet on a Draft, so extending soft-delete to a live/paid event is a separate,
// materially riskier decision this pass didn't make).
export async function deleteOrgEventAsAdmin(accessToken: string, orgId: string, eventId: string) {
  await api.delete(`/v1/orgs/${orgId}/events/${eventId}`, auth(accessToken));
}

// D-191: bulk moderation — one endpoint looping the SAME single-event methods above; never a parallel
// implementation. `reason` is required by the backend for suspend/hide/reject/archive.
export const bulkActionResultSchema = z.object({
  succeeded: z.array(z.string()),
  failed: z.array(z.object({ event_id: z.string(), error: z.string() }))
});
export type BulkActionResult = z.infer<typeof bulkActionResultSchema>;
export async function bulkManageEvents(
  accessToken: string,
  eventIds: string[],
  action: "suspend" | "unsuspend" | "hide" | "unhide" | "approve" | "reject" | "archive",
  reason?: string
) {
  const { data } = await api.post("/v1/admin/events/bulk", { eventIds, action, reason }, auth(accessToken));
  return bulkActionResultSchema.parse(data);
}

// D-191: reuses AdminEventListFilter (no second filter parser) — same params listAdminEvents sends, plus
// an optional comma-joined eventIds for "export selected." Fetched server-side (real Bearer auth, same as
// every other admin call) and handed to the client as text — never a token-bearing URL, which would leak
// via browser history/Referer/logs.
export async function exportAdminEventsCsv(accessToken: string, params: AdminEventListParams & { eventIds?: string }) {
  const { data } = await api.get("/v1/admin/events/export", { ...auth(accessToken), params, responseType: "text" });
  return data as string;
}

// D-191: Super Admin emergency edit — an exceptional path over the same update core the organizer PATCH
// uses; a mandatory reason and a full audit snapshot are what make it different, not a second update shape.
export async function emergencyEditEvent(accessToken: string, eventId: string, reason: string, input: Record<string, unknown>) {
  await api.post(`/v1/admin/events/${eventId}/emergency-edit`, { reason, input }, auth(accessToken));
}

// D-186: fraud/risk — GetRiskScoreAsync existed but was never wired to any endpoint; these are its first
// callers. `subjectType` is polymorphic ("Event" for the workspace's Overview/Moderation tabs,
// "Organization" for the Organizer tab) — same one FraudSignal table, same one score.
export async function getFraudScore(accessToken: string, subjectType: "Event" | "Organization", subjectId: string) {
  const { data } = await api.get("/v1/admin/fraud-signals/score", { ...auth(accessToken), params: { subjectType, subjectId } });
  return z.object({ subject_type: z.string(), subject_id: z.string(), risk_score: z.number() }).parse(data);
}
export async function getFraudScoresBatch(accessToken: string, subjectType: "Event" | "Organization", subjectIds: string[]) {
  if (subjectIds.length === 0) return [];
  const { data } = await api.get("/v1/admin/fraud-signals/scores", {
    ...auth(accessToken), params: { subjectType, subjectIds: subjectIds.join(",") }
  });
  return z.array(z.object({ subject_id: z.string(), risk_score: z.number() })).parse(data);
}

// D-186: the event workspace's Registrations/Tickets/Attendees/Organizer tabs reuse the SAME org-scoped
// endpoints web's organizer dashboard already calls — the admin session's own kurx_admin claim already
// bypasses the org-membership check on every one of these (EventService/TicketTypeService/etc. all take
// `isAdmin`), so this is a thin pass-through, never a rewritten query. Schemas intentionally match
// web/lib/api.ts's shapes exactly — same wire contract, same backend endpoint.
export const orgEventRowSchema = z.object({
  id: z.string(), title: z.string(), slug: z.string(), status: z.string(), visibility: z.string(),
  category_name: z.string().nullable(), venue_name: z.string(), city: z.string(), capacity: z.number().nullable(),
  is_paid: z.boolean(), tickets_sold: z.number(), checked_in: z.number(), revenue_paise: z.number(),
  starts_at: z.string(), updated_at: z.string()
});
export type OrgEventRow = z.infer<typeof orgEventRowSchema>;
export async function listOrgEventsAsAdmin(accessToken: string, orgId: string) {
  const { data } = await api.get(`/v1/orgs/${orgId}/events`, auth(accessToken));
  return z.object({ items: z.array(orgEventRowSchema), total: z.number() }).parse(data);
}

export const eventAnalyticsSchema = z.object({
  event_id: z.string(), view_count: z.number(), ticket_types: z.number(), tickets_issued: z.number(),
  checked_in: z.number(), orders_paid: z.number(), gross_paise: z.number()
});
export type EventAnalyticsAdmin = z.infer<typeof eventAnalyticsSchema>;
export async function getEventAnalyticsAsAdmin(accessToken: string, orgId: string, eventId: string) {
  const { data } = await api.get(`/v1/orgs/${orgId}/events/${eventId}/analytics`, auth(accessToken));
  return eventAnalyticsSchema.parse(data);
}

export const ticketTypeSchema = z.object({
  id: z.string(), event_id: z.string(), name: z.string(), price_paise: z.number(), pricing_unit: z.string(),
  registration_mode: z.string(), group_min: z.number().nullable(), group_max: z.number().nullable(),
  quantity: z.number(), sold: z.number(), available: z.number(), sale_starts: z.string(), sale_ends: z.string(),
  per_user_limit: z.number(), is_all_access: z.boolean(), is_competition: z.boolean()
});
export type TicketTypeAdmin = z.infer<typeof ticketTypeSchema>;
export async function listTicketTypesAsAdmin(accessToken: string, orgId: string, eventId: string) {
  const { data } = await api.get(`/v1/orgs/${orgId}/events/${eventId}/ticket-types`, auth(accessToken));
  return z.array(ticketTypeSchema).parse(data);
}

export const attendeeSchema = z.object({
  ticket_id: z.string(), code: z.string(), state: z.string(), checked_in_at: z.string().nullable(),
  buyer_name: z.string(), buyer_phone: z.string(), ticket_type_id: z.string(), ticket_type_name: z.string(),
  group_id: z.string().nullable(), group_number: z.number().nullable(), group_display_name: z.string().nullable(),
  answers: z.record(z.string().nullable())
});
export type AttendeeAdmin = z.infer<typeof attendeeSchema>;
export async function listAttendeesAsAdmin(accessToken: string, orgId: string, eventId: string) {
  const { data } = await api.get(`/v1/orgs/${orgId}/events/${eventId}/attendees`, auth(accessToken));
  return z.object({ items: z.array(attendeeSchema), total: z.number() }).parse(data);
}

// snake_case, matching every other response on the platform (D-259 addendum). This endpoint used to
// answer in camelCase — it forwards the WalletView record straight out of Results.Ok and the API had
// no naming policy — so this schema's snake_case threw a ZodError on every call and the org wallet
// panel could not render. The server now applies the platform convention to those records too.
export const walletSchema = z.object({
  org_id: z.string(), collected_paise: z.number(), available_paise: z.number(), advanced_paise: z.number(),
  reserved_paise: z.number(), settled_paise: z.number(), lifetime_earned_paise: z.number(),
  lifetime_withdrawn_paise: z.number(), updated_at: z.string(), currency: z.string()
});
export type WalletAdmin = z.infer<typeof walletSchema>;
// D-186: the one org-scoped service that had NO admin bypass anywhere — added here (see
// IWalletService.GetWalletAsync/GetLedgerAsync's new `isAdmin` param). Read-only; admin still cannot
// initiate a withdrawal on an org's behalf — that stayed deliberately un-admin-gated.
export async function getOrgWalletAsAdmin(accessToken: string, orgId: string) {
  const { data } = await api.get(`/v1/orgs/${orgId}/wallet`, auth(accessToken));
  return walletSchema.parse(data);
}

export const ledgerEntrySchema = z.object({
  id: z.string(), amount_paise: z.number(), state: z.string(), ref_type: z.string(), ref_id: z.string(),
  event_id: z.string(), currency: z.string(), created_at: z.string()
});
export type LedgerEntryAdmin = z.infer<typeof ledgerEntrySchema>;
export async function getOrgWalletLedgerAsAdmin(accessToken: string, orgId: string, page = 1, pageSize = 20) {
  const { data } = await api.get(`/v1/orgs/${orgId}/wallet/ledger`, { ...auth(accessToken), params: { page, pageSize } });
  return z.array(ledgerEntrySchema).parse(data);
}

// Lean subset of web/lib/api.ts's eventDetailSchema — the Media tab and Overview's publish date are
// the only reason the workspace needs this call; everything else already comes from AdminEventView.
export const eventMediaItemSchema = z.object({ id: z.string(), kind: z.string(), key: z.string(), caption: z.string(), sort: z.number() });
export const eventDetailForAdminSchema = z.object({
  media: z.array(eventMediaItemSchema), published_at: z.string().nullable()
});
export async function getEventDetailAsAdmin(accessToken: string, orgId: string, eventId: string) {
  const { data } = await api.get(`/v1/orgs/${orgId}/events/${eventId}`, auth(accessToken));
  return eventDetailForAdminSchema.parse(data);
}

// ── Audit log (D-062) — SuperAdmin / ReadOnlyAuditor ─────────────────────────
export const auditEntrySchema = z.object({
  id: z.string(),
  actor_type: z.string(),
  actor_id: z.string().nullable(),
  action: z.string(),
  entity: z.string(),
  entity_id: z.string().nullable(),
  details: z.string().nullable(),
  created_at: z.string()
});
export type AuditEntry = z.infer<typeof auditEntrySchema>;

export async function listAudit(accessToken: string, params?: { actor?: string; entity?: string; entityId?: string; action?: string }) {
  const { data } = await api.get("/v1/admin/audit", { ...auth(accessToken), params });
  return z.array(auditEntrySchema).parse(data);
}

// ── Platform analytics (D-063) — any staff (non-PII aggregates) ──────────────
const dayCount = z.object({ date: z.string(), count: z.number() });
export const analyticsSchema = z.object({
  window_days: z.number(),
  total_users: z.number(),
  total_orgs: z.number(),
  total_events: z.number(),
  new_users_in_window: z.number(),
  signups_by_day: z.array(dayCount),
  events_by_day: z.array(dayCount),
  events_by_status: z.array(z.object({ status: z.string(), count: z.number() })),
  top_organizers: z.array(z.object({ org_id: z.string(), name: z.string(), published_events: z.number() })),
  top_events: z.array(z.object({ event_id: z.string(), title: z.string(), views: z.number() }))
});
export type Analytics = z.infer<typeof analyticsSchema>;

export async function getAnalytics(accessToken: string, days = 30) {
  const { data } = await api.get("/v1/admin/analytics", { ...auth(accessToken), params: { days } });
  return analyticsSchema.parse(data);
}

// ── Dashboard summary (D-058) — any staff role ───────────────────────────────
export const dashboardSummarySchema = z.object({
  pending_org_verifications: z.number(),
  pending_membership_claims: z.number(),
  pending_events: z.number(),
  blacklist_entries: z.number(),
  staff_count: z.number(),
  new_users_24h: z.number(),
  total_users: z.number(),
  total_orgs: z.number(),
  total_events: z.number()
});
export type DashboardSummary = z.infer<typeof dashboardSummarySchema>;

// React-cached per request: the console layout (sidebar badges) and the dashboard page both need
// this, and without dedup that's two network round trips for the same data on every page view.
export const getDashboardSummary = cache(async (accessToken: string) => {
  const { data } = await api.get("/v1/admin/dashboard/summary", auth(accessToken));
  return dashboardSummarySchema.parse(data);
});

// ── Reports & moderation (D-059) — Moderation staff (Reviewer/Support/SuperAdmin) ──
export const reportSchema = z.object({
  id: z.string(),
  reporter_id: z.string(),
  entity_type: z.string(),
  entity_id: z.string(),
  reason: z.string(),
  details: z.string().nullable(),
  status: z.string(),
  resolved_by: z.string().nullable(),
  resolved_at: z.string().nullable(),
  created_at: z.string()
});
export type Report = z.infer<typeof reportSchema>;

export async function listReports(accessToken: string, status?: string) {
  const { data } = await api.get("/v1/admin/reports", { ...auth(accessToken), params: status ? { status } : {} });
  return z.array(reportSchema).parse(data);
}

export async function resolveReport(accessToken: string, id: string, dismiss: boolean) {
  await api.post(`/v1/admin/reports/${id}/${dismiss ? "dismiss" : "resolve"}`, {}, auth(accessToken));
}

// ── User administration & moderation (D-060) — Moderation staff ──────────────
export const adminUserSchema = z.object({
  id: z.string(),
  phone: z.string(),
  name: z.string(),
  username: z.string().nullable(),
  email: z.string().nullable(),
  suspended: z.boolean(),
  banned: z.boolean(),
  moderation_reason: z.string().nullable(),
  created_at: z.string()
});
export type AdminUser = z.infer<typeof adminUserSchema>;

// Empty q → the currently-suspended/banned accounts (backend default), not the whole table.
export async function listUsers(accessToken: string, q?: string) {
  const { data } = await api.get("/v1/admin/users", { ...auth(accessToken), params: q ? { q } : {} });
  return z.array(adminUserSchema).parse(data);
}

// action: "suspend" | "ban" | "unban"
export async function moderateUser(accessToken: string, userId: string, action: string, reason?: string) {
  const { data } = await api.post(`/v1/admin/users/${userId}/${action}`, { reason }, auth(accessToken));
  return adminUserSchema.parse(data);
}

// ── Security Center (Phase 2E) + factor lists (Phase 2A/2C) ──────────────────
// Staff account security. Reads run server-side (the console keeps the token server-side); mutations
// go through server actions in security-actions.ts.

export type SecurityOverview = {
  has_password: boolean;
  email: string | null;
  email_verified: boolean;
  phone: string | null;
  phone_verified: boolean;
  trusted_browsers: number;
  trusted_devices: number;
  passkeys: number;
  active_sessions: number;
  recovery_codes_remaining: number;
  step_up_satisfied: boolean;
  can_step_up: boolean;
};

export async function securityOverview(accessToken: string): Promise<SecurityOverview> {
  const { data } = await api.get("/v1/auth/security-center", auth(accessToken));
  return data as SecurityOverview;
}

export type SecurityActivityItem = { type: string; severity: string; context: string | null; created_at: string };

export async function securityActivity(accessToken: string, limit = 25): Promise<SecurityActivityItem[]> {
  const { data } = await api.get("/v1/auth/security-center/activity", { ...auth(accessToken), params: { limit } });
  return data as SecurityActivityItem[];
}

export async function signOutEverywhere(accessToken: string): Promise<number> {
  const { data } = await api.post("/v1/auth/security-center/sign-out-all", {}, auth(accessToken));
  return (data as { revoked: number }).revoked;
}

export type TrustedBrowserView = {
  id: string;
  label: string | null;
  browser: string | null;
  operatingSystem: string | null;
  ip: string | null;
  approxLocation: string | null;
  isCurrent: boolean;
  createdAt: string;
  lastUsedAt: string | null;
  expiresAt: string;
};

export async function listTrustedBrowsers(accessToken: string): Promise<TrustedBrowserView[]> {
  const { data } = await api.get("/v1/auth/trusted-browsers", auth(accessToken));
  return data as TrustedBrowserView[];
}

export async function revokeTrustedBrowser(accessToken: string, id: string) {
  await api.post(`/v1/auth/trusted-browsers/${id}/revoke`, {}, auth(accessToken));
}

export type DeviceView = { id: string; name: string | null; platform: string; state: string; lastSeenAt: string | null; createdAt: string };

export async function listDevices(accessToken: string): Promise<DeviceView[]> {
  const { data } = await api.get("/v1/auth/devices", auth(accessToken));
  return data as DeviceView[];
}

export async function revokeDevice(accessToken: string, id: string) {
  await api.post(`/v1/auth/devices/${id}/revoke`, {}, auth(accessToken));
}

export type SessionView = { id: string; deviceId: string | null; deviceName: string | null; platform: string | null; isCurrent: boolean; createdAt: string; lastRotatedAt: string | null };

export async function listSessions(accessToken: string): Promise<SessionView[]> {
  const { data } = await api.get("/v1/auth/sessions", auth(accessToken));
  return data as SessionView[];
}

export async function revokeSession(accessToken: string, id: string) {
  await api.post(`/v1/auth/sessions/${id}/revoke`, {}, auth(accessToken));
}

export async function recoveryCodesRemaining(accessToken: string): Promise<number> {
  const { data } = await api.get("/v1/auth/recovery-codes", auth(accessToken));
  return (data as { remaining: number }).remaining;
}

export async function generateRecoveryCodes(accessToken: string): Promise<string[]> {
  const { data } = await api.post("/v1/auth/recovery-codes", {}, auth(accessToken));
  return (data as { codes: string[] }).codes;
}

export type StepUpStatusResult = { satisfied: boolean; valid_until: string | null; can_step_up: boolean };

export async function stepUpStatus(accessToken: string): Promise<StepUpStatusResult> {
  const { data } = await api.get("/v1/auth/step-up/status", auth(accessToken));
  return data as StepUpStatusResult;
}

// ── Password lifecycle (Phase 2C/2D, D-126/D-127/D-129) ──────────────────────
// Staff password management: create/change (authenticated) + the anonymous reset ceremony. Same
// frozen endpoints and NIST policy as the web app.

export type PasswordStatus = { has_password: boolean; min_length: number; max_length: number };

export async function passwordStatus(accessToken: string): Promise<PasswordStatus> {
  const { data } = await api.get("/v1/auth/password/status", auth(accessToken));
  return data as PasswordStatus;
}

export async function setPassword(accessToken: string, password: string) {
  await api.post("/v1/auth/password/set", { password }, auth(accessToken));
}

export async function changePassword(accessToken: string, currentPassword: string, newPassword: string) {
  await api.post("/v1/auth/password/change", { currentPassword, newPassword }, auth(accessToken));
}

/** Anonymous — always succeeds (anti-enumeration), sending a reset OTP to the account if it exists. */
export async function startPasswordReset(identifier: string) {
  await api.post("/v1/auth/password/reset/start", { identifier });
}

/** Anonymous. OTP + recovery code (INV-B). On success returns a fresh session (the reset logs you in). */
export async function completePasswordReset(
  identifier: string,
  otpCode: string,
  newPassword: string,
  recoveryCode: string
) {
  const { data } = await api.post("/v1/auth/password/reset/complete", {
    identifier,
    otpCode,
    newPassword,
    recoveryCode
  });
  return data as { access_token: string; refresh_token: string; user_id: string };
}

// ── Event taxonomy / categories (D-037/D-188) — read is public, writes are KurxAdmin ──
export const categorySchema = z.object({
  id: z.string(),
  parent_id: z.string().nullable(),
  level: z.string(),
  name: z.string(),
  slug: z.string(),
  sort: z.number(),
  is_visible: z.boolean()
});
export type Category = z.infer<typeof categorySchema>;

// The public list omits hidden rows (the backend passes includeHidden: false); there is no
// admin-visible variant yet, so the console can only manage what the catalog exposes.
export async function listCategories(accessToken: string, params?: { level?: string; q?: string }) {
  const { data } = await api.get("/v1/categories", { ...auth(accessToken), params });
  return z.array(categorySchema).parse(data);
}

export type CategoryWriteBody = {
  description?: string; iconKey?: string; color?: string; badge?: string; searchKeywords?: string;
};

export async function createCategory(
  accessToken: string,
  body: { level: string; name: string; parentId?: string; sort?: number; isVisible?: boolean } & CategoryWriteBody
) {
  const { data } = await api.post("/v1/categories", body, auth(accessToken));
  return categorySchema.parse(data);
}

export async function updateCategory(
  accessToken: string,
  id: string,
  // parentId: Type-level reparent only — the backend ignores it for Audience/Category rows.
  body: { name?: string; sort?: number; isVisible?: boolean; parentId?: string } & CategoryWriteBody
) {
  const { data } = await api.patch(`/v1/categories/${id}`, body, auth(accessToken));
  return categorySchema.parse(data);
}

export async function deleteCategory(accessToken: string, id: string) {
  await api.delete(`/v1/categories/${id}`, auth(accessToken));
}

// D-188 (Platform Taxonomy Management) — admin-only richer view: lifecycle, metadata, audit fields, real
// usage counts. Registrations/attendees/revenue/views/favorites/trending are deliberate extension points
// on the backend DTO — always null today (see AdminCategoryView's doc comment), not fabricated here.
export const adminCategorySchema = z.object({
  id: z.string(), parent_id: z.string().nullable(), level: z.string(), name: z.string(), slug: z.string(),
  sort: z.number(), is_visible: z.boolean(), status: z.string(), description: z.string().nullable(),
  icon_key: z.string().nullable(), color: z.string().nullable(), badge: z.string().nullable(),
  search_keywords: z.string().nullable(), version: z.number(), created_at: z.string(), updated_at: z.string(),
  created_by: z.string().nullable(), updated_by: z.string().nullable(), usage_count: z.number(),
  registrations_count: z.number().nullable(), attendees_count: z.number().nullable(),
  revenue_paise: z.number().nullable(), views_count: z.number().nullable(),
  favorites_count: z.number().nullable(), is_trending: z.boolean().nullable()
});
export type AdminCategory = z.infer<typeof adminCategorySchema>;

export async function listAdminCategories(accessToken: string, params?: { level?: string; q?: string }) {
  const { data } = await api.get("/v1/admin/categories", { ...auth(accessToken), params });
  return z.array(adminCategorySchema).parse(data);
}

// Lifecycle overrides — orthogonal to IsVisible (see D-188). All return the updated row.
export async function disableCategory(accessToken: string, id: string) {
  const { data } = await api.post(`/v1/categories/${id}/disable`, {}, auth(accessToken));
  return categorySchema.parse(data);
}
export async function enableCategory(accessToken: string, id: string) {
  const { data } = await api.post(`/v1/categories/${id}/enable`, {}, auth(accessToken));
  return categorySchema.parse(data);
}
export async function archiveCategory(accessToken: string, id: string) {
  const { data } = await api.post(`/v1/categories/${id}/archive`, {}, auth(accessToken));
  return categorySchema.parse(data);
}
// Archived restores to Disabled, never straight to Active — a deliberate two-step (D-188).
export async function restoreCategory(accessToken: string, id: string) {
  const { data } = await api.post(`/v1/categories/${id}/restore`, {}, auth(accessToken));
  return categorySchema.parse(data);
}
export async function setCategoryVisibility(accessToken: string, id: string, visible: boolean) {
  const { data } = await api.post(`/v1/categories/${id}/visibility`, { visible }, auth(accessToken));
  return categorySchema.parse(data);
}
// Type-level only — the backend 400s (invalid_level) for an Audience/Category id.
export async function duplicateCategory(accessToken: string, id: string) {
  const { data } = await api.post(`/v1/categories/${id}/duplicate`, {}, auth(accessToken));
  return categorySchema.parse(data);
}

// parentId: null reorders the top-level Audience rows.
export async function reorderCategories(accessToken: string, parentId: string | null, order: { id: string; sort: number }[]) {
  await api.post("/v1/categories/reorder", { parent_id: parentId, order }, auth(accessToken));
}

// Type-level capability defaults — reuses the existing V3 Capability registry (~45 slugs), never a new
// capability catalog. "off" means no row exists for that slug.
export const typeCapabilitySchema = z.object({ slug: z.string(), name: z.string(), group_slug: z.string(), state: z.string() });
export type TypeCapability = z.infer<typeof typeCapabilitySchema>;

export async function getTypeCapabilities(accessToken: string, typeId: string) {
  const { data } = await api.get(`/v1/categories/${typeId}/capabilities`, auth(accessToken));
  return z.array(typeCapabilitySchema).parse(data);
}
export async function setTypeCapabilities(accessToken: string, typeId: string, capabilities: { slug: string; state: string }[]) {
  await api.put(`/v1/categories/${typeId}/capabilities`, { capabilities }, auth(accessToken));
}

// Export/Import — merge-only (D-188 refinement round 2 #1). Preview before Apply; Apply refuses to run at
// all if Preview reported any errors. Nodes are keyed by slug, not id, so a payload from one environment
// merges cleanly into another.
export const taxonomyExportNodeSchema = z.object({
  slug: z.string(), parent_slug: z.string().nullable(), level: z.string(), name: z.string(), sort: z.number(),
  is_visible: z.boolean(), description: z.string().nullable(), icon_key: z.string().nullable(),
  color: z.string().nullable(), badge: z.string().nullable(), search_keywords: z.string().nullable()
});
export type TaxonomyExportNode = z.infer<typeof taxonomyExportNodeSchema>;
export const taxonomyExportSchema = z.object({ nodes: z.array(taxonomyExportNodeSchema), exported_at: z.string() });
export type TaxonomyExport = z.infer<typeof taxonomyExportSchema>;

export async function exportTaxonomy(accessToken: string) {
  const { data } = await api.get("/v1/admin/categories/export", auth(accessToken));
  return taxonomyExportSchema.parse(data);
}

export const importPreviewSchema = z.object({
  to_create: z.array(taxonomyExportNodeSchema), to_update: z.array(taxonomyExportNodeSchema),
  conflicts: z.array(z.string()), errors: z.array(z.string())
});
export type ImportPreview = z.infer<typeof importPreviewSchema>;

export async function previewImportTaxonomy(accessToken: string, nodes: TaxonomyExportNode[]) {
  const { data } = await api.post("/v1/admin/categories/import/preview", { nodes }, auth(accessToken));
  return importPreviewSchema.parse(data);
}
export async function applyImportTaxonomy(accessToken: string, nodes: TaxonomyExportNode[]) {
  const { data } = await api.post("/v1/admin/categories/import/apply", { nodes }, auth(accessToken));
  return z.object({ created: z.number(), updated: z.number(), skipped: z.number() }).parse(data);
}

// ── Certificates (D-036/D-064) — event-scoped roster; the kurx_admin claim bypasses the org-role check ──
export const certificateSchema = z.object({
  id: z.string(),
  event_id: z.string(),
  verify_code: z.string(),
  user_id: z.string().nullable(),
  holder_name: z.string().nullable(),
  kind: z.string(),
  status: z.string(),
  is_revoked: z.boolean(),
  revoked_reason: z.string().nullable(),
  issued_at: z.string()
});
export type Certificate = z.infer<typeof certificateSchema>;

export async function listEventCertificates(accessToken: string, eventId: string) {
  const { data } = await api.get(`/v1/events/${eventId}/certificates`, auth(accessToken));
  return z.array(certificateSchema).parse(data);
}

export async function generateEventCertificates(accessToken: string, eventId: string) {
  const { data } = await api.post(`/v1/events/${eventId}/certificates/generate`, {}, auth(accessToken));
  return z.object({ generated: z.number() }).parse(data).generated;
}

export async function revokeCertificate(accessToken: string, certificateId: string, reason: string) {
  await api.post(`/v1/certificates/${certificateId}/revoke`, { reason }, auth(accessToken));
}

// ── Broadcast notification — fans out to EVERY user, synchronously, on the request thread ──
export async function broadcastNotification(
  accessToken: string,
  body: { title: string; message: string; dataJson?: string }
) {
  const { data } = await api.post("/v1/admin/notifications/broadcast", body, auth(accessToken));
  return z.object({ ok: z.boolean(), sent_to_users_count: z.number() }).parse(data).sent_to_users_count;
}

// ── Health — ASP.NET health report. Not under /v1, and camelCase unlike the rest of the API. ──
export const healthSchema = z.object({
  status: z.string(),
  totalDurationMs: z.number(),
  checks: z.array(
    z.object({
      name: z.string(),
      status: z.string(),
      description: z.string().nullable(),
      durationMs: z.number()
    })
  )
});
export type Health = z.infer<typeof healthSchema>;

/** 503 is a real answer here — an unhealthy report is the whole point — so it must not throw. */
export async function getHealth() {
  const { data } = await api.get("/health", {
    validateStatus: (s) => s === 200 || s === 503
  });
  return healthSchema.parse(data);
}

// ── Competition engine (V3 §10) ──────────────────────────────────────────────
// NOTE: these endpoints return C# records directly, so their JSON is **camelCase** — unlike every
// /v1/admin/* response above, which is hand-shaped snake_case. Do not "fix" these to snake_case.
// Cross-org access rides the kurx_admin claim (IsOrganiserAsync = isAdmin || event:manage), so the
// screens are SuperAdmin-gated.

export const stageSchema = z.object({
  id: z.string(),
  eventId: z.string(),
  sequence: z.number(),
  name: z.string(),
  format: z.string(),
  participantSource: z.string(),
  advancedFromStageId: z.string().nullable(),
  advancementRule: z.string(),
  advancementThreshold: z.number().nullable(),
  scoringPolicyId: z.string().nullable(),
  startsAt: z.string().nullable(),
  endsAt: z.string().nullable(),
  venueId: z.string().nullable(),
  mode: z.string(),
  resultsVisibility: z.string(),
  spectatorPoolId: z.string().nullable(),
  state: z.string(),
  participantCount: z.number()
});
export type Stage = z.infer<typeof stageSchema>;

/** `subjectName` is resolved server-side; `subjectId` is retained for traceability. Never null. */
export const stageParticipantSchema = z.object({
  id: z.string(),
  stageId: z.string(),
  subjectType: z.string(),
  subjectId: z.string(),
  subjectName: z.string(),
  seed: z.number().nullable(),
  advanced: z.boolean()
});
export type StageParticipant = z.infer<typeof stageParticipantSchema>;

export const stageResultSchema = z.object({
  id: z.string(),
  stageId: z.string(),
  subjectType: z.string(),
  subjectId: z.string(),
  subjectName: z.string(),
  rank: z.number(),
  finalScore: z.number().nullable(),
  scoreBreakdownJson: z.string().nullable(),
  state: z.string(),
  publishedAt: z.string().nullable(),
  correctionCount: z.number()
});
export type StageResult = z.infer<typeof stageResultSchema>;

// Participants are intentionally read as a count only: FixtureView reuses the *input* record
// FixtureSubjectInput, which carries no subjectName — rendering raw GUIDs is worse than a count.
// Tracked in docs/roadmap/README.md.
export const fixtureSchema = z.object({
  id: z.string(),
  stageId: z.string(),
  roundNo: z.number(),
  label: z.string().nullable(),
  venueId: z.string().nullable(),
  slotStart: z.string().nullable(),
  slotEnd: z.string().nullable(),
  state: z.string(),
  resultJson: z.string().nullable(),
  participants: z.array(z.unknown()),
  officials: z.array(z.string())
});
export type Fixture = z.infer<typeof fixtureSchema>;

export async function listStages(accessToken: string, eventId: string) {
  const { data } = await api.get(`/v1/events/${eventId}/stages`, auth(accessToken));
  return z.array(stageSchema).parse(data);
}

export async function getStage(accessToken: string, stageId: string) {
  const { data } = await api.get(`/v1/stages/${stageId}`, auth(accessToken));
  return stageSchema.parse(data);
}

export async function createStage(
  accessToken: string,
  eventId: string,
  body: {
    name: string; sequence?: number; format?: string; participantSource?: string;
    advancementRule?: string; advancementThreshold?: number; resultsVisibility?: string;
  }
) {
  const { data } = await api.post(`/v1/events/${eventId}/stages`, body, auth(accessToken));
  return stageSchema.parse(data);
}

/** `open` (Draft→Live) and `close` (Live→Closed) are the only transitions; anything else 400s. */
export async function transitionStage(accessToken: string, stageId: string, action: "open" | "close") {
  const { data } = await api.post(`/v1/stages/${stageId}/transition`, { action }, auth(accessToken));
  return stageSchema.parse(data);
}

export async function deleteStage(accessToken: string, stageId: string) {
  await api.delete(`/v1/stages/${stageId}`, auth(accessToken));
}

export async function listStageParticipants(accessToken: string, stageId: string) {
  const { data } = await api.get(`/v1/stages/${stageId}/participants`, auth(accessToken));
  return z.array(stageParticipantSchema).parse(data);
}

export async function seedFromRegistered(accessToken: string, stageId: string) {
  const { data } = await api.post(`/v1/stages/${stageId}/participants/seed-registered`, {}, auth(accessToken));
  return z.object({ added: z.number() }).parse(data).added;
}

export async function removeStageParticipant(accessToken: string, rowId: string) {
  await api.delete(`/v1/stages/participants/${rowId}`, auth(accessToken));
}

export async function listFixtures(accessToken: string, stageId: string) {
  const { data } = await api.get(`/v1/stages/${stageId}/fixtures`, auth(accessToken));
  return z.array(fixtureSchema).parse(data);
}

/** Admin sees every state (Provisional/Disputed included); the Published-only filter is public-only. */
export async function getStageResults(accessToken: string, stageId: string) {
  const { data } = await api.get(`/v1/stages/${stageId}/results`, auth(accessToken));
  return z.array(stageResultSchema).parse(data);
}

export async function computeStageResults(accessToken: string, stageId: string) {
  const { data } = await api.post(`/v1/stages/${stageId}/results/compute`, {}, auth(accessToken));
  return z.array(stageResultSchema).parse(data);
}

export async function publishStageResults(accessToken: string, stageId: string) {
  const { data } = await api.post(`/v1/stages/${stageId}/results/publish`, {}, auth(accessToken));
  return z.array(stageResultSchema).parse(data);
}

export async function advanceStage(accessToken: string, stageId: string) {
  const { data } = await api.post(`/v1/stages/${stageId}/advance`, {}, auth(accessToken));
  return z.object({ advanced: z.number() }).parse(data).advanced;
}

// ── Speakers (org-scoped, admin bypass via kurx_admin) ───────────────────────
// NOTE: snake_case — SpeakerEndpoints hand-shapes its JSON (`ToJson`), unlike the competition
// endpoints above which return records as camelCase. Verified against the endpoint, not assumed.
// `photo_key` is a storage key, NOT a URL: GET /v1/storage/{key} requires a signed `sig` and always
// responds application/octet-stream (deliberate anti-XSS), and nothing mints a GET signature for an
// arbitrary speaker photo — so the console shows whether a photo is set, never the image.
export const speakerSchema = z.object({
  id: z.string(),
  org_id: z.string(),
  name: z.string(),
  bio: z.string().nullable(),
  photo_key: z.string().nullable(),
  company: z.string().nullable(),
  role: z.string().nullable(),
  social_links_json: z.string().nullable()
});
export type Speaker = z.infer<typeof speakerSchema>;

/** Every speaker the org owns. No paging/filter params exist; the full set comes back ordered by name. */
export async function listOrgSpeakers(accessToken: string, orgId: string) {
  const { data } = await api.get(`/v1/orgs/${orgId}/speakers`, auth(accessToken));
  return z.array(speakerSchema).parse(data);
}

/** The subset assigned to one event, in the organiser's chosen order. */
export async function listEventSpeakers(accessToken: string, orgId: string, eventId: string) {
  const { data } = await api.get(`/v1/orgs/${orgId}/events/${eventId}/speakers`, auth(accessToken));
  return z.array(speakerSchema).parse(data);
}

export type SpeakerInput = {
  name: string; bio?: string; company?: string; role?: string; photoKey?: string; socialLinksJson?: string;
};

export async function createSpeaker(accessToken: string, orgId: string, body: SpeakerInput) {
  const { data } = await api.post(`/v1/orgs/${orgId}/speakers`, body, auth(accessToken));
  return speakerSchema.parse(data);
}

export async function updateSpeaker(accessToken: string, orgId: string, speakerId: string, body: SpeakerInput) {
  const { data } = await api.patch(`/v1/orgs/${orgId}/speakers/${speakerId}`, body, auth(accessToken));
  return speakerSchema.parse(data);
}

export async function deleteSpeaker(accessToken: string, orgId: string, speakerId: string) {
  await api.delete(`/v1/orgs/${orgId}/speakers/${speakerId}`, auth(accessToken));
}

/** Idempotent server-side: assigning an already-assigned speaker is a no-op, not an error. */
export async function assignSpeakerToEvent(accessToken: string, orgId: string, eventId: string, speakerId: string) {
  await api.post(`/v1/orgs/${orgId}/events/${eventId}/speakers`, { speakerId }, auth(accessToken));
}

export async function removeSpeakerFromEvent(accessToken: string, orgId: string, eventId: string, speakerId: string) {
  await api.delete(`/v1/orgs/${orgId}/events/${eventId}/speakers/${speakerId}`, auth(accessToken));
}

// ── Sponsors (org-scoped, admin bypass via kurx_admin) ───────────────────────
// snake_case, hand-shaped by SponsorEndpoints.ToJson — same as speakers, unlike the camelCase
// competition records. `tier` is emitted LOWERCASE (s.Tier.ToLowerInvariant()) while the input parse
// is case-insensitive, so read lowercase and either casing is accepted on write.
// `logo_key` is a storage key, not a URL — same constraint as a speaker's photo_key.
export const sponsorSchema = z.object({
  id: z.string(),
  org_id: z.string(),
  name: z.string(),
  logo_key: z.string().nullable(),
  website: z.string().nullable(),
  tier: z.string(),
  priority: z.number()
});
export type Sponsor = z.infer<typeof sponsorSchema>;

/** Whole roster, ordered by tier then priority. No paging/search params exist. */
export async function listOrgSponsors(accessToken: string, orgId: string) {
  const { data } = await api.get(`/v1/orgs/${orgId}/sponsors`, auth(accessToken));
  return z.array(sponsorSchema).parse(data);
}

export async function listEventSponsors(accessToken: string, orgId: string, eventId: string) {
  const { data } = await api.get(`/v1/orgs/${orgId}/events/${eventId}/sponsors`, auth(accessToken));
  return z.array(sponsorSchema).parse(data);
}

export type SponsorInput = {
  name: string; website?: string; tier?: string; priority?: number; logoKey?: string;
};

export async function createSponsor(accessToken: string, orgId: string, body: SponsorInput) {
  const { data } = await api.post(`/v1/orgs/${orgId}/sponsors`, body, auth(accessToken));
  return sponsorSchema.parse(data);
}

export async function updateSponsor(accessToken: string, orgId: string, sponsorId: string, body: SponsorInput) {
  const { data } = await api.patch(`/v1/orgs/${orgId}/sponsors/${sponsorId}`, body, auth(accessToken));
  return sponsorSchema.parse(data);
}

export async function deleteSponsor(accessToken: string, orgId: string, sponsorId: string) {
  await api.delete(`/v1/orgs/${orgId}/sponsors/${sponsorId}`, auth(accessToken));
}

/** Idempotent: re-assigning an already-linked sponsor updates its sort rather than erroring. */
export async function assignSponsorToEvent(accessToken: string, orgId: string, eventId: string, sponsorId: string) {
  await api.post(`/v1/orgs/${orgId}/events/${eventId}/sponsors`, { sponsorId }, auth(accessToken));
}

export async function removeSponsorFromEvent(accessToken: string, orgId: string, eventId: string, sponsorId: string) {
  await api.delete(`/v1/orgs/${orgId}/events/${eventId}/sponsors/${sponsorId}`, auth(accessToken));
}
