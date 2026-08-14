import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { useState } from "react";
import { describe, expect, it } from "vitest";

import { CommandPalette, ConfirmDialog, Dialog, Menu, Sheet } from "@kurx/ui";

/**
 * Guards on the overlay contract (`docs/ui-ux/accessibility-foundation.md` §6).
 *
 * Audit S1-2: Dialog and Sheet both asserted aria-modal="true" while trapping
 * nothing — focus never entered, Tab walked onto the still-operable page behind,
 * focus was never restored, and the body kept scrolling. ConfirmDialog composes
 * Dialog, so every destructive confirmation in web and admin inherited it.
 *
 * Audit S1-3: Sheet's name was `typeof title === "string" ? title : undefined`,
 * so a ReactNode title — the normal case in the admin workspaces — produced an
 * unnamed dialog.
 */

function DialogHarness({ title = "Confirm removal" }: { title?: React.ReactNode }) {
  const [open, setOpen] = useState(false);
  return (
    <>
      <button type="button" onClick={() => setOpen(true)}>
        open
      </button>
      <button type="button">outside</button>
      <Dialog open={open} onClose={() => setOpen(false)} title={title}>
        <button type="button">inside one</button>
        <button type="button">inside two</button>
      </Dialog>
    </>
  );
}

describe("Dialog — the S1-2 contract", () => {
  it("names itself from the rendered heading, not a duplicated string", () => {
    render(<Dialog open onClose={() => {}} title="Remove attendee" children={<p>body</p>} />);
    const dialog = screen.getByRole("dialog", { name: "Remove attendee" });
    // aria-labelledby, so the name cannot drift from what is on screen.
    expect(dialog).toHaveAttribute("aria-labelledby");
    expect(dialog).not.toHaveAttribute("aria-label");
  });

  it("moves focus into the panel on open", async () => {
    render(<DialogHarness />);
    await userEvent.click(screen.getByRole("button", { name: "open" }));
    await waitFor(() => {
      expect(screen.getByRole("dialog")).toContainElement(document.activeElement as HTMLElement);
    });
  });

  it("cycles Tab inside the panel instead of escaping to the page behind", async () => {
    render(<DialogHarness />);
    await userEvent.click(screen.getByRole("button", { name: "open" }));
    const dialog = await screen.findByRole("dialog");

    // Walk forward past the last focusable and confirm we stay inside.
    for (let i = 0; i < 6; i++) {
      await userEvent.tab();
      expect(dialog).toContainElement(document.activeElement as HTMLElement);
    }
  });

  it("restores focus to whatever opened it", async () => {
    render(<DialogHarness />);
    const trigger = screen.getByRole("button", { name: "open" });
    await userEvent.click(trigger);
    await screen.findByRole("dialog");
    await userEvent.keyboard("{Escape}");
    await waitFor(() => expect(trigger).toHaveFocus());
  });

  it("closes on Escape", async () => {
    render(<DialogHarness />);
    await userEvent.click(screen.getByRole("button", { name: "open" }));
    await screen.findByRole("dialog");
    await userEvent.keyboard("{Escape}");
    await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
  });

  it("locks body scroll while open and releases it after", async () => {
    render(<DialogHarness />);
    await userEvent.click(screen.getByRole("button", { name: "open" }));
    await screen.findByRole("dialog");
    expect(document.body.style.overflow).toBe("hidden");
    await userEvent.keyboard("{Escape}");
    await waitFor(() => expect(document.body.style.overflow).not.toBe("hidden"));
  });

  it("exposes an accessible close control", () => {
    render(<Dialog open onClose={() => {}} title="Remove" children={<p>body</p>} />);
    expect(screen.getByRole("button", { name: "Close" })).toBeInTheDocument();
  });
});

describe("Sheet — the S1-3 contract", () => {
  it("is named even when the title is a ReactNode", () => {
    // The admin workspaces all pass nodes; this used to yield an unnamed dialog.
    render(
      <Sheet
        open
        onClose={() => {}}
        title={
          <span>
            Event <strong>details</strong>
          </span>
        }
      >
        <p>body</p>
      </Sheet>
    );
    expect(screen.getByRole("dialog", { name: /Event details/ })).toBeInTheDocument();
  });

  it("traps focus like Dialog", async () => {
    render(
      <Sheet open onClose={() => {}} title="Filters">
        <button type="button">apply</button>
      </Sheet>
    );
    const sheet = screen.getByRole("dialog");
    await waitFor(() => expect(sheet).toContainElement(document.activeElement as HTMLElement));
    await userEvent.tab();
    expect(sheet).toContainElement(document.activeElement as HTMLElement);
  });
});

