/// Payload shaping for the create-event wizard (D-265).
///
/// Extracted from the wizard component so it can be tested without a DOM: both functions encode a
/// server contract, and getting either wrong corrupts data silently rather than failing loudly.

/// Drops empty strings, `undefined` and `NaN`, returning `undefined` when nothing survives.
///
/// This is not tidiness. On the server a **null field means "leave alone"** and an **empty string
/// means "clear"**, so a wizard step the organiser never opened must send *nothing* — sending its
/// blanks would wipe fields set in a different step. `undefined` groups vanish under
/// `JSON.stringify`, which is exactly the "untouched" the server expects.
///
/// `false` and `0` are deliberately kept: an unchecked switch and a zero fee are real values.
export function cleanGroup<T extends Record<string, unknown>>(group: T): T | undefined {
  const kept = Object.entries(group).filter(
    ([, v]) => v !== "" && v !== undefined && v !== null && !(typeof v === "number" && Number.isNaN(v))
  );
  return kept.length > 0 ? (Object.fromEntries(kept) as T) : undefined;
}

/// Converts an `<input type="datetime-local">` value to ISO-8601 UTC.
///
/// The input yields a wall-clock string with no zone (`2026-08-10T18:30`). Sent raw, the server
/// reads it as UTC and every time silently shifts by the organiser's offset — in India that is a
/// five-and-a-half hour error nobody notices until check-in. Passing it through `Date` applies the
/// browser's zone, which is the zone the organiser typed in.
export function toIsoUtc(local: string | undefined): string | undefined {
  if (!local) return undefined;
  const parsed = new Date(local);
  return Number.isNaN(parsed.getTime()) ? undefined : parsed.toISOString();
}

/// Types the chosen product class permits (D-305).
///
/// **Absent `product_class` means Public** — the same fallback `ResolveArchetypeAsync` applies
/// server-side, so an admin-created Type that predates classification behaves identically on both
/// sides. The gate filters the catalogue; `Event.Product` is still derived and snapshotted from the
/// chosen Type at create (D-266 M1), so this can narrow what is offered but can never contradict what
/// the server will store.
///
/// Lives here rather than in the component for the reason this file exists: it encodes a server-side
/// rule, and a wrong answer shows the organiser the wrong catalogue rather than failing loudly.
/// Flutter applies the same predicate as `EventCategory.allowsProduct`.
export function typesFor<T extends { product_class?: string | null }>(
  subcategories: T[],
  product: "Public" | "Private"
): T[] {
  return subcategories.filter((s) =>
    product === "Private" ? s.product_class === "Private" : s.product_class !== "Private");
}

/// Categories worth offering for this product class.
///
/// For **Private**, a category must have at least one Private Type — otherwise it is selectable and
/// then shows an empty Type step with no way forward. **Public** keeps every category, because a
/// category with no Types at all still yields a Public event: Type is optional and a null Type resolves
/// to Public.
export function categoriesFor<T extends { id: string }, S extends { parent_id: string | null; product_class?: string | null }>(
  categories: T[],
  subcategories: S[],
  product: "Public" | "Private"
): T[] {
  if (product !== "Private") return categories;
  const allowed = typesFor(subcategories, product);
  return categories.filter((c) => allowed.some((t) => t.parent_id === c.id));
}

