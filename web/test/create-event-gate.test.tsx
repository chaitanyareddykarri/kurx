import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { CreateEventGate } from "@/components/host/create-event-gate";
import type { Category } from "@/lib/api";

/**
 * D-305 — the Create-Event gate. D-343 — the two verification tiers.
 *
 * Three things are worth pinning, and none is visible in a diff:
 *
 *   1. **The form cannot be reached without passing the gate.** The whole point of D-305 is that
 *      eligibility happens *before* eleven steps of work. A refactor that renders the wizard eagerly
 *      would look fine on screen — the gate would still paint — and silently restore the old behaviour.
 *   2. **A free public event must not be asked for bank details.** That was the D-307 behaviour and
 *      D-343 removed it: bank ownership answers "whose account receives money", which a free event
 *      never asks. A change that re-merged the tiers would make the flow work while demanding four
 *      submissions nobody needs.
 *   3. **Paid stays behind the financial tier.** Entering as Free and switching to Paid must not be a
 *      way around it.
 *
 * The Type-filtering rule is tested separately in `create-event-types.test.ts` against the predicate
 * itself, because it is a data rule rather than a rendering one.
 */

vi.mock("@/lib/api", () => ({ api: { get: vi.fn(), post: vi.fn(), patch: vi.fn(), delete: vi.fn() } }));

// The wizard is the thing that must NOT appear until the gate is satisfied. Stubbed so the assertion is
// about reachability and about what the gate hands over, not about the wizard's own rendering.
vi.mock("@/components/host/create-event-wizard", () => ({
  CreateEventWizard: ({ product, initialPricing }: { product: string; initialPricing?: string }) => (
    <div data-testid="wizard">wizard:{product}:{initialPricing}</div>
  )
}));

const categories: Category[] = [];
const subcategories: Category[] = [];

/// D-350 — Paid also needs a VERIFIED organization to represent, because settlement is keyed on one.
/// Two fixtures so the pending case (a legitimate row in this list) is testable against the verified one.
const VERIFIED_REP = {
  organization_id: "org-verified", name: "Verified Fest Co", slug: "verified-fest-co",
  logo_key: null, authority: "owner", is_verified: true
};
const PENDING_REP = {
  organization_id: "org-pending", name: "Pending Institute", slug: "pending-institute",
  logo_key: null, authority: "representative", is_verified: false
};

function renderGate(overrides: Partial<React.ComponentProps<typeof CreateEventGate>> = {}) {
  return render(
    <CreateEventGate
      representations={[]}
      canHostPaid={false}
      categories={categories}
      subcategories={subcategories}
      presets={[]}
      identityVerified={false}
      panVerified={false}
      bankVerified={false}
      pennyDropPassed={false}
      bankNameMatched={false}
      canCreatePublicEvent={false}
      canCreatePrivateEvent
      requiresRepresentation
      representativeRoles={["Principal", "Head of Department", "Other"]}
      {...overrides}
    />
  );
}

const continueButton = () => screen.getByRole("button", { name: /continue/i });

/** Product step → pricing step. */
async function chooseProduct(user: ReturnType<typeof userEvent.setup>, which: RegExp) {
  await user.click(screen.getByRole("button", { name: which }));
  await user.click(continueButton());
}

