import { render, screen, waitFor } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";

vi.mock("next/navigation", () => ({ useRouter: () => ({ push: vi.fn(), refresh: vi.fn() }) }));
vi.mock("@/lib/api", () => ({ getMe: vi.fn(), requestOtp: vi.fn(), verifyOtp: vi.fn() }));
vi.mock("@/lib/auth-api", () => ({ loginPassword: vi.fn() }));
vi.mock("@/lib/session", () => ({ saveSession: vi.fn() }));

import { Field, Input } from "@kurx/ui";

/**
 * Guards on the OTP entry contract (Phase 16).
 *
 * The panel itself pulls in the whole session/auth module graph, so these assert
 * the two invariants at the level they actually live — the shared Field/Input —
 * rather than mounting a component that would need six more mocks to render.
 */

describe("one-time-code field", () => {
  it("declares one-time-code autofill", () => {
    // iOS and Android only offer to autofill an SMS code when the field says so.
    // Without it every user retypes the code by hand on the highest-friction
    // step of signing in — this was missing.
    render(
      <Field label="One-time code">
        <Input inputMode="numeric" autoComplete="one-time-code" maxLength={6} />
      </Field>
    );
    const input = screen.getByLabelText("One-time code");
    expect(input).toHaveAttribute("autocomplete", "one-time-code");
    expect(input).toHaveAttribute("inputmode", "numeric");
  });

  it("has a real label, not a placeholder standing in for one", () => {
    render(
      <Field label="One-time code" helper="We sent a 6-digit code to your phone.">
        <Input placeholder="6-digit code" />
      </Field>
    );
    // The previous field carried only aria-label and a placeholder.
    const input = screen.getByLabelText("One-time code");
    expect(input).toHaveAccessibleDescription("We sent a 6-digit code to your phone.");
  });

  it("forwards a ref so focus can be moved to it", async () => {
    // A field that appears mid-flow must be focusable programmatically; without
    // ref forwarding the OTP panel could not move focus when the code step
    // replaced the phone step.
    function Harness() {
      return (
        <Field label="One-time code">
          <Input ref={(el) => el?.focus()} />
        </Field>
      );
    }
    render(<Harness />);
    await waitFor(() => expect(screen.getByLabelText("One-time code")).toHaveFocus());
  });
});
