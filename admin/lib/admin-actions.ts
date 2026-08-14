"use server";

import { revalidatePath } from "next/cache";
import { currentSession } from "@/lib/session";
import {
  reviewOrgVerification, reviewMembershipClaim, suspendOrg, blacklistOrg, mergeOrgs,
  addBlacklist, removeBlacklist, recordFraudSignal, transitionEvent, resolveReport, moderateUser,
  setEventFeatured, suspendEvent, unsuspendEvent, hideEvent, unhideEvent, messageOrganizer, warnOrganizer,
  deleteOrgEventAsAdmin, bulkManageEvents, exportAdminEventsCsv, emergencyEditEvent, apiErrorMessage,
  type AdminEventListParams,
  createCategory, updateCategory, deleteCategory,
  disableCategory, enableCategory, archiveCategory, restoreCategory, setCategoryVisibility,
  duplicateCategory, reorderCategories, getTypeCapabilities, setTypeCapabilities,
  exportTaxonomy, previewImportTaxonomy, applyImportTaxonomy, type TaxonomyExportNode,
  generateEventCertificates, revokeCertificate, broadcastNotification,
  createStage, transitionStage, deleteStage, seedFromRegistered, removeStageParticipant,
  computeStageResults, publishStageResults, advanceStage,
  createSpeaker, updateSpeaker, deleteSpeaker, assignSpeakerToEvent, removeSpeakerFromEvent,
  createSponsor, updateSponsor, deleteSponsor, assignSponsorToEvent, removeSponsorFromEvent,
  setReviewChecklistItem, reviewEventAuthorization, recordFinancialReview   // D-266 M7
} from "@/lib/api";

// Every action re-reads the live session; the backend also enforces VerificationReviewer on each
// endpoint (defense in depth), so a non-reviewer's submit fails server-side regardless.
async function token(): Promise<string | null> {
  const s = await currentSession();
  return s?.accessToken ?? null;
}

function str(formData: FormData, key: string): string | undefined {
  const v = formData.get(key);
  return typeof v === "string" && v.length > 0 ? v : undefined;
}

/** Numeric field, or `fallback` when absent/blank. A non-numeric value is treated as absent so a
 *  typo can never silently reorder the catalog with NaN. */
function numberOr(formData: FormData, key: string, fallback: number | undefined) {
  const raw = str(formData, key);
  if (raw === undefined) return fallback;
  const n = Number(raw);
  return Number.isFinite(n) ? n : fallback;
}

export async function reviewOrgAction(orgId: string, _: unknown, formData: FormData) {
  const t = await token();
  if (!t) return { error: "Session expired." };
  const decision = str(formData, "decision");
  if (!decision) return { error: "No decision." };
  try {
    await reviewOrgVerification(t, orgId, { decision, notes: str(formData, "notes") });
  } catch (err) {
    return { error: apiErrorMessage(err) };
  }
  revalidatePath("/verification");
  return { ok: true };
}

export async function reviewClaimAction(claimId: string, _: unknown, formData: FormData) {
  const t = await token();
  if (!t) return { error: "Session expired." };
  const decision = str(formData, "decision");
  if (!decision) return { error: "No decision." };
  try {
    await reviewMembershipClaim(t, claimId, { decision, notes: str(formData, "notes") });
  } catch (err) {
    return { error: apiErrorMessage(err) };
  }
  revalidatePath("/verification");
  return { ok: true };
}

// ── Org lifecycle (M12): suspend (reversible) / blacklist (hard) / merge ──────
export async function moderateOrgAction(_: unknown, formData: FormData) {
  const t = await token();
  if (!t) return { error: "Session expired." };
  const orgId = str(formData, "orgId");
  const op = str(formData, "op") ?? "suspend";
  if (!orgId) return { error: "Enter an organization ID." };
  try {
    if (op === "blacklist") await blacklistOrg(t, orgId, str(formData, "reason"));
    else await suspendOrg(t, orgId, str(formData, "reason"));
  } catch (err) {
    return { error: apiErrorMessage(err) };
  }
  revalidatePath("/organizations");
  return { ok: true, op };
}

