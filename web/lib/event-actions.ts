"use server";

import { redirect } from "next/navigation";
import { revalidatePath } from "next/cache";
import { requireSession } from "@/lib/session";
import {
  createEvent, createTicketType, updateOrgEvent, transitionOrgEvent, deleteOrgEvent, cloneOrgEvent,
  createOrgVenue, createOrgSpeaker, assignEventSpeaker, createOrgSponsor, assignEventSponsor,
  createEventSession, presignEventMedia, attachEventMedia, exportAttendeesCsv,
  updateEventSession, deleteEventSession, reorderEventSessions,
  updateOrgSpeaker, removeEventSpeaker, updateOrgSponsor, removeEventSponsor,
  deleteEventMedia, exportEventAnalyticsCsv, apiErrorMessage, searchPublicUsers,
  presignAuthorizationDocument, submitEventAuthorization,
  createInviteLink, revokeInviteLink,
  createEventChangeRequest, withdrawEventChangeRequest
} from "@/lib/api";

/** Resolves the optional "link to a Kurx account" field on a speaker form to a userId — an exact
 * username match (the common, unambiguous case for "@handle"). Returns undefined for a blank
 * field or no match, so linking stays opt-in and never fails the rest of the save. */
async function resolveLinkUsername(formData: FormData): Promise<string | undefined> {
  const handle = str(formData, "linkUsername")?.replace(/^@/, "");
  if (!handle) return undefined;
  const results = await searchPublicUsers(handle, 1, 5).catch(() => []);
  return results.find((r) => r.username.toLowerCase() === handle.toLowerCase())?.id;
}

function str(formData: FormData, key: string): string | undefined {
  const v = formData.get(key);
  return typeof v === "string" && v.length > 0 ? v : undefined;
}

function num(formData: FormData, key: string): number | undefined {
  const v = str(formData, key);
  return v === undefined ? undefined : Number(v);
}

/// The D-265 field groups, mirroring the backend's grouped inputs one-for-one. Grouped rather than
/// flattened for the same reason the server groups them: a wizard PATCHes one step at a time, and a
/// group left undefined means "this step was not touched" — not "clear it".
export type EventContentValues = {
  tagline?: string;
  shortDescription?: string;
  logoKey?: string;
  thumbnailKey?: string;
  promoVideoKey?: string;
  rules?: string;
  faqJson?: string;
};

export type EventLegalValues = {
  termsUrl?: string;
  termsText?: string;
  codeOfConduct?: string;
  refundPolicy?: string;
  cancellationPolicy?: string;
  requiresConsent?: boolean;
  consentText?: string;
};

export type EventScheduleValues = {
  registrationOpensAt?: string;
  registrationClosesAt?: string;
  checkinOpensAt?: string;
  checkinClosesAt?: string;
  resultDate?: string;
  certificateReleaseAt?: string;
  autoClose?: boolean;
};

export type EventLocationValues = {
  building?: string;
  floor?: string;
  room?: string;
  googleMapsUrl?: string;
  meetingPlatform?: string;
  meetingPassword?: string;
};

export type EventEligibilityValues = {
  minAge?: number;
  maxAge?: number;
  genderRestriction?: string;
  maxTeams?: number;
};

export type EventCommerceValues = {
  platformFeePercent?: number;
  platformFeeFlatPaise?: number;
  taxPercent?: number;
  taxInclusive?: boolean;
  prizePoolJson?: string;
};

export type CreateEventValues = {
  title: string;
  subtitle?: string;
  description?: string;
  categoryId: string;
  typeId?: string;
  visibility: string;
  startsAt: string;
  endsAt: string;
  venueName?: string;
  venueAddress?: string;
  city?: string;
  capacity?: number;
  eventMode?: string;
  onlineUrl?: string;
  content?: EventContentValues;
  legal?: EventLegalValues;
  schedule?: EventScheduleValues;
  location?: EventLocationValues;
  eligibility?: EventEligibilityValues;
  commerce?: EventCommerceValues;
};

