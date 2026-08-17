import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";

import {
  CUSTOM_PAGE_MAX_MM, CUSTOM_PAGE_MIN_MM, mmToPoints, pageAspect, squareOnPage, validateCustomPage
} from "@/lib/certificate-editor";
import { CertificateSizePicker } from "@/components/host/certificates/certificate-size-picker";
import type { PageSizePreset } from "@/lib/certificate-api";

/**
 * Certificate page sizes (D-361).
 *
 * The behaviours worth pinning are the ones a screenshot would not reveal: that the canvas takes its
 * shape from the millimetres rather than from a name it recognises (which is what makes a custom page
 * work at all), that turning a page over preserves its aspect exactly rather than approximately, and
 * that a half-typed dimension never reaches the canvas.
 */

const inch = 25.4;

const PRESETS: PageSizePreset[] = [
  { slug: "a4-portrait", family: "a4", label: "A4", landscape: false, width_mm: 210, height_mm: 297 },
  { slug: "a4-landscape", family: "a4", label: "A4", landscape: true, width_mm: 297, height_mm: 210 },
  { slug: "a5-portrait", family: "a5", label: "A5", landscape: false, width_mm: 148, height_mm: 210 },
  { slug: "a5-landscape", family: "a5", label: "A5", landscape: true, width_mm: 210, height_mm: 148 },
  { slug: "letter-portrait", family: "letter", label: "Letter", landscape: false, width_mm: 8.5 * inch, height_mm: 11 * inch },
  { slug: "letter-landscape", family: "letter", label: "Letter", landscape: true, width_mm: 11 * inch, height_mm: 8.5 * inch },
  { slug: "legal-portrait", family: "legal", label: "Legal", landscape: false, width_mm: 8.5 * inch, height_mm: 14 * inch },
  { slug: "legal-landscape", family: "legal", label: "Legal", landscape: true, width_mm: 14 * inch, height_mm: 8.5 * inch },
  { slug: "8x10-portrait", family: "8x10", label: "8 × 10 in", landscape: false, width_mm: 8 * inch, height_mm: 10 * inch },
  { slug: "8x10-landscape", family: "8x10", label: "8 × 10 in", landscape: true, width_mm: 10 * inch, height_mm: 8 * inch },
  { slug: "11x14-portrait", family: "11x14", label: "11 × 14 in", landscape: false, width_mm: 11 * inch, height_mm: 14 * inch },
  { slug: "11x14-landscape", family: "11x14", label: "11 × 14 in", landscape: true, width_mm: 14 * inch, height_mm: 11 * inch },
  { slug: "12x16-portrait", family: "12x16", label: "12 × 16 in", landscape: false, width_mm: 12 * inch, height_mm: 16 * inch },
  { slug: "12x16-landscape", family: "12x16", label: "12 × 16 in", landscape: true, width_mm: 16 * inch, height_mm: 12 * inch }
];

describe("page geometry", () => {
  it("takes the aspect from the millimetres, not from a name it recognises", () => {
    // The point of D-361: a page the client has never heard of still scales correctly.
    expect(pageAspect("custom", 400, 200)).toBe(2);
    expect(pageAspect("12x16-portrait", 304.8, 406.4)).toBeCloseTo(304.8 / 406.4, 6);
  });

  it("falls back to the name for a payload written before dimensions travelled with it", () => {
    expect(pageAspect("a4-portrait")).toBeCloseTo(210 / 297, 6);
    expect(pageAspect("a4-landscape")).toBeCloseTo(297 / 210, 6);
  });

  // Zero and negative would otherwise produce Infinity or a negative height, collapsing or inverting
  // the canvas rather than falling back.
  it.each([[0, 100], [100, 0], [-210, 297], [Number.NaN, 297]])(
    "ignores an unusable dimension pair (%s × %s)", (w, h) => {
      expect(pageAspect("a4-landscape", w, h)).toBeCloseTo(297 / 210, 6);
    });

  it("keeps a QR square on the page, whatever the page is", () => {
    // Width and height are percentages of DIFFERENT edges, so equal percentages are square only on a
    // square page — and a QR that is not square does not scan.
    const onWide = squareOnPage(10, "custom", 400, 200);
    expect(onWide.height).toBeCloseTo(10 * 2, 6);
    const onSquare = squareOnPage(10, "custom", 300, 300);
    expect(onSquare.height).toBeCloseTo(10, 6);
  });

  it("converts millimetres to points the way the renderer measures type", () => {
    expect(mmToPoints(25.4)).toBeCloseTo(72, 6);
    expect(mmToPoints(297)).toBeCloseTo(842, 0);   // A4 portrait height
    expect(mmToPoints(210)).toBeCloseTo(595, 0);   // A4 landscape height
  });
});

