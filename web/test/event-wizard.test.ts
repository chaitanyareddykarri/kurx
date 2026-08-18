import { describe, expect, it } from "vitest";
import {
  categoriesFor, cleanGroup, DETAILS_FIELD_ORDER, firstError, toIsoUtc, toLocalInput, typesFor,
  validateAuthorization, validateContent, validateDetails, validateEligibility, validateLegal,
  validatePlace, validateTicket, validateWindows, withUtcTimes, archetypeSupportsTeams,
  tiersToPayload
} from "@/lib/event-wizard";

/// D-265 — the create-event wizard's payload shaping. Both functions encode a server contract and
/// fail silently when wrong, which is exactly the kind of logic that needs a test rather than a
/// careful read.

describe("cleanGroup", () => {
  it("returns undefined when every field is blank, so an untouched step sends nothing", () => {
    // The whole point: on the server null means "leave alone", empty string means "clear". A step
    // the organiser never opened must not clear the fields another step set.
    expect(cleanGroup({ tagline: "", rules: "", faqJson: undefined })).toBeUndefined();
  });

  it("keeps only the fields that carry a value", () => {
    expect(cleanGroup({ tagline: "Ship it", shortDescription: "", rules: undefined })).toEqual({
      tagline: "Ship it"
    });
  });

  it("keeps false — an unchecked switch is a real answer, not an absent one", () => {
    expect(cleanGroup({ autoClose: false })).toEqual({ autoClose: false });
  });

  it("keeps zero — a zero fee is a deliberate waiver", () => {
    expect(cleanGroup({ platformFeePercent: 0 })).toEqual({ platformFeePercent: 0 });
  });

  it("drops NaN, which is what an empty numeric input parses to", () => {
    expect(cleanGroup({ minAge: Number("") || Number.NaN, maxAge: 25 })).toEqual({ maxAge: 25 });
  });

  it("drops null as well as undefined", () => {
    expect(cleanGroup({ a: null, b: "x" })).toEqual({ b: "x" });
  });
});

describe("toIsoUtc", () => {
  it("returns undefined for an empty input", () => {
    expect(toIsoUtc("")).toBeUndefined();
    expect(toIsoUtc(undefined)).toBeUndefined();
  });

  it("converts a zoneless datetime-local value to a UTC instant", () => {
    // `datetime-local` has no zone. Sent raw the server reads it as UTC and every time shifts by the
    // organiser's offset — in India that is 5h30m, which nobody notices until check-in.
    const iso = toIsoUtc("2026-08-10T18:30");
    expect(iso).toMatch(/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z$/);
    // Round-tripping through the local zone must land on the same wall clock the organiser typed.
    const back = new Date(iso!);
    expect(back.getHours()).toBe(18);
    expect(back.getMinutes()).toBe(30);
  });

  it("returns undefined rather than 'Invalid Date' for junk", () => {
    expect(toIsoUtc("not-a-date")).toBeUndefined();
  });
});

/// D-305 — which Types and Categories the gate's Public/Private answer permits.
///
/// This is a data rule, not a rendering one, so it is tested against the predicate rather than through
/// the DOM. Getting it wrong shows the organiser the wrong catalogue — a wizard that looks completely
/// normal and offers a Wedding under "Public" — which is the failure shape this whole session has been
/// chasing: correct-looking output over the wrong data.
describe("typesFor", () => {
  const types = [
    { id: "wedding", parent_id: "family", product_class: "Private" },
    { id: "meetup", parent_id: "community", product_class: "Public" },
    { id: "legacy", parent_id: "community", product_class: null },
    { id: "unset", parent_id: "community" }
  ];

  it("offers only Private types when Private was chosen", () => {
    expect(typesFor(types, "Private").map((t) => t.id)).toEqual(["wedding"]);
  });

  it("treats an unclassified type as Public, exactly as the server's fallback does", () => {
    // ResolveArchetypeAsync returns EventProduct.Public when ProductClass is null. If the client ever
    // disagreed, a Type would be offered here and then produce an event of the other class.
    expect(typesFor(types, "Public").map((t) => t.id)).toEqual(["meetup", "legacy", "unset"]);
  });

  it("never offers a Private type under Public", () => {
    expect(typesFor(types, "Public").some((t) => t.id === "wedding")).toBe(false);
  });
});

