import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";

vi.mock("next/navigation", () => ({ useRouter: () => ({ push: vi.fn(), refresh: vi.fn() }) }));
vi.mock("@/lib/api", () => ({ apiErrorMessage: () => "failed", apiErrorStatus: () => 400 }));
vi.mock("@/lib/session", () => ({ saveSession: vi.fn() }));
vi.mock("@/lib/auth-api", () => ({
  startPasswordReset: vi.fn(async () => undefined),
  completePasswordReset: vi.fn(async () => ({
    access_token: "a",
    refresh_token: "r"
  }))
}));

import { completePasswordReset, startPasswordReset } from "@/lib/auth-api";
import { ResetPasswordPanel } from "@/components/auth/reset-password-panel";

/**
 * The recovery code is factor 2 only when one is SUPPLIED.
 *
 * `PasswordResetService` picks the factor that is present — a recovery code when the field carries one,
 * otherwise `stepUp.StatusAsync(...).Satisfied`. This panel used to require the code to submit at all,
 * a rule the server never had. Because codes are only ever minted by an explicit, step-up-gated call
 * (`POST /v1/auth/recovery-codes`), most accounts hold none — so the one screen built for a locked-out
 * user refused nearly all of them before a request was ever sent.
 *
 * Mobile carries the twin of this guard in `password_test.dart`. Web had no reset test at all, which is
 * why the local rule survived here unnoticed; without this it can come back the same way.
 */

const VALID = "correct-horse-battery";

async function reachResetStep(user: ReturnType<typeof userEvent.setup>) {
  render(<ResetPasswordPanel />);
  await user.type(screen.getByLabelText("Phone, email, or username"), "9652455883");
  await user.click(screen.getByRole("button", { name: "Send code" }));
  await waitFor(() => expect(startPasswordReset).toHaveBeenCalled());
}

describe("password reset — factor 2 is the server's call", () => {
  beforeEach(() => vi.clearAllMocks());

  it("submits with no recovery code, carrying an empty one rather than blocking", async () => {
    const user = userEvent.setup();
    await reachResetStep(user);

    await user.type(screen.getByLabelText("Code from your phone"), "123456");
    await user.type(screen.getByLabelText("New password"), VALID);
    await user.type(screen.getByLabelText("Confirm new password"), VALID);

    const submit = screen.getByRole("button", { name: "Reset password" });
    expect(submit).toBeEnabled();

    await user.click(submit);
    await waitFor(() => expect(completePasswordReset).toHaveBeenCalledWith(
      "+919652455883", "123456", VALID, ""
    ));
  });

  it("still forwards a recovery code when one is given", async () => {
    const user = userEvent.setup();
    await reachResetStep(user);

    await user.type(screen.getByLabelText("Code from your phone"), "123456");
    await user.type(screen.getByLabelText(/Recovery code/), "aaaa1111-bbbb2222");
    await user.type(screen.getByLabelText("New password"), VALID);
    await user.type(screen.getByLabelText("Confirm new password"), VALID);
    await user.click(screen.getByRole("button", { name: "Reset password" }));

    await waitFor(() => expect(completePasswordReset).toHaveBeenCalledWith(
      "+919652455883", "123456", VALID, "aaaa1111-bbbb2222"
    ));
  });

  it("names the recovery field optional, so nobody goes looking for a code they never had", async () => {
    const user = userEvent.setup();
    await reachResetStep(user);
    expect(screen.getByLabelText(/Recovery code \(optional\)/)).toBeInTheDocument();
  });

  it("still refuses an OTP with no password — factor 1 alone is never a submit", async () => {
    const user = userEvent.setup();
    await reachResetStep(user);

    await user.type(screen.getByLabelText("Code from your phone"), "123456");
    expect(screen.getByRole("button", { name: "Reset password" })).toBeDisabled();
    expect(completePasswordReset).not.toHaveBeenCalled();
  });
});