describe("ConfirmDialog", () => {
  it("inherits the trap, and uses the shared destructive treatment", async () => {
    // The danger button used to be hand-rolled as `bg-danger text-white`, which
    // is 3.29:1 on the dark-theme danger token.
    render(
      <ConfirmDialog
        open
        onClose={() => {}}
        onConfirm={() => {}}
        title="Blacklist user"
        tone="danger"
        confirmLabel="Blacklist"
      />
    );
    expect(screen.getByRole("dialog", { name: "Blacklist user" })).toBeInTheDocument();
    const confirm = screen.getByRole("button", { name: "Blacklist" });
    expect(confirm.className).toContain("bg-danger");
    expect(confirm.className).not.toContain("text-white");
  });

  it("states the consequence by default rather than assuming the caller did", () => {
    render(<ConfirmDialog open onClose={() => {}} onConfirm={() => {}} title="Delete" />);
    expect(screen.getByText(/cannot be undone/i)).toBeInTheDocument();
  });
});

describe("Menu — the dropdown contract", () => {
  const items = [
    { label: "Profile", onSelect: () => {} },
    { label: "Settings", onSelect: () => {} },
    { label: "Sign out", onSelect: () => {}, destructive: true }
  ];

  function MenuHarness() {
    return (
      <Menu
        items={items}
        trigger={(p) => (
          <button type="button" {...p}>
            Account
          </button>
        )}
      />
    );
  }

  it("declares expanded state and popup type on the trigger", async () => {
    render(<MenuHarness />);
    const trigger = screen.getByRole("button", { name: "Account" });
    expect(trigger).toHaveAttribute("aria-haspopup", "menu");
    expect(trigger).toHaveAttribute("aria-expanded", "false");
    await userEvent.click(trigger);
    expect(trigger).toHaveAttribute("aria-expanded", "true");
  });

  it("is a single tab stop — arrows move, Tab does not walk every item", async () => {
    render(<MenuHarness />);
    await userEvent.click(screen.getByRole("button", { name: "Account" }));
    const menuitems = screen.getAllByRole("menuitem");
    // Exactly one item is tabbable at a time; that is what role=menu promises.
    expect(menuitems.filter((el) => el.getAttribute("tabindex") === "0")).toHaveLength(1);
  });

  it("moves the active item with ArrowDown", async () => {
    render(<MenuHarness />);
    await userEvent.click(screen.getByRole("button", { name: "Account" }));
    await waitFor(() => expect(screen.getByRole("menuitem", { name: "Profile" })).toHaveFocus());
    await userEvent.keyboard("{ArrowDown}");
    await waitFor(() => expect(screen.getByRole("menuitem", { name: "Settings" })).toHaveFocus());
  });

  it("closes on Escape and returns focus to the trigger", async () => {
    render(<MenuHarness />);
    const trigger = screen.getByRole("button", { name: "Account" });
    await userEvent.click(trigger);
    await screen.findByRole("menu");
    await userEvent.keyboard("{Escape}");
    await waitFor(() => expect(trigger).toHaveFocus());
  });

  it("marks a destructive item in text, not only colour", async () => {
    render(<MenuHarness />);
    await userEvent.click(screen.getByRole("button", { name: "Account" }));
    expect(screen.getByRole("menuitem", { name: /Sign out.*destructive/i })).toBeInTheDocument();
  });
});

describe("CommandPalette", () => {
  const items = [
    { id: "a", label: "Create event", group: "Actions", onSelect: () => {} },
    { id: "b", label: "My tickets", group: "Go to", onSelect: () => {} }
  ];

  it("uses the combobox pattern so typing and arrowing do not compete", () => {
    render(<CommandPalette open onClose={() => {}} items={items} />);
    const input = screen.getByRole("combobox");
    expect(input).toHaveAttribute("aria-expanded", "true");
    // The virtual cursor moves; DOM focus stays in the input.
    expect(input).toHaveAttribute("aria-activedescendant");
    expect(screen.getByRole("listbox", { name: "Results" })).toBeInTheDocument();
  });

  it("announces the result count", () => {
    render(<CommandPalette open onClose={() => {}} items={items} />);
    expect(screen.getByRole("status")).toHaveTextContent("2 results");
  });

  it("says what to do next when nothing matches", async () => {
    render(<CommandPalette open onClose={() => {}} items={items} />);
    await userEvent.type(screen.getByRole("combobox"), "zzzz");
    expect(screen.getByText(/Try a different word/i)).toBeInTheDocument();
    expect(screen.getByRole("status")).toHaveTextContent("0 results");
  });
});
