"use client";

import { useEffect, useState } from "react";
import { getDeliveriesAction, sendBatchAction } from "@/lib/certificate-actions";
import type { CertificateBatch, DeliverySummary } from "@/lib/certificate-api";

/**
 * Emailing a run's certificates (D-355, Phase 8).
 *
 * Two things this screen must never do. It must not say "delivered" — the platform knows only that an
 * email provider accepted the message, and there is no bounce pipeline that could tell it more, so every
 * word here says *sent*. And it must not quietly drop the people it cannot reach: a run where 4 of 400
 * recipients have no email address says so on the face of it, because that is the organiser's problem to
 * solve and they can only solve it if they are told.
 *
 * Sending queues; the emails leave in the background. The button therefore reports what was queued, and
 * this polls until the queue drains.
 */

const POLL_MS = 4000;

export function BatchSender({ eventId, batch }: { eventId: string; batch: CertificateBatch }) {
  const [summary, setSummary] = useState<DeliverySummary | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const draining = summary !== null && summary.pending > 0;

  useEffect(() => {
    let cancelled = false;
    void (async () => {
      const result = await getDeliveriesAction(batch.id);
      if (!cancelled && result.ok) setSummary(result.summary);
    })();
    return () => { cancelled = true; };
  }, [batch.id]);

  useEffect(() => {
    if (!draining) return;
    const timer = setInterval(async () => {
      const result = await getDeliveriesAction(batch.id);
      if (result.ok) setSummary(result.summary);
    }, POLL_MS);
    return () => clearInterval(timer);
  }, [batch.id, draining]);

  async function send() {
    setBusy(true);
    setError(null);
    try {
      const result = await sendBatchAction(eventId, batch.id);
      if (result.ok) setSummary(result.summary); else setError(result.error);
    } catch {
      setError("Sending could not be queued. Nothing has been sent — try again.");
    } finally {
      setBusy(false);
    }
  }

  if (batch.issued_count === 0) {
    return (
      <p className="text-sm text-slate-600">
        Nothing to send yet — this run hasn&apos;t generated its certificates.
      </p>
    );
  }

  const nothingSentYet = summary === null || (summary.sent === 0 && summary.pending === 0
    && summary.failed === 0);

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center gap-3">
        <button
          type="button"
          disabled={busy || draining}
          onClick={() => void send()}
          className="rounded-md bg-slate-900 px-4 py-2 text-sm font-semibold text-white disabled:opacity-50"
        >
          {busy ? "Queueing…" : nothingSentYet ? "Email these certificates" : "Send any not yet sent"}
        </button>
        {draining && (
          <span className="text-sm text-slate-600">
            Sending {summary!.pending.toLocaleString()} more…
          </span>
        )}
      </div>

      {error && <p role="alert" className="text-sm text-red-700">{error}</p>}

      {summary && !nothingSentYet && (
        <dl className="grid grid-cols-2 gap-3 sm:grid-cols-4">
          <Stat label="Certificates" value={summary.total} />
          {/* Deliberately "Sent", never "Delivered". The provider accepted it; nobody knows more. */}
          <Stat label="Sent" value={summary.sent} />
          <Stat label="Queued" value={summary.pending} />
          <Stat label="Failed" value={summary.failed} tone={summary.failed > 0 ? "bad" : undefined} />
        </dl>
      )}

      {summary && summary.sent > 0 && (
        <p className="text-xs text-slate-500">
          &ldquo;Sent&rdquo; means the email provider accepted the message. It isn&apos;t confirmation that
          it reached anyone&apos;s inbox.
        </p>
      )}

      {/* Said on the face of it, not buried in a report. */}
      {summary && summary.no_destination > 0 && (
        <p role="alert" className="rounded-md bg-amber-50 p-3 text-sm text-amber-900">
          {summary.no_destination.toLocaleString()} of {summary.total.toLocaleString()} recipients have no
          email address, so they cannot be sent anything. Their certificates exist and can be shared with a
          link.
        </p>
      )}

      {summary && summary.failed > 0 && (
        <div role="alert" className="rounded-md bg-red-50 p-3 text-sm text-red-800">
          <p>{summary.failed.toLocaleString()} could not be sent.</p>
          <ul className="mt-2 space-y-1">
            {summary.recent
              .filter((d) => d.status === "failed")
              .slice(0, 5)
              .map((d) => (
                <li key={d.id}>
                  {d.destination ?? "no address"} — {d.error ?? "unknown error"}
                </li>
              ))}
          </ul>
        </div>
      )}
    </div>
  );
}

function Stat({ label, value, tone }: { label: string; value: number; tone?: "bad" }) {
  return (
    <div className="rounded-md border border-slate-200 p-3">
      <dt className="text-xs text-slate-500">{label}</dt>
      <dd className={`text-lg font-semibold ${tone === "bad" ? "text-red-700" : "text-slate-900"}`}>
        {value.toLocaleString()}
      </dd>
    </div>
  );
}
