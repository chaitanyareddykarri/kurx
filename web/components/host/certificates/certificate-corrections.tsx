"use client";

import { useState } from "react";
import {
  getLineageAction, reissueCertificateAction, revokeBatchAction, revokeCertificateAction
} from "@/lib/certificate-actions";
import type { CertificateLineage } from "@/lib/certificate-api";

/**
 * Withdrawing a certificate, and correcting one (D-355, Phase 9).
 *
 * These are two different public claims and the UI keeps them apart at every step. **Withdraw** says
 * "this should not be honoured" — permanent, and the reason typed here is shown to anyone who checks the
 * certificate. **Correct** says "a fixed one exists, here it is" — the holder's old link keeps working
 * and points at the replacement.
 *
 * Offering these as one control with a dropdown is how someone with a misspelled name ends up publicly
 * marked as having had their certificate withdrawn, so they are separate buttons with separate wording.
 */

export function CertificateCorrections({ eventId, certificate, onChanged }: {
  eventId: string;
  certificate: { id: string; certificate_id: string; recipient_name: string; status: string };
  onChanged?: () => void;
}) {
  const [mode, setMode] = useState<"idle" | "revoke" | "reissue">("idle");
  const [reason, setReason] = useState("");
  const [name, setName] = useState(certificate.recipient_name);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [lineage, setLineage] = useState<CertificateLineage[] | null>(null);

  const live = certificate.status === "issued";

  async function submit() {
    setBusy(true);
    setError(null);
    try {
      const result = mode === "revoke"
        ? await revokeCertificateAction(eventId, certificate.id, reason)
        : await reissueCertificateAction(eventId, certificate.id, { recipientName: name, reason });
      if (!result.ok) { setError(result.error); return; }
      setMode("idle");
      setReason("");
      onChanged?.();
    } catch {
      setError("That could not be applied. The certificate is unchanged — try again.");
    } finally {
      setBusy(false);
    }
  }

  async function showHistory() {
    const result = await getLineageAction(certificate.id);
    if (result.ok) setLineage(result.lineage); else setError(result.error);
  }

  if (mode === "revoke") {
    return (
      <Form
        title="Withdraw this certificate"
        // The consequence stated before the button, not after it.
        note="This is permanent. Anyone who checks this certificate — including anyone the holder already
              sent it to — will be told it was withdrawn, and will see the reason you type here."
        confirmLabel="Withdraw it"
        busy={busy}
        error={error}
        onCancel={() => { setMode("idle"); setError(null); }}
        onConfirm={submit}
      >
        <label className="block">
          <span className="text-sm font-medium text-text">Why?</span>
          <textarea
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            maxLength={500}
            rows={3}
            placeholder="Issued to the wrong person"
            className="mt-1 w-full rounded border-border-strong text-sm"
          />
        </label>
      </Form>
    );
  }

  if (mode === "reissue") {
    return (
      <Form
        title="Correct this certificate"
        note="The original stays exactly as issued and is marked as replaced — the holder's existing link
              keeps working and points at the corrected one. Anything you don't change carries over."
        confirmLabel="Issue the corrected certificate"
        busy={busy}
        error={error}
        onCancel={() => { setMode("idle"); setError(null); }}
        onConfirm={submit}
      >
        <label className="block">
          <span className="text-sm font-medium text-text">Name as it should read</span>
          <input
            value={name}
            onChange={(e) => setName(e.target.value)}
            maxLength={200}
            className="mt-1 w-full rounded border-border-strong text-sm"
          />
        </label>
        <label className="block">
          <span className="text-sm font-medium text-text">What was wrong?</span>
          <textarea
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            maxLength={500}
            rows={2}
            placeholder="Name was misspelled"
            className="mt-1 w-full rounded border-border-strong text-sm"
          />
        </label>
      </Form>
    );
  }

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap gap-2">
        {live && (
          <>
            <button
              type="button"
              onClick={() => setMode("reissue")}
              className="min-h-11 rounded-md border border-border-strong px-3 py-1.5 text-sm lg:min-h-0"
            >
              Correct
            </button>
            <button
              type="button"
              onClick={() => setMode("revoke")}
              className="min-h-11 rounded-md border border-danger/40 px-3 py-1.5 text-sm text-danger lg:min-h-0"
            >
              Withdraw
            </button>
          </>
        )}
        <button type="button" onClick={() => void showHistory()} className="min-h-11 text-sm underline lg:min-h-0">
          History
        </button>
      </div>

      {!live && (
        <p className="text-sm text-muted">
          {certificate.status === "revoked"
            ? "This certificate was withdrawn."
            : "This certificate was replaced by a corrected one."}
        </p>
      )}

      {error && <p role="alert" className="text-sm text-danger">{error}</p>}

      {lineage && (
        <ol className="space-y-2 border-l border-border pl-4 text-sm">
          {lineage.map((link) => (
            <li key={link.id}>
              <span className="break-all font-mono text-xs">{link.certificate_id}</span>{" "}
              <span className="text-muted">
                — {link.recipient_name}, {statusWords(link.status)}
              </span>
              {link.revocation_reason && (
                <div className="text-xs text-muted">{link.revocation_reason}</div>
              )}
            </li>
          ))}
        </ol>
      )}
    </div>
  );
}