describe("categoriesFor", () => {
  const categories = [
    { id: "family" },
    { id: "community" },
    { id: "empty" }
  ];
  const types = [
    { id: "wedding", parent_id: "family", product_class: "Private" },
    { id: "meetup", parent_id: "community", product_class: "Public" }
  ];

  it("keeps every category for Public, including one with no types at all", () => {
    // A category with no Types still yields a Public event: Type is optional and a null Type resolves
    // to Public. Hiding it would block a legitimate event.
    expect(categoriesFor(categories, types, "Public").map((c) => c.id)).toEqual(["family", "community", "empty"]);
  });

  it("hides categories that cannot produce a Private event", () => {
    // Otherwise the organiser picks "Community", reaches the Type step, finds it empty, and has no way
    // forward — a dead end that looks like a bug in the taxonomy.
    expect(categoriesFor(categories, types, "Private").map((c) => c.id)).toEqual(["family"]);
  });
});

describe("withUtcTimes", () => {
  /// D-289 — the return leg of the round-trip. `toLocalInput` renders a stored instant in the
  /// browser's zone; without this the form posted that wall-clock string bare, the server read it as
  /// UTC, and the event moved by the organiser's offset **on every save**, so the error compounded.

  function fd(entries: Record<string, string>) {
    const f = new FormData();
    for (const [k, v] of Object.entries(entries)) f.set(k, v);
    return f;
  }

  it("converts both datetime fields to UTC instants", () => {
    const out = withUtcTimes(fd({ startsAt: "2026-09-15T10:00", endsAt: "2026-09-15T17:00" }));
    expect(out.get("startsAt")).toBe(toIsoUtc("2026-09-15T10:00"));
    expect(out.get("endsAt")).toBe(toIsoUtc("2026-09-15T17:00"));
  });

  it("is idempotent, which is what stops the shift compounding across saves", () => {
    // The bug was not that one save was wrong — it was that saving again moved it again. Feeding a
    // value that has already been converted must be a no-op.
    const once = withUtcTimes(fd({ startsAt: "2026-09-15T10:00" }));
    const twice = withUtcTimes(once);
    expect(twice.get("startsAt")).toBe(once.get("startsAt"));
  });

  it("preserves the wall clock the organiser typed", () => {
    const out = withUtcTimes(fd({ startsAt: "2026-09-15T10:00" }));
    const back = new Date(String(out.get("startsAt")));
    expect(back.getHours()).toBe(10);
    expect(back.getMinutes()).toBe(0);
  });

  it("leaves a blank field blank — absent means 'leave alone', not 'set to now'", () => {
    const out = withUtcTimes(fd({ startsAt: "", endsAt: "2026-09-15T17:00" }));
    expect(out.get("startsAt")).toBe("");
    expect(out.get("endsAt")).toBe(toIsoUtc("2026-09-15T17:00"));
  });

  it("leaves an unparseable value untouched rather than inventing one", () => {
    const out = withUtcTimes(fd({ startsAt: "not-a-date" }));
    expect(out.get("startsAt")).toBe("not-a-date");
  });

  it("only touches the fields it is given", () => {
    const out = withUtcTimes(fd({ title: "Workshop", startsAt: "2026-09-15T10:00" }));
    expect(out.get("title")).toBe("Workshop");
  });
});

