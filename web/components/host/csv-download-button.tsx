"use client";

import { useState, useTransition } from "react";
import { Button } from "@kurx/ui";

type ExportResult = { ok: true; csv: string } | { ok: false; error: string };

/// Downloads a CSV produced by an authenticated server action (which fetches it with the session token
/// and returns the text), turning it into a client-side Blob download — no unauthenticated file link.
export function CsvDownloadButton({
  action,
  filename,
  label = "Export CSV"
}: {
  action: () => Promise<ExportResult>;
  filename: string;
  label?: string;
}) {
  const [pending, start] = useTransition();
  const [error, setError] = useState<string | null>(null);

  function download() {
    setError(null);
    start(async () => {
      const r = await action();
      if (!r.ok) {
        setError(r.error);
        return;
      }
      const blob = new Blob([r.csv], { type: "text/csv;charset=utf-8" });
      const url = URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = filename;
      a.click();
      URL.revokeObjectURL(url);
    });
  }

  return (
    <div className="flex flex-col items-end gap-1">
      <Button variant="secondary" disabled={pending} onClick={download}>{pending ? "Exporting…" : label}</Button>
      {error ? <span className="text-xs text-danger">{error}</span> : null}
    </div>
  );
}
