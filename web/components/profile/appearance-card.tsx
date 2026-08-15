"use client";

import { useState } from "react";
import { ThemeToggle } from "@/components/layout/theme-toggle";
import { ImageUpload } from "@/components/profile/image-upload";
import { Button } from "@/components/ui/button";
import { saveAvatarAction } from "@/lib/profile-actions";

/**
 * Appearance: theme and profile picture in one place.
 *
 * The theme control is the existing `ThemeToggle`, mounted rather than reimplemented — it writes to
 * next-themes' own storage and never touches the profile request, so a picture that fails to save
 * cannot revert a theme choice, and vice versa. They share a card, not a code path.
 *
 * Unlike the picture control inside the profile form, this one owns its save: there is no surrounding
 * form to carry the key, so it PATCHes the avatar on its own. Save stays disabled until a key exists
 * and differs from what is already stored, which also makes "removed, not yet saved" a distinct state
 * from "already has no picture".
 */
export function AppearanceCard({ initialAvatarKey, initialAvatarUrl, name }: {
  initialAvatarKey: string;
  /** Presigned companion to the key (D-302) — what the preview renders. */
  initialAvatarUrl?: string | null;
  name: string;
}) {
  const [key, setKey] = useState(initialAvatarKey);
  const [savedKey, setSavedKey] = useState(initialAvatarKey);
  const [busy, setBusy] = useState(false);
  const [result, setResult] = useState<{ ok: boolean; message: string } | null>(null);
  // `ImageUpload` holds its own preview state, so reverting only this component's copy would leave the
  // thumbnail showing the abandoned picture. Bumping the token remounts it, which is the whole reset.
  const [resetToken, setResetToken] = useState(0);

  const dirty = key !== savedKey;

  function cancel() {
    setKey(savedKey);
    setResult(null);
    setResetToken((n) => n + 1);
  }

  async function save() {
    setBusy(true);
    setResult(null);
    const res = await saveAvatarAction(key);
    if (res.ok) {
      setSavedKey(key);
      setResult({ ok: true, message: key ? "Profile picture saved." : "Profile picture removed." });
    } else {
      setResult({ ok: false, message: res.error });
    }
    setBusy(false);
  }

  return (
    <div className="space-y-5">
      <div className="flex items-center justify-between gap-4">
        <div>
          <h2 className="font-semibold">Theme</h2>
          <p className="text-sm text-muted">Light or dark appearance.</p>
        </div>
        <ThemeToggle />
      </div>

      <div className="border-t border-border pt-5">
        <ImageUpload
          key={resetToken}
          slot="avatar"
          initialKey={savedKey}
          initialUrl={initialAvatarUrl}
          label="Profile picture"
          previewName={name}
          onChange={(next) => { setKey(next); setResult(null); }}
        />
        <div className="mt-3 flex flex-wrap items-center gap-3">
          <Button type="button" onClick={save} disabled={!dirty || busy}>
            {busy ? "Saving…" : "Save picture"}
          </Button>
          {dirty ? (
            <Button type="button" variant="ghost" disabled={busy} onClick={cancel}>
              Cancel
            </Button>
          ) : null}
          {result ? (
            <p role={result.ok ? "status" : "alert"} className={`text-xs ${result.ok ? "text-muted" : "text-danger"}`}>
              {result.message}
            </p>
          ) : null}
        </div>
      </div>
    </div>
  );
}
