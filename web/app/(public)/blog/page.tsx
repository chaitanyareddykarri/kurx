import { Card } from "@kurx/ui";

import { InfoPage } from "@/components/marketing/info-page";
import { createMetadata } from "@/lib/site";

export const metadata = createMetadata({ title: "Blog", path: "/blog" });

export default function BlogPage() {
  return (
    <InfoPage
      title="Blog"
      schemaType="Blog"
      intro="Articles, release notes, and organizer playbooks. Nothing is published yet — this page says so rather than showing an empty shell."
    >
      <Card>
        <h2 className="text-h3">Kurx product notes</h2>
        <p className="mt-2 text-body text-muted">
          The first posts will cover event setup, ticket configuration, and payout operations.
        </p>
      </Card>
    </InfoPage>
  );
}
