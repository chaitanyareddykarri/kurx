import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";

vi.mock("next/navigation", () => ({ useRouter: () => ({ push: vi.fn() }) }));
vi.mock("@/lib/event-actions", () => ({ createEventWizardAction: vi.fn(), updateEventAction: vi.fn() }));

// `useFormState` returns [state, action]; the state is what the form renders its outcome from.
let formState: unknown = { ok: true };
vi.mock("react-dom", async () => {
  const actual = await vi.importActual<typeof import("react-dom")>("react-dom");
  return { ...actual, // A string, not a vi.fn(): React warns on a non-serialisable `action` prop in jsdom, and the
    // action itself is never invoked by these tests — only the state it returns is under test.
    useFormState: () => [formState, "/noop"], useFormStatus: () => ({ pending: false }) };
});

import { CreateEventWizard } from "@/components/host/create-event-wizard";
import { EditEventForm } from "@/components/host/edit-event-form";

const EVENT = {
  id: "e1", title: "Hack Day", subtitle: "", description: "", category_id: "c1", type_id: null,
  starts_at: "2026-03-01T10:00:00Z", ends_at: "2026-03-01T18:00:00Z",
  venue: { name: "Hall", address: "", city: "Chennai" },
  capacity: null, visibility: "Listed", contact_email: "", contact_phone: "", website: "",
} as never;

/**
 * Guards for Phase 22 — event creation.
 *
 * An eleven-step wizard is the one place where "which step am I on" and "why won't it let me
 * continue" have to be answerable without sight.
 */

const CATEGORIES = [
  { id: "c1", name: "Hackathon", parent_id: null },
  { id: "c2", name: "Workshop", parent_id: null },
] as never[];

function renderWizard(over: Record<string, unknown> = {}) {
  return render(
    <CreateEventWizard
      // D-353 — a Public wizard now requires a verified representation to leave step 1. The walkthrough
      // below exercises the LATER steps, so it is seeded with one rather than asserting the block here
      // (that rule has its own tests in create-event-gate.test.tsx).
      representations={[{
        organization_id: "org-1", name: "Verified Fest Co", slug: "verified-fest-co",
        logo_key: null, authority: "owner", is_verified: true, can_back_paid_event: true
      }]}
      canHostPaid={false}
      // Both required since D-305: the gate chooses the product before the form opens, and the
      // D-353 — a Public event represents a verified organization; there is no personal option, and
      // representing one appends the D-351 Authorization step, making the flow 12 steps rather than 11.
      product="Public"
      requiresRepresentation
      representativeRoles={["Principal", "Other"]}
      categories={CATEGORIES}
      subcategories={[]}
      presets={[]}
      // D-357 — no archetype on offer permits teams by default, so the Registration step shows the
      // individual path. The team cases pass a capable archetype explicitly.
      teamCapableArchetypes={[]}
      {...over}
    />
  );
}

describe("the wizard's single-select steps are real groups", () => {
  it("names the group and exposes each option as a radio", () => {
    renderWizard();
    // Five grids of plain <button>s announced as unrelated controls with no selected state.
    // D-353 — no personal card. A Public event opens preselected on its first verified representation.
    const group = screen.getByRole("group", { name: /Who are you hosting this event on behalf of\?/ });
    expect(within(group).getByRole("radio", { name: /Verified Fest Co/ })).toBeChecked();
    expect(screen.queryByRole("radio", { name: /Test Host/ })).not.toBeInTheDocument();
  });

  it("carries the choice on the input, not on a border colour", async () => {
    renderWizard({
      representations: [
        { organization_id: "o1", name: "Verified Fest Co", authority: "owner", is_verified: true, can_back_paid_event: true },
        { organization_id: "o2", name: "IIT Madras", authority: "owner", is_verified: true, can_back_paid_event: true },
      ] as never[],
    });
    const org = screen.getByRole("radio", { name: /IIT Madras/ });
    expect(org).not.toBeChecked();
    await userEvent.click(org);
    expect(org).toBeChecked();
    expect(screen.getByRole("radio", { name: /Verified Fest Co/ })).not.toBeChecked();
  });

  it("marks an option the caller may not choose as disabled, not merely dimmed", async () => {
    renderWizard();
    // Visibility: Listed is offered, and a Private product's forbidden options are absent rather than
    // dimmed. (The disabled-Paid case moved to the gate with the question itself — see
    // create-event-gate.test.tsx, which is now the only place free/paid is asked.)
    await userEvent.click(screen.getByRole("button", { name: "Continue" }));
    expect(screen.getByRole("radio", { name: /Listed/ })).toBeChecked();
  });
});

