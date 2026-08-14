"use client";

import {
  AlertCircle, Download, FileArchive, FileSpreadsheet, FileText, File as FileIcon,
  Image as ImageIcon, Presentation, X
} from "lucide-react";
import { useCallback, useEffect, useRef, useState } from "react";
import { attachmentUrlAction } from "@/lib/chat-actions";
import type { ChatAttachment } from "@/lib/chat-api";
import {
  describeUploadError,
  isAudio,
  isImage,
  isVideo,
  readableSize,
  type PendingUpload
} from "@/lib/chat-upload";

function iconFor(contentType: string) {
  if (contentType === "application/pdf") return FileText;
  if (contentType.startsWith("text/")) return FileText;
  if (contentType === "application/zip") return FileArchive;
  if (contentType.includes("sheet")) return FileSpreadsheet;
  if (contentType.includes("presentation")) return Presentation;
  if (contentType.startsWith("image/")) return ImageIcon;
  return FileIcon;   // unknown type: still a usable card
}

/// A confirmed attachment on a message.
///
/// A pending upload renders through the same component (with `pending` supplied) so the layout is
/// identical before and after confirmation and nothing jumps when the upload completes.
export function AttachmentView({
  attachment,
  pending,
  onCancel,
  onRetry,
  onRemove
}: {
  attachment: ChatAttachment;
  pending?: PendingUpload;
  onCancel?: () => void;
  onRetry?: () => void;
  onRemove?: () => void;
}) {
  if (pending?.status === "failed" || pending?.status === "cancelled") {
    return (
      <FailedAttachment
        fileName={attachment.fileName}
        code={pending.error}
        cancelled={pending.status === "cancelled"}
        onRetry={onRetry}
        onRemove={onRemove}
      />
    );
  }

  if (isImage(attachment.contentType))
    return <ImageAttachment attachment={attachment} pending={pending} onCancel={onCancel} />;

  // D-298 — media plays in place. Until the upload confirms there is no signed URL to play, so an
  // in-flight recording still renders as a document card with its progress.
  if (attachment.url && (isAudio(attachment.contentType) || isVideo(attachment.contentType)))
    return <MediaAttachment attachment={attachment} />;

  return <DocumentAttachment attachment={attachment} pending={pending} onCancel={onCancel} />;
}

/// Audio and video (D-298).
///
/// `preload="metadata"` rather than `auto`: a room with twenty voice notes would otherwise download
/// all twenty on open. Metadata is enough to draw the duration and the scrubber.
function MediaAttachment({ attachment }: { attachment: ChatAttachment }) {
  const audio = isAudio(attachment.contentType);

  return (
    <div className="mb-1 max-w-[18rem]">
      {audio ? (
        // eslint-disable-next-line jsx-a11y/media-has-caption -- a voice note has no caption track
        <audio
          controls
          preload="metadata"
          src={attachment.url}
          className="w-full"
          aria-label={`Voice note ${attachment.fileName}`}
        />
      ) : (
        // eslint-disable-next-line jsx-a11y/media-has-caption -- attendee upload, no caption track
        <video
          controls
          playsInline
          preload="metadata"
          src={attachment.url}
          className="w-full rounded-lg"
          aria-label={`Video ${attachment.fileName}`}
        />
      )}
      <p className="mt-0.5 truncate text-[11px] text-muted">
        {attachment.fileName} · {readableSize(attachment.sizeBytes)}
      </p>
    </div>
  );
}

function ImageAttachment({
  attachment,
  pending,
  onCancel
}: {
  attachment: ChatAttachment;
  pending?: PendingUpload;
  onCancel?: () => void;
}) {
  const [lightboxOpen, setLightboxOpen] = useState(false);
  const inFlight = pending && pending.status !== "done";

  return (
    <div className="relative mb-1 max-w-[18rem] overflow-hidden rounded-lg">
      {attachment.url ? (
        <button
          type="button"
          disabled={Boolean(inFlight)}
          onClick={() => setLightboxOpen(true)}
          className="block w-full disabled:cursor-default"
          aria-label={`Open image ${attachment.fileName}`}
        >
          {/* eslint-disable-next-line @next/next/no-img-element -- storage URLs are signed and
              short-lived, so next/image's optimizer cannot fetch or cache them. */}
          <img
            src={attachment.url}
            alt={attachment.fileName}
            // Lazy + async decode keeps a long history from decoding every image up front, and
            // keeps decoding off the main thread.
            loading="lazy"
            decoding="async"
            width={attachment.width ?? undefined}
            height={attachment.height ?? undefined}
            className="max-h-64 w-auto object-cover"
          />
        </button>
      ) : (
        <div className="flex h-32 w-48 items-center justify-center bg-elevated text-muted">
          <ImageIcon size={20} aria-hidden />
        </div>
      )}

      {inFlight && <UploadOverlay pending={pending!} onCancel={onCancel} />}

      {lightboxOpen && attachment.url && (
        <Lightbox url={attachment.url} fileName={attachment.fileName} onClose={() => setLightboxOpen(false)} />
      )}
    </div>
  );
}