export async function mergeOrgsAction(_: unknown, formData: FormData) {
  const t = await token();
  if (!t) return { error: "Session expired." };
  const dup = str(formData, "duplicateOrgId");
  const canonical = str(formData, "canonicalOrgId");
  if (!dup || !canonical) return { error: "Enter both organization IDs." };
  if (dup === canonical) return { error: "Duplicate and canonical must be different orgs." };
  try {
    await mergeOrgs(t, dup, canonical);
  } catch (err) {
    return { error: apiErrorMessage(err) };
  }
  revalidatePath("/organizations");
  return { ok: true };
}

// ── Event approval (M8, D-057): approve = publish, reject = reject ────────────
export async function reviewEventAction(orgId: string, eventId: string, _: unknown, formData: FormData) {
  const t = await token();
  if (!t) return { error: "Session expired." };
  const action = str(formData, "action");
  if (!action) return { error: "No action." };
  // D-266 M4: reason code and notes ride along for the actions that need them. Not validated here —
  // the backend rejects a missing reason or notes, and duplicating that rule is how the two drift.
  const reasonCode = str(formData, "reasonCode") || undefined;
  const notes = str(formData, "notes") || undefined;
  try {
    await transitionEvent(t, orgId, eventId, action, reasonCode as never, notes);
  } catch (err) {
    return { error: apiErrorMessage(err) };
  }
  revalidatePath("/events/pending");
  revalidatePath("/events/review");
  // The action rides back so the toast can name where the event went. Without it the row simply
  // vanished from the tab and the reviewer had no confirmation of their own decision.
  return { ok: true as const, action };
}

/** D-266 M7 — tick or untick one checklist item. The backend refuses a key that is not on this event's
 *  live checklist (`unknown_checklist_item`), so the console never has to know the item list. */
export async function setReviewChecklistItemAction(eventId: string, itemKey: string, checked: boolean) {
  const t = await token();
  if (!t) return { error: "Session expired." };
  try {
    const checklist = await setReviewChecklistItem(t, eventId, itemKey, checked);
    revalidatePath("/events/review");
    return { ok: true as const, checklist };
  } catch (err) {
    return { error: apiErrorMessage(err) };
  }
}

/** D-266 M5/M7 — the reviewer's verdict on institutional authorization. Distinct from the event's own
 *  review: this decides whether the named institution consented, not whether the event may run. */
export async function reviewEventAuthorizationAction(
  eventId: string, _: unknown, formData: FormData,
) {
  const t = await token();
  if (!t) return { error: "Session expired." };
  const decision = str(formData, "decision") as "approve" | "reject" | "request_changes" | "";
  if (!decision) return { error: "No decision." };
  // Reason and notes ride along; the backend enforces which are required, and duplicating that rule here
  // is how the two drift.
  const reasonCode = str(formData, "reasonCode") || undefined;
  const notes = str(formData, "notes") || undefined;
  try {
    await reviewEventAuthorization(t, eventId, decision, reasonCode, notes);
  } catch (err) {
    return { error: apiErrorMessage(err) };
  }
  revalidatePath("/events/review");
  return { ok: true };
}

// ── Global event management (D-061/D-186): feature/unfeature, moderation overrides, or a forced
// content-lifecycle transition — one action dispatcher, matching the shape this file already uses. ──
export async function manageEventAction(orgId: string, eventId: string, _: unknown, formData: FormData) {
  const t = await token();
  if (!t) return { error: "Session expired." };
  const action = formData.get("action");
  if (typeof action !== "string" || !action) return { error: "No action." };
  const reason = str(formData, "reason");
  try {
    if (action === "feature") await setEventFeatured(t, eventId, true);
    else if (action === "unfeature") await setEventFeatured(t, eventId, false);
    else if (action === "suspend") await suspendEvent(t, eventId, reason);
    else if (action === "unsuspend") await unsuspendEvent(t, eventId);
    else if (action === "hide") await hideEvent(t, eventId, reason);
    else if (action === "unhide") await unhideEvent(t, eventId);
    else await transitionEvent(t, orgId, eventId, action);   // publish / reject / unpublish / close / archive / cancel
  } catch (err) {
    return { error: apiErrorMessage(err) };
  }
  revalidatePath("/events");
  return { ok: true };
}

