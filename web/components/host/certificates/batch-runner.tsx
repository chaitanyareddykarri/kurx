"use client";

import { useCallback, useEffect, useState } from "react";
import {
  approveBatchAction, cancelBatchAction, getBatchAction, previewBatchAction
} from "@/lib/certificate-actions";
import { createCertificateBatch } from "@/lib/certificate-uploads";
import type { CertificateBatch, CertificateTemplate } from "@/lib/certificate-api";
import type { ColumnMapping, MappableField } from "@/lib/certificate-mapping";
import { BatchSender } from "./batch-sender";
import { BatchWithdrawal } from "./certificate-corrections";
import { ParticipantMapping } from "./participant-mapping";

/**
 * The generation flow, end to end (D-355, Phase 7): upload → map → preview → approve → watch.
 *
 * Approval is deliberately its own step with its own button and its own warning. Everything before it is
 * cheap and undoable — a parsed file, some rows, three sample renders. Everything after it is hundreds of
 * certificates in storage, signed, some of them already emailed. The gate is the only thing standing
 * between a mistyped event name and hundreds of apologies, so it is never combined with the upload.
 *
 * Once approved the run happens in the background, and this polls. A progress number that stops moving is
 * the failure mode to avoid here, which is why a finished-but-incomplete run says so explicitly rather
 * than showing a full bar.
 */

/** How often a running batch is re-checked. Slow enough not to hammer the API, fast enough that the
 *  number visibly moves. */
const POLL_MS = 3000;

type Stage = "map" | "preview" | "running";

export function BatchRunner({ eventId, template, fields, onFinished }: {
  eventId: string;
  template: CertificateTemplate;
  fields: MappableField[];
  onFinished?: (batch: CertificateBatch) => void;
}) {
  const [stage, setStage] = useState<Stage>("map");
  const [batch, setBatch] = useState<CertificateBatch | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const running = batch?.status === "approved" || batch?.status === "generating";

  // Polls only while the run is actually in flight. A finished batch stops the timer rather than
  // re-fetching a row that will never change again.
  useEffect(() => {
    if (!batch || !running) return;
    const timer = setInterval(async () => {
      const result = await getBatchAction(batch.id);
      if (result.ok) {
        setBatch(result.batch);
        if (result.batch.status !== "approved" && result.batch.status !== "generating") {
          onFinished?.(result.batch);
        }
      }
    }, POLL_MS);
    return () => clearInterval(timer);
  }, [batch, running, onFinished]);

  const confirmMapping = useCallback(async (
    file: File, mapping: ColumnMapping, name: string
  ) => {
    setBusy(true);
    setError(null);
    try {
      const result = await createCertificateBatch(eventId, { name, templateId: template.id, file, mapping });
      if (!result.ok) { setError(result.error); return; }

      // Straight on to rendering samples: the organiser has just confirmed a mapping and the next thing
      // they want is to see what it produced, not another button.
      const preview = await previewBatchAction(result.value.id);
      if (!preview.ok) { setBatch(result.value); setError(preview.error); setStage("preview"); return; }

      setBatch(preview.batch);
      setStage("preview");
    } catch {
      setError("That run could not be started. Nothing has been generated — try again.");
    } finally {
      setBusy(false);
    }
  }, [eventId, template.id]);

  async function approve() {
    if (!batch) return;
    setBusy(true);
    setError(null);
    try {
      const result = await approveBatchAction(eventId, batch.id);
      if (!result.ok) { setError(result.error); return; }
      setBatch(result.batch);
      setStage("running");
    } catch {
      setError("Generation could not be started. Nothing has been generated — try again.");
    } finally {
      setBusy(false);
    }
  }

  async function cancel() {
    if (!batch) return;
    setBusy(true);
    try {
      const result = await cancelBatchAction(eventId, batch.id);
      if (result.ok) setBatch(result.batch); else setError(result.error);
    } catch {
      setError("That run could not be stopped. Refresh to see where it got to.");
    } finally {
      setBusy(false);
    }
  }

  if (stage === "map") {
    return (
      <MapStage
        eventId={eventId}
        fields={fields}
        busy={busy}
        error={error}
        onConfirmed={confirmMapping}
      />
    );
  }

  if (!batch) return null;

  if (stage === "preview") {
    return (
      <div className="space-y-6">
        <div>
          <h3 className="text-base font-semibold text-text">Check these before you continue</h3>
          <p className="mt-1 text-sm text-muted">
            These are real certificates from your file, rendered exactly as the rest will be.
            {" "}{batch.row_count.toLocaleString()} will be generated.
          </p>
        </div>

        <div className="grid gap-4 sm:grid-cols-3">
          {batch.preview_urls.map((url, i) => (
            // eslint-disable-next-line @next/next/no-img-element
            <img key={url} src={url} alt={`Sample certificate ${i + 1}`}
                 className="w-full rounded border border-border" />
          ))}
        </div>

        {error && <p role="alert" className="text-sm text-danger">{error}</p>}

        <div className="rounded-md bg-warning/10 p-4">
          <p className="text-sm text-warning">
            Generating creates {batch.row_count.toLocaleString()} certificates. Correcting a mistake
            afterwards means revoking and reissuing every one of them, so check the spelling, the dates and
            the names now.
          </p>
        </div>

        <div className="flex flex-wrap gap-3">
          <button
            type="button"
            disabled={busy}
            onClick={() => void approve()}
            className="rounded-md bg-accent px-4 py-2 text-sm font-semibold text-on-accent disabled:opacity-50"
          >
            {busy ? "Starting…" : `Generate ${batch.row_count.toLocaleString()} certificates`}
          </button>
          <button
            type="button"
            disabled={busy}
            onClick={() => { setBatch(null); setStage("map"); setError(null); }}
            className="rounded-md border border-border-strong px-4 py-2 text-sm"
          >
            Start over
          </button>
        </div>
      </div>
    );
  }

  return <RunningStage eventId={eventId} batch={batch} busy={busy} error={error} onCancel={cancel} />;
}

