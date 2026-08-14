import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";

import { Alert, Banner, NotFoundState, OfflineState, PermissionDeniedState, ToastProvider, useToast } from "@kurx/ui";

/**
 * Guards on the feedback contract (`docs/ui-ux/accessibility-foundation.md` §3.3).
 *
 * Audit S1-4: the toast had no live region and no role, so the repo-mandated
 * feedback channel announced nothing at all — an admin bulk action reported its
 * outcome to sighted users only — and auto-dismissed at a fixed 3.2s with no
 * dismiss control (WCAG 2.2.1).
 */

function Harness({ tone, message }: { tone?: "success" | "error" | "info" | "warning"; message: string }) {
  const toast = useToast();
  return (
    <button type="button" onClick={() => toast(message, tone)}>
      fire
    </button>
  );
}

function renderToast(props: { tone?: "success" | "error" | "info" | "warning"; message: string }) {
  return render(
    <ToastProvider>
      <Harness {...props} />
    </ToastProvider>
  );
}

describe("Toast — the S1-4 contract", () => {
  it("keeps a polite live region mounted before any message exists", async () => {
    // A region inserted at the same moment as its first message is not announced
    // by most screen readers; it has to already be there for the mutation to be seen.
    renderToast({ message: "Saved" });
    const region = screen.getByRole("status");
    expect(region).toHaveAttribute("aria-live", "polite");
  });

  it("announces the message through that region", async () => {
    renderToast({ message: "Attendee removed" });
    await userEvent.click(screen.getByRole("button", { name: "fire" }));
    expect(screen.getByRole("status")).toHaveTextContent("Attendee removed");
  });

  it("carries tone as text, not colour alone", async () => {
    renderToast({ message: "Upload failed", tone: "error" });
    await userEvent.click(screen.getByRole("button", { name: "fire" }));
    expect(screen.getByRole("status")).toHaveTextContent(/Error:/);
  });

  it("can always be dismissed by hand", async () => {
    renderToast({ message: "Copied" });
    await userEvent.click(screen.getByRole("button", { name: "fire" }));
    expect(screen.getByRole("status")).toHaveTextContent("Copied");
    await userEvent.click(screen.getByRole("button", { name: "Dismiss" }));
    // waitFor, not a bare assertion: AnimatePresence keeps the node mounted for
    // its exit transition, so the removal is asynchronous by design.
    await waitFor(() => expect(screen.queryByRole("button", { name: "Dismiss" })).not.toBeInTheDocument());
    expect(screen.getByRole("status")).not.toHaveTextContent("Copied");
  });

  it("gives concurrent toasts distinct identities", async () => {
    // Keys were previously Date.now() + Math.random(); two pushed in one tick
    // could collide and one would be dropped.
    renderToast({ message: "One" });
    const fire = screen.getByRole("button", { name: "fire" });
    await userEvent.click(fire);
    await userEvent.click(fire);
    expect(screen.getAllByRole("button", { name: "Dismiss" })).toHaveLength(2);
  });
});

describe("Alert", () => {
  it("interrupts only when it is a consequence of a user action", () => {
    const { rerender } = render(<Alert tone="danger">Could not save</Alert>);
    // Static notices are plain regions — interrupting to announce page furniture
    // is worse than saying nothing.
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();

    rerender(
      <Alert tone="danger" live>
        Could not save
      </Alert>
    );
    expect(screen.getByRole("alert")).toHaveTextContent("Could not save");
  });

  it("carries tone as hidden text as well as an icon", () => {
    render(<Alert tone="warning">Payouts are delayed</Alert>);
    expect(screen.getByText(/Warning:/)).toBeInTheDocument();
  });
});

describe("Banner", () => {
  it("never interrupts, because it persists until its condition clears", () => {
    render(<Banner tone="warning">Verification pending</Banner>);
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
    expect(screen.getByText(/Verification pending/)).toBeInTheDocument();
  });
});

describe("whole-region states", () => {
  it("distinguishes denied from missing, and says what to do next", () => {
    render(<PermissionDeniedState />);
    expect(screen.getByRole("heading", { name: /don't have access/i })).toBeInTheDocument();
    expect(screen.getByText(/Ask the event's host/i)).toBeInTheDocument();
  });

  it("offers a next step on not-found", () => {
    render(<NotFoundState />);
    expect(screen.getByRole("heading", { name: /couldn't find that/i })).toBeInTheDocument();
    expect(screen.getByText(/may have been removed/i)).toBeInTheDocument();
  });

  it("tells an offline user what still works rather than inviting a pointless retry", () => {
    render(<OfflineState />);
    expect(screen.getByRole("alert")).toBeInTheDocument();
    expect(screen.getByText(/tickets and saved events are still available/i)).toBeInTheDocument();
  });
});