describe("CreateEventGate", () => {
  it("does not render the creation form until the gate has been passed", () => {
    renderGate();
    expect(screen.queryByTestId("wizard")).not.toBeInTheDocument();
    expect(screen.getByText("Who is this event for?")).toBeInTheDocument();
  });

  it("refuses to leave the product step until Public or Private is chosen", async () => {
    renderGate();
    expect(continueButton()).toBeDisabled();
    expect(screen.queryByTestId("wizard")).not.toBeInTheDocument();
  });

  // ── D-343 · the identity tier gates Public ────────────────────────────────

  it("asks a free public event for identity only, never for bank details", async () => {
    const user = userEvent.setup();
    renderGate({ canCreatePublicEvent: true, identityVerified: true });
    await chooseProduct(user, /public/i);

    // Free is the default answer, so this is the state the person lands in.
    expect(screen.getByText(/government id or pan approved/i)).toBeInTheDocument();
    expect(screen.queryByText(/penny drop/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/bank account approved/i)).not.toBeInTheDocument();
    // Exact, not a regex: /pan approved$/ also matches "Government ID or PAN approved" — the row that
    // is SUPPOSED to be there. The standalone PAN row is the financial-tier one.
    expect(screen.queryByText("PAN approved")).not.toBeInTheDocument();
  });

  it("blocks Public when identity is not verified", async () => {
    const user = userEvent.setup();
    renderGate({ canCreatePublicEvent: false });
    await chooseProduct(user, /public/i);

    expect(continueButton()).toBeDisabled();
    expect(screen.queryByTestId("wizard")).not.toBeInTheDocument();
  });

  it("opens the form for a free public event once identity has passed", async () => {
    const user = userEvent.setup();
    renderGate({ canCreatePublicEvent: true, identityVerified: true });
    await chooseProduct(user, /public/i);
    await user.click(continueButton());

    // Identity alone, no bank — this is the combination D-307 used to refuse.
    expect(screen.getByTestId("wizard")).toHaveTextContent("wizard:Public:free");
  });

  // ── D-343 · the financial tier gates Paid ─────────────────────────────────

  it("names the bank-ownership links only once Paid is chosen", async () => {
    const user = userEvent.setup();
    renderGate({ canCreatePublicEvent: true, identityVerified: true });
    await chooseProduct(user, /public/i);
    await user.click(screen.getByRole("button", { name: /paid/i }));

    // Penny drop and name match sit INSIDE bankVerified; they are named, not extra predicates.
    expect(screen.getByText(/bank ownership confirmed/i)).toBeInTheDocument();
    expect(screen.getByText(/account holder name matches your pan/i)).toBeInTheDocument();
  });

  it("does not let Paid past the gate without the financial tier, even when Public is open", async () => {
    const user = userEvent.setup();
    // The exact D-343 shape: identity cleared, bank not. Public is allowed; selling is not.
    renderGate({ canCreatePublicEvent: true, identityVerified: true, canHostPaid: false });
    await chooseProduct(user, /public/i);
    await user.click(screen.getByRole("button", { name: /paid/i }));

    expect(continueButton()).toBeDisabled();
    expect(screen.queryByTestId("wizard")).not.toBeInTheDocument();
  });

  it("opens the form for Paid once the financial tier has passed", async () => {
    const user = userEvent.setup();
    renderGate({
      canCreatePublicEvent: true, canHostPaid: true,
      identityVerified: true, panVerified: true, bankVerified: true,
      pennyDropPassed: true, bankNameMatched: true,
      // D-350 — the personal proofs alone are no longer enough; settlement needs an organization.
      representations: [VERIFIED_REP]
    });
    await chooseProduct(user, /public/i);
    await user.click(screen.getByRole("button", { name: /paid/i }));
    await user.click(continueButton());

    // The answer must reach the wizard, or the person picks free/paid twice.
    expect(screen.getByTestId("wizard")).toHaveTextContent("wizard:Public:paid");
  });

  // ── D-350 · Paid also needs a verified organization to represent ──────────

  it("blocks Paid when the person is fully verified but represents no organization", async () => {
    const user = userEvent.setup();
    renderGate({
      canCreatePublicEvent: true, canHostPaid: true,
      identityVerified: true, panVerified: true, bankVerified: true,
      pennyDropPassed: true, bankNameMatched: true,
      representations: []          // every personal proof passed; nothing to settle into
    });
    await chooseProduct(user, /public/i);
    await user.click(screen.getByRole("button", { name: /paid/i }));

    expect(continueButton()).toBeDisabled();
    expect(screen.getByText(/can't sell as yourself/i)).toBeInTheDocument();
    expect(screen.queryByTestId("wizard")).not.toBeInTheDocument();
  });

  it("does not count a PENDING organization towards the paid requirement", async () => {
    const user = userEvent.setup();
    renderGate({
      canCreatePublicEvent: true, canHostPaid: true,
      identityVerified: true, panVerified: true, bankVerified: true,
      pennyDropPassed: true, bankNameMatched: true,
      // A staged representation request is a real row in this list and a legitimate choice for a FREE
      // event — its events simply cannot publish until approval. It is never enough for a paid one.
      representations: [PENDING_REP]
    });
    await chooseProduct(user, /public/i);
    await user.click(screen.getByRole("button", { name: /paid/i }));

    expect(continueButton()).toBeDisabled();
  });

  it("never asks a FREE public event for an organization", async () => {
    const user = userEvent.setup();
    renderGate({ canCreatePublicEvent: true, identityVerified: true, representations: [] });
    await chooseProduct(user, /public/i);

    // Free is the default. Representing nobody is the whole point of a self-hosted free event.
    expect(screen.queryByText(/verified organization to represent/i)).not.toBeInTheDocument();
    expect(continueButton()).toBeEnabled();
  });

  // ── Private ───────────────────────────────────────────────────────────────

  it("lets a completely unverified account create a PRIVATE event", async () => {
    const user = userEvent.setup();
    renderGate({ canCreatePublicEvent: false, canCreatePrivateEvent: true });
    await chooseProduct(user, /private/i);
    await user.click(continueButton());

    expect(screen.getByTestId("wizard")).toHaveTextContent("wizard:Private:free");
  });

  it("never asks a Private host for identity or bank details", async () => {
    const user = userEvent.setup();
    renderGate({ canCreatePublicEvent: false });
    await chooseProduct(user, /private/i);

    expect(screen.queryByText(/penny drop/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/government id or pan approved/i)).not.toBeInTheDocument();
    expect(screen.getByText(/nothing to verify/i)).toBeInTheDocument();
  });

  it("cannot sell tickets on a Private event", async () => {
    const user = userEvent.setup();
    renderGate({ canCreatePrivateEvent: true, canHostPaid: true });
    await chooseProduct(user, /private/i);

    // Disabled rather than absent: the person asked, and a missing card answers nothing.
    expect(screen.getByRole("button", { name: /paid/i })).toBeDisabled();
  });

  it("resets a chosen Paid back to Free when the product switches to Private", async () => {
    const user = userEvent.setup();
    renderGate({
      canCreatePublicEvent: true, canHostPaid: true,
      identityVerified: true, panVerified: true, bankVerified: true
    });
    await chooseProduct(user, /public/i);
    await user.click(screen.getByRole("button", { name: /paid/i }));

    // Back, switch to Private, forward again — a stale "paid" would reach a product that cannot honour
    // it and the event would be created unbookable.
    await user.click(screen.getByRole("button", { name: /back/i }));
    await user.click(screen.getByRole("button", { name: /private/i }));
    await user.click(continueButton());
    await user.click(continueButton());

    expect(screen.getByTestId("wizard")).toHaveTextContent("wizard:Private:free");
  });

  // ── D-323 · the bypass must not be restated as a verification ─────────────

  it("does not claim a verification that never happened when the gate is merely bypassed", async () => {
    const user = userEvent.setup();
    // The capability is open while the proofs stay off file. This combination cannot occur in
    // Production (startup refuses the flag there), so it means exactly one thing.
    renderGate({ canCreatePublicEvent: true, identityVerified: false });
    await chooseProduct(user, /public/i);

    expect(screen.getByText(/enabled without verification/i)).toBeInTheDocument();
    expect(screen.getByText(/nothing about your identity has been confirmed/i)).toBeInTheDocument();
  });

  it("says nothing about a bypass when the identity is genuinely on file", async () => {
    const user = userEvent.setup();
    renderGate({ canCreatePublicEvent: true, identityVerified: true });
    await chooseProduct(user, /public/i);

    expect(screen.queryByText(/enabled without verification/i)).not.toBeInTheDocument();
  });

  it("can go back from the pricing step without losing the gate", async () => {
    const user = userEvent.setup();
    renderGate({ canCreatePublicEvent: true });
    await chooseProduct(user, /public/i);
    await user.click(screen.getByRole("button", { name: /back/i }));

    expect(screen.getByText("Who is this event for?")).toBeInTheDocument();
  });
});
