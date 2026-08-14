import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";

vi.mock("@/lib/admin-actions", () => ({ reviewEventAction: vi.fn() }));
vi.mock("react-dom", async () => {
  const actual = await vi.importActual<typeof import("react-dom")>("react-dom");
  return { ...actual, useFormState: () => [null, "/noop"], useFormStatus: () => ({ pending: false }) };
});

import { ConfirmDecisionButton } from "@/components/admin/confirm-decision-button";

/**
 * Guards for Phase 30 — moderation.
 *
 * These are the highest-stakes controls in the product: approving publishes an event to everyone,
 * rejecting takes it away from an organiser who has usually spent days on it. Both fired on a single
 * click, from buttons sitting next to each other.
 */

describe("a moderation decision must be confirmed", () => {
  function Harness() {
    return (
      <form>
        <ConfirmDecisionButton
          name="action"
          value="reject"
          label="Reject"
          tone="reject"
          title="Reject this event?"
          description="The organiser is told it was rejected."
          confirmLabel="Reject"
        />
      </form>
    );
  }

  it("does not submit on the first click", async () => {
    render(<Harness />);
    const form = document.querySelector("form")!;
    const submitted = vi.fn();
    form.addEventListener("submit", (e) => { e.preventDefault(); submitted(); });

    await userEvent.click(screen.getByRole("button", { name: "Reject" }));
    expect(submitted).not.toHaveBeenCalled();
    expect(await screen.findByRole("dialog")).toBeInTheDocument();
  });

  it("submits the original payload once confirmed", async () => {
    render(<Harness />);
    const form = document.querySelector("form")!;
    const submitted = vi.fn();
    form.addEventListener("submit", (e) => { e.preventDefault(); submitted(); });

    await userEvent.click(screen.getByRole("button", { name: "Reject" }));
    const dialog = await screen.findByRole("dialog");
    await userEvent.click(within(dialog).getByRole("button", { name: "Reject" }));

    expect(submitted).toHaveBeenCalledTimes(1);
    // The hidden field is what the server action reads; the gate must not change the payload.
    expect(form.querySelector('input[name="action"]')).toHaveValue("reject");
  });

  it("keeps a 44px target", () => {
    render(<Harness />);
    expect(screen.getByRole("button", { name: "Reject" }).className).toMatch(/min-h-11/);
  });
});

describe("no moderation decision fires straight through", () => {
  it("routes every approve/reject submit through the confirmation", () => {
    for (const file of ["components/admin/event-review-form.tsx", "components/admin/review-actions.tsx"]) {
      const src = readFileSync(resolve(__dirname, "..", file), "utf8");
      // A bare `type="submit"` carrying an approve/reject action is the shape that fired directly.
      const bare = /<button[^>]*type="submit"[^>]*value="(publish|reject|approve_review|publish_approved)"/;
      expect(bare.test(src)).toBe(false);
      expect(src).toContain("ConfirmDecisionButton");
    }
  });
});

describe("the live feed must say when it is not live", () => {
  it("distinguishes a dead socket from a quiet gate", () => {
    /*
     * The workspace showed a "live" badge when connected and nothing at all when not, so an operator
     * watching an empty feed on the door could not tell "nobody has arrived in ten minutes" from
     * "the feed died ten minutes ago" — which is a decision they make at the gate.
     */
    const src = readFileSync(
      resolve(__dirname, "..", "components/admin/events-workspace/event-workspace-sheet.tsx"),
      "utf8"
    );
    expect(src).toContain("not live");
    // …and stays silent when no live feed was promised in the first place.
    expect(src).toContain("liveExpected");
  });

  it("keeps the realtime token behind the console's own staff gate", () => {
    // Every other route on this origin requires a platform role; this one asked only for a session.
    const src = readFileSync(resolve(__dirname, "..", "app/api/realtime-token/route.ts"), "utf8");
    // Assert the import, not the file: the route's comment names `currentSession` to explain what
    // it replaced, and a whole-file match would flag that explanation as the defect.
    expect(src).toMatch(/import \{ requireStaffSession \} from "@\/lib\/session";/);
    expect(src).not.toMatch(/import \{ currentSession \}/);
    expect(src).toContain("no-store");
  });
});
