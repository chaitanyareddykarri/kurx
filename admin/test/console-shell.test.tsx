import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";

vi.mock("next/navigation", () => ({ usePathname: () => "/verification", useRouter: () => ({ push: vi.fn() }) }));

import { NAV, requiredRolesFor } from "@/components/layout/nav-config";
import { hasRole, type PlatformRole } from "@/lib/roles";
import { AppShell } from "@/components/layout/app-shell";
import { RoleRequired } from "@/components/layout/role-required";

/**
 * **Admin's first tests.**
 *
 * Until now `typecheck && lint && build` plus manual browser checks were the entire guard on 27
 * screens — the largest untested surface in the product, and the one where a mistake means an
 * operator suspending the wrong account rather than a card sitting a few pixels off.
 *
 * D-109 excludes Playwright, Cypress and Storybook and that still holds. This is vitest and React
 * Testing Library, the same runner web has had since Phase 0, so no new dependency class enters the
 * repo — only the ability to assert on admin instead of looking at it.
 */

describe("route roles resolve from the navigation itself", () => {
  it("scopes a route to the roles its nav entry declares", () => {
    // Phase 26: the IA was declared in NAV and enforced nowhere.
    expect(requiredRolesFor("/staff")).toEqual(["SuperAdmin"]);
    expect(requiredRolesFor("/verification")).toEqual(["VerificationReviewer"]);
    expect(requiredRolesFor("/audit")).toEqual(["SuperAdmin", "ReadOnlyAuditor"]);
  });

  it("gives a nested route its parent's scope by longest match", () => {
    // `/events/review` must not fall through to the dashboard's `/`.
    expect(requiredRolesFor("/events/review")).toEqual(["VerificationReviewer"]);
    expect(requiredRolesFor("/users/abc-123")).toEqual(["Support", "VerificationReviewer"]);
    expect(requiredRolesFor("/competitions/stage-1")).toEqual(["SuperAdmin"]);
  });

  it("leaves the operator's own pages open to any staff", () => {
    for (const route of ["/", "/account", "/security", "/analytics"]) {
      expect(requiredRolesFor(route)).toEqual([]);
    }
  });

  it("keeps a Support admin out of every SuperAdmin area", () => {
    const support: PlatformRole[] = ["Support"];
    for (const route of ["/staff", "/competitions", "/broadcast", "/health"]) {
      expect(hasRole(support, requiredRolesFor(route))).toBe(false);
    }
  });

  it("lets SuperAdmin through everything", () => {
    for (const group of NAV) {
      for (const item of group.items) {
        expect(hasRole(["SuperAdmin"], requiredRolesFor(item.href))).toBe(true);
      }
    }
  });

  it("offers no destination that is not built", () => {
    // Phase 26 removed the five `ready: false` entries rather than leave permanently disabled spans.
    const unbuilt = NAV.flatMap((g) => g.items).filter((i) => !i.ready);
    expect(unbuilt).toEqual([]);
  });
});

describe("RoleRequired explains the refusal", () => {
  it("names the role needed and the role held", () => {
    render(<RoleRequired required={["SuperAdmin"]} held={["Support"]} />);
    // Both are emphasised, so the operator can see at a glance which is which.
    expect(screen.getByText("Super Admin", { selector: "strong" })).toBeInTheDocument();
    expect(screen.getByText("Support", { selector: "strong" })).toBeInTheDocument();
    // And it is not the sign-you-out message `/forbidden` shows.
    expect(screen.getByText(/needs a different role/i)).toBeInTheDocument();
  });
});

describe("the mobile drawer must be the modal it claims to be", () => {
  const shellProps = { roles: ["SuperAdmin"] as PlatformRole[], name: "Ops", summary: null };

  it("moves focus into the drawer and restores it on Escape", async () => {
    /*
     * It declared `role="dialog"` and `aria-modal="true"` and implemented none of it — focus never
     * entered, Tab walked onto the page behind, Escape did nothing, and focus was never restored.
     * Claiming the background is inert without making it inert is worse than omitting the attribute,
     * because assistive tech believes it (audit S1-2).
     */
    render(<AppShell {...shellProps}><p>page</p></AppShell>);

    const menu = screen.getByRole("button", { name: "Open navigation" });
    menu.focus();
    await userEvent.click(menu);

    const drawer = await screen.findByRole("dialog", { name: "Navigation" });
    await waitFor(() => expect(drawer.contains(document.activeElement)).toBe(true));

    await userEvent.keyboard("{Escape}");
    await waitFor(() => expect(screen.queryByRole("dialog", { name: "Navigation" })).not.toBeInTheDocument());
    await waitFor(() => expect(document.activeElement).toBe(menu));
  });
});

describe("the shell's landmarks", () => {
  it("names its navigation and offers a skip link", () => {
    render(<AppShell roles={["SuperAdmin"]} name="Ops" summary={null}><p>page</p></AppShell>);
    expect(screen.getByRole("navigation", { name: "Admin navigation" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Skip to content" })).toHaveAttribute("href", "#main");
    expect(screen.getByRole("main")).toHaveAttribute("id", "main");
  });
});
