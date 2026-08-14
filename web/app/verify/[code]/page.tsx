import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { Download, ShieldCheck, ShieldX } from "lucide-react";
import { Card, LinkButton } from "@kurx/ui";
import { section, verifyCertificate } from "@/lib/api";
import { createMetadata, jsonLd } from "@/lib/site";

export async function generateMetadata({ params }: { params: { code: string } }): Promise<Metadata> {
  return createMetadata({ title: `Verify ${params.code}`, path: `/verify/${params.code}` });
}

export default async function VerifyPage({ params }: { params: { code: string } }) {
  /*
   * `.catch(() => null) → notFound()` is worse here than anywhere else it appeared. This page exists
   * to answer "is this credential real", and it answered "no such certificate" for a 500, a timeout
   * or a dropped connection — telling somebody checking a credential that it was fabricated, on the
   * strength of an outage (D-235). 404 means absent; everything else reaches the error boundary.
   */
  const result = await section(verifyCertificate(params.code));
  if (result.state === "hidden") notFound();
  if (result.state === "unavailable") {
    throw new Error(`Could not verify the certificate ${params.code}.`);
  }
  const cert = result.data;

  const isValid = cert.status === "issued";

  return (
    <main className="container-shell py-10">
      <script type="application/ld+json" dangerouslySetInnerHTML={jsonLd("EducationalOccupationalCredential", { name: cert.event_title, identifier: params.code })} />
      <Card className="mx-auto max-w-3xl">
        {/* The verdict was a coloured shield and nothing else — the one thing this page exists to
            say was carried by hue alone. It is a sentence now, and the icon is decorative. */}
        {isValid ? (
          <ShieldCheck size={40} aria-hidden className="text-success" />
        ) : (
          <ShieldX size={40} aria-hidden className="text-danger" />
        )}
        <h1 className="mt-4 text-3xl font-semibold text-text">Certificate verification</h1>
        <p className={`mt-2 text-body font-semibold ${isValid ? "text-success" : "text-danger"}`}>
          {isValid
            ? "This certificate is valid."
            : `This certificate is not valid — it is ${cert.status}.`}
        </p>
        <dl className="mt-6 grid gap-4 sm:grid-cols-2">
          {[
            ["Event", cert.event_title],
            ["Issued to", cert.issued_to],
            ["Organizer", cert.organizer],
            ["Issued at", new Date(cert.issued_at).toLocaleDateString("en-IN", { dateStyle: "medium" })],
            ["Verification status", isValid ? "Valid" : cert.status],
            ["Code", cert.verify_code],
          ].map(([label, value]) => (
            <div key={label} className="rounded-md border border-border p-3">
              <dt className="text-sm text-muted">{label}</dt>
              <dd className="mt-1 font-semibold text-text">{value}</dd>
            </div>
          ))}
        </dl>
        {/* A 64px lucide `QrCode` glyph sat here — a picture of a QR, not one, beside the deep link
            it was decorating. The third instance of the REG-005 placeholder pattern; removed on the
            same grounds. */}
        <div className="mt-6 flex flex-wrap items-center gap-3">
          <LinkButton href={`kurx://verify/${params.code}`} variant="secondary">Open in app</LinkButton>
          {cert.pdf_url && <LinkButton href={cert.pdf_url}><Download size={16} aria-hidden /> Download</LinkButton>}
        </div>
      </Card>
    </main>
  );
}
