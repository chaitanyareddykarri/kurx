import { readFileSync } from "node:fs";
import { relative, resolve, sep } from "node:path";
import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";

import { RepresentationStatus } from "@/components/host/representation-status";
import type { EventAuthorizationView } from "@/lib/api";

/**
 * Guards for the ONE event-level representation workflow.
 *
 * Representation — the organisation an event is hosted on behalf of, and that event's own
 * authorisation letter — is answered exactly once, inside Create Event's Representing step. The only
 * thing that re-opens it is a reviewer asking for changes, and that is an EDIT of the event.
 *
 * Three failures are pinned here, each of which has actually shipped:
 *
 *   · **The duplicate form.** The authorisation form lived on both the wizard and the Readiness page,
 *     either of which would overwrite the single `event_authorizations` row the other had written.
 *   · **The workspace tab.** A standing `Workspace ▸ Representing` page made a second permanent place
 *     to answer a question creation had already answered, so organisers believed creation was
 *     unfinished and went looking for it.
 *   · **The redirect.** The step linked out to `/host/representing/new` to register an institution — a
 *     full navigation out of a wizard holding ten steps of unsaved answers, so anyone who did not
 *     already represent an organisation lost the event they were creating.
 */

const web = (p: string) => resolve(__dirname, "..", p);
const source = (p: string) => readFileSync(web(p), "utf8");

const READINESS = "app/(app)/host/events/[id]/readiness/page.tsx";
/// Edit Event. The correction path lives with editing the event, not as a workspace section.
const EDIT_EVENT = "app/(app)/host/events/[id]/details/page.tsx";
const WIZARD = "components/host/create-event-wizard.tsx";
const LAYOUT = "app/(app)/host/events/[id]/layout.tsx";

function authorization(overrides: Partial<EventAuthorizationView> = {}): EventAuthorizationView {
  return {
    event_id: "e1",
    head_name: "A. Rao", head_designation: "Principal",
    official_email: "principal@nsrit.edu.in", official_phone: "+919876543210",
    representative_role: "Principal", representative_role_other: null,
    representative_user_id: null, representative_username: null, reviewer_name: null,
    letterhead_url: null, signature_url: null, supporting_document_urls: [],
    status: "Submitted", reason_code: null, notes: null,
    created_at: "2026-08-18T00:00:00Z", updated_at: "2026-08-18T00:00:00Z",
    ...overrides,
  };
}

describe("Readiness reports representation and never collects it", () => {
  it("renders no authorization input of any kind", () => {
    render(<RepresentationStatus eventId="e1" orgName="NSRIT College" authorization={authorization()} />);
    // The seven inputs the duplicate form carried. A status card has no business owning any of them.
    expect(screen.queryAllByRole("textbox")).toHaveLength(0);
    expect(screen.queryAllByRole("combobox")).toHaveLength(0);
    expect(screen.queryAllByRole("button")).toHaveLength(0);
    expect(document.querySelector("input")).toBeNull();
    expect(document.querySelector("form")).toBeNull();
  });

  it("names the organization and links to Edit Event, not to a workspace Representing page", () => {
    render(<RepresentationStatus eventId="e1" orgName="NSRIT College" authorization={authorization()} />);
    expect(screen.getByText("NSRIT College")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Manage representation" }))
      .toHaveAttribute("href", "/host/events/e1/details#representing");
  });

  it("says pending review while the filing waits for a reviewer", () => {
    render(<RepresentationStatus eventId="e1" orgName="NSRIT College" authorization={authorization()} />);
    expect(screen.getByText(/Pending review/)).toBeInTheDocument();
  });

  it("says approved, and offers to view rather than to fix", () => {
    render(<RepresentationStatus eventId="e1" orgName="NSRIT College"
      authorization={authorization({ status: "Approved" })} />);
    expect(screen.getByText(/^Approved$/)).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "View representation" })).toBeInTheDocument();
  });

  it("names a legacy self-representation as legacy rather than leaving the line blank", () => {
    // A pre-D-379 event's "organization" is a self-representation row (D-268). Blank reads as
    // loading, and the person's name there would claim institutional backing nobody gave.
    render(<RepresentationStatus eventId="e1" orgName={null} isPersonal authorization={authorization()} />);
    expect(screen.getByText("Legacy personal representation")).toBeInTheDocument();
  });

  it("says action required when nothing is filed at all", () => {
    render(<RepresentationStatus eventId="e1" orgName="NSRIT College" authorization={null} />);
    expect(screen.getByText(/Action required/)).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Complete representation" })).toBeInTheDocument();
  });

  it("carries the reviewer's own reason when changes were requested", () => {
    // "Action required" without the reason is a verdict with no way to act on it.
    render(<RepresentationStatus eventId="e1" orgName="NSRIT College"
      authorization={authorization({ status: "ChangesRequested", reason_code: "Incomplete", notes: "The letter doesn't name the event." })} />);
    expect(screen.getByText(/Action required/)).toBeInTheDocument();
    expect(screen.getByText("Incomplete")).toBeInTheDocument();
    expect(screen.getByText(/doesn't name the event/)).toBeInTheDocument();
  });
});

describe("the event workspace has no Representing section", () => {
  it("declares no Representing tab", () => {
    expect(source(LAYOUT)).not.toMatch(/key: "representing"/);
    expect(source(LAYOUT)).not.toMatch(/\$\{base\}\/representing/);
  });

  it("has no /host/events/[id]/representing route at all", () => {
    const { existsSync } = require("node:fs") as typeof import("node:fs");
    expect(existsSync(web("app/(app)/host/events/[id]/representing"))).toBe(false);
  });
});

