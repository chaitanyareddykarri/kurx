"use client";

import { useRef, useState } from "react";
import { presignProfileImageAction } from "@/lib/profile-actions";
import { Avatar } from "@/components/ui/avatar";
import { Button } from "@/components/ui/button";
import { ImageEditor } from "@/components/profile/image-editor";
import {
  ACCEPT_ATTRIBUTE, MIN_IMAGE_DIMENSION, validateImageFile, validateImageSize, type Slot
} from "@/lib/image-edit";

type Phase =
  | { kind: "idle" }
  | { kind: "checking"; file: File }
  | { kind: "editing"; file: File }
  | { kind: "uploading"; percent: number };

/**
 * Pick → edit → upload, producing a storage key the surrounding form persists.
 *
 * **Nothing is uploaded until Save.** The picked file is decoded and edited entirely in the browser;
 * only the canvas output is sent. That ordering is what makes the failure modes safe — a rejected
 * file, an abandoned edit and a failed PUT all leave `value` at whatever was already saved, so the
 * existing profile image survives every one of them. The only call that can change the saved image is
 * the form submit that carries a new key.
 */
export function ImageUpload({ slot, name, initialKey, initialUrl, label, previewName, onChange }: {
  slot: Slot;
  name?: string;
  /** The storage KEY — what the form submits and the profile stores. Not renderable on its own. */
  initialKey: string;
  /** The presigned URL for that key — what the preview renders (D-302). A key fed to `<img src>`
   *  resolves against the web origin and 404s, which is the bug this pair exists to close. */
  initialUrl?: string | null;
  label: string;
  previewName: string;
  /** Notified whenever the key changes, for callers that save on their own rather than via a form. */
  onChange?: (key: string) => void;
}) {
  const [value, setValue] = useState(initialKey);
  // What the preview shows. Starts as the server's presigned URL and becomes a local object URL after
  // an upload — the client cannot presign the key it just received, and the bytes are already in hand,
  // so the freshly cropped image is the truest preview available anyway.
  const [previewUrl, setPreviewUrl] = useState<string | null>(initialUrl ?? null);
  const [phase, setPhase] = useState<Phase>({ kind: "idle" });
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const inputRef = useRef<HTMLInputElement | null>(null);

  function commit(key: string, message: string | null, preview?: string | null) {
    setValue(key);
    if (preview !== undefined) setPreviewUrl(preview);
    onChange?.(key);
    setNotice(message);
  }

  async function pick(file: File) {
    setError(null);
    setNotice(null);
    const fileError = validateImageFile(file);
    if (fileError) {
      setError(fileError);
      // Clearing the input matters: picking the SAME rejected file again fires no change event
      // otherwise, so the second attempt would look like the button had stopped working.
      if (inputRef.current) inputRef.current.value = "";
      return;
    }
    setPhase({ kind: "checking", file });
    const sizeError = await measure(file);
    if (sizeError) {
      setError(sizeError);
      setPhase({ kind: "idle" });
      if (inputRef.current) inputRef.current.value = "";
      return;
    }
    setPhase({ kind: "editing", file });
  }

  async function save(blob: Blob) {
    setError(null);
    setPhase({ kind: "uploading", percent: 0 });
    try {
      const presigned = await presignProfileImageAction(slot, "image/webp", blob.size);
      if (!presigned.ok) {
        setError(presigned.error);
        setPhase({ kind: "idle" });
        return;
      }
      setPhase({ kind: "uploading", percent: 40 });
      const res = await fetch(presigned.url, {
        method: "PUT",
        headers: { "Content-Type": "image/webp" },
        body: blob
      });
      if (!res.ok) {
        setError("Upload failed. Your existing picture is unchanged — try again.");
        setPhase({ kind: "idle" });
        return;
      }
      setPhase({ kind: "uploading", percent: 100 });
      commit(presigned.key, "Image ready. Save your profile to apply it.", URL.createObjectURL(blob));
      setPhase({ kind: "idle" });
    } catch {
      setError("Upload failed. Your existing picture is unchanged — try again.");
      setPhase({ kind: "idle" });
    } finally {
      if (inputRef.current) inputRef.current.value = "";
    }
  }

  const busy = phase.kind === "uploading" || phase.kind === "checking";

  return (
    <div>
      <span className="text-sm font-medium text-text">{label}</span>
      {name ? <input type="hidden" name={name} value={value} /> : null}

      <div className="mt-2 flex flex-wrap items-center gap-3">
        {slot === "avatar" ? (
          <Avatar name={previewName} src={previewUrl || undefined} size={56} />
        ) : (
          <div className="h-14 w-28 overflow-hidden rounded-md border border-border bg-elevated">
            {/* eslint-disable-next-line @next/next/no-img-element -- provider-swappable storage host */}
            {previewUrl ? <img src={previewUrl} alt={`${label} preview`} className="h-full w-full object-cover" /> : null}
          </div>
        )}

        <label className="inline-flex min-h-11 cursor-pointer items-center rounded-md border border-border px-3 py-2 text-sm text-text hover:bg-elevated focus-within:outline focus-within:outline-2 focus-within:outline-offset-2 focus-within:outline-accent lg:min-h-0">
          {value ? "Replace image" : "Upload image"}
          <input
            ref={inputRef}
            type="file"
            accept={ACCEPT_ATTRIBUTE}
            className="sr-only"
            disabled={busy}
            aria-label={value ? `Replace ${label.toLowerCase()}` : `Upload ${label.toLowerCase()}`}
            onChange={(e) => {
              const file = e.target.files?.[0];
              if (file) void pick(file);
            }}
          />
        </label>

        {value ? (
          <Button type="button" variant="ghost" disabled={busy}
            onClick={() => { commit("", "Image removed. Save your profile to apply it.", null); setError(null); }}>
            Remove image
          </Button>
        ) : null}
      </div>

      <p className="mt-1 text-xs text-muted">
        JPG, PNG or WebP, up to 5 MB and at least {MIN_IMAGE_DIMENSION}×{MIN_IMAGE_DIMENSION} pixels.
      </p>

      {phase.kind === "uploading" ? (
        <div className="mt-2">
          <div
            role="progressbar"
            aria-label="Upload progress"
            aria-valuenow={phase.percent}
            aria-valuemin={0}
            aria-valuemax={100}
            className="h-1.5 w-full overflow-hidden rounded-full bg-elevated"
          >
            <div className="h-full bg-accent transition-all" style={{ width: `${phase.percent}%` }} />
          </div>
          <p className="mt-1 text-xs text-muted">Uploading… {phase.percent}%</p>
        </div>
      ) : null}

      {phase.kind === "editing" ? (
        <div className="mt-3">
          <ImageEditor
            slot={slot}
            file={phase.file}
            saving={false}
            onSave={save}
            onCancel={() => {
              setPhase({ kind: "idle" });
              setNotice(null);
              if (inputRef.current) inputRef.current.value = "";
            }}
          />
        </div>
      ) : null}

      {error ? <p role="alert" className="mt-1 text-xs text-danger">{error}</p> : null}
      {notice && !error ? <p role="status" className="mt-1 text-xs text-muted">{notice}</p> : null}
    </div>
  );
}

/** Decode just far enough to read the dimensions, then reject anything too small to crop usefully.
 *  Resolves to a sentence or null, never throws — an undecodable file is a validation result here,
 *  not an exception for the caller to handle. */
function measure(file: File): Promise<string | null> {
  return new Promise((resolve) => {
    if (typeof URL.createObjectURL !== "function" || typeof Image === "undefined") return resolve(null);
    const url = URL.createObjectURL(file);
    const img = new Image();
    img.onload = () => {
      URL.revokeObjectURL(url);
      resolve(validateImageSize(img.width, img.height));
    };
    img.onerror = () => {
      URL.revokeObjectURL(url);
      resolve("That image couldn't be opened. It may be corrupt — try another file.");
    };
    img.src = url;
  });
}
