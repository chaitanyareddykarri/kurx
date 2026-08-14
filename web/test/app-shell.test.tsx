import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";

vi.mock("next/navigation", () => ({ usePathname: () => "/posts", useRouter: () => ({ push: vi.fn() }) }));

import { MobileNav } from "@/components/layout/mobile-nav";
import { SidebarNav } from "@/components/layout/sidebar-nav";
import { primaryNav, secondaryNav } from "@/components/layout/nav-config";

/**
 * Guards on the application shell (Phase 11).
 *
 * The defect these exist to prevent recurring: the shell's only navigation was a
 * sidebar marked `hidden … lg:block`, with no drawer, bottom bar or hamburger
 * anywhere else, so below 1024px the entire signed-in application was
 * unreachable except by typing URLs.
 */

describe("MobileNav — navigation must exist below lg", () => {
  it("renders every primary area as a real link", () => {
    render(<MobileNav />);
    for (const item of primaryNav) {
      expect(screen.getByRole("link", { name: new RegExp(item.label) })).toHaveAttribute("href", item.href);
    }
  });

  it("marks the current area", () => {
    render(<MobileNav />);
    // usePathname is mocked to /posts.
    expect(screen.getByRole("link", { name: /Posts/ })).toHaveAttribute("aria-current", "page");
    expect(screen.getByRole("link", { name: /Home/ })).not.toHaveAttribute("aria-current");
  });

  it("reaches every secondary destination through More", async () => {
    render(<MobileNav />);
    await userEvent.click(screen.getByRole("button", { name: /More/ }));
    const sheet = await screen.findByRole("dialog", { name: "More" });
    for (const item of secondaryNav) {
      expect(screen.getByRole("link", { name: item.label })).toHaveAttribute("href", item.href);
    }
    // Inherits the overlay contract from Sheet.
    await waitFor(() => expect(sheet).toContainElement(document.activeElement as HTMLElement));
  });

  it("shows no admin surface — to anyone, including a platform admin", async () => {
    // This app is the user account. One number can be both a user and a SuperAdmin, and web used to
    // grow a "Platform review" section for the second identity; the console has its own sign-in.
    // There is no longer a prop that could turn it back on.
    render(<MobileNav />);
    await userEvent.click(screen.getByRole("button", { name: /More/ }));
    await screen.findByRole("dialog", { name: "More" });
    for (const label of ["Review", "Fraud", "Org tools"]) {
      expect(screen.queryByRole("link", { name: label })).not.toBeInTheDocument();
    }
    expect(screen.queryByRole("navigation", { name: "Platform review" })).not.toBeInTheDocument();
  });

  it("declares the More trigger as opening a dialog", () => {
    render(<MobileNav />);
    const trigger = screen.getByRole("button", { name: /More/ });
    expect(trigger).toHaveAttribute("aria-haspopup", "dialog");
    expect(trigger).toHaveAttribute("aria-expanded", "false");
  });
});

describe("SidebarNav", () => {
  it("separates the primary map from the secondary list as named landmarks", () => {
    render(<SidebarNav />);
    expect(screen.getByRole("navigation", { name: "Primary" })).toBeInTheDocument();
    expect(screen.getByRole("navigation", { name: "Secondary" })).toBeInTheDocument();
  });

  it("marks the current page — previously no item indicated position at all", () => {
    render(<SidebarNav />);
    expect(screen.getByRole("link", { name: "Posts" })).toHaveAttribute("aria-current", "page");
  });

  it("carries no Platform review group at all", () => {
    render(<SidebarNav />);
    expect(screen.queryByRole("navigation", { name: "Platform review" })).not.toBeInTheDocument();
    expect(screen.queryByRole("link", { name: /Review/ })).not.toBeInTheDocument();
  });

  it("renders the same areas as the mobile bar, from one source", () => {
    // Two hardcoded copies of the nav would drift; both read nav-config.
    render(<SidebarNav />);
    for (const item of primaryNav) {
      expect(screen.getByRole("link", { name: item.label })).toHaveAttribute("href", item.href);
    }
  });
});
