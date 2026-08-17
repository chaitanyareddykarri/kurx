import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";

import { CertificateCanvas } from "@/components/host/certificates/certificate-canvas";
import { newField } from "@/lib/certificate-editor";
import type { DraftField } from "@/lib/certificate-editor";

/**
 * Clicking the words on the certificate and changing them (D-355).
 *
 * The one interaction the redesign exists for: a creator who can see text on their uploaded design
 * reaches for it directly, rather than placing a box beside it and covering the original by hand.
 *
 * The defect these guard against is not a broken handler — it is a canvas that *accepts* editing props
 * and is simply never given them, which is how this shipped looking finished while the editor offered no
 * way to edit text at all.
 */

const text = (over: Partial<DraftField> = {}): DraftField =>
  ({ ...newField("text"), static_text: "Certificate of Completion", x: 10, y: 20, width: 60, height: 8, ...over });

function setup(fields: DraftField[], props: Partial<React.ComponentProps<typeof CertificateCanvas>> = {}) {
  const onStartEditing = vi.fn();
  const onTextChange = vi.fn();
  render(
    <CertificateCanvas
      pageSize="a4-landscape"
      fields={fields}
      width={800}
      interactive
      onStartEditing={onStartEditing}
      onTextChange={onTextChange}
      {...props}
    />
  );
  return { onStartEditing, onTextChange };
}

describe("editing text on the design", () => {
  it("double-clicking a piece of text asks to edit it", async () => {
    const field = text();
    const { onStartEditing } = setup([field]);

    await userEvent.dblClick(screen.getByRole("button", { name: /field/i }));

    expect(onStartEditing).toHaveBeenCalledWith(field.id);
  });

  it("puts a caret in the text once editing starts", () => {
    const field = text();
    setup([field], { editingId: field.id, selectedId: field.id });

    const editable = screen.getByText("Certificate of Completion");
    expect(editable.getAttribute("contenteditable")).toBe("true");
  });

  it("reports what was typed when the creator leaves the text", async () => {
    const field = text();
    const { onTextChange } = setup([field], { editingId: field.id, selectedId: field.id });

    const editable = screen.getByText("Certificate of Completion");
    editable.textContent = "Certificate of Attendance";
    editable.blur();

    expect(onTextChange).toHaveBeenCalledWith(field.id, "Certificate of Attendance");
  });

  /**
   * A dynamic field draws a *sample* of what gets filled in per recipient. Typing over that sample would
   * quietly break the binding and print one person's name on every certificate in the run — so the
   * gesture is refused here and the change is made deliberately, in the panel.
   */
  it("refuses to let a per-recipient field be typed over", async () => {
    const field = { ...newField("dynamicfield", { fieldKey: "participant_name", label: "Participant name" }), x: 10, y: 20, width: 60, height: 8 };
    const { onStartEditing } = setup([field]);

    await userEvent.dblClick(screen.getByRole("button", { name: /field/i }));

    expect(onStartEditing).not.toHaveBeenCalled();
  });

  /**
   * A handle on the artwork's own words draws nothing and fills nothing (D-356).
   *
   * The canvas has to agree with the renderer here. If it painted the fill, the creator would judge their
   * layout against a patch the certificate will not have; if it drew the text, they would see the words
   * twice — once in the browser's font, once in the design's.
   */
  it("draws nothing for text the artwork already prints", () => {
    setup([text({ mirrors_artwork: true, background_color: "#FDF6E3" })]);

    const box = screen.getByRole("button", { name: /field/i });
    expect(box.textContent).toBe("");
    expect(box.style.backgroundColor).toBe("");
  });

  it("still lets that text be reached and typed into", async () => {
    const field = text({ mirrors_artwork: true, background_color: "#FDF6E3" });
    const { onStartEditing } = setup([field]);

    await userEvent.dblClick(screen.getByRole("button", { name: /field/i }));

    expect(onStartEditing).toHaveBeenCalledWith(field.id);
  });

  /** An unselected canvas is the certificate, not a wireframe: no outlines, no handles, no field names. */
  it("shows no chrome around text nobody is working on", () => {
    setup([text()]);

    expect(screen.queryByRole("button", { name: "Resize" })).toBeNull();
    expect(screen.queryByText("Text")).toBeNull();
  });
});
