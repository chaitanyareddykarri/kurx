import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";

import {
  GettingThereSection, ParticipationSection, RulesSection, TermsSection,
  priceLabel, teamPriceRows, teamSizeLabel
} from "@/components/events/event-detail-sections";
import type { EventDetail } from "@/lib/api";

/**
 * The D-265 field groups on the public event page.
 *
 * They have been validated by the create-event wizard, stored, and returned by
 * `GET /v1/events/{slug}` since D-265 — and rendered by nothing. So an organiser could set an 18+ rule,
 * a registration deadline, a refund policy and a binding consent statement, and the person deciding
 * whether to register saw none of it. These pin both halves: that the rules now show, and that an event
 * which set none of them still renders a short page rather than a wall of dashes.
 */

const BARE = {
  id: "e1", title: "Bare Event", slug: "bare-event", subtitle: "", description: "",
  status: "published", visibility: "listed"
} as unknown as EventDetail;

function withGroups(over: Partial<EventDetail>): EventDetail {
  return { ...BARE, ...over } as EventDetail;
}

describe("ParticipationSection", () => {
  it("renders nothing when the organiser set no rule", () => {
    const { container } = render(<ParticipationSection event={BARE} />);
    expect(container).toBeEmptyDOMElement();
  });

  it("shows an age range, a gender restriction and the registration window", () => {
    render(<ParticipationSection event={withGroups({
      eligibility: { min_age: 18, max_age: 60, gender_restriction: "Female", max_teams: 40 },
      schedule: { registration_opens_at: "2026-09-01T09:00:00Z", registration_closes_at: "2026-09-10T09:00:00Z" }
    })} />);
    expect(screen.getByText("18–60")).toBeInTheDocument();
    expect(screen.getByText("Female")).toBeInTheDocument();
    expect(screen.getByText("40")).toBeInTheDocument();
    expect(screen.getByText("Registration closes")).toBeInTheDocument();
  });

  it("never renders 'Any' — that is the server's default for no restriction, not a rule", () => {
    render(<ParticipationSection event={withGroups({
      eligibility: { gender_restriction: "Any", min_age: 18 }
    })} />);
    expect(screen.queryByText("Any")).not.toBeInTheDocument();
    expect(screen.getByText("18 and over")).toBeInTheDocument();
  });

  it("reads a one-sided bound as a sentence rather than a broken range", () => {
    render(<ParticipationSection event={withGroups({ eligibility: { max_age: 12 } })} />);
    expect(screen.getByText("12 and under")).toBeInTheDocument();
  });
});

describe("RulesSection", () => {
  it("renders nothing without rules or a code of conduct", () => {
    const { container } = render(<RulesSection event={BARE} />);
    expect(container).toBeEmptyDOMElement();
  });

  it("shows the organiser's rules and code of conduct separately", () => {
    render(<RulesSection event={withGroups({
      content: { rules: "Bring a laptop." },
      legal: { code_of_conduct: "Be kind." }
    })} />);
    expect(screen.getByText("Bring a laptop.")).toBeInTheDocument();
    expect(screen.getByText("Code of conduct")).toBeInTheDocument();
    expect(screen.getByText("Be kind.")).toBeInTheDocument();
  });
});

describe("TermsSection", () => {
  it("renders nothing when there are no terms", () => {
    const { container } = render(<TermsSection event={BARE} />);
    expect(container).toBeEmptyDOMElement();
  });

  it("shows the exact consent wording a registrant will be bound by", () => {
    // `RegistrationConsent` records an acceptance against this exact string, so it has to be legible
    // before the checkbox rather than at it.
    render(<TermsSection event={withGroups({
      legal: { requires_consent: true, consent_text: "I accept the code of conduct." }
    })} />);
    expect(screen.getByText("You will be asked to accept")).toBeInTheDocument();
    expect(screen.getByText("I accept the code of conduct.")).toBeInTheDocument();
  });

  it("does not show a stale statement when the event no longer requires consent", () => {
    render(<TermsSection event={withGroups({
      legal: { requires_consent: false, consent_text: "stale wording", refund_policy: "No refunds." }
    })} />);
    expect(screen.queryByText("stale wording")).not.toBeInTheDocument();
    expect(screen.getByText("No refunds.")).toBeInTheDocument();
  });

  it("links the full terms out of the page", () => {
    render(<TermsSection event={withGroups({ legal: { terms_url: "https://audit.test/terms" } })} />);
    expect(screen.getByRole("link", { name: /Full terms/ })).toHaveAttribute("href", "https://audit.test/terms");
  });
});

