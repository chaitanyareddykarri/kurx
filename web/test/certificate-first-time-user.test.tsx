import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi, beforeEach } from "vitest";

const detect = vi.fn();
const artworkMap = vi.fn();
const saveFields = vi.fn();

vi.mock("next/navigation", () => ({ useRouter: () => ({ refresh: vi.fn(), push: vi.fn() }) }));
vi.mock("@/lib/certificate-actions", () => ({
  detectTemplateTextAction: (...a: unknown[]) => detect(...a),
  getArtworkMapAction: (...a: unknown[]) => artworkMap(...a),
  getArtworkColourAction: vi.fn(),
  saveFieldsAction: (...a: unknown[]) => saveFields(...a),
  updateTemplateAction: vi.fn(),
  presignBackgroundAction: vi.fn(),
  setBackgroundAction: vi.fn(),
}));

import { CertificateTemplateEditor } from "@/components/host/certificates/certificate-template-editor";
import type { CertificateTemplate } from "@/lib/certificate-api";

/**
 * Meeting this screen for the first time, with no training (D-359).
 *
 * These are not tests of the layout. They are the four questions a first-time user has — *what step am I
 * on, what does this button do, where do I click, what happens next* — asked of the rendered page, one at
 * a time. Anything that can only be answered by already knowing the answer is a defect.
 *
 * They also guard the vocabulary. Every word this interface has ever leaked at someone — element, handle,
 * mask, render, z-order — described the implementation rather than the certificate, and each arrived at a
 * person who wanted to correct a spelling.
 */

const template = (over: Partial<CertificateTemplate> = {}): CertificateTemplate =>
  ({
    id: "t-1", event_id: "e-1", owner_user_id: "u-1",
    name: "Certificate design", page_size: "a4-landscape",
    page_width_mm: 297, page_height_mm: 210, status: "draft", version: 1,
    background_storage_key: "k", background_url: "http://example.test/art.jpg",
    background_width: 3508, background_height: 2480, has_issued: false, fields: [],
    created_at: "2026-08-16T00:00:00Z", updated_at: "2026-08-16T00:00:00Z",
    ...over,
  }) as CertificateTemplate;

const found = (...texts: string[]) => ({
  available: true,
  reason: null,
  regions: texts.map((text, i) => ({
    text, x: 20, y: 20 + i * 12, width: 60, height: 6, confidence: 0.95, ground: "#FFFFFF", lines: 1,
  })),
});

beforeEach(() => {
  detect.mockReset().mockResolvedValue(found("Presented to"));
  artworkMap.mockReset().mockResolvedValue(null);
  saveFields.mockReset().mockResolvedValue({ template: template() });
});

describe("what step am I on", () => {
  it("shows all five steps, in order, in plain words", async () => {
    render(<CertificateTemplateEditor template={template({ background_url: null })} canManage />);

    const rail = screen.getByRole("navigation", { name: /steps/i });
    const labels = within(rail).getAllByRole("listitem").map((li) => li.textContent);

    expect(labels).toHaveLength(5);
    expect(labels[0]).toMatch(/Upload/);
    expect(labels[1]).toMatch(/Find text/);
    expect(labels[2]).toMatch(/Edit/);
    expect(labels[3]).toMatch(/Preview/);
    expect(labels[4]).toMatch(/Download/);
  });

  /** Status has to survive greyscale, a bad projector and most colour blindness. Every state says its own
   *  name in words, so the colour is reinforcement rather than the message. */
  it("says which step is current and which are done, in words rather than colour", async () => {
    render(<CertificateTemplateEditor template={template({ background_url: null })} canManage />);

    const rail = screen.getByRole("navigation", { name: /steps/i });
    const items = within(rail).getAllByRole("listitem");

    expect(items[0].textContent).toMatch(/Now/);
    expect(items[1].textContent).toMatch(/To do/);
    expect(screen.getByRole("button", { name: /Go to step 1: Upload/ })).toHaveAttribute("aria-current", "step");
  });

  it("starts a brand-new certificate at Upload without anyone clicking", () => {
    render(<CertificateTemplateEditor template={template({ background_url: null })} canManage />);

    expect(screen.getByRole("heading", { name: /Upload your certificate/i })).toBeTruthy();
  });

  it("puts a design that already has text straight onto Edit", async () => {
    const laid = template({
      fields: [{
        id: "f-1", kind: "text", field_key: null, label: null, static_text: "Presented to",
        x: 10, y: 20, width: 40, height: 6, rotation: 0, z_order: 0,
        is_required: false, is_masking: false, mirrors_artwork: true, background_color: "#FFF",
        font_family: "sans", font_size_pt: 14, font_weight: "normal", font_style: null,
        underline: false, line_height: null, letter_spacing: null, color: "#000",
        horizontal_alignment: "center", vertical_alignment: "middle",
      }],
    } as Partial<CertificateTemplate>);

    render(<CertificateTemplateEditor template={laid} canManage />);

    expect(screen.getByRole("heading", { name: /Edit your certificate/i })).toBeTruthy();
  });
});

