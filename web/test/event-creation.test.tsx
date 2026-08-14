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
      representations={[]}
      canHostPaid={false}
      // Both required since D-305: the gate chooses the product before the form opens, and the
      // Representing step titles the personal option with the host's own name, never "Personal".
      product="Public"
      hostName="Test Host"
      categories={CATEGORIES}
      subcategories={[]}
      presets={[]}
      {...over}
    />
  );
}

describe("the wizard's single-select steps are real groups", () => {
  it("names the group and exposes each option as a radio", () => {
    renderWizard();
    // Five grids of plain <button>s announced as unrelated controls with no selected state.
    const group = screen.getByRole("group", { name: /Who are you hosting this event as\?/ });
    expect(within(group).getByRole("radio", { name: /Test Host/ })).toBeChecked();
  });

  it("carries the choice on the input, not on a border colour", async () => {
    renderWizard({
      representations: [{ organization_id: "o1", name: "IIT Madras", authority: "Owner" }] as never[],
    });
    const org = screen.getByRole("radio", { name: /IIT Madras/ });
    expect(org).not.toBeChecked();
    await userEvent.click(org);
    expect(org).toBeChecked();
    expect(screen.getByRole("radio", { name: /Test Host/ })).not.toBeChecked();
  });

  it("marks an option the caller may not choose as disabled, not merely dimmed", async () => {
    renderWizard();
    // Step 0 -> 1 -> 2 (Pricing). `canHostPaid` is false.
    await userEvent.click(screen.getByRole("button", { name: "Continue" }));
    await userEvent.click(screen.getByRole("button", { name: "Continue" }));
    expect(screen.getByRole("radio", { name: /Paid/ })).toBeDisabled();
    expect(screen.getByRole("radio", { name: /Free/ })).toBeChecked();
  });
});

describe("the wizard says where you are and why you are stuck", () => {
  it("announces the current step", () => {
    renderWizard();
    expect(screen.getByText(/Step 1 of 11: Representing\./)).toBeInTheDocument();
  });

  it("marks the active step programmatically", () => {
    renderWizard();
    // The stepper was pills coloured by a border; nothing said which was current.
    expect(screen.getByText("Representing", { selector: '[aria-current="step"]' })).toBeInTheDocument();
  });

  it("explains a blocked Continue rather than only disabling it", async () => {
    renderWizard();
    // Walk to Category (step 3), which requires a choice.
    for (let i = 0; i < 3; i++) await userEvent.click(screen.getByRole("button", { name: "Continue" }));

    expect(screen.getByRole("button", { name: "Continue" })).toBeDisabled();
    // A disabled control cannot carry its own reason, and some screen-reader navigation skips it.
    expect(screen.getByText("Choose a category to continue.")).toBeInTheDocument();
  });

  it("clears the reason once the step is satisfied", async () => {
    renderWizard();
    for (let i = 0; i < 3; i++) await userEvent.click(screen.getByRole("button", { name: "Continue" }));
    await userEvent.click(screen.getByRole("radio", { name: "Hackathon" }));

    expect(screen.queryByText("Choose a category to continue.")).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Continue" })).toBeEnabled();
  });
});

/**
 * D-327 — a step may not wave through its own required fields.
 *
 * `canNext` ended in `step >= 4`, an unconditional pass, so title/start/end could all be skipped and
 * the wizard only objected on step 11 of 11. These walk the real control flow rather than asserting on
 * `canNext` directly: the bug was that Continue was clickable, and only a click proves it isn't.
 */
async function walkToDetails(typeName?: string) {
  for (let i = 0; i < 3; i++) await userEvent.click(screen.getByRole("button", { name: "Continue" }));
  await userEvent.click(screen.getByRole("radio", { name: "Hackathon" }));
  await userEvent.click(screen.getByRole("button", { name: "Continue" }));
  // Type is required only when the category HAS types; the default fixture has none.
  if (typeName) await userEvent.click(screen.getByRole("radio", { name: typeName }));
  await userEvent.click(screen.getByRole("button", { name: "Continue" }));
}

