import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";

import { ReviewDossier } from "@/components/admin/review-dossier";
import type { ReviewEvent } from "@/lib/api";

/**
 * What a reviewer can see before approving.
 *
 * The review queue's list row (`adminEventSchema`) carries 29 fields and **not one is the event's
 * description** — so a reviewer deciding whether a public event may carry Kurx's name into discovery
 * was shown a title, a category, a city, a venue name, a capacity and a date. The queue card deep-linked
 * to the 9-tab event workspace for "the rest", but that workspace's Overview tab renders the same list
 * row, so the description, rules, terms, consent statement, eligibility gates and registration windows
 * appeared on neither screen.
 *
 * Every one of those is collected by the Create Event wizard, stored, and returned by
 * `GET /v1/events/{id}` — which `CanViewAsync` already opens to `kurx_admin` and `VerificationReviewer`
 * (D-191). These pin that the mapping now reaches the reviewer, and that it stops where it should.
 */

const BARE = {
  id: "e1", title: "Bare", slug: "bare", subtitle: "", description: "",
  status: "pendingreview", visibility: "listed",
  starts_at: "2026-09-14T11:00:00Z", ends_at: "2026-09-14T19:00:00Z", timezone: "Asia/Kolkata",
  capacity: null, event_mode: "offline", online_url: null,
  contact_email: "", contact_phone: "", website: "", language: "en", settlement_currency: "INR"
} as unknown as ReviewEvent;

const full = (over: Partial<ReviewEvent> = {}): ReviewEvent => ({
  ...BARE,
  subtitle: "Proving the pipeline",
  description: "A full end-to-end audit event.",
  capacity: 250,
  venue: { name: "Audit Hall", address: "1 Anna Salai", city: "Chennai" },
  content: { tagline: "The pipeline, proven", short_description: "Blurb", rules: "Bring a laptop." },
  legal: {
    terms_url: "https://audit.test/terms", code_of_conduct: "Be kind.",
    refund_policy: "Full refund 7 days prior.", cancellation_policy: "48h notice.",
    requires_consent: true, consent_text: "I accept the code of conduct."
  },
  schedule: {
    registration_opens_at: "2026-09-01T09:00:00Z", registration_closes_at: "2026-09-10T09:00:00Z",
    checkin_opens_at: "2026-09-14T08:00:00Z", auto_close: true
  },
  location_detail: { building: "Block A", floor: "3", room: "301", google_maps_url: "https://maps.example/x" },
  eligibility: { min_age: 18, max_age: 60, gender_restriction: "Any", max_teams: 40 },
  ...over
} as ReviewEvent);

describe("the reviewer sees the event, not a list row", () => {
  it("shows the description — the field the queue row never carried", () => {
    render(<ReviewDossier event={full()} />);
    expect(screen.getByText("A full end-to-end audit event.")).toBeInTheDocument();
    expect(screen.getByText("Proving the pipeline")).toBeInTheDocument();
  });

  it("shows every D-265 group a decision depends on", () => {
    render(<ReviewDossier event={full()} />);
    // Content
    expect(screen.getByText("Bring a laptop.")).toBeInTheDocument();
    // Legal — the consent statement is the wording each acceptance is recorded against.
    expect(screen.getByText("I accept the code of conduct.")).toBeInTheDocument();
    expect(screen.getByText("Full refund 7 days prior.")).toBeInTheDocument();
    expect(screen.getByText("Be kind.")).toBeInTheDocument();
    // Eligibility
    expect(screen.getByText("18 to 60")).toBeInTheDocument();
    expect(screen.getByText("40")).toBeInTheDocument();
    // Windows and location detail
    expect(screen.getByText("Registration closes")).toBeInTheDocument();
    expect(screen.getByText("Block A · Floor 3 · Room 301")).toBeInTheDocument();
    // And the venue the row did carry but the workspace never rendered
    expect(screen.getByText("Audit Hall")).toBeInTheDocument();
    expect(screen.getByText("Chennai")).toBeInTheDocument();
  });

  it("flags consent that is required but empty, rather than showing a blank", () => {
    // An event that demands acceptance of nothing is a review finding, not a missing field.
    render(<ReviewDossier event={full({
      legal: { requires_consent: true, consent_text: "" }
    })} />);
    expect(screen.getByText("REQUIRED BUT EMPTY")).toBeInTheDocument();
  });

  it("says self-hosted in words rather than leaving the section blank", () => {
    // Null representation is the correct, common answer for a Private event (and a Public one under the
    // dev bypass) — an empty section would read like a failed load.
    render(<ReviewDossier event={full()} />);
    expect(screen.getByText(/Hosted by the organiser personally/)).toBeInTheDocument();
  });

  it("names an unverified organization as unverified", () => {
    render(<ReviewDossier event={full({
      representing: { org_id: "o1", name: "Pending College", slug: "pending", is_verified: false }
    })} />);
    expect(screen.getByText("Pending College · NOT verified")).toBeInTheDocument();
  });

  it("omits a section the organiser left entirely empty", () => {
    // A short event should produce a short page, not eight headings over rows of dashes.
    render(<ReviewDossier event={BARE} />);
    expect(screen.queryByText("Eligibility")).not.toBeInTheDocument();
    expect(screen.queryByText("Content")).not.toBeInTheDocument();
    expect(screen.queryByText("Legal")).not.toBeInTheDocument();
  });

  it("never renders 'Any' — the server's default for no restriction is not a rule", () => {
    render(<ReviewDossier event={full()} />);
    expect(screen.queryByText("Any")).not.toBeInTheDocument();
  });

  it("does not carry the meeting password into the review surface", () => {
    // Confirmed registrants only; no review decision depends on it, so the schema does not read it.
    render(<ReviewDossier event={full()} />);
    expect(screen.queryByText(/password/i)).not.toBeInTheDocument();
  });
});