// D-186: organizer messaging — composition on the backend (NotifyAsync + audit), no new entity here.
export async function messageOrganizerAction(eventId: string, _: unknown, formData: FormData) {
  const t = await token();
  if (!t) return { error: "Session expired." };
  const message = str(formData, "message");
  if (!message) return { error: "A message is required." };
  try {
    await messageOrganizer(t, eventId, message);
  } catch (err) {
    return { error: apiErrorMessage(err) };
  }
  return { ok: true };
}

export async function warnOrganizerAction(eventId: string, _: unknown, formData: FormData) {
  const t = await token();
  if (!t) return { error: "Session expired." };
  const message = str(formData, "message");
  if (!message) return { error: "A message is required." };
  try {
    await warnOrganizer(t, eventId, message);
  } catch (err) {
    return { error: apiErrorMessage(err) };
  }
  revalidatePath("/events");
  return { ok: true };
}

// D-186: reuses DeleteDraftAsync — Draft-only, see the note on deleteOrgEventAsAdmin in lib/api.ts.
// Callable directly (not via useFormState) from a ConfirmDialog's onConfirm — Server Actions are plain
// async functions, no <form> required to invoke one.
export async function deleteDraftEventAction(orgId: string, eventId: string) {
  const t = await token();
  if (!t) return { error: "Session expired." };
  try {
    await deleteOrgEventAsAdmin(t, orgId, eventId);
  } catch (err) {
    return { error: apiErrorMessage(err) };
  }
  revalidatePath("/events");
  return { ok: true };
}

// D-191: one dispatcher for the bulk toolbar — loops the SAME single-event calls above on the backend,
// never a parallel implementation here either.
export async function bulkManageEventsAction(
  eventIds: string[],
  action: "suspend" | "unsuspend" | "hide" | "unhide" | "approve" | "reject" | "archive",
  reason?: string
) {
  const t = await token();
  if (!t) return { error: "Session expired." };
  try {
    const result = await bulkManageEvents(t, eventIds, action, reason);
    revalidatePath("/events");
    return { ok: true, result };
  } catch (err) {
    return { error: apiErrorMessage(err) };
  }
}

// D-191: fetched server-side (real Bearer auth) and handed back as plain text — the client component
// triggers the actual file download via a Blob, so the access token never appears in a URL.
export async function exportAdminEventsCsvAction(params: AdminEventListParams & { eventIds?: string }) {
  const t = await token();
  if (!t) return { error: "Session expired." };
  try {
    const csv = await exportAdminEventsCsv(t, params);
    return { ok: true, csv };
  } catch (err) {
    return { error: apiErrorMessage(err) };
  }
}

// D-191: Super Admin emergency edit — mandatory reason, full audit snapshot on the backend; this action is
// just the transport, the same shape every other action in this file already uses.
export async function emergencyEditEventAction(eventId: string, reason: string, input: Record<string, unknown>) {
  const t = await token();
  if (!t) return { error: "Session expired." };
  if (!reason.trim()) return { error: "A reason is required." };
  try {
    await emergencyEditEvent(t, eventId, reason, input);
  } catch (err) {
    return { error: apiErrorMessage(err) };
  }
  revalidatePath("/events");
  return { ok: true };
}

// ── Fraud (M13) ──────────────────────────────────────────────────────────────
export async function addBlacklistAction(_: unknown, formData: FormData) {
  const t = await token();
  if (!t) return { error: "Session expired." };
  const kind = str(formData, "kind");
  const value = str(formData, "value");
  if (!kind || !value) return { error: "Kind and value are required." };
  try {
    await addBlacklist(t, { kind, value, reason: str(formData, "reason") });
  } catch (err) {
    return { error: apiErrorMessage(err) };
  }
  revalidatePath("/blacklist");
  return { ok: true };
}