describe("the wizard says where you are and why you are stuck", () => {
  it("announces the current step", () => {
    renderWizard();
    expect(screen.getByText(/Step 1 of 12: Representing\./)).toBeInTheDocument();
  });

  it("marks the active step programmatically", () => {
    renderWizard();
    // The stepper was pills coloured by a border; nothing said which was current.
    expect(screen.getByText("Representing", { selector: '[aria-current="step"]' })).toBeInTheDocument();
  });

  it("explains a blocked Continue rather than only disabling it", async () => {
    renderWizard();
    // Walk to Category (step 3), which requires a choice.
    for (let i = 0; i < 2; i++) await userEvent.click(screen.getByRole("button", { name: "Continue" }));

    expect(screen.getByRole("button", { name: "Continue" })).toBeDisabled();
    // A disabled control cannot carry its own reason, and some screen-reader navigation skips it.
    expect(screen.getByText("Choose a category to continue.")).toBeInTheDocument();
  });

  it("clears the reason once the step is satisfied", async () => {
    renderWizard();
    for (let i = 0; i < 2; i++) await userEvent.click(screen.getByRole("button", { name: "Continue" }));
    await userEvent.click(screen.getByRole("radio", { name: "Hackathon" }));

    expect(screen.queryByText("Choose a category to continue.")).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Continue" })).toBeEnabled();
  });
});

/**
 * A step may not wave through its own required fields.
 *
 * `canNext` was a hand-written boolean disjunction with one clause per step, written independently —
 * so Content, Location and Eligibility had NO clause (a bare `step === 6 || step === 7 || step === 9`,
 * an unconditional pass), Details checked 3 of the 9 fields it renders, and Windows checked only pair
 * ordering. It is now one per-step table. These walk the real control flow rather than asserting on
 * the table directly: the bug was that Continue was clickable, and only a click proves it isn't.
 */

