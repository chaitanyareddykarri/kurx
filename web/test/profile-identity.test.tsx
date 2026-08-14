import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";

const socialActions = vi.hoisted(() => ({
  searchPeopleAction: vi.fn(),
  respondAllyRequestAction: vi.fn(),
  revokeAllyAction: vi.fn(),
  requestAllyAction: vi.fn(),
  getMutualDetailAction: vi.fn(async () => ({ relationships: [], shared_events: [], shared_orgs: [] })),
  toggleFollowAction: vi.fn(),
  getMySuggestionsAction: vi.fn(async () => []),
}));

vi.mock("@/lib/social-actions", () => socialActions);
vi.mock("next/navigation", () => ({ useRouter: () => ({ push: vi.fn() }) }));

// `lib/api` calls React's `cache()` at module scope, which only exists under the react-server export
// condition — importing it from jsdom throws "cache is not a function" and vitest reports the whole
// suite as collecting zero tests rather than as a failure. Only `apiErrorMessage` is reached at
// runtime from these components; its contract for an Error is `err.message`.
vi.mock("@/lib/api", () => ({
  apiErrorMessage: (err: unknown) => (err instanceof Error ? err.message : "Something went wrong."),
}));

import { Tabs, TabPanel, ToastProvider } from "@kurx/ui";
import { AlliesManager } from "@/components/profile/allies-manager";
import { PeopleSearch } from "@/components/profile/people-search";
import { AllyConnectButton } from "@/components/profile/ally-connect-button";
import { FollowButton } from "@/components/orgs/follow-button";
import { ProvenanceBadge } from "@/components/profile/provenance-badge";
import { ProfileSections } from "@/components/profile/profile-sections";

/**
 * Guards for Phase 18A — public profile, allies and professional identity.
 *
 * Each block below corresponds to a defect that was live on `main`, not to a hypothetical. The
 * `Tabs` block in particular guards a primitive that declared `role="tablist"` while implementing
 * none of the pattern that role promises.
 */

function ally(over: Record<string, unknown> = {}) {
  return {
    id: "c1",
    other_user_id: "u2",
    other_name: "Priya Nair",
    other_username: "priya",
    other_avatar_key: null,
    status: "Accepted",
    first_shared_event_title: null,
    ...over,
  } as never;
}

beforeEach(() => {
  vi.clearAllMocks();
});

describe("Tabs — a declared role must be an implemented role", () => {
  function Harness() {
    const [tab, setTab] = require("react").useState("one");
    return (
      <>
        <Tabs
          id="t"
          value={tab}
          onChange={setTab}
          tabs={[
            { id: "one", label: "One" },
            { id: "two", label: "Two" },
            { id: "three", label: "Three" },
          ]}
        />
        <TabPanel tabsId="t" id="one" active={tab === "one"}>first</TabPanel>
        <TabPanel tabsId="t" id="two" active={tab === "two"}>second</TabPanel>
        <TabPanel tabsId="t" id="three" active={tab === "three"}>third</TabPanel>
      </>
    );
  }

  it("uses a roving tabindex — only the selected tab is in the tab order", () => {
    render(<Harness />);
    const tabs = screen.getAllByRole("tab");
    expect(tabs[0]).toHaveAttribute("tabindex", "0");
    expect(tabs[1]).toHaveAttribute("tabindex", "-1");
    expect(tabs[2]).toHaveAttribute("tabindex", "-1");
  });

  it("moves selection and focus with the arrow keys", async () => {
    render(<Harness />);
    const tabs = screen.getAllByRole("tab");
    tabs[0].focus();
    await userEvent.keyboard("{ArrowRight}");
    expect(tabs[1]).toHaveAttribute("aria-selected", "true");
    expect(tabs[1]).toHaveFocus();
    expect(screen.getByRole("tabpanel")).toHaveTextContent("second");
  });

  it("wraps at both ends and jumps with Home/End", async () => {
    render(<Harness />);
    const tabs = screen.getAllByRole("tab");
    tabs[0].focus();
    await userEvent.keyboard("{ArrowLeft}");
    expect(tabs[2]).toHaveAttribute("aria-selected", "true");
    await userEvent.keyboard("{Home}");
    expect(tabs[0]).toHaveAttribute("aria-selected", "true");
    await userEvent.keyboard("{End}");
    expect(tabs[2]).toHaveAttribute("aria-selected", "true");
  });

  it("cross-references each tab with the panel it controls", () => {
    render(<Harness />);
    const selected = screen.getByRole("tab", { selected: true });
    const panel = screen.getByRole("tabpanel");
    expect(selected).toHaveAttribute("aria-controls", panel.id);
    expect(panel).toHaveAttribute("aria-labelledby", selected.id);
  });

  it("emits no dangling aria-controls when no panels are wired", () => {
    render(<Tabs value="a" onChange={() => {}} tabs={[{ id: "a", label: "A" }, { id: "b", label: "B" }]} />);
    for (const tab of screen.getAllByRole("tab")) {
      expect(tab).not.toHaveAttribute("aria-controls");
    }
  });
});

