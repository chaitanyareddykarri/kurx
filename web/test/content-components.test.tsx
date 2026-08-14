import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";

import {
  CardAction, CategoryCard, EventCard, FilterChips, LinkCard, SearchBar, Stat, TicketCard, Timeline
} from "@kurx/ui";

/** Guards on the content components (Phase 10). */

describe("LinkCard", () => {
  it("is a single tab stop named by its title, not by every word inside", async () => {
    render(
      <LinkCard href="/e/x" label="Kurx Hack Night">
        <p>Some descriptive body text that should not become the link name.</p>
      </LinkCard>
    );
    const link = screen.getByRole("link", { name: "Kurx Hack Night" });
    expect(link).toHaveAttribute("href", "/e/x");
    await userEvent.tab();
    expect(link).toHaveFocus();
  });

  it("keeps nested controls above the stretched overlay", async () => {
    const clicks: string[] = [];
    render(
      <LinkCard href="/e/x" label="Card">
        <CardAction>
          <button type="button" onClick={() => clicks.push("action")}>
            Save
          </button>
        </CardAction>
      </LinkCard>
    );
    await userEvent.click(screen.getByRole("button", { name: "Save" }));
    expect(clicks).toEqual(["action"]);
  });
});

describe("EventCard", () => {
  it("shows one status, once, in human words", () => {
    // The web card previously rendered the raw status in a pill AND a mapped
    // label at the foot, so "cancelled … Cancelled" appeared on one card.
    render(
      <EventCard href="/e/a" title="Hack Night" status="cancelled" when="14 Mar, 6:00 pm" where="Bengaluru" />
    );
    expect(screen.getAllByText(/cancelled/i)).toHaveLength(1);
    expect(screen.getByText("Cancelled")).toBeInTheDocument();
  });

  it("maps under-review to the attestation tone rather than a generic one", () => {
    render(<EventCard href="/e/a" title="X" status="review" when="soon" />);
    expect(screen.getByText("Under review").className).toContain("text-teal");
  });

  it("exposes exactly one link, named by the event", () => {
    render(<EventCard href="/e/a" title="Hack Night" status="available" when="14 Mar" where="Bengaluru" />);
    const links = screen.getAllByRole("link");
    expect(links).toHaveLength(1);
    expect(links[0]).toHaveAccessibleName("Hack Night");
  });
});

describe("CategoryCard", () => {
  it("puts the count in the accessible name rather than leaving it orphaned", () => {
    render(<CategoryCard href="/c/music" label="Music" count={12} />);
    expect(screen.getByRole("link", { name: "Music, 12 events" })).toBeInTheDocument();
  });

  it("singularises correctly", () => {
    render(<CategoryCard href="/c/x" label="Theatre" count={1} />);
    expect(screen.getByRole("link", { name: "Theatre, 1 events" })).toBeInTheDocument();
    expect(screen.getByText("1 event")).toBeInTheDocument();
  });
});

describe("TicketCard", () => {
  it("names itself with event, type and status so a wallet list is navigable", () => {
    render(
      <TicketCard href="/tickets/1" eventTitle="Hack Night" when="14 Mar" ticketType="General" status="used" />
    );
    expect(screen.getByRole("link", { name: "Hack Night — General, Checked in" })).toBeInTheDocument();
  });
});

describe("Stat", () => {
  it("says the trend direction in words, not only in colour", () => {
    render(<Stat label="Registrations" value="1,204" trend="12%" tone="up" />);
    expect(screen.getByText(/up/)).toBeInTheDocument();
  });
});

describe("Timeline", () => {
  it("is an ordered list and states each entry's state in text", () => {
    render(
      <Timeline
        entries={[
          { id: "1", title: "Submitted", timestamp: "1 Mar", state: "done" },
          { id: "2", title: "Under review", timestamp: "2 Mar", state: "current" }
        ]}
      />
    );
    expect(screen.getAllByRole("listitem")).toHaveLength(2);
    expect(screen.getByText(/— done/)).toBeInTheDocument();
    expect(screen.getByText(/— current/)).toBeInTheDocument();
  });
});

describe("SearchBar", () => {
  it("uses a boundary that identifies the control, and meets the touch floor", () => {
    render(<SearchBar value="" onChange={() => {}} />);
    const cls = screen.getByRole("searchbox").className;
    expect(cls).toContain("border-border-strong");
    expect(cls).toContain("min-h-11");
  });

  it("announces the result count once the user has actually searched", async () => {
    // A debounced search silently replaces the page; without this a screen-reader
    // user gets no signal that anything happened.
    const { rerender } = render(<SearchBar value="" onChange={() => {}} resultCount={40} />);
    expect(screen.getByRole("status")).toHaveTextContent("");
    await userEvent.type(screen.getByRole("searchbox"), "hack");
    rerender(<SearchBar value="hack" onChange={() => {}} resultCount={3} />);
    expect(screen.getByRole("status")).toHaveTextContent("3 results");
  });

  it("gives the clear control a real target", async () => {
    render(<SearchBar value="hack" onChange={() => {}} />);
    const clear = screen.getByRole("button", { name: "Clear search" });
    // Was a 24px box.
    expect(clear.className).toContain("h-10");
  });
});

describe("FilterChips", () => {
  it("names both the facet and its value, so a bare value is never guesswork", () => {
    render(
      <FilterChips filters={[{ id: "city", label: "City", value: "Bengaluru" }]} onRemove={() => {}} />
    );
    expect(screen.getByText("City:")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Remove filter City: Bengaluru" })).toBeInTheDocument();
  });

  it("distinguishes remove buttons from one another", () => {
    render(
      <FilterChips
        filters={[
          { id: "city", label: "City", value: "Bengaluru" },
          { id: "when", label: "Date", value: "This weekend" }
        ]}
        onRemove={() => {}}
        onClearAll={() => {}}
      />
    );
    // Five "Remove" buttons in a row would be indistinguishable by name.
    expect(screen.getByRole("button", { name: /Remove filter Date/ })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Clear all" })).toBeInTheDocument();
  });

  it("renders nothing when no filter is applied", () => {
    const { container } = render(<FilterChips filters={[]} onRemove={() => {}} />);
    expect(container).toBeEmptyDOMElement();
  });
});
