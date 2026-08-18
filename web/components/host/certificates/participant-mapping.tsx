"use client";

import { useMemo, useRef, useState } from "react";
import { uploadParticipantList } from "@/lib/certificate-uploads";
import type { SpreadsheetPreview } from "@/lib/certificate-api";
import {
  assign, missingRequired, resolveRow, rowsMissingValues, unmappedColumns,
  type ColumnMapping, type MappableField
} from "@/lib/certificate-mapping";

/**
 * Upload a participant list and confirm what each column means (D-355, Phase 6).
 *
 * The mapping is the step this screen exists for. Getting it wrong is not a rendering bug someone
 * catches in a preview — it prints the wrong words on every certificate in the run, correctly and
 * confidently, and is discovered after they are sent. So the server's guess is pre-filled but never
 * hidden: real values from the file sit under every column, and the design's placeholders are listed
 * with what is still unfilled, so a wrong guess is something you see rather than something you trust.
 *
 * Nothing is generated here. This reads the file and hands back a confirmed mapping; the batch itself is
 * created by the caller.
 */

export const PARTICIPANT_FILE_TYPES = ".csv,.xlsx";
export const MAX_PARTICIPANT_BYTES = 10 * 1024 * 1024;

export function ParticipantMapping({ eventId, fields, onConfirmed, onFileSelected }: {
  eventId: string;
  /** The design's placeholders. Everything required must be fed before generation is allowed. */
  fields: MappableField[];
  onConfirmed: (result: { preview: SpreadsheetPreview; mapping: ColumnMapping }) => void;
  /** The file that was read. Handed back because the caller needs the same bytes to create the run —
   *  re-uploading would mean generating from a file nobody reviewed. */
  onFileSelected?: (file: File) => void;
}) {
  const [preview, setPreview] = useState<SpreadsheetPreview | null>(null);
  const [mapping, setMapping] = useState<ColumnMapping>({});
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const inputRef = useRef<HTMLInputElement | null>(null);

  const blocking = useMemo(() => missingRequired(fields, mapping), [fields, mapping]);
  const ignored = useMemo(
    () => (preview ? unmappedColumns(preview.columns, mapping) : []), [preview, mapping]);
  const blankRows = useMemo(
    () => (preview ? rowsMissingValues(preview.sampleRows, preview.columns, mapping, fields) : []),
    [preview, mapping, fields]);

  async function pick(file: File) {
    setError(null);
    // Checked here as well as on the server so a 10MB mistake is refused before it is uploaded, not
    // after. The server's check is the one that counts.
    if (file.size > MAX_PARTICIPANT_BYTES) {
      setError("That file is over 10 MB.");
      if (inputRef.current) inputRef.current.value = "";
      return;
    }

    setBusy(true);
    try {
      const result = await uploadParticipantList(eventId, file);

      if (!result.ok) {
        setError(result.error);
        return;
      }
      setPreview(result.value);
      onFileSelected?.(file);
      // Pre-filled from the server's guess, then shown for correction — never applied silently.
      setMapping(result.value.suggestedMapping);
    } catch {
      // A Server Action can reject before any of our code runs — a body over Next's limit, a dropped
      // connection, a redirect on an expired session. Previously that left `busy` true forever and the
      // screen sat on "Reading your file…" with nothing to act on, which is a worse failure than the
      // upload not working: it gives the person no way to tell a broken file from a broken app.
      setError("That file could not be read. Check it opens as a spreadsheet, then try again.");
    } finally {
      // Always. The spinner is not allowed to outlive the attempt.
      setBusy(false);
      if (inputRef.current) inputRef.current.value = "";
    }
  }

  if (!preview) {
    return (
      <div className="rounded-lg border border-dashed border-border-strong p-8 text-center">
        <h3 className="text-base font-semibold text-text">Upload your participant list</h3>
        <p className="mt-1 text-sm text-muted">
          A .csv or .xlsx file with one row per participant and a header row naming each column.
        </p>
        <input
          ref={inputRef}
          type="file"
          accept={PARTICIPANT_FILE_TYPES}
          disabled={busy}
          className="mt-4 mx-auto block text-sm"
          onChange={(e) => { const f = e.target.files?.[0]; if (f) void pick(f); }}
        />
        {busy && <p className="mt-3 text-sm text-muted">Reading your file…</p>}
        {error && <p role="alert" className="mt-3 text-sm text-danger">{error}</p>}
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <h3 className="text-base font-semibold text-text">
          {preview.totalRows.toLocaleString()} participants
        </h3>
        <button
          type="button"
          className="min-h-11 text-sm underline lg:min-h-0"
          onClick={() => { setPreview(null); setMapping({}); }}
        >
          Upload a different file
        </button>
      </div>

      {/* Truncation is said out loud. "We generated 1000 of your 1500" must never be discovered by
          counting the results. */}
      {preview.truncated && (
        <p role="alert" className="rounded-md bg-warning/10 p-3 text-sm text-warning">
          Only the first {preview.totalRows.toLocaleString()} rows were read. Split the file and upload
          the rest separately.
        </p>
      )}

      {/* Focusable like DataTable's wrapper: a scroll region a keyboard can't reach hides columns. */}
      <div
        tabIndex={0}
        role="group"
        aria-label="Columns from your file"
        className="overflow-x-auto focus-visible:outline focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-accent"
      >
        <table className="w-full text-left text-sm">
          <thead>
            <tr>
              {preview.columns.map((column, index) => (
                <th key={`${column}-${index}`} className="border-b p-2 align-top">
                  <div className="font-semibold text-text">{column || <em>Unnamed</em>}</div>
                  <select
                    aria-label={`What is “${column}”?`}
                    value={mapping[column] ?? ""}
                    className="mt-1 w-full rounded border-border-strong text-sm font-normal"
                    onChange={(e) => setMapping(assign(mapping, column, e.target.value))}
                  >
                    <option value="">Don’t use this column</option>
                    {fields.map((field) => (
                      <option key={field.key} value={field.key}>
                        {field.label}{field.required ? " (required)" : ""}
                      </option>
                    ))}
                  </select>
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {/* Real values from the file, so a column called "Name" that actually holds team names is
                visible rather than assumed. */}
            {preview.sampleRows.map((row, r) => (
              <tr key={r}>
                {preview.columns.map((column, c) => (
                  <td key={`${column}-${c}`} className="border-b p-2 text-text">{row[c] ?? ""}</td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {ignored.length > 0 && (
        <p className="text-sm text-muted">
          Not used: {ignored.join(", ")}.
        </p>
      )}

      {blankRows.length > 0 && (
        <p role="alert" className="rounded-md bg-warning/10 p-3 text-sm text-warning">
          {blankRows.length === 1
            ? "One of the rows shown is missing a required value."
            : `${blankRows.length} of the rows shown are missing a required value.`}{" "}
          Those certificates would print with a blank space.
        </p>
      )}

      {blocking.length > 0 ? (
        <p role="alert" className="rounded-md bg-danger/10 p-3 text-sm text-danger">
          Still needed: {blocking.map((f) => f.label).join(", ")}. Choose the column that holds{" "}
          {blocking.length === 1 ? "it" : "each of them"}.
        </p>
      ) : (
        <button
          type="button"
          className="min-h-11 rounded-md bg-accent px-4 py-2 text-sm font-semibold text-on-accent lg:min-h-0"
          onClick={() => onConfirmed({ preview, mapping })}
        >
          Continue with this mapping
        </button>
      )}
    </div>
  );
}

/** The first sample row, resolved — what the caller renders as a preview certificate. Exported so the
 *  preview and the real run resolve values through exactly the same code. */
export function firstResolvedRow(preview: SpreadsheetPreview, mapping: ColumnMapping) {
  return preview.sampleRows.length
    ? resolveRow(preview.sampleRows[0], preview.columns, mapping)
    : {};
}
