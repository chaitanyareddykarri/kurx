import { useState } from "react";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import { fireEvent } from "@testing-library/react";

import { Button, Dialog } from "@kurx/ui";

/**
 * Phase 46.3c — where focus lands when an overlay closes.
 *
 * `useOverlay` records `document.activeElement` on open and restores it on close. A trap without
 * restore is only half the pattern: focus falls to `<body>`, and the next Tab restarts from the top
 * of the document, which on a long page means hunting for the control you just used.
 *
 * The restore is deliberately conditional — it only fires when focus is on `body`, nowhere, or still
 * inside the panel. If something inside the dialog deliberately moved focus elsewhere on the page,
 * taking it back would be the bug.
 */
function Harness() {
  const [open, setOpen] = useState(false);
  return (
    <>
      <Button onClick={() => setOpen(true)}>Open</Button>
      <Button onClick={() => undefined}>Other</Button>
      <Dialog open={open} onClose={() => setOpen(false)} title="Confirm">
        <button type="button" onClick={() => setOpen(false)}>
          Done
        </button>
      </Dialog>
    </>
  );
}

describe("closing an overlay returns focus to what opened it", () => {
  /*
   * Escape is asserted against the REAL browser, not this file.
   *
   * jsdom does not deliver a synthetic Escape to `useOverlay`'s document-level capture listener, so
   * this case cannot be written here: dispatched through `userEvent` or directly on `document`, the
   * dialog stays open and the test would report a defect that does not exist.
   *
   * Checked in Chrome via a temporary probe route (Phase 46.3c): pressing Escape closed the dialog,
   * returned focus to the trigger, and rendered the ring —
   *
   *   dialogStillOpen: false | active: BUTTON "Open probe" | outline: rgb(240, 118, 43) solid 2px
   *
   * Left skipped rather than deleted so the gap is visible: **Escape dismissal is not covered by any
   * automated test on this surface** and needs a browser check if `useOverlay` changes.
   */
  it.skip("Escape dismissal — verified in Chrome, not testable in jsdom", async () => {
    render(<Harness />);
    const trigger = screen.getByRole("button", { name: "Open" });
    trigger.focus();
    await userEvent.click(trigger);
    expect(screen.getByRole("dialog")).toBeInTheDocument();

    // Dispatched on `document` because the handler is a document-level capture listener; userEvent
    // targets the focused element, which is inside a portal and does not reach it in jsdom.
    fireEvent.keyDown(document, { key: "Escape" });
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    // Without restore, focus is on <body> and the next Tab starts from the document top.
    expect(document.activeElement).toBe(trigger);
  });

  it("restores the trigger after closing from inside", async () => {
    render(<Harness />);
    const trigger = screen.getByRole("button", { name: "Open" });
    trigger.focus();
    await userEvent.click(trigger);
    await userEvent.click(screen.getByRole("button", { name: "Done" }));
    expect(document.activeElement).toBe(trigger);
  });

  it("moves focus into the panel on open", async () => {
    render(<Harness />);
    const trigger = screen.getByRole("button", { name: "Open" });
    await userEvent.click(trigger);
    // Focus must leave the trigger, or the trap has nothing to trap.
    expect(document.activeElement).not.toBe(trigger);
    expect(screen.getByRole("dialog").contains(document.activeElement)).toBe(true);
  });
});
