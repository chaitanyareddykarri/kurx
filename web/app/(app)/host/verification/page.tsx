import { Card, LinkButton } from "@kurx/ui";
import { requireSession } from "@/lib/session";
import { getMyIdentity, getMyOrgCapabilities, section, listMyRepresentations, apiErrorMessage } from "@/lib/api";
import { formatDate } from "@/lib/formatters";

// A person's identity verification is "good enough" for the paid path once it is Approved.
function identityIsApproved(status: string) {
  return status.toLowerCase() === "approved";
}

function maskedOrNone(last4: string | null) {
  return last4 ? `•••• ${last4}` : "Not provided";
}

function StatusRow({ label, ok, okText, noText }: { label: string; ok: boolean; okText: string; noText: string }) {
  return (
    <div className="flex items-center justify-between gap-4 border-b border-border py-3 last:border-0">
      <span className="text-sm text-text">{label}</span>
      <span className={`text-sm font-semibold ${ok ? "text-success" : "text-muted"}`}>{ok ? okText : noText}</span>
    </div>
  );
}

export default async function VerificationPage() {
  const session = await requireSession();

  try {
    const identity = await getMyIdentity(session.accessToken);
    // Verification standing for EVERY organization the caller represents, not for one "current" org
    // (D-267). Readiness is a per-representation fact, so showing a single row was always a half-answer
    // for anyone who represents two institutions.
    // An outage told a host they represent no organization — which on this page is a statement about
  // their standing, not about the network (D-235).
  const representationsResult = await section(listMyRepresentations(session.accessToken));
  const representations = representationsResult.state === "ok" ? representationsResult.data : [];
  const representationsFailed = representationsResult.state !== "ok";
    const standings = await Promise.all(
      representations.map(async (r) => ({
        org: r,
        caps: await getMyOrgCapabilities(session.accessToken, r.organization_id).catch(() => null)
      }))
    );
    const anyOrgVerified = standings.some((s) => s.caps?.is_org_verified);
    const idApproved = identityIsApproved(identity.status);

    return (
      <div className="space-y-6">
        <div>
          <p className="text-sm font-semibold text-accent-text">Your account</p>
          <h1 className="mt-2 text-3xl font-semibold">Verification &amp; Readiness</h1>
          <p className="mt-1 text-sm text-muted">
            Running paid events requires your identity verified and the organization you represent verified. Each event&apos;s payment-readiness is re-checked live at publish and checkout.
          </p>
        </div>

        {/* Personal identity verification (M3) */}
        <section className="space-y-3">
          <h2 className="text-lg font-semibold">Your identity</h2>
          <Card>
            <div className="flex items-center justify-between gap-4">
              <div>
                <p className="text-sm text-muted">Status</p>
                <strong className={`text-xl ${idApproved ? "text-success" : "text-text"}`}>{identity.status}</strong>
              </div>
              <div className="text-right">
                <p className="text-sm text-muted">Highest level</p>
                <strong className="text-xl text-text">{identity.level}</strong>
              </div>
            </div>
            <div className="mt-4 space-y-0">
              <StatusRow label="Government ID" ok={!!identity.govt_id_last4} okText={`${identity.govt_id_kind ?? "ID"} · ${maskedOrNone(identity.govt_id_last4)}`} noText="Not provided" />
              <StatusRow label="PAN" ok={!!identity.pan_last4} okText={maskedOrNone(identity.pan_last4)} noText="Not provided" />
              <StatusRow label="Bank account" ok={!!identity.bank_last4} okText={maskedOrNone(identity.bank_last4)} noText="Not provided" />
            </div>
            {identity.reviewed_at ? (
              <p className="mt-4 text-xs text-muted">Last reviewed {formatDate(identity.reviewed_at, "en-IN", { day: "numeric", month: "short", year: "numeric" })}.</p>
            ) : null}
            {!idApproved ? (
              <p className="mt-3 text-sm text-muted">Complete identity verification to unlock paid events and payouts. Only masked last-4 values are ever stored.</p>
            ) : null}
          </Card>
        </section>

        {/* Organization verification & your standing (M5/M6/M7) */}
        <section className="space-y-3">
          <h2 className="text-lg font-semibold">Organizations you represent</h2>
          {standings.length === 0 ? (
            <Card>
              <p className="text-sm text-muted">
                You don&apos;t represent an organization. Personal events need none — represent one only to run
                events under an institution&apos;s name.
              </p>
              <LinkButton href="/host/representing" className="mt-4">Represent an organization</LinkButton>
            </Card>
          ) : (
            standings.map(({ org, caps }) =>
              caps ? (
                <Card key={org.organization_id}>
                  <h3 className="font-semibold">{org.name}</h3>
                  <div className="mt-2">
                    <StatusRow label="Organization verified" ok={caps.is_org_verified} okText="Verified" noText="Not verified" />
                    <StatusRow label="You can represent this organization" ok={caps.can_represent_org} okText="Yes" noText="No" />
                    <StatusRow label="Verified representative" ok={caps.is_org_verified_rep} okText="Yes" noText="No" />
                  </div>
                  {!caps.is_org_verified ? (
                    <p className="mt-3 text-sm text-muted">An admin must verify this organization (registry + evidence) to unlock paid events under its name.</p>
                  ) : null}
                </Card>
              ) : null
            )
          )}
        </section>

        {/* Overall paid-event readiness — derived from the live flags above, never a stored value. */}
        {/*
          The verdict is only stated when the facts behind it actually arrived. `anyOrgVerified` is
          derived from the representations list, and that list previously fell back to `[]` on
          failure — so an outage produced a confident "Not ready yet", which is a claim about the
          host's standing rather than about the network.
        */}
        <Card className={!representationsFailed && idApproved && anyOrgVerified ? "border-success/60" : ""}>
          <p className="text-sm font-semibold text-accent-text">Paid-event readiness</p>
          <p className="mt-2 text-sm text-text">
            {representationsFailed
              ? "Your organizations couldn't be loaded, so this can't be worked out right now. Try refreshing — nothing about your verification has changed."
              : idApproved && anyOrgVerified
                ? "Your identity and the organization you represent are verified. Individual events still pass a live payment-readiness check at publish/checkout."
                : "Not ready yet — verify both your identity and the organization you represent above. Free events are unaffected."}
          </p>
        </Card>
      </div>
    );
  } catch (err) {
    return (
      <div className="space-y-4">
        <p className="text-sm font-semibold text-accent-text">Your account</p>
        <h1 className="text-3xl font-semibold">Verification &amp; Readiness</h1>
        <Card>
          <p className="text-sm text-muted">Couldn&apos;t load your verification status: <span className="text-text">{apiErrorMessage(err)}</span></p>
        </Card>
      </div>
    );
  }
}
