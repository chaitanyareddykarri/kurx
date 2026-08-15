"use client";

import { useMemo, useState, useTransition } from "react";
import { useRouter } from "next/navigation";
import { Globe, EyeOff, Lock, Gift, Ticket, ShieldCheck, Check } from "lucide-react";
import { Button, FormSteps, Spinner, controlClass } from "@kurx/ui";
import { SelectCard, SelectCardGroup } from "@/components/host/select-card-group";
import { createEventWizardAction, CreateEventValues,
  submitAuthorizationAction, uploadAuthorizationDocumentAction } from "@/lib/event-actions";
import type { Category, FieldPreset, Representation } from "@/lib/api";
import { categoriesFor, cleanGroup, toIsoUtc, typesFor } from "@/lib/event-wizard";

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
const STEPS = [
  "Representing", "Visibility", "Pricing", "Category", "Type", "Details",
  "Content", "Location", "Windows", "Eligibility", "Legal"
];

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
  representations,
  canHostPaid,
  categories,
  subcategories,
  presets,
  product,
  initialPricing = "free",
  requiresRepresentation,
  representativeRoles
}: {
  /// Institutions the caller may represent. Empty is normal and fully functional — Personal is always
  /// available, which is what makes Create Event reachable without registering anything (D-267).
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
}) {
  const router = useRouter();
  const [isPending, startTransition] = useTransition();
  const [step, setStep] = useState(0);
  /// null = Personal — the user represents themselves. Not "an organization that is personal": there is
  /// no organization in that branch at all, and the client neither names nor creates one (D-268).
  /// Null = Personal, and the right default for a free event. Entering as Paid it is not a legal answer
  /// at all (D-350), so the first verified organization is preselected rather than opening on a choice
  /// the Continue button would immediately refuse.
  /// D-353 — null means "hosted by the person", which is legal ONLY for a Private event. A Public
  /// event opens on its first selectable representation rather than on an answer Continue would refuse.
  const [representingOrgId, setRepresentingOrgId] = useState<string | null>(
    product === "Private"
      ? null
      : representations.find((r) => r.can_back_paid_event ?? r.is_verified)?.organization_id ?? null);
  const [error, setError] = useState<string | null>(null);

  // Unlisted is the only sane default for Private — Listed is forbidden and InviteOnly is a stronger
  // claim than the organiser has made yet.
  const [visibility, setVisibility] = useState<string>(product === "Private" ? "Unlisted" : "Listed");
  const [pricing, setPricing] = useState<"free" | "paid">(initialPricing);
  /// A Private product cannot take payment (`private_product_cannot_take_payment`), so Paid is not
  /// offered at all — and the price field with it.
  const canChoosePaid = product === "Public" && canHostPaid;
  /// The one ticket created with the event. Without it the event is unbookable — see the Pricing step.
  /// Rupees on the way in, paise on the wire (D-004).
  const [ticket, setTicket] = useState({ name: "General Admission", priceRupees: "", quantity: "100" });
  const [categoryId, setCategoryId] = useState<string>("");
  const [typeId, setTypeId] = useState<string>("");
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

  // Mirrors the server's `consent_text_required`: asking people to accept an empty string would
  // record a consent that evidences nothing.
  const consentOk = !legal.requiresConsent || legal.consentText.trim().length > 0;
  /// A paid event must carry a real price: creating one at zero would silently publish a paid event
  /// that charges nothing, which is worse than refusing to submit.
  const ticketOk =
    ticket.name.trim().length > 0 &&
    Number(ticket.quantity) > 0 &&
    (pricing === "free" || Number(ticket.priceRupees) > 0);

  /*
   * The three fields the server actually requires, checked on the step that ASKS for them.
   *
   * `endsAt > startsAt` is the server's own rule (`CreateEventBodyValidator`), and Flutter's
   * `_basicsValid` already carried it — web checked only that both were present, so the one ordering
   * an event cannot get wrong was left to a 400 at the very end.
   */
  const datesOrdered =
    !!details.startsAt && !!details.endsAt && new Date(details.endsAt) > new Date(details.startsAt);
  const detailsValid = details.title.trim().length >= 2 && datesOrdered;

  /// Both are refused by `EventService` (`invalid_registration_window` / `invalid_checkin_window`), so
  /// this surfaces the existing server rule on the step that sets it instead of at submit. Each pair is
  /// only checked when BOTH ends are given — one end alone is a legitimate open-ended window.
  const windowPairsOrdered =
    (!windows.registrationOpensAt || !windows.registrationClosesAt
      || new Date(windows.registrationClosesAt) > new Date(windows.registrationOpensAt)) &&
    (!windows.checkinOpensAt || !windows.checkinClosesAt
      || new Date(windows.checkinClosesAt) > new Date(windows.checkinOpensAt));

  /*
   * Each step gates its OWN fields.
   *
   * This was `step >= 4`, an unconditional pass for Type, Details, Content, Location, Windows,
   * Eligibility and Legal — so the three required fields (title, start, end) all sat behind a step
   * that could not refuse, and the wizard only objected on step 11 of 11. That is the exact failure
   * D-305 was written to end ("asked every eligibility question at publish, after eleven steps of
   * work"), reproduced inside the form D-305 created. Steps 6, 7 and 9 are genuinely all-optional and
   * say so by returning true; that is a statement, not an omission.
   */
  /*
   * D-353 — a PUBLIC event must represent a real, verified organization. Self-hosting is a Private-only
   * affordance.
   *
   * The axis is public exposure, not money (D-307/D-343): a public event carries the platform's name
   * into discovery whether or not a ticket is sold, so a named institution has to be answerable for it.
   * A private event reaches no discovery surface and can never sell, so self-hosting stands there and
   * the organization question is not asked at all.
   *
   * A PendingReview organization is deliberately NOT selectable: it is a staged request, and treating a
   * pending representation as an approved one is the exact thing §7 of the requirement forbids. The
   * server refuses it too — this is presentation over `CreateAsync`'s `representation_required`.
   */
  const selectableReps = representations.filter((r) => r.can_back_paid_event ?? r.is_verified);
  const representingValid = product === "Private" || !requiresRepresentation
    ? true
    : selectableReps.some((r) => r.organization_id === representingOrgId);

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
  const needsAuthorization = product === "Public" && representingOrgId !== null;
  const steps = needsAuthorization ? [...STEPS, "Authorization"] : STEPS;
  const authStep = steps.length - 1;

  const canNext =
    (step === 0 && representingValid) ||
    (step === 1 && !!visibility) ||
    // `representingValid` is re-checked here, not only on step 0: someone can pass Representing as
    // themselves while Free, then switch to Paid on this step, and the stale Personal answer would
    // otherwise sail through to a submission the server refuses (D-350).
    (step === 2 && (pricing === "free" || canHostPaid) && representingValid && ticketOk) ||
    (step === 3 && !!categoryId) ||
    // Type is required only when the category HAS types — the step itself says "This category has no
    // subcategories. Continue to details." for the empty case, and the server takes TypeId as optional.
    (step === 4 && (subs.length === 0 || !!typeId)) ||
    (step === 5 && detailsValid) ||
    (step === 8 && windowPairsOrdered) ||
    step === 6 || step === 7 || step === 9 ||
    // Legal (10) had NO clause because it used to be the terminal step, where Continue is never
    // rendered. D-351's Authorization step made it non-terminal and its Continue was disabled forever.
    // All-optional except the consent text, which `consentOk` gates at submit — the same rule the last
    // step already applies.
    (step === 10 && consentOk);

  /*
   * Why the button is disabled, in words.
   *
   * A disabled control with no explanation is the whole failure: somebody stares at a greyed-out
   * "Continue" with nothing on screen saying which choice is missing. Disabled controls are also
   * skipped by some screen-reader navigation entirely, so the reason has to live outside the button.
   *
   * Named per missing field rather than per step: "Make a choice to continue." on a step with four
   * inputs is a shrug, and the whole point of blocking earlier is to say what is wrong while the
   * field that is wrong is still on screen.
   */
  const blockedReason = (() => {
    if (canNext) return null;
    if (step === 0) {
      return selectableReps.length > 0
        ? "Choose the organization you are hosting this event on behalf of."
        : "A public event has to represent an organization Kurx has verified. Request representation to continue.";
    }
    if (step === 2) {
      if (!(pricing === "free" || canHostPaid)) return "Paid events need identity, PAN and a verified bank account. Choose Free, or verify first.";
      if (!representingValid) return "Go back to Representing and choose a verified organization — a paid event can't be hosted under your own name.";
      if (ticket.name.trim().length === 0) return "Name the ticket people will book.";
      if (!(Number(ticket.quantity) > 0)) return "Set how many tickets are available (at least 1).";
      return "Set a price above zero, or choose Free.";
    }
    // D-351 made Legal non-terminal, so it needs its own named reason like every other step (D-327):
    // falling through to the generic "Make a choice to continue." on a step whose only rule is the
    // consent text is exactly the shrug that rule was written to remove.
    if (step === 10) return "Write the statement registrants must accept, or turn consent off.";
    if (step === 3) return "Choose a category to continue.";
    if (step === 4) return "Choose a type to continue.";
    if (step === 5) {
      if (details.title.trim().length < 2) return "Add a title of at least 2 characters.";
      if (!details.startsAt) return "Set when the event starts.";
      if (!details.endsAt) return "Set when the event ends.";
      return "The end time must be after the start time.";
    }
    if (step === 8) {
      return windows.registrationOpensAt && windows.registrationClosesAt
        && !(new Date(windows.registrationClosesAt) > new Date(windows.registrationOpensAt))
        ? "Registration must close after it opens."
        : "Check-in must close after it opens.";
    }
    return "Make a choice to continue.";
  })();

  const submitBlockedReason =
    canSubmitReady() ? null : missingForSubmit().join(" ");

  function canSubmitReady() {
    return missingForSubmit().length === 0;
  }

  /** The specific things still missing, so the last step never just refuses. */
  function missingForSubmit(): string[] {
    const missing: string[] = [];
    if (details.title.trim().length < 2) missing.push("Add a title of at least 2 characters (Details step).");
    if (!details.startsAt) missing.push("Set a start time (Details step).");
    if (!details.endsAt) missing.push("Set an end time (Details step).");
    if (!categoryId) missing.push("Choose a category (Category step).");
    if (legal.requiresConsent && legal.consentText.trim().length === 0) {
      missing.push("Write the statement registrants must accept (Legal step).");
    }
    /*
     * D-351 — named per field rather than as one "authorization incomplete", because the server's own
     * refusals are `authorization_fields_required`, `letterhead_required`, `official_phone_invalid` and
     * `representative_role_other_required`. Mirroring them here means the wizard refuses for the same
     * reasons the API would, while the field is still on screen.
     */
    if (needsAuthorization) {
      if (!authorization.headName.trim()) missing.push("Name the signatory who authorises this event (Authorization step).");
      if (!authorization.headDesignation.trim()) missing.push("Give the signatory's designation (Authorization step).");
      if (!authorization.officialEmail.trim()) missing.push("Give the organization's official email (Authorization step).");
      if (!authorization.officialPhone.trim()) missing.push("Give the organization's official phone (Authorization step).");
      if (!authorization.representativeRole) missing.push("Choose your role in the organization (Authorization step).");
      if (authorization.representativeRole === "Other" && !authorization.representativeRoleOther.trim()) {
        missing.push("Describe your role, since you chose Other (Authorization step).");
      }
      if (!letterFile) missing.push("Attach the authorization letter (Authorization step).");
    }
    return missing;
  }

  const canSubmit = missingForSubmit().length === 0 && consentOk && ticketOk;

  function submit() {
    setError(null);
    // Through `toIsoUtc`, exactly like the six schedule windows below. These two were the only
    // datetime fields sent raw, so the wizard shifted the one time an event cannot get wrong by the
    // organiser's whole offset — 5h30m in India (D-289). Converted before the payload rather than
    // inline because the contract requires both: refusing here beats sending a time we know is
    // unparseable, which the server would store as something else entirely.
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
      const res = await createEventWizardAction(representingOrgId, values, {
        name: ticket.name,
        // Rupees in, paise on the wire (D-004). A free event is genuinely 0, not absent.
        pricePaise: pricing === "paid" ? Math.round(Number(ticket.priceRupees) * 100) : 0,
        quantity: Number(ticket.quantity) || 100
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
      {step === 0 ? (
        <div className="space-y-3">
          {product === "Private" ? (
            <div className="rounded-lg border border-border bg-surface p-4">
              <p className="text-sm text-text">Hosted by you</p>
              <p className="mt-1 text-xs text-muted">
                A private event is invitation-only, never appears in search or on Home, and can&apos;t
                sell tickets — so there&apos;s no organization to name and nothing to verify.
              </p>
            </div>
          ) : selectableReps.length > 0 ? (
            <>
              <p className="text-sm text-muted">
                Select the organization you are authorized to represent for this event.
              </p>
              <SelectCardGroup
                legend="Who are you hosting this event on behalf of?"
                name="representing"
                className="grid gap-3 sm:grid-cols-2"
              >
                {selectableReps.map((r) => (
                  <SelectCard
                    key={r.organization_id}
                    name="representing"
                    value={r.organization_id}
                    checked={representingOrgId === r.organization_id}
                    onSelect={() => setRepresentingOrgId(r.organization_id)}
                    icon={<ShieldCheck size={18} aria-hidden className="text-accent-text" />}
                    title={r.name}
                    description={`Representing · your authority: ${r.authority}`}
                  />
                ))}
              </SelectCardGroup>
            </>
          ) : !requiresRepresentation ? (
            <div className="rounded-lg border border-dashed border-border bg-surface p-4">
              <p className="text-sm text-text">Hosted by you</p>
              <p className="mt-1 text-xs text-muted">
                This environment has the verification checks switched off, so a public event can be
                created without an organization. In production this step requires a verified one.
              </p>
            </div>
          ) : (
            <div className="rounded-lg border border-dashed border-border bg-surface p-4">
              <p className="text-sm text-text">
                You don&apos;t currently have an approved organization representation.
              </p>
              <p className="mt-1 text-xs text-muted">
                A public event has to be hosted on behalf of an organization that Kurx has verified.
                Request representation and submit the organization&apos;s official authorization —
                an admin reviews it before it can be used.
              </p>
            </div>
          )}

          {/*
            Pending and rejected requests are SHOWN but never selectable (§7, §11-C/D): a staged
            representation is not an approved one, and hiding it entirely would leave someone
            re-requesting something already in the queue.
          */}
          {product === "Public" && representations.length > selectableReps.length ? (
            <ul className="space-y-2">
              {representations
                .filter((r) => !(r.can_back_paid_event ?? r.is_verified))
                .map((r) => (
                  <li key={r.organization_id}
                    className="rounded-lg border border-border bg-surface px-4 py-3 text-xs text-muted">
                    <span className="text-text">{r.name}</span> — awaiting verification. It can&apos;t
                    host a public event until an admin approves it.
                  </li>
                ))}
            </ul>
          ) : null}

          {product === "Public" ? (
            <p className="text-xs text-muted">
              Representing an organization you don&apos;t see here?{" "}
              {/*
                The dedicated request workflow (D-074/D-075), not a generic membership form: it stages a
                HIDDEN PendingReview organization plus evidence, which an admin verifies before it joins
                the registry or becomes selectable here. `/host/representing` is the list; `/new` is the
                request — linking to the list left people on a page with nothing to do.
              */}
              <a href="/host/representing/new" className="text-accent-text hover:underline">
                Register the organization
              </a>{" "}
              — an admin verifies the institution itself before it can host anything. That is a
              one-time step per organization. This event&apos;s own authorization letter is asked for
              later in this form, and is needed for every event.
            </p>
          ) : null}
        </div>
      ) : null}

      {/* Step 2 — Visibility.
          visibilityFor, not VISIBILITY: a Private event can never be Listed (D-305), and the option
          has to be absent here rather than refused eleven steps later at publish. */}
      {step === 1 ? (
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

      {/* Step 2 — Pricing */}
      {step === 2 ? (
        <div className="space-y-3">
          <SelectCardGroup legend="Is this event free or paid?" name="pricing" className="grid gap-3 sm:grid-cols-2">
            <SelectCard
              name="pricing"
              value="free"
              checked={pricing === "free"}
              onSelect={() => setPricing("free")}
              icon={<Gift size={18} aria-hidden className="text-accent-text" />}
              title="Free"
              description="No ticket charges. Anyone can register."
            />
            {/* canChoosePaid, not canHostPaid: a Private event can never take payment (D-305), so
                trust alone is not enough to offer this. The copy says "below" because the wizard now
                creates the first ticket here rather than deferring it to a tab. */}
            <SelectCard
              name="pricing"
              value="paid"
              checked={pricing === "paid"}
              onSelect={() => canChoosePaid && setPricing("paid")}
              disabled={!canChoosePaid}
              icon={<Ticket size={18} aria-hidden className="text-accent-text" />}
              title="Paid"
              description="Sell tickets. Set the price below."
            />
          </SelectCardGroup>
          {product === "Private" ? (
            <p className="flex items-center gap-2 text-xs text-muted">
              <ShieldCheck size={14} aria-hidden />
              Private events are always free — they can&apos;t sell tickets.
            </p>
          ) : !canHostPaid ? (
            <p className="flex items-center gap-2 text-xs text-muted">
              <ShieldCheck size={14} aria-hidden />
              Paid events require a verified organization.{" "}
              <a href="/host/verification" className="text-accent-text hover:underline">Verify now</a>.
            </p>
          ) : null}

          {/*
            The ticket itself, created with the event.

            This used to say "prices are set in the Tickets tab after creation" and create nothing — so
            every event the wizard produced had **no ticket type**, and an event with no ticket type
            cannot be registered for at all: the booking form answers "The host has not published any
            ticket types for this event yet." The host completed eleven steps and got an event nobody
            could join, with nothing saying so.

            One ticket is created here, free or paid. More tiers, sale windows and per-tier limits stay
            in Workspace ▸ Tickets — this is the minimum that makes an event bookable, not a second
            ticket editor.
          */}
          <div className="rounded-lg border border-border bg-surface p-4">
            <p className="text-label text-text">
              {pricing === "paid" ? "Your ticket" : "Your free ticket"}
            </p>
            <p className="mt-1 text-caption text-muted">
              Every event needs at least one ticket before anyone can register. You can add more tiers later.
            </p>
            <div className="mt-3 grid gap-3 sm:grid-cols-3">
              <div className={pricing === "paid" ? "" : "sm:col-span-2"}>
                <label className="text-sm font-medium" htmlFor="ticketName">Ticket name</label>
                <input
                  id="ticketName"
                  className={`mt-1 ${inputClass}`}
                  maxLength={80}
                  value={ticket.name}
                  onChange={(e) => setTicket({ ...ticket, name: e.target.value })}
                />
              </div>
              {pricing === "paid" ? (
                <div>
                  <label className="text-sm font-medium" htmlFor="ticketPrice">Price (₹)</label>
                  <input
                    id="ticketPrice"
                    type="number"
                    min={1}
                    className={`mt-1 ${inputClass}`}
                    value={ticket.priceRupees}
                    onChange={(e) => setTicket({ ...ticket, priceRupees: e.target.value })}
                  />
                  {/* Money is stored as paise (D-004); rupees are only the input unit. */}
                  <p className="mt-1 text-xs text-muted">Per ticket, in rupees.</p>
                </div>
              ) : null}
              <div>
                <label className="text-sm font-medium" htmlFor="ticketQuantity">How many</label>
                <input
                  id="ticketQuantity"
                  type="number"
                  min={1}
                  className={`mt-1 ${inputClass}`}
                  value={ticket.quantity}
                  onChange={(e) => setTicket({ ...ticket, quantity: e.target.value })}
                />
                <p className="mt-1 text-xs text-muted">Total available.</p>
              </div>
            </div>
          </div>
        </div>
      ) : null}

      {/* Step 3 — Category.
          shownCategories, not categories: the product gate filters the catalogue (D-305) so a Private
          host is never offered a category with no Private type behind it. */}
      {step === 3 ? (
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
      {step === 4 ? (
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

      {/* Step 5 — Details */}
      {step === 5 ? (
        <div className="space-y-4">
          <div>
            <label className="text-sm font-medium" htmlFor="title">Title</label>
            <input id="title" className={`mt-1 ${inputClass}`} value={details.title}
              onChange={(e) => setDetails({ ...details, title: e.target.value })} minLength={2} maxLength={200} />
          </div>
          <div>
            <label className="text-sm font-medium" htmlFor="subtitle">Subtitle</label>
            <input id="subtitle" className={`mt-1 ${inputClass}`} value={details.subtitle}
              onChange={(e) => setDetails({ ...details, subtitle: e.target.value })} maxLength={200} />
          </div>
          <div>
            <label className="text-sm font-medium" htmlFor="description">Description</label>
            <textarea id="description" rows={4} className={`mt-1 ${textareaClass}`} value={details.description}
              onChange={(e) => setDetails({ ...details, description: e.target.value })} />
          </div>
          <div className="grid gap-4 sm:grid-cols-2">
            <div>
              <label className="text-sm font-medium" htmlFor="startsAt">Starts at</label>
              <input id="startsAt" type="datetime-local" className={`mt-1 ${inputClass}`} value={details.startsAt}
                onChange={(e) => setDetails({ ...details, startsAt: e.target.value })} />
            </div>
            <div>
              <label className="text-sm font-medium" htmlFor="endsAt">Ends at</label>
              <input id="endsAt" type="datetime-local" className={`mt-1 ${inputClass}`} value={details.endsAt}
                onChange={(e) => setDetails({ ...details, endsAt: e.target.value })} />
            </div>
          </div>
          <div className="grid gap-4 sm:grid-cols-2">
            <div>
              <label className="text-sm font-medium" htmlFor="venueName">Venue name</label>
              <input id="venueName" className={`mt-1 ${inputClass}`} value={details.venueName}
                onChange={(e) => setDetails({ ...details, venueName: e.target.value })} />
            </div>
            <div>
              <label className="text-sm font-medium" htmlFor="city">City</label>
              <input id="city" className={`mt-1 ${inputClass}`} value={details.city}
                onChange={(e) => setDetails({ ...details, city: e.target.value })} />
            </div>
          </div>
          <div>
            <label className="text-sm font-medium" htmlFor="venueAddress">Venue address</label>
            <input id="venueAddress" className={`mt-1 ${inputClass}`} value={details.venueAddress}
              onChange={(e) => setDetails({ ...details, venueAddress: e.target.value })} />
          </div>
          <div>
            <label className="text-sm font-medium" htmlFor="capacity">Capacity</label>
            <input id="capacity" type="number" min={1} className={`mt-1 ${inputClass}`} value={details.capacity}
              onChange={(e) => setDetails({ ...details, capacity: e.target.value })} />
          </div>
          {pricing === "paid" ? (
            <p className="text-xs text-muted">You&apos;ll add ticket types and prices in the event&apos;s Tickets tab after it&apos;s created.</p>
          ) : null}
        </div>
      ) : null}

      {/* Step 6 — Content */}
      {step === 6 ? (
        <div className="space-y-4">
          <p className="text-sm text-muted">Optional, but these are what a listing card and a share preview show.</p>
          <div>
            <label className="text-sm font-medium" htmlFor="tagline">Tagline</label>
            {/* The counter was a paragraph nothing pointed at, so somebody typing into a capped
                field could not hear how much room was left — and `maxLength` stops accepting input
                silently when it runs out. `aria-describedby` links it; the live region announces it
                politely as it changes. */}
            <input id="tagline" aria-describedby="tagline-count" className={`mt-1 ${inputClass}`} maxLength={160} value={content.tagline}
              onChange={(e) => setContent({ ...content, tagline: e.target.value })} />
            <p id="tagline-count" role="status" className="mt-1 text-xs text-muted">
              {content.tagline.length}/160 characters
            </p>
          </div>
          <div>
            <label className="text-sm font-medium" htmlFor="shortDescription">Short description</label>
            <textarea id="shortDescription" rows={2} maxLength={300} className={`mt-1 ${textareaClass}`}
              value={content.shortDescription}
              onChange={(e) => setContent({ ...content, shortDescription: e.target.value })} />
            <p id="shortDescription-count" role="status" className="mt-1 text-xs text-muted">
              {content.shortDescription.length}/300 characters
            </p>
          </div>
          <div>
            <label className="text-sm font-medium" htmlFor="rules">Rules</label>
            <textarea id="rules" rows={4} className={`mt-1 ${textareaClass}`} value={content.rules}
              onChange={(e) => setContent({ ...content, rules: e.target.value })} />
          </div>
        </div>
      ) : null}

      {/* Step 7 — Location */}
      {step === 7 ? (
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
              <div>
                <label className="text-sm font-medium" htmlFor="building">Building</label>
                <input id="building" className={`mt-1 ${inputClass}`} value={place.building}
                  onChange={(e) => setPlace({ ...place, building: e.target.value })} />
              </div>
              <div>
                <label className="text-sm font-medium" htmlFor="floor">Floor</label>
                <input id="floor" className={`mt-1 ${inputClass}`} value={place.floor}
                  onChange={(e) => setPlace({ ...place, floor: e.target.value })} />
              </div>
              <div>
                <label className="text-sm font-medium" htmlFor="room">Room</label>
                <input id="room" className={`mt-1 ${inputClass}`} value={place.room}
                  onChange={(e) => setPlace({ ...place, room: e.target.value })} />
              </div>
            </div>
          ) : null}

          {place.eventMode !== "Online" ? (
            <div>
              <label className="text-sm font-medium" htmlFor="googleMapsUrl">Google Maps link</label>
              <input id="googleMapsUrl" type="url" className={`mt-1 ${inputClass}`} value={place.googleMapsUrl}
                onChange={(e) => setPlace({ ...place, googleMapsUrl: e.target.value })} />
            </div>
          ) : null}

          {place.eventMode !== "Offline" ? (
            <>
              <div>
                <label className="text-sm font-medium" htmlFor="onlineUrl">Join link</label>
                <input id="onlineUrl" type="url" className={`mt-1 ${inputClass}`} value={place.onlineUrl}
                  onChange={(e) => setPlace({ ...place, onlineUrl: e.target.value })} />
              </div>
              <div className="grid gap-4 sm:grid-cols-2">
                <div>
                  <label className="text-sm font-medium" htmlFor="meetingPlatform">Platform</label>
                  <input id="meetingPlatform" className={`mt-1 ${inputClass}`} placeholder="Zoom, Meet, Teams…"
                    value={place.meetingPlatform}
                    onChange={(e) => setPlace({ ...place, meetingPlatform: e.target.value })} />
                </div>
                <div>
                  <label className="text-sm font-medium" htmlFor="meetingPassword">Meeting password</label>
                  <input id="meetingPassword" aria-describedby="meetingPassword-help" className={`mt-1 ${inputClass}`} value={place.meetingPassword}
                    onChange={(e) => setPlace({ ...place, meetingPassword: e.target.value })} />
                  <p id="meetingPassword-help" className="mt-1 text-xs text-muted">Only shown to confirmed registrants.</p>
                </div>
              </div>
            </>
          ) : null}
        </div>
      ) : null}

      {/* Step 8 — Windows */}
      {step === 8 ? (
        <div className="space-y-4">
          <p className="text-sm text-muted">
            All optional. Registration times bound every ticket type; a ticket&apos;s own sale window can
            narrow that further, never widen it.
          </p>
          <div className="grid gap-4 sm:grid-cols-2">
            <div>
              <label className="text-sm font-medium" htmlFor="regOpens">Registration opens</label>
              <input id="regOpens" type="datetime-local" className={`mt-1 ${inputClass}`}
                value={windows.registrationOpensAt}
                onChange={(e) => setWindows({ ...windows, registrationOpensAt: e.target.value })} />
            </div>
            <div>
              <label className="text-sm font-medium" htmlFor="regCloses">Registration closes</label>
              <input id="regCloses" type="datetime-local" className={`mt-1 ${inputClass}`}
                value={windows.registrationClosesAt}
                onChange={(e) => setWindows({ ...windows, registrationClosesAt: e.target.value })} />
            </div>
            <div>
              <label className="text-sm font-medium" htmlFor="checkinOpens">Check-in opens</label>
              <input id="checkinOpens" type="datetime-local" className={`mt-1 ${inputClass}`}
                value={windows.checkinOpensAt}
                onChange={(e) => setWindows({ ...windows, checkinOpensAt: e.target.value })} />
            </div>
            <div>
              <label className="text-sm font-medium" htmlFor="checkinCloses">Check-in closes</label>
              <input id="checkinCloses" type="datetime-local" className={`mt-1 ${inputClass}`}
                value={windows.checkinClosesAt}
                onChange={(e) => setWindows({ ...windows, checkinClosesAt: e.target.value })} />
            </div>
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
                <div>
                  <label className="text-sm font-medium" htmlFor="resultDate">Results announced</label>
                  <input id="resultDate" type="datetime-local" className={`mt-1 ${inputClass}`}
                    value={windows.resultDate}
                    onChange={(e) => setWindows({ ...windows, resultDate: e.target.value })} />
                </div>
                <div>
                  <label className="text-sm font-medium" htmlFor="certRelease">Certificates released</label>
                  <input id="certRelease" type="datetime-local" className={`mt-1 ${inputClass}`}
                    value={windows.certificateReleaseAt}
                    onChange={(e) => setWindows({ ...windows, certificateReleaseAt: e.target.value })} />
                </div>
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
      {step === 9 ? (
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
              <div>
                <label className="text-sm font-medium" htmlFor="minAge">Minimum age</label>
                <input id="minAge" type="number" min={0} max={120} className={`mt-1 ${inputClass}`}
                  value={eligibility.minAge}
                  onChange={(e) => setEligibility({ ...eligibility, minAge: e.target.value })} />
              </div>
              <div>
                <label className="text-sm font-medium" htmlFor="maxAge">Maximum age</label>
                <input id="maxAge" type="number" min={0} max={120} className={`mt-1 ${inputClass}`}
                  value={eligibility.maxAge}
                  onChange={(e) => setEligibility({ ...eligibility, maxAge: e.target.value })} />
              </div>
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
            <div>
              <label className="text-sm font-medium" htmlFor="maxTeams">Maximum teams</label>
              <input id="maxTeams" type="number" min={1} className={`mt-1 ${inputClass}`}
                value={eligibility.maxTeams}
                onChange={(e) => setEligibility({ ...eligibility, maxTeams: e.target.value })} />
              <p className="mt-1 text-xs text-muted">Total teams for the event, not teams per person.</p>
            </div>
          )}
        </div>
      ) : null}

      {/* Step 10 — Legal */}
      {step === 10 ? (
        <div className="space-y-4">
          <p className="text-sm text-muted">
            Kurx&apos;s own terms always apply. These are your additional terms for this event.
          </p>
          <div>
            <label className="text-sm font-medium" htmlFor="termsUrl">Terms link</label>
            <input id="termsUrl" type="url" className={`mt-1 ${inputClass}`} value={legal.termsUrl}
              onChange={(e) => setLegal({ ...legal, termsUrl: e.target.value })} />
          </div>
          <div>
            <label className="text-sm font-medium" htmlFor="codeOfConduct">Code of conduct</label>
            <textarea id="codeOfConduct" rows={3} className={`mt-1 ${textareaClass}`} value={legal.codeOfConduct}
              onChange={(e) => setLegal({ ...legal, codeOfConduct: e.target.value })} />
          </div>
          <div className="grid gap-4 sm:grid-cols-2">
            <div>
              <label className="text-sm font-medium" htmlFor="refundPolicy">Refund policy</label>
              <textarea id="refundPolicy" rows={3} className={`mt-1 ${textareaClass}`} value={legal.refundPolicy}
                onChange={(e) => setLegal({ ...legal, refundPolicy: e.target.value })} />
            </div>
            <div>
              <label className="text-sm font-medium" htmlFor="cancellationPolicy">Cancellation policy</label>
              <textarea id="cancellationPolicy" rows={3} className={`mt-1 ${textareaClass}`}
                value={legal.cancellationPolicy}
                onChange={(e) => setLegal({ ...legal, cancellationPolicy: e.target.value })} />
            </div>
          </div>
          <label className="flex items-center gap-2 text-sm text-text">
            <input type="checkbox" checked={legal.requiresConsent}
              onChange={(e) => setLegal({ ...legal, requiresConsent: e.target.checked })} />
            Require registrants to accept a statement
          </label>
          {legal.requiresConsent ? (
            <div>
              <label className="text-sm font-medium" htmlFor="consentText">What they must accept</label>
              <textarea id="consentText" aria-describedby="consentText-help" required rows={3} className={`mt-1 ${textareaClass}`} value={legal.consentText}
                onChange={(e) => setLegal({ ...legal, consentText: e.target.value })} />
              <p id="consentText-help" className="mt-1 text-xs text-muted">
                Required — acceptance is recorded against this exact wording.
              </p>
            </div>
          ) : null}
        </div>
      ) : null}

      {/* Nav */}
      {/*
        D-351 — the institution's written consent, asked in-flow.

        Only rendered for a Public event that represents an institution, which is exactly the shape
        `PolicyResolver` raises `event_authorization_required` for. A self-represented event has no
        institution to authorise it and never sees this step.
      */}
      {needsAuthorization && step === authStep ? (
        <div className="space-y-4">
          <div>
            <p className="text-sm text-muted">
              {representations.find((r) => r.organization_id === representingOrgId)?.name ?? "This organization"}
              {" "}has to confirm it authorises this event. Filed once, with the event — a reviewer reads
              it as part of the event&apos;s review, and the event can&apos;t publish until it&apos;s approved.
            </p>
          </div>

          <div className="grid gap-4 sm:grid-cols-2">
            <div>
              <label className="text-sm font-medium text-text" htmlFor="auth-head-name">Signatory&apos;s name</label>
              <input id="auth-head-name" className={inputClass} value={authorization.headName} maxLength={160}
                onChange={(e) => setAuthorization({ ...authorization, headName: e.target.value })} />
            </div>
            <div>
              <label className="text-sm font-medium text-text" htmlFor="auth-head-designation">Their designation</label>
              <input id="auth-head-designation" className={inputClass} value={authorization.headDesignation} maxLength={160}
                onChange={(e) => setAuthorization({ ...authorization, headDesignation: e.target.value })} />
            </div>
            <div>
              <label className="text-sm font-medium text-text" htmlFor="auth-email">Official email</label>
              <input id="auth-email" type="email" className={inputClass} value={authorization.officialEmail}
                onChange={(e) => setAuthorization({ ...authorization, officialEmail: e.target.value })} />
            </div>
            <div>
              <label className="text-sm font-medium text-text" htmlFor="auth-phone">Official phone</label>
              <input id="auth-phone" className={inputClass} value={authorization.officialPhone}
                placeholder="+919876543210"
                onChange={(e) => setAuthorization({ ...authorization, officialPhone: e.target.value })} />
              <p className="mt-1 text-xs text-muted">International format, e.g. +919876543210.</p>
            </div>
          </div>

          <div>
            <label className="text-sm font-medium text-text" htmlFor="auth-role">Your role in this organization</label>
            {/* The server's vocabulary, never a copy — a list that drifts offers a role the API refuses. */}
            <select id="auth-role" className={inputClass} value={authorization.representativeRole}
              onChange={(e) => setAuthorization({ ...authorization, representativeRole: e.target.value })}>
              <option value="">Select a role…</option>
              {representativeRoles.map((r) => <option key={r} value={r}>{r}</option>)}
            </select>
          </div>

          {authorization.representativeRole === "Other" ? (
            <div>
              <label className="text-sm font-medium text-text" htmlFor="auth-role-other">Describe your role</label>
              <input id="auth-role-other" className={inputClass} value={authorization.representativeRoleOther} maxLength={80}
                onChange={(e) => setAuthorization({ ...authorization, representativeRoleOther: e.target.value })} />
            </div>
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
          <Button variant="ghost" disabled={step === 0 || isPending} onClick={() => setStep((s) => s - 1)}>Back</Button>
          {step < steps.length - 1 ? (
            <Button disabled={!canNext} onClick={() => setStep((s) => s + 1)}>Continue</Button>
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
