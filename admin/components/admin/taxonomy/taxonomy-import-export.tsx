"use client";

import { useState, type ChangeEvent } from "react";
import { Button, Dialog, useToast } from "@kurx/ui";
import { exportTaxonomyAction, previewImportTaxonomyAction, applyImportTaxonomyAction } from "@/lib/admin-actions";
import type { ImportPreview, TaxonomyExportNode } from "@/lib/api";

/** D-188 refinement round 2 #1 — merge-only, Preview before Apply, never a destructive "replace." */
export function TaxonomyImportExport({ onClose }: { onClose: () => void }) {
  const toast = useToast();
  const [preview, setPreview] = useState<ImportPreview | null>(null);
  const [nodes, setNodes] = useState<TaxonomyExportNode[] | null>(null);
  const [busy, setBusy] = useState(false);

  async function handleExport() {
    const r = await exportTaxonomyAction();
    if (r && "error" in r) {
      toast(String(r.error), "error");
      return;
    }
    if (!r || !("data" in r)) return;
    const blob = new Blob([JSON.stringify(r.data, null, 2)], { type: "application/json" });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = `kurx-taxonomy-${new Date().toISOString().slice(0, 10)}.json`;
    a.click();
    URL.revokeObjectURL(url);
  }

  async function handleFile(e: ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    if (!file) return;
    setBusy(true);
    try {
      const text = await file.text();
      const parsed = JSON.parse(text) as { nodes: TaxonomyExportNode[] };
      const result = await previewImportTaxonomyAction(parsed.nodes);
      if (result && "error" in result) {
        toast(String(result.error), "error");
        return;
      }
      if (result && "preview" in result) {
        setPreview(result.preview);
        setNodes(parsed.nodes);
      }
    } catch {
      toast("Couldn't read that file as taxonomy JSON.", "error");
    } finally {
      setBusy(false);
      e.target.value = "";
    }
  }

  async function handleApply() {
    if (!nodes) return;
    setBusy(true);
    const r = await applyImportTaxonomyAction(nodes);
    setBusy(false);
    if (r && "error" in r) {
      toast(String(r.error), "error");
      return;
    }
    if (r && "result" in r) {
      toast(`Imported: ${r.result.created} created, ${r.result.updated} updated, ${r.result.skipped} skipped.`, "success");
      onClose();
    }
  }

  return (
    <Dialog open onClose={onClose} title="Export / Import taxonomy">
      <div className="space-y-4">
        <div>
          <p className="mb-2 text-sm text-muted">Export the full taxonomy as JSON — a backup, or a seed for another environment.</p>
          <Button variant="secondary" onClick={handleExport}>
            Export JSON
          </Button>
        </div>

        <div className="border-t border-border pt-4">
          <p className="mb-2 text-sm text-muted">
            Import is merge-only — it never deletes or overwrites anything. Nodes are matched by slug: new
            slugs are created, existing ones get their metadata updated, and anything that would need
            overwriting a different level or parent is skipped, not silently changed.
          </p>
          <input type="file" accept="application/json" onChange={handleFile} disabled={busy} className="text-sm text-text" />
        </div>

        {preview ? (
          <div className="space-y-3 rounded-lg border border-border p-3">
            <p className="text-sm text-text">
              {preview.to_create.length} to create · {preview.to_update.length} to update · {preview.conflicts.length} skipped
            </p>
            {preview.errors.length > 0 ? (
              <div className="rounded-md border border-danger/30 bg-danger/5 p-2">
                <p className="text-xs font-semibold text-danger">Errors — import cannot run until these are fixed:</p>
                <ul className="mt-1 list-disc pl-4 text-xs text-danger">
                  {preview.errors.map((e, i) => (
                    <li key={i}>{e}</li>
                  ))}
                </ul>
              </div>
            ) : null}
            {preview.conflicts.length > 0 ? (
              <div className="rounded-md border border-border bg-elevated p-2">
                <p className="text-xs font-semibold text-text">Conflicts — these will be skipped:</p>
                <ul className="mt-1 list-disc pl-4 text-xs text-muted">
                  {preview.conflicts.map((c, i) => (
                    <li key={i}>{c}</li>
                  ))}
                </ul>
              </div>
            ) : null}
            <div className="flex justify-end gap-2">
              <Button
                variant="ghost"
                onClick={() => {
                  setPreview(null);
                  setNodes(null);
                }}
              >
                Discard
              </Button>
              <Button onClick={handleApply} disabled={busy || preview.errors.length > 0}>
                {busy ? "Applying…" : "Apply merge"}
              </Button>
            </div>
          </div>
        ) : null}
      </div>
    </Dialog>
  );
}