export async function removeBlacklistAction(id: string, _: unknown, _formData: FormData) {
  const t = await token();
  if (!t) return { error: "Session expired." };
  try {
    await removeBlacklist(t, id);
  } catch (err) {
    return { error: apiErrorMessage(err) };
  }
  revalidatePath("/blacklist");
  return { ok: true };
}

// ── Reports & moderation (D-059) — resolve (dismiss=false) or dismiss (dismiss=true) ──
export async function resolveReportAction(id: string, _: unknown, formData: FormData) {
  const t = await token();
  if (!t) return { error: "Session expired." };
  const dismiss = formData.get("dismiss") === "true";
  try {
    await resolveReport(t, id, dismiss);
  } catch (err) {
    return { error: apiErrorMessage(err) };
  }
  revalidatePath("/reports");
  return { ok: true };
}

// ── User moderation (D-060) — action from the clicked button (suspend/ban/unban) ──
export async function moderateUserAction(userId: string, _: unknown, formData: FormData) {
  const t = await token();
  if (!t) return { error: "Session expired." };
  const action = formData.get("action");
  if (typeof action !== "string" || !action) return { error: "No action." };
  const reasonRaw = formData.get("reason");
  const reason = typeof reasonRaw === "string" && reasonRaw.trim() ? reasonRaw.trim() : undefined;
  try {
    await moderateUser(t, userId, action, reason);
  } catch (err) {
    return { error: apiErrorMessage(err) };
  }
  revalidatePath("/users");
  // The detail route is a separate cache entry — revalidating /users alone would leave a moderation
  // performed from /users/[id] showing the old status on that page.
  revalidatePath("/users/[id]", "page");
  return { ok: true };
}

export async function recordSignalAction(_: unknown, formData: FormData) {
  const t = await token();
  if (!t) return { error: "Session expired." };
  const subjectType = str(formData, "subjectType");
  const subjectId = str(formData, "subjectId");
  const kind = str(formData, "kind");
  const scoreRaw = str(formData, "score");
  if (!subjectType || !subjectId || !kind) return { error: "Subject type, ID and kind are required." };
  const score = Number(scoreRaw ?? "");
  if (!Number.isFinite(score)) return { error: "Score must be a number." };
  try {
    const { risk_score } = await recordFraudSignal(t, {
      subjectType, subjectId, kind, value: str(formData, "value"), score
    });
    revalidatePath("/risk");
    return { ok: true, riskScore: risk_score };
  } catch (err) {
    return { error: apiErrorMessage(err) };
  }
}

// ── Categories / Event Taxonomy (D-037/D-188) — writes are KurxAdmin ──
// The legacy flat /categories screen has been retired (parity verified — D-188 decision 9); this is the
// one source of truth now.
function revalidateTaxonomy() {
  revalidatePath("/platform/event-taxonomy");
}

export async function createCategoryAction(_: unknown, formData: FormData) {
  const t = await token();
  if (!t) return { error: "Session expired." };
  const level = str(formData, "level");
  const name = str(formData, "name");
  if (!level || !name) return { error: "Level and name are required." };
  if (name.length < 2 || name.length > 100) return { error: "Name must be 2–100 characters." };
  try {
    await createCategory(t, {
      level,
      name,
      parentId: str(formData, "parentId"),
      sort: numberOr(formData, "sort", 0),
      // Standard checkbox semantics: absent from the payload means unchecked, i.e. hidden.
      isVisible: formData.get("isVisible") === "true",
      description: str(formData, "description"),
      iconKey: str(formData, "iconKey"),
      color: str(formData, "color"),
      badge: str(formData, "badge"),
      searchKeywords: str(formData, "searchKeywords")
    });
  } catch (err) {
    return { error: apiErrorMessage(err) };
  }
  revalidateTaxonomy();
  return { ok: true };
}