/// Create-Event wizard submit. Returns the new event id on success (client navigates to its workspace),
/// or an error message.
///
/// **Creates the event AND its first ticket type.** Until now it created only the event, and an event
/// with no ticket type cannot be registered for at all — the booking form answers "The host has not
/// published any ticket types for this event yet." So every event this wizard produced was unbookable
/// until the host separately found Workspace ▸ Tickets, and nothing said so.
///
/// The ticket is created second, in its own call, because the create API does not accept one. A failure
/// there is reported without discarding the event: the draft exists and the host can finish it in the
/// Tickets tab, which is strictly better than losing eleven steps of work to a ticket-shaped error.
///
/// `representingOrgId` is required — null is `representation_required`, not "Personal" (D-379 retired
/// self-representation). The parameter stays nullable because the wizard holds it as "not answered yet";
/// the Representing step is what makes it non-null, registering the institution in place if the caller
/// represents none (D-389). No organization is chosen before this point — it is a field on the request,
/// not a path segment.
export async function createEventWizardAction(
  representingOrgId: string | null,
  values: CreateEventValues,
  /// D-372 — the registration option created with the event, and the UNIT its price is charged in.
  /// `pricingUnit`/`registrationMode`/`groupMin`/`groupMax` were hardcoded here; they are the wizard's
  /// answer now, and `TicketType` has carried all four since D-020.
  ticket: {
    name: string; pricePaise: number; quantity: number;
    pricingUnit: "PerTicket" | "PerGroup";
    registrationMode: "Individual" | "Group";
    groupMin?: number; groupMax?: number;
    /// D-366 — team-size price bands. Absent for every ticket priced by one amount, which keeps the
    /// created request identical to what it was for an unbanded ticket.
    priceTiers?: { minSize: number; maxSize: number; pricePaise: number }[];
  }
) {
  const session = await requireSession();
  try {
    const event = await createEvent(session.accessToken, {
      representingOrgId: representingOrgId ?? undefined,
      title: values.title,
      subtitle: values.subtitle,
      description: values.description,
      categoryId: values.categoryId,
      typeId: values.typeId,
      visibility: values.visibility,
      startsAt: values.startsAt,
      endsAt: values.endsAt,
      venueName: values.venueName,
      venueAddress: values.venueAddress,
      city: values.city,
      capacity: values.capacity,
      eventMode: values.eventMode,
      onlineUrl: values.onlineUrl,
      // Undefined groups are omitted by JSON.stringify, which is exactly the "untouched" the server
      // expects — sending an empty object would be a request to clear nothing, harmlessly, but
      // sending nulls inside one would clear fields the organiser never opened.
      content: values.content,
      legal: values.legal,
      schedule: values.schedule,
      location: values.location,
      eligibility: values.eligibility,
      commerce: values.commerce
    });

    // The sale window mirrors the event's own dates: a ticket's window may narrow that later, never
    // widen it, so starting them equal is the only value that cannot already be wrong.
    try {
      await createTicketType(session.accessToken, event.representing_org_id, event.id, {
        name: ticket.name.trim() || "General Admission",
        pricePaise: ticket.pricePaise,
        // D-372 — from the Registration step, not a literal. `PerGroup` is what tells the money path to
        // charge once per team and take one inventory unit for it.
        pricingUnit: ticket.pricingUnit,
        registrationMode: ticket.registrationMode,
        groupMin: ticket.groupMin,
        groupMax: ticket.groupMax,
        // D-366 — when present the server derives the headline price from the cheapest band, so
        // `pricePaise` above is only load-bearing for an unbanded ticket.
        priceTiers: ticket.priceTiers,
        quantity: ticket.quantity,
        saleStarts: new Date().toISOString(),
        saleEnds: values.endsAt,
        perUserLimit: 1,
        isAllAccess: false
      });
    } catch (err) {
      // Deliberately not fatal — see the note above. The event is real and the host is told what is
      // missing, rather than being shown a generic failure for work that actually succeeded.
      revalidatePath("/workspace");
      return { id: event.id, ticketError: apiErrorMessage(err) };
    }

    revalidatePath("/workspace");
    return { id: event.id };
  } catch (err) {
    return { error: apiErrorMessage(err) };
  }
}

/**
 * `EditEventForm` was written against `{ error }` — it renders `state && "error" in state` — but this
 * action never produced one: an `updateOrgEvent` rejection propagated straight out of the server
 * action, so the form's only error path was dead code and a failed save took the whole page to an
 * error boundary instead. The sibling `createEventWizardAction` already had the shape; this is it
 * delivering the contract the form was built for.
 */
