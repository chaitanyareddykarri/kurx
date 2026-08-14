import type { ChatAttachment } from "@/lib/chat-api";

/// Framework-free upload logic, kept out of React so it can be tested directly and so the rules
/// cannot drift from the Flutter client (`chat_attachment.dart`, D-111).

export type UploadStatus = "queued" | "uploading" | "confirming" | "done" | "failed" | "cancelled";

/// One file moving through presign → PUT → confirm.
export type PendingUpload = {
  /// Local id. Never a server attachment id — those only exist after confirm.
  localId: string;
  file: File;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  status: UploadStatus;
  /// 0..1, meaningful while `uploading`.
  progress: number;
  /// Server error code, translated for display by `describeUploadError`.
  error?: string;
  /// Set once confirmed — the id the message will reference.
  attachmentId?: string;
  /// Object URL for a local image preview, so a pending image looks like the image it will become.
  previewUrl?: string;
};

let counter = 0;

/// Browser MIME sniffing is a hint, nothing more — the server re-derives the type from the bytes and
/// refuses anything the magic bytes contradict. This exists so an obviously wrong pick fails fast,
/// never to decide what is allowed.
const EXTENSION_TYPES: Record<string, string> = {
  jpg: "image/jpeg",
  jpeg: "image/jpeg",
  png: "image/png",
  gif: "image/gif",
  webp: "image/webp",
  pdf: "application/pdf",
  docx: "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
  xlsx: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
  pptx: "application/vnd.openxmlformats-officedocument.presentationml.presentation",
  csv: "text/csv",
  txt: "text/plain",
  log: "text/plain",
  md: "text/plain",
  zip: "application/zip",
  // Voice notes and video (D-295). The server's magic-byte check is the real gate; these exist so an
  // obviously wrong pick fails before a presign is spent.
  mp3: "audio/mpeg",
  m4a: "audio/mp4",
  ogg: "audio/ogg",
  opus: "audio/ogg",
  weba: "audio/webm",
  mp4: "video/mp4",
  // iOS Safari's camera capture produces QuickTime, which the server accepts as its own type (D-298).
  mov: "video/quicktime",
  webm: "video/webm"
};

/// Resolves the content type to declare at presign.
///
/// The extension wins over `file.type`: browsers report inconsistent types for Office formats and
/// sometimes an empty string, whereas the extension is what the server's extension/content-type
/// agreement check actually compares against. An unknown extension deliberately yields
/// `application/octet-stream`, which the allow-list rejects — an unrecognised file is refused rather
/// than smuggled through under a plausible-looking type.
export function resolveContentType(file: { name: string; type?: string }): string {
  const ext = file.name.split(".").pop()?.toLowerCase() ?? "";
  // No fallback to `file.type`. A browser-claimed type for an unrecognised extension is exactly the
  // smuggling this is meant to prevent — `payload.exe` reporting `image/png` must not be forwarded
  // as an image. Unknown extension ⇒ octet-stream ⇒ refused by the server's allow-list.
  return EXTENSION_TYPES[ext] ?? "application/octet-stream";
}

export function createPendingUpload(file: File): PendingUpload {
  const contentType = resolveContentType(file);
  return {
    localId: `upload-${Date.now()}-${(counter += 1)}`,
    file,
    fileName: file.name,
    contentType,
    sizeBytes: file.size,
    status: "queued",
    progress: 0,
    previewUrl:
      contentType.startsWith("image/") && typeof URL !== "undefined" && URL.createObjectURL
        ? URL.createObjectURL(file)
        : undefined
  };
}

/// Object URLs are a leak if never revoked — one per pending image, held until the tab closes.
export function releasePendingUpload(upload: PendingUpload): void {
  if (upload.previewUrl && typeof URL !== "undefined" && URL.revokeObjectURL) {
    URL.revokeObjectURL(upload.previewUrl);
  }
}

