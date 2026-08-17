/**
 * Backend error codes → words a person can act on.
 *
 * Every Kurx error response is an RFC7807 `ProblemDetails` whose `error` field is, per
 * `docs/api/README.md`, "a stable machine-readable code" — `invalid_code`, `rate_limited`,
 * `forbidden`. Web and admin both returned that field straight out of `apiErrorMessage`, so a
 * buyer who mistyped an OTP was shown the string `invalid_code`, and a rate-limited one was shown
 * `rate_limited`. Mobile has mapped these to sentences since it was built; the two web surfaces
 * never did.
 *
 * The wording here is copied deliberately from `mobile/lib/core/network/api_error.dart` rather than
 * written afresh. Phase 41 found the cost of a second vocabulary for the same concept, and an error
 * a user might hit on the site and then again in the app is exactly where that shows.
 *
 * A code with no entry falls back by status rather than to the code itself: an unrecognised code is
 * still a machine token, and showing it is the defect this file exists to fix.
 */
export const PROBLEM_COPY: Record<string, string> = {
  rate_limited: "Too many attempts. Please wait a minute and try again.",
  invalid_code: "That code isn't right. Check it and try again.",
  invalid_token: "Your session expired. Please sign in again.",
  validation_failed: "Please check the details and try again.",
  not_found: "We couldn't find that.",
  timeout: "The request timed out. Please try again.",
  network_error: "Can't reach Kurx. Check your connection and try again.",

  // DB-2 — the three database-capacity refusals. All say "try again" because that is genuinely the
  // user's move: the request was valid and nothing was written, so a retry is both safe and likely to
  // work. Which ceiling was actually hit is deliberately NOT surfaced — that is capacity intelligence
  // for an operator (the `reason` tag on kurx.db.failures), not something a person can act on, and
  // telling an anonymous caller where saturation begins hands them the shape of a cheap DoS.
  database_busy: "Kurx is busy right now. Please try again in a moment.",
  database_timeout: "That took longer than expected. Please try again.",
  database_unavailable: "Kurx is temporarily unavailable. Please try again in a moment.",
  // A row lock was held by someone else — concretely, two withdrawals for the same organisation at
  // once. Worded as the collision it is, so a finance user retries rather than reporting an outage.
  resource_busy: "Someone else is updating this right now. Please try again in a moment.",
  phone_mismatch: "This ticket was sent to a different mobile number.",
  transfer_expired: "This transfer has expired. Ask the sender to send it again.",
  transfer_not_pending: "This ticket has already been claimed.",
  // D-319 — answering a staff invite is one-shot, and answering it twice is ordinary (two tabs, a back
  // button), not exceptional. Without this it fell through to "Something went wrong", which reads as a
  // bug rather than as the already-settled state it is.
  not_pending: "You've already answered this invitation.",
  user_not_found: "Nobody on Kurx is registered with that mobile number.",
  transfers_disabled: "Transfers are turned off for this event.",
  forbidden: "You don't have access to that.",

  // Event lifecycle refusals (EventEndpoints.Fail). Every one of these is a 403 or 409 that the caller
  // CAN act on, and before this none of them had copy: the 403s all rendered as `forbidden` above
  // ("You don't have access to that.") and the 409s as "Something went wrong.", because
  // `problemMessage` falls back on status once the code misses and `looksLikeCode` suppresses the
  // `detail` carrying the same token. That is D-315's defect exactly, in a second vocabulary — an
  // organiser told "no access" to their OWN event has been handed a bug report, not an instruction.
  //
  // The self-represented case is the one worth wording carefully: a personal event cannot sell paid
  // tickets because a personal organization can never be verified (D-055), so the actionable sentence
  // names both ways out rather than restating the refusal.
  org_not_verified:
    "This event is hosted personally, and personal events can't sell paid tickets. Represent a verified organization, or remove the paid ticket types.",
  organizer_not_verified_for_paid:
    "Paid events need your identity and bank details verified first. Finish verification in Settings, then try again.",
  pending_org_verification:
    "That organization's verification is still being reviewed. You'll be able to continue once it's approved.",
  representation_vacant:
    "This event doesn't currently represent an organization. Choose one before continuing.",
  representation_required:
    "Choose the organization this event represents before publishing it.",
  reviewer_required:
    "A reviewer has to approve this — you can't approve your own submission.",
  paid_event_requires_review:
    "Paid events are reviewed before they go live. Submit it for review instead of publishing directly.",
  event_under_review:
    "This event is with a reviewer right now, so it can't be edited. You'll get it back with their notes.",
  event_authorization_required:
    "This event needs an authorization letter before it can be published.",
  not_draft: "This event has moved past draft, so that action no longer applies.",
  // D-363 — the refusal that protects orders, tickets and registrations from a cascade delete. It names
  // the two things that DO work, because "no" without an alternative reads as a bug.
  event_has_history:
    "People have already registered for this event, so it can't be unpublished or deleted. Cancel it instead — everyone is refunded and notified — or close it once it's over.",
  tickets_already_sold:
    "This ticket type has already been sold, so it can't be deleted. Stop its sales instead by ending the sale period.",
  event_archived: "This event is archived. Unarchive it before making changes.",
  event_already_started: "This event has already started, so that action no longer applies.",
  invalid_transition: "That action isn't available from this event's current state.",

  // Publish-readiness and review blockers (EventService.ValidatePublishReadiness, the M4 checklist, and
  // the create/update validators). Added after "Approve & publish" reported "Something went wrong."
  // for `missing_description` — the event simply had no description, which is a thing the reviewer
  // could have fixed in seconds had anyone told them. Same fallback, same cause as the lifecycle codes
  // above: an unmapped code on a 400 lands on the generic sentence, so the whole readiness vocabulary
  // read as a platform fault rather than as a checklist.
  missing_description: "Add a description before publishing this event.",
  missing_venue: "Add a venue before publishing this event.",
  missing_online_url: "Add the joining link before publishing this online event.",
  missing_venue_or_url: "Add a venue or a joining link before publishing this event.",
  missing_owner_unit: "This event isn't attached to a team yet. Set one before publishing.",
  // The V3 §14.2 lifecycle gates. Every one of these is a refusal an organiser can act on in a minute,
  // and not one had copy anywhere — so `open_registration` on an event with no ticket type reached them
  // as "Something went wrong. Please try again." on all three surfaces.
  // D-367 — the `teams` capability, enforced by the domain rather than only described. Both name the
  // fix, because "not supported" without a next step reads as a platform limitation the organiser can do
  // nothing about — when in fact choosing a different event type is exactly the answer.
  teams_not_supported:
    "This kind of event doesn't support team entry. Choose an event type that does, or register people individually.",
  type_conflicts_with_team_ticket:
    "This event has a team registration, and the type you picked doesn't support teams. Change the registration to individual first, or choose a different type.",
  // D-366 — team-size price bands. Each names the specific rule that failed: "invalid pricing" would
  // leave an organiser comparing four rules against a table they thought was right.
  overlapping_price_tiers:
    "Two price rules cover the same team size. Each size can have only one price — narrow one of the rules.",
  price_tier_gap:
    "Some allowed team sizes have no price. Cover every size from the smallest team to the largest, with no gaps.",
  price_tier_outside_group_size:
    "A price rule covers a team size this event doesn't allow. Keep every rule inside your smallest and largest team size.",
  invalid_price_tier: "A price rule is incomplete — each needs a team size range and a price above zero.",
  price_tiers_require_group: "Price-by-team-size only applies to team registration.",
  price_tiers_require_group_size: "Set the smallest and largest team size before pricing by team size.",
  no_price_for_team_size:
    "There's no price set for a team of this size. Ask the organiser to add one, or change your team size.",
  ambiguous_price_rule:
    "This event has two prices for a team of your size, so we can't charge you. We've told the organiser.",
  no_pass: "Add at least one ticket type before opening registration — there is nothing for anyone to book yet.",
  no_inventory_pool: "This event has no ticket inventory yet. Set a quantity on a ticket type, then open registration.",
  no_currency: "This event has no settlement currency set, so it can't sell anything yet.",
  no_staff_assigned: "Assign at least one staff member who has accepted before taking this event live.",
  results_not_published: "Publish the results before marking this event completed.",
  checklist_incomplete: "Work through the review checklist before approving this event.",
  claimed_by_another_reviewer: "Another reviewer is working on this one. They need to finish or release it first.",
  notes_required: "Add a note explaining this decision.",
  reason_required: "Add a reason before continuing.",
  reason_code_required: "Choose a reason before continuing.",
  invalid_title: "That title isn't valid. Give the event a clear name.",
  invalid_dates: "Check the start and end times — the event has to end after it starts.",
  invalid_category: "Choose a category for this event.",
  invalid_type: "Choose an event type.",
  invalid_audience_level: "Choose who this event is for.",
  invalid_visibility: "Choose whether this event is listed, unlisted or invite-only.",
  invalid_template: "That template isn't available for this event.",
  invalid_parent_event: "That parent event can't be used here.",
  max_composition_depth: "This event is nested too deeply. Attach it higher up instead.",
  slug_conflict: "Another event already uses that web address. Try a different title.",
  // Two people acted on the same event at once. Worded as the collision it is, so the reader retries
  // rather than reporting a fault — the same treatment `resource_busy` gets above.
  transition_conflict: "Someone else just changed this event. Refresh and try again.",
  invalid_action: "That action isn't available for this event.",

  // Admin console surface (Trust & Safety, People & Organizations, Events). Derived by enumerating
  // every `Fail("...")` reachable from the /v1/admin endpoints and the nine services they delegate to:
  // 71 codes, of which 40 had no copy, so most admin refusals reached a moderator as
  // "Something went wrong. Please try again." A moderator cannot act on that, and — critically — cannot
  // tell a refusal apart from an outage, which is why the console read as broken rather than as strict.
  already_member: "That person is already a member of this organization.",
  already_merged: "This organization has already been merged into another.",
  already_pending: "There's already a request waiting on this. Review the existing one instead.",
  already_verified: "This is already verified.",
  authorization_fields_required: "Fill in every authorization field before submitting.",
  cannot_merge_has_events: "This organization still has events. Move or archive them before merging it.",
  cannot_merge_has_funds: "This organization still holds funds. Settle them before merging it.",
  cannot_merge_has_ledger: "This organization has ledger entries and can't be merged. Its financial history has to stay attached to it.",
  cannot_moderate_self: "You can't apply a moderation action to your own account.",
  cannot_moderate_superadmin: "Super Admins can't be moderated from here.",
  cannot_revoke_last_superadmin: "This is the last Super Admin. Grant the role to someone else before revoking it.",
  duplicate_verified_org: "A verified organization with these details already exists.",
  has_active_events: "This still has active events. Resolve them before continuing.",
  invalid_account_number: "That account number isn't valid.",
  invalid_batch_size: "Too many records at once. Reduce the batch and try again.",
  invalid_decision: "Choose approve or reject.",
  invalid_domain: "That domain isn't valid.",
  invalid_evidence: "Attach at least one supporting document, each with a type and a file.",
  invalid_ifsc: "That IFSC code isn't valid.",
  invalid_kind: "That type isn't valid here.",
  invalid_merge: "These two organizations can't be merged.",
  invalid_name: "That name isn't valid.",
  invalid_pan: "That PAN isn't valid.",
  invalid_role: "That role isn't one this account can be given.",
  invalid_score: "That score is out of range.",
  invalid_subject_type: "That subject type isn't supported here.",
  invalid_value: "That value isn't valid.",
  last_owner: "This is the organization's last owner. Make someone else an owner first.",
  letterhead_required: "Attach the letterhead document before submitting.",
  not_verified: "This isn't verified yet, so that action isn't available.",
  official_phone_invalid: "That official phone number isn't valid.",
  org_blacklisted: "This organization is blacklisted. Lift the blacklist before continuing.",
  organization_domain_taken: "Another organization already uses that domain.",
  phone_required: "A phone number is required.",
  representative_role_invalid: "Choose a valid representative role.",
  representative_role_other_required: "Describe the role when choosing Other.",
  representative_user_not_found: "No Kurx account matches that representative.",
  resubmit_limit_reached: "This has been resubmitted too many times. It needs a manual decision.",
  unknown_action: "That action isn't recognised.",
  unknown_checklist_item: "That checklist item isn't on this event's checklist.",
  nodes_required: "Add at least one category row before importing.",

  // D-335 ID-card issuance. The issuer is the event's verified creator/organizer — not an
  // organization — so every one of these names the event, never a college or an org seat.
  not_event_organizer: "Only the event's creator or an organizer can issue ID cards for it.",
  issuer_not_verified: "Your account needs to be verified before you can issue ID cards. Finish verification in Settings.",
  cannot_issue_to_self: "You can't issue an ID card to yourself — that's what makes it proof of participation.",
  holder_not_a_participant: "That person isn't a participant in this event yet, so they can't be issued a card for it.",
  invalid_storage_key: "That file isn't one you uploaded. Upload the image again and retry.",

  // Password policy (PasswordService.RejectionCode, D-129). These live here rather than only in
  // `web/lib/password.ts` because the signup surfaces do not use that helper: registration renders
  // whatever `apiErrorMessage` returns, so before this every policy refusal — a breached password,
  // one containing the user's own phone — reached the user as "Something went wrong. Please try
  // again." A refusal the user cannot act on is indistinguishable from a broken form, which is
  // exactly how it was reported.
  //
  // The 12/128 figures track PasswordPolicy.MinLength/MaxLength. They are also the fallback both
  // clients hold when `GET /v1/auth/password/status` cannot be reached, so this is the third copy of
  // the same constant, not a new one.
  password_too_short: "Use at least 12 characters.",
  password_too_long: "That password is too long (max 128).",
  password_breached: "That password is too common. Choose something less guessable.",
  password_contains_identifier: "Don't use your name, email, or phone number in your password.",
  password_reused: "You've used that password recently. Choose a new one.",
  password_already_set: "You already have a password. Use Change password instead.",
  password_not_set: "You don't have a password yet. Create one first.",
  password_invalid: "That password isn't allowed. Choose a different one.",

  // Phone. `invalid_phone` means the number is phone-SHAPED but cannot exist (libphonenumber could
  // not place it); `validation_failed` above covers input that is not phone-shaped at all. Two codes,
  // two different things for the user to fix, so they must not collapse into one sentence.
  invalid_phone: "That phone number doesn't look right. Check the country code and number."
};

