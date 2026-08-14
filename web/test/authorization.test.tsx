import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";

const actions = vi.hoisted(() => ({
  submitAuthorizationAction: vi.fn(),
  uploadAuthorizationDocumentAction: vi.fn(),
}));
const invitations = vi.hoisted(() => ({ searchUsersAction: vi.fn(async () => ({ users: [] })) }));

vi.mock("@/lib/event-actions", () => actions);
vi.mock("@/lib/invitation-actions", () => invitations);
vi.mock("next/navigation", () => ({ useRouter: () => ({ refresh: vi.fn() }) }));

import { AuthorizationForm } from "@/components/host/authorization-form";

/**
 * Guards for Phase 24 — institutional authorization 🔒.
 *
 * This form produces the evidence a reviewer approves an event against, so its two failure modes are
 * the ones that matter: a field whose name nobody can parse, and the wrong document going on record.
 */

// The server publishes this vocabulary (D-266 M5 addendum 2); the component never owns a copy,
// so the test has to supply it too.
const ROLES = ["Principal", "Head of Department", "Other"];

beforeEach(() => vi.clearAllMocks());

describe("a field's name must be its name, not its explanation", () => {
  it("keeps the email helper out of the accessible name", () => {
    render(<AuthorizationForm eventId="e1" existing={null} roles={ROLES} />);
    /*
     * The `<label>` wrapped both the input and its explanatory `<span>`, so the accessible name was
     * "Official email An address at the organization's own domain gets reviewed faster. A personal one
     * is accepted, but then the letter has to carry the whole claim." — twenty-five words on an email
     * box. An exact-name query is what pins this down.
     */
    const email = screen.getByRole("textbox", { name: "Official email" });
    expect(email).toHaveAttribute("type", "email");
    // The helper still exists — as a description, which is read after the name and can be skipped.
    expect(email).toHaveAccessibleDescription(/organization's own domain gets reviewed faster/);
  });

  it("keeps the phone helper out of the accessible name", () => {
    render(<AuthorizationForm eventId="e1" existing={null} roles={ROLES} />);
    const phone = screen.getByRole("textbox", { name: "Official phone" });
    expect(phone).toHaveAccessibleDescription(/Include the country code/);
  });
});

describe("the letter on file must be the letter that was uploaded", () => {
  it("drops the previous attachment before attempting a replacement", async () => {
    // First upload succeeds.
    actions.uploadAuthorizationDocumentAction.mockResolvedValueOnce({ url: "https://s3/put", headers: {}, key: "k1" });
    const fetchMock = vi.fn(async () => ({ ok: true }) as Response);
    vi.stubGlobal("fetch", fetchMock);

    render(<AuthorizationForm eventId="e1" existing={null} roles={ROLES} />);
    const input = document.getElementById("auth-letter") as HTMLInputElement;
    await userEvent.upload(input, new File(["a"], "first.pdf", { type: "application/pdf" }));
    expect(await screen.findByText(/first\.pdf attached\./)).toBeInTheDocument();

    // The replacement fails at the storage PUT.
    actions.uploadAuthorizationDocumentAction.mockResolvedValueOnce({ url: "https://s3/put", headers: {}, key: "k2" });
    fetchMock.mockResolvedValueOnce({ ok: false } as Response);
    await userEvent.upload(input, new File(["b"], "second.pdf", { type: "application/pdf" }));

    await screen.findByRole("alert");
    /*
     * The old key and filename used to survive a failed replace, so the form showed a letter attached
     * and submitting would have filed the PREVIOUS document under the new details — the wrong evidence
     * going on record for the event a reviewer then approves.
     */
    expect(screen.queryByText(/first\.pdf attached\./)).not.toBeInTheDocument();
    expect(screen.queryByText(/second\.pdf attached\./)).not.toBeInTheDocument();

    vi.unstubAllGlobals();
  });

  it("reports a dropped connection instead of leaving the button re-enabled", async () => {
    actions.uploadAuthorizationDocumentAction.mockResolvedValueOnce({ url: "https://s3/put", headers: {}, key: "k1" });
    vi.stubGlobal("fetch", vi.fn(async () => { throw new Error("network down"); }));

    render(<AuthorizationForm eventId="e1" existing={null} roles={ROLES} />);
    const input = document.getElementById("auth-letter") as HTMLInputElement;
    await userEvent.upload(input, new File(["a"], "letter.pdf", { type: "application/pdf" }));

    // `fetch` rejects rather than returning a non-ok response, and that rejection went unhandled.
    expect(await screen.findByRole("alert")).toHaveTextContent(/connection dropped/);
    vi.unstubAllGlobals();
  });

  it("refuses to submit without a letter", async () => {
    render(<AuthorizationForm eventId="e1" existing={null} roles={ROLES} />);
    await userEvent.click(screen.getByRole("button", { name: /Submit for review/ }));
    expect(await screen.findByRole("alert")).toHaveTextContent(/Attach the authorization letter/);
    expect(actions.submitAuthorizationAction).not.toHaveBeenCalled();
  });
});

describe("the file input must be reachable, not merely clickable", () => {
  it("exposes it to assistive tech rather than hiding it", () => {
    render(<AuthorizationForm eventId="e1" existing={null} roles={ROLES} />);
    // `className="hidden"` removes an element from the accessibility tree entirely, so the only route
    // to it was a button forwarding a click.
    const input = document.getElementById("auth-letter") as HTMLInputElement;
    expect(input.className).not.toMatch(/\bhidden\b/);
    expect(input).toHaveAccessibleName("Authorization letter (on the organization's letterhead)");
    expect(input).toHaveAccessibleDescription(/up to 10 MB/);
  });
});
