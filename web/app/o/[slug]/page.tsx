import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { Badge, Card, LinkButton, Stat } from "@kurx/ui";
import { FollowButton } from "@/components/orgs/follow-button";
import { getPublicOrg, listFollowing, section } from "@/lib/api";
import { currentSession } from "@/lib/session";
import { createMetadata, jsonLd } from "@/lib/site";

/** Tier is a stored integer; anything outside the three named plans is stated plainly rather than
 *  guessed at, so an unrecognised value never becomes a plan name the org does not hold. */
function tierLabelOf(tier: number): string {
  return tier === 1 ? "Starter" : tier === 2 ? "Pro" : tier === 3 ? "Enterprise" : `Tier ${tier}`;
}

export async function generateMetadata({ params }: { params: { slug: string } }): Promise<Metadata> {
  const org = await getPublicOrg(params.slug).catch(() => null);
  const title = org ? `${org.name}` : `${params.slug} organization`;
  return createMetadata({ title, path: `/o/${params.slug}` });
}

export default async function OrganizationPage({ params }: { params: { slug: string } }) {
  // `.catch(() => null) → notFound()` is the bug D-235 fixed on `/u/[username]` and this page still
  // carried: it turned every failure into "this organization does not exist". A 500, a timeout or a
  // dropped connection told the visitor a real organization was gone. 404 is the only status that
  // means absent — and by D-018 it also covers "hidden from you", which must stay indistinguishable.
  // Anything else is rethrown to the error boundary, which reports our fault instead of denying the
  // organization exists.
  const orgResult = await section(getPublicOrg(params.slug));
  if (orgResult.state === "hidden") notFound();
  if (orgResult.state === "unavailable") {
    throw new Error(`Could not load the organization ${params.slug}.`);
  }
  const org = orgResult.data;
  const tierLabel = tierLabelOf(org.tier);

  const session = await currentSession();
  const initialFollowing = session
    ? await listFollowing(session.accessToken).then((f) => f.some((o) => o.org_id === org.id)).catch(() => false)
    : false;

  return (
    <main className="container-shell py-10">
      <script type="application/ld+json" dangerouslySetInnerHTML={jsonLd("Organization", { name: org.name, description: org.bio ?? undefined })} />

      <div className="rounded-lg border border-border bg-surface p-6">
        <div className="flex flex-wrap items-start justify-between gap-4">
          {/* Was a hand-rolled pill in `text-accent` — 2.80:1 on light, below AA. `Badge` is the
              system's pill and solves its foreground against all three surface steps. */}
          <Badge tone="accent">{tierLabel} plan</Badge>
          {session
            ? <FollowButton orgId={org.id} orgName={org.name} initialFollowing={initialFollowing} />
            : <LinkButton href="/?login=required#login" variant="secondary">Sign in to follow</LinkButton>}
        </div>
        <h1 className="mt-4 text-3xl font-semibold text-text sm:text-4xl">{org.name}</h1>
        {org.bio && <p className="mt-3 max-w-3xl text-muted">{org.bio}</p>}
      </div>

      <div className="mt-6 grid gap-4 sm:grid-cols-3">
        <Stat label="Events hosted" value={String(org.events_count)} />
        <Stat label="Members" value={String(org.members_count)} />
        <Stat label="Plan" value={tierLabel} />
      </div>

      {/*
        These two cards restated the numbers directly above them and led nowhere.

        A working link is not available to build: the API does support `orgId` on event search, but
        the only surface that renders it (`/discover`) sits inside the `(app)` group behind
        `requireSession()` and does not read the parameter — so linking there would send an anonymous
        visitor to a login wall to see public events. Wiring a public events-by-organization view is
        product scope, not redesign scope, and is logged as REG-006 rather than invented here.

        Restating a count twice is not content, so the pair collapses to the one thing this page can
        honestly say.
      */}
      <Card className="mt-6">
        <h2 className="font-semibold text-text">About this organization</h2>
        <p className="mt-2 text-sm text-muted">
          {org.events_count === 0
            ? "No public events have been hosted by this organization yet."
            : `${org.events_count} public event${org.events_count === 1 ? "" : "s"} hosted by ${org.members_count} member${org.members_count === 1 ? "" : "s"}. Individual events link back here from their own pages.`}
        </p>
      </Card>
    </main>
  );
}