describe("AlliesManager — an action that fails must not look like one that worked", () => {
  it("keeps the request in place when the server rejects the accept", async () => {
    socialActions.respondAllyRequestAction.mockRejectedValue(new Error("Network is down"));
    render(
      <ToastProvider>
        <AlliesManager initialMine={[]} initialIncoming={[ally({ status: "Pending" })]} initialOutgoing={[]} />
      </ToastProvider>
    );

    await userEvent.click(screen.getByRole("button", { name: /Accept Priya Nair/ }));

    await screen.findByText(/Network is down/);
    // The card must still be there: the connection was never accepted.
    expect(screen.getByText("Priya Nair")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /Accept Priya Nair/ })).toBeInTheDocument();
  });

  it("announces a successful accept through the live region", async () => {
    socialActions.respondAllyRequestAction.mockResolvedValue(undefined);
    render(
      <ToastProvider>
        <AlliesManager initialMine={[]} initialIncoming={[ally({ status: "Pending" })]} initialOutgoing={[]} />
      </ToastProvider>
    );

    await userEvent.click(screen.getByRole("button", { name: /Accept Priya Nair/ }));
    await screen.findByText(/Priya Nair is now an ally/);
  });

  it("gates the destructive remove behind a confirmation", async () => {
    socialActions.revokeAllyAction.mockResolvedValue(undefined);
    render(
      <ToastProvider>
        <AlliesManager initialMine={[ally()]} initialIncoming={[]} initialOutgoing={[]} />
      </ToastProvider>
    );

    await userEvent.click(screen.getByRole("button", { name: /Remove Priya Nair/ }));
    // Nothing may have been sent yet — the dialog is the gate.
    expect(socialActions.revokeAllyAction).not.toHaveBeenCalled();

    const dialog = await screen.findByRole("dialog");
    await userEvent.click(within(dialog).getByRole("button", { name: "Remove" }));
    await waitFor(() => expect(socialActions.revokeAllyAction).toHaveBeenCalledWith("c1"));
  });

  it("names each control by the person it acts on", () => {
    render(
      <ToastProvider>
        <AlliesManager
          initialMine={[]}
          initialIncoming={[ally({ id: "a", other_name: "Asha" }), ally({ id: "b", other_name: "Rahul" })]}
          initialOutgoing={[]}
        />
      </ToastProvider>
    );
    // Two "Accept" buttons that differ only by position are indistinguishable in a control list.
    expect(screen.getByRole("button", { name: /Accept Asha/ })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /Accept Rahul/ })).toBeInTheDocument();
  });
});

describe("PeopleSearch — the search must be findable, and its outcome perceivable", () => {
  it("gives the input a real accessible name, not a placeholder", () => {
    render(<PeopleSearch />);
    expect(screen.getByRole("searchbox", { name: /Search people/ })).toBeInTheDocument();
  });

  it("explains why a one-character query does not search", async () => {
    render(<PeopleSearch />);
    await userEvent.type(screen.getByRole("searchbox", { name: /Search people/ }), "a");
    expect(await screen.findByText(/searching starts at 2 characters/i)).toBeInTheDocument();
    expect(socialActions.searchPeopleAction).not.toHaveBeenCalled();
  });

  it("announces the result count in a live region", async () => {
    socialActions.searchPeopleAction.mockResolvedValue([
      { user: { id: "u1", name: "Asha", username: "asha", avatar_key: null, headline: null }, status: "None" },
    ]);
    render(<PeopleSearch />);
    await userEvent.type(screen.getByRole("searchbox", { name: /Search people/ }), "asha");

    await waitFor(() => expect(socialActions.searchPeopleAction).toHaveBeenCalledWith("asha"));
    const status = await screen.findByText(/1 result/);
    expect(status).toHaveAttribute("role", "status");
  });

  it("reports a failed search as an error, never as an empty result", async () => {
    socialActions.searchPeopleAction.mockRejectedValue(new Error("Search is unavailable"));
    render(<PeopleSearch />);
    await userEvent.type(screen.getByRole("searchbox", { name: /Search people/ }), "asha");

    const alert = await screen.findByRole("alert");
    expect(alert).toHaveTextContent(/Search is unavailable/);
    // Saying "no profiles match" would be a claim about the people, not about the outage.
    expect(screen.queryByText(/No public profiles match/)).not.toBeInTheDocument();
  });
});