describe("toLocalInput", () => {
  /// The display leg. One of the three copies this replaced was `iso.slice(0, 16)`, which puts the
  /// UTC wall clock into a control the browser reads as local — invisible until something converts
  /// on the way back, at which point it shifts the value the other way (D-289).

  it("round-trips with toIsoUtc, which is the only property that matters", () => {
    const typed = "2026-09-15T10:00";
    expect(toLocalInput(toIsoUtc(typed)!)).toBe(typed);
  });

  it("renders a UTC instant in the browser's zone, not by truncating the string", () => {
    const iso = "2026-09-15T04:30:00Z";
    const expected = new Date(iso);
    const [, hh, mm] = toLocalInput(iso).match(/T(\d{2}):(\d{2})$/)!;
    expect(Number(hh)).toBe(expected.getHours());
    expect(Number(mm)).toBe(expected.getMinutes());
  });

  it("returns an empty string for junk rather than 'NaN-NaN-NaN'", () => {
    expect(toLocalInput("not-a-date")).toBe("");
  });
});

/**
 * Per-step validation — the rules behind every Continue button in the wizard.
 *
 * `canNext` was a hand-written boolean disjunction with one clause per step, each written
 * independently: three steps had NO clause (a bare `step === 6 || step === 7 || step === 9`,
 * an unconditional pass), Details checked 3 of the 9 fields it renders, and Windows checked only pair
 * ordering. These pin each rule against the server refusal it mirrors, without a DOM; the wizard test
 * walks the same rules through the buttons.
 */

/// Never a literal: a fixed date rots into a failing test the day it goes past.
function local(msFromNow: number): string {
  const d = new Date(Date.now() + msFromNow);
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}
const HOUR = 3_600_000;
const DAY = 24 * HOUR;

const VALID_DETAILS = {
  title: "Hack Day", subtitle: "A day of building", description: "Bring a laptop.",
  startsAt: local(30 * DAY), endsAt: local(30 * DAY + 8 * HOUR),
  venueName: "Main Hall", city: "Chennai", venueAddress: "1 Anna Salai", capacity: "100"
};

describe("validateDetails", () => {
  it("passes a fully completed step", () => {
    expect(validateDetails(VALID_DETAILS)).toEqual({});
  });

  /*
   * The reported defect. `detailsValid` was `title.trim().length >= 2 && datesOrdered`, so a step with
   * a title and two dates and NOTHING ELSE passed — six inputs the step renders were never consulted.
   */
  it("refuses a step carrying only a title", () => {
    const errors = validateDetails({
      ...VALID_DETAILS, subtitle: "", description: "", venueName: "", city: "", venueAddress: "",
      capacity: ""
    });
    expect(Object.keys(errors).sort()).toEqual(
      ["capacity", "city", "description", "subtitle", "venueAddress", "venueName"]);
  });

  it.each([
    ["title", "Title is required"],
    ["subtitle", "Subtitle is required"],
    ["description", "Description is required"],
    ["venueName", "Venue name is required"],
    ["city", "City is required"],
    ["venueAddress", "Venue address is required"],
    ["capacity", "Capacity is required"]
  ])("treats a whitespace-only %s as empty", (field, message) => {
    const errors = validateDetails({ ...VALID_DETAILS, [field]: "   " });
    expect(errors[field as keyof typeof errors]).toBe(message);
  });

  it("holds the server's own two-character floor on the title", () => {
    expect(validateDetails({ ...VALID_DETAILS, title: "x" }).title)
      .toBe("Title must be at least 2 characters");
  });

  it("refuses a capacity that is zero, negative or fractional", () => {
    for (const capacity of ["0", "-5", "1.5"]) {
      expect(validateDetails({ ...VALID_DETAILS, capacity }).capacity)
        .toBe("Capacity must be greater than 0");
    }
  });

  // The date rules, pinned against an injected clock rather than the wall clock.
  const NOON = new Date("2026-08-15T12:00:00");

  it("refuses a start on a past date", () => {
    expect(validateDetails({ ...VALID_DETAILS, startsAt: "2026-08-14T18:00" }, NOON).startsAt)
      .toBe("Start date cannot be in the past");
  });

  it("refuses a start earlier today, which a date-only check would miss", () => {
    expect(validateDetails({ ...VALID_DETAILS, startsAt: "2026-08-15T11:59" }, NOON).startsAt)
      .toBe("Start date cannot be in the past");
  });

  it("accepts today at a time still to come", () => {
    expect(validateDetails(
      { ...VALID_DETAILS, startsAt: "2026-08-15T12:30", endsAt: "2026-08-15T18:00" }, NOON).startsAt)
      .toBeUndefined();
  });

  it("refuses an end equal to the start, not only one before it", () => {
    const at = local(10 * DAY);
    expect(validateDetails({ ...VALID_DETAILS, startsAt: at, endsAt: at }).endsAt)
      .toBe("End date and time must be after the start date");
  });

  it("invalidates a previously fine end when the start moves past it", () => {
    // Nothing touched `endsAt`; moving `startsAt` is what broke it, and the step must say so at once.
    const errors = validateDetails({ ...VALID_DETAILS, startsAt: local(40 * DAY) });
    expect(errors.endsAt).toBe("End date and time must be after the start date");
  });

  it("names the topmost unmet requirement, so the spoken reason matches the screen", () => {
    const errors = validateDetails({ ...VALID_DETAILS, title: "", city: "" });
    expect(firstError(errors, DETAILS_FIELD_ORDER)).toBe("Title is required");
  });
});