function DocumentAttachment({
  attachment,
  pending,
  onCancel
}: {
  attachment: ChatAttachment;
  pending?: PendingUpload;
  onCancel?: () => void;
}) {
  const Icon = iconFor(attachment.contentType);
  const inFlight = pending && pending.status !== "done";
  const [busy, setBusy] = useState(false);
  const [failed, setFailed] = useState(false);

  /// Always mints a fresh signed URL. Never cached: the signature expires and the server re-checks
  /// room membership every time, so a stored URL would fail in a way the user cannot act on.
  const download = useCallback(async () => {
    setBusy(true);
    setFailed(false);
    try {
      const url = await attachmentUrlAction(attachment.id);
      window.open(url, "_blank", "noopener,noreferrer");
    } catch {
      setFailed(true);
    } finally {
      setBusy(false);
    }
  }, [attachment.id]);

  return (
    <div className="mb-1">
      <div
        className="flex min-h-[3.25rem] w-full max-w-sm items-center gap-3 rounded-lg bg-elevated p-2"
        aria-busy={inFlight ? true : undefined}
      >
        <span className="flex h-9 w-9 shrink-0 items-center justify-center">
          {inFlight ? (
            <ProgressRing value={pending!.progress} />
          ) : (
            <Icon size={20} className="text-accent" aria-hidden />
          )}
        </span>

        <span className="min-w-0 flex-1">
          <span className="block truncate text-sm font-semibold">{attachment.fileName}</span>
          <span className="block text-xs text-muted">
            {inFlight
              ? `Uploading… ${Math.round(pending!.progress * 100)}%`
              : readableSize(attachment.sizeBytes)}
          </span>
        </span>

        {inFlight ? (
          onCancel && (
            <button
              type="button"
              onClick={onCancel}
              aria-label={`Cancel upload of ${attachment.fileName}`}
              className="rounded p-1.5 text-muted hover:bg-surface hover:text-text"
            >
              <X size={16} aria-hidden />
            </button>
          )
        ) : (
          <button
            type="button"
            onClick={download}
            disabled={busy}
            aria-label={`Download ${attachment.fileName}`}
            className="rounded p-1.5 text-muted hover:bg-surface hover:text-text disabled:opacity-50"
          >
            <Download size={16} aria-hidden />
          </button>
        )}
      </div>

      {failed && (
        <p role="alert" className="mt-1 text-xs text-danger">
          This file could not be opened. It may have been removed, or your access may have changed.
        </p>
      )}
    </div>
  );
}

function FailedAttachment({
  fileName,
  code,
  cancelled,
  onRetry,
  onRemove
}: {
  fileName: string;
  code?: string;
  cancelled: boolean;
  onRetry?: () => void;
  onRemove?: () => void;
}) {
  return (
    <div
      role="alert"
      className="mb-1 flex max-w-sm items-start gap-2 rounded-lg border border-danger/50 p-2 text-sm"
    >
      <AlertCircle size={16} className="mt-0.5 shrink-0 text-danger" aria-hidden />
      <span className="min-w-0 flex-1">
        <span className="block truncate font-semibold">{fileName}</span>
        <span className="block text-xs text-danger">
          {cancelled ? "Upload cancelled." : describeUploadError(code)}
        </span>
        <span className="mt-1 flex gap-3 text-xs">
          {onRetry && (
            <button type="button" onClick={onRetry} className="font-semibold text-accent hover:underline">
              Retry
            </button>
          )}
          {onRemove && (
            <button type="button" onClick={onRemove} className="text-muted hover:underline">
              Remove
            </button>
          )}
        </span>
      </span>
    </div>
  );
}

function UploadOverlay({ pending, onCancel }: { pending: PendingUpload; onCancel?: () => void }) {
  return (
    <div className="absolute inset-0 flex flex-col items-center justify-center gap-2 bg-black/40">
      <ProgressRing value={pending.progress} light />
      {onCancel && (
        <button
          type="button"
          onClick={onCancel}
          aria-label={`Cancel upload of ${pending.fileName}`}
          className="rounded bg-black/50 px-2 py-1 text-xs text-white hover:bg-black/70"
        >
          Cancel
        </button>
      )}
    </div>
  );
}

/// Progress as a real progressbar role, so a screen reader announces the percentage rather than the
/// user only seeing a spinner.
function ProgressRing({ value, light = false }: { value: number; light?: boolean }) {
  const percent = Math.round(value * 100);
  return (
    <span
      role="progressbar"
      aria-valuenow={percent}
      aria-valuemin={0}
      aria-valuemax={100}
      aria-label={`Upload progress ${percent} percent`}
      className={`inline-flex h-7 w-7 items-center justify-center rounded-full border-2 text-[9px] font-bold ${
        light ? "border-white text-white" : "border-accent text-accent"
      }`}
    >
      {percent}
    </span>
  );
}

/// Full-size image view. Focus is trapped to the close button and restored on dismiss, and Escape
/// closes — the minimum for a modal that is not a focus trap for keyboard users.
function Lightbox({ url, fileName, onClose }: { url: string; fileName: string; onClose: () => void }) {
  const closeRef = useRef<HTMLButtonElement>(null);
  const previouslyFocused = useRef<HTMLElement | null>(null);

  useEffect(() => {
    previouslyFocused.current = document.activeElement as HTMLElement | null;
    closeRef.current?.focus();

    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") onClose();
    };
    window.addEventListener("keydown", onKey);
    return () => {
      window.removeEventListener("keydown", onKey);
      previouslyFocused.current?.focus();
    };
  }, [onClose]);

  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-label={fileName}
      onClick={onClose}
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/90 p-4"
    >
      <button
        ref={closeRef}
        type="button"
        onClick={onClose}
        aria-label="Close image"
        className="absolute right-4 top-4 rounded p-2 text-white hover:bg-white/10"
      >
        <X size={20} aria-hidden />
      </button>
      {/* eslint-disable-next-line @next/next/no-img-element -- signed, short-lived storage URL */}
      <img
        src={url}
        alt={fileName}
        onClick={(e) => e.stopPropagation()}
        className="max-h-full max-w-full object-contain"
      />
    </div>
  );
}