describe("AllyConnectButton — a control's name must state what activating it does", () => {
  it("names the cancel action, not the state it is showing", () => {
    render(<AllyConnectButton targetUserId="u2" targetName="Priya" initialRelation="outgoing" initialConnectionId="c1" />);
    // Visible label stays "Requested"; the accessible name says what the click will do.
    const button = screen.getByRole("button", { name: /Cancel your ally request to Priya/ });
    expect(button).toHaveTextContent("Requested");
  });

  it("names the remove action behind the accepted state", () => {
    render(<AllyConnectButton targetUserId="u2" targetName="Priya" initialRelation="accepted" initialConnectionId="c1" />);
    expect(screen.getByRole("button", { name: /Remove Priya from your allies/ })).toBeInTheDocument();
    // The literal "✓" is gone — screen readers read it as "check mark" or drop it silently.
    expect(screen.queryByText(/✓/)).not.toBeInTheDocument();
  });

  it("announces a failed connect", async () => {
    socialActions.requestAllyAction.mockRejectedValue(new Error("Already blocked"));
    render(<AllyConnectButton targetUserId="u2" targetName="Priya" initialRelation="none" initialConnectionId={null} />);
    await userEvent.click(screen.getByRole("button", { name: /Connect with Priya/ }));
    expect(await screen.findByRole("alert")).toHaveTextContent(/Already blocked/);
  });
});

describe("FollowButton — a rejected toggle must not read as a completed one", () => {
  it("keeps the previous state and reports the failure", async () => {
    socialActions.toggleFollowAction.mockRejectedValue(new Error("Could not follow"));
    render(
      <ToastProvider>
        <FollowButton orgId="o1" orgName="IIT Madras" initialFollowing={false} />
      </ToastProvider>
    );
    await userEvent.click(screen.getByRole("button", { name: /Follow IIT Madras/ }));
    await screen.findByText(/Could not follow/);
    expect(screen.getByRole("button", { name: /Follow IIT Madras/ })).toBeInTheDocument();
  });

  it("announces the new state on success", async () => {
    socialActions.toggleFollowAction.mockResolvedValue(true);
    render(
      <ToastProvider>
        <FollowButton orgId="o1" orgName="IIT Madras" initialFollowing={false} />
      </ToastProvider>
    );
    await userEvent.click(screen.getByRole("button", { name: /Follow IIT Madras/ }));
    await screen.findByText(/Following IIT Madras/);
  });
});

describe("ProvenanceBadge — the trust explanation must not be pointer-only", () => {
  it("exposes the hint as text rather than only as a title attribute", () => {
    const { container } = render(<ProvenanceBadge source="verified" />);
    // Twice on purpose: the screen-reader copy, and the hover bubble that is aria-hidden so the
    // hint is not announced a second time to anyone who already heard it.
    const hints = screen.getAllByText(/From records Kurx can prove/);
    expect(hints).toHaveLength(2);
    expect(hints.some((el) => el.className.includes("sr-only"))).toBe(true);
    expect(container.querySelector("[title]")).toBeNull();
  });

  it("renders nothing for an unknown provenance rather than guessing", () => {
    const { container } = render(<ProvenanceBadge source="invented" />);
    expect(container).toBeEmptyDOMElement();
  });
});

describe("ProfileSections — no control may advertise itself and go nowhere", () => {
  it("renders an ally with no username as text, not as a link to #", async () => {
    render(
      <ProfileSections
        timeline={[]}
        events={[]}
        organizations={[]}
        certificates={[]}
        achievements={[]}
        allies={[
          { user_id: "u1", name: "No Handle", username: null, mutual_event_count: 0 } as never,
          { user_id: "u2", name: "Has Handle", username: "has", mutual_event_count: 0 } as never,
        ]}
      />
    );
    await userEvent.click(screen.getByRole("tab", { name: /Allies/ }));

    expect(screen.getByRole("link", { name: "Has Handle" })).toHaveAttribute("href", "/u/has");
    expect(screen.queryByRole("link", { name: "No Handle" })).not.toBeInTheDocument();
    expect(screen.getByText("No Handle")).toBeInTheDocument();
  });
});
