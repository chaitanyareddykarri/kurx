import { beforeEach, describe, expect, it, vi } from "vitest";
import {
  canSend,
  confirmedAttachmentIds,
  createPendingUpload,
  describeUploadError,
  extractErrorCode,
  isImage,
  pendingAsAttachment,
  readableSize,
  releasePendingUpload,
  resolveContentType,
  uploadToPresignedUrl,
  type PendingUpload
} from "@/lib/chat-upload";

function file(name: string, type = "", size = 1024): File {
  const f = new File(["x".repeat(size)], name, { type });
  Object.defineProperty(f, "size", { value: size });
  return f;
}

function pending(overrides: Partial<PendingUpload> = {}): PendingUpload {
  return {
    localId: "u1",
    file: file("a.png", "image/png"),
    fileName: "a.png",
    contentType: "image/png",
    sizeBytes: 1024,
    status: "done",
    progress: 1,
    attachmentId: "att-1",
    ...overrides
  };
}

describe("content type resolution", () => {
  it("prefers the extension over the browser's guess", () => {
    // Browsers report Office formats inconsistently and sometimes report nothing at all; the
    // extension is what the server's agreement check actually compares against.
    expect(resolveContentType({ name: "report.docx", type: "" })).toBe(
      "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
    );
    expect(resolveContentType({ name: "photo.png", type: "application/octet-stream" })).toBe("image/png");
  });

  it("maps every allowed extension", () => {
    expect(resolveContentType({ name: "a.pdf" })).toBe("application/pdf");
    expect(resolveContentType({ name: "a.zip" })).toBe("application/zip");
    expect(resolveContentType({ name: "a.csv" })).toBe("text/csv");
    expect(resolveContentType({ name: "a.txt" })).toBe("text/plain");
  });

  it("gives an unknown extension a type the server will reject", () => {
    // Refused by the allow-list rather than smuggled through under a plausible-looking type.
    expect(resolveContentType({ name: "payload.exe", type: "image/png" })).toBe("application/octet-stream");
  });

  it("is case-insensitive about extensions", () => {
    expect(resolveContentType({ name: "PHOTO.PNG" })).toBe("image/png");
  });
});

describe("pending uploads", () => {
  beforeEach(() => {
    vi.stubGlobal("URL", {
      createObjectURL: vi.fn(() => "blob:preview"),
      revokeObjectURL: vi.fn()
    });
  });

  it("creates an object URL for an image so a pending photo previews immediately", () => {
    const upload = createPendingUpload(file("a.png", "image/png"));
    expect(upload.status).toBe("queued");
    expect(upload.previewUrl).toBe("blob:preview");
  });

  it("does not create one for a document", () => {
    expect(createPendingUpload(file("a.pdf", "application/pdf")).previewUrl).toBeUndefined();
  });

  it("revokes the object URL on release — one per image would otherwise leak", () => {
    const upload = createPendingUpload(file("a.png", "image/png"));
    releasePendingUpload(upload);
    expect(URL.revokeObjectURL).toHaveBeenCalledWith("blob:preview");
  });

  it("renders as the attachment it will become, so the layout does not jump", () => {
    const upload = createPendingUpload(file("a.png", "image/png"));
    const asAttachment = pendingAsAttachment(upload);

    expect(asAttachment.fileName).toBe("a.png");
    expect(asAttachment.contentType).toBe("image/png");
    expect(asAttachment.url).toBe("blob:preview");
    // Dimensions are unknown until the server decodes it.
    expect(asAttachment.width).toBeNull();
  });
});

describe("send gating", () => {
  it("refuses while anything is still in flight", () => {
    expect(canSend("hello", [pending({ status: "uploading" })])).toBe(false);
    expect(canSend("hello", [pending({ status: "queued" })])).toBe(false);
    expect(canSend("hello", [pending({ status: "confirming" })])).toBe(false);
  });

  it("allows a caption-less message once a file is confirmed", () => {
    // The attachment IS the message.
    expect(canSend("", [pending({ status: "done" })])).toBe(true);
  });

  it("refuses an empty message with no attachments", () => {
    expect(canSend("   ", [])).toBe(false);
  });

  it("allows text alongside a failed upload the user chose to keep", () => {
    expect(canSend("still worth sending", [pending({ status: "failed", attachmentId: undefined })])).toBe(true);
  });

  it("only references confirmed attachments", () => {
    const ids = confirmedAttachmentIds([
      pending({ localId: "a", attachmentId: "att-a", status: "done" }),
      pending({ localId: "b", attachmentId: undefined, status: "uploading" }),
      pending({ localId: "c", attachmentId: undefined, status: "failed" })
    ]);
    // A pending upload has no server id; sending it would reference an id the server never issued.
    expect(ids).toEqual(["att-a"]);
  });
});

