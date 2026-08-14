import { InfoPage } from "@/components/marketing/info-page";
import { createMetadata } from "@/lib/site";

export const metadata = createMetadata({ title: "Privacy", path: "/privacy" });

/**
 * The body is a single summarising paragraph, unchanged.
 *
 * A ticketing product taking payments in India needs a real privacy policy, but
 * legal copy is not something a redesign should invent — the wording carries
 * obligations. Flagged in the Deferred ledger rather than expanded here.
 */
export default function PrivacyPage() {
  return (
    <InfoPage
      title="Privacy Policy"
      schemaType="PrivacyPolicy"
      intro="Kurx respects profile privacy, ticket privacy, certificate visibility settings, and organizer access controls while processing data needed to operate events."
    />
  );
}
