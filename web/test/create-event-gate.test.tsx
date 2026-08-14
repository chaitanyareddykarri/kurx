import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { CreateEventGate } from "@/components/host/create-event-gate";
import type { Category } from "@/lib/api";

/**
 * D-305 — the Create-Event gate.
 *
 * Two things are worth pinning, and neither is visible in a diff:
 *
 *   1. **The form cannot be reached without passing the gate.** The whole point of D-305 is that
 *      eligibility and the Public/Private decision happen *before* eleven steps of work. A refactor
 *      that renders the wizard eagerly would look fine on screen — the gate would still paint — and
 *      silently restore the old behaviour.
 *   2. **The gate never blocks a free event.** `TrustService` sets `canOrganizeFree = true` for any
 *      account. A well-meaning change that gates creation on identity would invent a requirement the
 *      platform does not have, and would lock out every unverified organiser.
 *
 * The Type-filtering rule is tested separately in `create-event-types.test.ts` against the predicate
 * itself, because it is a data rule rather than a rendering one.
 */

vi.mock("@/lib/api", () => ({ api: { get: vi.fn(), post: vi.fn(), patch: vi.fn(), delete: vi.fn() } }));

// The wizard is the thing that must NOT appear until the gate is satisfied. Stubbed so the assertion is
// about reachability, not about the wizard's own rendering.
vi.mock("@/components/host/create-event-wizard", () => ({
  CreateEventWizard: ({ product }: { product: string }) => (
    <div data-testid="wizard">wizard:{product}</div>
  )
}));

const categories: Category[] = [];
const subcategories: Category[] = [];

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
      hostName="Audit Organiser"
      {...overrides}
    />
  );
}

