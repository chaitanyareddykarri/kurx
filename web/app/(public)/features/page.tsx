import { CheckCircle2 } from "lucide-react";
import { Card } from "@kurx/ui";

import { InfoPage } from "@/components/marketing/info-page";
import { createMetadata } from "@/lib/site";

export const metadata = createMetadata({ title: "Features", path: "/features" });

const features = [
  "Discover events",
  "Dynamic ticket booking",
  "Group registration",
  "Certificates",
  "Host dashboards",
  "Template editors",
  "SignalR live sales",
  "Razorpay checkout",
  "PWA install",
  "Offline My Tickets"
];

export default function FeaturesPage() {
  return (
    <InfoPage
      title="Features"
      schemaType="ItemList"
      intro="What Kurx does today, across the web app, the installable PWA, and the mobile app."
    >
      {/* A real list, so a screen reader announces how many features there are
          rather than reading ten unrelated headings. */}
      <ul className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
        {features.map((feature) => (
          <li key={feature}>
            <Card>
              <CheckCircle2 size={18} aria-hidden="true" className="text-success" />
              <h2 className="mt-3 text-h3">{feature}</h2>
            </Card>
          </li>
        ))}
      </ul>
    </InfoPage>
  );
}
