import { Card, LinkButton } from "@kurx/ui";

import { InfoPage } from "@/components/marketing/info-page";
import { createMetadata, jsonLd } from "@/lib/site";

export const metadata = createMetadata({ title: "Pricing", path: "/pricing" });

const plans = [
  ["Free", "Start listing events and issuing simple tickets."],
  ["Growth", "Advanced forms, analytics, team roles, and exports."],
  ["Pro", "Payout operations, templates, risk flags, and premium support."]
];

export default function PricingPage() {
  return (
    <InfoPage title="Pricing" intro="Three plans. Every one of them includes discovery, booking, and certificates.">
      <script
        type="application/ld+json"
        dangerouslySetInnerHTML={jsonLd("Product", {
          name: "Kurx",
          offers: plans.map(([name]) => ({ "@type": "Offer", name }))
        })}
      />
      <ul className="grid gap-4 md:grid-cols-3">
        {plans.map(([name, description]) => (
          <li key={name}>
            <Card className="flex h-full flex-col">
              <h2 className="text-h3">{name}</h2>
              <p className="mt-3 flex-1 text-body text-muted">{description}</p>
              {/* The accessible name says which plan — three identical "Talk to
                  sales" buttons are indistinguishable in a links list. */}
              <LinkButton href="/contact" className="mt-lg" aria-label={`Talk to sales about the ${name} plan`}>
                Talk to sales
              </LinkButton>
            </Card>
          </li>
        ))}
      </ul>
    </InfoPage>
  );
}
