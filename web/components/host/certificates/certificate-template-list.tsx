"use client";

import Link from "next/link";
import { useState, useTransition } from "react";
import { useRouter } from "next/navigation";
import { Button, Card } from "@kurx/ui";
import { archiveTemplateAction, createTemplateAction } from "@/lib/certificate-actions";
import { CertificateCanvas } from "@/components/host/certificates/certificate-canvas";
import { toDraft } from "@/lib/certificate-editor";
import type { CertificateTemplate } from "@/lib/certificate-api";
import { UseSavedDesign } from "@/components/host/certificates/template-reuse";

/**
 * An event's certificate designs (D-344, Phase 3).
 *
 * Creating a design and uploading artwork are two steps rather than one, because the upload needs a
 * template to be keyed under — the storage prefix is what authorises the object, so there has to be
 * something to authorise it against before any bytes move.
 */
export function CertificateTemplateList({ eventId, templates, canManage, library = [] }: {
  eventId: string;
  templates: CertificateTemplate[];
  canManage: boolean;
  /** The creator's own saved designs (D-344, Phase 13). Empty when they have none, which is when the
   *  reuse control does not render at all. */
  library?: CertificateTemplate[];
}) {
  const router = useRouter();
  const [pending, start] = useTransition();
  const [error, setError] = useState<string | null>(null);
  const [name, setName] = useState("");
  const [creating, setCreating] = useState(false);

  function create() {
    setError(null);
    start(async () => {
      const result = await createTemplateAction(eventId, name.trim() || "Certificate design");
      if ("error" in result && result.error) { setError(result.error); return; }
      if ("template" in result && result.template) {
        // Straight into the editor: the next thing to do is always upload the artwork.
        router.push(`/host/events/${eventId}/certificates/${result.template.id}`);
      }
    });
  }

  function archive(templateId: string) {
    setError(null);
    start(async () => {
      const result = await archiveTemplateAction(templateId, eventId);
      if ("error" in result && result.error) setError(result.error);
      else router.refresh();
    });
  }

  return (
    <div className="space-y-4">
      {error ? <p role="alert" className="text-sm text-danger">{error}</p> : null}

      {/* Offered beside creating a new one, because "start from one I've used before" is the same
          decision as "start from scratch" and belongs in the same place. */}
      {canManage ? <UseSavedDesign eventId={eventId} library={library} /> : null}

      {canManage ? (
        <div className="flex flex-wrap items-end gap-2">
          {creating ? (
            <>
              <label className="text-xs text-muted">
                Design name
                <input
                  value={name}
                  onChange={(e) => setName(e.target.value)}
                  placeholder="Participation certificate"
                  aria-label="Design name"
                  className="mt-1 block h-10 w-64 rounded-md border border-border bg-background px-3 text-sm text-text"
                />
              </label>
              <Button type="button" disabled={pending} onClick={create}>
                {pending ? "Creating…" : "Create and upload artwork"}
              </Button>
              <Button type="button" variant="secondary" disabled={pending} onClick={() => setCreating(false)}>
                Cancel
              </Button>
            </>
          ) : (
            <>
              <Button type="button" onClick={() => setCreating(true)}>New certificate design</Button>
              <p className="text-xs text-muted">
                Upload the certificate you already designed — Kurx adds the participant details on top.
              </p>
            </>
          )}
        </div>
      ) : (
        <p className="text-sm text-muted">
          You can view this event&apos;s certificate designs. Changing them needs Manager access.
        </p>
      )}

      {templates.length === 0 ? (
        <Card>
          <div className="py-6 text-center">
            <h3 className="font-semibold text-text">No certificate designs yet</h3>
            <p className="mx-auto mt-2 max-w-md text-sm text-muted">
              Upload the certificate artwork you already have. Kurx places the participant name, event
              details and a verification QR on top of it — your design is never redrawn.
            </p>
          </div>
        </Card>
      ) : (
        <ul className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
          {templates.map((t) => (
            <li key={t.id}>
              <Card>
                <CertificateCanvas
                  pageSize={t.page_size}
                  backgroundUrl={t.background_url}
                  fields={t.fields.map(toDraft)}
                  width={280}
                />
                <div className="mt-3 flex items-start justify-between gap-2">
                  <div className="min-w-0">
                    <h3 className="truncate font-semibold text-text">{t.name}</h3>
                    <p className="mt-0.5 text-xs text-muted">
                      v{t.version} · {t.status} · {t.fields.length} detail{t.fields.length === 1 ? "" : "s"}
                      {t.has_issued_certificates ? " · issued" : ""}
                    </p>
                  </div>
                </div>

                {canManage ? (
                  <div className="mt-3 flex flex-wrap gap-2">
                    <Link
                      href={`/host/events/${eventId}/certificates/${t.id}`}
                      className="inline-flex h-9 items-center rounded-md border border-border-strong bg-surface px-3 text-caption font-semibold text-text hover:bg-elevated"
                    >
                      Edit
                    </Link>
                    <Button type="button" variant="ghost" size="sm" disabled={pending}
                      onClick={() => archive(t.id)}>
                      Archive
                    </Button>
                  </div>
                ) : null}
              </Card>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
