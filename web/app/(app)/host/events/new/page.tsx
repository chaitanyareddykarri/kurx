import { Card } from "@kurx/ui";
import { requireSession } from "@/lib/session";
import {
  getMe, listMyRepresentations, listCategories, listSubcategories, listFieldPresets, getMyIdentity,
  getRepresentativeRoles, getArchetypeCapabilities, section
} from "@/lib/api";
import { archetypeSupportsTeams } from "@/lib/event-wizard";
import { CreateEventGate } from "@/components/host/create-event-gate";

/// Create Event is entered from **Profile** (D-305) and opens the **gate**, not the form: eligibility is
/// checked against the caller's existing verification state, then Public or Private is chosen, and only
/// then are event details typed. It previously opened the eleven-step form directly and asked every
/// eligibility question at publish — after eleven steps of work.
///
/// It still needs no organization (D-267): Representation is a step inside the form, and Personal is
/// always available.
export default async function CreateEventPage() {
  const session = await requireSession();

  // The list contains only institutions the caller may represent — Personal is not in it, because
  // representing yourself is not an organization (D-268). The wizard offers it as its own choice.
  //
  // Identity is fetched here so the gate can show what is ALREADY verified and never re-ask for it
  // (D-305). It degrades to "nothing verified" rather than failing the page: a user who cannot read
  // their identity status can still create a free event, which requires none of it.
  // `representativeRoles` is the closed vocabulary the server validates against (D-266 M5). Fetched
  // here so the Authorization step can offer it (D-351); a hardcoded copy is how a role gets offered
  // and then refused by the API. An empty list degrades to a free-text-free step rather than failing
  // the page — the step itself is only reached by an event that represents an institution.
  const [me, representations, categories, subcategories, presets, identity, representativeRoles] = await Promise.all([
    getMe(session.accessToken),
    section(listMyRepresentations(session.accessToken)),
    listCategories(),
    listSubcategories(),
    listFieldPresets(),
    getMyIdentity(session.accessToken).catch(() => null),
    getRepresentativeRoles(session.accessToken).catch(() => [] as string[])
  ]);

  /*
   * `.catch(() => [])` here silently removed every organization from the wizard's first step, so a
   * host whose representations failed to load was offered "Personal" alone — and would have created
   * the event under their own name without ever knowing the other choice existed. That is a wrong
   * event, not a degraded one (D-235).
   */
  const representationsFailed = representations.state !== "ok";

  /*
   * D-357 — which of the archetypes on offer permit team entry.
   *
   * Asked of the capability engine, once per DISTINCT archetype among the Types this caller could
   * choose, and resolved here rather than in the wizard: the wizard is a client component, and a
   * `@/lib/api` import there drags React's server-only `cache()` into the browser bundle and the test
   * environment. One server round-trip per archetype beats a client fetch on every Type click.
   *
   * Fails CLOSED — an archetype whose capability set cannot be read is simply absent from the list, so
   * the Registration step offers individual entry only rather than a team option the server may refuse.
   */
  const archetypeSlugs = [...new Set(subcategories.map((s) => s.archetype_slug).filter((a): a is string => !!a))];
  const teamCapableArchetypes = (await Promise.all(
    archetypeSlugs.map(async (slug) =>
      archetypeSupportsTeams(await getArchetypeCapabilities(slug).catch(() => null)) ? slug : null)
  )).filter((s): s is string => s !== null);

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-3xl font-semibold text-text">Create event</h1>
      </div>
      {representationsFailed ? (
        <p role="status" className="rounded-lg border border-dashed border-border bg-surface p-4 text-sm text-muted">
          The organizations you represent couldn&apos;t be loaded, so only Personal is offered below.
          If you meant to host this on behalf of an organization, refresh before you continue.
        </p>
      ) : null}
      <Card>
        {/* Unwrapped here, not inside the gate: a failed read falls back to Personal-only and the
            banner above says so, rather than the gate having to know about SectionResult. */}
        <CreateEventGate
          representations={representations.state === "ok" ? representations.data : []}
          canHostPaid={me.can_organize_paid}
          categories={categories}
          subcategories={subcategories}
          presets={presets}
          identityVerified={identity?.govt_id_status === "Approved" || identity?.pan_status === "Approved"}
          panVerified={identity?.pan_status === "Approved"}
          bankVerified={identity?.bank_status === "Approved"}
          pennyDropPassed={identity?.penny_drop_status === "Passed"}
          bankNameMatched={identity?.bank_name_match === "Match"}
          // D-307 — the server's answer, never re-derived here. `?? false` is the closed position: an
          // identity read that failed must not read as permission.
          canCreatePublicEvent={me.trust?.can_create_public_event ?? false}
          canCreatePrivateEvent={me.trust?.can_create_private_event ?? true}
          // D-353/D-352 — the server states whether a Public event needs an organization at all.
          // Under the dev bypass it does not, and the gate must not add a rule the server has lifted.
          requiresRepresentation={me.requires_representation}
          representativeRoles={representativeRoles}
          teamCapableArchetypes={teamCapableArchetypes}
        />
      </Card>
    </div>
  );
}