describe("validatePlace", () => {
  /// D-378 — every field the Mode asks for is required. `FULL_OFFLINE` / `FULL_ONLINE` are complete, so
  /// each test blanks exactly ONE field and a rule that stops firing cannot hide behind a neighbour.
  const FULL_OFFLINE = {
    eventMode: "Offline", onlineUrl: "", building: "Block A", floor: "2", room: "204",
    googleMapsUrl: "https://maps.example.com/x", meetingPlatform: "", meetingPassword: ""
  };
  const FULL_ONLINE = {
    eventMode: "Online", onlineUrl: "https://meet.example.com/x", building: "", floor: "", room: "",
    googleMapsUrl: "", meetingPlatform: "Meet", meetingPassword: "abc123"
  };

  it("accepts a fully answered step in either mode", () => {
    expect(validatePlace(FULL_OFFLINE)).toEqual({});
    expect(validatePlace(FULL_ONLINE)).toEqual({});
  });

  it.each(["building", "floor", "room", "googleMapsUrl"] as const)(
    "requires %s on an in-person event", (field) => {
      expect(validatePlace({ ...FULL_OFFLINE, [field]: "" })[field]).toBeDefined();
      expect(validatePlace({ ...FULL_OFFLINE, [field]: "   " })[field]).toBeDefined();
    });

  it.each(["onlineUrl", "meetingPlatform", "meetingPassword"] as const)(
    "requires %s on an online event", (field) => {
      expect(validatePlace({ ...FULL_ONLINE, [field]: "" })[field]).toBeDefined();
    });

  /*
   * The conditional IS the rule, not a softening of it. A join link on an in-person event and a floor
   * number on an online one are not missing — they do not exist. Requiring both groups unconditionally
   * would make every event unsubmittable, which is what §9 forbids.
   */
  it("asks an in-person event for nothing online, and an online event for nothing physical", () => {
    const offline = validatePlace(FULL_OFFLINE);
    expect(offline.onlineUrl).toBeUndefined();
    expect(offline.meetingPlatform).toBeUndefined();
    const online = validatePlace(FULL_ONLINE);
    expect(online.building).toBeUndefined();
    expect(online.googleMapsUrl).toBeUndefined();
  });

  it("asks a Hybrid event for BOTH groups", () => {
    const errors = validatePlace({
      eventMode: "Hybrid", onlineUrl: "", building: "", floor: "", room: "",
      googleMapsUrl: "", meetingPlatform: "", meetingPassword: ""
    });
    expect(errors.building).toBeDefined();
    expect(errors.onlineUrl).toBeDefined();
  });

  it("refuses a link with no scheme, which is what type=url already promised", () => {
    expect(validatePlace({ ...FULL_ONLINE, onlineUrl: "meet.example.com" }).onlineUrl)
      .toBe("Join link must be a full URL, including https://");
    expect(validatePlace({ ...FULL_OFFLINE, googleMapsUrl: "maps.example" }).googleMapsUrl)
      .toBe("Google Maps link must be a full URL, including https://");
  });

  it("says nothing else until the mode itself is valid", () => {
    expect(validatePlace({ ...FULL_OFFLINE, eventMode: "Teleport" }))
      .toEqual({ eventMode: "Choose how the event is held" });
  });
});

