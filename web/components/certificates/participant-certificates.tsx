import type { ParticipantCertificates } from "@/lib/certificate-api";

/**
 * A participant's own certificates (D-355, Phase 10).
 *
 * Shared by the capability-link page and the signed-in list, because they show the same thing to the same
 * person — one just happens to have proved who they are and the other is holding a secret URL.
 *
 * A certificate that is no longer live is still listed. Hiding it would leave the holder unable to find
 * out what happened to a document they may have already sent to an employer — which is exactly when they
 * most need to know. What is withheld is the download, not the fact.
 */
export function ParticipantCertificateList({ data }: { data: ParticipantCertificates }) {
  if (data.certificates.length === 0) {
    return (
      <p className="rounded-lg border border-dashed border-border-strong p-8 text-center text-sm text-muted">
        No certificates here yet. If you were expecting one, the organiser of your event is the person to
        ask.
      </p>
    );
  }

  return (
    <ul className="space-y-4">
      {data.certificates.map((certificate) => (
        <li key={certificate.certificate_id} className="rounded-lg border border-border p-4">
          <div className="flex flex-wrap items-start justify-between gap-3">
            <div>
              <h3 className="font-semibold text-text">{certificate.event_title}</h3>
              <p className="mt-0.5 text-sm text-muted">
                Issued {new Date(certificate.issued_at).toLocaleDateString()} ·{" "}
                <span className="font-mono text-xs">{certificate.certificate_id}</span>
              </p>
            </div>
            <StatusBadge status={certificate.status} />
          </div>

          {certificate.status === "revoked" && (
            <p role="alert" className="mt-3 rounded-md bg-danger/10 p-3 text-sm text-danger">
              This certificate was withdrawn by the organiser
              {certificate.revocation_reason ? `: ${certificate.revocation_reason}` : "."}
            </p>
          )}

          {certificate.status === "superseded" && (
            <p className="mt-3 rounded-md bg-warning/10 p-3 text-sm text-warning">
              This one was replaced by a corrected certificate
              {certificate.replaced_by && (
                <> — <span className="font-mono text-xs">{certificate.replaced_by}</span>, which is in this
                list</>
              )}
              .
            </p>
          )}

          <div className="mt-3 flex flex-wrap gap-3 text-sm">
            {/* Only a live certificate offers a download. Handing back a fresh copy of a document the
                platform publicly calls invalid would be arming a misunderstanding. */}
            {certificate.download_pdf_url && (
              <a
                href={certificate.download_pdf_url}
                className="rounded-md bg-accent px-3 py-1.5 font-semibold text-on-accent"
              >
                Download PDF
              </a>
            )}
            {certificate.download_png_url && (
              <a href={certificate.download_png_url} className="rounded-md border border-border-strong px-3 py-1.5">
                Download image
              </a>
            )}
            <a href={certificate.verification_url} className="self-center underline">
              Verification page
            </a>
          </div>
        </li>
      ))}
    </ul>
  );
}

function StatusBadge({ status }: { status: string }) {
  const [label, tone] =
    status === "issued" ? ["Valid", "bg-success/15 text-success"]
    : status === "revoked" ? ["Withdrawn", "bg-danger/15 text-danger"]
    : ["Replaced", "bg-warning/15 text-warning"];

  return <span className={`rounded-full px-2.5 py-1 text-xs font-semibold ${tone}`}>{label}</span>;
}