export async function updateCategoryAction(id: string, _: unknown, formData: FormData) {
  const t = await token();
  if (!t) return { error: "Session expired." };
  const name = str(formData, "name");
  if (name && (name.length < 2 || name.length > 100)) return { error: "Name must be 2–100 characters." };
  try {
    // The edit form always renders every field, so an absent checkbox is a deliberate "hide", not a
    // partial payload — send it as false rather than leaving the flag unchanged.
    await updateCategory(t, id, {
      name,
      sort: numberOr(formData, "sort", undefined),
      isVisible: formData.get("isVisible") === "true",
      description: str(formData, "description"),
      iconKey: str(formData, "iconKey"),
      color: str(formData, "color"),
      badge: str(formData, "badge"),
      searchKeywords: str(formData, "searchKeywords"),
      // Type-level reparent only — the backend ignores this for Audience/Category rows.
      parentId: str(formData, "parentId")
    });
  } catch (err) {
    return { error: apiErrorMessage(err) };
  }
  revalidateTaxonomy();
  return { ok: true };
}

/** Refused by the backend with 409 when the category is in use or still has children. */
export async function deleteCategoryAction(id: string) {
  const t = await token();
  if (!t) return { error: "Session expired." };
  try {
    await deleteCategory(t, id);
  } catch (err) {
    return { error: apiErrorMessage(err) };
  }
  revalidateTaxonomy();
  return { ok: true };
}

// D-188 — lifecycle overrides (orthogonal to visibility), reorder, duplicate, capabilities, import/export.
// Each mutation is its own action/endpoint (never one mega-action) — see the plan's permissions refinement:
// this is what makes a future narrower "TaxonomyManager" role a policy change, not a rewrite.
export async function disableCategoryAction(id: string) {
  const t = await token();
  if (!t) return { error: "Session expired." };
  try { await disableCategory(t, id); } catch (err) { return { error: apiErrorMessage(err) }; }
  revalidateTaxonomy();
  return { ok: true };
}
export async function enableCategoryAction(id: string) {
  const t = await token();
  if (!t) return { error: "Session expired." };
  try { await enableCategory(t, id); } catch (err) { return { error: apiErrorMessage(err) }; }
  revalidateTaxonomy();
  return { ok: true };
}
export async function archiveCategoryAction(id: string) {
  const t = await token();
  if (!t) return { error: "Session expired." };
  try { await archiveCategory(t, id); } catch (err) { return { error: apiErrorMessage(err) }; }
  revalidateTaxonomy();
  return { ok: true };
}
export async function restoreCategoryAction(id: string) {
  const t = await token();
  if (!t) return { error: "Session expired." };
  try { await restoreCategory(t, id); } catch (err) { return { error: apiErrorMessage(err) }; }
  revalidateTaxonomy();
  return { ok: true };
}
export async function setCategoryVisibilityAction(id: string, visible: boolean) {
  const t = await token();
  if (!t) return { error: "Session expired." };
  try { await setCategoryVisibility(t, id, visible); } catch (err) { return { error: apiErrorMessage(err) }; }
  revalidateTaxonomy();
  return { ok: true };
}
/** Type-level only — the backend 400s (invalid_level) for an Audience/Category id. */
export async function duplicateCategoryAction(id: string) {
  const t = await token();
  if (!t) return { error: "Session expired." };
  try { await duplicateCategory(t, id); } catch (err) { return { error: apiErrorMessage(err) }; }
  revalidateTaxonomy();
  return { ok: true };
}

export async function reorderCategoriesAction(parentId: string | null, order: { id: string; sort: number }[]) {
  const t = await token();
  if (!t) return { error: "Session expired." };
  try { await reorderCategories(t, parentId, order); } catch (err) { return { error: apiErrorMessage(err) }; }
  revalidateTaxonomy();
  return { ok: true };
}

// Client components never hold the session token directly (it's httpOnly) — this is the read-side
// counterpart to setTypeCapabilitiesAction so the detail sheet can load a Type's current capabilities.
export async function getTypeCapabilitiesAction(typeId: string) {
  const t = await token();
  if (!t) return { error: "Session expired." };
  try {
    const capabilities = await getTypeCapabilities(t, typeId);
    return { ok: true, capabilities };
  } catch (err) {
    return { error: apiErrorMessage(err) };
  }
}

