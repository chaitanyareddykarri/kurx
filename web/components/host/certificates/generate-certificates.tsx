"use client";

import { useState } from "react";
import type { CertificateTemplate } from "@/lib/certificate-api";
import type { MappableField } from "@/lib/certificate-mapping";
import { BatchRunner } from "./batch-runner";

/**
 * Picks the design, then runs the flow (D-355, Phase 7).
 *
 * Only designs with artwork are offered. A design with no background cannot render a certificate, and
 * discovering that after uploading a participant list and confirming a mapping wastes the two steps that
 * take the longest.
 */
export function GenerateCertificates({ eventId, templates }: {
  eventId: string;
  templates: CertificateTemplate[];
}) {
  const usable = templates.filter((t) => t.background_url !== null && t.status !== "archived");
  const [templateId, setTemplateId] = useState(usable[0]?.id ?? "");
  const template = usable.find((t) => t.id === templateId);

  if (usable.length === 0) {
    return (
      <p className="rounded-lg border border-dashed border-slate-300 p-8 text-center text-sm text-slate-600">
        None of your designs have artwork on them yet. Upload a certificate image to a design first.
      </p>
    );
  }

  return (
    <div className="space-y-6">
      <label className="block max-w-md">
        <span className="text-sm font-medium text-slate-900">Which design?</span>
        <select
          value={templateId}
          onChange={(e) => setTemplateId(e.target.value)}
          className="mt-1 w-full rounded border-slate-300 text-sm"
        >
          {usable.map((t) => (
            <option key={t.id} value={t.id}>{t.name}</option>
          ))}
        </select>
      </label>

      {template && (
        <BatchRunner
          // Remounts when the design changes: a mapping confirmed against one design's placeholders is
          // not a mapping for another's.
          key={template.id}
          eventId={eventId}
          template={template}
          fields={mappableFields(template)}
        />
      )}
    </div>
  );
}

/** The design's own placeholders, as mapping targets. Taken from the template rather than a fixed list,
 *  because the field vocabulary is open — a creator may place anything they want on their certificate. */
function mappableFields(template: CertificateTemplate): MappableField[] {
  return template.fields
    .filter((f) => f.kind === "dynamicfield" && f.field_key)
    .map((f) => ({
      key: f.field_key!,
      label: f.label ?? f.field_key!,
      required: f.is_required,
    }));
}
