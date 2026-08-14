import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";

let pending = false;
vi.mock("react-dom", async () => {
  // react-dom 18's build resolved under vitest does not export `useFormStatus` at runtime, so the
  // rest of this suite mocks it too. That puts React's own hook out of scope here; what is in scope
  // is the contract this component holds up against it, which is where the defect was.
  const actual = await vi.importActual<typeof import("react-dom")>("react-dom");
  return { ...actual, useFormStatus: () => ({ pending }) };
});

import { ConfirmSubmitButton } from "@/components/host/confirm-submit-button";

/**
 * Phase 42 — a confirmed destructive action has to look like it started.
 *
 * `ConfirmSubmitButton` submits its owning form natively, so there is no client-side promise to hang
 * a spinner on, and the trigger looked identical before and during the delete. On a slow connection
 * that reads as a click that never registered, and the obvious response is to click again — which
 * reopens the dialog and deletes a second thing.
 */
describe("a destructive submit reports that it is running", () => {
  beforeEach(() => { pending = false; });

  function subject() {
    return (
      <ConfirmSubmitButton
        label="Delete"
        title="Delete the Early Bird ticket type?"
        description="Anyone holding one keeps it. No new ones can be sold."
      />
    );
  }

  it("cannot be triggered a second time while the action is in flight", async () => {
    pending = true;
    render(subject());
    const trigger = screen.getByRole("button", { name: /Delete/ });
    expect(trigger).toBeDisabled();

    // Disabled is the load-bearing part: the dialog is what makes a second click destructive.
    await userEvent.click(trigger);
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("says so in words, not only by going grey", async () => {
    pending = true;
    render(subject());
    // Colour alone is the S1-1 defect class. The label carries it too.
    expect(screen.getByRole("button", { name: /Delete…/ })).toBeInTheDocument();
  });

  it("is fully operable when nothing is in flight", async () => {
    render(subject());
    const trigger = screen.getByRole("button", { name: "Delete" });
    expect(trigger).toBeEnabled();
    await userEvent.click(trigger);
    expect(screen.getByRole("dialog")).toBeInTheDocument();
  });
});