describe("validateWindows", () => {
  const EMPTY = {
    registrationOpensAt: "", registrationClosesAt: "", checkinOpensAt: "",
    checkinClosesAt: "", resultDate: "", certificateReleaseAt: "", autoClose: false
  };
  const FULL = {
    ...EMPTY,
    registrationOpensAt: local(DAY), registrationClosesAt: local(2 * DAY),
    checkinOpensAt: local(3 * DAY), checkinClosesAt: local(4 * DAY),
    resultDate: local(5 * DAY), certificateReleaseAt: local(6 * DAY)
  };

  it("accepts a fully answered step", () => {
    expect(validateWindows(FULL)).toEqual({});
  });

  it("D-378 — every window is required", () => {
    expect(validateWindows(EMPTY)).toEqual({
      registrationOpensAt: "Registration opening time is required",
      registrationClosesAt: "Registration closing time is required",
      checkinOpensAt: "Check-in opening time is required",
      checkinClosesAt: "Check-in closing time is required",
      resultDate: "Results announcement time is required",
      certificateReleaseAt: "Certificate release time is required"
    });
  });

  /// A Private event never SEES the results or certificate dates (D-266 M2 — the archetype has both
  /// Unsupported), so blocking on them would strand it on a step with nothing to fill in.
  it("does not ask a private event for the two dates it is never shown", () => {
    expect(validateWindows({ ...FULL, resultDate: "", certificateReleaseAt: "" }, true)).toEqual({});
  });

  it("orders each pair once both ends are given", () => {
    expect(validateWindows({
      ...FULL, registrationOpensAt: local(5 * DAY), registrationClosesAt: local(4 * DAY)
    }).registrationClosesAt).toBe("Registration must close after it opens");
    expect(validateWindows({
      ...FULL, checkinOpensAt: local(5 * DAY), checkinClosesAt: local(5 * DAY)
    }).checkinClosesAt).toBe("Check-in must close after it opens");
  });
});

describe("validateEligibility", () => {
  const FULL = { minAge: "16", maxAge: "30", genderRestriction: "Any", maxTeams: "40" };

  it("accepts a fully answered step", () => {
    expect(validateEligibility(FULL)).toEqual({});
  });

  it("D-378 — the ages and the team cap are required", () => {
    expect(validateEligibility({ ...FULL, minAge: "", maxAge: "", maxTeams: "" })).toEqual({
      minAge: "Minimum age is required",
      maxAge: "Maximum age is required",
      maxTeams: "Maximum teams is required"
    });
  });

  it("does not ask a private event for the team cap it is never shown", () => {
    expect(validateEligibility({ ...FULL, maxTeams: "" }, true)).toEqual({});
  });

  it("refuses a maximum age below the minimum (invalid_age_range)", () => {
    expect(validateEligibility({ ...FULL, minAge: "25", maxAge: "18" }).maxAge)
      .toBe("Maximum age must be at least the minimum age");
  });

  it("refuses a team cap of zero, which the server would silently discard", () => {
    // `ApplyFieldGroups` does `MaxTeams <= 0 ? null` — so this used to mean "no cap", with no warning.
    expect(validateEligibility({ ...FULL, maxTeams: "0" }).maxTeams)
      .toBe("Maximum teams must be greater than 0");
  });

  it("refuses an age outside the range the inputs already declare", () => {
    expect(validateEligibility({ ...FULL, minAge: "200" }).minAge).toBeDefined();
  });
});