export async function setTypeCapabilitiesAction(typeId: string, capabilities: { slug: string; state: string }[]) {
  const t = await token();
  if (!t) return { error: "Session expired." };
  try { await setTypeCapabilities(t, typeId, capabilities); } catch (err) { return { error: apiErrorMessage(err) }; }
  revalidateTaxonomy();
  return { ok: true };
}

export async function exportTaxonomyAction() {
  const t = await token();
  if (!t) return { error: "Session expired." };
  try {
    const data = await exportTaxonomy(t);
    return { ok: true, data };
  } catch (err) {
    return { error: apiErrorMessage(err) };
  }
}

/** Never destructive — preview only, no mutation (D-188 refinement round 2 #1). */
export async function previewImportTaxonomyAction(nodes: TaxonomyExportNode[]) {
  const t = await token();
  if (!t) return { error: "Session expired." };
  try {
    const preview = await previewImportTaxonomy(t, nodes);
    return { ok: true, preview };
  } catch (err) {
    return { error: apiErrorMessage(err) };
  }
}

/** Refuses to run (backend 400 import_has_errors) if the preview reported any hard errors. */
export async function applyImportTaxonomyAction(nodes: TaxonomyExportNode[]) {
  const t = await token();
  if (!t) return { error: "Session expired." };
  try {
    const result = await applyImportTaxonomy(t, nodes);
    revalidateTaxonomy();
    return { ok: true, result };
  } catch (err) {
    return { error: apiErrorMessage(err) };
  }
}

// ── Certificates (D-036/D-064) — bulk generate for an event, revoke one ──
export async function generateCertificatesAction(eventId: string) {
  const t = await token();
  if (!t) return { error: "Session expired." };
  try {
    const generated = await generateEventCertificates(t, eventId);
    revalidatePath("/certificates");
    return { ok: true, generated };
  } catch (err) {
    return { error: apiErrorMessage(err) };
  }
}

export async function revokeCertificateAction(certificateId: string, reason: string) {
  const t = await token();
  if (!t) return { error: "Session expired." };
  if (!reason.trim()) return { error: "A revocation reason is required." };
  try {
    await revokeCertificate(t, certificateId, reason.trim());
  } catch (err) {
    return { error: apiErrorMessage(err) };
  }
  revalidatePath("/certificates");
  return { ok: true };
}

// ── Broadcast — reaches every user on the platform; irreversible once sent ──
export async function broadcastAction(_: unknown, formData: FormData) {
  const t = await token();
  if (!t) return { error: "Session expired." };
  const title = str(formData, "title");
  const message = str(formData, "message");
  if (!title || !message) return { error: "Title and message are required." };
  if (title.length > 200) return { error: "Title must not exceed 200 characters." };
  if (message.length > 2000) return { error: "Message must not exceed 2000 characters." };
  try {
    const sent = await broadcastNotification(t, { title, message, dataJson: str(formData, "dataJson") });
    return { ok: true, sent };
  } catch (err) {
    return { error: apiErrorMessage(err) };
  }
}

// ── Competition engine (V3 §10) — SuperAdmin; the backend gate is
// IsOrganiserAsync = isAdmin || event:manage, so a non-SuperAdmin submit fails server-side.
export async function createStageAction(eventId: string, _: unknown, formData: FormData) {
  const t = await token();
  if (!t) return { error: "Session expired." };
  const name = str(formData, "name");
  if (!name) return { error: "Stage name is required." };
  try {
    await createStage(t, eventId, {
      name,
      sequence: numberOr(formData, "sequence", undefined),
      format: str(formData, "format"),
      participantSource: str(formData, "participantSource"),
      advancementRule: str(formData, "advancementRule"),
      advancementThreshold: numberOr(formData, "advancementThreshold", undefined),
      resultsVisibility: str(formData, "resultsVisibility")
    });
  } catch (err) {
    return { error: apiErrorMessage(err) };
  }
  revalidatePath("/competitions");
  return { ok: true };
}

