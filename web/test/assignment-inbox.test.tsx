import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi, beforeEach } from "vitest";

const respond = vi.fn();
const refresh = vi.fn();

vi.mock("next/navigation", () => ({ useRouter: () => ({ refresh }) }));
vi.mock("@/lib/assignment-actions", () => ({
  respondToAssignmentAction: (...args: unknown[]) => respond(...args)
}));

import { AssignmentRow } from "@/components/assignments/assignment-row";
import type { EventAssignment } from "@/lib/api";

/**
 * D-319 — the invitee's side of event staffing.
 *
 * `RespondAsync` refuses every actor except the invitee, so this row is the only path an assignment has
 * from `Invited` to `Accepted`, and the §14.2 go-live gate waits on that. These guard the two things
 * that made the missing screen a bug rather than a gap: the invite has to be identifiable, and a
 * self-represented event must not name the internal representation row (D-268).
 */
function make(over: Partial<EventAssignment> = {}): EventAssignment {
  return {
    id: "a1", event_id: "e1", org_id: "o1", user_id: "u1",
    role: "Judge", custom_role: null, status: "invited", show_on_profile: true, notes: null,
    created_at: "2026-08-01T10:00:00Z",
    assignee_name: "Asha", assignee_username: null, assignee_avatar_key: null,
    event_title: "Winter Hack", event_slug: "winter-hack",
    event_starts_at: "2026-09-01T10:00:00Z", representing_org_name: "Acme Institute",
    ...over
  };
}

beforeEach(() => { respond.mockReset(); refresh.mockReset(); respond.mockResolvedValue({ ok: true }); });

describe("AssignmentRow", () => {
  it("names the event, the role and the host so the invite is answerable", () => {
    render(<AssignmentRow assignment={make()} />);
    // A row reading "Judge" beside a bare uuid is not something anyone can decide on.
    expect(screen.getByRole("link", { name: "Winter Hack" })).toHaveAttribute("href", "/e/winter-hack");
    expect(screen.getByText("Judge")).toBeInTheDocument();
    expect(screen.getByText(/Acme Institute/)).toBeInTheDocument();
  });

  it("offers accept and decline only while the invite is unanswered", async () => {
    const { rerender } = render(<AssignmentRow assignment={make()} />);
    expect(screen.getByRole("button", { name: "Accept" })).toBeInTheDocument();

    rerender(<AssignmentRow assignment={make({ status: "accepted" })} />);
    expect(screen.queryByRole("button", { name: "Accept" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Decline" })).not.toBeInTheDocument();
  });

  it("sends accept and decline as distinct answers", async () => {
    const user = userEvent.setup();
    render(<AssignmentRow assignment={make()} />);

    await user.click(screen.getByRole("button", { name: "Accept" }));
    expect(respond).toHaveBeenCalledWith("a1", true);

    respond.mockClear();
    await user.click(screen.getByRole("button", { name: "Decline" }));
    expect(respond).toHaveBeenCalledWith("a1", false);
  });

  it("shows the refusal instead of discarding it", async () => {
    // Answering twice is ordinary — two tabs, a back button — and has to read as already-settled.
    respond.mockResolvedValue({ error: "You've already answered this invitation." });
    const user = userEvent.setup();
    render(<AssignmentRow assignment={make()} />);

    await user.click(screen.getByRole("button", { name: "Accept" }));
    expect(await screen.findByRole("alert")).toHaveTextContent("already answered");
    expect(refresh).not.toHaveBeenCalled();
  });

  it("names no organization for a self-represented event", () => {
    // D-268 — the self-representation row is named after the person. Rendering a fallback label here
    // would reintroduce the "personal organization" concept the domain model does not have.
    render(<AssignmentRow assignment={make({ representing_org_name: null })} />);
    expect(screen.queryByText(/Acme Institute/)).not.toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Winter Hack" })).toBeInTheDocument();
  });

  it("still lists an invite whose event is gone, so it can be declined", () => {
    render(<AssignmentRow assignment={make({ event_slug: null, event_title: "(event unavailable)" })} />);
    expect(screen.queryByRole("link")).not.toBeInTheDocument();
    expect(screen.getByText("(event unavailable)")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Accept" })).toBeInTheDocument();
  });
});
