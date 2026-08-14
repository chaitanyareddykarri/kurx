import { act, render, renderHook, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { AttachmentView } from "@/components/chat/attachment-view";
import type { ChatAttachment } from "@/lib/chat-api";
import type { PendingUpload } from "@/lib/chat-upload";
import { useAttachmentUploads } from "@/lib/use-attachment-uploads";

// Server actions are network boundaries; the hook tests drive them directly.
const presign = vi.fn();
const confirm = vi.fn();
const attachmentUrl = vi.fn();
vi.mock("@/lib/chat-actions", () => ({
  presignAttachmentAction: (...a: unknown[]) => presign(...a),
  confirmAttachmentAction: (...a: unknown[]) => confirm(...a),
  attachmentUrlAction: (...a: unknown[]) => attachmentUrl(...a)
}));

const upload = vi.fn();
vi.mock("@/lib/chat-upload", async () => {
  const actual = await vi.importActual<typeof import("@/lib/chat-upload")>("@/lib/chat-upload");
  return { ...actual, uploadToPresignedUrl: (...a: unknown[]) => upload(...a) };
});

function attachment(overrides: Partial<ChatAttachment> = {}): ChatAttachment {
  return {
    id: "att-1",
    url: "https://storage/signed",
    fileName: "report.pdf",
    contentType: "application/pdf",
    sizeBytes: 2048,
    width: null,
    height: null,
    ...overrides
  };
}

function pending(overrides: Partial<PendingUpload> = {}): PendingUpload {
  return {
    localId: "u1",
    file: new File(["x"], "report.pdf"),
    fileName: "report.pdf",
    contentType: "application/pdf",
    sizeBytes: 2048,
    status: "uploading",
    progress: 0.4,
    ...overrides
  };
}

function makeFile(name: string, type = "image/png") {
  return new File(["bytes"], name, { type });
}

beforeEach(() => {
  vi.clearAllMocks();
  vi.stubGlobal("URL", { createObjectURL: () => "blob:preview", revokeObjectURL: vi.fn() });
  presign.mockResolvedValue({ key: "chat/room-1/abc", url: "https://storage/put", headers: {} });
  confirm.mockResolvedValue(attachment());
  upload.mockResolvedValue(undefined);
});

describe("AttachmentView — documents", () => {
  it("shows filename, size and a download action", () => {
    render(<AttachmentView attachment={attachment()} />);
    expect(screen.getByText("report.pdf")).toBeInTheDocument();
    expect(screen.getByText("2 KB")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Download report.pdf" })).toBeInTheDocument();
  });

  it("renders an unknown type as a usable card", () => {
    render(<AttachmentView attachment={attachment({ contentType: "application/x-weird", fileName: "thing.bin" })} />);
    expect(screen.getByText("thing.bin")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Download thing.bin" })).toBeInTheDocument();
  });

  it("requests a fresh signed URL on every download", async () => {
    const user = userEvent.setup();
    attachmentUrl.mockResolvedValue("https://storage/fresh");
    vi.stubGlobal("open", vi.fn());

    render(<AttachmentView attachment={attachment()} />);
    await user.click(screen.getByRole("button", { name: "Download report.pdf" }));
    await user.click(screen.getByRole("button", { name: "Download report.pdf" }));

    // Never cached: the signature expires and membership is re-checked server-side each time.
    expect(attachmentUrl).toHaveBeenCalledTimes(2);
  });

  it("explains a failed download instead of doing nothing", async () => {
    const user = userEvent.setup();
    attachmentUrl.mockRejectedValue(new Error("forbidden"));

    render(<AttachmentView attachment={attachment()} />);
    await user.click(screen.getByRole("button", { name: "Download report.pdf" }));

    expect(await screen.findByRole("alert")).toHaveTextContent(/could not be opened/i);
  });

  it("shows progress and a cancel action while uploading, and no download", () => {
    render(
      <AttachmentView attachment={attachment()} pending={pending()} onCancel={vi.fn()} />
    );

    expect(screen.getByText("Uploading… 40%")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Cancel upload of report.pdf" })).toBeInTheDocument();
    // Nothing to download until it is confirmed.
    expect(screen.queryByRole("button", { name: /Download/ })).not.toBeInTheDocument();
  });
});

describe("AttachmentView — images", () => {
  const image = attachment({ contentType: "image/png", fileName: "photo.png", url: "https://storage/photo" });

  it("renders a lazily-loaded thumbnail", () => {
    render(<AttachmentView attachment={image} />);
    const img = screen.getByAltText("photo.png");
    expect(img).toHaveAttribute("loading", "lazy");
    expect(img).toHaveAttribute("decoding", "async");
  });

  it("opens a lightbox on click and restores focus on close", async () => {
    const user = userEvent.setup();
    render(<AttachmentView attachment={image} />);

    const trigger = screen.getByRole("button", { name: "Open image photo.png" });
    trigger.focus();
    await user.click(trigger);

    const dialog = screen.getByRole("dialog", { name: "photo.png" });
    expect(within(dialog).getByRole("button", { name: "Close image" })).toHaveFocus();

    await user.keyboard("{Escape}");
    await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
    expect(trigger).toHaveFocus();
  });

  it("cannot be enlarged while still uploading", () => {
    render(
      <AttachmentView
        attachment={attachment({ contentType: "image/png", fileName: "photo.png" })}
        pending={pending({ contentType: "image/png", fileName: "photo.png" })}
      />
    );
    expect(screen.getByRole("button", { name: "Open image photo.png" })).toBeDisabled();
  });
});

describe("AttachmentView — failures", () => {
  it("names the reason and offers retry and remove", async () => {
    const user = userEvent.setup();
    const onRetry = vi.fn();
    const onRemove = vi.fn();

    render(
      <AttachmentView
        attachment={attachment()}
        pending={pending({ status: "failed", error: "file_too_large" })}
        onRetry={onRetry}
        onRemove={onRemove}
      />
    );

    expect(screen.getByRole("alert")).toHaveTextContent("That file is over the 25 MB limit.");
    await user.click(screen.getByRole("button", { name: "Retry" }));
    await user.click(screen.getByRole("button", { name: "Remove" }));
    expect(onRetry).toHaveBeenCalledOnce();
    expect(onRemove).toHaveBeenCalledOnce();
  });

  it("distinguishes a cancellation from a rejection", () => {
    render(<AttachmentView attachment={attachment()} pending={pending({ status: "cancelled" })} />);
    expect(screen.getByRole("alert")).toHaveTextContent("Upload cancelled.");
  });

  it("states a malware rejection plainly", () => {
    render(
      <AttachmentView attachment={attachment()} pending={pending({ status: "failed", error: "file_infected" })} />
    );
    expect(screen.getByRole("alert")).toHaveTextContent("rejected by a security scan");
  });
});

describe("accessibility", () => {
  it("announces upload progress as a progressbar, not just a spinner", () => {
    render(<AttachmentView attachment={attachment()} pending={pending({ progress: 0.6 })} />);
    const bar = screen.getByRole("progressbar");
    expect(bar).toHaveAttribute("aria-valuenow", "60");
    expect(bar).toHaveAccessibleName("Upload progress 60 percent");
  });

  it("marks an uploading tile as busy", () => {
    const { container } = render(<AttachmentView attachment={attachment()} pending={pending()} />);
    expect(container.querySelector('[aria-busy="true"]')).toBeInTheDocument();
  });

  it("every action is a real button, reachable by keyboard", async () => {
    const user = userEvent.setup();
    const onRetry = vi.fn();
    render(
      <AttachmentView
        attachment={attachment()}
        pending={pending({ status: "failed", error: "network" })}
        onRetry={onRetry}
        onRemove={vi.fn()}
      />
    );

    await user.tab();
    expect(screen.getByRole("button", { name: "Retry" })).toHaveFocus();
    await user.keyboard("{Enter}");
    expect(onRetry).toHaveBeenCalled();
  });
});

describe("useAttachmentUploads — mutation behaviour", () => {
  it("runs presign → PUT → confirm in order and records the attachment id", async () => {
    const { result } = renderHook(() => useAttachmentUploads("room-1"));

    await act(async () => {
      result.current.add([makeFile("a.png")]);
    });

    await waitFor(() => expect(result.current.uploads[0].status).toBe("done"));
    expect(presign).toHaveBeenCalledWith("room-1", "a.png", "image/png", expect.any(Number));
    expect(upload).toHaveBeenCalledOnce();
    expect(confirm).toHaveBeenCalledWith("room-1", "chat/room-1/abc");
    expect(result.current.uploads[0].attachmentId).toBe("att-1");
  });

  it("uploads sequentially rather than in parallel", async () => {
    let inFlight = 0;
    let maxConcurrent = 0;
    upload.mockImplementation(async () => {
      inFlight += 1;
      maxConcurrent = Math.max(maxConcurrent, inFlight);
      await new Promise((r) => setTimeout(r, 5));
      inFlight -= 1;
    });

    const { result } = renderHook(() => useAttachmentUploads("room-1"));
    await act(async () => {
      result.current.add([makeFile("a.png"), makeFile("b.png"), makeFile("c.png")]);
    });

    await waitFor(() => expect(result.current.uploads.every((u) => u.status === "done")).toBe(true));
    // A composer is not a bulk uploader: serial keeps progress legible and bounds memory.
    expect(maxConcurrent).toBe(1);
  });

  it("surfaces a server refusal by code and stops at that file", async () => {
    presign.mockRejectedValueOnce(new Error('{"error":"file_too_large"}'));

    const { result } = renderHook(() => useAttachmentUploads("room-1"));
    await act(async () => {
      result.current.add([makeFile("huge.png")]);
    });

    await waitFor(() => expect(result.current.uploads[0].status).toBe("failed"));
    expect(result.current.uploads[0].error).toBe("file_too_large");
  });

  it("retries a failed upload with a fresh presign", async () => {
    presign.mockRejectedValueOnce(new Error('{"error":"scan_unavailable"}'));

    const { result } = renderHook(() => useAttachmentUploads("room-1"));
    await act(async () => {
      result.current.add([makeFile("a.png")]);
    });
    await waitFor(() => expect(result.current.uploads[0].status).toBe("failed"));

    await act(async () => {
      result.current.retry(result.current.uploads[0].localId);
    });

    await waitFor(() => expect(result.current.uploads[0].status).toBe("done"));
    expect(presign).toHaveBeenCalledTimes(2);
  });

  it("marks an aborted upload cancelled, not failed", async () => {
    upload.mockRejectedValueOnce(new Error("cancelled"));

    const { result } = renderHook(() => useAttachmentUploads("room-1"));
    await act(async () => {
      result.current.add([makeFile("a.png")]);
    });

    await waitFor(() => expect(result.current.uploads[0].status).toBe("cancelled"));
    // Cancelling is a user action, not an error — it must never be confirmed.
    expect(confirm).not.toHaveBeenCalled();
  });

  it("removes a pending upload and releases its preview", async () => {
    const { result } = renderHook(() => useAttachmentUploads("room-1"));
    await act(async () => {
      result.current.add([makeFile("a.png")]);
    });
    await waitFor(() => expect(result.current.uploads).toHaveLength(1));

    await act(async () => {
      result.current.remove(result.current.uploads[0].localId);
    });

    expect(result.current.uploads).toHaveLength(0);
    expect(URL.revokeObjectURL).toHaveBeenCalled();
  });

  it("clears everything once the message has been sent", async () => {
    const { result } = renderHook(() => useAttachmentUploads("room-1"));
    await act(async () => {
      result.current.add([makeFile("a.png")]);
    });
    await waitFor(() => expect(result.current.uploads[0].status).toBe("done"));

    await act(async () => {
      result.current.clear();
    });
    expect(result.current.uploads).toHaveLength(0);
  });

  it("does nothing when the room is not yet known", async () => {
    const { result } = renderHook(() => useAttachmentUploads(undefined));
    await act(async () => {
      result.current.add([makeFile("a.png")]);
    });

    expect(presign).not.toHaveBeenCalled();
  });
});
