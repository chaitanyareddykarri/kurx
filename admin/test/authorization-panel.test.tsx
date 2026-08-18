import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";

vi.mock("@/lib/admin-actions", () => ({ reviewEventAuthorizationAction: vi.fn() }));
// `useFormState`/`useFormStatus` are server-action hooks: they exist in the Next runtime, not in the
// plain `react-dom` this suite renders against, so the component throws before it renders anything.
// Stubbed rather than avoided — the point of these tests is what the panel PUTS ON SCREEN.
vi.mock("react-dom", async (importOriginal) => ({
  ...(await importOriginal<typeof import("react-dom")>()),
  useFormState: (_action: unknown, initial: unknown) => [initial, vi.fn()],
  useFormStatus: () => ({ pending: false }),
}));
vi.mock("@kurx/ui", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@kurx/ui")>()),
  useToast: () => vi.fn(),
}));

import { AuthorizationPanel } from "@/components/admin/authorization-panel";
import type { EventAuthorization } from "@/lib/api";

/**
 * D-382 — the authorization is evidence, and evidence is readable before the claim.
 *
 * The panel used to render only for an event already `UnderReview`, so the queue showed a reviewer the
 * event's details and hid the one document the decision turns on. Deciding whether to pick an item up
 * was therefore done blind, and the only way to learn whether an authorization had even been filed was
 * to claim the event — taking it out of the queue and into your own name.
 *
 * The split these pin is *reading* from *deciding*: the evidence renders for every row, the verdict
 * buttons only for the row you hold.
 */

const filed = (over: Partial<EventAuthorization> = {}): EventAuthorization => ({
  event_id: "e1",
  head_name: "A. Rao",
  head_designation: "Principal",
  official_email: "principal@nsrit.edu.in",
  official_phone: "+919876543210",
  representative_role: "Principal",
  representative_role_other: null,
  representative_user_id: null,
  representative_username: null,
  reviewer_name: null,
  reviewed_at: null,
  letterhead_url: "https://storage/letter.pdf",
  signature_url: null,
  supporting_document_urls: [],
  status: "Submitted",
  reason_code: null,
  notes: null,
  ...over,
} as unknown as EventAuthorization);

describe("an unclaimed event still shows its evidence", () => {
  it("renders the signatory, the contacts and the letter", () => {
    render(<AuthorizationPanel eventId="e1" authorization={filed()} readOnly />);

    expect(screen.getByText("A. Rao")).toBeInTheDocument();
    // Twice, and deliberately: this signatory's DESIGNATION and the submitter's ROLE at the
    // organization both read "Principal". They are different questions with the same answer here.
    expect(screen.getAllByText("Principal", { selector: "dd" })).toHaveLength(2);
    expect(screen.getByText("principal@nsrit.edu.in")).toBeInTheDocument();
    expect(screen.getByText("+919876543210")).toBeInTheDocument();
    // The document itself — the thing a reviewer is actually approving against.
    expect(screen.getByRole("link", { name: /Letterhead/ })).toHaveAttribute(
      "href", "https://storage/letter.pdf");
  });

  it("offers no verdict, and says what unlocks one", () => {
    render(<AuthorizationPanel eventId="e1" authorization={filed()} readOnly />);

    // Reading is not deciding. The transition endpoints refuse an unclaimed verdict anyway, so a
    // button here would only produce a refusal the reviewer cannot act on.
    expect(screen.queryByRole("button", { name: /Approve authorization/ })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /^Reject$/ })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /Request changes/ })).not.toBeInTheDocument();
    expect(screen.getByText(/Claim this event to approve/)).toBeInTheDocument();
  });

  it("reports a missing authorization rather than rendering nothing", () => {
    // The most decision-relevant state of all, and the one the claim gate used to hide completely.
    render(<AuthorizationPanel eventId="e1" authorization={null} readOnly />);
    expect(screen.getByText(/No authorization filed for this event/)).toBeInTheDocument();
  });
});

describe("a claimed event carries the verdict", () => {
  it("offers all three decisions", () => {
    render(<AuthorizationPanel eventId="e1" authorization={filed()} />);

    expect(screen.getByRole("button", { name: /Approve authorization/ })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /^Reject$/ })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /Request changes/ })).toBeInTheDocument();
    expect(screen.queryByText(/Claim this event to approve/)).not.toBeInTheDocument();
  });
});
