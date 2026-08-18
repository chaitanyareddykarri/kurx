"use client";

import { useEffect, useMemo, useState, useTransition } from "react";
import { useRouter } from "next/navigation";
import { Globe, EyeOff, Lock, Ticket, ShieldCheck, Check } from "lucide-react";
import { Button, DateTimeField, Field, FormSteps, Input, Select, Spinner, Textarea, controlClass } from "@kurx/ui";
import { SelectCard, SelectCardGroup } from "@/components/host/select-card-group";
import { CreateOrgForm } from "@/components/host/create-org-form";
import { createEventWizardAction, CreateEventValues,
  submitAuthorizationAction, uploadAuthorizationDocumentAction } from "@/lib/event-actions";
import type { Category, FieldPreset, Representation } from "@/lib/api";
import {
  AUTHORIZATION_FIELD_ORDER, categoriesFor, cleanGroup, CONTENT_FIELD_ORDER, DETAILS_FIELD_ORDER,
  ELIGIBILITY_FIELD_ORDER, firstError, LEGAL_FIELD_ORDER, PLACE_FIELD_ORDER, TICKET_FIELD_ORDER,
  toIsoUtc, toLocalInput, typesFor, validateAuthorization, validateContent, validateDetails,
  validateEligibility, validateLegal, validatePlace, validateTicket, validateWindows,
  archetypeSupportsTeams, WINDOW_FIELD_ORDER, tiersToPayload
} from "@/lib/event-wizard";
import type { TicketValues } from "@/lib/event-wizard";

const inputClass = controlClass;
const textareaClass = controlClass;

/// D-266 M8 — the three values `EventVisibility` actually has.
///
/// `Private` used to be a fourth option here and had been dead since migration
/// `RetireLegacyPrivateVisibility`: the server's `Enum.TryParse` rejected it, so an organiser choosing it
/// silently got the default. Presentation lives here; **which values are legal is the server's** — a
/// Private product is never Listed, and `PolicyResolver` refuses that combination whatever this list says.
/// D-307 — a Private product can NEVER be Listed: `PolicyResolver` answers
/// `private_product_cannot_be_listed`, so offering it would accept an input the server refuses eleven
/// steps later. Filtered here rather than validated at publish.
function visibilityFor(product: "Public" | "Private") {
  return product === "Private" ? VISIBILITY.filter((v) => v.value !== "Listed") : VISIBILITY;
}

const VISIBILITY = [
  { value: "Listed", label: "Listed", icon: Globe, desc: "Listed in discovery and search. Anyone can find and book." },
  { value: "Unlisted", label: "Unlisted", icon: EyeOff, desc: "Hidden from discovery. Only people with the link can view." },
  { value: "InviteOnly", label: "Invite only", icon: Lock, desc: "Not discoverable. Only invited people, or someone holding an invite link, can register." }
] as const;

// The product flow names 18 steps. Nine of them (tickets, teams, modules, staff, sponsors, media,
// preview, submit, finance) configure an event that must already exist, and each already has its own
// workspace tab — duplicating them here would be a second implementation of each. This wizard's job
// is to produce a Draft complete enough to publish; the workspace finishes it.
/*
 * There is ONE free/paid decision, and this form is not where it is made.
 *
 * The gate asks it (`create-event-gate.tsx`) because D-343 uses the product+pricing PAIR to select the
 * verification tier the caller must clear before this form opens at all. This wizard used to ask it a
 * second time at step 2 — the same question, with a control that could change the answer, after the
 * eligibility decision had already been made on it. Two problems, one cause:
 *
 *   · the organiser was asked something they had just answered, and
 *   · the two guards were not the same guard. The gate refuses Paid unless the caller is BOTH
 *     `canHostPaid` AND representing a verified organization; this step's `canChoosePaid` checked only
 *     the first. So someone the gate had refused could re-select Paid here and spend eleven steps on an
 *     event the server would reject at submit.
 *
 * The step is gone and `pricing` is now a read-only value carried from the gate. Changing free↔paid is
 * a decision with verification consequences, so it belongs where those consequences are evaluated.
 */
const STEPS = [
  "Representing", "Visibility", "Category", "Type", "Registration", "Details",
  "Content", "Location", "Windows", "Eligibility", "Legal"
];

/*
 * Names for the step indices, because inserting "Registration" shifted six of them.
 *
 * The whole file addressed steps as bare numbers, so adding one meant renumbering seven `step === N`
 * guards and an entry in two index-ordered tables — a rename with seven chances to silently point a
 * panel at the wrong step. `STEP.details` cannot be off by one; `step === 6` can. Removing Pricing just
 * proved the point: it shifted nine of these, and not one `step === N` had to be found by eye.
 */
const STEP = {
  representing: 0, visibility: 1, category: 2, type: 3, registration: 4,
  details: 5, content: 6, location: 7, windows: 8, eligibility: 9, legal: 10
  // `authorization: 11` lived here until D-382. D-379 had already folded the letter into Representing
  // and deleted the step, but its index survived — an unreferenced name for a step that does not
  // exist, and the next person to add a step would have had to decide what it meant.
} as const;

const GENDERS = ["Any", "Male", "Female", "NonBinary"] as const;

function card(selected: boolean, disabled = false) {
  return `w-full rounded-lg border p-4 text-left transition ${
    disabled
      ? "cursor-not-allowed border-border opacity-50"
      : selected
        ? "border-accent ring-1 ring-accent"
        : "border-border hover:border-accent/60"
  }`;
}

/// Reads the field labels a preset would seed into the registration form (metadata-driven preview).
function presetFields(preset: FieldPreset): string[] {
  try {
    const parsed = JSON.parse(preset.payload) as { fields?: { label?: string }[] };
    return (parsed.fields ?? []).map((f) => f.label).filter((l): l is string => !!l);
  } catch {
    return [];
  }
}