/** Bulk withdrawal, for the case a whole run went out wrong. */
export function BatchWithdrawal({ eventId, batchId, issuedCount, onChanged }: {
  eventId: string;
  batchId: string;
  issuedCount: number;
  onChanged?: () => void;
}) {
  const [open, setOpen] = useState(false);
  const [reason, setReason] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [done, setDone] = useState<{ revoked: number; skipped: number } | null>(null);

  if (done) {
    return (
      <p className="text-sm text-text">
        {done.revoked.toLocaleString()} certificates withdrawn
        {done.skipped > 0 && ` (${done.skipped.toLocaleString()} were already not live)`}.
      </p>
    );
  }

  if (!open) {
    return (
      <button type="button" onClick={() => setOpen(true)} className="min-h-11 text-left text-sm text-danger underline lg:min-h-0">
        Withdraw all {issuedCount.toLocaleString()} certificates from this run
      </button>
    );
  }

  return (
    <Form
      title={`Withdraw all ${issuedCount.toLocaleString()} certificates`}
      note="This is permanent and applies to every certificate this run generated. Anyone who checks one —
            including recipients who already have theirs — will be told it was withdrawn, with the reason
            you type here."
      confirmLabel={`Withdraw all ${issuedCount.toLocaleString()}`}
      busy={busy}
      error={error}
      onCancel={() => { setOpen(false); setError(null); }}
      onConfirm={async () => {
        setBusy(true);
        setError(null);
        const result = await revokeBatchAction(eventId, batchId, reason);
        setBusy(false);
        if (result.ok) { setDone(result.result); onChanged?.(); } else setError(result.error);
      }}
    >
      <label className="block">
        <span className="text-sm font-medium text-text">Why?</span>
        <textarea
          value={reason}
          onChange={(e) => setReason(e.target.value)}
          maxLength={500}
          rows={3}
          placeholder="The wrong event date was printed on all of them"
          className="mt-1 w-full rounded border-border-strong text-sm"
        />
      </label>
    </Form>
  );
}

function Form({ title, note, confirmLabel, busy, error, children, onCancel, onConfirm }: {
  title: string;
  note: string;
  confirmLabel: string;
  busy: boolean;
  error: string | null;
  children: React.ReactNode;
  onCancel: () => void;
  onConfirm: () => void;
}) {
  return (
    <div className="space-y-3 rounded-md border border-border p-4">
      <h4 className="text-sm font-semibold text-text">{title}</h4>
      <p className="text-sm text-muted">{note}</p>
      {children}
      {error && <p role="alert" className="text-sm text-danger">{error}</p>}
      <div className="flex gap-2">
        <button
          type="button"
          disabled={busy}
          onClick={onConfirm}
          className="min-h-11 rounded-md bg-accent px-3 py-1.5 text-sm font-semibold text-on-accent disabled:opacity-50 lg:min-h-0"
        >
          {busy ? "Working…" : confirmLabel}
        </button>
        <button
          type="button"
          disabled={busy}
          onClick={onCancel}
          className="min-h-11 rounded-md border border-border-strong px-3 py-1.5 text-sm lg:min-h-0"
        >
          Cancel
        </button>
      </div>
    </div>
  );
}

function statusWords(status: string) {
  if (status === "issued") return "current";
  if (status === "revoked") return "withdrawn";
  if (status === "superseded") return "replaced";
  return status;
}