describe("validateLegal", () => {
  const FULL = {
    termsUrl: "https://example.com/terms", codeOfConduct: "Be kind",
    refundPolicy: "No refunds", cancellationPolicy: "Cancel anytime",
    requiresConsent: false, consentText: ""
  };

  it("accepts a fully answered step", () => {
    expect(validateLegal(FULL)).toEqual({});
  });

  it("D-378 — the four policy fields are required", () => {
    expect(validateLegal({ ...FULL, termsUrl: "" }).termsUrl).toBe("Terms link is required");
    expect(validateLegal({ ...FULL, codeOfConduct: "  " }).codeOfConduct)
      .toBe("Code of conduct is required");
    expect(validateLegal({ ...FULL, refundPolicy: "" }).refundPolicy)
      .toBe("Refund policy is required");
    expect(validateLegal({ ...FULL, cancellationPolicy: "" }).cancellationPolicy)
      .toBe("Cancellation policy is required");
  });

  it("refuses a terms link with no scheme", () => {
    expect(validateLegal({ ...FULL, termsUrl: "example.com/terms" }).termsUrl)
      .toBe("Terms link must be a full URL, including https://");
  });

  it("makes the consent text required only once consent is switched on", () => {
    expect(validateLegal({ ...FULL, requiresConsent: true }).consentText).toBeDefined();
    expect(validateLegal({ ...FULL, requiresConsent: true, consentText: "  " }).consentText)
      .toBeDefined();
    expect(validateLegal({ ...FULL, requiresConsent: true, consentText: "I agree" })).toEqual({});
  });
});