describe("custom dimension validation", () => {
  it("accepts a page inside the bounds, and the bounds themselves", () => {
    expect(validateCustomPage(200, 300)).toBeNull();
    expect(validateCustomPage(CUSTOM_PAGE_MIN_MM, CUSTOM_PAGE_MIN_MM)).toBeNull();
    expect(validateCustomPage(CUSTOM_PAGE_MAX_MM, CUSTOM_PAGE_MAX_MM)).toBeNull();
  });

  it.each([
    [0, 300], [-1, 300], [300, -1], [CUSTOM_PAGE_MIN_MM - 1, 300], [300, CUSTOM_PAGE_MAX_MM + 1]
  ])("refuses an impossible page (%s × %s)", (w, h) => {
    expect(validateCustomPage(w, h)).not.toBeNull();
  });

  // NaN fails every comparison, so a bare `< min || > max` lets it through — and a NaN page reaches
  // the renderer as a page of undefined size.
  it("refuses values a range check silently accepts", () => {
    expect(validateCustomPage(Number.NaN, 300)).toBe("Width needs to be a number.");
    expect(validateCustomPage(300, Number.POSITIVE_INFINITY)).toBe("Height needs to be a number.");
  });

  it("says what to do, in millimetres, rather than naming an error code", () => {
    expect(validateCustomPage(10, 300)).toBe("Width needs to be at least 50 mm.");
    expect(validateCustomPage(300, 5000)).toBe("Height can be at most 1000 mm.");
  });
});