/** The upload + mapping step, wrapped so the run can be named before it is created. */
function MapStage({ eventId, fields, busy, error, onConfirmed }: {
  eventId: string;
  fields: MappableField[];
  busy: boolean;
  error: string | null;
  onConfirmed: (file: File, mapping: ColumnMapping, name: string) => void;
}) {
  const [name, setName] = useState("");
  const [file, setFile] = useState<File | null>(null);

  return (
    <div className="space-y-6">
      <label className="block">
        <span className="text-sm font-medium text-text">Name this run</span>
        <input
          value={name}
          onChange={(e) => setName(e.target.value)}
          maxLength={120}
          placeholder="Participation certificates"
          className="mt-1 w-full rounded border-border-strong text-sm"
        />
      </label>

      <ParticipantMapping
        eventId={eventId}
        fields={fields}
        onFileSelected={setFile}
        onConfirmed={({ mapping }) => {
          if (file) onConfirmed(file, mapping, name.trim() || "Certificate run");
        }}
      />

      {busy && <p className="text-sm text-muted">Reading your file and rendering samples…</p>}
      {error && <p role="alert" className="text-sm text-danger">{error}</p>}
    </div>
  );
}

function RunningStage({ eventId, batch, busy, error, onCancel }: {
  eventId: string;
  batch: CertificateBatch;
  busy: boolean;
  error: string | null;
  onCancel: () => void;
}) {
  const done = batch.issued_count;
  const total = Math.max(batch.row_count, 1);
  const percent = Math.min(100, Math.round((done / total) * 100));
  const finished = batch.status === "completed" || batch.status === "failed"
    || batch.status === "cancelled";

  return (
    <div className="space-y-4">
      <h3 className="text-base font-semibold text-text">{batch.name}</h3>

      <div className="h-2 w-full overflow-hidden rounded bg-border">
        <div className="h-full bg-accent transition-all" style={{ width: `${percent}%` }} />
      </div>
      <p className="text-sm text-text">
        {done.toLocaleString()} of {batch.row_count.toLocaleString()} certificates generated.
      </p>

      {batch.status === "completed" && (
        <p className="rounded-md bg-success/10 p-3 text-sm text-success">All certificates are ready.</p>
      )}

      {/* A partial run says so. Showing a full bar on a run that dropped four people is how nobody finds
          out those four never got theirs. */}
      {batch.status === "failed" && (
        <div role="alert" className="rounded-md bg-danger/10 p-3 text-sm text-danger">
          <p>
            {(batch.row_count - batch.issued_count).toLocaleString()} of {batch.row_count.toLocaleString()}{" "}
            certificates could not be generated.
          </p>
          {batch.failed_rows.length > 0 && (
            <p className="mt-1">
              Rows in your file: {batch.failed_rows.slice(0, 20).join(", ")}
              {batch.failed_rows.length > 20 ? "…" : ""}.
            </p>
          )}
        </div>
      )}

      {batch.status === "cancelled" && (
        <p className="rounded-md bg-elevated p-3 text-sm text-text">
          This run was stopped. The {batch.issued_count.toLocaleString()} certificates it had already
          generated still exist.
        </p>
      )}

      {error && <p role="alert" className="text-sm text-danger">{error}</p>}

      {!finished && (
        <button
          type="button"
          disabled={busy}
          onClick={onCancel}
          className="rounded-md border border-border-strong px-4 py-2 text-sm disabled:opacity-50"
        >
          Stop this run
        </button>
      )}

      {/* Sending is offered only once generating has stopped. Emailing a run that is still producing
          certificates would send some people theirs and quietly leave the rest out. */}
      {finished && batch.issued_count > 0 && (
        <div className="border-t border-border pt-4">
          <h4 className="text-sm font-semibold text-text">Send them out</h4>
          <div className="mt-3">
            <BatchSender eventId={eventId} batch={batch} />
          </div>
        </div>
      )}

      {/* Kept at the bottom and behind a link rather than beside the send button: withdrawing a whole run
          is permanent and publicly visible, and it should take a deliberate reach to get to. */}
      {finished && batch.issued_count > 0 && (
        <div className="border-t border-border pt-4">
          <BatchWithdrawal
            eventId={eventId}
            batchId={batch.id}
            issuedCount={batch.issued_count}
          />
        </div>
      )}
    </div>
  );
}