export async function updateEventAction(orgId: string, eventId: string, _: unknown, formData: FormData) {
  const session = await requireSession();
  try {
    await updateOrgEvent(session.accessToken, orgId, eventId, {
      title: str(formData, "title"),
      subtitle: str(formData, "subtitle"),
      description: str(formData, "description"),
      categoryId: str(formData, "categoryId"),
      typeId: str(formData, "typeId"),
      venueName: str(formData, "venueName"),
      venueAddress: str(formData, "venueAddress"),
      city: str(formData, "city"),
      startsAt: str(formData, "startsAt"),
      endsAt: str(formData, "endsAt"),
      capacity: num(formData, "capacity"),
      visibility: str(formData, "visibility"),
      contactEmail: str(formData, "contactEmail"),
      contactPhone: str(formData, "contactPhone"),
      website: str(formData, "website")
    });
    revalidatePath(`/host/events/${eventId}`);
    return { ok: true };
  } catch (err) {
    return { error: apiErrorMessage(err) };
  }
}

/*
 * D-388 — the host's proposal on a LIVE event.
 *
 * Deliberately a SEPARATE action from `updateEventAction` rather than a branch inside it. The two do
 * different things — one changes the event, one asks to — and a single action choosing between them on a
 * status read would mean the form could not tell the host, before they typed anything, which of the two
 * their save was going to do.
 *
 * The field list is the PROTECTED set. Contact details, website, banner and the registration windows are
 * absent because they stay directly editable on a live event and go through `updateEventAction`.
 */
export async function requestEventChangesAction(orgId: string, eventId: string, _: unknown, formData: FormData) {
  const session = await requireSession();
  try {
    const cr = await createEventChangeRequest(session.accessToken, orgId, eventId, {
      title: str(formData, "title"),
      subtitle: str(formData, "subtitle"),
      description: str(formData, "description"),
      categoryId: str(formData, "categoryId"),
      typeId: str(formData, "typeId"),
      venueName: str(formData, "venueName"),
      venueAddress: str(formData, "venueAddress"),
      city: str(formData, "city"),
      startsAt: str(formData, "startsAt"),
      endsAt: str(formData, "endsAt"),
      capacity: num(formData, "capacity"),
      visibility: str(formData, "visibility"),
      reason: str(formData, "reason")
    });
    revalidatePath(`/host/events/${eventId}/details`);
    return { ok: true, changeRequestId: cr.id };
  } catch (err) {
    return { error: changeRequestErrorMessage(err) };
  }
}

export async function withdrawEventChangesAction(orgId: string, eventId: string, changeRequestId: string) {
  const session = await requireSession();
  try {
    await withdrawEventChangeRequest(session.accessToken, orgId, eventId, changeRequestId);
    revalidatePath(`/host/events/${eventId}/details`);
    return { ok: true };
  } catch (err) {
    return { error: changeRequestErrorMessage(err) };
  }
}

/// D-388 refusals, in the organiser's words. Same discipline as TRANSITION_ERRORS below: the server's
/// code is the contract and this only translates it, so an unmapped code still surfaces rather than
/// becoming a generic failure.
const CHANGE_REQUEST_ERRORS: Record<string, string> = {
  no_changes: "Nothing changed — edit a field before submitting for approval.",
  not_live_protected:
    "This event isn't live yet, so you can edit it directly — no approval needed.",
  change_request_decided:
    "A reviewer has already decided on this request. Refresh to see the outcome.",
  version_conflict:
    "This event changed after you started editing. Refresh and make your changes again so a reviewer sees the current values.",
  forbidden: "You don't have permission to change this event."
};

function changeRequestErrorMessage(err: unknown): string {
  const raw = apiErrorMessage(err);
  return CHANGE_REQUEST_ERRORS[raw] ?? raw;
}