describe("the size picker", () => {
  const onChange = vi.fn();
  beforeEach(() => onChange.mockClear());

  function picker(over: Partial<React.ComponentProps<typeof CertificateSizePicker>> = {}) {
    return render(
      <CertificateSizePicker
        presets={PRESETS} pageSize="a4-landscape" pageWidthMm={297} pageHeightMm={210}
        onChange={onChange} {...over}
      />
    );
  }

  it("offers every requested paper, once, with its measurements on the option", () => {
    picker();
    const options = screen.getAllByRole("option").map((o) => o.textContent ?? "");
    // One entry per paper — orientation is a separate control, so fourteen flat entries would make the
    // reader do the grouping themselves.
    for (const label of ["A4", "A5", "Letter", "Legal", "8 × 10 in", "11 × 14 in", "12 × 16 in"]) {
      expect(options.filter((o) => o.startsWith(label))).toHaveLength(1);
    }
    expect(options.some((o) => o.includes("Custom size"))).toBe(true);
    // Dimensions are on the option itself: nobody should need to know what Legal measures.
    expect(options.find((o) => o.startsWith("A4"))).toContain("210 × 297 mm");
    expect(options.find((o) => o.startsWith("Letter"))).toContain("215.9 × 279.4 mm");
  });

  it("states the current page in both units", () => {
    picker({ pageSize: "letter-portrait", pageWidthMm: 215.9, pageHeightMm: 279.4 });
    // Scoped to the summary line: the same measurement also appears on the Letter <option>, and a bare
    // text query cannot tell the two apart.
    const summary = screen.getByText(/This certificate is/).textContent ?? "";
    expect(summary).toContain("215.9 × 279.4 mm");
    expect(summary).toContain("8.5 × 11 in");
  });

  it("keeps the orientation when the paper changes", async () => {
    const user = userEvent.setup();
    picker({ pageSize: "a4-landscape" });
    await user.selectOptions(screen.getByLabelText("Paper"), "legal");
    // Landscape in, landscape out — changing paper is not a reason to flip the page over.
    expect(onChange).toHaveBeenCalledWith({ pageSize: "legal-landscape" });
  });

  it("switches orientation within the same paper", async () => {
    const user = userEvent.setup();
    picker({ pageSize: "a5-landscape", pageWidthMm: 210, pageHeightMm: 148 });
    await user.click(screen.getByRole("button", { name: "Portrait" }));
    expect(onChange).toHaveBeenCalledWith({ pageSize: "a5-portrait" });
  });

  it("shows which orientation is current, and not by colour alone", () => {
    picker({ pageSize: "a4-portrait", pageWidthMm: 210, pageHeightMm: 297 });
    expect(screen.getByRole("button", { name: "Portrait" })).toHaveAttribute("aria-pressed", "true");
    expect(screen.getByRole("button", { name: "Landscape" })).toHaveAttribute("aria-pressed", "false");
  });

  it("seeds Custom from the page in hand rather than an arbitrary default", async () => {
    const user = userEvent.setup();
    picker({ pageSize: "a5-portrait", pageWidthMm: 148, pageHeightMm: 210 });
    await user.selectOptions(screen.getByLabelText("Paper"), "custom");
    expect(onChange).toHaveBeenCalledWith({ pageSize: "custom", pageWidthMm: 148, pageHeightMm: 210 });
  });

  it("turning a custom page over swaps its sides, preserving the aspect exactly", async () => {
    const user = userEvent.setup();
    picker({ pageSize: "custom", pageWidthMm: 400, pageHeightMm: 200 });
    await user.click(screen.getByRole("button", { name: "Portrait" }));
    expect(onChange).toHaveBeenCalledWith({ pageSize: "custom", pageWidthMm: 200, pageHeightMm: 400 });
  });

  it("reports an invalid custom dimension and does not push it upward", async () => {
    const user = userEvent.setup();
    picker({ pageSize: "custom", pageWidthMm: 200, pageHeightMm: 200 });
    onChange.mockClear();

    const width = screen.getByLabelText(/Width \(mm\)/);
    await user.clear(width);
    await user.type(width, "5");

    expect(await screen.findByRole("alert")).toHaveTextContent("Width needs to be at least 50 mm.");
    // A half-typed "5" must not momentarily resize the canvas to 5 mm, nor be saveable.
    expect(onChange).not.toHaveBeenCalled();
  });

  it("accepts a valid custom dimension", async () => {
    const user = userEvent.setup();
    picker({ pageSize: "custom", pageWidthMm: 200, pageHeightMm: 200 });
    onChange.mockClear();

    const width = screen.getByLabelText(/Width \(mm\)/);
    await user.clear(width);
    await user.type(width, "320");

    expect(onChange).toHaveBeenLastCalledWith({ pageSize: "custom", pageWidthMm: 320, pageHeightMm: 200 });
  });

  // The Edit step passes this; the Upload step does not. Before artwork exists there is nothing to
  // reshape, and a warning about a consequence that cannot occur is noise.
  it("warns that artwork will be reshaped, but only once there is artwork", () => {
    picker({ warnArtworkWillRescale: true });
    const warning = screen.getByRole("status");
    expect(warning).toHaveTextContent(/reshapes the picture you uploaded/i);
    // States what does NOT move as well as what does — the reassurance is half the message.
    expect(warning).toHaveTextContent(/keeps its position/i);
  });

  it("says nothing about artwork before any has been uploaded", () => {
    picker();
    expect(screen.queryByRole("status")).not.toBeInTheDocument();
  });

  it("offers no dimension boxes unless the page is custom", () => {
    picker({ pageSize: "a4-landscape" });
    expect(screen.queryByLabelText(/Width \(mm\)/)).not.toBeInTheDocument();
  });

  it("is inert for someone who cannot change the certificate", () => {
    picker({ disabled: true });
    expect(screen.getByLabelText("Paper")).toBeDisabled();
    expect(screen.getByRole("button", { name: "Landscape" })).toBeDisabled();
  });
});
