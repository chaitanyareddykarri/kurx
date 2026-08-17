import { render, screen, waitFor } from "@testing-library/react";
import { describe, expect, it, vi, beforeEach } from "vitest";

const detect = vi.fn();
const artworkMap = vi.fn();

vi.mock("next/navigation", () => ({ useRouter: () => ({ refresh: vi.fn(), push: vi.fn() }) }));
vi.mock("@/lib/certificate-actions", () => ({
  detectTemplateTextAction: (...a: unknown[]) => detect(...a),
  getArtworkMapAction: (...a: unknown[]) => artworkMap(...a),
  getArtworkColourAction: vi.fn(),
  saveFieldsAction: vi.fn(),
  updateTemplateAction: vi.fn(),
}));

import { CertificateTemplateEditor } from "@/components/host/certificates/certificate-template-editor";
import type { CertificateTemplate } from "@/lib/certificate-api";

/**
 * The editor reads an uploaded design without being asked (D-358).
 *
 * The defect these exist to prevent is not a broken reader — it is a reader that is never *called*. The
 * detection action, the layout, the confirmation banner and its Add button all shipped and all worked;
 * nothing in the interface ever invoked any of it, and the branch meant to expose it rendered a heading
 * reading "Assisted" and no control. So a creator whose certificate already printed a name, a date and a
 * course was shown an empty panel and asked to retype and reposition each one by hand — exactly the work
 * the reader exists to remove.
 */

const template = (over: Partial<CertificateTemplate> = {}): CertificateTemplate =>
  ({
    id: "t-1",
    event_id: "e-1",
    owner_user_id: "u-1",
    name: "Certificate design",
    page_size: "a4-landscape",
    page_width_mm: 297,
    page_height_mm: 210,
    status: "draft",
    version: 1,
    background_storage_key: "k",
    background_url: "http://example.test/art.jpg",
    background_width: 3508,
    background_height: 2480,
    has_issued: false,
    fields: [],
    created_at: "2026-08-16T00:00:00Z",
    updated_at: "2026-08-16T00:00:00Z",
    ...over,
  }) as CertificateTemplate;

const reading = (text: string) => ({
  available: true,
  reason: null,
  regions: [
    { text, x: 20, y: 40, width: 60, height: 6, confidence: 0.95, ground: "#FFFFFF", lines: 1 },
  ],
});

beforeEach(() => {
  detect.mockReset();
  artworkMap.mockReset().mockResolvedValue(null);
});

describe("opening a design that has never been laid out", () => {
  it("reads it, without anyone asking", async () => {
    detect.mockResolvedValue(reading("OLIVER SMITH"));

    render(<CertificateTemplateEditor template={template()} canManage />);

    await waitFor(() => expect(detect).toHaveBeenCalled());
  });

  it("puts what it found on the certificate rather than an empty Add field prompt", async () => {
    detect.mockResolvedValue(reading("who has successfully completed"));

    render(<CertificateTemplateEditor template={template()} canManage />);

    await waitFor(() =>
      expect(screen.getByText(/Found 1 piece of text you can change/i)).toBeTruthy());
    expect(screen.queryByText(/Upload your certificate/i)).toBeNull();
  });

  /** Reading is help, not a rewrite. Once anything has been placed there is something to lose, so a
   *  re-read is offered instead of performed. */
  it("leaves a design that already has fields alone", async () => {
    detect.mockResolvedValue(reading("OLIVER SMITH"));
    const laid = template({
      fields: [{
        id: "f-1", kind: "text", field_key: null, label: null, static_text: "Signed",
        x: 10, y: 80, width: 30, height: 5, rotation: 0, z_order: 0,
        is_required: false, is_masking: false, mirrors_artwork: false, background_color: null,
        font_family: "sans", font_size_pt: 12, font_weight: "normal", font_style: null,
        underline: false, line_height: null, letter_spacing: null, color: "#000",
        horizontal_alignment: "left", vertical_alignment: "middle",
      }],
    } as Partial<CertificateTemplate>);

    render(<CertificateTemplateEditor template={laid} canManage />);

    // The availability probe still runs; what must not happen is the reading being applied over the top.
    await waitFor(() => expect(detect).toHaveBeenCalled());
    expect(screen.queryByText(/Found \d+ piece/i)).toBeNull();
  });

  /** A design with no artwork has nothing to read, and a viewer has nothing to change. */
  it("does not read a template with no design uploaded", async () => {
    detect.mockResolvedValue({ available: true, reason: null, regions: [] });

    render(<CertificateTemplateEditor template={template({ background_url: null })} canManage={false} />);

    await new Promise((r) => setTimeout(r, 20));
    expect(screen.queryByText(/Found \d+ piece/i)).toBeNull();
  });

  /** A deployment with no engine must be exactly the editor it was, with no failure reported at someone
   *  who never invoked the feature. */
  it("says nothing when this deployment has no reader", async () => {
    detect.mockResolvedValue({ available: false, reason: "Text detection is not configured.", regions: [] });

    render(<CertificateTemplateEditor template={template()} canManage />);

    await waitFor(() => expect(detect).toHaveBeenCalled());
    expect(screen.queryByText(/not available on this deployment/i)).toBeNull();
  });
});
