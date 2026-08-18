import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

/**
 * The badge designer and print run (D-362, fixed in D-385).
 *
 * These pin the behaviours that a screenshot of a rendering page cannot tell you: that the canvas opens
 * on the layout the *server* would print rather than on an empty card, that enabling one field does not
 * silently replace the whole default layout, and that a single badge can be downloaded on its own.
 */

const mocks = vi.hoisted(() => ({
  getIdCardTemplate: vi.fn(),
  getBadgeFieldDefaults: vi.fn(),
  saveIdCardTemplate: vi.fn(),
  previewIdCardTemplate: vi.fn(),
  idCardAssetUrl: vi.fn(),
  presignIdCardAsset: vi.fn(),
  downloadBadgeSheet: vi.fn(),
  downloadOneBadge: vi.fn(),
  revokeBadge: vi.fn(),
  generateIdCards: vi.fn()
}));

// A full replacement rather than a spread over the real module: `badge-api` imports the shared axios
// client, which builds on React `cache()` and does not exist in this environment.
vi.mock("@/lib/badge-api", () => ({
  ...mocks,
  BADGE_FIELD_KEYS: [
    "photo", "logo", "qr", "holder_name", "subtitle",
    "access_level", "event_name", "event_date", "card_number", "accent_band"
  ]
}));

vi.mock("next/navigation", () => ({ useRouter: () => ({ refresh: vi.fn() }) }));

import { BadgeTemplateEditor } from "@/components/host/badges/badge-template-editor";
import { BadgeExport } from "@/components/host/badges/badge-export";
import type { BadgeField, BadgeRecipient, BadgeSize } from "@/lib/badge-api";

const SIZES: BadgeSize[] = [
  { key: "lanyard", label: "Lanyard badge", width_mm: 88.9, height_mm: 139.7, landscape: false },
  { key: "card", label: "PVC card — CR80", width_mm: 85.6, height_mm: 54, landscape: true }
];

/** A stand-in for what `GET /badges/template/defaults` returns — the server's built-in placements. */
const DEFAULTS: BadgeField[] = [
  { key: "accent_band", x: 0, y: 0, width: 100, height: 11, fontSizePt: null, color: null, align: "center", weight: null, zOrder: 0, enabled: true },
  { key: "photo", x: 27, y: 14, width: 46, height: 26, fontSizePt: null, color: null, align: "center", weight: null, zOrder: 1, enabled: true },
  { key: "holder_name", x: 4, y: 42, width: 92, height: 8, fontSizePt: 16, color: null, align: "center", weight: "bold", zOrder: 1, enabled: true },
  { key: "qr", x: 33, y: 68, width: 34, height: 22, fontSizePt: null, color: null, align: "center", weight: null, zOrder: 2, enabled: true }
];

const CARD_DEFAULTS: BadgeField[] = [
  { key: "accent_band", x: 0, y: 0, width: 100, height: 15, fontSizePt: null, color: null, align: "center", weight: null, zOrder: 0, enabled: true },
  { key: "photo", x: 4, y: 22, width: 26, height: 46, fontSizePt: null, color: null, align: "center", weight: null, zOrder: 1, enabled: true },
  { key: "holder_name", x: 33, y: 22, width: 42, height: 11, fontSizePt: 11, color: null, align: "left", weight: "bold", zOrder: 1, enabled: true },
  { key: "qr", x: 77, y: 22, width: 20, height: 46, fontSizePt: null, color: null, align: "center", weight: null, zOrder: 2, enabled: true }
];

const SAVED_DEFAULT = {
  accentColor: null, textColor: null, logoKey: null, backgroundKey: null,
  sizeKey: "lanyard", fields: null
};

beforeEach(() => {
  vi.clearAllMocks();
  mocks.getIdCardTemplate.mockResolvedValue({ ...SAVED_DEFAULT });
  mocks.getBadgeFieldDefaults.mockImplementation((_t: string, _e: string, sizeKey: string) =>
    Promise.resolve(sizeKey === "card" ? CARD_DEFAULTS : DEFAULTS));
  mocks.previewIdCardTemplate.mockResolvedValue(new Blob(["png"], { type: "image/png" }));
  mocks.saveIdCardTemplate.mockImplementation((_t: string, _e: string, spec: unknown) => Promise.resolve(spec));
  URL.createObjectURL = vi.fn(() => "blob:preview");
  URL.revokeObjectURL = vi.fn();
  /*
   * `BadgeExport.save()` downloads by creating an `<a download href="blob:…">` and clicking it. A real
   * browser treats that as a download and does NOT navigate; jsdom implements neither `download` nor
   * navigation, so the programmatic click schedules one and reports
   * "Not implemented: navigation (except hash changes)" from a timer — after the test that caused it
   * has passed. Vitest counts that as an unhandled error, attributes it to no test, and can exit the
   * runner non-zero on a suite where every assertion passed.
   *
   * Stubbed as a spy rather than a no-op so the click stays observable: what a future test would want
   * to assert is that a download was triggered, which `anchorClick` still records.
   */
  anchorClick = vi.spyOn(HTMLAnchorElement.prototype, "click").mockImplementation(() => {});
});