/// Renders a stored UTC instant as an `<input type="datetime-local">` value in the browser's zone.
///
/// `toIsoUtc`'s inverse, and the reason it has to live beside it: the two are a pair, and a form is
/// only lossless when both legs agree on a zone. Three copies of this existed, and one of them —
/// `dtLocal` on the ticket-type edit form — was `iso.slice(0, 16)`, which puts the **UTC** wall clock
/// into a control the browser reads as local time (D-289). That truncation is invisible until
/// something converts on the way back, at which point it shifts the value in the opposite direction.
export function toLocalInput(iso: string): string {
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return "";
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

/// The Details step's own required set, and the single place it is decided.
///
/// Extracted here for the same reason `cleanGroup` and `toIsoUtc` are: it encodes a rule the Continue
/// button depends on, and a wrong answer is *silent* — the organiser either walks past an incomplete
/// step or is stranded on a complete one with nothing on screen naming the field that is wrong.
///
/// **Continue means "this step is valid", never "the user typed something".** That distinction is the
/// defect this replaces: `detailsValid` was `title.length >= 2 && endsAt > startsAt`, which is
/// `CreateEventBodyValidator`'s minimum — so the six other inputs the step *asks for* could all be
/// blank and the step still passed. The server takes those six as optional and still does; this is a
/// wizard-step completeness rule layered over it, not a contract change. A step that asks for a field
/// and then waves it through is asking for nothing.
export type DetailsValues = {
  title: string;
  subtitle: string;
  description: string;
  startsAt: string;
  endsAt: string;
  venueName: string;
  city: string;
  venueAddress: string;
  capacity: string;
};

export type DetailsErrors = Partial<Record<keyof DetailsValues, string>>;

/// Field order = the order they appear on the step, so "the first thing still wrong" is also the
/// topmost thing on screen. `blockedReason` reads this rather than re-deciding a priority.
export const DETAILS_FIELD_ORDER = [
  "title", "subtitle", "description", "startsAt", "endsAt",
  "venueName", "city", "venueAddress", "capacity"
] as const satisfies readonly (keyof DetailsValues)[];

/// `now` is a parameter, not a module-level constant, for two reasons: a test must be able to pin it,
/// and the caller must be able to re-evaluate it on every render. Memoising it would freeze the clock
/// and let a start time that was valid when the step opened stay valid an hour later.
///
/// Times are compared in the **browser's** zone because that is the zone a `datetime-local` value is
/// already in — the same assumption `toIsoUtc` and `toLocalInput` are built on (D-289). Introducing a
/// second zone here would put the picker, the validator and the payload into three different clocks.
export function validateDetails(v: DetailsValues, now: Date = new Date()): DetailsErrors {
  const errors: DetailsErrors = {};

  // Whitespace is not content. `.trim()` on every text field is the whole of requirement §1's
  // "do not treat a field as completed if it contains only whitespace".
  if (!v.title.trim()) errors.title = "Title is required";
  else if (v.title.trim().length < 2) errors.title = "Title must be at least 2 characters";
  if (!v.subtitle.trim()) errors.subtitle = "Subtitle is required";
  if (!v.description.trim()) errors.description = "Description is required";

  const startsAt = v.startsAt ? new Date(v.startsAt) : null;
  const startValid = !!startsAt && !Number.isNaN(startsAt.getTime());
  if (!startValid) errors.startsAt = "Start date and time is required";
  else if (startsAt!.getTime() < now.getTime()) errors.startsAt = "Start date cannot be in the past";

  const endsAt = v.endsAt ? new Date(v.endsAt) : null;
  const endValid = !!endsAt && !Number.isNaN(endsAt.getTime());
  if (!endValid) errors.endsAt = "End date and time is required";
  // Only comparable once the start parses. Reported against `endsAt` even though *changing the start*
  // is what usually breaks it, because that is the field whose value is now wrong.
  else if (startValid && endsAt!.getTime() <= startsAt!.getTime()) {
    errors.endsAt = "End date and time must be after the start date";
  }

  if (!v.venueName.trim()) errors.venueName = "Venue name is required";
  if (!v.city.trim()) errors.city = "City is required";
  if (!v.venueAddress.trim()) errors.venueAddress = "Venue address is required";

  const capacity = v.capacity.trim();
  if (!capacity) errors.capacity = "Capacity is required";
  // `Number.isInteger` as well as `> 0`: `Capacity` binds to an `int?` server-side, so "1.5" is not a
  // small validation nicety — it is a 400 from the model binder with no field named.
  else if (!Number.isInteger(Number(capacity)) || Number(capacity) <= 0) {
    errors.capacity = "Capacity must be greater than 0";
  }

  return errors;
}

/// The first unmet requirement, in on-screen order — what a blocked Continue says out loud.
///
/// Order is passed in rather than sorted, so "the first thing still wrong" is also the topmost thing on
/// screen. Every step reads this; none re-decides a priority of its own.
export function firstError<K extends string>(
  errors: Partial<Record<K, string>>,
  order: readonly K[]
): string | null {
  for (const field of order) if (errors[field]) return errors[field]!;
  return null;
}

// ── The remaining steps' rules ────────────────────────────────────────────────────────────────────
//
// Each is a pure function over one step's state, mirroring a rule the server already enforces, and each
// returns field → message. Together with `validateDetails` they are the wizard's whole validation
// vocabulary: `create-event-wizard.tsx` composes them into one per-step lookup and derives Continue
// from it. Nothing here is a NEW business rule — every message below names an existing refusal in
// `EventService.ApplyFieldGroups` / `ValidateMode`, surfaced on the step that asks for the field
// instead of at the final POST.

/// An absolute URL, in the sense `type="url"` already promises and `Uri.TryCreate(..., Absolute)`
/// enforces server-side for `Website`. A bare "meet.google.com/x" is refused here for the same reason
/// the browser refuses it in a native form: without a scheme it is not a link.
function isAbsoluteUrl(value: string): boolean {
  try {
    new URL(value);
    return true;
  } catch {
    return false;
  }
}

export type ContentValues = { tagline: string; shortDescription: string; rules: string };
export const CONTENT_FIELD_ORDER = ["tagline", "shortDescription", "rules"] as const;

/// D-378 — Content is **required**, and this is the one place that decides it.
///
/// Every field on `EventContentInput` is still nullable on the wire, so this is a *submission
/// completeness* rule layered over the storage contract, not a change to it — the same relationship
/// `validateDetails` already has with `CreateEventBodyValidator`'s much smaller minimum. Drafts keep
/// saving blank (D-266 M8); what is refused is carrying a listing with no listing copy into review.
///
/// The two length ceilings stay: they mirror `ApplyFieldGroups`'s `tagline_too_long` /
/// `short_description_too_long`, and the inputs carry matching `maxLength`, so those remain the
/// backstop for state that did not come from typing.
export function validateContent(v: ContentValues): Partial<Record<keyof ContentValues, string>> {
  const errors: Partial<Record<keyof ContentValues, string>> = {};

  // Trimmed, because "   " is not a tagline. Same rule `validateDetails` applies to every text field.
  if (!v.tagline.trim()) errors.tagline = "Tagline is required";
  else if (v.tagline.length > 160) errors.tagline = "Tagline must be 160 characters or fewer";

  if (!v.shortDescription.trim()) errors.shortDescription = "Short description is required";
  else if (v.shortDescription.length > 300) {
    errors.shortDescription = "Short description must be 300 characters or fewer";
  }

  if (!v.rules.trim()) errors.rules = "Rules are required";

  return errors;
}

export type PlaceValues = {
  eventMode: string; onlineUrl: string; building: string; floor: string; room: string;
  googleMapsUrl: string; meetingPlatform: string; meetingPassword: string;
};
export const PLACE_FIELD_ORDER = [
  "eventMode", "building", "floor", "room", "googleMapsUrl",
  "onlineUrl", "meetingPlatform", "meetingPassword"
] as const;

/// D-378 — every field the step ASKS FOR is required, gated by whether the chosen Mode asks for it.
///
/// The condition is not a softening, it is the whole rule: a join link on an in-person event and a floor
/// number on an online one are not "missing", they do not exist. The wizard already renders these two
/// groups conditionally; this makes the validator agree with what is on screen, so Continue never waits
/// on a field the organiser cannot see.
///
/// `online_url_required` remains the server's own refusal; the rest are submission-completeness rules
/// with no storage-contract equivalent, exactly like the Content step's.
export function validatePlace(v: PlaceValues): Partial<Record<keyof PlaceValues, string>> {
  const errors: Partial<Record<keyof PlaceValues, string>> = {};
  if (!["Offline", "Online", "Hybrid"].includes(v.eventMode)) {
    errors.eventMode = "Choose how the event is held";
    return errors;   // Nothing below is answerable until the Mode is known.
  }

  const hasPhysical = v.eventMode !== "Online";
  const hasOnline = v.eventMode !== "Offline";

  if (hasPhysical) {
    if (!v.building.trim()) errors.building = "Building is required";
    if (!v.floor.trim()) errors.floor = "Floor is required";
    if (!v.room.trim()) errors.room = "Room is required";
    if (!v.googleMapsUrl.trim()) errors.googleMapsUrl = "Google Maps link is required";
    else if (!isAbsoluteUrl(v.googleMapsUrl.trim())) {
      errors.googleMapsUrl = "Google Maps link must be a full URL, including https://";
    }
  }

  if (hasOnline) {
    if (!v.onlineUrl.trim()) errors.onlineUrl = "A join link is required for an online or hybrid event";
    else if (!isAbsoluteUrl(v.onlineUrl.trim())) {
      errors.onlineUrl = "Join link must be a full URL, including https://";
    }
    if (!v.meetingPlatform.trim()) errors.meetingPlatform = "Platform is required";
    if (!v.meetingPassword.trim()) errors.meetingPassword = "Meeting password is required";
  }
  return errors;
}

export type WindowValues = {
  registrationOpensAt: string; registrationClosesAt: string;
  checkinOpensAt: string; checkinClosesAt: string;
  resultDate: string; certificateReleaseAt: string; autoClose: boolean;
};
export const WINDOW_FIELD_ORDER = [
  "registrationOpensAt", "registrationClosesAt", "checkinOpensAt", "checkinClosesAt",
  "resultDate", "certificateReleaseAt"
] as const;

/// D-378 — every window the step asks for is required, and each pair must still be ordered.
///
/// `isPrivate` mirrors the step's own rendering: a Private event is not shown the results or certificate
/// dates at all (the capability engine DESCRIBES, it never gates — D-266 M2), so requiring them would
/// block on two controls that are not on the page.
///
/// Ordering messages still mirror `invalid_registration_window` / `invalid_checkin_window`. Deliberately
/// NOT extended with a relationship to the event's own start/end: `ApplyFieldGroups` enforces no such
/// rule, and inventing one here would refuse events the API accepts.
export function validateWindows(
  v: WindowValues, isPrivate = false
): Partial<Record<keyof WindowValues, string>> {
  const errors: Partial<Record<keyof WindowValues, string>> = {};
  const at = (s: string) => (s ? new Date(s).getTime() : NaN);

  if (!v.registrationOpensAt) errors.registrationOpensAt = "Registration opening time is required";
  if (!v.registrationClosesAt) errors.registrationClosesAt = "Registration closing time is required";
  else if (v.registrationOpensAt && !(at(v.registrationClosesAt) > at(v.registrationOpensAt))) {
    errors.registrationClosesAt = "Registration must close after it opens";
  }

  if (!v.checkinOpensAt) errors.checkinOpensAt = "Check-in opening time is required";
  if (!v.checkinClosesAt) errors.checkinClosesAt = "Check-in closing time is required";
  else if (v.checkinOpensAt && !(at(v.checkinClosesAt) > at(v.checkinOpensAt))) {
    errors.checkinClosesAt = "Check-in must close after it opens";
  }

  if (!isPrivate) {
    if (!v.resultDate) errors.resultDate = "Results announcement time is required";
    if (!v.certificateReleaseAt) errors.certificateReleaseAt = "Certificate release time is required";
  }
  return errors;
}

export type EligibilityValues = {
  minAge: string; maxAge: string; genderRestriction: string; maxTeams: string;
};
export const ELIGIBILITY_FIELD_ORDER = ["minAge", "maxAge", "genderRestriction", "maxTeams"] as const;

/// D-378 — the ages and the team cap are required; the value rules that were already here still hold.
///
/// `invalid_age_range` (maximum below minimum) and the `maxTeams <= 0` trap are unchanged: `ApplyFieldGroups`
/// silently **discards** a non-positive cap (`MaxTeams <= 0 ? null`), so an organiser capping teams at 0 got
/// no cap and no warning. `isPrivate` mirrors the step's rendering — a Private event is never shown the
/// team cap, so it cannot be asked for one.
export function validateEligibility(
  v: EligibilityValues, isPrivate = false
): Partial<Record<keyof EligibilityValues, string>> {
  const errors: Partial<Record<keyof EligibilityValues, string>> = {};
  /// Blank is valid (no bound). A given bound must be a whole number in the range the inputs already
  /// declare with `min`/`max`, so state that did not come from typing is refused the same way.
  const bound = (raw: string): number | "invalid" | null => {
    const t = raw.trim();
    if (!t) return null;
    const n = Number(t);
    return Number.isInteger(n) && n >= 0 && n <= 120 ? n : "invalid";
  };
  const AGE_RANGE = "Age must be a whole number between 0 and 120";

  const min = bound(v.minAge);
  const max = bound(v.maxAge);
  // A Private event is shown NEITHER age bound nor the team cap — only Gender survives the filter, so
  // those three are required exactly when the step renders them. Requiring them regardless would leave
  // a wedding permanently stuck on a step with one control and three invisible objections.
  if (!isPrivate) {
    if (min === null) errors.minAge = "Minimum age is required";
    else if (min === "invalid") errors.minAge = AGE_RANGE;
    if (max === null) errors.maxAge = "Maximum age is required";
    else if (max === "invalid") errors.maxAge = AGE_RANGE;

    const teams = v.maxTeams.trim();
    if (!teams) errors.maxTeams = "Maximum teams is required";
    else if (!Number.isInteger(Number(teams)) || Number(teams) <= 0) {
      errors.maxTeams = "Maximum teams must be greater than 0";
    }
  } else {
    // Still refuse a nonsense value that arrived some other way (a restored draft, a devtools edit).
    if (min === "invalid") errors.minAge = AGE_RANGE;
    if (max === "invalid") errors.maxAge = AGE_RANGE;
  }
  if (typeof min === "number" && typeof max === "number" && max < min) {
    errors.maxAge = "Maximum age must be at least the minimum age";
  }

  if (!["Any", "Male", "Female", "NonBinary"].includes(v.genderRestriction)) {
    errors.genderRestriction = "Choose who the event is open to";
  }
  return errors;
}

export type LegalValues = {
  termsUrl: string; codeOfConduct: string; refundPolicy: string; cancellationPolicy: string;
  requiresConsent: boolean; consentText: string;
};
export const LEGAL_FIELD_ORDER = [
  "termsUrl", "codeOfConduct", "refundPolicy", "cancellationPolicy", "consentText"
] as const;

/// D-378 — the four policy fields are required; the consent text stays conditionally required.
///
/// `consent_text_required` is the server's own refusal and the one rule here that predates this: consent
/// recorded against an empty string is evidence of nothing. The terms URL keeps the `type="url"` promise
/// the control was making and nothing was keeping.
export function validateLegal(v: LegalValues): Partial<Record<keyof LegalValues, string>> {
  const errors: Partial<Record<keyof LegalValues, string>> = {};

  if (!v.termsUrl.trim()) errors.termsUrl = "Terms link is required";
  else if (!isAbsoluteUrl(v.termsUrl.trim())) {
    errors.termsUrl = "Terms link must be a full URL, including https://";
  }
  if (!v.codeOfConduct.trim()) errors.codeOfConduct = "Code of conduct is required";
  if (!v.refundPolicy.trim()) errors.refundPolicy = "Refund policy is required";
  if (!v.cancellationPolicy.trim()) errors.cancellationPolicy = "Cancellation policy is required";

  // Conditional, and deliberately so: consent OFF means there is no statement to write.
  if (v.requiresConsent && !v.consentText.trim()) {
    errors.consentText = "Write the statement registrants must accept, or turn consent off";
  }
  return errors;
}

/// D-366 — one team-size price band, as the form holds it. Rupee strings, converted at submit like every
/// other money field in this file (D-004 keeps paise on the wire).
export type TierValues = { minSize: string; maxSize: string; priceRupees: string };

export type TicketValues = {
  name: string; priceRupees: string; quantity: string;
  /// D-372 — how people enter, which is what gives the price its unit.
  participation: "individual" | "team";
  teamMin: string; teamMax: string;
  /// D-366 — empty means the team pays one price whatever its size, which is the D-372 behaviour and
  /// stays the default. A non-empty set must cover teamMin..teamMax exactly once.
  tiers: TierValues[];
};
export const TICKET_FIELD_ORDER = [
  "name", "participation", "priceRupees", "quantity", "teamMin", "teamMax", "tiers"
] as const;

/// The registration option created with the event, and the unit its price is charged in (D-372).
///
/// Without one the event is unbookable, so these are genuinely required even though `TicketType` is a
/// separate resource — this step is what creates it. The domain already carries every concept here:
/// `participation` maps to `RegistrationMode`, and to `PricingUnit` — `team` ⇒ `PerGroup` (one charge,
/// one slot per team), `individual` ⇒ `PerTicket`. Nothing new is invented client-side.
///
/// `teamMin`/`teamMax` become `GroupMin`/`GroupMax`, which `CreateOrderAsync` enforces as
/// `invalid_group_size` and which D-373 makes the upper bound on any later `TeamPolicy`.
export function validateTicket(
  v: TicketValues, pricing: "free" | "paid"
): Partial<Record<(typeof TICKET_FIELD_ORDER)[number], string>> {
  const errors: Partial<Record<(typeof TICKET_FIELD_ORDER)[number], string>> = {};
  if (!v.name.trim()) errors.name = "Name the registration people will book";

  const qty = v.quantity.trim();
  if (!qty || !Number.isInteger(Number(qty)) || Number(qty) <= 0) {
    // The unit is the point: for a team event this is a count of TEAMS, because a PerGroup
    // registration takes exactly one inventory unit however many people are on the roster.
    errors.quantity = v.participation === "team"
      ? "Set how many teams can enter (at least 1)"
      : "Set how many places are available (at least 1)";
  }

  // A free event must not be made to type a price — §15. Zero is refused only when Paid was chosen,
  // because a paid event charging nothing publishes something worse than a refusal.
  //
  // D-366 — with bands, the single price field is not asked at all: the bands ARE the price, and the
  // ticket's headline is derived server-side from the cheapest one.
  const banded = v.participation === "team" && v.tiers.length > 0;
  if (pricing === "paid" && !banded) {
    const price = v.priceRupees.trim();
    if (!price || !(Number(price) > 0)) errors.priceRupees = "Set a price above zero, or choose Free";
  }

  if (v.participation === "team") {
    const min = v.teamMin.trim();
    const max = v.teamMax.trim();
    if (!min || !Number.isInteger(Number(min)) || Number(min) < 1) {
      errors.teamMin = "Set the smallest team size (at least 1)";
    }
    if (!max || !Number.isInteger(Number(max)) || Number(max) < 1) {
      errors.teamMax = "Set the largest team size (at least 1)";
    } else if (min && Number(max) < Number(min)) {
      errors.teamMax = "The largest team size must be at least the smallest";
    }
    if (v.tiers.length > 0) {
      const tierError = validateTiers(v.tiers, min, max, pricing);
      if (tierError) errors.tiers = tierError;
    }
  }
  return errors;
}

/// D-366 — the band set, checked as a SET.
///
/// Mirrors `TicketTypeService.ValidateTiers` code for code, so the form refuses what the server would
/// refuse rather than letting an organiser fill twelve fields and discover it at submit. The server
/// remains the authority — this only moves the sentence to where the numbers are still on screen.
///
/// Every rule here is invisible row by row: each band can be individually sane while the set leaves a
/// size unpriced ("2 and 4–5" refuses every team of 3) or prices one twice ("2–4 and 3–5").
export function validateTiers(
  tiers: TierValues[], teamMin: string, teamMax: string, pricing: "free" | "paid"
): string | null {
  // A free event has no bands at all — zero-priced rules would be a price that means "no price".
  if (pricing === "free") return "A free event has no prices to set by team size";

  const min = Number(teamMin);
  const max = Number(teamMax);
  if (!Number.isInteger(min) || !Number.isInteger(max) || min < 1 || max < min) {
    return "Set the smallest and largest team size first";
  }

  const parsed = tiers.map((t) => ({
    min: Number(t.minSize), max: Number(t.maxSize), price: Number(t.priceRupees),
  }));
  for (const t of parsed) {
    if (!Number.isInteger(t.min) || !Number.isInteger(t.max) || t.min < 1 || t.max < t.min) {
      return "Each rule needs a team size range, smallest first";
    }
    if (!(t.price > 0)) return "Each rule needs a price above zero";
    if (t.min < min || t.max > max) return `Keep every rule between ${min} and ${max} members`;
  }

  const ordered = [...parsed].sort((a, b) => a.min - b.min);
  for (let i = 1; i < ordered.length; i++) {
    if (ordered[i].min <= ordered[i - 1].max) return "Two rules cover the same team size";
  }
  if (ordered[0].min !== min || ordered[ordered.length - 1].max !== max) {
    return `Cover every team size from ${min} to ${max}`;
  }
  for (let i = 1; i < ordered.length; i++) {
    if (ordered[i].min !== ordered[i - 1].max + 1) {
      return `No price for teams of ${ordered[i - 1].max + 1}`;
    }
  }
  return null;
}

/// The bands as the API takes them (D-004: paise on the wire). Empty in, undefined out — the server
/// treats absent and empty alike, and sending `[]` would claim the organiser cleared a set they never had.
export function tiersToPayload(tiers: TierValues[]): { minSize: number; maxSize: number; pricePaise: number }[] | undefined {
  if (tiers.length === 0) return undefined;
  return tiers.map((t) => ({
    minSize: Number(t.minSize),
    maxSize: Number(t.maxSize),
    pricePaise: Math.round(Number(t.priceRupees) * 100),
  }));
}

/// Does this archetype support team entry?
///
/// Read from the capability engine's own answer rather than a list of type names: `Unsupported` is the
/// only state that means "this event cannot have teams", and it is the engine's word for it (D-266 M2).
/// A missing archetype — an unclassified Type — leaves the row absent, which is also "no teams", because
/// the engine cannot say otherwise and the wizard must not guess.
///
/// Hardcoding "hackathon | competition | tournament" here is exactly what D-372 forbids: the matrix is
/// data, an admin can change it, and a second copy in the client would silently disagree.
export function archetypeSupportsTeams(
  capabilities: { slug: string; state: string }[] | null | undefined
): boolean {
  const teams = capabilities?.find((c) => c.slug === "teams");
  return !!teams && teams.state.toLowerCase() !== "unsupported" && teams.state.toLowerCase() !== "locked";
}

export type AuthorizationValues = {
  headName: string; headDesignation: string; officialEmail: string; officialPhone: string;
  representativeRole: string; representativeRoleOther: string;
};
export const AUTHORIZATION_FIELD_ORDER = [
  "headName", "headDesignation", "officialEmail", "officialPhone",
  "representativeRole", "representativeRoleOther", "letterFile"
] as const;

/// D-351. Every message names the server's own refusal — `authorization_fields_required`,
/// `official_phone_invalid`, `representative_role_other_required`, `letterhead_required` — so the wizard
/// blocks for the reasons the API would, while the field is still on screen.
export function validateAuthorization(
  v: AuthorizationValues, letterAttached: boolean
): Partial<Record<(typeof AUTHORIZATION_FIELD_ORDER)[number], string>> {
  const errors: Partial<Record<(typeof AUTHORIZATION_FIELD_ORDER)[number], string>> = {};
  if (!v.headName.trim()) errors.headName = "Name the signatory who authorises this event";
  if (!v.headDesignation.trim()) errors.headDesignation = "Give the signatory's designation";
  if (!v.officialEmail.trim()) errors.officialEmail = "Give the organization's official email";
  // The same shape `EventAuthorizationBodyValidator` matches. Anything looser offers the organiser a
  // green button and then a 400.
  else if (!/^[^@\s]+@[^@\s.]+\.[^@\s]+$/.test(v.officialEmail.trim())) {
    errors.officialEmail = "Enter a valid email address";
  }
  if (!v.officialPhone.trim()) errors.officialPhone = "Give the organization's official phone";
  else if (!/^\+[1-9]\d{7,14}$/.test(v.officialPhone.trim())) {
    errors.officialPhone = "Phone must be in international format, e.g. +919876543210";
  }
  if (!v.representativeRole) errors.representativeRole = "Choose your role in the organization";
  else if (v.representativeRole === "Other" && !v.representativeRoleOther.trim()) {
    errors.representativeRoleOther = "Describe your role, since you chose Other";
  }
  if (!letterAttached) errors.letterFile = "Attach the authorization letter";
  return errors;
}

/// Applies `toIsoUtc` to named `datetime-local` fields of a `FormData` before a server action reads it.
///
/// The forms that post through a server action had no equivalent of the wizard's conversion, so they
/// sent the wall-clock string bare and the server read it as UTC. Because those same forms render the
/// stored instant back through a browser-local formatter, the error **compounded**: an event typed at
/// 10:00 was stored at 15:30, and one save that changed nothing moved it to 21:00 (D-289). A form is
/// only lossless when both directions agree on the zone, and this is the return leg.
///
/// A blank field is left untouched: on these endpoints a missing field means "leave alone", so
/// inventing a timestamp here would overwrite a good one with a guess.
export function withUtcTimes(formData: FormData, fields: readonly string[] = ["startsAt", "endsAt"]): FormData {
  for (const field of fields) {
    const local = formData.get(field);
    if (typeof local !== "string" || local === "") continue;
    const iso = toIsoUtc(local);
    if (iso) formData.set(field, iso);
  }
  return formData;
}