export function CreateEventWizard({
  representations: initialRepresentations,
  canHostPaid,
  categories,
  subcategories,
  presets,
  product,
  initialPricing = "free",
  requiresRepresentation,
  representativeRoles,
  teamCapableArchetypes
}: {
  /// Institutions the caller may represent, as the server knows them when the wizard opens. Empty is a
  /// normal, workable state: the Representing step registers one in place, so nobody has to arrive with
  /// a representation already in hand.
  representations: Representation[];
  canHostPaid: boolean;
  categories: Category[];
  subcategories: Category[];
  presets: FieldPreset[];
  /// Chosen in the gate before this form opened. Selects which Types are offered — never sent to the
  /// server, which derives `Product` from the Type itself.
  product: "Public" | "Private";
  /// Already answered in the gate (D-343): the free/paid pair is what selected the verification tier
  /// the caller just cleared. Carried in as the starting value rather than re-asked from scratch, and
  /// still changeable here — the Paid card stays gated on `canHostPaid`, so moving to Paid after
  /// entering as Free cannot escape the financial tier.
  initialPricing?: "free" | "paid";
  /// The closed representative-role vocabulary from the server that validates it (D-266 M5). A copy
  /// held here is how a role gets offered and then refused by the API.
  /// D-353/D-352 — see the gate. False under the dev bypass, where self-hosting a Public event is
  /// what the server will accept.
  requiresRepresentation: boolean;
  representativeRoles: string[];
  /// D-372 — archetype slugs whose `teams` capability is not Unsupported, resolved server-side from the
  /// capability engine. Empty means no Type on offer permits team entry, which is the closed default.
  teamCapableArchetypes: string[];
}) {
  const router = useRouter();
  const [isPending, startTransition] = useTransition();
  const [step, setStep] = useState(0);
  /// The caller's representations, held locally because this step can ADD one: registering an
  /// institution inline appends it here and selects it, with no reload and no navigation. Seeded from
  /// the server's list; the server stays the authority on what is verified.
  const [representations, setRepresentations] = useState<Representation[]>(initialRepresentations);

  /// null until an organization is chosen. There is no "Personal" answer to fall back to (D-379), so
  /// this opens on the first organization that could carry the event all the way — a verified one —
  /// rather than on a pending one whose selection Continue would accept but publish would later stop.
  const [representingOrgId, setRepresentingOrgId] = useState<string | null>(
    initialRepresentations.find((r) => r.can_back_paid_event ?? r.is_verified)?.organization_id
      ?? initialRepresentations[0]?.organization_id ?? null);
  const [error, setError] = useState<string | null>(null);

  // Unlisted is the only sane default for Private — Listed is forbidden and InviteOnly is a stronger
  // claim than the organiser has made yet.
  const [visibility, setVisibility] = useState<string>(product === "Private" ? "Unlisted" : "Listed");
  /// Read-only, and a `const` rather than state on purpose: the gate owns this decision (D-343 selects
  /// the verification tier from it), so a setter here is the bug — the form would be able to move an
  /// event to Paid after the eligibility for Paid had been decided against it.
  const pricing = initialPricing;
  /// The one ticket created with the event. Without it the event is unbookable — see the Pricing step.
  /// Rupees on the way in, paise on the wire (D-004).
  /// D-372 — `participation` is the registration UNIT, and it is what gives the price its meaning.
  /// It maps to `RegistrationMode` and `PricingUnit` together: team ⇒ Group + PerGroup (one charge and
  /// one inventory unit per team), individual ⇒ Individual + PerTicket. Defaults to individual, which is
  /// what every event created before this step existed already was.
  const [ticket, setTicket] = useState<TicketValues>({
    name: "General Admission", priceRupees: "", quantity: "100",
    participation: "individual", teamMin: "2", teamMax: "4",
    // D-366 — empty is the default and means one price for every team size, exactly as before.
    tiers: []
  });

  const [categoryId, setCategoryId] = useState<string>("");
  const [typeId, setTypeId] = useState<string>("");

  /*
   * Whether the chosen Type's archetype supports teams — the capability engine's answer, resolved on
   * the server and passed in.
   *
   * `archetype_slug` is on the Type node (D-372 exposed it; the column was always there and D-266 M1
   * snapshots it onto the event). The page asks `GET /v1/archetypes/{slug}/capabilities` for each
   * archetype it offers and hands down the set that permits `teams`, so this component performs no
   * fetch, has no loading state, and — importantly — pulls in no server-only module.
   *
   * A hardcoded list of "team-ish" type names here would be a second copy of a matrix an admin can
   * edit, and would disagree with the server the first time they did.
   */
  const selectedArchetype = subcategories.find((s) => s.id === typeId)?.archetype_slug ?? null;
  const teamsSupported = selectedArchetype !== null && teamCapableArchetypes.includes(selectedArchetype);

  /// Reconciled, never left invalid (§14): if the Type stops supporting teams, the answer reverts to
  /// individual on the same render rather than travelling to a submission the server would refuse.
  useEffect(() => {
    if (!teamsSupported && ticket.participation === "team") {
      setTicket((t) => ({ ...t, participation: "individual" }));
    }
  }, [teamsSupported, ticket.participation]);
  const [details, setDetails] = useState({
    title: "",
    subtitle: "",
    description: "",
    startsAt: "",
    endsAt: "",
    venueName: "",
    city: "",
    venueAddress: "",
    capacity: ""
  });

  // D-265 field groups, one piece of state per wizard step so a step's inputs stay together.
  const [content, setContent] = useState({ tagline: "", shortDescription: "", rules: "" });
  const [place, setPlace] = useState({
    eventMode: "Offline",
    onlineUrl: "",
    building: "",
    floor: "",
    room: "",
    googleMapsUrl: "",
    meetingPlatform: "",
    meetingPassword: ""
  });
  const [windows, setWindows] = useState({
    registrationOpensAt: "",
    registrationClosesAt: "",
    checkinOpensAt: "",
    checkinClosesAt: "",
    resultDate: "",
    certificateReleaseAt: "",
    autoClose: false
  });
  const [eligibility, setEligibility] = useState({
    minAge: "",
    maxAge: "",
    genderRestriction: "Any",
    maxTeams: ""
  });
  const [legal, setLegal] = useState({
    termsUrl: "",
    codeOfConduct: "",
    refundPolicy: "",
    cancellationPolicy: "",
    requiresConsent: false,
    consentText: ""
  });

  /// D-351 — the institution's written consent, collected in-flow. The letter is held as a File until
  /// the event exists, because both presign and submit are keyed on an eventId that only the final POST
  /// produces. Nothing is uploaded until then, so abandoning the wizard leaves no orphaned object.
  const [authorization, setAuthorization] = useState({
    headName: "",
    headDesignation: "",
    officialEmail: "",
    officialPhone: "",
    representativeRole: "",
    representativeRoleOther: ""
  });
  const [letterFile, setLetterFile] = useState<File | null>(null);

  /// D-327 — the private-gathering archetype marks `scoring`, `certificates` and `teams` Unsupported,
  /// so three inputs on the Windows and Eligibility steps described capabilities the event cannot have.
  const isPrivate = product === "Private";

  /// Only the Types this product class permits, and then only those under the chosen category.
  const allowedTypes = useMemo(() => typesFor(subcategories, product), [subcategories, product]);
  const subs = useMemo(() => allowedTypes.filter((s) => s.parent_id === categoryId), [allowedTypes, categoryId]);

  /// A category is offered only if it can actually produce an event of this product class — see
  /// `categoriesFor`, which is where the rule lives and is tested.
  const shownCategories = useMemo(
    () => categoriesFor(categories, subcategories, product),
    [categories, subcategories, product]);
  const selectedType = subcategories.find((s) => s.id === typeId);
  const typePresetFields = useMemo(() => {
    if (!selectedType) return [];
    const p = presets.find((x) => x.slug === selectedType.slug);
    return p ? presetFields(p) : [];
  }, [presets, selectedType]);

  /*
   * Every field the Details step ASKS for, checked on the step that asks for it.
   *
   * This used to be `title.trim().length >= 2 && datesOrdered` — `CreateEventBodyValidator`'s minimum,
   * not the step's. Six inputs the step renders (subtitle, description, venue name, city, venue
   * address, capacity) were therefore never checked at all, so Continue lit up on a step that was
   * mostly blank: the button meant "the server would accept this", not "this step is complete".
   *
   * Deliberately NOT memoised. `validateDetails` defaults `now` to the current instant, and a
   * `useMemo` keyed on `details` would freeze that clock — a start time typed at 16:35 would still
   * read as valid at 18:00. Re-deriving on each render is what makes §2's "validate continuously"
   * true for the *passage of time* as well as for edits, and it is a nine-field object comparison.
   */
  const detailsErrors = validateDetails(details);
  const detailsValid = Object.keys(detailsErrors).length === 0;

  /*
   * The picker's own floor, so the past is unreachable rather than merely refused (§3/§5).
   *
   * `toLocalInput` renders an instant as a `datetime-local` value in the browser's zone — the same
   * conversion the payload leg uses, so the control, the validator and the wire agree on one clock.
   * Recomputed each render for the same reason `detailsErrors` is.
   *
   * A `min` is a floor the browser enforces on the picker UI, never a guarantee: it is trivially
   * removed in devtools and absent entirely from a hand-rolled request. It is layer 1 of four
   * (picker → `validateDetails` → `submit` → `CreateEventBodyValidator`), not the defence.
   */
  const nowLocal = toLocalInput(new Date().toISOString());
  // The End floor tracks Start, so choosing a later Start narrows what End will offer. When Start is
  // unset or unparseable the floor falls back to now — never to nothing.
  const endsAtMin = details.startsAt && !Number.isNaN(new Date(details.startsAt).getTime())
    ? details.startsAt
    : nowLocal;

  // D-379 — computed here so Step 1 can merge it in; the Authorization step no longer exists.
  const authorizationErrors: Record<string, string> = validateAuthorization(authorization, letterFile !== null);
  const contentErrors = validateContent(content);
  const placeErrors = validatePlace(place);
  // D-378 — `isPrivate` is passed because a Private event is never SHOWN the results/certificate dates
  // or the team cap, and a step must never wait on a control that is not on the page.
  const windowErrors = validateWindows(windows, isPrivate);
  const eligibilityErrors = validateEligibility(eligibility, isPrivate);
  const legalErrors = validateLegal(legal);
  const ticketErrors = validateTicket(ticket, pricing);

  /*
   * Two sets, because "may this back a draft?" and "may this back a paid event?" are different
   * questions and collapsing them is what used to strand people.
   *
   * `selectableReps` is every representation the caller holds, PendingReview included. A staged
   * organization can carry a Draft: `EventService.CreateAsync` refuses only a self-representation row
   * or a deleted one, and `ResolveOrgAsync` grants Manager off the pending `Representative` seat. What
   * a pending organization cannot do is PUBLISH — `pending_org_verification` blocks that at transition
   * time and is untouched here. Filtering it out of the picker did not enforce that rule (the server
   * already did); it only meant someone who registered their college inside this step had nothing to
   * select afterwards and could not finish the event they were in the middle of creating.
   *
   * `paidCapableReps` keeps the stricter bar exactly where it was: money needs a verified institution,
   * so the Registration step still gates on this set and the server still refuses the rest at
   * submit-for-review.
   */
  const selectableReps = representations;
  const paidCapableReps = representations.filter((r) => r.can_back_paid_event ?? r.is_verified);
  const representingIsPending =
    representingOrgId !== null && !paidCapableReps.some((r) => r.organization_id === representingOrgId);
  /*
   * D-379 — every event represents a real organization. Both escapes are gone:
   *
   *   · `product === "Private"` — a private event represents somebody too. Visibility never decided
   *     who is answerable for an event.
   *   · `!requiresRepresentation` — the dev bypass. It lifts the identity proofs because those are
   *     mock-backed; whether an event names an organization is not, and an environment that needs a
   *     represented event seeds a real test organization.
   *
   * The organization must be one the caller may actually act for — which `selectableReps` is, since it
   * is the server's own list of the caller's representations. The server refuses anything else with
   * `representation_required`; this is presentation over that rule, never a substitute for it.
   */
  const representingValid = selectableReps.some((r) => r.organization_id === representingOrgId);

  /*
   * D-351 — institutional authorization, asked HERE rather than on a page after creation.
   *
   * `PolicyResolver` raises `event_authorization_required` for exactly this shape — a Public event that
   * represents an institution — and it is a publish blocker. It used to be collected only on
   * /host/events/[id]/readiness, so the organiser finished eleven steps, landed on a draft, and then
   * discovered a twelfth requirement on a different page. That is the same "found out at the end"
   * failure D-305 exists to remove, one screen further along.
   *
   * The step is appended rather than inserted so every existing index-keyed rule below is untouched;
   * it also reads correctly last, since it is the consent that accompanies a finished proposal.
   */
  /*
   * D-379 — authorization is required for EVERY event, so the step is always present.
   *
   * Was `product === "Public" && representingOrgId !== null`: a private event carried no letter, and
   * neither did any event created before an organization was picked. Both arms are gone — the letter
   * proves "this representative may run THIS event for this organization", which a wedding needs as much
   * as a conference. `PolicyResolver` raises `event_authorization_required` unconditionally now, and
   * `submit_review` refuses without it, so a conditional step here would only hide the refusal until the
   * end of the wizard.
   */
  /*
   * D-379 — authorization is collected on Step 1 (Representing), not as a step of its own.
   *
   * It was `product === "Public" && representingOrgId !== null`, appended after Legal. Now every event
   * carries a letter and it is asked with the organization it authorises. The flag survives only to
   * drive the upload after create.
   */
  const needsAuthorization = true;
  const steps = STEPS;

  /*
   * ── The wizard's one validation mechanism ────────────────────────────────────────────────────────
   *
   * `currentStep → the step's rules → errors → Continue enabled/disabled`, for EVERY step, from ONE
   * table. This replaces a hand-written boolean disjunction whose per-step clauses were written
   * independently, which is the systemic defect:
   *
   *   · steps 6 (Content), 7 (Location) and 9 (Eligibility) had NO clause at all — they appeared as a
   *     bare `step === 6 || step === 7 || step === 9`, an unconditional pass. Location was the
   *     expensive one: `ValidateMode` refuses an Online/Hybrid event with no join link
   *     (`online_url_required`), so an organiser chose Online, left the link blank, walked four more
   *     steps and lost the lot to a refusal at submit.
   *   · step 5 (Details) checked 3 of the 9 fields it renders — `CreateEventBodyValidator`'s minimum
   *     rather than the step's, which is why Continue lit up on a nearly-blank step.
   *   · step 8 (Windows) checked pair ordering but nothing else.
   *
   * Every entry returns field → message, so one derivation feeds four consumers: the button, the
   * spoken reason, the click guard, and the submit backstop. A step with no rules returns `{}` — an
   * explicit statement that it is all-optional, not an omission that looks like one.
   *
   * Nothing here is memoised. Every value is derived from current state on every render, which is what
   * makes §15 (no stale validity) and §16 (navigating back recomputes) true by construction rather
   * than by remembering to invalidate something.
   */
  const stepErrors: Record<string, string>[] = [
    /*
     * 0 · Representing — D-379. The organization AND this event's own authorization, in one step.
     *
     * The authorization used to be a separate step appended after Legal, asked only of public
     * institutional events. It is asked here, of every event, because the two answers are one question:
     * "who is this event for, and who says you may run it for them?" Splitting them let an organiser
     * walk ten steps before learning the second half was required.
     *
     * Both halves gate Continue together — `authorizationErrors` is merged in rather than kept in a
     * step of its own, so `canNext` and `blockedReason` need no special case for it.
     */
    {
      ...(representingValid
        ? {}
        : {
            representingOrgId: selectableReps.length > 0
              ? "Choose the organization you are hosting this event on behalf of"
              // Names the action that is now ON this step. It used to end "Request representation to
              // continue", which described a link to somewhere else — the thing that has been removed.
              : "Every event must represent an organization Kurx has verified. Add yours below to continue"
          }),
      ...authorizationErrors
    },
    // 1 · Visibility — one of a server-filtered list, always preselected, so this can only fail if the
    // value was tampered with.
    visibilityFor(product).some((v) => v.value === visibility) ? {} : { visibility: "Choose who can find this event" },
    // 2 · Category.
    categoryId ? {} : { categoryId: "Choose a category to continue" },
    // 4 · Type — required only when the category HAS types. The step says so on screen for the empty
    // case, and the server takes TypeId as optional.
    subs.length === 0 || typeId ? {} : { typeId: "Choose a type to continue" },
    // 4 · Registration — the unit, the price in that unit, and the team bounds when there are teams.
    //
    // The two paid-event eligibility checks moved here from the deleted Pricing step. They still have to
    // run inside the form — the gate cleared them before it opened, but Representing is chosen HERE, and
    // a paid event hosted under a personal name is refused at submit — and this is the first step where
    // money is actually typed, so it is where saying "you cannot charge" belongs.
    {
      ...ticketErrors,
      ...(pricing === "free" || canHostPaid
        ? {}
        : { priceRupees: "Paid events need identity, PAN and a verified bank account — verify first" }),
      // Deliberately `paidCapableReps`, not `representingValid`: a PendingReview organization may carry
      // a free draft but may never back a paid one, and the server refuses that at submit-for-review.
      ...(pricing === "free" || paidCapableReps.some((r) => r.organization_id === representingOrgId)
        ? {}
        : { name: "Go back to Representing and choose a verified organization — a paid event needs one Kurx has already verified" }),
    },
    // 5 · Details.
    detailsErrors,
    // 6 · Content — all optional (every field on `EventContentInput` is nullable); only the two length
    // ceilings apply.
    contentErrors,
    // 7 · Location — all optional EXCEPT the join link, which Online/Hybrid makes required.
    placeErrors,
    // 8 · Windows — all optional; each pair is ordered only when both ends are given.
    windowErrors,
    // 9 · Eligibility — all optional; the values, once given, have ranges.
    eligibilityErrors,
    // 10 · Legal — all optional except the consent text, which requiring consent makes required.
    legalErrors
  ];

  /// On-screen order per step, so the spoken reason is always the topmost unmet requirement.
  /// Index-aligned with `stepErrors` and with `STEPS`. Registration sits at `STEP.registration`.
  const stepFieldOrder: readonly string[][] = [
    ["representingOrgId"], ["visibility"],
    ["categoryId"], ["typeId"], [...TICKET_FIELD_ORDER], [...DETAILS_FIELD_ORDER],
    [...CONTENT_FIELD_ORDER], [...PLACE_FIELD_ORDER], [...WINDOW_FIELD_ORDER],
    [...ELIGIBILITY_FIELD_ORDER], [...LEGAL_FIELD_ORDER], [...AUTHORIZATION_FIELD_ORDER]
  ];

  const currentErrors = stepErrors[step] ?? {};
  const canNext = Object.keys(currentErrors).length === 0;
  /// The Authorization step's own result, for rendering. Read from `stepErrors` rather than recomputed,
  /// so the fields and the button can never disagree.
  // D-379 — the authorization now lives on Step 1, so its per-field errors come from the same result
  // that gates Continue there. Read from `authorizationErrors` directly rather than from a step slot
  // that no longer exists.
  const authErrors: Record<string, string> = authorizationErrors;

  /*
   * Why the button is disabled, in words.
   *
   * A disabled control with no explanation is the whole failure: somebody stares at a greyed-out
   * "Continue" with nothing on screen saying which choice is missing. Disabled controls are also
   * skipped by some screen-reader navigation entirely, so the reason has to live outside the button.
   *
   * Named per missing field rather than per step: "Make a choice to continue." on a step with four
   * inputs is a shrug, and the whole point of blocking earlier is to say what is wrong while the field
   * that is wrong is still on screen. Read from the same result the fields render — one rule, one
   * message, no second copy to drift.
   */
  const blockedReason = canNext
    ? null
    : (firstError(currentErrors, stepFieldOrder[step] ?? []) ?? "Make a choice to continue.") + ".";

  /*
   * §6 — the guard behind the disabled button.
   *
   * `disabled` is a rendering state, not an authorization: a programmatic click, a stale render or a
   * devtools edit all reach `onClick` regardless. Advancing re-reads the same per-step result the
   * button did, so an invalid step stays put and says why instead of moving on.
   */
  function goNext() {
    if (Object.keys(stepErrors[step] ?? {}).length > 0) {
      setError(blockedReason);
      return;
    }
    setError(null);
    setStep((s) => Math.min(s + 1, steps.length - 1));
  }

  /** The specific things still missing, so the last step never just refuses. */
  function missingForSubmit(): string[] {
    /*
     * The backstop, and now genuinely complete: EVERY step's rules, not the four the old list
     * remembered. It walked title/start/end, category, consent and authorization — so the six other
     * Details fields, the Online join link, both window orderings and the age range could all reach
     * the final POST with the button lit.
     */
    const missing: string[] = [];
    for (const [index, errors] of stepErrors.entries()) {
      if (index >= steps.length) break;   // the Authorization step is absent unless it is rendered
      for (const field of stepFieldOrder[index] ?? []) {
        if (errors[field]) missing.push(`${errors[field]} (${steps[index]} step).`);
      }
    }
    return missing;
  }

  const submitBlockedReason = missingForSubmit().length === 0 ? null : missingForSubmit().join(" ");
  const canSubmit = missingForSubmit().length === 0;

  function submit() {
    setError(null);
    // Through `toIsoUtc`, exactly like the six schedule windows below. These two were the only
    // datetime fields sent raw, so the wizard shifted the one time an event cannot get wrong by the
    // organiser's whole offset — 5h30m in India (D-289). Converted before the payload rather than
    // inline because the contract requires both: refusing here beats sending a time we know is
    // unparseable, which the server would store as something else entirely.
    /*
     * Layer 3 of four. Every step re-validated at the moment of submission rather than trusted from
     * when it was left, because up to eleven steps may have passed since — and "now" moves. A start
     * time that was twenty minutes out when it was typed can be in the past by the time the organiser
     * reaches the last step, and the step that checked it is long gone from the screen.
     */
    const stillMissing = missingForSubmit();
    if (stillMissing.length > 0) {
      setError(stillMissing[0]);
      return;
    }
    const startsAt = toIsoUtc(details.startsAt);
    const endsAt = toIsoUtc(details.endsAt);
    if (!startsAt || !endsAt) {
      setError("Enter a valid start and end time on the Details step.");
      return;
    }
    const values: CreateEventValues = {
      title: details.title.trim(),
      subtitle: details.subtitle || undefined,
      description: details.description || undefined,
      categoryId,
      typeId: typeId || undefined,
      visibility,
      startsAt,
      endsAt,
      venueName: details.venueName || undefined,
      venueAddress: details.venueAddress || undefined,
      city: details.city || undefined,
      capacity: details.capacity ? Number(details.capacity) : undefined,
      eventMode: place.eventMode,
      onlineUrl: place.onlineUrl || undefined,
      content: cleanGroup({
        tagline: content.tagline,
        shortDescription: content.shortDescription,
        rules: content.rules
      }),
      location: cleanGroup({
        building: place.building,
        floor: place.floor,
        room: place.room,
        googleMapsUrl: place.googleMapsUrl,
        meetingPlatform: place.meetingPlatform,
        meetingPassword: place.meetingPassword
      }),
      schedule: cleanGroup({
        registrationOpensAt: toIsoUtc(windows.registrationOpensAt),
        registrationClosesAt: toIsoUtc(windows.registrationClosesAt),
        checkinOpensAt: toIsoUtc(windows.checkinOpensAt),
        checkinClosesAt: toIsoUtc(windows.checkinClosesAt),
        resultDate: toIsoUtc(windows.resultDate),
        certificateReleaseAt: toIsoUtc(windows.certificateReleaseAt),
        autoClose: windows.autoClose || undefined
      }),
      eligibility: cleanGroup({
        minAge: eligibility.minAge ? Number(eligibility.minAge) : undefined,
        maxAge: eligibility.maxAge ? Number(eligibility.maxAge) : undefined,
        // "Any" is the server default; sending it would be noise.
        genderRestriction: eligibility.genderRestriction === "Any" ? undefined : eligibility.genderRestriction,
        maxTeams: eligibility.maxTeams ? Number(eligibility.maxTeams) : undefined
      }),
      legal: cleanGroup({
        termsUrl: legal.termsUrl,
        codeOfConduct: legal.codeOfConduct,
        refundPolicy: legal.refundPolicy,
        cancellationPolicy: legal.cancellationPolicy,
        requiresConsent: legal.requiresConsent || undefined,
        consentText: legal.consentText
      })
    };
    startTransition(async () => {
      const isTeam = ticket.participation === "team";
      const res = await createEventWizardAction(representingOrgId, values, {
        name: ticket.name,
        // Rupees in, paise on the wire (D-004). A free event is genuinely 0, not absent.
        // With bands the server derives the headline from the cheapest one, so this is only the
        // single-price case (D-366).
        pricePaise: pricing === "paid" && ticket.tiers.length === 0
          ? Math.round(Number(ticket.priceRupees) * 100)
          : 0,
        quantity: Number(ticket.quantity) || 100,
        /*
         * D-372 — the unit, no longer a literal.
         *
         * These two lines read `pricingUnit: "PerTicket", registrationMode: "Individual"` and made a
         * capable API uni-modal: `TicketType` has carried both fields since D-020 and the wizard could
         * only ever say one thing. `PerGroup` is what makes the backend charge once per team and take
         * one inventory unit for it.
         */
        pricingUnit: isTeam ? "PerGroup" : "PerTicket",
        registrationMode: isTeam ? "Group" : "Individual",
        groupMin: isTeam ? Number(ticket.teamMin) : undefined,
        groupMax: isTeam ? Number(ticket.teamMax) : undefined,
        // D-366 — undefined for an unbanded ticket, so the request is byte-identical to before.
        priceTiers: isTeam && pricing === "paid" ? tiersToPayload(ticket.tiers) : undefined
      });
      if ("id" in res) {
        /*
         * D-351 — file the institution's authorization now, in the same action, so the organiser never
         * leaves the wizard to satisfy a publish blocker they were already told about.
         *
         * It runs AFTER creation by necessity: both presign and submit are keyed on an eventId that
         * only this POST produces. That ordering means the event can exist while the authorization
         * fails, so the failure is carried to the workspace rather than swallowed — the event is real
         * and the organiser has to know its consent is still missing.
         */
        const createdId = res.id;
        let authError: string | null = null;
        if (needsAuthorization && createdId) {
          try {
            let letterheadDocumentKey: string | undefined;
            if (letterFile) {
              const presigned = await uploadAuthorizationDocumentAction(
                createdId, letterFile.type || "application/octet-stream", letterFile.size);
              if ("error" in presigned) throw new Error(presigned.error);
              const put = await fetch(presigned.url, {
                method: "PUT", body: letterFile, headers: presigned.headers
              });
              if (!put.ok) throw new Error("The authorization letter could not be uploaded.");
              letterheadDocumentKey = presigned.key;
            }
            const filed = await submitAuthorizationAction(createdId, {
              ...authorization,
              representativeRoleOther: authorization.representativeRoleOther || undefined,
              letterheadDocumentKey
            });
            if ("error" in filed) authError = filed.error ?? "The authorization could not be filed.";
          } catch (err) {
            authError = err instanceof Error ? err.message : "The authorization could not be filed.";
          }
        }
        // The event exists either way; a ticket failure is surfaced on the workspace it lands on
        // rather than swallowed, because "created but unbookable" is the state this change exists
        // to prevent going unnoticed.
        if (authError) {
          setError(`Event created, but its authorization was not filed: ${authError} `
            + "Open the event's Readiness tab to file it.");
          router.push(`/host/events/${res.id}/readiness`);
          return;
        }
        router.push(`/host/events/${res.id}${res.ticketError ? "/tickets" : ""}`);
      } else {
        setError(res.error);
      }
    });
  }

  return (
    <div className="space-y-6">
      {/*
        Was a bare `<ol>` of pills where the current step was marked by a border colour and a text
        colour — nothing programmatic, so somebody eleven steps into a wizard had no way to hear
        which one they were on. `FormSteps` is the primitive Phase 7 built for this and it was
        unused; it carries `aria-current="step"` and spells out "(completed)" / "(current step)".
      */}
      <FormSteps steps={STEPS} current={step} />

      {/*
        Moving between steps swapped the panel underneath and said nothing at all. The region is
        rendered unconditionally so its first message is observed — a live region inserted with its
        own content is not announced by most screen readers.
      */}
      <p role="status" className="sr-only">
        {`Step ${step + 1} of ${steps.length}: ${steps[step]}.`}
      </p>

      {/*
        Step 1 — Representing (D-353).

        A PUBLIC event must name a verified organization answerable for it; there is no self-hosting
        card, because a public event carries the platform's name into discovery whether or not money
        moves. A PRIVATE event reaches no discovery surface and can never sell, so it is hosted by the
        person and the organization question is not asked at all — and crucially it is NOT presented as
        an "organization" of any kind, because self-representation is not a concept in this model.
      */}
      {step === STEP.representing ? (
        <div className="space-y-3">
          {/* D-379 — the Private branch is gone. It rendered "Hosted by you … there's no organization to
              name and nothing to verify", which is the rule this decision retires: a private event
              represents somebody too, and visibility never decided who is answerable for an event. Every
              product now picks an organization from the same list. */}
          {selectableReps.length > 0 ? (
            <>
              <p className="text-sm text-muted">
                Select the organization you are authorized to represent for this event.
              </p>
              <SelectCardGroup
                legend="Who are you hosting this event on behalf of?"
                name="representing"
                className="grid gap-3 sm:grid-cols-2"
              >
                {selectableReps.map((r) => {
                  const paidCapable = r.can_back_paid_event ?? r.is_verified;
                  return (
                    <SelectCard
                      key={r.organization_id}
                      name="representing"
                      value={r.organization_id}
                      checked={representingOrgId === r.organization_id}
                      onSelect={() => setRepresentingOrgId(r.organization_id)}
                      icon={<ShieldCheck size={18} aria-hidden className="text-accent-text" />}
                      title={r.name}
                      // A pending organization is selectable and says so on its own card, rather than
                      // being listed separately as unusable — it CAN carry this draft, and the thing
                      // it cannot do is publish, which is what the description now states.
                      description={paidCapable
                        ? `Representing · your authority: ${r.authority}`
                        : "Awaiting admin verification · you can start the event now, but it can't publish until that's approved"}
                    />
                  );
                })}
              </SelectCardGroup>
            </>
          ) : (
            <div className="rounded-lg border border-dashed border-border bg-surface p-4">
              <p className="text-sm text-text">
                You don&apos;t represent an organization yet.
              </p>
              <p className="mt-1 text-xs text-muted">
                Every event is hosted on behalf of an organization. Add it below — you can carry on
                creating this event straight away; an admin verifies the organization before the event
                can be published.
              </p>
            </div>
          )}

          {/*
            The registration form, RENDERED HERE rather than linked to.
            It used to be a link out to the standalone request page: a full navigation away from a
            wizard holding ten steps of unsaved state, which is why someone with no representation could
            not finish an event without starting over. The staged organization is appended to the list
            and selected on the spot, so the step completes without the flow ever leaving.
          */}
          <details className="rounded-lg border border-border bg-surface p-4" open={selectableReps.length === 0}>
            <summary className="cursor-pointer text-sm font-medium text-text">
              {selectableReps.length === 0
                ? "Add your college or organization"
                : "Representing an organization you don't see here? Add it"}
            </summary>
            <p className="mt-2 text-xs text-muted">
              An admin verifies the institution itself before this event can publish — a one-time step
              per organization. This event&apos;s own authorization letter is asked for below and is
              needed for every event, however many you run under the same organization.
            </p>
            <div className="mt-4">
              <CreateOrgForm
                onRegistered={(rep) => {
                  setRepresentations((prev) =>
                    prev.some((r) => r.organization_id === rep.organization_id) ? prev : [...prev, rep]);
                  setRepresentingOrgId(rep.organization_id);
                }}
              />
            </div>
          </details>

          {representingIsPending ? (
            <p className="rounded-lg border border-border bg-surface px-4 py-3 text-xs text-muted">
              <span className="text-text">Awaiting verification.</span> You can finish creating this
              event and file its authorization now. It stays a draft until an admin verifies the
              organization{pricing === "paid" ? ", and a paid event needs that verification before you can continue" : ""}.
            </p>
          ) : null}
        </div>
      ) : null}

      {/* Step 2 — Visibility.
          visibilityFor, not VISIBILITY: a Private event can never be Listed (D-305), and the option
          has to be absent here rather than refused eleven steps later at publish. */}
      {step === STEP.visibility ? (
        <SelectCardGroup legend="Who can find this event?" name="visibility" className="grid gap-3 sm:grid-cols-3">
          {visibilityFor(product).map((v) => {
            const Icon = v.icon;
            return (
              <SelectCard
                key={v.value}
                name="visibility"
                value={v.value}
                checked={visibility === v.value}
                onSelect={() => setVisibility(v.value)}
                icon={<Icon size={18} aria-hidden className="text-accent-text" />}
                title={v.label}
                description={v.desc}
              />
            );
          })}
        </SelectCardGroup>
      ) : null}

      {/* Step 2 — Category.
          shownCategories, not categories: the product gate filters the catalogue (D-305) so a Private
          host is never offered a category with no Private type behind it. */}
      {step === STEP.category ? (
        shownCategories.length === 0 ? (
          <p className="text-sm text-muted">
            {categories.length === 0
              ? "No event categories exist yet. A platform admin must create at least one before events can be created."
              : `No ${product.toLowerCase()} event categories are available yet. Go back and choose the other kind, or ask a platform admin to classify a type.`}
          </p>
        ) : (
          <SelectCardGroup legend="What kind of event is this?" name="category" className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
            {shownCategories.map((c) => (
              <SelectCard
                key={c.id}
                name="category"
                value={c.id}
                checked={categoryId === c.id}
                onSelect={() => { setCategoryId(c.id); setTypeId(""); }}
                title={c.name}
              />
            ))}
          </SelectCardGroup>
        )
      ) : null}

      {/* Step 4 — Subcategory (Type) + metadata preview */}
      {step === STEP.type ? (
        <div className="space-y-4">
          {subs.length === 0 ? (
            <p className="text-sm text-muted">This category has no subcategories. Continue to details.</p>
          ) : (
            <SelectCardGroup legend="Which type?" name="type" className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
              {subs.map((sub) => (
                <SelectCard
                  key={sub.id}
                  name="type"
                  value={sub.id}
                  checked={typeId === sub.id}
                  onSelect={() => setTypeId(sub.id)}
                  title={sub.name}
                />
              ))}
            </SelectCardGroup>
          )}
          {typePresetFields.length > 0 ? (
            <div className="rounded-lg border border-border bg-surface p-4">
              <p className="text-sm font-medium">Registration form template</p>
              <p className="mt-1 text-xs text-muted">Choosing this pre-fills your attendee registration form with:</p>
              <div className="mt-2 flex flex-wrap gap-2">
                {typePresetFields.map((f) => (
                  <span key={f} className="rounded-full border border-border px-2 py-0.5 text-xs text-muted">{f}</span>
                ))}
              </div>
            </div>
          ) : null}
        </div>
      ) : null}

      {/*
        Step 6 — Registration (D-372).

        The step that gives the price a unit. It runs after Type because the Type derives the archetype
        and the archetype's `teams` capability is the only authority on whether team entry exists for
        this event — asked of the engine, never inferred from a type name.
      */}
      {step === STEP.registration ? (
        <div className="space-y-4">
          {/*
            States the pricing mode; does not ask it. It was decided at the gate, where the verification
            tier hangs off it (D-343), and this is the step where its consequence appears — a price field
            or the absence of one. Shown rather than silent, because an organiser who chose Paid a minute
            ago and is now looking at a form with no price needs to know which of the two they are in.
          */}
          <p className="text-caption text-muted">
            {pricing === "paid"
              ? "Paid event — chosen during setup. Set what people pay below."
              : "Free event — chosen during setup. No one will be charged to register."}
          </p>
          <Field label="What people are booking" required error={ticketErrors.name}>
            <Input id="ticketName" value={ticket.name} maxLength={80}
              onChange={(e) => setTicket({ ...ticket, name: e.target.value })} />
          </Field>

          {/*
            Individual vs team. Shown as a choice only where the archetype allows it: a conference or a
            wedding marks `teams` Unsupported, so offering it would let the organiser configure something
            the event can never run.
          */}
          {teamsSupported ? (
            <SelectCardGroup legend="How do people take part?" name="participation" className="grid gap-3 sm:grid-cols-2">
              <SelectCard
                name="participation" value="individual"
                checked={ticket.participation === "individual"}
                onSelect={() => setTicket({ ...ticket, participation: "individual" })}
                icon={<Ticket size={18} aria-hidden className="text-accent-text" />}
                title="Individually"
                description="Each person registers for themselves."
              />
              <SelectCard
                name="participation" value="team"
                checked={ticket.participation === "team"}
                onSelect={() => setTicket({ ...ticket, participation: "team" })}
                icon={<ShieldCheck size={18} aria-hidden className="text-accent-text" />}
                title="As a team"
                description="One person registers the team and the rest join it."
              />
            </SelectCardGroup>
          ) : (
            <p className="flex items-center gap-2 text-xs text-muted">
              <ShieldCheck size={14} aria-hidden />
              {selectedArchetype
                ? "This kind of event doesn't support team entry, so people register individually."
                : "People register individually."}
            </p>
          )}

          {ticket.participation === "team" ? (
            <div className="grid gap-4 sm:grid-cols-2">
              {/* GroupMin/GroupMax — enforced at purchase as `invalid_group_size`, and the ceiling any
                  later TeamPolicy may narrow within but never exceed (D-373). */}
              <Field label="Smallest team" required error={ticketErrors.teamMin}>
                <Input id="teamMin" type="number" min={1} step={1} value={ticket.teamMin}
                  onChange={(e) => setTicket({ ...ticket, teamMin: e.target.value })} />
              </Field>
              <Field label="Largest team" required error={ticketErrors.teamMax}>
                <Input id="teamMax" type="number" min={1} step={1} value={ticket.teamMax}
                  onChange={(e) => setTicket({ ...ticket, teamMax: e.target.value })} />
              </Field>
            </div>
          ) : null}

          {/*
            D-366 — price by team size.

            Offered only for a PAID TEAM ticket, because that is the only shape where the question has
            an answer: an individual price already scales with the roster, and a free event has no
            prices to band. Empty means one price for every size, which is D-372 unchanged and stays the
            default — an organiser who does not need bands never sees a table.
          */}
          {pricing === "paid" && ticket.participation === "team" ? (
            <div className="space-y-2 rounded-lg border border-border bg-surface p-4">
              <div className="flex items-center justify-between gap-2">
                <p className="text-sm font-medium text-text">Price by team size</p>
                {ticket.tiers.length === 0 ? (
                  <Button type="button" variant="secondary"
                    onClick={() => setTicket({
                      ...ticket,
                      // Seeded across the whole allowed range so the first thing shown is already a
                      // valid set: an editor that opens in an invalid state teaches people to ignore it.
                      tiers: [{ minSize: ticket.teamMin || "2", maxSize: ticket.teamMax || "5", priceRupees: ticket.priceRupees || "" }]
                    })}>
                    Set different prices per size
                  </Button>
                ) : (
                  <Button type="button" variant="ghost"
                    onClick={() => setTicket({ ...ticket, tiers: [] })}>
                    Use one price for all sizes
                  </Button>
                )}
              </div>

              {ticket.tiers.length === 0 ? (
                <p className="text-caption text-muted">
                  Every team pays the same, whatever its size. Add rules to charge a team of 2 differently
                  from a team of 5.
                </p>
              ) : (
                <>
                  {ticket.tiers.map((tier, i) => (
                    <div key={i} className="grid items-end gap-2 sm:grid-cols-[1fr_1fr_1fr_auto]">
                      <Field label="From (members)">
                        <Input id={`tierMin${i}`} type="number" min={1} step={1} value={tier.minSize}
                          onChange={(e) => setTicket({
                            ...ticket,
                            tiers: ticket.tiers.map((t, j) => j === i ? { ...t, minSize: e.target.value } : t)
                          })} />
                      </Field>
                      <Field label="To (members)">
                        <Input id={`tierMax${i}`} type="number" min={1} step={1} value={tier.maxSize}
                          onChange={(e) => setTicket({
                            ...ticket,
                            tiers: ticket.tiers.map((t, j) => j === i ? { ...t, maxSize: e.target.value } : t)
                          })} />
                      </Field>
                      <Field label="Price per team (₹)">
                        <Input id={`tierPrice${i}`} type="number" min={1} value={tier.priceRupees}
                          onChange={(e) => setTicket({
                            ...ticket,
                            tiers: ticket.tiers.map((t, j) => j === i ? { ...t, priceRupees: e.target.value } : t)
                          })} />
                      </Field>
                      <Button type="button" variant="ghost"
                        onClick={() => setTicket({ ...ticket, tiers: ticket.tiers.filter((_, j) => j !== i) })}>
                        Remove
                      </Button>
                    </div>
                  ))}
                  <Button type="button" variant="secondary"
                    onClick={() => {
                      // The next rule starts where the last one ended, because a set built by hand is
                      // where gaps come from and the common case is contiguous bands.
                      const last = ticket.tiers[ticket.tiers.length - 1];
                      const next = String(Number(last?.maxSize || ticket.teamMin || 2) + 1);
                      setTicket({ ...ticket, tiers: [...ticket.tiers, { minSize: next, maxSize: next, priceRupees: "" }] });
                    }}>
                    + Add price rule
                  </Button>
                  {ticketErrors.tiers ? (
                    <p role="alert" className="text-caption text-danger">{ticketErrors.tiers}</p>
                  ) : (
                    <p className="text-caption text-muted">
                      Each rule is the price for the WHOLE team, not per member. Every size from{" "}
                      {ticket.teamMin || "—"} to {ticket.teamMax || "—"} needs exactly one rule.
                    </p>
                  )}
                </>
              )}
            </div>
          ) : null}

          <div className="grid gap-4 sm:grid-cols-2">
            {pricing === "paid" && ticket.tiers.length === 0 ? (
              // The unit is stated on the control, not left to the organiser to infer. ₹2,000 alone is
              // the ambiguity this whole change exists to remove. Hidden once bands exist: the bands ARE
              // the price then, and two price inputs is the duplicate-question mistake again (D-365).
              <Field label={ticket.participation === "team" ? "Price per team (₹)" : "Price per participant (₹)"}
                required error={ticketErrors.priceRupees}
                helper={ticketErrors.priceRupees ? undefined
                  : ticket.participation === "team"
                    ? "Charged once for the whole team, whatever its size."
                    : "Charged once per person."}>
                <Input id="ticketPrice" type="number" min={1} value={ticket.priceRupees}
                  onChange={(e) => setTicket({ ...ticket, priceRupees: e.target.value })} />
              </Field>
            ) : null}
            {/* Under PerGroup one team takes exactly one unit, so this really is a count of teams. */}
            <Field label={ticket.participation === "team" ? "How many teams" : "How many places"}
              required error={ticketErrors.quantity}
              helper={ticketErrors.quantity ? undefined
                : ticket.participation === "team"
                  ? "Team slots available. A team takes one, however many people are on it."
                  : "Total places available."}>
              <Input id="ticketQuantity" type="number" min={1} step={1} value={ticket.quantity}
                onChange={(e) => setTicket({ ...ticket, quantity: e.target.value })} />
            </Field>
          </div>

          {/* The summary reads back what was configured, in the unit it is charged in — a price with no
              unit is the ambiguity this step exists to remove. With bands it names the range rather than
              one number, because "₹250" on a ticket that also charges ₹400 would be the same lie. */}
          <p className="text-caption text-muted">
            {pricing === "paid"
              ? ticket.participation === "team"
                ? ticket.tiers.length > 0
                  ? `₹${Math.min(...ticket.tiers.map((t) => Number(t.priceRupees) || 0))}–₹${Math.max(...ticket.tiers.map((t) => Number(t.priceRupees) || 0))} per team by size · teams of ${ticket.teamMin || "—"}–${ticket.teamMax || "—"} · ${ticket.quantity || "—"} team slots`
                  : `₹${ticket.priceRupees || "—"} per team · teams of ${ticket.teamMin || "—"}–${ticket.teamMax || "—"} · ${ticket.quantity || "—"} team slots`
                : `₹${ticket.priceRupees || "—"} per participant · ${ticket.quantity || "—"} places`
              : ticket.participation === "team"
                ? `Free · teams of ${ticket.teamMin || "—"}–${ticket.teamMax || "—"} · ${ticket.quantity || "—"} team slots`
                : `Free · ${ticket.quantity || "—"} places`}
          </p>
        </div>
      ) : null}

      {/*
        Step 7 — Details.

        `Field`/`Input`/`Textarea`/`DateTimeField` rather than the hand-rolled `<label>` + `controlClass`
        the other steps still use. That is not a restyle: `Field` is the ONE place a control gets
        `aria-invalid`, `aria-describedby` and a `role="alert"` error node (audit S1-1), and this step is
        the only one in the wizard that now has per-field errors to announce. Every message below comes
        from `validateDetails` — there is no second validation system here, only its rendering.
      */}
      {step === STEP.details ? (
        <div className="space-y-4">
          <Field label="Title" required error={detailsErrors.title}>
            <Input id="title" value={details.title} minLength={2} maxLength={200}
              onChange={(e) => setDetails({ ...details, title: e.target.value })} />
          </Field>
          <Field label="Subtitle" required error={detailsErrors.subtitle}>
            <Input id="subtitle" value={details.subtitle} maxLength={200}
              onChange={(e) => setDetails({ ...details, subtitle: e.target.value })} />
          </Field>
          <Field label="Description" required error={detailsErrors.description}>
            <Textarea id="description" rows={4} value={details.description}
              onChange={(e) => setDetails({ ...details, description: e.target.value })} />
          </Field>
          <div className="grid gap-4 sm:grid-cols-2">
            {/* `min` is the picker's floor (§3/§4): today is reachable, earlier today is not, and the
                past is not offered at all. Recomputed every render, never hard-coded. */}
            <DateTimeField id="startsAt" label="Starts at" required value={details.startsAt}
              min={nowLocal} error={detailsErrors.startsAt}
              helper={detailsErrors.startsAt ? undefined : "Cannot be in the past."}
              onChange={(e) => setDetails({ ...details, startsAt: e.target.value })} />
            {/* The End floor follows Start, so moving Start forward narrows End with it (§5). */}
            <DateTimeField id="endsAt" label="Ends at" required value={details.endsAt}
              min={endsAtMin} error={detailsErrors.endsAt}
              helper={detailsErrors.endsAt ? undefined : "Must be after the start."}
              onChange={(e) => setDetails({ ...details, endsAt: e.target.value })} />
          </div>
          <div className="grid gap-4 sm:grid-cols-2">
            <Field label="Venue name" required error={detailsErrors.venueName}>
              <Input id="venueName" value={details.venueName}
                onChange={(e) => setDetails({ ...details, venueName: e.target.value })} />
            </Field>
            <Field label="City" required error={detailsErrors.city}>
              <Input id="city" value={details.city}
                onChange={(e) => setDetails({ ...details, city: e.target.value })} />
            </Field>
          </div>
          <Field label="Venue address" required error={detailsErrors.venueAddress}>
            <Input id="venueAddress" value={details.venueAddress}
              onChange={(e) => setDetails({ ...details, venueAddress: e.target.value })} />
          </Field>
          <Field label="Capacity" required error={detailsErrors.capacity}>
            <Input id="capacity" type="number" min={1} step={1} value={details.capacity}
              onChange={(e) => setDetails({ ...details, capacity: e.target.value })} />
          </Field>
          {pricing === "paid" ? (
            <p className="text-xs text-muted">You&apos;ll add ticket types and prices in the event&apos;s Tickets tab after it&apos;s created.</p>
          ) : null}
        </div>
      ) : null}

      {/* Step 6 — Content */}
      {step === STEP.content ? (
        <div className="space-y-4">
          {/* D-378 — these three ARE the listing. A card and a share preview built from an empty
              tagline and no summary is a listing nobody can act on, so the step that asks for them
              now waits for them. `Field` carries the required mark, the error and the counter
              through one `aria-describedby`, exactly as the Details step does. */}
          <p className="text-sm text-muted">These are what a listing card and a share preview show.</p>
          <Field label="Tagline" required error={contentErrors.tagline}
            helper={`${content.tagline.length}/160 characters`}>
            <Input id="tagline" maxLength={160} value={content.tagline}
              onChange={(e) => setContent({ ...content, tagline: e.target.value })} />
          </Field>
          <Field label="Short description" required error={contentErrors.shortDescription}
            helper={`${content.shortDescription.length}/300 characters`}>
            <Textarea id="shortDescription" rows={2} maxLength={300} value={content.shortDescription}
              onChange={(e) => setContent({ ...content, shortDescription: e.target.value })} />
          </Field>
          <Field label="Rules" required error={contentErrors.rules}>
            <Textarea id="rules" rows={4} value={content.rules}
              onChange={(e) => setContent({ ...content, rules: e.target.value })} />
          </Field>
        </div>
      ) : null}

      {/* Step 7 — Location */}
      {step === STEP.location ? (
        <div className="space-y-4">
          <div>
            <label className="text-sm font-medium" htmlFor="eventMode">Mode</label>
            <select id="eventMode" className={`mt-1 ${inputClass}`} value={place.eventMode}
              onChange={(e) => setPlace({ ...place, eventMode: e.target.value })}>
              <option value="Offline">In person</option>
              <option value="Online">Online</option>
              <option value="Hybrid">Hybrid</option>
            </select>
          </div>

          {place.eventMode !== "Online" ? (
            <div className="grid gap-4 sm:grid-cols-3">
              <Field label="Building" required error={placeErrors.building}>
                <Input id="building" value={place.building}
                  onChange={(e) => setPlace({ ...place, building: e.target.value })} />
              </Field>
              <Field label="Floor" required error={placeErrors.floor}>
                <Input id="floor" value={place.floor}
                  onChange={(e) => setPlace({ ...place, floor: e.target.value })} />
              </Field>
              <Field label="Room" required error={placeErrors.room}>
                <Input id="room" value={place.room}
                  onChange={(e) => setPlace({ ...place, room: e.target.value })} />
              </Field>
            </div>
          ) : null}

          {place.eventMode !== "Online" ? (
            <Field label="Google Maps link" required error={placeErrors.googleMapsUrl}>
              <Input id="googleMapsUrl" type="url" value={place.googleMapsUrl}
                onChange={(e) => setPlace({ ...place, googleMapsUrl: e.target.value })} />
            </Field>
          ) : null}

          {place.eventMode !== "Offline" ? (
            <>
              {/* Conditionally REQUIRED, and the one field on this step that is: `ValidateMode`
                  refuses an Online/Hybrid event with no link (`online_url_required`). Switching Mode
                  back to In person makes it optional again on the same render. */}
              <Field label="Join link" required error={placeErrors.onlineUrl}>
                <Input id="onlineUrl" type="url" value={place.onlineUrl}
                  onChange={(e) => setPlace({ ...place, onlineUrl: e.target.value })} />
              </Field>
              <div className="grid gap-4 sm:grid-cols-2">
                <Field label="Platform" required error={placeErrors.meetingPlatform}>
                  <Input id="meetingPlatform" placeholder="Zoom, Meet, Teams…"
                    value={place.meetingPlatform}
                    onChange={(e) => setPlace({ ...place, meetingPlatform: e.target.value })} />
                </Field>
                <Field label="Meeting password" required error={placeErrors.meetingPassword}
                  helper={placeErrors.meetingPassword ? undefined : "Only shown to confirmed registrants."}>
                  <Input id="meetingPassword" value={place.meetingPassword}
                    onChange={(e) => setPlace({ ...place, meetingPassword: e.target.value })} />
                </Field>
              </div>
            </>
          ) : null}
        </div>
      ) : null}

      {/* Step 8 — Windows */}
      {step === STEP.windows ? (
        <div className="space-y-4">
          <p className="text-sm text-muted">
            Registration times bound every ticket type; a ticket&apos;s own sale window can narrow that
            further, never widen it.
          </p>
          <div className="grid gap-4 sm:grid-cols-2">
            <DateTimeField id="regOpens" label="Registration opens" required
              value={windows.registrationOpensAt} error={windowErrors.registrationOpensAt}
              onChange={(e) => setWindows({ ...windows, registrationOpensAt: e.target.value })} />
            {/* `min` follows the opening time, so the picker cannot offer a close before an open —
                the same rule `invalid_registration_window` refuses. */}
            <DateTimeField id="regCloses" label="Registration closes" required
              value={windows.registrationClosesAt} min={windows.registrationOpensAt || undefined}
              error={windowErrors.registrationClosesAt}
              onChange={(e) => setWindows({ ...windows, registrationClosesAt: e.target.value })} />
            <DateTimeField id="checkinOpens" label="Check-in opens" required
              value={windows.checkinOpensAt} error={windowErrors.checkinOpensAt}
              onChange={(e) => setWindows({ ...windows, checkinOpensAt: e.target.value })} />
            <DateTimeField id="checkinCloses" label="Check-in closes" required
              value={windows.checkinClosesAt} min={windows.checkinOpensAt || undefined}
              error={windowErrors.checkinClosesAt}
              onChange={(e) => setWindows({ ...windows, checkinClosesAt: e.target.value })} />
            {/* `scoring` and `certificates` are Unsupported for the private-gathering archetype, so a
                wedding was being asked when its results are announced. Keyed on `product` rather than
                on a fetched capability set for two reasons: the wizard already holds the product, and
                Private ⇔ private-gathering is a one-to-one the D12 matrix asserts
                (`ArchetypeFoundationTests.Seeds_fourteen_archetypes_and_rerun_adds_none` pins exactly
                one Private archetype). If a second Private archetype is ever added, this proxy stops
                being exact and the capability set has to be fetched — that test is what will say so.
                Hiding, never disabling: the engine DESCRIBES what an event supports and never gates
                (D-266 M2), and an unclassified Type still resolves Public, so nothing is hidden by an
                unknown archetype. */}
            {isPrivate ? null : (
              <>
                <DateTimeField id="resultDate" label="Results announced" required
                  value={windows.resultDate} error={windowErrors.resultDate}
                  onChange={(e) => setWindows({ ...windows, resultDate: e.target.value })} />
                <DateTimeField id="certRelease" label="Certificates released" required
                  value={windows.certificateReleaseAt} error={windowErrors.certificateReleaseAt}
                  onChange={(e) => setWindows({ ...windows, certificateReleaseAt: e.target.value })} />
              </>
            )}
          </div>
          <label className="flex items-center gap-2 text-sm text-text">
            <input type="checkbox" checked={windows.autoClose}
              onChange={(e) => setWindows({ ...windows, autoClose: e.target.checked })} />
            Close registration automatically when capacity is reached
          </label>
        </div>
      ) : null}

      {/* Step 9 — Eligibility */}
      {step === STEP.eligibility ? (
        <div className="space-y-4">
          <p className="text-sm text-muted">
            Optional. Anyone turned away is told which rule stopped them, so only set a restriction the
            event genuinely has.
          </p>
          {/* Age bounds are an admission rule for an open door: they exist to turn away a stranger who
              registered. A private event has no open door — attendance is the invitation list — so the
              rule has nothing to act on and its refusal copy ("Anyone turned away is told which rule
              stopped them") describes an interaction that cannot happen. */}
          {isPrivate ? null : (
            <div className="grid gap-4 sm:grid-cols-2">
              {/* Optional — an event with no age bound is the normal case. What is checked is the
                  RANGE once both are given, which `ApplyFieldGroups` refuses as `invalid_age_range`. */}
              <Field label="Minimum age" required error={eligibilityErrors.minAge}>
                <Input id="minAge" type="number" min={0} max={120} step={1} value={eligibility.minAge}
                  onChange={(e) => setEligibility({ ...eligibility, minAge: e.target.value })} />
              </Field>
              <Field label="Maximum age" required error={eligibilityErrors.maxAge}>
                <Input id="maxAge" type="number" min={0} max={120} step={1} value={eligibility.maxAge}
                  onChange={(e) => setEligibility({ ...eligibility, maxAge: e.target.value })} />
              </Field>
            </div>
          )}
          <div>
            <label className="text-sm font-medium" htmlFor="gender">Gender</label>
            <select id="gender" className={`mt-1 ${inputClass}`} value={eligibility.genderRestriction}
              onChange={(e) => setEligibility({ ...eligibility, genderRestriction: e.target.value })}>
              {GENDERS.map((g) => (
                <option key={g} value={g}>{g === "Any" ? "Open to everyone" : g}</option>
              ))}
            </select>
          </div>
          {/* `teams` is Unsupported for private-gathering — a wedding has no team cap. Same rule and
              same caveat as the two date fields on the Windows step. */}
          {isPrivate ? null : (
            // Optional, but a 0 is not "no cap" — `ApplyFieldGroups` silently DISCARDS `MaxTeams <= 0`,
            // so an organiser capping teams at 0 got no cap and no warning. Refused here instead.
            <Field label="Maximum teams" required error={eligibilityErrors.maxTeams}
              helper={eligibilityErrors.maxTeams ? undefined : "Total teams for the event, not teams per person."}>
              <Input id="maxTeams" type="number" min={1} step={1} value={eligibility.maxTeams}
                onChange={(e) => setEligibility({ ...eligibility, maxTeams: e.target.value })} />
            </Field>
          )}
        </div>
      ) : null}

      {/* Step 10 — Legal */}
      {step === STEP.legal ? (
        <div className="space-y-4">
          <p className="text-sm text-muted">
            Kurx&apos;s own terms always apply. These are your additional terms for this event.
          </p>
          <Field label="Terms link" required error={legalErrors.termsUrl}>
            <Input id="termsUrl" type="url" value={legal.termsUrl}
              onChange={(e) => setLegal({ ...legal, termsUrl: e.target.value })} />
          </Field>
          <Field label="Code of conduct" required error={legalErrors.codeOfConduct}>
            <Textarea id="codeOfConduct" rows={3} value={legal.codeOfConduct}
              onChange={(e) => setLegal({ ...legal, codeOfConduct: e.target.value })} />
          </Field>
          <div className="grid gap-4 sm:grid-cols-2">
            <Field label="Refund policy" required error={legalErrors.refundPolicy}>
              <Textarea id="refundPolicy" rows={3} value={legal.refundPolicy}
                onChange={(e) => setLegal({ ...legal, refundPolicy: e.target.value })} />
            </Field>
            <Field label="Cancellation policy" required error={legalErrors.cancellationPolicy}>
              <Textarea id="cancellationPolicy" rows={3} value={legal.cancellationPolicy}
                onChange={(e) => setLegal({ ...legal, cancellationPolicy: e.target.value })} />
            </Field>
          </div>
          <label className="flex items-center gap-2 text-sm text-text">
            <input type="checkbox" checked={legal.requiresConsent}
              onChange={(e) => setLegal({ ...legal, requiresConsent: e.target.checked })} />
            Require registrants to accept a statement
          </label>
          {/* The wizard's other conditional requirement: optional until the switch above is on, then
              required on the same render (`consent_text_required`). */}
          {legal.requiresConsent ? (
            <Field label="What they must accept" required error={legalErrors.consentText}
              helper={legalErrors.consentText ? undefined : "Acceptance is recorded against this exact wording."}>
              <Textarea id="consentText" rows={3} value={legal.consentText}
                onChange={(e) => setLegal({ ...legal, consentText: e.target.value })} />
            </Field>
          ) : null}
        </div>
      ) : null}

      {/* Nav */}
      {/*
        D-351/D-379 — the organization's written consent for THIS event, asked on Step 1 beside the
        organization it authorises.

        Rendered for every event now. It used to appear only for a public institutional one, appended
        after Legal, so an organiser learned on step twelve that step one was incomplete. The letter is
        held in memory here and uploaded the moment the event exists — chosen at Step 1, filed at create.
      */}
      {step === STEP.representing && representingValid ? (
        <div className="space-y-4">
          <div>
            <p className="text-sm text-muted">
              {representations.find((r) => r.organization_id === representingOrgId)?.name ?? "This organization"}
              {" "}has to confirm it authorises this event. Filed once, with the event — a reviewer reads
              it as part of the event&apos;s review, and the event can&apos;t publish until it&apos;s approved.
            </p>
          </div>

          <div className="grid gap-4 sm:grid-cols-2">
            <Field label="Signatory's name" required error={authErrors.headName}>
              <Input id="auth-head-name" value={authorization.headName} maxLength={160}
                onChange={(e) => setAuthorization({ ...authorization, headName: e.target.value })} />
            </Field>
            <Field label="Their designation" required error={authErrors.headDesignation}>
              <Input id="auth-head-designation" value={authorization.headDesignation} maxLength={160}
                onChange={(e) => setAuthorization({ ...authorization, headDesignation: e.target.value })} />
            </Field>
            <Field label="Official email" required error={authErrors.officialEmail}>
              <Input id="auth-email" type="email" value={authorization.officialEmail}
                onChange={(e) => setAuthorization({ ...authorization, officialEmail: e.target.value })} />
            </Field>
            {/* The same E.164 shape `EventAuthorizationBodyValidator` matches — checked here so the
                refusal names the field rather than arriving as a 400 after the event exists. */}
            <Field label="Official phone" required error={authErrors.officialPhone}
              helper={authErrors.officialPhone ? undefined : "International format, e.g. +919876543210."}>
              <Input id="auth-phone" value={authorization.officialPhone} placeholder="+919876543210"
                onChange={(e) => setAuthorization({ ...authorization, officialPhone: e.target.value })} />
            </Field>
          </div>

          {/* The server's vocabulary, never a copy — a list that drifts offers a role the API refuses. */}
          <Field label="Your role in this organization" required error={authErrors.representativeRole}>
            <Select id="auth-role" value={authorization.representativeRole}
              onChange={(e) => setAuthorization({ ...authorization, representativeRole: e.target.value })}>
              <option value="">Select a role…</option>
              {representativeRoles.map((r) => <option key={r} value={r}>{r}</option>)}
            </Select>
          </Field>

          {/* Conditionally required: choosing "Other" is what makes the free-text field mandatory
              (`representative_role_other_required`). */}
          {authorization.representativeRole === "Other" ? (
            <Field label="Describe your role" required error={authErrors.representativeRoleOther}>
              <Input id="auth-role-other" value={authorization.representativeRoleOther} maxLength={80}
                onChange={(e) => setAuthorization({ ...authorization, representativeRoleOther: e.target.value })} />
            </Field>
          ) : null}

          <div>
            <label className="text-sm font-medium text-text" htmlFor="auth-letter">Authorization letter</label>
            {/* Held in the browser until the event exists — presign is keyed on an eventId this wizard
                does not have yet. Nothing is uploaded if the wizard is abandoned. */}
            <input id="auth-letter" type="file" className={inputClass}
              accept="application/pdf,image/png,image/jpeg"
              onChange={(e) => setLetterFile(e.target.files?.[0] ?? null)} />
            {/*
              The letter is per-EVENT, not per-organization: `event_authorizations` is UNIQUE on EventId,
              so representing the same organization again next month needs a new letter naming that event.
              Spelling out what it must contain — and echoing the title and dates just typed — is what
              stops a reviewer rejecting a generic "X may run events for us" letter days later.
            */}
            <div className="mt-2 rounded-lg border border-border bg-surface p-3">
              <p className="text-xs text-text">The letter must be specific to this event and state:</p>
              <ul className="mt-1 list-inside list-disc text-xs text-muted">
                <li>the event by name — <span className="text-text">{details.title || "your event title"}</span></li>
                <li>
                  its dates —{" "}
                  <span className="text-text">
                    {details.startsAt
                      ? `${details.startsAt.replace("T", " ")}${details.endsAt ? ` to ${details.endsAt.replace("T", " ")}` : ""}`
                      : "set on the Details step"}
                  </span>
                </li>
                <li>that you are authorized to organize it on the organization&apos;s behalf</li>
                <li>the signatory&apos;s name, designation and signature</li>
              </ul>
              <p className="mt-2 text-xs text-muted">
                On the organization&apos;s official letterhead. PDF or image, up to 10 MB. A reviewer reads
                it as part of this event&apos;s review — a generic authorization letter that doesn&apos;t
                name the event is usually rejected.
              </p>
            </div>
          </div>
        </div>
      ) : null}

      <div className="border-t border-border pt-4">
        {/* The reason sits beside the control, not inside it — a disabled button cannot carry its
            own explanation, and some screen-reader navigation skips disabled controls outright. */}
        {step < steps.length - 1 && blockedReason ? (
          <p role="status" className="mb-3 text-sm text-muted">{blockedReason}</p>
        ) : null}
        {step === steps.length - 1 && submitBlockedReason ? (
          <div role="status" className="mb-3 rounded-md border border-border bg-surface p-3 text-sm text-muted">
            <p className="font-medium text-text">Still needed before this can be created:</p>
            <ul className="mt-1 list-inside list-disc">
              {missingForSubmit().map((m) => <li key={m}>{m}</li>)}
            </ul>
          </div>
        ) : null}
        {error ? <p role="alert" className="mb-3 text-sm text-danger">{error}</p> : null}
        <div className="flex items-center justify-between">
          {/* Back never validates — leaving a half-filled step to go correct an earlier one is the
              whole point of a wizard, and the error is cleared so a stale refusal does not follow. */}
          <Button variant="ghost" disabled={step === 0 || isPending}
            onClick={() => { setError(null); setStep((s) => Math.max(s - 1, 0)); }}>Back</Button>
          {step < steps.length - 1 ? (
            // Disabled AND guarded: `disabled` is presentation, `goNext` is the rule (§6).
            <Button disabled={!canNext} onClick={goNext}>Continue</Button>
          ) : (
            <Button disabled={!canSubmit || isPending} onClick={submit}>
              {isPending ? <Spinner size={16} decorative /> : null}
              {isPending ? "Creating…" : "Create draft event"}
            </Button>
          )}
        </div>
      </div>
    </div>
  );
}