/// Organiser-facing copy for the transition refusals a host can actually act on. The server's code is the
/// contract (D-266); this only translates it. Anything unmapped falls through to the raw code rather than a
/// generic "something went wrong", so an unexpected refusal is still diagnosable.
const TRANSITION_ERRORS: Record<string, string> = {
  // D-266 M5 — the two representation rules.
  // D-382 — both refusals name the ONE surface that fixes them. "Add it under Authorization" pointed at
  // a panel on Readiness that no longer exists; representation is entered on the Representing tab.
  event_authorization_required:
    "This event represents an organization, so it needs that organization's signed authorization before it can go live. File it on the event's Representing tab, then publish.",
  representation_required:
    "This type of event has to be run on behalf of an organization. See the event's Representing tab, then publish.",
  // The event moved between this page loading and the button being pressed — usually a reviewer acting on
  // it at the same moment. Refreshing shows what it is now; retrying blind would decide about a state that
  // no longer exists.
  transition_conflict:
    "Someone else just changed this event's status. Refresh to see where it stands, then try again.",
  // Pre-existing publish gates, previously just as silent.
  pending_org_verification: "The organization you're representing is still being verified. You can publish once that completes.",
  representation_vacant: "This organization has no verified representative right now, so new events can't go live. An admin needs to verify one.",
  missing_description: "Add a description before publishing.",
  missing_venue_or_url: "Add a venue (or a joining link for an online event) before publishing.",
  // The two codes `publish` itself emits. Only `missing_venue_or_url` — the granular Scheduled gate's
  // code — was mapped, so the far more common publish refusal rendered as the raw string `missing_venue`
  // (D-299).
  missing_venue: "Add a venue before publishing.",
  missing_online_url: "Add a joining link before publishing — an online event needs somewhere to go.",
  paid_event_requires_review: "A paid event has to pass review before it can be published.",
  event_under_review: "A reviewer is looking at this event right now, so it can't be changed.",
  approval_pending: "This event is waiting on an internal approval step."
};

export async function transitionEventAction(orgId: string, eventId: string, action: string) {
  const session = await requireSession();
  try {
    await transitionOrgEvent(session.accessToken, orgId, eventId, action);
  } catch (err) {
    // Previously this threw into the void: the host clicked Publish and nothing happened, with no clue
    // why. D-266 M5 made a silent refusal far more likely, so the reason now comes back to the caller.
    const code = apiErrorMessage(err);
    return { error: TRANSITION_ERRORS[code] ?? code };
  }
  revalidatePath(`/host/events/${eventId}`);
  revalidatePath("/workspace");
  return { ok: true as const };
}

export async function deleteDraftEventAction(orgId: string, eventId: string) {
  const session = await requireSession();
  // The refusal is the interesting outcome and it was being thrown away: a server action that throws
  // reaches the client as an opaque digest in production, so the caller could only print "That draft
  // could not be deleted." D-363's whole point is that `event_has_history` names what to do instead —
  // cancel or close — and that sentence never arrived. `not_draft` likewise.
  try {
    await deleteOrgEvent(session.accessToken, orgId, eventId);
  } catch (err) {
    return { error: apiErrorMessage(err) };
  }
  revalidatePath("/workspace");
  // OUTSIDE the try on purpose: `redirect` signals by throwing, so inside it the catch above would
  // treat every successful delete as a failure.
  redirect("/workspace");
}

export async function duplicateEventAction(orgId: string, eventId: string) {
  const session = await requireSession();
  await cloneOrgEvent(session.accessToken, orgId, eventId);
  revalidatePath("/workspace");
}

/// Runs one action across many events (events table bulk bar). Each item is isolated so an invalid
/// state transition (e.g. archiving a draft) skips that row rather than aborting the batch.
export async function bulkEventAction(
  orgId: string,
  ids: string[],
  action: "publish" | "archive" | "duplicate" | "delete"
) {
  const session = await requireSession();
  let done = 0;
  let failed = 0;
  for (const id of ids) {
    try {
      if (action === "delete") await deleteOrgEvent(session.accessToken, orgId, id);
      else if (action === "duplicate") await cloneOrgEvent(session.accessToken, orgId, id);
      else await transitionOrgEvent(session.accessToken, orgId, id, action);
      done++;
    } catch {
      failed++;
    }
  }
  revalidatePath("/workspace");
  return { done, failed };
}

export async function exportAttendeesAction(orgId: string, eventId: string) {
  const session = await requireSession();
  try {
    const csv = await exportAttendeesCsv(session.accessToken, orgId, eventId);
    return { ok: true as const, csv };
  } catch (err) {
    return { ok: false as const, error: apiErrorMessage(err) };
  }
}

