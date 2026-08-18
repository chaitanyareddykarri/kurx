import Link from "next/link";
import { notFound } from "next/navigation";
import { requireEventOrg } from "@/lib/event-org";
import { can } from "@/lib/capabilities";
import { section } from "@/lib/api";
import { listEventTemplates } from "@/lib/certificate-api";
import { GenerateCertificates } from "@/components/host/certificates/generate-certificates";
import { Card } from "@kurx/ui";

/**
 * Event Dashboard → Certificates → Generate (D-355, Phase 7).
 *
 * A page of its own rather than a panel on the designs list: generating is a multi-step flow with an
 * approval in the middle of it, and burying that inside a list is how someone approves a run they meant
 * to be reviewing.
 */
export default async function GenerateCertificatesPage({ params }: { params: { id: string } }) {
  const { session, caps } = await requireEventOrg(params.id);
  if (!can(caps, "events", "update")) notFound();

  const templates = await section(listEventTemplates(session.accessToken, params.id));

  return (
    <div className="space-y-6">
      <div>
        <Link href={`/host/events/${params.id}/certificates`} className="inline-flex min-h-11 items-center text-sm underline lg:min-h-0">
          ← Certificate designs
        </Link>
        <h1 className="mt-2 text-h2 text-text">Generate certificates</h1>
        <p className="mt-1 max-w-2xl text-sm text-muted">
          Upload your participant list, check what each column means, review real samples, then generate.
        </p>
      </div>

      {templates.state !== "ok" ? (
        <Card>
          <p role="status" className="text-sm text-muted">
            Your certificate designs couldn&apos;t be loaded. That is a failure to read, not an empty
            list. Refresh to try again.
          </p>
        </Card>
      ) : (
        <GenerateCertificates eventId={params.id} templates={templates.data} />
      )}
    </div>
  );
}