describe("the authorization form has exactly one home outside Create Event", () => {
  it("is rendered by Edit Event, for the reviewer's changes-requested case", () => {
    const page = source(EDIT_EVENT);
    expect(page).toMatch(/<AuthorizationForm/);
    // Gated, not standing: rendering it unconditionally rebuilds the workspace tab under a new name.
    expect(page).toMatch(/needsCorrection/);
    expect(page).toMatch(/ChangesRequested/);
    expect(page).toMatch(/Rejected/);
  });

  it("is not rendered — or even imported — by Readiness", () => {
    // An import with no JSX would be the next version of this bug: the form one refactor away from
    // coming back to a page whose job is to report state.
    expect(source(READINESS)).not.toMatch(/AuthorizationForm/);
  });

  it("is rendered nowhere else in the app", () => {
    const { readdirSync, statSync } = require("node:fs") as typeof import("node:fs");
    const walk = (dir: string): string[] =>
      readdirSync(dir).flatMap((entry: string) => {
        const full = resolve(dir, entry);
        return statSync(full).isDirectory() ? walk(full) : /\.tsx$/.test(entry) ? [full] : [];
      });
    const renderers = [...walk(web("app")), ...walk(web("components"))]
      .filter((f) => /<AuthorizationForm/.test(readFileSync(f, "utf8")))
      // Windows separators, normalised so the expectation reads as a route rather than a path.
      .map((f) => relative(web("."), f).split(sep).join("/"));
    expect(renderers).toEqual([EDIT_EVENT]);
  });
});

describe("Create Event registers an organization WITHOUT leaving the wizard", () => {
  it("renders the registration form inline on the Representing step", () => {
    expect(source(WIZARD)).toMatch(/<CreateOrgForm/);
  });

  it("never navigates to the standalone request page from the wizard", () => {
    // The whole point: an `<a href>` or a router push here discards every unsaved answer behind it.
    // The string may appear in a comment explaining why it is gone — an attribute may not.
    const wizard = source(WIZARD);
    expect(wizard).not.toMatch(/href="\/host\/representing\/new/);
    expect(wizard).not.toMatch(/router\.push\(\s*["'`]\/host\/representing/);
  });

  it("selects the staged organization on the spot, so the step is answerable immediately", () => {
    const wizard = source(WIZARD);
    expect(wizard).toMatch(/onRegistered=\{/);
    expect(wizard).toMatch(/setRepresentingOrgId\(rep\.organization_id\)/);
  });

  it("files the request through the SAME two actions the standalone page uses", () => {
    // Two code paths for "what registering an institution requires" is how one of them drifts.
    const actions = readFileSync(web("lib/org-actions.ts"), "utf8");
    expect(actions).toMatch(/presignRepresentationDocAction/);
    expect(actions).toMatch(/fileRepresentationRequestAction/);
  });

  it("uploads the proof from the BROWSER, never from a server action", () => {
    /*
     * The bug this pins shipped and was reported: "fetch failed" on the letterhead upload.
     *
     * The presigned URL is browser-facing (`http://localhost:5080/...`). A server action runs inside the
     * Next.js container, where that host is ECONNREFUSED, and undici surfaces it as a bare
     * `TypeError: fetch failed`. So the PUT belongs in the client component — which is what
     * `AuthorizationForm` and the wizard have always done for the authorization letter.
     */
    const actions = readFileSync(web("lib/org-actions.ts"), "utf8");
    expect(actions).not.toMatch(/fetch\(\s*presigned\.url/);
    expect(source("components/host/create-org-form.tsx")).toMatch(/fetch\(presigned\.url/);
  });
});

describe("a pending organization may carry a draft but never a paid event", () => {
  it("offers every representation the caller holds, pending included", () => {
    // Filtering pending ones out of the picker enforced nothing the server was not already enforcing,
    // and left whoever had just registered their college with nothing to select.
    expect(source(WIZARD)).toMatch(/const selectableReps = representations;/);
  });

  it("keeps the paid bar on verification, separately", () => {
    const wizard = source(WIZARD);
    expect(wizard).toMatch(/const paidCapableReps = representations\.filter/);
    expect(wizard).toMatch(/can_back_paid_event \?\? r\.is_verified/);
    // The Registration step's money gate reads the STRICTER set.
    expect(wizard).toMatch(/pricing === "free" \|\| paidCapableReps\.some/);
  });

  it("tells the organiser a pending organization blocks publishing, not creating", () => {
    expect(source(WIZARD)).toMatch(/can't publish until that's approved/);
  });
});

describe("the standalone request page still exists for the profile path", () => {
  it("survives a repeated returnTo without throwing", () => {
    // Next hands a repeated `?returnTo=a&returnTo=b` over as an ARRAY. `.startsWith` on one is a 500
    // on a URL anyone can type, so the guard normalises before testing it.
    const guard = readFileSync(web("lib/safe-return-to.ts"), "utf8");
    expect(guard).toMatch(/Array\.isArray\(value\)/);
    expect(source("app/(app)/host/representing/new/page.tsx")).toMatch(/safeReturnTo\(searchParams/);
  });

  it("follows only an in-app host path, never an absolute URL", () => {
    // A redirect target that arrives in a query parameter is untrusted input, and it is untrusted at the
    // point of USE, not only where it was read — the redirect happens in the browser now, so the guard
    // is applied there too rather than only by the page that rendered the form.
    const guard = readFileSync(web("lib/safe-return-to.ts"), "utf8");
    expect(guard).toMatch(/startsWith\("\/host\/"\)/);
    expect(source("components/host/create-org-form.tsx")).toMatch(/safeReturnTo\(returnTo\)/);
  });
});
