import Link from "next/link";
import { requireEventOrg } from "@/lib/event-org";
import { can } from "@/lib/capabilities";
import { section } from "@/lib/api";
import {
  getCertificateDashboard, listEventTemplates, listLibraryTemplates
} from "@/lib/certificate-api";
import { CertificateTemplateList } from "@/components/host/certificates/certificate-template-list";
import { CertificateDashboardPanel } from "@/components/host/certificates/certificate-dashboard";
import { Card } from "@kurx/ui";

/**
 * Event Dashboard → Certificates (D-344).
 *
 * `requireEventOrg` is the same gate every other host page uses; the server refuses independently, so
 * this only decides what to render.
 */
export default async function EventCertificatesPage({ params }: { params: { id: string } }) {
  const { session, caps } = await requireEventOrg(params.id);
  // The server gates on EventPermission.ManageContent, which resolves to Manager level. `events.update`
  // is the same Manager-level gate the host nav already uses, so what reveals this page and what the
  // page itself allows agree — anything revealed in the nav but refused on arrival reads as broken.
  const canManage = can(caps, "events", "update");

  // Through `section` so an API outage renders "couldn't load" rather than an empty list: an empty list
  // is a specific, actionable claim ("you have no designs") and must not be what a failed request looks
  // like (D-235).
  const [templates, dashboard, library] = await Promise.all([
    section(listEventTemplates(session.accessToken, params.id)),
    section(getCertificateDashboard(session.accessToken, params.id)),
    // The creator's own saved designs (D-344, Phase 13). Through `section` like the rest, so a failure to
    // read the library degrades to "no reuse offered" rather than taking the page down.
    section(listLibraryTemplates(session.accessToken)),
  ]);

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <h1 className="text-h2 text-text">Certificates</h1>
          <p className="mt-1 max-w-2xl text-sm text-muted">
            Upload the certificate you already designed. Kurx adds the participant and event details on top,
            then issues a verifiable certificate to each recipient.
          </p>
        </div>
        {canManage && (
          <Link
            href={`/host/events/${params.id}/certificates/generate`}
            className="rounded-md bg-slate-900 px-4 py-2 text-sm font-semibold text-white"
          >
            Generate certificates
          </Link>
        )}
      </div>

      {/* Only once something has been issued — an empty dashboard of zeroes on a fresh event is noise
          in front of the thing the organiser actually came to do. */}
      {dashboard.state === "ok" && dashboard.data.live + dashboard.data.revoked > 0 && (
        <CertificateDashboardPanel eventId={params.id} data={dashboard.data} />
      )}

      {templates.state !== "ok" ? (
        <Card>
          <p role="status" className="text-sm text-muted">
            This event&apos;s certificate designs couldn&apos;t be loaded. That is a failure to read, not
            an empty list — nothing has been deleted. Refresh to try again.
          </p>
        </Card>
      ) : (
        <CertificateTemplateList
          eventId={params.id}
          templates={templates.data}
          canManage={canManage}
          library={library.state === "ok" ? library.data : []}
        />
      )}
    </div>
  );
}