describe("GettingThereSection", () => {
  it("renders nothing when the organiser gave no in-building detail", () => {
    const { container } = render(<GettingThereSection event={BARE} />);
    expect(container).toBeEmptyDOMElement();
  });

  it("joins building, floor and room into one line", () => {
    render(<GettingThereSection event={withGroups({
      location_detail: { building: "Block A", floor: "3", room: "301" }
    })} />);
    expect(screen.getByText("Block A · Floor 3 · Room 301")).toBeInTheDocument();
  });

  it("shows the check-in window a ticket holder needs on the day", () => {
    render(<GettingThereSection event={withGroups({
      schedule: { checkin_opens_at: "2026-09-14T08:00:00Z", checkin_closes_at: "2026-09-14T12:00:00Z" }
    })} />);
    expect(screen.getByText("Check-in opens")).toBeInTheDocument();
    expect(screen.getByText("Check-in closes")).toBeInTheDocument();
  });

  it("never renders the meeting password — that is for confirmed registrants only", () => {
    render(<GettingThereSection event={withGroups({
      location_detail: { meeting_platform: "Zoom" }
    })} />);
    expect(screen.getByText("Zoom")).toBeInTheDocument();
    expect(screen.queryByText(/password/i)).not.toBeInTheDocument();
  });
});

/**
 * D-372 — a price must say what it buys.
 *
 * `pricing_unit` has been on the public ticket-type response since D-020 and no surface rendered it, so
 * an event charging ₹2,000 per TEAM and one charging ₹2,000 per PERSON looked identical to a registrant.
 * Mirrors Flutter's `TicketType.priceLabel`/`teamSizeLabel` word for word.
 */
const money = (paise: number) => `₹${Math.round(paise / 100)}`;

describe("priceLabel", () => {
  it("says per team when the unit is PerGroup", () => {
    expect(priceLabel(200_000, "PerGroup", money)).toBe("₹2000 per team");
  });

  it("says per participant otherwise", () => {
    expect(priceLabel(50_000, "PerTicket", money)).toBe("₹500 per participant");
  });

  /**
   * D-366 — a banded ticket has no single price, so the headline becomes a RANGE.
   *
   * `price_paise` is the cheapest band on such a ticket. Printing it alone quotes ₹250 on an event that
   * also charges ₹400, which is the same class of lie D-372's unit label exists to prevent.
   */
  describe("team-size bands", () => {
    const BANDS = [
      { min_size: 2, max_size: 2, price_paise: 25_000 },
      { min_size: 3, max_size: 3, price_paise: 30_000 },
      { min_size: 4, max_size: 5, price_paise: 40_000 },
    ];

    it("shows the range and says it varies by size", () => {
      expect(priceLabel(25_000, "PerGroup", money, BANDS)).toBe("₹250–₹400 per team, by size");
    });

    it("collapses to one figure when every band charges the same", () => {
      expect(priceLabel(25_000, "PerGroup", money, [
        { min_size: 2, max_size: 3, price_paise: 25_000 },
        { min_size: 4, max_size: 5, price_paise: 25_000 },
      ])).toBe("₹250 per team");
    });

    it("ignores an empty band list, so an unbanded ticket reads exactly as before", () => {
      expect(priceLabel(200_000, "PerGroup", money, [])).toBe("₹2000 per team");
      expect(priceLabel(200_000, "PerGroup", money, null)).toBe("₹2000 per team");
    });
  });

  describe("teamPriceRows", () => {
    it("names the size beside every price, sorted", () => {
      // §21 of the report: "₹300" is meaningless on an event that also charges ₹250 and ₹400.
      expect(teamPriceRows([
        { min_size: 4, max_size: 5, price_paise: 40_000 },
        { min_size: 2, max_size: 2, price_paise: 25_000 },
      ], money)).toEqual([
        { size: "2 members", price: "₹250" },
        { size: "4–5 members", price: "₹400" },
      ]);
    });

    it("returns null when there are no bands, so the caller renders its single price", () => {
      expect(teamPriceRows(null, money)).toBeNull();
      expect(teamPriceRows([], money)).toBeNull();
    });
  });

  it("treats an absent unit as per participant — every pre-D-372 row", () => {
    expect(priceLabel(50_000, null, money)).toBe("₹500 per participant");
    expect(priceLabel(50_000, undefined, money)).toBe("₹500 per participant");
  });

  it("still says what free is free FOR", () => {
    expect(priceLabel(0, "PerGroup", money)).toBe("Free per team");
    expect(priceLabel(0, "PerTicket", money)).toBe("Free");
  });
});

describe("teamSizeLabel", () => {
  it("renders the range a registrant needs before deciding", () => {
    expect(teamSizeLabel("Group", 3, 5)).toBe("teams of 3–5");
    expect(teamSizeLabel("Group", 4, 4)).toBe("teams of 4");
    expect(teamSizeLabel("Group", 2, null)).toBe("teams of 2+");
    expect(teamSizeLabel("Group", null, 6)).toBe("teams of up to 6");
  });

  it("is absent for an individual ticket, whatever bounds happen to be stored", () => {
    expect(teamSizeLabel("Individual", 3, 5)).toBeNull();
    expect(teamSizeLabel(null, 3, 5)).toBeNull();
  });

  it("is absent when a team ticket set no bounds", () => {
    expect(teamSizeLabel("Group", null, null)).toBeNull();
  });
});