describe("validateTicket", () => {
  /// D-372 — the registration option and the UNIT its price is charged in.
  const SOLO = {
    name: "General", priceRupees: "", quantity: "10",
    participation: "individual" as const, teamMin: "2", teamMax: "4",
    // D-366 — no bands is the default and means one price for every team size.
    tiers: [] as { minSize: string; maxSize: string; priceRupees: string }[]
  };
  const TEAM = { ...SOLO, participation: "team" as const };

  it("requires a name and a positive quantity", () => {
    const errors = validateTicket({ ...SOLO, name: " ", quantity: "0" }, "free");
    expect(errors.name).toBeDefined();
    expect(errors.quantity).toBeDefined();
  });

  it("requires a price above zero only when the event is paid", () => {
    expect(validateTicket(SOLO, "free")).toEqual({});
    expect(validateTicket(SOLO, "paid").priceRupees).toBe("Set a price above zero, or choose Free");
  });

  it("names the quantity in the unit it is counted in", () => {
    // "How many teams" vs "how many places" is not cosmetic: under PerGroup one team takes one unit,
    // so the number means something different and the message has to say which.
    expect(validateTicket({ ...SOLO, quantity: "" }, "free").quantity).toMatch(/places/);
    expect(validateTicket({ ...TEAM, quantity: "" }, "free").quantity).toMatch(/teams/);
  });

  it("asks for team bounds only when the entry is a team", () => {
    expect(validateTicket({ ...SOLO, teamMin: "", teamMax: "" }, "free")).toEqual({});
    const errors = validateTicket({ ...TEAM, teamMin: "", teamMax: "" }, "free");
    expect(errors.teamMin).toBeDefined();
    expect(errors.teamMax).toBeDefined();
  });

  it("refuses a largest team below the smallest", () => {
    expect(validateTicket({ ...TEAM, teamMin: "4", teamMax: "2" }, "free").teamMax)
      .toBe("The largest team size must be at least the smallest");
  });

  it("accepts a fully configured paid team entry", () => {
    expect(validateTicket({ ...TEAM, priceRupees: "2000" }, "paid")).toEqual({});
  });

  /**
   * D-366 — a team's price may depend on its size.
   *
   * The rules that matter are the ones invisible row by row: a set of individually-sane bands can still
   * leave a size unpriced or price one twice, and only reading them together shows it. Mirrors
   * `TicketTypeService.ValidateTiers`, so the form refuses exactly what the server would.
   */
  describe("team-size price bands", () => {
    const banded = (tiers: { minSize: string; maxSize: string; priceRupees: string }[]) =>
      ({ ...TEAM, teamMin: "2", teamMax: "5", tiers });

    const FULL = [
      { minSize: "2", maxSize: "2", priceRupees: "250" },
      { minSize: "3", maxSize: "3", priceRupees: "300" },
      { minSize: "4", maxSize: "5", priceRupees: "400" },
    ];

    it("accepts bands that cover every allowed size exactly once", () => {
      expect(validateTicket(banded(FULL), "paid")).toEqual({});
    });

    it("stops asking for a single price once bands exist", () => {
      // Two price inputs for one decision is the duplicate-question mistake (D-365); with bands the
      // bands ARE the price, and the headline is derived server-side from the cheapest.
      expect(validateTicket(banded(FULL), "paid").priceRupees).toBeUndefined();
    });

    it("refuses two rules covering the same size", () => {
      expect(validateTicket(banded([
        { minSize: "2", maxSize: "4", priceRupees: "250" },
        { minSize: "3", maxSize: "5", priceRupees: "300" },
      ]), "paid").tiers).toBe("Two rules cover the same team size");
    });

    it("refuses a hole between two rules", () => {
      // Teams of 3 are allowed by the ticket and priced by nothing — a refusal at checkout for a size
      // the event advertises.
      expect(validateTicket(banded([
        { minSize: "2", maxSize: "2", priceRupees: "250" },
        { minSize: "4", maxSize: "5", priceRupees: "400" },
      ]), "paid").tiers).toBe("No price for teams of 3");
    });

    it("refuses a set that stops short of the largest team", () => {
      expect(validateTicket(banded([{ minSize: "2", maxSize: "3", priceRupees: "250" }]), "paid").tiers)
        .toBe("Cover every team size from 2 to 5");
    });

    it("refuses a rule outside the allowed team sizes", () => {
      expect(validateTicket(banded([
        { minSize: "2", maxSize: "5", priceRupees: "250" },
        { minSize: "6", maxSize: "6", priceRupees: "500" },
      ]), "paid").tiers).toBe("Keep every rule between 2 and 5 members");
    });

    it("refuses a rule priced at zero", () => {
      expect(validateTicket(banded([{ minSize: "2", maxSize: "5", priceRupees: "0" }]), "paid").tiers)
        .toBe("Each rule needs a price above zero");
    });

    it("refuses bands on a free event rather than inventing ₹0 rules", () => {
      expect(validateTicket(banded(FULL), "free").tiers)
        .toBe("A free event has no prices to set by team size");
    });
  });

  describe("tiersToPayload", () => {
    it("converts rupees to paise and sizes to numbers", () => {
      expect(tiersToPayload([{ minSize: "2", maxSize: "3", priceRupees: "250" }]))
        .toEqual([{ minSize: 2, maxSize: 3, pricePaise: 25000 }]);
    });

    it("sends nothing at all when there are no bands", () => {
      // `[]` would claim the organiser cleared a set they never had; the server treats absent and empty
      // alike, and an unbanded ticket's request must stay byte-identical to before.
      expect(tiersToPayload([])).toBeUndefined();
    });
  });
});

