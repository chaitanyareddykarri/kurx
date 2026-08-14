import type { Metadata } from "next";
import { requireSession } from "@/lib/session";
import { listMyAllies, listIncomingAllyRequests, listOutgoingAllyRequests } from "@/lib/api";
import { createMetadata } from "@/lib/site";
import { AlliesManager } from "@/components/profile/allies-manager";
import { SuggestionsSection } from "@/components/profile/suggestions-section";
import { PeopleSearch } from "@/components/profile/people-search";

export const metadata: Metadata = createMetadata({ title: "Allies", path: "/allies" });

/**
 * Moved from `web/app/allies/` into the `(app)` group. The route is unchanged — group folders do not
 * appear in the URL — but the page now renders inside `AppShell` instead of directly under the root
 * layout.
 *
 * It called `requireSession()` while sitting outside every shell, so a signed-in user who reached
 * their allies had no navigation, no skip link and no way back into the product except the browser's
 * back button. It was the only auth-gated route on web outside `(app)`.
 *
 * Its own `<main>` goes with the move: `AppShell` provides the landmark and the skip target, and
 * keeping both would nest two — the regression this program introduced in Phase 11 and did not catch
 * for two phases.
 */
export default async function AlliesPage() {
  const session = await requireSession();
  const [mine, incoming, outgoing] = await Promise.all([
    listMyAllies(session.accessToken),
    listIncomingAllyRequests(session.accessToken),
    listOutgoingAllyRequests(session.accessToken),
  ]);

  return (
    <>
      <h1 className="text-2xl font-semibold text-text">Allies</h1>
      <p className="mt-1 text-sm text-muted">
        Mutual professional connections from real, verified event activity — not followers.
      </p>

      <section className="mt-6">
        <h2 className="mb-3 font-semibold text-text">Find people</h2>
        <PeopleSearch />
      </section>

      <div className="mt-8">
        <AlliesManager initialMine={mine} initialIncoming={incoming} initialOutgoing={outgoing} />
      </div>

      <div className="mt-8">
        <SuggestionsSection />
      </div>
    </>
  );
}
