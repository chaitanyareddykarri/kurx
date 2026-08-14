import { Card } from "@kurx/ui";
import { requireSession } from "@/lib/session";
import {
  getMe, listMyRepresentations, listCategories, listSubcategories, listFieldPresets, getMyIdentity, section
} from "@/lib/api";
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
  const [me, representations, categories, subcategories, presets, identity] = await Promise.all([
    getMe(session.accessToken),
    section(listMyRepresentations(session.accessToken)),
    listCategories(),
    listSubcategories(),
    listFieldPresets(),
    getMyIdentity(session.accessToken).catch(() => null)
  ]);

  /*
   * `.catch(() => [])` here silently removed every organization from the wizard's first step, so a
   * host whose representations failed to load was offered "Personal" alone — and would have created
   * the event under their own name without ever knowing the other choice existed. That is a wrong
   * event, not a degraded one (D-235).
   */
  const representationsFailed = representations.state !== "ok";

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
          hostName={me.name?.trim() || "Myself"}
        />
      </Card>
    </div>
  );
}
