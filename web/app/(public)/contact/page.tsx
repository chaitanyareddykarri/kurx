import { Mail } from "lucide-react";

import { InfoPage } from "@/components/marketing/info-page";
import { createMetadata } from "@/lib/site";

export const metadata = createMetadata({ title: "Contact", path: "/contact" });

export default function ContactPage() {
  return (
    <InfoPage
      title="Contact"
      schemaType="ContactPage"
      intro="For partnerships, support, event operations, or enterprise plans, reach the Kurx team."
    >
      {/* `accent-text`, not `accent`: the ember fill is 2.80:1 on the light
          background. The link also now meets the 44px touch floor. */}
      <a
        className="inline-flex min-h-11 items-center gap-sm rounded-md text-body-lg text-accent-text hover:underline"
        href="mailto:hello@kurx.app"
      >
        <Mail size={18} aria-hidden="true" /> hello@kurx.app
      </a>
    </InfoPage>
  );
}