async function fillDetails(title: string) {
  await userEvent.type(screen.getByLabelText("Title"), title);
  await userEvent.type(screen.getByLabelText("Starts at"), "2026-03-01T10:00");
  await userEvent.type(screen.getByLabelText("Ends at"), "2026-03-01T18:00");
}

/// A Private product needs a Private Type to reach a category at all — `categoriesFor` offers only
/// categories that can actually produce one (D-326), so an empty `subcategories` list means the Private
/// branch has nothing to show. That is the fix working, not a fixture convenience.
const PRIVATE_TYPES = [
  { id: "t1", name: "Wedding", parent_id: "c1", product_class: "Private" },
] as never[];

describe("a step refuses its own required fields", () => {
  it("will not leave Details until the title and both times are set", async () => {
    renderWizard();
    await walkToDetails();

    expect(screen.getByText(/Step 6 of 11: Details\./)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Continue" })).toBeDisabled();
    expect(screen.getByText("Add a title of at least 2 characters.")).toBeInTheDocument();

    await userEvent.type(screen.getByLabelText("Title"), "Hack Day");
    expect(screen.getByText("Set when the event starts.")).toBeInTheDocument();

    await userEvent.type(screen.getByLabelText("Starts at"), "2026-03-01T10:00");
    expect(screen.getByText("Set when the event ends.")).toBeInTheDocument();

    await userEvent.type(screen.getByLabelText("Ends at"), "2026-03-01T18:00");
    expect(screen.getByRole("button", { name: "Continue" })).toBeEnabled();
  });

  it("refuses an end time that is not after the start", async () => {
    renderWizard();
    await walkToDetails();
    await userEvent.type(screen.getByLabelText("Title"), "Hack Day");
    await userEvent.type(screen.getByLabelText("Starts at"), "2026-03-01T18:00");
    await userEvent.type(screen.getByLabelText("Ends at"), "2026-03-01T10:00");

    // The server's own rule (`EndsAt must be after StartsAt`) and Flutter's `_basicsValid` both had
    // this; web checked only that the two were present and left the ordering to a 400 at submit.
    expect(screen.getByRole("button", { name: "Continue" })).toBeDisabled();
    expect(screen.getByText("The end time must be after the start time.")).toBeInTheDocument();
  });

  it("will not leave Pricing with an unbookable ticket", async () => {
    renderWizard();
    for (let i = 0; i < 2; i++) await userEvent.click(screen.getByRole("button", { name: "Continue" }));

    expect(screen.getByText(/Step 3 of 11: Pricing\./)).toBeInTheDocument();
    await userEvent.clear(screen.getByLabelText("Ticket name"));
    expect(screen.getByRole("button", { name: "Continue" })).toBeDisabled();
    expect(screen.getByText("Name the ticket people will book.")).toBeInTheDocument();

    await userEvent.type(screen.getByLabelText("Ticket name"), "General");
    await userEvent.clear(screen.getByLabelText("How many"));
    expect(screen.getByText("Set how many tickets are available (at least 1).")).toBeInTheDocument();
  });
});

describe("the last step lists what is still missing", () => {
  it("names each unmet requirement instead of refusing silently", async () => {
    renderWizard();
    await walkToDetails();
    await fillDetails("Hack Day");
    // Details -> Content -> Location -> Windows -> Eligibility -> Legal.
    for (let i = 0; i < 5; i++) await userEvent.click(screen.getByRole("button", { name: "Continue" }));

    // Consent is the one requirement still reachable at the end, because it is set ON the last step —
    // every other entry in `missingForSubmit` is now caught by the step that asks for it. The list
    // stays as the backstop it always was; this proves it still speaks.
    await userEvent.click(screen.getByRole("checkbox", { name: /Require registrants to accept/ }));

    expect(screen.getByRole("button", { name: /Create draft event/ })).toBeDisabled();
    expect(screen.getByText(/Still needed before this can be created:/)).toBeInTheDocument();
    expect(screen.getByText(/Write the statement registrants must accept/)).toBeInTheDocument();
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
    expect(screen.getByLabelText("Minimum age")).toBeInTheDocument();
    expect(screen.getByLabelText("Maximum teams")).toBeInTheDocument();
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
});
