import { redirect } from "next/navigation";
import { Card, EmptyState } from "@kurx/ui";
import { Award } from "lucide-react";
import { requireStaffSession } from "@/lib/session";
import { PageHeader } from "@/components/layout/page-header";
import { listAdminEvents, listEventCertificates, apiErrorMessage, apiErrorStatus } from "@/lib/api";
import { CertificatesManager } from "@/components/admin/certificates-manager";
import { EventPicker } from "@/components/admin/event-picker";

// Certificates are event-scoped on the backend (D-036/D-064) — there is no platform-wide roster
// endpoint — so this screen is "pick an event, then manage its roster". Cross-org access rides on the
// `kurx_admin` claim, which is granted to SuperAdmin only; any other staff role reaches the backend
// without it and gets a 403, surfaced below.
export default async function CertificatesPage({
  searchParams
}: {
  searchParams: { eventId?: string; q?: string };
}) {
  const session = await requireStaffSession();
  // SuperAdmin only — cross-org certificate access rides on the kurx_admin claim, which only
  // SuperAdmin receives. Same bounce as staff/page.tsx.
  if (!session.roles.includes("SuperAdmin")) redirect("/");
  const eventId = searchParams.eventId ?? "";
  const q = (searchParams.q ?? "").trim();

  try {
    const { items: events } = await listAdminEvents(session.accessToken, q ? { q } : undefined);
    const selected = events.find((e) => e.event_id === eventId) ?? null;
    const certificates = selected ? await listEventCertificates(session.accessToken, selected.event_id) : [];

    return (
      <div className="space-y-6">
        <PageHeader
          kicker="Events"
          title="Certificates"
          description="Bulk-generate certificates for an event's eligible tickets, and revoke individual ones. Revoked certificates stay verifiable — they report as revoked rather than disappearing."
        />

        <EventPicker events={events} selectedId={eventId} query={q} />

        {selected ? (
          <CertificatesManager
            eventId={selected.event_id}
            eventTitle={selected.title}
            certificates={certificates}
          />
        ) : (
          <Card>
            <EmptyState
              icon={<Award size={20} />}
              title="Choose an event"
              message="Search for an event above to see and manage its certificate roster."
            />
          </Card>
        )}
      </div>
    );
  } catch (err) {
    const forbidden = apiErrorStatus(err) === 403;
    return (
      <div className="space-y-4">
        <h1 className="text-2xl font-semibold text-text">Certificates</h1>
        <Card>
          {forbidden ? (
            <p className="text-sm text-muted">You don&apos;t have access to certificates.</p>
          ) : (
            <p className="text-sm text-muted">
              Couldn&apos;t load certificates: <span className="text-text">{apiErrorMessage(err)}</span>
            </p>
          )}
        </Card>
      </div>
    );
  }
}