export async function exportEventAnalyticsAction(orgId: string, eventId: string) {
  const session = await requireSession();
  try {
    const csv = await exportEventAnalyticsCsv(session.accessToken, orgId, eventId);
    return { ok: true as const, csv };
  } catch (err) {
    return { ok: false as const, error: apiErrorMessage(err) };
  }
}

// ── Sessions ────────────────────────────────────────────────────────────────
export async function updateSessionAction(orgId: string, eventId: string, sessionId: string, _: unknown, formData: FormData) {
  const session = await requireSession();
  try {
    await updateEventSession(session.accessToken, orgId, eventId, sessionId, {
      title: str(formData, "title"),
      kind: str(formData, "kind") ?? "Session",
      startsAt: str(formData, "startsAt"),
      endsAt: str(formData, "endsAt")
    });
  } catch {
    /* the re-render reflects the unchanged session */
  }
  revalidatePath(`/host/events/${eventId}/schedule`);
}
export async function deleteSessionAction(orgId: string, eventId: string, sessionId: string) {
  const session = await requireSession();
  await deleteEventSession(session.accessToken, orgId, eventId, sessionId);
  revalidatePath(`/host/events/${eventId}/schedule`);
}
export async function reorderSessionsAction(orgId: string, eventId: string, sessionIds: string[]) {
  const session = await requireSession();
  await reorderEventSessions(session.accessToken, orgId, eventId, sessionIds);
  revalidatePath(`/host/events/${eventId}/schedule`);
}

// ── Speakers ────────────────────────────────────────────────────────────────
export async function updateSpeakerAction(orgId: string, eventId: string, speakerId: string, _: unknown, formData: FormData) {
  const session = await requireSession();
  try {
    await updateOrgSpeaker(session.accessToken, orgId, speakerId, {
      name: str(formData, "name"),
      company: str(formData, "company"),
      role: str(formData, "role"),
      bio: str(formData, "bio"),
      userId: await resolveLinkUsername(formData)
    });
  } catch {
    /* the re-render reflects the unchanged speaker */
  }
  revalidatePath(`/host/events/${eventId}/people`);
}
export async function removeSpeakerAction(orgId: string, eventId: string, speakerId: string) {
  const session = await requireSession();
  await removeEventSpeaker(session.accessToken, orgId, eventId, speakerId);
  revalidatePath(`/host/events/${eventId}/people`);
}

// ── Sponsors ────────────────────────────────────────────────────────────────
export async function updateSponsorAction(orgId: string, eventId: string, sponsorId: string, _: unknown, formData: FormData) {
  const session = await requireSession();
  try {
    await updateOrgSponsor(session.accessToken, orgId, sponsorId, {
      name: str(formData, "name"),
      tier: str(formData, "tier"),
      website: str(formData, "website")
    });
  } catch {
    /* the re-render reflects the unchanged sponsor */
  }
  revalidatePath(`/host/events/${eventId}/people`);
}
export async function removeSponsorAction(orgId: string, eventId: string, sponsorId: string) {
  const session = await requireSession();
  await removeEventSponsor(session.accessToken, orgId, eventId, sponsorId);
  revalidatePath(`/host/events/${eventId}/people`);
}

// ── Media ───────────────────────────────────────────────────────────────────
export async function deleteMediaAction(orgId: string, eventId: string, mediaId: string) {
  const session = await requireSession();
  await deleteEventMedia(session.accessToken, orgId, eventId, mediaId);
  revalidatePath(`/host/events/${eventId}/media`);
}

export async function addVenueAction(orgId: string, eventId: string, _: unknown, formData: FormData) {
  const session = await requireSession();
  const venue = await createOrgVenue(session.accessToken, orgId, {
    name: str(formData, "name"),
    address: str(formData, "address"),
    city: str(formData, "city"),
    capacity: num(formData, "capacity"),
    googleMapsUrl: str(formData, "googleMapsUrl")
  });
  await updateOrgEvent(session.accessToken, orgId, eventId, { venueId: venue.id });
  revalidatePath(`/host/events/${eventId}`);
  return { ok: true };
}