describe("CreateEventGate", () => {
  it("does not render the creation form until the gate has been passed", () => {
    renderGate();
    expect(screen.queryByTestId("wizard")).not.toBeInTheDocument();
    expect(screen.getByText("Before you start")).toBeInTheDocument();
  });

  it("lets an unverified account reach the product choice", async () => {
    const user = userEvent.setup();
    renderGate({ canHostPaid: false });

    // Step 1 is informational. The refusal, when there is one, belongs to the product choice — nobody
    // should be stopped before being told which products they can make.
    await user.click(screen.getByRole("button", { name: /continue/i }));
    expect(screen.getByText("What kind of event is this?")).toBeInTheDocument();
  });

  // D-307: Public requires the full set, free or paid. This block replaces an assertion that used to
  // read "an unverified account can create a free event" — that WAS the rule and is now deliberately
  // false, because a free public event still reaches every user through discovery.

  it("blocks Public when the account is not verified, and lists only what is missing", async () => {
    const user = userEvent.setup();
    renderGate({ canCreatePublicEvent: false, identityVerified: true, panVerified: false });
    await user.click(screen.getByRole("button", { name: /continue/i }));
    await user.click(screen.getByRole("button", { name: /public/i }));

    expect(screen.getByText(/verify your identity to host public events/i)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /continue/i })).toBeDisabled();
    expect(screen.queryByTestId("wizard")).not.toBeInTheDocument();
  });

  it("names the bank-ownership links so the person knows what is outstanding", async () => {
    const user = userEvent.setup();
    renderGate({ canCreatePublicEvent: false });
    await user.click(screen.getByRole("button", { name: /continue/i }));
    await user.click(screen.getByRole("button", { name: /public/i }));

    // Penny drop and name match sit INSIDE bankVerified; they are named, not extra predicates.
    expect(screen.getByText(/bank ownership confirmed/i)).toBeInTheDocument();
    expect(screen.getByText(/account holder name matches your pan/i)).toBeInTheDocument();
  });

  it("opens the form for Public once every requirement has passed", async () => {
    const user = userEvent.setup();
    renderGate({ canCreatePublicEvent: true });
    await user.click(screen.getByRole("button", { name: /continue/i }));
    await user.click(screen.getByRole("button", { name: /public/i }));
    await user.click(screen.getByRole("button", { name: /continue/i }));

    expect(screen.getByTestId("wizard")).toHaveTextContent("wizard:Public");
  });

  it("lets a completely unverified account create a PRIVATE event", async () => {
    const user = userEvent.setup();
    renderGate({ canCreatePublicEvent: false, canCreatePrivateEvent: true });
    await user.click(screen.getByRole("button", { name: /continue/i }));
    await user.click(screen.getByRole("button", { name: /private/i }));
    await user.click(screen.getByRole("button", { name: /continue/i }));

    expect(screen.getByTestId("wizard")).toHaveTextContent("wizard:Private");
  });

  it("never asks a Private host for penny drop or bank details", async () => {
    const user = userEvent.setup();
    renderGate({ canCreatePublicEvent: false });
    await user.click(screen.getByRole("button", { name: /continue/i }));
    await user.click(screen.getByRole("button", { name: /private/i }));

    // A private event cannot take payment, so a bank requirement would be proof of something that
    // can never happen.
    expect(screen.queryByText(/penny drop/i)).not.toBeInTheDocument();
    expect(screen.getByText(/private events need no financial verification/i)).toBeInTheDocument();
  });

  it("states that paid hosting is what needs verification, not creation", () => {
    renderGate({ canHostPaid: false });
    expect(screen.getByText(/you don't need any of this to create a free event/i)).toBeInTheDocument();
  });

  it("shows paid hosting as available once the account is paid-capable", () => {
    // The proofs must be passed too: capability WITH the facts on file is the genuinely-verified case,
    // and it is the only one allowed to say "verified" (D-323).
    renderGate({ canHostPaid: true, identityVerified: true, panVerified: true, bankVerified: true });
    expect(screen.getByText(/identity, pan and bank account are verified/i)).toBeInTheDocument();
    // The "what's still needed" panel belongs only to the not-yet-capable case.
    expect(screen.queryByText(/complete verification/i)).not.toBeInTheDocument();
  });

  it("does not claim a verification that never happened when the gate is merely bypassed", () => {
    // D-323 — `IDENTITY_VERIFICATION_BYPASS` opens the capability while the proofs stay off file. This
    // combination cannot occur in Production (startup refuses the flag there), so it means exactly one
    // thing, and saying "verified" here would forge the fact the bypass is explicitly forbidden from
    // forging. The panel is still hidden — the capability is real — but the wording is honest.
    renderGate({ canHostPaid: true, identityVerified: false, panVerified: false, bankVerified: false });
    expect(screen.queryByText(/identity, pan and bank account are verified/i)).not.toBeInTheDocument();
    expect(screen.getByText(/enabled without verification/i)).toBeInTheDocument();
    expect(screen.getByText(/nothing about your identity has been confirmed/i)).toBeInTheDocument();
  });

  it("refuses to continue past the product step until Public or Private is chosen", async () => {
    const user = userEvent.setup();
    renderGate();
    await user.click(screen.getByRole("button", { name: /continue/i }));

    const advance = screen.getByRole("button", { name: /continue/i });
    expect(advance).toBeDisabled();
    expect(screen.queryByTestId("wizard")).not.toBeInTheDocument();
  });

  it("opens the form only after a product is chosen, and hands it that product", async () => {
    const user = userEvent.setup();
    renderGate({ canCreatePublicEvent: true });
    await user.click(screen.getByRole("button", { name: /continue/i }));
    await user.click(screen.getByRole("button", { name: /private/i }));
    await user.click(screen.getByRole("button", { name: /continue/i }));

    // The product must reach the wizard: it is what filters the Types, and a wizard that received the
    // wrong one would quietly offer the wrong catalogue.
    expect(screen.getByTestId("wizard")).toHaveTextContent("wizard:Private");
  });

  it("can go back from the product step without losing the gate", async () => {
    const user = userEvent.setup();
    renderGate();
    await user.click(screen.getByRole("button", { name: /continue/i }));
    await user.click(screen.getByRole("button", { name: /back/i }));
    expect(screen.getByText("Before you start")).toBeInTheDocument();
  });
});
