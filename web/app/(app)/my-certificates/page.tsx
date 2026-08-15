import { requireSession } from "@/lib/session";
import { section } from "@/lib/api";
import { listMyCertificates } from "@/lib/certificate-api";
import { ParticipantCertificateList } from "@/components/certificates/participant-certificates";
import { Card } from "@kurx/ui";

/**
 * The signed-in user's own certificates (D-344, Phase 10).
 *
 * The server claims any recipient rows matching this account's *verified* email as part of answering, so
 * a certificate issued before someone signed up appears the first time they look rather than after some
 * later backfill they have no way to ask for.
 */
export const dynamic = "force-dynamic";

export default async function MyCertificatesPage() {
  const session = await requireSession();

  // Through `section` so an API outage renders "couldn't load" rather than an empty list — an empty list
  // is a specific claim ("you have no certificates") and must not be what a failed request looks like
  // (D-235).
  const certificates = await section(listMyCertificates(session.accessToken));

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-h2 text-text">My certificates</h1>
        <p className="mt-1 max-w-2xl text-sm text-muted">
          Certificates issued to you. Ones issued to an email address show up here once that address is
          verified on your account.
        </p>
      </div>

      {certificates.state !== "ok" ? (
        <Card>
          <p role="status" className="text-sm text-muted">
            Your certificates couldn&apos;t be loaded. That is a failure to read, not an empty list —
            nothing has been lost. Refresh to try again.
          </p>
        </Card>
      ) : (
        <ParticipantCertificateList data={certificates.data} />
      )}
    </div>
  );
}
