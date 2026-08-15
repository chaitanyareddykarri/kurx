import { notFound } from "next/navigation";
import { resolveCertificateAccess } from "@/lib/certificate-api";
import { ParticipantCertificateList } from "@/components/certificates/participant-certificates";

/**
 * A participant's certificates, reached by capability link (D-355, Phase 10).
 *
 * Public and unauthenticated by design: the token in the URL is the credential. That is what lets someone
 * who was on a spreadsheet and never had a Kurx account collect what was issued to them — requiring
 * signup to receive a certificate would exclude most of the people certificates are issued to.
 */

// Never cached or statically rendered: the page is keyed on a secret and contains presigned download
// URLs that expire. A cached copy would be both stale and a leak.
export const dynamic = "force-dynamic";
export const revalidate = 0;

export const metadata = {
  title: "Your certificates",
  // Keep it out of search results. The URL is a credential; an indexed copy would publish it.
  robots: { index: false, follow: false },
};

export default async function CertificateAccessPage({ params }: { params: { token: string } }) {
  let data;
  try {
    data = await resolveCertificateAccess(params.token);
  } catch {
    // A revoked link, a mistyped one and one that never existed all land here identically — telling them
    // apart would confirm to someone guessing that they had found a real one.
    notFound();
  }

  return (
    <main className="mx-auto max-w-2xl px-4 py-12">
      <h1 className="text-2xl font-semibold text-slate-900">
        {data.recipient_name ? `Certificates for ${data.recipient_name}` : "Your certificates"}
      </h1>
      <p className="mt-1 text-sm text-slate-600">
        This link is yours. Anyone who has it can see and download these, so treat it like the certificate
        itself.
      </p>

      <div className="mt-8">
        <ParticipantCertificateList data={data} />
      </div>
    </main>
  );
}
