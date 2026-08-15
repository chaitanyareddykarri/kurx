import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";

const presign = vi.fn();
const saveAvatar = vi.fn();
vi.mock("@/lib/profile-actions", () => ({
  presignProfileImageAction: (...a: unknown[]) => presign(...a),
  saveAvatarAction: (...a: unknown[]) => saveAvatar(...a),
  updateProfileAction: vi.fn()
}));
vi.mock("next-themes", () => ({ useTheme: () => ({ resolvedTheme: "dark", setTheme: vi.fn() }) }));

import { ImageUpload } from "@/components/profile/image-upload";
import { AppearanceCard } from "@/components/profile/appearance-card";
import { MAX_IMAGE_BYTES } from "@/lib/image-edit";

/**
 * The upload → edit → save flow, and the guarantee underneath it: **nothing reaches the network until
 * Save, and nothing replaces the stored key unless the PUT succeeded.** Every rejection path below
 * asserts the negative — no presign call, or a preview still showing the previous picture — because a
 * failed upload that quietly clears the avatar is the expensive bug here, not a missing error string.
 *
 * jsdom decodes no images, so `Image` is stubbed to report dimensions the tests choose. That is the
 * only fake: validation, state and wiring are the real code.
 */

// Dimensions the stubbed decoder reports for the next load, and whether it should fail outright.
let nextImage = { width: 900, height: 900, fail: false };

class StubImage {
  onload: (() => void) | null = null;
  onerror: (() => void) | null = null;
  width = 0;
  height = 0;
  set src(_v: string) {
    queueMicrotask(() => {
      if (nextImage.fail) return this.onerror?.();
      this.width = nextImage.width;
      this.height = nextImage.height;
      this.onload?.();
    });
  }
}

function imageFile(name: string, type: string, size: number) {
  const f = new File(["x"], name, { type });
  Object.defineProperty(f, "size", { value: size });
  return f;
}

const VALID = () => imageFile("me.png", "image/png", 400 * 1024);

beforeEach(() => {
  vi.clearAllMocks();
  nextImage = { width: 900, height: 900, fail: false };
  vi.stubGlobal("Image", StubImage);
  // jsdom ships no raster backend: `getContext` returns null and `toBlob` throws "not implemented".
  // The editor already tolerates a null context (that branch is why it mounts here at all); `toBlob`
  // is stubbed to the encoded bytes it would otherwise produce, so the save path can be exercised.
  // What the canvas actually painted is not assertable in jsdom — the geometry tests cover that.
  HTMLCanvasElement.prototype.getContext = (() => null) as unknown as HTMLCanvasElement["getContext"];
  HTMLCanvasElement.prototype.toBlob = function (cb: BlobCallback) {
    cb(new Blob(["encoded"], { type: "image/webp" }));
  };
  vi.stubGlobal("URL", Object.assign(Object.create(URL), {
    createObjectURL: () => "blob:stub",
    revokeObjectURL: () => {}
  }));
  presign.mockResolvedValue({ ok: true, key: "users/u1/avatar/new.webp", url: "http://storage.local/put" });
  saveAvatar.mockResolvedValue({ ok: true });
  vi.stubGlobal("fetch", vi.fn(async () => ({ ok: true })));
});

/**
 * `applyAccept: false` deliberately defeats the input's own `accept` filter.
 *
 * The attribute is a convenience, not a control: a drag-drop, an "All files" picker or a renamed
 * extension all hand over a file the browser never screened. What is under test is the component
 * refusing it regardless — and with user-event's default the change event never fires for a
 * non-matching file, so that check would silently go unexercised while still reporting green.
 * It is a setup option, not a per-call one.
 */
const setupUser = () => userEvent.setup({ applyAccept: false });

async function pick(user: ReturnType<typeof userEvent.setup>, file: File, label = /upload profile photo/i) {
  await user.upload(screen.getByLabelText(label), file);
}

