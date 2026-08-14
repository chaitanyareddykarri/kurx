import { InfoPage } from "@/components/marketing/info-page";
import { createMetadata } from "@/lib/site";

export const metadata = createMetadata({ title: "Terms", path: "/terms" });

/** Single summarising paragraph, unchanged — see the note in privacy/page.tsx. */
export default function TermsPage() {
  return (
    <InfoPage
      title="Terms of Service"
      schemaType="WebPage"
      intro="These terms describe acceptable use, organizer responsibilities, attendee purchase expectations, payment handling, refunds, and account safety for Kurx."
    />
  );
}
