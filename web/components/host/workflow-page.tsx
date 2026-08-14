import { Construction } from "lucide-react";
import { Card, LinkButton } from "@kurx/ui";

/**
 * The stand-in for a host area that has not been built.
 *
 * What this rendered before was a grid of cards, one per planned feature, each captioned
 * *"Connected to backend APIs as they are exposed; frontend keeps validation and UI state only."*
 * That is a note between engineers, shipped to hosts, on four routes — and the cards themselves read
 * as modules that exist, because a card in a grid is what every working area of this product uses.
 *
 * It now says the one true thing: the area is not available. The planned scope is kept, because a
 * host who lands here deserves to know what is coming, but as plain text under a heading that calls
 * it planned rather than as tiles that imply it is here.
 *
 * The pages themselves are **not** deleted here. As of Phase 21 nothing in the product links to any
 * of the four — `/workspace` was their last inbound link and Phase 20 removed it — so they are
 * reachable only by typing the URL. That makes them Phase 49's to remove, with the orphan check this
 * note records.
 */
export function WorkflowPage({ title, items }: { title: string; items: string[] }) {
  return (
    <div className="mx-auto max-w-2xl space-y-6">
      <div>
        <p className="text-sm font-semibold text-accent-text">Host workflow</p>
        <h1 className="mt-2 text-3xl font-semibold text-text">{title}</h1>
      </div>

      <Card className="space-y-4">
        <div className="flex items-start gap-3">
          <Construction size={20} aria-hidden className="mt-0.5 shrink-0 text-muted" />
          <div>
            <h2 className="font-semibold text-text">This area isn&apos;t available yet</h2>
            <p className="mt-2 text-sm text-muted">
              There is nothing to configure here today. Nothing you have set up elsewhere depends on
              it, and your events are unaffected.
            </p>
          </div>
        </div>

        {items.length > 0 ? (
          <div>
            <h3 className="text-sm font-semibold text-text">Planned to cover</h3>
            <ul className="mt-2 list-inside list-disc text-sm text-muted">
              {items.map((item) => (
                <li key={item}>{item}</li>
              ))}
            </ul>
          </div>
        ) : null}

        <LinkButton href="/workspace" variant="secondary">Back to your workspace</LinkButton>
      </Card>
    </div>
  );
}
