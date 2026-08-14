import { Card, LinkButton } from "@kurx/ui";

import { InfoPage } from "@/components/marketing/info-page";
import { createMetadata } from "@/lib/site";

export const metadata = createMetadata({ title: "Support", path: "/support" });

const topics = [
  ["Attendee tickets", "Finding a booking, transferring a ticket, or requesting a refund."],
  ["Organizer setup", "Creating an event, configuring tickets, and inviting a team."],
  ["Payments and payouts", "Checkout problems, settlement timing, and payout verification."]
];

export default function SupportPage() {
  return (
    <InfoPage
      title="Support"
      schemaType="FAQPage"
      intro="Pick the area closest to your question, or write to the team directly."
    >
      <ul className="grid gap-4 md:grid-cols-3">
        {topics.map(([title, description]) => (
          <li key={title}>
            <Card className="h-full">
              <h2 className="text-h3">{title}</h2>
              {/* Each card said only "Get help from the Kurx support team" — the
                  same sentence three times, so the three cards were
                  indistinguishable and none of them led anywhere. */}
              <p className="mt-2 text-body text-muted">{description}</p>
            </Card>
          </li>
        ))}
      </ul>
      <LinkButton href="/contact" variant="secondary" className="mt-lg">
        Contact the team
      </LinkButton>
    </InfoPage>
  );
}