export async function addSpeakerAction(orgId: string, eventId: string, _: unknown, formData: FormData) {
  const session = await requireSession();
  const speaker = await createOrgSpeaker(session.accessToken, orgId, {
    name: str(formData, "name"),
    company: str(formData, "company"),
    role: str(formData, "role"),
    bio: str(formData, "bio")
  });
  await assignEventSpeaker(session.accessToken, orgId, eventId, speaker.id);
  revalidatePath(`/host/events/${eventId}`);
  return { ok: true };
}

export async function addSponsorAction(orgId: string, eventId: string, _: unknown, formData: FormData) {
  const session = await requireSession();
  const sponsor = await createOrgSponsor(session.accessToken, orgId, {
    name: str(formData, "name"),
    tier: str(formData, "tier"),
    website: str(formData, "website")
  });
  await assignEventSponsor(session.accessToken, orgId, eventId, sponsor.id);
  revalidatePath(`/host/events/${eventId}`);
  return { ok: true };
}

export async function addSessionAction(orgId: string, eventId: string, _: unknown, formData: FormData) {
  const session = await requireSession();
  await createEventSession(session.accessToken, orgId, eventId, {
    title: str(formData, "title"),
    kind: str(formData, "kind") ?? "Session",
    startsAt: str(formData, "startsAt"),
    endsAt: str(formData, "endsAt")
  });
  revalidatePath(`/host/events/${eventId}`);
  return { ok: true };
}

export async function uploadEventMediaAction(orgId: string, eventId: string, _: unknown, formData: FormData) {
  const session = await requireSession();
  const file = formData.get("file");
  const kind = str(formData, "kind") ?? "Gallery";
  if (!(file instanceof File) || file.size === 0) return { ok: false, error: "No file selected." };

  const presigned = await presignEventMedia(session.accessToken, orgId, eventId, file.type || "application/octet-stream", file.size);
  await fetch(presigned.url, {
    method: "PUT",
    headers: presigned.headers,
    body: await file.arrayBuffer()
  });
  await attachEventMedia(session.accessToken, orgId, eventId, kind, presigned.key, str(formData, "caption"));
  revalidatePath(`/host/events/${eventId}`);
  return { ok: true };
}

// ── D-266 M5/M8 · institutional authorization ───────────────────────────────────────────────

/** Prepares a presigned PUT. The bytes go straight to storage from the browser — they never pass through
 *  the API — and only the returned key is submitted with the form. */
export async function uploadAuthorizationDocumentAction(eventId: string, contentType: string, sizeBytes: number) {
  const session = await requireSession();
  try {
    // Ceiling mirrors the server's clamp; a friendlier refusal than a rejected upload.
    return await presignAuthorizationDocument(session.accessToken, eventId, contentType, Math.min(sizeBytes || 1, 10_000_000));
  } catch (err) {
    return { error: apiErrorMessage(err) };
  }
}

const AUTHORIZATION_ERRORS: Record<string, string> = {
  letterhead_required: "Attach the authorization letter before submitting.",
  authorization_fields_required:
    "Fill in the signatory's name, designation, official email, phone and role.",
  official_phone_invalid: "Enter the phone in international format, e.g. +919876543210.",
  representative_role_invalid: "Choose a role from the list.",
  representative_role_other_required: "Type the role, since you chose Other.",
  representative_user_not_found: "That Kurx account no longer exists. Search again or leave it blank.",
  event_under_review: "A reviewer is looking at this event right now, so the authorization can't be changed.",
};

export async function submitAuthorizationAction(
  eventId: string,
  body: {
    headName: string; headDesignation: string; officialEmail: string; officialPhone: string;
    representativeRole: string; representativeRoleOther?: string; representativeUserId?: string;
    letterheadDocumentKey?: string;
  },
) {
  const session = await requireSession();
  try {
    await submitEventAuthorization(session.accessToken, eventId, body);
  } catch (err) {
    const code = apiErrorMessage(err);
    return { error: AUTHORIZATION_ERRORS[code] ?? code };
  }
  revalidatePath(`/host/events/${eventId}`);
  revalidatePath("/workspace");
  return { ok: true as const };
}

// ── D-266 M6/M8 · invite links ──────────────────────────────────────────────────────────────

