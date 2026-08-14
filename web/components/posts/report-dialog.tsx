"use client";

import { useState } from "react";
import { reportContentAction } from "@/lib/posts-actions";

/// Reasons are a closed list rather than free text: a controlled vocabulary is what makes the
/// moderation queue triageable, and the optional note carries anything the list does not cover.
/// Kept identical to the Flutter sheet so one queue sees one vocabulary.
const REASONS: Record<string, string> = {
  spam: "Spam or misleading",
  harassment: "Harassment or bullying",
  hate: "Hate speech",
  violence: "Violence or threats",
  sexual: "Sexual content",
  misinformation: "False information",
  impersonation: "Impersonation",
  other: "Something else"
};

export function ReportDialog({
  entityType,
  entityId,
  onClose
}: {
  entityType: "post" | "post_comment";
  entityId: string;
  onClose: () => void;
}) {
  const [reason, setReason] = useState<string | null>(null);
  const [details, setDetails] = useState("");
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [done, setDone] = useState(false);

  async function submit() {
    if (!reason) return;
    setPending(true);
    setError(null);
    const result = await reportContentAction(entityType, entityId, reason, details.trim() || undefined);
    setPending(false);
    if (result && "error" in result) setError(result.error);
    else setDone(true);
  }

  return (
    <div
      className="fixed inset-0 z-50 grid place-items-center bg-black/50 p-4"
      role="dialog"
      aria-modal="true"
      aria-label="Report content"
      onClick={onClose}
    >
      <div
        className="w-full max-w-md rounded-lg border border-border bg-surface p-4"
        onClick={(e) => e.stopPropagation()}
      >
        {done ? (
          <>
            <h2 className="text-sm font-semibold text-text">Thanks — reported</h2>
            <p className="mt-2 text-sm text-muted">Our team will review it.</p>
            <div className="mt-3 flex justify-end">
              <button
                type="button"
                onClick={onClose}
                className="rounded-md bg-accent px-3 py-1.5 text-sm font-medium text-on-accent"
              >
                Close
              </button>
            </div>
          </>
        ) : (
          <>
            <h2 className="text-sm font-semibold text-text">
              Report this {entityType === "post_comment" ? "comment" : "post"}
            </h2>

            <fieldset className="mt-3 space-y-1">
              <legend className="sr-only">Reason</legend>
              {Object.entries(REASONS).map(([key, label]) => (
                <label key={key} className="flex items-center gap-2 text-sm text-text">
                  <input
                    type="radio"
                    name="report-reason"
                    value={key}
                    checked={reason === key}
                    onChange={() => setReason(key)}
                  />
                  {label}
                </label>
              ))}
            </fieldset>

            <label htmlFor="report-details" className="mt-3 block text-xs text-muted">
              Anything else? (optional)
            </label>
            <textarea
              id="report-details"
              value={details}
              onChange={(e) => setDetails(e.target.value)}
              rows={3}
              maxLength={500}
              className="mt-1 w-full rounded-md border border-border-strong bg-background px-3 py-2 text-sm text-text"
            />

            {error ? <p className="mt-2 text-xs text-danger">{error}</p> : null}

            <div className="mt-3 flex justify-end gap-2">
              <button
                type="button"
                onClick={onClose}
                className="rounded-md px-3 py-1.5 text-sm text-muted hover:bg-elevated hover:text-text"
              >
                Cancel
              </button>
              <button
                type="button"
                onClick={submit}
                disabled={!reason || pending}
                className="rounded-md bg-accent px-3 py-1.5 text-sm font-medium text-on-accent disabled:opacity-50"
              >
                {pending ? "Reporting…" : "Report"}
              </button>
            </div>
          </>
        )}
      </div>
    </div>
  );
}
