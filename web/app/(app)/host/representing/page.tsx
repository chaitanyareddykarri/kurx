import { Button, Card, LinkButton, controlClass } from "@kurx/ui";
import { requireSession } from "@/lib/session";
import { searchOrganizations, getMyMembershipClaims, listMyRepresentations, apiErrorMessage } from "@/lib/api";
import { ClaimOrgForm } from "@/components/host/claim-org-form";

function normalize(raw: string | string[] | undefined) {
  return (Array.isArray(raw) ? raw[0] : raw ?? "").trim();
}

// Representation workspace (user-first model). A user REPRESENTS an organization after admin verification;
// organizations have no account and are never "managed" here — only Kurx admins manage org records.
export default async function RepresentingPage({
  searchParams
}: {
  searchParams: { [key: string]: string | string[] | undefined };
}) {
  const session = await requireSession();
  const q = normalize(searchParams.q);

  try {
    // Every organization the caller represents, not one "current" org held in a cookie (D-267). There
    // is no active-organization concept any more — representation is a list, and each event picks from
    // it at creation time. The server excludes self-representation: it is not an organization (D-268).
    const [claims, representations] = await Promise.all([
      getMyMembershipClaims(session.accessToken),
      listMyRepresentations(session.accessToken)
    ]);
    const results = q.length >= 2 ? await searchOrganizations(session.accessToken, q, 10) : [];

    return (
      <div className="space-y-6">
        <div>
          <p className="text-sm font-semibold text-accent-text">Your account</p>
          <h1 className="mt-2 text-3xl font-semibold">Representing</h1>
          <p className="mt-1 text-sm text-muted">
            Organizations you represent, and your requests to represent new ones. Only Kurx admins manage
            organization records — you represent them, you don&apos;t own them.
          </p>
        </div>

        {representations.length > 0 ? (
          <section className="space-y-3">
            <h2 className="text-lg font-semibold">Organizations you represent</h2>
            {representations.map((r) => (
              <Card key={r.organization_id}>
                <div className="flex flex-wrap items-start justify-between gap-3">
                  <div>
                    <h3 className="font-semibold">{r.name}</h3>
                    <p className="mt-1 text-xs text-muted">Your authority: {r.authority}</p>
                  </div>
                </div>
                {/* Deliberately not a launcher. This states the caller's authority; it is not a hub
                    with tiles into an organization's own sections (D-267). Events live in Workspace. */}
                <p className="mt-3 text-xs text-muted">
                  Pick this organization on the Representing step when you create an event.
                </p>
              </Card>
            ))}
          </section>
        ) : null}

        <section className="space-y-3">
          <h2 className="text-lg font-semibold text-text">Represent an organization</h2>
          <form className="flex flex-col gap-2 sm:flex-row" action="/host/representing" method="get">
            <input
              name="q"
              defaultValue={q}
              placeholder="Search by name, alias, or domain…"
              aria-label="Search organizations"
              className={controlClass}
            />
            <Button type="submit" className="shrink-0">Search</Button>
          </form>

          {/* A server-rendered search replaces the region below on navigation with nothing said
              about it; the count is announced so the search is perceivable as having run. */}
          <p role="status" className="sr-only">
            {q.length >= 2
              ? `${results.length} ${results.length === 1 ? "organization matches" : "organizations match"} ${q}.`
              : ""}
          </p>

          {q.length >= 2 ? (
            results.length === 0 ? (
              <Card>
                <p className="text-sm text-muted">No organizations match &ldquo;{q}&rdquo;.</p>
                <LinkButton href="/host/representing/new" variant="secondary" className="mt-4">Request to represent a new organization</LinkButton>
              </Card>
            ) : (
              <div className="grid gap-3 md:grid-cols-2">
                {results.map((r) => (
                  <Card key={r.id}>
                    <div className="flex items-start justify-between gap-3">
                      <div>
                        <h3 className="font-semibold">{r.name}</h3>
                        <p className="mt-1 text-xs text-muted">
                          {r.type}{r.primary_domain ? ` · ${r.primary_domain}` : ""} · matched on {r.match}
                        </p>
                      </div>
                      <span className={`shrink-0 text-xs font-semibold ${r.verification_status === "verified" ? "text-success" : "text-muted"}`}>
                        {r.verification_status}
                      </span>
                    </div>
                    <ClaimOrgForm orgId={r.id} />
                  </Card>
                ))}
              </div>
            )
          ) : (
            <Card className="flex flex-wrap items-center justify-between gap-3">
              <p className="text-sm text-muted">Type at least two characters to search the organization registry.</p>
              <LinkButton href="/host/representing/new" variant="secondary">Request a new one</LinkButton>
            </Card>
          )}
        </section>

        <section className="space-y-3">
          <h2 className="text-lg font-semibold">Your representation requests</h2>
          {claims.length === 0 ? (
            <Card><p className="text-sm text-muted">No representation requests yet. Search above and request to represent an organization.</p></Card>
          ) : (
            <div className="space-y-3">
              {claims.map((c) => (
                <Card key={c.id}>
                  <div className="flex items-center justify-between gap-4">
                    <div>
                      <h3 className="font-semibold">{c.org_name}</h3>
                      <p className="mt-1 text-xs text-muted">
                        Requested authority: {c.claimed_role}{c.fast_track ? " · fast-track (verified email domain)" : ""}
                      </p>
                    </div>
                    <span className={`shrink-0 text-sm font-semibold ${c.status === "approved" ? "text-success" : c.status === "rejected" ? "text-danger" : "text-muted"}`}>
                      {c.status}
                    </span>
                  </div>
                </Card>
              ))}
            </div>
          )}
        </section>
      </div>
    );
  } catch (err) {
    return (
      <div className="space-y-4">
        <p className="text-sm font-semibold text-accent-text">Your account</p>
        <h1 className="text-3xl font-semibold">Representing</h1>
        <Card><p className="text-sm text-muted">Couldn&apos;t load your representations: <span className="text-text">{apiErrorMessage(err)}</span></p></Card>
      </div>
    );
  }
}