/// Dates must be in the future or `validateDetails` refuses them — a fixed literal would silently rot
/// into a failing test the day it passed. Computed from the clock, never hard-coded.
function futureLocal(daysAhead: number, hour = 10): string {
  const d = new Date();
  d.setDate(d.getDate() + daysAhead);
  d.setHours(hour, 0, 0, 0);
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

const START = futureLocal(30, 10);
const END = futureLocal(30, 18);

/// Labels carry "(required)" now — `Field`/`DateTimeField` spell it out rather than using a bare
/// asterisk, which has no meaning to a screen reader. Matched by prefix so the suffix is not repeated
/// at forty call sites.
const label = (name: string) => screen.getByLabelText(new RegExp(`^${name}`));

async function walkToDetails(typeName?: string) {
  for (let i = 0; i < 2; i++) await userEvent.click(screen.getByRole("button", { name: "Continue" }));
  await userEvent.click(screen.getByRole("radio", { name: "Hackathon" }));
  await userEvent.click(screen.getByRole("button", { name: "Continue" }));
  // Type is required only when the category HAS types; the default fixture has none.
  if (typeName) await userEvent.click(screen.getByRole("radio", { name: typeName }));
  await userEvent.click(screen.getByRole("button", { name: "Continue" }));
  // D-357 — Registration sits between Type and Details. Its defaults (a named registration, 100
  // places, individual, free) are already valid, so this Continue passes straight through; the step's
  // own rules are asserted in its dedicated block below.
  await userEvent.click(screen.getByRole("button", { name: "Continue" }));
}

/// Every field the Details step asks for — which is the point of the change: filling only the three
/// the server hard-requires no longer leaves the step.
async function fillDetails(title: string) {
  await userEvent.type(label("Title"), title);
  await userEvent.type(label("Subtitle"), "A day of building");
  await userEvent.type(label("Description"), "Bring a laptop.");
  await userEvent.type(label("Starts at"), START);
  await userEvent.type(label("Ends at"), END);
  await userEvent.type(label("Venue name"), "Main Hall");
  await userEvent.type(label("City"), "Chennai");
  await userEvent.type(label("Venue address"), "1 Anna Salai");
  await userEvent.type(label("Capacity"), "100");
}

/// A Private product needs a Private Type to reach a category at all — `categoriesFor` offers only
/// categories that can actually produce one (D-326), so an empty `subcategories` list means the Private
/// branch has nothing to show. That is the fix working, not a fixture convenience.
const PRIVATE_TYPES = [
  { id: "t1", name: "Wedding", parent_id: "c1", product_class: "Private" },
] as never[];

describe("Step 6 — Details refuses every field it asks for", () => {
  const cont = () => screen.getByRole("button", { name: "Continue" });

  it("is disabled on an empty step", async () => {
    renderWizard();
    await walkToDetails();
    expect(screen.getByText(/Step 6 of 12: Details\./)).toBeInTheDocument();
    expect(cont()).toBeDisabled();
    expect(screen.getByText("Title is required.")).toBeInTheDocument();
  });

  /*
   * The reported defect, pinned field by field. Each of these used to pass: `detailsValid` was
   * `title.trim().length >= 2 && datesOrdered`, so six of the nine inputs on this step were never
   * consulted and the button meant "the server would accept this", not "this step is complete".
   */
  it("stays disabled through every partial state, and enables only on the last field", async () => {
    renderWizard();
    await walkToDetails();

    await userEvent.type(label("Title"), "Hack Day");
    expect(cont()).toBeDisabled();
    await userEvent.type(label("Subtitle"), "A day of building");
    expect(cont()).toBeDisabled();
    await userEvent.type(label("Description"), "Bring a laptop.");
    expect(cont()).toBeDisabled();
    await userEvent.type(label("Starts at"), START);
    expect(cont()).toBeDisabled();
    await userEvent.type(label("Ends at"), END);
    expect(cont()).toBeDisabled();
    await userEvent.type(label("Venue name"), "Main Hall");
    expect(cont()).toBeDisabled();
    await userEvent.type(label("City"), "Chennai");
    expect(cont()).toBeDisabled();
    await userEvent.type(label("Venue address"), "1 Anna Salai");
    expect(cont()).toBeDisabled();

    await userEvent.type(label("Capacity"), "100");
    expect(cont()).toBeEnabled();
  });

  it("goes straight back to disabled when a required field is cleared", async () => {
    renderWizard();
    await walkToDetails();
    await fillDetails("Hack Day");
    expect(cont()).toBeEnabled();

    // §15 — derived from current state, never from a previously calculated result.
    await userEvent.clear(label("City"));
    expect(cont()).toBeDisabled();
    expect(screen.getByText("City is required.")).toBeInTheDocument();
  });

  it("does not accept whitespace as content", async () => {
    renderWizard();
    await walkToDetails();
    await fillDetails("Hack Day");
    await userEvent.clear(label("Venue name"));
    await userEvent.type(label("Venue name"), "   ");

    expect(cont()).toBeDisabled();
    expect(screen.getByText("Venue name is required.")).toBeInTheDocument();
  });

  it("refuses a capacity of zero", async () => {
    renderWizard();
    await walkToDetails();
    await fillDetails("Hack Day");
    await userEvent.clear(label("Capacity"));
    await userEvent.type(label("Capacity"), "0");

    expect(cont()).toBeDisabled();
    expect(screen.getByText("Capacity must be greater than 0.")).toBeInTheDocument();
  });

  it("floors the start picker at now, so the past is not offered", async () => {
    renderWizard();
    await walkToDetails();
    // Dynamic, never a literal: the floor must move with the clock.
    const min = label("Starts at").getAttribute("min")!;
    expect(new Date(min).getTime()).toBeLessThanOrEqual(Date.now());
    expect(new Date(min).getTime()).toBeGreaterThan(Date.now() - 60_000);
  });

  it("moves the end picker's floor to whatever the start is set to", async () => {
    renderWizard();
    await walkToDetails();
    await userEvent.type(label("Starts at"), START);
    expect(label("Ends at")).toHaveAttribute("min", START);
  });

  it("refuses a start in the past", async () => {
    renderWizard();
    await walkToDetails();
    await fillDetails("Hack Day");
    await userEvent.clear(label("Starts at"));
    await userEvent.type(label("Starts at"), futureLocal(-1, 10));

    expect(cont()).toBeDisabled();
    expect(screen.getByText("Start date cannot be in the past.")).toBeInTheDocument();
  });

  it("refuses an end time that is not after the start", async () => {
    renderWizard();
    await walkToDetails();
    await fillDetails("Hack Day");
    await userEvent.clear(label("Ends at"));
    await userEvent.type(label("Ends at"), START);   // exactly equal — not after

    expect(cont()).toBeDisabled();
    expect(screen.getByText("End date and time must be after the start date.")).toBeInTheDocument();
  });

  it("invalidates an already-set end when the start moves past it", async () => {
    renderWizard();
    await walkToDetails();
    await fillDetails("Hack Day");
    expect(cont()).toBeEnabled();

    // The end was valid a moment ago and nothing touched it — moving the start is what broke it.
    await userEvent.clear(label("Starts at"));
    await userEvent.type(label("Starts at"), futureLocal(40, 10));
    expect(cont()).toBeDisabled();
    expect(screen.getByText("End date and time must be after the start date.")).toBeInTheDocument();
  });

  it("recomputes on return, so a step that was valid can become invalid again", async () => {
    renderWizard();
    await walkToDetails();
    await fillDetails("Hack Day");
    await userEvent.click(screen.getByRole("button", { name: "Continue" }));
    expect(screen.getByText(/Step 7 of 12: Content\./)).toBeInTheDocument();

    // §16 — Back must not restore a remembered `isValid`, it must re-derive from the values.
    await userEvent.click(screen.getByRole("button", { name: "Back" }));
    await userEvent.clear(label("Title"));
    expect(cont()).toBeDisabled();
  });
});

describe("Steps 7–11 gate their own fields too", () => {
  const cont = () => screen.getByRole("button", { name: "Continue" });

  async function walkTo(stepsPastDetails: number) {
    await walkToDetails();
    await fillDetails("Hack Day");
    for (let i = 0; i < stepsPastDetails; i++) await userEvent.click(cont());
  }

  it("Step 7 · Content is all-optional, and says so by letting an empty step continue", async () => {
    // Not an omission: every field on `EventContentInput` is nullable. The distinction the fix draws
    // is between "no rules" and "no clause" — this step genuinely has no required field.
    await (async () => { renderWizard(); await walkTo(1); })();
    expect(screen.getByText(/Step 7 of 12: Content\./)).toBeInTheDocument();
    expect(cont()).toBeEnabled();
  });

  it("Step 8 · Location requires a join link once the event is Online", async () => {
    renderWizard();
    await walkTo(2);
    expect(screen.getByText(/Step 8 of 12: Location\./)).toBeInTheDocument();
    // In person: no link needed.
    expect(cont()).toBeEnabled();

    // Conditional requirement appears the instant the controlling field changes (§13). This step had
    // NO clause at all, so an Online event with no link walked four more steps to a submit refusal.
    await userEvent.selectOptions(label("Mode"), "Online");
    expect(cont()).toBeDisabled();
    expect(screen.getByText("A join link is required for an online or hybrid event.")).toBeInTheDocument();

    await userEvent.type(label("Join link"), "https://meet.example.com/hack");
    expect(cont()).toBeEnabled();

    // …and back to optional when the condition goes away.
    await userEvent.clear(label("Join link"));
    expect(cont()).toBeDisabled();
    await userEvent.selectOptions(label("Mode"), "Offline");
    expect(cont()).toBeEnabled();
  });

  it("Step 8 · Location refuses a link that is not a URL", async () => {
    renderWizard();
    await walkTo(2);
    await userEvent.selectOptions(label("Mode"), "Online");
    await userEvent.type(label("Join link"), "meet.example.com/hack");   // no scheme

    expect(cont()).toBeDisabled();
    expect(screen.getByText("Join link must be a full URL, including https://.")).toBeInTheDocument();
  });

  it("Step 9 · Windows keeps each pair ordered but leaves one-ended windows alone", async () => {
    renderWizard();
    await walkTo(3);
    expect(screen.getByText(/Step 9 of 12: Windows\./)).toBeInTheDocument();
    expect(cont()).toBeEnabled();

    // One end alone is a legitimate open-ended window and must not block.
    await userEvent.type(label("Registration opens"), futureLocal(5, 9));
    expect(cont()).toBeEnabled();

    await userEvent.type(label("Registration closes"), futureLocal(4, 9));
    expect(cont()).toBeDisabled();
    expect(screen.getByText("Registration must close after it opens.")).toBeInTheDocument();

    await userEvent.clear(label("Registration closes"));
    await userEvent.type(label("Registration closes"), futureLocal(6, 9));
    expect(cont()).toBeEnabled();
  });

  it("Step 10 · Eligibility refuses an inverted age range and a zero team cap", async () => {
    renderWizard();
    await walkTo(4);
    expect(screen.getByText(/Step 10 of 12: Eligibility\./)).toBeInTheDocument();
    // All-optional: an event with no restriction is the normal case.
    expect(cont()).toBeEnabled();

    await userEvent.type(label("Minimum age"), "25");
    await userEvent.type(label("Maximum age"), "18");
    expect(cont()).toBeDisabled();
    expect(screen.getByText("Maximum age must be at least the minimum age.")).toBeInTheDocument();

    await userEvent.clear(label("Maximum age"));
    await userEvent.type(label("Maximum age"), "30");
    expect(cont()).toBeEnabled();

    // `ApplyFieldGroups` DISCARDS `MaxTeams <= 0`, so this silently meant "no cap" before.
    await userEvent.type(label("Maximum teams"), "0");
    expect(cont()).toBeDisabled();
    expect(screen.getByText("Maximum teams must be greater than 0.")).toBeInTheDocument();
  });

  it("Step 11 · Legal makes the consent text required only once consent is switched on", async () => {
    renderWizard();
    await walkTo(5);
    expect(screen.getByText(/Step 11 of 12: Legal\./)).toBeInTheDocument();
    expect(cont()).toBeEnabled();

    await userEvent.type(label("Terms link"), "not-a-url");
    expect(cont()).toBeDisabled();
    await userEvent.clear(label("Terms link"));
    expect(cont()).toBeEnabled();

    await userEvent.click(screen.getByRole("checkbox", { name: /Require registrants to accept/ }));
    expect(cont()).toBeDisabled();
    await userEvent.type(label("What they must accept"), "I agree to the rules.");
    expect(cont()).toBeEnabled();
  });
});

describe("Step 5 · Registration — the unit the price is charged in (D-357)", () => {
  const cont = () => screen.getByRole("button", { name: "Continue" });

  /// A Type whose archetype the capability engine says supports teams, and one it says does not.
  const TEAM_TYPE = [{ id: "t1", name: "Hackathon Track", parent_id: "c1", archetype_slug: "competitive" }] as never[];
  const SOLO_TYPE = [{ id: "t2", name: "Lecture", parent_id: "c1", archetype_slug: "learning" }] as never[];

  async function walkToRegistration(over: Record<string, unknown>, typeName: string) {
    renderWizard(over);
    for (let i = 0; i < 2; i++) await userEvent.click(cont());
    await userEvent.click(screen.getByRole("radio", { name: "Hackathon" }));
    await userEvent.click(cont());
    await userEvent.click(screen.getByRole("radio", { name: typeName }));
    await userEvent.click(cont());
  }

  it("offers team entry only when the archetype supports it", async () => {
    // Read from the capability engine's answer, never from a hardcoded list of type names.
    await walkToRegistration({ subcategories: TEAM_TYPE, teamCapableArchetypes: ["competitive"] }, "Hackathon Track");
    expect(screen.getByText(/Step 5 of 12: Registration\./)).toBeInTheDocument();
    expect(screen.getByRole("radio", { name: /As a team/ })).toBeInTheDocument();
  });

  it("hides team entry for an archetype that cannot have teams", async () => {
    await walkToRegistration({ subcategories: SOLO_TYPE, teamCapableArchetypes: ["competitive"] }, "Lecture");
    expect(screen.queryByRole("radio", { name: /As a team/ })).not.toBeInTheDocument();
    expect(screen.getByText(/doesn't support team entry/)).toBeInTheDocument();
  });

  it("asks for team bounds only once team entry is chosen, and blocks until they are valid", async () => {
    await walkToRegistration({ subcategories: TEAM_TYPE, teamCapableArchetypes: ["competitive"] }, "Hackathon Track");
    expect(screen.queryByLabelText(/^Smallest team/)).not.toBeInTheDocument();
    expect(cont()).toBeEnabled();

    await userEvent.click(screen.getByRole("radio", { name: /As a team/ }));
    expect(screen.getByLabelText(/^Smallest team/)).toBeInTheDocument();

    await userEvent.clear(label("Largest team"));
    await userEvent.type(label("Largest team"), "1");
    expect(cont()).toBeDisabled();
    expect(screen.getByText("The largest team size must be at least the smallest.")).toBeInTheDocument();

    await userEvent.clear(label("Largest team"));
    await userEvent.type(label("Largest team"), "5");
    expect(cont()).toBeEnabled();
  });

  it("counts capacity in the unit people register in", async () => {
    await walkToRegistration({ subcategories: TEAM_TYPE, teamCapableArchetypes: ["competitive"] }, "Hackathon Track");
    expect(screen.getByLabelText(/^How many places/)).toBeInTheDocument();

    await userEvent.click(screen.getByRole("radio", { name: /As a team/ }));
    // One team takes one inventory unit under PerGroup, so this really is a count of teams.
    expect(screen.getByLabelText(/^How many teams/)).toBeInTheDocument();
  });

  it("never shows a price without saying what it buys", async () => {
    await walkToRegistration(
      { subcategories: TEAM_TYPE, teamCapableArchetypes: ["competitive"], canHostPaid: true, initialPricing: "paid" },
      "Hackathon Track");

    expect(screen.getByLabelText(/^Price per participant/)).toBeInTheDocument();
    await userEvent.click(screen.getByRole("radio", { name: /As a team/ }));
    expect(screen.getByLabelText(/^Price per team/)).toBeInTheDocument();

    // The helper only shows once the field is valid — an error replaces it, which is `Field`'s
    // contract and the reason a blank price says "set a price" rather than explaining the unit.
    await userEvent.type(label("Price per team"), "2000");
    expect(screen.getByText(/Charged once for the whole team/)).toBeInTheDocument();
    expect(screen.getByText(/₹2000 per team/)).toBeInTheDocument();
  });

  it("does not make a free event type a price", async () => {
    await walkToRegistration({ subcategories: TEAM_TYPE, teamCapableArchetypes: ["competitive"] }, "Hackathon Track");
    expect(screen.queryByLabelText(/^Price per/)).not.toBeInTheDocument();
    expect(cont()).toBeEnabled();
  });
});

describe("Steps 1–4 gate their own fields", () => {
  const cont = () => screen.getByRole("button", { name: "Continue" });

  it("Step 1 · Representing refuses a Public event with no verified organization", async () => {
    renderWizard({ representations: [] });
    expect(cont()).toBeDisabled();
    expect(screen.getByText(/has to represent an organization Kurx has verified/)).toBeInTheDocument();
  });

  /// Free/paid is asked ONCE, at the gate, because the verification tier is chosen from it (D-343).
  /// The wizard asked it a second time — the same question, with a control that could change the answer
  /// after eligibility had been decided on it. There is no Pricing step now, and no way to reach one.
  it("never asks free or paid again — the gate already did", async () => {
    renderWizard();
    for (const label of ["Representing", "Visibility", "Category", "Type", "Registration"]) {
      expect(screen.queryByRole("radio", { name: /^Paid/ })).not.toBeInTheDocument();
      expect(screen.queryByText(/Is this event free or paid\?/)).not.toBeInTheDocument();
      expect(screen.queryByText(/Set the price below/)).not.toBeInTheDocument();
      if (label === "Category") await userEvent.click(screen.getByRole("radio", { name: "Hackathon" }));
      if (label !== "Registration") await userEvent.click(cont());
    }
    // …and the step it reached instead is Registration, which is where money is actually configured.
    expect(screen.getByText(/Step 5 of 12: Registration\./)).toBeInTheDocument();
  });

  it("states the pricing mode it inherited rather than asking for it", async () => {
    renderWizard({ canHostPaid: true, initialPricing: "paid" });
    for (let i = 0; i < 2; i++) await userEvent.click(cont());
    await userEvent.click(screen.getByRole("radio", { name: "Hackathon" }));
    await userEvent.click(cont());
    await userEvent.click(cont());

    expect(screen.getByText(/Paid event — chosen during setup/)).toBeInTheDocument();
    expect(screen.queryByRole("radio", { name: /^Paid/ })).not.toBeInTheDocument();
  });

  it("Step 3 · Category requires a choice", async () => {
    renderWizard();
    for (let i = 0; i < 2; i++) await userEvent.click(cont());
    expect(cont()).toBeDisabled();
    expect(screen.getByText("Choose a category to continue.")).toBeInTheDocument();
    await userEvent.click(screen.getByRole("radio", { name: "Hackathon" }));
    expect(cont()).toBeEnabled();
  });

  it("Step 4 · Type requires a choice only when the category has types", async () => {
    renderWizard({ subcategories: [{ id: "t1", name: "Web Track", parent_id: "c1" }] as never[] });
    for (let i = 0; i < 2; i++) await userEvent.click(cont());
    await userEvent.click(screen.getByRole("radio", { name: "Hackathon" }));
    await userEvent.click(cont());

    expect(screen.getByText(/Step 4 of 12: Type\./)).toBeInTheDocument();
    expect(cont()).toBeDisabled();
    expect(screen.getByText("Choose a type to continue.")).toBeInTheDocument();
    await userEvent.click(screen.getByRole("radio", { name: "Web Track" }));
    expect(cont()).toBeEnabled();
  });
});

describe("the last step lists what is still missing", () => {
  it("refuses to leave Legal with consent required but unwritten", async () => {
    renderWizard();
    await walkToDetails();
    await fillDetails("Hack Day");
    // Details -> Content -> Location -> Windows -> Eligibility -> Legal.
    for (let i = 0; i < 5; i++) await userEvent.click(screen.getByRole("button", { name: "Continue" }));

    /*
     * This asserted the SUBMIT list, because Legal used to be the terminal step and consent was the one
     * requirement set on it. D-351's Authorization step made Legal non-terminal, so consent is now caught
     * by the step that asks for it — which is D-327's rule working, not a regression. The backstop list
     * is still asserted below; this half moved one step earlier because the field did.
     */
    await userEvent.click(screen.getByRole("checkbox", { name: /Require registrants to accept/ }));

    expect(screen.getByRole("button", { name: "Continue" })).toBeDisabled();
    // Said twice on purpose now, and `getAllByText` rather than `getByText` because of it: once beside
    // the button (a disabled control cannot carry its own reason) and once as the field's own
    // `role="alert"` error. Naming the field is what the per-field rendering added.
    expect(screen.getAllByText(/Write the statement registrants must accept/).length).toBeGreaterThan(0);
  });

  it("still names each unmet requirement on the submit step", async () => {
    renderWizard();
    await walkToDetails();
    await fillDetails("Hack Day");
    for (let i = 0; i < 6; i++) await userEvent.click(screen.getByRole("button", { name: "Continue" }));

    // Authorization is the terminal step for an event representing an institution, and its evidence is
    // unfilled — so the backstop list speaks, which is the property this block exists to pin.
    expect(screen.getByRole("button", { name: /Create draft event/ })).toBeDisabled();
    expect(screen.getByText(/Still needed before this can be created:/)).toBeInTheDocument();
    expect(screen.getByText(/Attach the authorization letter/)).toBeInTheDocument();
  });
});

describe("a private event is not asked for capabilities it cannot have", () => {
  it("hides results, certificates and team caps", async () => {
    renderWizard({ product: "Private", subcategories: PRIVATE_TYPES });
    await walkToDetails("Wedding");
    await fillDetails("Our Wedding");
    // Details -> Content -> Location -> Windows.
    for (let i = 0; i < 3; i++) await userEvent.click(screen.getByRole("button", { name: "Continue" }));

    // `scoring` and `certificates` are Unsupported for private-gathering.
    expect(screen.queryByLabelText("Results announced")).not.toBeInTheDocument();
    expect(screen.queryByLabelText("Certificates released")).not.toBeInTheDocument();
    // Registration and check-in windows ARE supported and must survive the filter.
    expect(screen.getByLabelText("Registration opens")).toBeInTheDocument();

    await userEvent.click(screen.getByRole("button", { name: "Continue" }));
    expect(screen.queryByLabelText("Maximum teams")).not.toBeInTheDocument();
    // Age bounds turn away a stranger who registered; a private event has no open door to turn
    // anyone away from, so the rule has nothing to act on.
    expect(screen.queryByLabelText("Minimum age")).not.toBeInTheDocument();
    expect(screen.queryByLabelText("Maximum age")).not.toBeInTheDocument();
    // Gender is the one eligibility rule still offered — see D-327's open question.
    expect(screen.getByLabelText("Gender")).toBeInTheDocument();
  });

  it("still offers all three to a public event", async () => {
    renderWizard();
    await walkToDetails();
    await fillDetails("Hack Day");
    for (let i = 0; i < 3; i++) await userEvent.click(screen.getByRole("button", { name: "Continue" }));

    expect(screen.getByLabelText("Results announced")).toBeInTheDocument();
    expect(screen.getByLabelText("Certificates released")).toBeInTheDocument();

    await userEvent.click(screen.getByRole("button", { name: "Continue" }));
    expect(label("Minimum age")).toBeInTheDocument();
    expect(label("Maximum teams")).toBeInTheDocument();
  });
});

describe("EditEventForm — both outcomes of a save must be visible", () => {
  it("announces a successful save", () => {
    // The action returns `{ ok: true }` and the form rendered it nowhere, so saving fourteen fields
    // confirmed nothing at all.
    render(<EditEventForm orgId="o1" event={EVENT} categories={CATEGORIES} />);
    expect(screen.getByRole("status")).toHaveTextContent("Changes saved.");
  });

  it("announces a failed save", () => {
    formState = { error: "The venue is required" };
    render(<EditEventForm orgId="o1" event={EVENT} categories={CATEGORIES} />);
    const alert = screen.getByRole("alert");
    expect(alert).toHaveTextContent("The venue is required");
  });

  // D-363 §4 — this form posts title, dates, venue and capacity on every save, so any save from it is a
  // material edit and drops an approved event back into the review queue. Saying so beforehand is the
  // difference between a rule and a trap.
  it("warns before a save that would send an approved event back to review", () => {
    render(<EditEventForm orgId="o1" event={{ ...(EVENT as object), status: "approved" } as never} categories={CATEGORIES} />);
    expect(screen.getByText(/returns it to review/)).toBeInTheDocument();
  });

  it("says nothing of the sort while the event is still a draft", () => {
    render(<EditEventForm orgId="o1" event={{ ...(EVENT as object), status: "draft" } as never} categories={CATEGORIES} />);
    expect(screen.queryByText(/returns it to review/)).not.toBeInTheDocument();
  });
});