export async function createInviteLinkAction(eventId: string, _: unknown, formData: FormData) {
  const session = await requireSession();
  const maxSeatsRaw = str(formData, "maxSeats");
  const expiresRaw = str(formData, "expiresAt");
  try {
    await createInviteLink(session.accessToken, eventId, {
      // Blank = unlimited / never expires. Sent as null rather than omitted so the intent is explicit.
      maxSeats: maxSeatsRaw ? Number(maxSeatsRaw) : null,
      singleUse: formData.get("singleUse") === "on",
      expiresAt: expiresRaw ? new Date(expiresRaw).toISOString() : null,
      passcode: str(formData, "passcode") ?? null,
    });
  } catch (err) {
    return { error: apiErrorMessage(err) };
  }
  revalidatePath(`/host/events/${eventId}/invitations`);
  return { ok: true as const };
}

export async function revokeInviteLinkAction(eventId: string, linkId: string) {
  const session = await requireSession();
  try {
    await revokeInviteLink(session.accessToken, linkId);
  } catch (err) {
    return { error: apiErrorMessage(err) };
  }
  revalidatePath(`/host/events/${eventId}/invitations`);
  return { ok: true as const };
}

// ── Event banner (D-302) ────────────────────────────────────────────────────

/**
 * Uploads and persists the event's banner — the image every card and detail hero renders.
 *
 * **Reuses the existing pipeline rather than adding a media system.** `IMediaService` states the
 * architecture plainly: "the primary banner is a plain Event.BannerKey field set via UpdateEventInput;
 * everything else attaches here." So this presigns through the same event-media presign (authorized on
 * `ManageContent`, keyed under `events/{id}/media/…`), PUTs the bytes straight to storage, and then
 * persists the key with the ordinary event PATCH. It deliberately does **not** create an `EventMedia`
 * row: `MediaKind.Banner` exists in the enum, but nothing reads gallery rows for a card, so attaching
 * one would look like it worked and change nothing on any surface.
 *
 * Banner lives in the workspace, not the create wizard, for the reason the wizard itself gives: media
 * configures an event that already exists, and the wizard's job is a draft complete enough to publish.
 */
const BANNER_TYPES = ["image/jpeg", "image/png", "image/webp", "image/avif"];
const BANNER_MAX_BYTES = 10 * 1024 * 1024;

export async function uploadEventBannerAction(orgId: string, eventId: string, _: unknown, formData: FormData) {
  const session = await requireSession();
  const file = formData.get("banner");
  if (!(file instanceof File) || file.size === 0) return { ok: false, error: "Choose an image first." };

  // Checked here as well as server-side: a 10 MB refusal after the bytes have already been uploaded is
  // a worse experience than one before, and the storage presign clamps size but not type.
  if (!BANNER_TYPES.includes(file.type)) {
    return { ok: false, error: "Use a JPEG, PNG, WebP or AVIF image." };
  }
  if (file.size > BANNER_MAX_BYTES) {
    return { ok: false, error: "That image is over 10 MB. Use a smaller one." };
  }

  try {
    const presigned = await presignEventMedia(session.accessToken, orgId, eventId, file.type, file.size);
    const put = await fetch(presigned.url, {
      method: "PUT",
      headers: presigned.headers,
      body: await file.arrayBuffer()
    });
    // The upload bypasses the API entirely, so a failure here is invisible unless it is checked. The
    // previous media upload never looked at this response — a rejected PUT still attached the key, and
    // the event ended up pointing at an object that was never stored.
    if (!put.ok) return { ok: false, error: `Upload failed (${put.status}).` };

    await updateOrgEvent(session.accessToken, orgId, eventId, { bannerKey: presigned.key });
    revalidatePath(`/host/events/${eventId}`);
    revalidatePath(`/e`, "layout");
    return { ok: true };
  } catch (err) {
    return { ok: false, error: apiErrorMessage(err) };
  }
}

/** Removes the banner. An empty string is the "clear" signal — `null` means "this PATCH did not
 *  mention the banner", so it could never express removal (D-302). */
export async function removeEventBannerAction(orgId: string, eventId: string) {
  const session = await requireSession();
  try {
    await updateOrgEvent(session.accessToken, orgId, eventId, { bannerKey: "" });
    revalidatePath(`/host/events/${eventId}`);
    revalidatePath(`/e`, "layout");
    return { ok: true };
  } catch (err) {
    return { ok: false, error: apiErrorMessage(err) };
  }
}
