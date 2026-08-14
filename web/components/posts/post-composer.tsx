"use client";

import { useRef, useState } from "react";
import { BarChart3, ImagePlus, Loader2, X } from "lucide-react";
import { Avatar } from "@/components/ui/avatar";
import { createPostAction, presignPostMediaAction, confirmPostMediaAction } from "@/lib/posts-actions";
import {
  POST_BODY_MAX, POST_VISIBILITIES, VISIBILITY_LABEL, MEDIA_CAPS,
  type Post, type PostVisibility, type PostMedia
} from "@/lib/posts-api";

type Draft = { id: string; name: string; media?: PostMedia; error?: string };

/// Composer for a new post. Media uploads two-step exactly like chat attachments (D-110):
/// presign → the browser PUTs bytes straight to storage → confirm. Bytes never pass through the
/// Next server, which is what keeps a 10-image post from being a 10-image request body here.
export function PostComposer({
  author,
  eventId,
  onCreated
}: {
  author: { name: string; avatarKey?: string | null };
  eventId?: string;
  onCreated?: (post: Post) => void;
}) {
  const [body, setBody] = useState("");
  const [visibility, setVisibility] = useState<PostVisibility>(eventId ? "event_participants" : "public");
  const [drafts, setDrafts] = useState<Draft[]>([]);
  const [pollOpen, setPollOpen] = useState(false);
  const [pollQuestion, setPollQuestion] = useState("");
  const [pollOptions, setPollOptions] = useState(["", ""]);
  const [pollMultiple, setPollMultiple] = useState(false);
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const fileRef = useRef<HTMLInputElement>(null);

  const uploading = drafts.some((d) => !d.media && !d.error);
  const imageCount = drafts.filter((d) => d.media?.content_type.startsWith("image/")).length;
  const canSubmit =
    !pending && !uploading &&
    (body.trim().length > 0 || drafts.some((d) => d.media) || (pollOpen && pollOptions.filter(Boolean).length >= 2));

  async function upload(files: FileList) {
    for (const file of Array.from(files)) {
      if (imageCount + drafts.length >= MEDIA_CAPS.images) {
        setError("too_many_media");
        return;
      }
      const draftId = crypto.randomUUID();
      setDrafts((d) => [...d, { id: draftId, name: file.name }]);
      try {
        const ticket = await presignPostMediaAction({
          fileName: file.name,
          contentType: file.type || "application/octet-stream",
          sizeBytes: file.size
        });
        const res = await fetch(ticket.upload_url, {
          method: "PUT",
          body: file,
          headers: { "Content-Type": file.type || "application/octet-stream" }
        });
        if (!res.ok) throw new Error("upload_failed");
        const media = await confirmPostMediaAction(ticket.media_id, ticket.storage_key);
        setDrafts((d) => d.map((x) => (x.id === draftId ? { ...x, media } : x)));
      } catch {
        setDrafts((d) => d.map((x) => (x.id === draftId ? { ...x, error: "upload_failed" } : x)));
      }
    }
  }

  async function submit() {
    setPending(true);
    setError(null);

    const form = new FormData();
    form.set("body", body);
    form.set("visibility", visibility);
    if (eventId) form.set("eventId", eventId);
    for (const d of drafts) if (d.media) form.append("mediaIds", d.media.id);

    const hasMedia = drafts.some((d) => d.media);
    form.set("kind", pollOpen ? "poll" : eventId ? "event" : hasMedia ? "images" : "text");

    if (pollOpen) {
      form.set("pollQuestion", pollQuestion);
      if (pollMultiple) form.set("pollAllowMultiple", "on");
      for (const o of pollOptions.map((o) => o.trim()).filter(Boolean)) form.append("pollOption", o);
    }

    const result = await createPostAction(form);
    setPending(false);
    if ("error" in result) {
      setError(result.error);
      return;
    }
    setBody("");
    setDrafts([]);
    setPollOpen(false);
    setPollQuestion("");
    setPollOptions(["", ""]);
    onCreated?.(result);
  }

  return (
    <div className="rounded-lg border border-border bg-surface p-4">
      <div className="flex gap-3">
        <Avatar name={author.name} src={author.avatarKey ?? undefined} size={40} />
        <div className="min-w-0 flex-1">
          <label htmlFor="composer-body" className="sr-only">
            What&apos;s happening?
          </label>
          <textarea
            id="composer-body"
            value={body}
            onChange={(e) => setBody(e.target.value)}
            rows={3}
            maxLength={POST_BODY_MAX}
            placeholder={eventId ? "Post to this event…" : "Share something with your network…"}
            className="w-full resize-none border-0 bg-transparent p-0 text-sm text-text outline-none placeholder:text-muted"
          />

          {drafts.length > 0 ? (
            <ul className="mt-2 flex flex-wrap gap-2">
              {drafts.map((d) => (
                <li
                  key={d.id}
                  className={`flex items-center gap-1.5 rounded-md border px-2 py-1 text-xs
                    ${d.error ? "border-danger text-danger" : "border-border text-muted"}`}
                >
                  {!d.media && !d.error ? <Loader2 size={12} className="animate-spin" /> : null}
                  <span className="max-w-[10rem] truncate">{d.name}</span>
                  <button
                    type="button"
                    aria-label={`Remove ${d.name}`}
                    onClick={() => setDrafts((cur) => cur.filter((x) => x.id !== d.id))}
                    className="text-muted hover:text-text"
                  >
                    <X size={12} />
                  </button>
                </li>
              ))}
            </ul>
          ) : null}

          {pollOpen ? (
            <div className="mt-3 space-y-2 rounded-md border border-border bg-background p-3">
              <input
                value={pollQuestion}
                onChange={(e) => setPollQuestion(e.target.value)}
                placeholder="Ask a question"
                aria-label="Poll question"
                className="w-full rounded-md border border-border-strong bg-surface px-2 py-1.5 text-sm text-text"
              />
              {pollOptions.map((o, i) => (
                <input
                  key={i}
                  value={o}
                  onChange={(e) => setPollOptions((cur) => cur.map((x, j) => (j === i ? e.target.value : x)))}
                  placeholder={`Option ${i + 1}`}
                  aria-label={`Poll option ${i + 1}`}
                  className="w-full rounded-md border border-border-strong bg-surface px-2 py-1.5 text-sm text-text"
                />
              ))}
              <div className="flex items-center justify-between">
                <button
                  type="button"
                  disabled={pollOptions.length >= 6}
                  onClick={() => setPollOptions((cur) => [...cur, ""])}
                  className="text-xs text-accent disabled:opacity-40"
                >
                  + Add option
                </button>
                <label className="flex items-center gap-1.5 text-xs text-muted">
                  <input
                    type="checkbox"
                    checked={pollMultiple}
                    onChange={(e) => setPollMultiple(e.target.checked)}
                  />
                  Allow multiple
                </label>
              </div>
            </div>
          ) : null}

          <div className="mt-3 flex items-center gap-2 border-t border-border pt-2">
            <input
              ref={fileRef}
              type="file"
              multiple
              accept="image/*,video/*,.pdf"
              className="hidden"
              onChange={(e) => e.target.files && upload(e.target.files)}
            />
            <button
              type="button"
              aria-label="Add media"
              onClick={() => fileRef.current?.click()}
              className="rounded-md p-1.5 text-muted hover:bg-elevated hover:text-text"
            >
              <ImagePlus size={16} />
            </button>
            <button
              type="button"
              aria-label="Add poll"
              aria-pressed={pollOpen}
              onClick={() => setPollOpen((o) => !o)}
              className={`rounded-md p-1.5 hover:bg-elevated hover:text-text ${pollOpen ? "text-accent" : "text-muted"}`}
            >
              <BarChart3 size={16} />
            </button>

            <label htmlFor="composer-visibility" className="sr-only">
              Who can see this
            </label>
            <select
              id="composer-visibility"
              value={visibility}
              onChange={(e) => setVisibility(e.target.value as PostVisibility)}
              className="rounded-md border border-border-strong bg-background px-2 py-1 text-xs text-muted"
            >
              {POST_VISIBILITIES.filter((v) => v !== "event_participants" || eventId).map((v) => (
                <option key={v} value={v}>
                  {VISIBILITY_LABEL[v]}
                </option>
              ))}
            </select>

            <span className="flex-1" />
            <span className="text-xs tabular-nums text-muted">
              {body.length}/{POST_BODY_MAX}
            </span>
            <button
              type="button"
              onClick={submit}
              disabled={!canSubmit}
              className="rounded-md bg-accent px-3 py-1.5 text-sm font-medium text-on-accent disabled:opacity-50"
            >
              {pending ? "Posting…" : "Post"}
            </button>
          </div>

          {error ? <p className="mt-2 text-xs text-danger">{error}</p> : null}
        </div>
      </div>
    </div>
  );
}