/** The stubbed `<a>` click — see `beforeEach`. Restored between tests so nothing leaks into another file. */
let anchorClick: ReturnType<typeof vi.spyOn>;

afterEach(() => anchorClick.mockRestore());

const editor = () => <BadgeTemplateEditor eventId="e1" accessToken="t" sizes={SIZES} />;

describe("the designer opens on a real card, not an empty one", () => {
  /**
   * The bug this pins: `fields: null` from the server means "use the built-in layout", and the editor
   * used to render that as *no fields* — a blank canvas, every checkbox unticked, against a card the
   * server prints fully populated.
   */
  it("seeds the canvas from the server's built-in layout when nothing has been saved", async () => {
    render(editor());

    await waitFor(() => expect(mocks.getBadgeFieldDefaults).toHaveBeenCalled());
    const canvas = await screen.findByTestId("badge-canvas");

    for (const label of ["Photo", "Name", "QR code", "Colour band"]) {
      expect(within(canvas).getByRole("button", { name: label })).toBeTruthy();
    }
  });

  it("ticks the checkboxes for the fields the built-in layout actually places", async () => {
    render(editor());

    await screen.findByTestId("badge-canvas");
    // Scoped to the field list: the canvas labels its boxes with the same names.
    const list = screen.getByRole("group", { name: /fields on the card/i });
    const box = (name: string) => within(list).getByLabelText(name) as HTMLInputElement;

    // Unticked boxes over a populated card is the state that made the layout look empty.
    await waitFor(() => expect(box("Photo").checked).toBe(true));
    expect(box("Name").checked).toBe(true);
    expect(box("QR code").checked).toBe(true);
    // Not in this layout, so genuinely off.
    expect(box("Event date").checked).toBe(false);
  });

  /**
   * The consequence of the empty canvas, and the more serious half: the server treats any non-empty
   * `fields` array as the WHOLE layout. Enabling one field on an empty canvas therefore used to save a
   * one-field card — dropping the QR, which is the credential the badge exists to carry.
   */
  it("enabling a field adds to the layout instead of replacing it", async () => {
    const user = userEvent.setup();
    render(editor());
    await waitFor(() => expect(mocks.getBadgeFieldDefaults).toHaveBeenCalled());

    await user.click(screen.getByLabelText("Event date"));
    await user.click(screen.getByRole("button", { name: /save design/i }));

    await waitFor(() => expect(mocks.saveIdCardTemplate).toHaveBeenCalled());
    const saved = mocks.saveIdCardTemplate.mock.calls[0][2] as { fields: BadgeField[] };
    const keys = saved.fields.map((f) => f.key);

    expect(keys).toContain("event_date");
    expect(keys).toContain("qr");
    expect(keys).toContain("holder_name");
    expect(saved.fields.length).toBe(DEFAULTS.length + 1);
  });

  it("keeps a saved layout instead of overwriting it with the defaults", async () => {
    const mine: BadgeField[] = [
      { key: "holder_name", x: 10, y: 10, width: 50, height: 6, fontSizePt: 20, color: "#ff0000", align: "left", weight: "bold", zOrder: 1, enabled: true }
    ];
    mocks.getIdCardTemplate.mockResolvedValue({ ...SAVED_DEFAULT, fields: mine });

    render(editor());

    await waitFor(() => expect(screen.getByTestId("badge-canvas")).toBeTruthy());
    expect(mocks.getBadgeFieldDefaults).not.toHaveBeenCalled();
  });
});

describe("card size", () => {
  it("re-seeds the layout when the size changes and nothing has been touched", async () => {
    const user = userEvent.setup();
    render(editor());
    await waitFor(() => expect(mocks.getBadgeFieldDefaults).toHaveBeenCalledTimes(1));

    await user.selectOptions(screen.getByLabelText("Card size"), "card");

    // CR80 is the one landscape size and has its own arrangement — the portrait layout would not fit.
    await waitFor(() =>
      expect(mocks.getBadgeFieldDefaults).toHaveBeenLastCalledWith("t", "e1", "card", "staff"));
  });

  it("gives the canvas the card's real aspect ratio", async () => {
    render(editor());
    const canvas = await screen.findByTestId("badge-canvas");

    // 300px wide at 88.9 x 139.7mm -> 471px tall. The shape on screen is the shape that prints.
    expect(canvas.style.width).toBe("300px");
    expect(canvas.style.height).toBe(`${Math.round(300 * (139.7 / 88.9))}px`);
  });
});

