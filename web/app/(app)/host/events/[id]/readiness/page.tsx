import { requireEventOrg } from "@/lib/event-org";
import {
  getEventCapabilities, getPolicyRequirements, getEventAuthorization, getRepresentativeRoles
} from "@/lib/api";
import { Card } from "@kurx/ui";
import { ModulePicker } from "@/components/host/module-picker";
import { AuthorizationForm } from "@/components/host/authorization-form";

/**
 * D-266 M8 — everything that decides whether this event can go live, on one page.
 *
 * **Nothing here is decided by the client.** The modules come from the Capability Engine, the blockers
 * from the Policy Engine, and the authorization state from the event's own record. This page renders
 * three server answers; it contains no rule about what a Hackathon is or when a letter is required.
 *
 * It exists because M5's publish gate was otherwise unactionable: the workspace would refuse to publish
 * for `event_authorization_required` and offer the organiser nowhere to provide one.
 */
export default async function EventReadinessPage({ params }: { params: { id: string } }) {
  const { session, orgId, event } = await requireEventOrg(params.id);

  // Each is independently non-fatal: a page that renders two of three panels is more useful than one
  // that 500s because a single engine was briefly unavailable.
  const [capabilities, policy, authorization, roles] = await Promise.all([
    getEventCapabilities(session.accessToken, orgId, event.id).catch(() => []),
    getPolicyRequirements(session.accessToken, orgId, event.id).catch(() => null),
    getEventAuthorization(session.accessToken, event.id).catch(() => null),
    // The vocabulary the server validates against. An empty list on failure is honest: the form then
    // offers nothing rather than a stale guess the API would reject.
    getRepresentativeRoles(session.accessToken).catch(() => [] as string[]),
  ]);

  // Shown when the policy engine says this event needs one, OR when one has already been filed — never
  // inferred from the organisation being non-personal, which would be the client re-deriving a server rule.
  //
  // The second clause is load-bearing. An APPROVED authorization is no longer a publish blocker, so keying
  // only on the blocker made the whole panel disappear the moment it was approved: the organiser could not
  // see what they had submitted, that it had been approved, or the reviewer's notes — and if a reviewer
  // later requested changes the panel reappeared, flickering in and out with the verdict. `??` did not
  // catch this because it fires only on null, and `includes()` returns false.
  const needsAuthorization =
    (policy?.publish_blockers.includes("event_authorization_required") ?? false) || authorization !== null;

  return (
    <div className="space-y-6">
      {/*
        A failed policy read used to remove the whole panel silently. On a page called Readiness, an
        absent "Before you publish" section is read as "nothing is blocking" — the most consequential
        wrong conclusion this screen can produce. It says which it is now.
      */}
      {policy === null ? (
        <Card>
          <h2 className="mb-1 text-lg font-semibold text-text">Before you publish</h2>
          <p role="status" className="text-sm text-muted">
            The publish checks couldn&apos;t be loaded, so this is not a statement that nothing is
            blocking your event. Try refreshing before you publish.
          </p>
        </Card>
      ) : null}
      {policy ? (
        <Card>
          <h2 className="mb-1 text-lg font-semibold">Before you publish</h2>
          {policy.publish_blockers.length === 0 ? (
            <p className="text-sm text-success">Nothing is blocking this event from going live.</p>
          ) : (
            <>
              <p className="mb-3 text-sm text-muted">
                These have to be resolved first. They are the same checks a reviewer works through.
              </p>
              <ul className="space-y-1.5">
                {policy.publish_blockers.map((b) => (
                  <li key={b} className="flex items-start gap-2 text-sm text-text">
                    <span aria-hidden className="mt-1.5 h-1.5 w-1.5 shrink-0 rounded-full bg-danger" />
                    {BLOCKER_COPY[b] ?? b}
                  </li>
                ))}
              </ul>
            </>
          )}
        </Card>
      ) : null}

      {needsAuthorization ? <AuthorizationForm eventId={event.id} existing={authorization} eventStatus={event.status} roles={roles} /> : null}

      <Card>
        <h2 className="mb-1 text-lg font-semibold">Modules</h2>
        <p className="mb-4 text-sm text-muted">
          What this kind of event supports. Some are always on, and some aren&apos;t available here at all.
        </p>
        <ModulePicker capabilities={capabilities} />
      </Card>
    </div>
  );
}

/// Presentation only — the blocker codes are the server's, and an unmapped one renders verbatim rather
/// than being swallowed by a generic message.
const BLOCKER_COPY: Record<string, string> = {
  event_authorization_required:
    "The organization this event represents has to authorize it in writing, and that authorization has to be approved.",
  representation_required:
    "This type of event has to be run on behalf of an organization — choose who you're representing.",
  financial_review_required:
    "A fundraising event needs its money path cleared by our finance team before it can go live.",
  private_product_cannot_be_listed: "A private event can't be listed in discovery.",
  private_product_cannot_take_payment: "A private event can't take payment.",
  registration_policy_not_allowed_for_type:
    "The registration policy you've chosen isn't allowed for this event type.",
};