/** Draft --open--> Live --close--> Closed. Any other pairing returns invalid_transition. */
export async function transitionStageAction(stageId: string, action: "open" | "close") {
  const t = await token();
  if (!t) return { error: "Session expired." };
  try {
    await transitionStage(t, stageId, action);
  } catch (err) {
    return { error: apiErrorMessage(err) };
  }
  revalidatePath("/competitions");
  revalidatePath("/competitions/[stageId]", "page");
  return { ok: true };
}

/** Refused with has_published_results / is_advancement_source — surfaced, not swallowed. */
export async function deleteStageAction(stageId: string) {
  const t = await token();
  if (!t) return { error: "Session expired." };
  try {
    await deleteStage(t, stageId);
  } catch (err) {
    return { error: apiErrorMessage(err) };
  }
  revalidatePath("/competitions");
  return { ok: true };
}

export async function seedParticipantsAction(stageId: string) {
  const t = await token();
  if (!t) return { error: "Session expired." };
  try {
    const added = await seedFromRegistered(t, stageId);
    revalidatePath("/competitions/[stageId]", "page");
    return { ok: true, added };
  } catch (err) {
    return { error: apiErrorMessage(err) };
  }
}

export async function removeParticipantAction(rowId: string) {
  const t = await token();
  if (!t) return { error: "Session expired." };
  try {
    await removeStageParticipant(t, rowId);
  } catch (err) {
    return { error: apiErrorMessage(err) };
  }
  revalidatePath("/competitions/[stageId]", "page");
  return { ok: true };
}

/** compute → already_published / nothing_to_score · publish → nothing_to_publish
 *  advance → manual_advancement / no_next_stage / results_not_published */
export async function stageResultsAction(stageId: string, op: "compute" | "publish" | "advance") {
  const t = await token();
  if (!t) return { error: "Session expired." };
  try {
    if (op === "compute") {
      const rows = await computeStageResults(t, stageId);
      revalidatePath("/competitions/[stageId]", "page");
      return { ok: true, count: rows.length };
    }
    if (op === "publish") {
      const rows = await publishStageResults(t, stageId);
      revalidatePath("/competitions/[stageId]", "page");
      return { ok: true, count: rows.length };
    }
    const advanced = await advanceStage(t, stageId);
    revalidatePath("/competitions/[stageId]", "page");
    revalidatePath("/competitions");
    return { ok: true, count: advanced };
  } catch (err) {
    return { error: apiErrorMessage(err) };
  }
}

// ── Speakers — org-scoped; CanManage(isAdmin, role) means SuperAdmin passes without a membership.
function speakerInput(formData: FormData) {
  return {
    name: str(formData, "name") ?? "",
    // Empty string is meaningful: the service only skips a field when it is null, so "" clears it.
    bio: formData.get("bio") === null ? undefined : String(formData.get("bio")),
    company: formData.get("company") === null ? undefined : String(formData.get("company")),
    role: formData.get("role") === null ? undefined : String(formData.get("role")),
    photoKey: str(formData, "photoKey"),
    socialLinksJson: str(formData, "socialLinksJson")
  };
}

export async function createSpeakerAction(orgId: string, _: unknown, formData: FormData) {
  const t = await token();
  if (!t) return { error: "Session expired." };
  const input = speakerInput(formData);
  if (input.name.trim().length < 2 || input.name.trim().length > 150)
    return { error: "Name must be 2–150 characters." };
  try {
    await createSpeaker(t, orgId, input);
  } catch (err) {
    return { error: apiErrorMessage(err) };
  }
  revalidatePath("/speakers");
  return { ok: true };
}

export async function updateSpeakerAction(orgId: string, speakerId: string, _: unknown, formData: FormData) {
  const t = await token();
  if (!t) return { error: "Session expired." };
  const input = speakerInput(formData);
  if (input.name.trim().length < 2 || input.name.trim().length > 150)
    return { error: "Name must be 2–150 characters." };
  try {
    await updateSpeaker(t, orgId, speakerId, input);
  } catch (err) {
    return { error: apiErrorMessage(err) };
  }
  revalidatePath("/speakers");
  return { ok: true };
}