describe("valid upload opens the editor", () => {
  it("shows the editor with every edit control after a valid pick", async () => {
    const user = setupUser();
    render(<ImageUpload slot="avatar" name="avatarKey" initialKey="" label="Profile photo" previewName="Naveen" />);
    await pick(user, VALID());

    expect(await screen.findByTestId("image-editor")).toBeInTheDocument();
    expect(screen.getByLabelText("Zoom")).toBeInTheDocument();
    expect(screen.getByLabelText("Brightness")).toBeInTheDocument();
    expect(screen.getByLabelText("Contrast")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Rotate left" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Rotate right" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Reset edits" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Save image" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Cancel" })).toBeInTheDocument();
    // The crop frame is the preview: it exists before anything is uploaded.
    expect(screen.getByRole("img", { name: /drag or use arrow keys/i })).toBeInTheDocument();
  });

  it("uploads nothing until Save is pressed", async () => {
    const user = setupUser();
    render(<ImageUpload slot="avatar" name="avatarKey" initialKey="" label="Profile photo" previewName="Naveen" />);
    await pick(user, VALID());
    await screen.findByTestId("image-editor");
    expect(presign).not.toHaveBeenCalled();
    expect(fetch).not.toHaveBeenCalled();
  });
});

describe("rejections", () => {
  it("refuses an unsupported file type and never opens the editor", async () => {
    const user = setupUser();
    render(<ImageUpload slot="avatar" name="avatarKey" initialKey="" label="Profile photo" previewName="Naveen" />);
    await pick(user, imageFile("cv.pdf", "application/pdf", 1024));

    expect(await screen.findByRole("alert")).toHaveTextContent(/JPG, PNG or WebP/);
    expect(screen.queryByTestId("image-editor")).not.toBeInTheDocument();
    expect(presign).not.toHaveBeenCalled();
  });

  it("refuses an oversized file", async () => {
    const user = setupUser();
    render(<ImageUpload slot="avatar" name="avatarKey" initialKey="" label="Profile photo" previewName="Naveen" />);
    await pick(user, imageFile("huge.png", "image/png", MAX_IMAGE_BYTES + 1));

    expect(await screen.findByRole("alert")).toHaveTextContent(/5 MB/);
    expect(screen.queryByTestId("image-editor")).not.toBeInTheDocument();
  });

  it("refuses an image below the minimum dimensions, which is only knowable after decode", async () => {
    const user = setupUser();
    nextImage = { width: 64, height: 64, fail: false };
    render(<ImageUpload slot="avatar" name="avatarKey" initialKey="" label="Profile photo" previewName="Naveen" />);
    await pick(user, VALID());

    expect(await screen.findByRole("alert")).toHaveTextContent(/64×64/);
    expect(screen.queryByTestId("image-editor")).not.toBeInTheDocument();
  });

  // A rejected file leaves the stored key alone — the hidden input still carries the old one, so a
  // subsequent profile save re-persists the existing picture rather than clearing it.
  it("leaves the existing picture untouched when a file is rejected", async () => {
    const user = setupUser();
    const { container } = render(
      <ImageUpload slot="avatar" name="avatarKey" initialKey="users/u1/avatar/old.webp" label="Profile photo" previewName="Naveen" />
    );
    await pick(user, imageFile("cv.pdf", "application/pdf", 1024), /replace profile photo/i);
    await screen.findByRole("alert");

    expect(container.querySelector('input[name="avatarKey"]')).toHaveValue("users/u1/avatar/old.webp");
  });
});

describe("cancel and reset", () => {
  it("closes the editor on Cancel without uploading anything", async () => {
    const user = setupUser();
    render(<ImageUpload slot="avatar" name="avatarKey" initialKey="" label="Profile photo" previewName="Naveen" />);
    await pick(user, VALID());
    await screen.findByTestId("image-editor");

    await user.click(screen.getByRole("button", { name: "Cancel" }));

    await waitFor(() => expect(screen.queryByTestId("image-editor")).not.toBeInTheDocument());
    expect(presign).not.toHaveBeenCalled();
    expect(fetch).not.toHaveBeenCalled();
  });

  it("keeps the previously saved key after cancelling an edit", async () => {
    const user = setupUser();
    const { container } = render(
      <ImageUpload slot="avatar" name="avatarKey" initialKey="users/u1/avatar/old.webp" label="Profile photo" previewName="Naveen" />
    );
    await pick(user, VALID(), /replace profile photo/i);
    await screen.findByTestId("image-editor");
    await user.click(screen.getByRole("button", { name: "Cancel" }));

    expect(container.querySelector('input[name="avatarKey"]')).toHaveValue("users/u1/avatar/old.webp");
  });

  it("disables Reset until an edit has been made, then enables it", async () => {
    const user = setupUser();
    render(<ImageUpload slot="avatar" name="avatarKey" initialKey="" label="Profile photo" previewName="Naveen" />);
    await pick(user, VALID());
    await screen.findByTestId("image-editor");

    const reset = screen.getByRole("button", { name: "Reset edits" });
    expect(reset).toBeDisabled();

    await user.click(screen.getByRole("button", { name: "Rotate right" }));
    expect(reset).toBeEnabled();

    await user.click(reset);
    expect(reset).toBeDisabled();
  });
});

describe("upload outcomes", () => {
  it("stores the returned key and reports readiness after a successful save", async () => {
    const user = setupUser();
    const { container } = render(
      <ImageUpload slot="avatar" name="avatarKey" initialKey="" label="Profile photo" previewName="Naveen" />
    );
    await pick(user, VALID());
    await screen.findByTestId("image-editor");
    await user.click(screen.getByRole("button", { name: "Save image" }));

    await waitFor(() => expect(presign).toHaveBeenCalledWith("avatar", "image/webp", expect.any(Number)));
    await waitFor(() =>
      expect(container.querySelector('input[name="avatarKey"]')).toHaveValue("users/u1/avatar/new.webp")
    );
    expect(await screen.findByRole("status")).toHaveTextContent(/Save your profile to apply it/);
  });

  // The requirement stated as "failed uploads do not overwrite the existing profile image".
  it("keeps the old key and says so when the PUT fails", async () => {
    const user = setupUser();
    vi.stubGlobal("fetch", vi.fn(async () => ({ ok: false })));
    const { container } = render(
      <ImageUpload slot="avatar" name="avatarKey" initialKey="users/u1/avatar/old.webp" label="Profile photo" previewName="Naveen" />
    );
    await pick(user, VALID(), /replace profile photo/i);
    await screen.findByTestId("image-editor");
    await user.click(screen.getByRole("button", { name: "Save image" }));

    expect(await screen.findByRole("alert")).toHaveTextContent(/existing picture is unchanged/i);
    expect(container.querySelector('input[name="avatarKey"]')).toHaveValue("users/u1/avatar/old.webp");
  });

  it("keeps the old key when presigning is refused", async () => {
    const user = setupUser();
    presign.mockResolvedValue({ ok: false, error: "That file type isn't allowed." });
    const { container } = render(
      <ImageUpload slot="avatar" name="avatarKey" initialKey="users/u1/avatar/old.webp" label="Profile photo" previewName="Naveen" />
    );
    await pick(user, VALID(), /replace profile photo/i);
    await screen.findByTestId("image-editor");
    await user.click(screen.getByRole("button", { name: "Save image" }));

    expect(await screen.findByRole("alert")).toHaveTextContent("That file type isn't allowed.");
    expect(container.querySelector('input[name="avatarKey"]')).toHaveValue("users/u1/avatar/old.webp");
    expect(fetch).not.toHaveBeenCalled();
  });
});

describe("remove and replace", () => {
  it("clears the key to an empty string — the value that actually clears it server-side", async () => {
    const user = setupUser();
    const { container } = render(
      <ImageUpload slot="avatar" name="avatarKey" initialKey="users/u1/avatar/old.webp" label="Profile photo" previewName="Naveen" />
    );
    await user.click(screen.getByRole("button", { name: "Remove image" }));

    expect(container.querySelector('input[name="avatarKey"]')).toHaveValue("");
    expect(await screen.findByRole("status")).toHaveTextContent(/removed/i);
  });

  it("labels the control Replace when a picture already exists, and Upload when it does not", () => {
    const { unmount } = render(
      <ImageUpload slot="avatar" name="avatarKey" initialKey="users/u1/avatar/old.webp" label="Profile photo" previewName="Naveen" />
    );
    expect(screen.getByLabelText(/replace profile photo/i)).toBeInTheDocument();
    unmount();

    render(<ImageUpload slot="avatar" name="avatarKey" initialKey="" label="Profile photo" previewName="Naveen" />);
    expect(screen.getByLabelText(/upload profile photo/i)).toBeInTheDocument();
  });

  it("replaces an existing image with the newly uploaded key", async () => {
    const user = setupUser();
    const { container } = render(
      <ImageUpload slot="avatar" name="avatarKey" initialKey="users/u1/avatar/old.webp" label="Profile photo" previewName="Naveen" />
    );
    await pick(user, VALID(), /replace profile photo/i);
    await screen.findByTestId("image-editor");
    await user.click(screen.getByRole("button", { name: "Save image" }));

    await waitFor(() =>
      expect(container.querySelector('input[name="avatarKey"]')).toHaveValue("users/u1/avatar/new.webp")
    );
  });
});

describe("Appearance card — theme alongside the picture", () => {
  it("keeps the theme control mounted next to the image editor", () => {
    render(<AppearanceCard initialAvatarKey="" name="Naveen" />);
    expect(screen.getByRole("heading", { name: "Theme" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /toggle theme/i })).toBeInTheDocument();
    expect(screen.getByLabelText(/upload profile picture/i)).toBeInTheDocument();
  });

  it("disables Save until an image has been processed", async () => {
    const user = setupUser();
    render(<AppearanceCard initialAvatarKey="" name="Naveen" />);

    expect(screen.getByRole("button", { name: "Save picture" })).toBeDisabled();

    await pick(user, VALID(), /upload profile picture/i);
    await screen.findByTestId("image-editor");
    await user.click(screen.getByRole("button", { name: "Save image" }));

    await waitFor(() => expect(screen.getByRole("button", { name: "Save picture" })).toBeEnabled());
  });

  it("persists through the avatar-only action, leaving the rest of the profile alone", async () => {
    const user = setupUser();
    render(<AppearanceCard initialAvatarKey="" name="Naveen" />);
    await pick(user, VALID(), /upload profile picture/i);
    await screen.findByTestId("image-editor");
    await user.click(screen.getByRole("button", { name: "Save image" }));
    await waitFor(() => expect(screen.getByRole("button", { name: "Save picture" })).toBeEnabled());

    await user.click(screen.getByRole("button", { name: "Save picture" }));

    await waitFor(() => expect(saveAvatar).toHaveBeenCalledWith("users/u1/avatar/new.webp"));
    // Two live regions coexist here: the uploader's "ready to save" notice and the card's save result.
    await waitFor(() =>
      expect(screen.getAllByRole("status").map((n) => n.textContent)).toContain("Profile picture saved.")
    );
  });

  it("surfaces a failed save without claiming success", async () => {
    const user = setupUser();
    saveAvatar.mockResolvedValue({ ok: false, error: "Something went wrong. Please try again." });
    render(<AppearanceCard initialAvatarKey="" name="Naveen" />);
    await pick(user, VALID(), /upload profile picture/i);
    await screen.findByTestId("image-editor");
    await user.click(screen.getByRole("button", { name: "Save image" }));
    await waitFor(() => expect(screen.getByRole("button", { name: "Save picture" })).toBeEnabled());

    await user.click(screen.getByRole("button", { name: "Save picture" }));

    expect(await screen.findByRole("alert")).toHaveTextContent("Something went wrong. Please try again.");
  });
});