describe("error translation", () => {
  it("turns server codes into something a user can act on", () => {
    expect(describeUploadError("file_too_large")).toContain("25 MB");
    expect(describeUploadError("file_infected")).toContain("security scan");
    expect(describeUploadError("upload_not_found")).toContain("expired");
    expect(describeUploadError("scan_unavailable")).toContain("Try again");
  });

  it("still says something useful for an unknown code", () => {
    expect(describeUploadError("something_new")).toBe("That file could not be uploaded.");
    expect(describeUploadError(undefined)).toBe("That file could not be uploaded.");
  });

  it("recovers the code from a server action error", () => {
    expect(extractErrorCode(new Error('Request failed: {"error":"file_too_large"}'))).toBe("file_too_large");
    expect(extractErrorCode(new Error("boom"))).toBeUndefined();
  });
});

describe("formatting", () => {
  it("formats sizes in binary units", () => {
    expect(readableSize(512)).toBe("512 B");
    expect(readableSize(2048)).toBe("2 KB");
    expect(readableSize(5 * 1024 * 1024)).toBe("5.0 MB");
  });

  it("recognises images", () => {
    expect(isImage("image/png")).toBe(true);
    expect(isImage("application/pdf")).toBe(false);
  });
});

describe("presigned upload transport", () => {
  class FakeXhr {
    static instances: FakeXhr[] = [];
    upload = { onprogress: null as ((e: { lengthComputable: boolean; loaded: number; total: number }) => void) | null };
    status = 200;
    withCredentials = true;
    headers: Record<string, string> = {};
    onload: (() => void) | null = null;
    onerror: (() => void) | null = null;
    onabort: (() => void) | null = null;
    aborted = false;
    sent: unknown;
    method = "";
    url = "";

    constructor() {
      FakeXhr.instances.push(this);
    }
    open(method: string, url: string) {
      this.method = method;
      this.url = url;
    }
    setRequestHeader(k: string, v: string) {
      this.headers[k] = v;
    }
    send(body: unknown) {
      this.sent = body;
    }
    abort() {
      this.aborted = true;
      this.onabort?.();
    }
  }

  beforeEach(() => {
    FakeXhr.instances = [];
    vi.stubGlobal("XMLHttpRequest", FakeXhr);
  });

  it("PUTs to the presigned URL without credentials", async () => {
    const promise = uploadToPresignedUrl("https://storage/key", new Blob(["x"]), {
      "Content-Type": "image/png"
    });
    const xhr = FakeXhr.instances[0];

    expect(xhr.method).toBe("PUT");
    expect(xhr.url).toBe("https://storage/key");
    // A presigned URL carries its own signature — cookies or an Authorization header alongside it
    // can invalidate that signature on real object storage.
    expect(xhr.withCredentials).toBe(false);
    expect(xhr.headers["Content-Type"]).toBe("image/png");

    xhr.onload?.();
    await expect(promise).resolves.toBeUndefined();
  });

  it("reports progress as a fraction", async () => {
    const seen: number[] = [];
    const promise = uploadToPresignedUrl("https://storage/key", new Blob(["x"]), {}, {
      onProgress: (f) => seen.push(f)
    });
    const xhr = FakeXhr.instances[0];

    xhr.upload.onprogress?.({ lengthComputable: true, loaded: 25, total: 100 });
    xhr.upload.onprogress?.({ lengthComputable: true, loaded: 100, total: 100 });
    xhr.onload?.();
    await promise;

    expect(seen).toEqual([0.25, 1]);
  });

  it("maps a 413 to the size error rather than a generic failure", async () => {
    const promise = uploadToPresignedUrl("https://storage/key", new Blob(["x"]), {});
    const xhr = FakeXhr.instances[0];
    xhr.status = 413;
    xhr.onload?.();

    await expect(promise).rejects.toThrow("file_too_large");
  });

  it("rejects with network on a transport failure", async () => {
    const promise = uploadToPresignedUrl("https://storage/key", new Blob(["x"]), {});
    FakeXhr.instances[0].onerror?.();
    await expect(promise).rejects.toThrow("network");
  });

  it("aborts when the signal fires, and reports it as cancelled not failed", async () => {
    const controller = new AbortController();
    const promise = uploadToPresignedUrl("https://storage/key", new Blob(["x"]), {}, {
      signal: controller.signal
    });

    controller.abort();
    await expect(promise).rejects.toThrow("cancelled");
    expect(FakeXhr.instances[0].aborted).toBe(true);
  });

  it("does not start when the signal is already aborted", async () => {
    const controller = new AbortController();
    controller.abort();
    const promise = uploadToPresignedUrl("https://storage/key", new Blob(["x"]), {}, {
      signal: controller.signal
    });

    await expect(promise).rejects.toThrow("cancelled");
  });
});