/** Hard delete — EventSpeaker / EventSessionSpeaker rows cascade with it. */
export async function deleteSpeakerAction(orgId: string, speakerId: string) {
  const t = await token();
  if (!t) return { error: "Session expired." };
  try {
    await deleteSpeaker(t, orgId, speakerId);
  } catch (err) {
    return { error: apiErrorMessage(err) };
  }
  revalidatePath("/speakers");
  return { ok: true };
}

export async function eventSpeakerAction(
  orgId: string,
  eventId: string,
  speakerId: string,
  op: "assign" | "remove"
) {
  const t = await token();
  if (!t) return { error: "Session expired." };
  try {
    if (op === "assign") await assignSpeakerToEvent(t, orgId, eventId, speakerId);
    else await removeSpeakerFromEvent(t, orgId, eventId, speakerId);
  } catch (err) {
    return { error: apiErrorMessage(err) };
  }
  revalidatePath("/speakers");
  return { ok: true };
}

// ── Sponsors — same org-scoped gate as speakers, plus a validated tier and a priority.
function sponsorInput(formData: FormData) {
  return {
    name: str(formData, "name") ?? "",
    // "" is meaningful (clears the field); the service only skips a null.
    website: formData.get("website") === null ? undefined : String(formData.get("website")),
    tier: str(formData, "tier"),
    priority: numberOr(formData, "priority", undefined),
    logoKey: str(formData, "logoKey")
  };
}

export async function createSponsorAction(orgId: string, _: unknown, formData: FormData) {
  const t = await token();
  if (!t) return { error: "Session expired." };
  const input = sponsorInput(formData);
  if (input.name.trim().length < 2 || input.name.trim().length > 150)
    return { error: "Name must be 2–150 characters." };
  try {
    await createSponsor(t, orgId, input);
  } catch (err) {
    return { error: apiErrorMessage(err) };
  }
  revalidatePath("/sponsors");
  return { ok: true };
}

export async function updateSponsorAction(orgId: string, sponsorId: string, _: unknown, formData: FormData) {
  const t = await token();
  if (!t) return { error: "Session expired." };
  const input = sponsorInput(formData);
  if (input.name.trim().length < 2 || input.name.trim().length > 150)
    return { error: "Name must be 2–150 characters." };
  try {
    await updateSponsor(t, orgId, sponsorId, input);
  } catch (err) {
    return { error: apiErrorMessage(err) };
  }
  revalidatePath("/sponsors");
  return { ok: true };
}

/** Hard delete — EventSponsor rows cascade with it. */
export async function deleteSponsorAction(orgId: string, sponsorId: string) {
  const t = await token();
  if (!t) return { error: "Session expired." };
  try {
    await deleteSponsor(t, orgId, sponsorId);
  } catch (err) {
    return { error: apiErrorMessage(err) };
  }
  revalidatePath("/sponsors");
  return { ok: true };
}

export async function eventSponsorAction(
  orgId: string,
  eventId: string,
  sponsorId: string,
  op: "assign" | "remove"
) {
  const t = await token();
  if (!t) return { error: "Session expired." };
  try {
    if (op === "assign") await assignSponsorToEvent(t, orgId, eventId, sponsorId);
    else await removeSponsorFromEvent(t, orgId, eventId, sponsorId);
  } catch (err) {
    return { error: apiErrorMessage(err) };
  }
  revalidatePath("/sponsors");
  return { ok: true };
}

/** D-266 M7 — FinanceOps clears (or refuses) a fundraising event's money path. Distinct from the event
 *  review and from the authorization review: three separate decisions, three separate audit trails. */
export async function recordFinancialReviewAction(eventId: string, _: unknown, formData: FormData) {
  const t = await token();
  if (!t) return { error: "Session expired." };
  const passed = str(formData, "passed") === "true";
  const notes = str(formData, "notes") || undefined;
  try {
    await recordFinancialReview(t, eventId, passed, notes);
  } catch (err) {
    return { error: apiErrorMessage(err) };
  }
  revalidatePath("/events/review");
  return { ok: true };
}
