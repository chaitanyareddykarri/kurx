"use client";

import { useRef, useState } from "react";
import { presignBackgroundAction, setBackgroundAction } from "@/lib/certificate-actions";
import type { CertificateTemplate } from "@/lib/certificate-api";

/**
 * Upload the certificate artwork (D-355, Phase 3): presign → PUT → record.
 *
 * Nothing is written to the template until the PUT succeeds, so a failed upload leaves the design
 * pointing at whatever it pointed at before. The accepted set is narrower than the browser's idea of an
 * image because the server has to decode it at generation time — a GIF or an AVIF it cannot read would
 * fail long after the creator believed the design was finished.
 *
 * The natural pixel size is read from the file before uploading: the editor needs the aspect ratio, and
 * reading it afterwards would mean downloading the bytes back to learn something we already had.
 */

export const ARTWORK_TYPES = ["image/png", "image/jpeg", "image/webp"];
export const MAX_ARTWORK_BYTES = 15 * 1024 * 1024;

export function BackgroundUpload({ template, onUploaded, prominent = false }: {
  template: CertificateTemplate;
  onUploaded: (template: CertificateTemplate, localPreviewUrl: string) => void;
  prominent?: boolean;
}) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const inputRef = useRef<HTMLInputElement | null>(null);

  async function readSize(file: File): Promise<{ width: number; height: number } | null> {
    try {
      const bitmap = await createImageBitmap(file);
      const size = { width: bitmap.width, height: bitmap.height };
      bitmap.close?.();
      return size;
    } catch {
      // The size is an optimisation, not a requirement — a browser that cannot decode a WebP must not
      // fail the upload over it.
      return null;
    }
  }

  async function pick(file: File) {
    setError(null);
    if (!ARTWORK_TYPES.includes(file.type)) {
      setError("Use a PNG, JPG or WebP image.");
      if (inputRef.current) inputRef.current.value = "";
      return;
    }
    if (file.size > MAX_ARTWORK_BYTES) {
      setError("That image is over 15 MB.");
      if (inputRef.current) inputRef.current.value = "";
      return;
    }

    setBusy(true);
    try {
      const size = await readSize(file);
      const presigned = await presignBackgroundAction(template.id, file.type, file.size);
      if (!("ok" in presigned) || !presigned.ok) {
        setError("error" in presigned ? presigned.error : "Upload failed.");
        return;
      }

      // Straight to storage. The bytes never pass through the Next server.
      const put = await fetch(presigned.url, {
        method: "PUT",
        headers: { "Content-Type": file.type },
        body: file
      });
      if (!put.ok) {
        setError("Upload failed. Your existing design is unchanged — try again.");
        return;
      }

      const recorded = await setBackgroundAction(template.id, {
        storageKey: presigned.key,
        contentType: file.type,
        widthPx: size?.width ?? 0,
        heightPx: size?.height ?? 0
      }, template.event_id);

      if ("error" in recorded && recorded.error) { setError(recorded.error); return; }
      if ("template" in recorded && recorded.template) {
        // A local object URL so the design appears instantly; the server's presigned URL takes over on
        // the next load.
        onUploaded(recorded.template, URL.createObjectURL(file));
      }
    } catch {
      setError("Upload failed. Your existing design is unchanged — try again.");
    } finally {
      setBusy(false);
      // Re-picking the SAME rejected file fires no change event otherwise, so the second attempt looks
      // like the button stopped working.
      if (inputRef.current) inputRef.current.value = "";
    }
  }

  const label = busy
    ? "Uploading…"
    : template.background_url ? "Choose a different picture" : "Upload Certificate";
  // Big enough to be the obvious thing to do, on a page where it IS the only thing to do. The smaller
  // variant is for later steps, where the design already exists and replacing it is the rare case.
  const chrome = prominent
    ? "inline-flex h-14 cursor-pointer items-center justify-center gap-2.5 rounded-xl bg-accent px-7 text-base font-bold text-on-accent hover:opacity-90 focus-within:outline focus-within:outline-2 focus-within:outline-offset-2 focus-within:outline-accent"
    : "inline-flex h-11 cursor-pointer items-center gap-2 rounded-lg border border-border px-4 text-sm font-semibold text-text hover:bg-elevated focus-within:outline focus-within:outline-2 focus-within:outline-offset-2 focus-within:outline-accent";

  return (
    <div>
      <label className={busy ? `${chrome} pointer-events-none opacity-60` : chrome}>
        <span aria-hidden className={prominent ? "text-2xl leading-none" : "text-lg leading-none"}>
          {busy ? "⏳" : "⬆"}
        </span>
        {label}
        <input
          ref={inputRef}
          type="file"
          accept={ARTWORK_TYPES.join(",")}
          className="sr-only"
          disabled={busy}
          aria-label={label}
          onChange={(e) => { const f = e.target.files?.[0]; if (f) void pick(f); }}
        />
      </label>
      {error ? <p role="alert" className="mt-2 text-sm font-medium text-danger">{error}</p> : null}
      {template.has_issued_certificates && !busy ? (
        <p className="mt-2 text-sm text-muted">
          Certificates have already been issued from this design. Replacing the artwork creates
          version {template.version + 1}; the ones already sent keep the version they were made with.
        </p>
      ) : null}
    </div>
  );
}