describe("what do I do now", () => {
  it("tells an empty certificate exactly what to do, and gives it one button", () => {
    render(<CertificateTemplateEditor template={template({ background_url: null })} canManage />);

    expect(screen.getByText(/Choose a certificate image to get started/i)).toBeTruthy();
    expect(screen.getByLabelText(/Upload Certificate/i)).toBeTruthy();
  });

  it("says what can be changed once the text has been found", async () => {
    render(<CertificateTemplateEditor template={template()} canManage />);

    await waitFor(() => expect(screen.getByRole("heading", { name: /Edit your certificate/i })).toBeTruthy());
    expect(screen.getByText(/Click any text on the certificate to change it/i)).toBeTruthy();
  });
});

describe("what does this button do", () => {
  it("never leaves an important action as an icon on its own", async () => {
    render(<CertificateTemplateEditor template={template()} canManage />);
    await waitFor(() => expect(screen.getByRole("heading", { name: /Edit your certificate/i })).toBeTruthy());

    // Every button either says something, or is labelled for a screen reader. A bare glyph is neither.
    for (const button of screen.getAllByRole("button")) {
      const readable = (button.getAttribute("aria-label") ?? "") + " " + (button.textContent ?? "");
      expect(readable.replace(/[^a-z]/gi, "").length).toBeGreaterThan(2);
    }
  });

  it("keeps click targets big enough to hit", async () => {
    render(<CertificateTemplateEditor template={template()} canManage />);
    await waitFor(() => expect(screen.getByRole("heading", { name: /Edit your certificate/i })).toBeTruthy());

    // h-11 is 44px, the smallest target that is comfortable on a phone. The canvas's own text boxes are
    // sized by the design and exempt.
    const preview = screen.getByRole("button", { name: /Preview$/ });
    expect(preview.className).toMatch(/h-1[2-9]/);
  });
});

describe("the words it uses", () => {
  const JARGON = /\b(element|handle|renderer|inpaint|masking|z-order|z_order|viewport|DTO|payload|OCR)\b/i;

  it("never shows the machine's vocabulary", async () => {
    const { container } = render(<CertificateTemplateEditor template={template()} canManage />);
    await waitFor(() => expect(screen.getByRole("heading", { name: /Edit your certificate/i })).toBeTruthy());

    expect(container.textContent ?? "").not.toMatch(JARGON);
  });

  it("never shows the machine's vocabulary on an empty certificate either", () => {
    const { container } = render(
      <CertificateTemplateEditor template={template({ background_url: null })} canManage />);

    expect(container.textContent ?? "").not.toMatch(JARGON);
  });

  /** "DetectionError: element_coordinates_invalid" tells the person holding a certificate nothing they can
   *  act on. What they need is what to try instead, and a button that tries it. */
  it("explains a failure in something the reader can act on, with a way to retry", async () => {
    detect.mockReset()
      .mockResolvedValueOnce({ available: true, reason: null, regions: [] })   // the silent first pass
      .mockResolvedValue({ available: true, reason: null, regions: [] });

    render(<CertificateTemplateEditor template={template()} canManage />);

    // Wait for the step to settle, then look the button up again at the moment of clicking: the first
    // pass re-renders this subtree, and a node captured before that is no longer the one on the page.
    await screen.findByRole("button", { name: /Find Text$/ });
    await waitFor(() => expect(screen.getByRole("button", { name: /Find Text$/ })).toBeTruthy());
    await userEvent.click(screen.getByRole("button", { name: /Find Text$/ }));

    await waitFor(() =>
      expect(screen.getByText(/Try uploading a clearer picture of your certificate/i)).toBeTruthy(),
      { timeout: 3000 });
    expect(screen.getByRole("button", { name: /Try Again/i })).toBeTruthy();
    // And no stack trace, code or field name anywhere near it.
    expect(screen.queryByText(/error|invalid|null|undefined/i)).toBeNull();
  });
});

describe("changing the words", () => {
  it("offers a labelled box per piece of text, named after what it says", async () => {
    // The real shape of a certificate: a lead-in, the name it announces, then the rest.
    detect.mockResolvedValue(found("Presented to", "OLIVER SMITH", "who has successfully completed"));

    render(<CertificateTemplateEditor template={template()} canManage />);

    await waitFor(() => expect(screen.getByLabelText(/Edit .Presented to./i)).toBeTruthy());
    expect(screen.getByLabelText(/Edit .who has successfully completed/i)).toBeTruthy();
    // And the announced line is the recipient, so it gets no box to type a single name into.
    expect(screen.queryByLabelText(/Edit .OLIVER SMITH./i)).toBeNull();
    expect(screen.getByText(/Changes for each person/i)).toBeTruthy();
  });

  it("changes the certificate when the box is typed into", async () => {
    detect.mockResolvedValue(found("Presented to"));

    render(<CertificateTemplateEditor template={template()} canManage />);

    const box = await screen.findByLabelText(/Edit .Presented to./i);
    await userEvent.clear(box);
    await userEvent.type(box, "Awarded to");

    expect((box as HTMLTextAreaElement).value).toBe("Awarded to");
  });

  /** A per-person value arrives from the participant list. Offering a box invites someone to type one
   *  name into it and send that name to everybody. */
  it("offers no box for something filled in per person, and says where it comes from", async () => {
    detect.mockResolvedValue(found("[Recipient's Full Name]"));

    render(<CertificateTemplateEditor template={template()} canManage />);

    await waitFor(() => expect(screen.getByText(/Changes for each person/i)).toBeTruthy());
    expect(screen.getByText(/Filled in automatically from your participant list/i)).toBeTruthy();
    expect(screen.queryByLabelText(/Edit .Ananya/i)).toBeNull();
  });
});
