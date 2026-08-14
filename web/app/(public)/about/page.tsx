import { InfoPage } from "@/components/marketing/info-page";
import { createMetadata } from "@/lib/site";

export const metadata = createMetadata({ title: "About", path: "/about" });

export default function AboutPage() {
  return (
    <InfoPage
      title="About Kurx"
      schemaType="Organization"
      intro="Kurx is an event infrastructure platform for discovery, booking, certificate verification, and organizer operations across web, PWA, and mobile."
    />
  );
}