describe("the print run", () => {
  const recipients: BadgeRecipient[] = [
    { user_id: "u1", name: "Priya Raghunathan", kind: "staff", subtitle: "Stage Manager", access_level: "All Access", has_photo: true,
      card: { id: "c1", card_number: "KRX-00001", verify_code: "ABC", status: "Active", is_revoked: false, generated_at: null } },
    { user_id: "u2", name: "Arjun Mehta", kind: "staff", subtitle: "Registration Desk", access_level: "Staff", has_photo: false, card: null },
    { user_id: "u3", name: "", kind: "attendee", subtitle: "Team Entry", access_level: null, has_photo: false,
      card: { id: "c3", card_number: "KRX-00003", verify_code: "XYZ", status: "Revoked", is_revoked: true, generated_at: null } }
  ];

  const exporter = () =>
    <BadgeExport eventId="e1" accessToken="t" sizes={SIZES} recipients={recipients} />;

  it("downloads one person's badge without regenerating the whole sheet", async () => {
    const user = userEvent.setup();
    mocks.downloadOneBadge.mockResolvedValue(new Blob(["%PDF"], { type: "application/pdf" }));
    render(exporter());

    await user.click(screen.getByRole("button", { name: /Download Priya Raghunathan badge as PDF/i }));

    await waitFor(() =>
      expect(mocks.downloadOneBadge).toHaveBeenCalledWith("t", "e1", "u1", "lanyard"));
    expect(mocks.downloadBadgeSheet).not.toHaveBeenCalled();
  });

  /**
   * A badge with no name is a useless credential, and the server prints the line blank rather than
   * filling it with a placeholder token (D-385) — so the only place this can be caught is before the run.
   */
  it("warns before printing badges for accounts with no name", async () => {
    render(exporter());
    expect(screen.getByText(/1 of these 3 have no name on their account/i)).toBeTruthy();
    expect(screen.getByText(/No name on this account/i)).toBeTruthy();
  });

  /**
   * Revoking marks the card document — it does not void the ticket or remove the assignment behind it.
   * The copy has to say so at the point of the click, because the opposite assumption is the one that
   * gets someone waved through a door they should not go through.
   */
  it("revokes an issued card and says what that does not do", async () => {
    const user = userEvent.setup();
    mocks.revokeBadge.mockResolvedValue({
      id: "c1", card_number: "KRX-00001", verify_code: "ABC", status: "Revoked",
      is_revoked: true, generated_at: null
    });
    render(exporter());

    await user.click(screen.getByRole("button", { name: /Revoke Priya Raghunathan badge/i }));

    await waitFor(() => expect(mocks.revokeBadge).toHaveBeenCalledWith("t", "e1", "u1"));
    expect(screen.getByText(/does not void their ticket or remove their assignment/i)).toBeTruthy();
  });

  it("offers no revoke for a card that was never issued or is already revoked", () => {
    render(exporter());
    // u2 has no card at all; u3 has one already revoked.
    expect(screen.queryByRole("button", { name: /Revoke Arjun Mehta badge/i })).toBeNull();
    expect(screen.queryByRole("button", { name: /Revoke Kavya Nair badge/i })).toBeNull();
  });

  it("names the physical size it is about to print at", async () => {
    render(exporter());
    expect(screen.getByText(/88\.9 × 139\.7 mm, laid out on A4 with cut guides/i)).toBeTruthy();
  });

  it("reports what issuing actually did rather than a bare success", async () => {
    const user = userEvent.setup();
    mocks.generateIdCards.mockResolvedValue({ issued: 2, regenerated: 1 });
    render(exporter());

    await user.click(screen.getByRole("button", { name: /Issue 3 ID cards/i }));

    await waitFor(() =>
      expect(screen.getByText(/2 cards issued, 1 regenerated \(existing numbers kept\)/i)).toBeTruthy());
  });

  it("says nothing was printed when the sheet fails", async () => {
    const user = userEvent.setup();
    mocks.downloadBadgeSheet.mockRejectedValue(new Error("boom"));
    render(exporter());

    await user.click(screen.getByRole("button", { name: /download print sheet/i }));

    await waitFor(() => expect(screen.getByText(/Nothing was printed/i)).toBeTruthy());
  });
});
