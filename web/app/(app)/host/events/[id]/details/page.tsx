import { ShieldCheck } from "lucide-react";
import { requireEventOrg } from "@/lib/event-org";
import {
  listCategories, listSubcategories, listOrgVenues, getEventAuthorization, getRepresentativeRoles,
  listEventChangeRequests
} from "@/lib/api";
import { Card } from "@kurx/ui";
import { liveProtected } from "@/lib/event-protection";
import { EditEventForm } from "@/components/host/edit-event-form";
import { EventDetailsReadonly } from "@/components/host/event-details-readonly";
import { PendingChangeRequest, DecidedChangeRequest } from "@/components/host/change-request-panel";
import { RequestChangesDisclosure } from "@/components/host/request-changes-disclosure";
import { AuthorizationForm } from "@/components/host/authorization-form";
import { VenueSection } from "@/components/host/event-sections";
import { VenueManager } from "@/components/host/venue-manager";

export default async function EventDetailsPage({ params }: { params: { id: string } }) {
  const { session, orgId, event, caps } = await requireEventOrg(params.id);
  const [categories, subcategories, venues, authorization, roles, changeRequests] = await Promise.all([
    listCategories(),
    listSubcategories(),
    listOrgVenues(session.accessToken, orgId).catch(() => []),
    getEventAuthorization(session.accessToken, event.id).catch(() => null),
    // The vocabulary the server validates against. Empty on failure is honest: the form then offers
    // nothing rather than a stale guess the API would reject.
    getRepresentativeRoles(session.accessToken).catch(() => [] as string[]),
    // D-388. Swallowed to empty rather than failing the page: a details page that renders without its
    // change history beats one that 500s, and the protected/editable decision below does not depend on
    // this call — it is the event's own product and status.
    listEventChangeRequests(session.accessToken, orgId, event.id).catch(() => [])
  ]);

  /*
   * D-388 — a live PUBLIC event's details are a record, not a form.
   *
   * `liveProtected` mirrors the server's `EventStatusWorkflow.IsLiveProtected`; the server is what
   * actually refuses a direct PATCH (`change_request_required`), and this only decides which of the two
   * experiences to draw. A draft, an approved-but-unpublished event, and every Private event fall
   * through to the editable form exactly as before — nothing about the creation flow changes.
   */
  const protectedNow = liveProtected(event.product, event.status);
  const pending = changeRequests.find((c) => c.status === "pending") ?? null;
  const decided = changeRequests.filter((c) => c.status !== "pending").slice(0, 5);
  const categoryName = categories.find((c) => c.id === event.category_id)?.name;
  const typeName = subcategories.find((c) => c.id === event.type_id)?.name;

  /*
   * Representation is answered once, on Create Event's Representing step. It re-appears HERE — inside
   * Edit Event — for exactly one reason: a reviewer read the authorization and asked for changes, or
   * refused it. That is a correction to this event, so it belongs with editing the event.
   *
   * Deliberately not a standing section. Rendering it unconditionally would rebuild the workspace
   * Representing tab this change removed, and put a second permanent place to answer a question that
   * Create Event already answered — which is what made organisers think creation was unfinished.
   *
   * `null` is included because an event whose authorization was never filed is in the same position as
   * one that was rejected: something is missing and only the organiser can supply it.
   */
  const needsCorrection = authorization === null
    || authorization.status === "Rejected"
    || authorization.status === "ChangesRequested";
  const rep = caps.representation;

  return (
    <div className="space-y-6">
      <Card>
        <div className="mb-4 flex flex-wrap items-center justify-between gap-2">
          <h2 className="text-lg font-semibold">Event details</h2>
          {protectedNow ? (
            <span className="text-xs text-muted">Live · version {event.version}</span>
          ) : null}
        </div>

        {!protectedNow ? (
          <EditEventForm orgId={orgId} event={event} categories={categories} subcategories={subcategories} />
        ) : pending ? (
          // A pending proposal replaces the form entirely. Showing both would let a second edit silently
          // overwrite the values a reviewer is reading — see the panel's own remarks.
          <PendingChangeRequest orgId={orgId} eventId={event.id} request={pending} />
        ) : (
          <div className="space-y-4">
            <p className="text-sm text-muted">
              This event is live. Its details are what a reviewer approved and what your attendees have
              seen, so changes to them go back through approval — your event stays exactly as it is
              until then.
            </p>
            <EventDetailsReadonly event={event} categoryName={categoryName} typeName={typeName} />
            <RequestChangesDisclosure>
              <EditEventForm
                orgId={orgId}
                event={event}
                categories={categories}
                subcategories={subcategories}
                mode="proposal"
              />
            </RequestChangesDisclosure>
          </div>
        )}

        {decided.length > 0 ? (
          <div className="mt-6 space-y-2">
            <h3 className="text-sm font-semibold text-text">Past change requests</h3>
            {decided.map((c) => <DecidedChangeRequest key={c.id} request={c} />)}
          </div>
        ) : null}
      </Card>

      {needsCorrection ? (
        // The anchor sits on a wrapper because `Card` takes no `id`; Readiness links straight at it, so
        // the organiser arrives on the thing they were told to fix rather than the top of an edit page.
        <div id="representing"><Card>
          <h2 className="flex items-center gap-2 text-lg font-semibold text-text">
            <ShieldCheck size={16} aria-hidden /> Representing
          </h2>
          <p className="mt-1 text-sm text-muted">
            {authorization === null
              ? "This event's authorization hasn't been filed yet."
              : "A reviewer asked for changes to this event's authorization."}{" "}
            Correct it here and submit it again.
          </p>
          <dl className="mt-4 grid gap-3 sm:grid-cols-2">
            <div>
              <dt className="text-xs text-muted">Organization</dt>
              {/* A pre-D-379 event whose "organization" is a self-representation row (D-268) is named
                  as legacy, never dressed up as an institution — the same call the admin console makes. */}
              <dd className="text-sm text-text">
                {rep.kind === "personal" ? "Legacy personal representation" : rep.name ?? "Not named"}
              </dd>
            </div>
            <div>
              <dt className="text-xs text-muted">Organization verification</dt>
              <dd className={`text-sm font-semibold ${rep.verified ? "text-success" : "text-muted"}`}>
                {rep.verification_status ?? (rep.verified ? "verified" : "unverified")}
              </dd>
            </div>
          </dl>
          {/* Stated rather than silently unavailable: the organization is fixed at creation
              (`Event.RepresentingOrgId` has no update path), so somebody looking for a way to swap it
              should find out here that an event needing a different one is a different event. */}
          <p className="mt-3 text-xs text-muted">
            The organization an event represents is chosen when the event is created and can&apos;t be
            changed afterwards. Only the authorization below can be corrected.
          </p>
          <div className="mt-6">
            <AuthorizationForm
              eventId={event.id}
              existing={authorization}
              eventStatus={event.status}
              roles={roles}
            />
          </div>
        </Card></div>
      ) : null}

      <Card>
        <h2 className="mb-4 text-lg font-semibold">Venue</h2>
        <VenueSection orgId={orgId} eventId={event.id} venueName={event.venue.name} />
      </Card>
      <Card>
        <h2 className="mb-4 text-lg font-semibold">Venue library</h2>
        <VenueManager orgId={orgId} eventId={event.id} venues={venues} />
      </Card>
    </div>
  );
}
