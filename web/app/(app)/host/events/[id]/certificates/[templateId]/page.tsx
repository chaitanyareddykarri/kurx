import Link from "next/link";
import { notFound } from "next/navigation";
import { requireEventOrg } from "@/lib/event-org";
import { can } from "@/lib/capabilities";
import { getTemplate, listPageSizes } from "@/lib/certificate-api";
import { CertificateTemplateEditor } from "@/components/host/certificates/certificate-template-editor";
import { SaveToLibrary } from "@/components/host/certificates/template-reuse";

/** The certificate editor (D-355, Phase 3). A template belonging to another event answers 404 on the
 *  server; this only turns that into Next's not-found rather than an error page. */
export default async function CertificateTemplatePage(
  { params }: { params: { id: string; templateId: string } }
) {
  const { session, caps } = await requireEventOrg(params.id);
  const canManage = can(caps, "events", "update");

  const template = await getTemplate(session.accessToken, params.templateId).catch(() => null);
  // The page-size catalogue (D-361). Fetched here rather than kept in the client, so the dimensions of
  // A4 are written down once — on the server that renders with them. An empty list on failure leaves the
  // picker offering Custom only: degraded, but the editor still opens.
  const pageSizes = await listPageSizes(session.accessToken).then((c) => c.presets).catch(() => []);
  // Belt and braces: the API already scopes by event, and this refuses a template id pasted from a
  // different event's URL even if that ever stopped being true.
  if (!template || template.event_id !== params.id) notFound();

  return (
    <div className="space-y-4">
      <Link href={`/host/events/${params.id}/certificates`} className="text-sm text-accent-text hover:underline">
        ← Back to Certificates
      </Link>
      <CertificateTemplateEditor template={template} canManage={canManage} pageSizes={pageSizes} />

      {/* Below the editor, not inside its toolbar: keeping a design for next time is a thing you do when
          you have finished, not while you are placing fields. */}
      {canManage ? (
        <div className="border-t border-border pt-4">
          <SaveToLibrary eventId={params.id} template={template} />
        </div>
      ) : null}
    </div>
  );
}