/** True for a string that looks like a machine code rather than a sentence: `invalid_code`. */
export function looksLikeCode(value: string): boolean {
  return /^[a-z][a-z0-9]*(_[a-z0-9]+)+$/.test(value.trim());
}

/**
 * The sentence to show for a failed request.
 *
 * `detail` is passed through only when it is prose. The backend fills it with a generic string for
 * unhandled 500s and leaves it absent otherwise, so it is a useful second source but never a
 * trusted one — and if it happens to hold a code too, it gets the same treatment.
 */
export function problemMessage(
  { code, detail, status }: { code?: string; detail?: string; status?: number }
): string {
  if (code && PROBLEM_COPY[code]) return PROBLEM_COPY[code];
  // Development only. An unmapped code is a GAP IN THIS TABLE, and the generic sentence below is
  // precisely what hides the gap: `missing_description` — an event with no description, fixable in
  // seconds — reached a reviewer as "Something went wrong. Please try again." twice before anyone
  // looked at the response body. Surfacing the code in dev makes the omission self-reporting.
  //
  // Checked against "development" rather than `!== "production"` on purpose: vitest runs with
  // NODE_ENV="test", so the suite keeps asserting the sentences real users actually see.
  if (code && process.env.NODE_ENV === "development") {
    return `${code} (HTTP ${status ?? "?"}) — no copy for this code yet; add it to PROBLEM_COPY.`;
  }
  if (detail && !looksLikeCode(detail)) return detail;
  if (status === 0 || status === undefined) return "Can't reach Kurx. Check your connection and try again.";
  if (status >= 500) return "Something went wrong on our end. Please try again.";
  if (status === 403) return PROBLEM_COPY.forbidden;
  if (status === 404) return PROBLEM_COPY.not_found;
  return "Something went wrong. Please try again.";
}