/// Server error codes translated into something a user can act on. "File too large" tells them what
/// to do; "upload failed" does not.
export function describeUploadError(code: string | undefined): string {
  switch (code) {
    case "file_too_large":
      return "That file is over the 25 MB limit.";
    case "unsupported_file_type":
      return "That file type is not allowed.";
    case "extension_mismatch":
      return "That file extension does not match its contents.";
    case "file_infected":
      return "That file was rejected by a security scan.";
    case "scan_unavailable":
      return "The file could not be checked right now. Try again shortly.";
    case "upload_not_allowed":
      return "You cannot upload files in this chat.";
    case "upload_not_found":
      return "The upload expired. Choose the file again.";
    case "forbidden":
      return "You do not have permission to upload here.";
    case "network":
      return "The upload was interrupted. Check your connection and retry.";
    default:
      return "That file could not be uploaded.";
  }
}

export function readableSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${Math.round(bytes / 1024)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

export function isImage(contentType: string): boolean {
  return contentType.startsWith("image/");
}

/// D-298. A voice note that downloads as a file is not a voice note, and a video the reader has to
/// save before watching is not a video — both play in place.
export function isAudio(contentType: string): boolean {
  return contentType.startsWith("audio/");
}

export function isVideo(contentType: string): boolean {
  return contentType.startsWith("video/");
}

/// PUTs bytes to a presigned URL with progress and cancellation.
///
/// XMLHttpRequest rather than fetch, deliberately: fetch has no upload-progress event, and this is
/// the one place the UI needs it. `credentials` are never attached — a presigned URL carries its own
/// signature, and sending cookies or an Authorization header alongside it can invalidate the
/// signature on real object storage.
export function uploadToPresignedUrl(
  url: string,
  file: Blob,
  headers: Record<string, string>,
  opts: { onProgress?: (fraction: number) => void; signal?: AbortSignal } = {}
): Promise<void> {
  return new Promise((resolve, reject) => {
    const xhr = new XMLHttpRequest();
    xhr.open("PUT", url, true);
    xhr.withCredentials = false;

    for (const [key, value] of Object.entries(headers)) xhr.setRequestHeader(key, value);

    xhr.upload.onprogress = (event) => {
      if (event.lengthComputable && event.total > 0) opts.onProgress?.(event.loaded / event.total);
    };

    xhr.onload = () =>
      xhr.status >= 200 && xhr.status < 300
        ? resolve()
        : reject(new Error(xhr.status === 413 ? "file_too_large" : "network"));

    xhr.onerror = () => reject(new Error("network"));
    xhr.onabort = () => reject(new Error("cancelled"));

    opts.signal?.addEventListener("abort", () => xhr.abort(), { once: true });
    if (opts.signal?.aborted) {
      xhr.abort();
      return;
    }

    xhr.send(file);
  });
}

/// Extracts the backend's `error` code from a failed server action.
///
/// Server actions surface an Error whose message carries the RFC7807 body, so the code has to be
/// recovered from the text rather than a typed field.
export function extractErrorCode(error: unknown): string | undefined {
  const message = error instanceof Error ? error.message : String(error ?? "");
  const known = [
    "file_too_large",
    "unsupported_file_type",
    "extension_mismatch",
    "file_infected",
    "scan_unavailable",
    "upload_not_allowed",
    "upload_not_found",
    "invalid_storage_key",
    "forbidden",
    "cancelled",
    "network"
  ];
  return known.find((code) => message.includes(code));
}

/// Only fully-confirmed uploads can be referenced by a message. A pending one has no server id, so
/// sending would reference an id the server never issued.
export function confirmedAttachmentIds(uploads: readonly PendingUpload[]): string[] {
  return uploads
    .filter((u) => u.status === "done" && u.attachmentId)
    .map((u) => u.attachmentId as string);
}

/// A message may be sent when nothing is still in flight and at least one of body/attachments has
/// content. An empty body with a file is valid — the attachment is the message.
export function canSend(body: string, uploads: readonly PendingUpload[]): boolean {
  const inFlight = uploads.some(
    (u) => u.status === "queued" || u.status === "uploading" || u.status === "confirming"
  );
  if (inFlight) return false;
  return body.trim().length > 0 || confirmedAttachmentIds(uploads).length > 0;
}

/// Renders a pending upload as the attachment it will become, so the layout does not jump when it
/// confirms. Width/height are unknown until the server decodes the image.
export function pendingAsAttachment(upload: PendingUpload): ChatAttachment {
  return {
    id: upload.localId,
    url: upload.previewUrl ?? "",
    fileName: upload.fileName,
    contentType: upload.contentType,
    sizeBytes: upload.sizeBytes,
    width: null,
    height: null
  };
}