describe("archetypeSupportsTeams", () => {
  /// Read from the capability engine's own answer — a hardcoded list of "team-ish" type names is the
  /// duplication D-372 exists to prevent, and would disagree the first time an admin edited the matrix.
  it("is false when the engine says the capability is unsupported", () => {
    expect(archetypeSupportsTeams([{ slug: "teams", state: "unsupported" }])).toBe(false);
    expect(archetypeSupportsTeams([{ slug: "teams", state: "locked" }])).toBe(false);
  });

  it("is true for any state that is not a refusal", () => {
    expect(archetypeSupportsTeams([{ slug: "teams", state: "off" }])).toBe(true);
    expect(archetypeSupportsTeams([{ slug: "teams", state: "optional" }])).toBe(true);
    expect(archetypeSupportsTeams([{ slug: "teams", state: "required" }])).toBe(true);
  });

  it("fails closed when the capability set is absent or has no teams row", () => {
    // An unreadable answer must not offer team entry the server may then refuse.
    expect(archetypeSupportsTeams(null)).toBe(false);
    expect(archetypeSupportsTeams([])).toBe(false);
    expect(archetypeSupportsTeams([{ slug: "paid", state: "optional" }])).toBe(false);
  });
});

describe("validateContent", () => {
  const FILLED = { tagline: "One line", shortDescription: "A summary", rules: "Be kind" };

  it("accepts a fully written step", () => {
    expect(validateContent(FILLED)).toEqual({});
  });

  /// D-378 — each field alone blocks the step. Asserted one at a time, because a rule written as
  /// "all three or nothing" passes a test that fills two and still lets the third through.
  it("requires all three fields, each independently", () => {
    expect(validateContent({ tagline: "", shortDescription: "", rules: "" })).toEqual({
      tagline: "Tagline is required",
      shortDescription: "Short description is required",
      rules: "Rules are required"
    });
    expect(validateContent({ ...FILLED, tagline: "" }).tagline).toBe("Tagline is required");
    expect(validateContent({ ...FILLED, shortDescription: "" }).shortDescription)
      .toBe("Short description is required");
    expect(validateContent({ ...FILLED, rules: "" }).rules).toBe("Rules are required");
  });

  /// Whitespace is not content — §3's explicit case.
  it("treats a whitespace-only value as empty", () => {
    const errors = validateContent({ tagline: "   ", shortDescription: "\t\n", rules: "  " });
    expect(errors.tagline).toBe("Tagline is required");
    expect(errors.shortDescription).toBe("Short description is required");
    expect(errors.rules).toBe("Rules are required");
  });

  /// The ceilings still mirror `tagline_too_long` / `short_description_too_long`, and a filled-but-
  /// over-long field reports the LENGTH problem rather than claiming it is missing.
  it("still caps the two lengths the server refuses", () => {
    expect(validateContent({ ...FILLED, tagline: "x".repeat(161) }).tagline)
      .toBe("Tagline must be 160 characters or fewer");
    expect(validateContent({ ...FILLED, shortDescription: "x".repeat(301) }).shortDescription)
      .toBe("Short description must be 300 characters or fewer");
    expect(validateContent({ ...FILLED, tagline: "x".repeat(160) }).tagline).toBeUndefined();
  });
});

describe("validateAuthorization", () => {
  const FILLED = {
    headName: "R Iyer", headDesignation: "Principal", officialEmail: "head@iitm.ac.in",
    officialPhone: "+919876543210", representativeRole: "Principal", representativeRoleOther: ""
  };

  it("passes a complete filing with a letter attached", () => {
    expect(validateAuthorization(FILLED, true)).toEqual({});
  });

  it("refuses a phone that is not E.164, matching the server's own regex", () => {
    expect(validateAuthorization({ ...FILLED, officialPhone: "9876543210" }, true).officialPhone)
      .toBe("Phone must be in international format, e.g. +919876543210");
  });

  it("makes the free-text role required only when the role is Other", () => {
    expect(validateAuthorization({ ...FILLED, representativeRole: "Other" }, true)
      .representativeRoleOther).toBeDefined();
    expect(validateAuthorization(
      { ...FILLED, representativeRole: "Other", representativeRoleOther: "Dean" }, true)).toEqual({});
  });

  it("refuses a missing letter (letterhead_required)", () => {
    expect(validateAuthorization(FILLED, false).letterFile).toBe("Attach the authorization letter");
  });
});
