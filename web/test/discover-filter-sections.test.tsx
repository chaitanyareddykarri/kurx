import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";

import { ChipRail } from "@/components/ui/chip-rail";
import { FilterSection } from "@/components/ui/filter-section";

/// The Discover filter groups. Both rails ship CLOSED — 21 kinds plus 13 categories pushed the results
/// the page exists for below two screens of pills — so what is worth pinning is the open/shut contract
/// itself: a regression here does not throw, it just quietly dumps 34 options back onto arrival.

/*
 * `next/link` stub. The `onClick` is not decoration: the real `Link` intercepts the click and routes
 * client-side, so a bare `<a href>` here is LESS faithful than it looks — jsdom cannot navigate, and
 * a click on one schedules a navigation that surfaces later as an unhandled
 * "Not implemented: navigation (except hash changes)". That error is attributed to no test, and it
 * exits the runner non-zero on a suite where every assertion passed (seen 2026-08-18). Preventing the
 * default is what the real component does; any handler passed in still runs first.
 */
vi.mock("next/link", () => ({
  default: ({ children, href, onClick, ...rest }: {
    children: React.ReactNode; href: string;
    onClick?: (e: React.MouseEvent<HTMLAnchorElement>) => void;
  }) => (
    <a href={href} {...rest} onClick={(e) => { onClick?.(e); e.preventDefault(); }}>
      {children}
    </a>
  )
}));

const KINDS = [
  { key: "hackathon", label: "Hackathon", href: "/discover?kind=hackathon" },
  { key: "race", label: "Race", href: "/discover?kind=race" }
];

function renderKindSection(selectedLabel?: string, items = KINDS) {
  return render(
    <FilterSection title="Event type" selectedLabel={selectedLabel}>
      <ChipRail ariaLabel="Event type" items={items} />
    </FilterSection>
  );
}

const toggle = () => screen.getByRole("button", { name: /event type/i });
/** The animated wrapper — `aria-controls` is the only stable handle on it. */
const panel = () => document.getElementById(toggle().getAttribute("aria-controls")!)!;

describe("Discover filter sections", () => {
  it("starts collapsed, so the options are not on the page on arrival", () => {
    renderKindSection();
    expect(toggle()).toHaveAttribute("aria-expanded", "false");
    // Height alone would still leave the links tabbable, which is why `inert` carries this.
    expect(panel()).toHaveAttribute("inert");
  });

  it("expands on click and collapses on the next one", async () => {
    const user = userEvent.setup();
    renderKindSection();

    await user.click(toggle());
    expect(toggle()).toHaveAttribute("aria-expanded", "true");
    expect(panel()).not.toHaveAttribute("inert");

    await user.click(toggle());
    expect(toggle()).toHaveAttribute("aria-expanded", "false");
    expect(panel()).toHaveAttribute("inert");
  });

  it("keeps every option, each carrying the filter URL that applies it", async () => {
    const user = userEvent.setup();
    renderKindSection();
    await user.click(toggle());

    const links = within(panel()).getAllByRole("link");
    expect(links).toHaveLength(KINDS.length);
    expect(links.map((l) => l.getAttribute("href"))).toEqual([
      "/discover?kind=hackathon",
      "/discover?kind=race"
    ]);
  });

  it("shows the current pick while shut, and drops it once the ticked chip says the same thing", async () => {
    const user = userEvent.setup();
    renderKindSection("Hackathon");

    expect(within(toggle()).getByText("Hackathon")).toBeInTheDocument();
    await user.click(toggle());
    expect(within(toggle()).queryByText("Hackathon")).not.toBeInTheDocument();
  });

  it("leaves the section open when an option is clicked — picking a filter must not shut the group", async () => {
    const user = userEvent.setup();
    renderKindSection();
    await user.click(toggle());

    await user.click(within(panel()).getByRole("link", { name: /hackathon/i }));

    expect(toggle()).toHaveAttribute("aria-expanded", "true");
  });

  it("marks the selected option and only that one", () => {
    render(
      <ChipRail
        ariaLabel="Event type"
        items={[{ ...KINDS[0], selected: true }, KINDS[1]]}
      />
    );
    const selected = screen.getByRole("link", { name: /hackathon/i });
    expect(selected).toHaveAttribute("aria-current", "true");
    expect(screen.getByRole("link", { name: /race/i })).not.toHaveAttribute("aria-current");
  });
});
