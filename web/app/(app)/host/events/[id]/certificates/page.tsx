import { requireEventOrg } from "@/lib/event-org";
import { can } from "@/lib/capabilities";
import { listEventCertificates, section } from "@/lib/api";
import { Button, Card, controlClass } from "@kurx/ui";
import { formatDate } from "@/lib/formatters";
import { GenerateCertificatesForm } from "@/components/host/generate-certificates-form";
import { revokeCertificateAction } from "@/lib/certificate-actions";
import { ConfirmSubmitButton } from "@/components/host/confirm-submit-button";

export default async function EventCertificatesPage({ params }: { params: { id: string } }) {
  const { session, orgId, caps } = await requireEventOrg(params.id);
  // From the capability matrix, not `role` (D-289). Issuing and revoking certificates is an
  // operational action on this event's attendees, which is exactly what `attendees.manage`
  // gates; `role` is representation authority and is `null` for a personal event (D-268).
  const canManage = can(caps, "attendees", "manage");
  // An outage rendered the same empty roster as "no certificates issued", which on this screen would
  // send a host off to re-issue certificates their attendees already hold (D-235).
  const rosterResult = await section(listEventCertificates(session.accessToken, params.id));
  const roster = rosterResult.state === "ok" ? rosterResult.data : [];
  const rosterFailed = rosterResult.state !== "ok";

  return (
    <div className="space-y-6">
      {rosterFailed ? (
        <p role="status" className="rounded-lg border border-dashed border-border p-4 text-sm text-muted">
          The issued-certificate roster couldn&apos;t be loaded. Generating is idempotent, so it is safe
          to retry — but the list below is not a record of what has been issued.
        </p>
      ) : null}
      <Card>
        <h2 className="font-semibold text-text">Generate certificates</h2>
        <p className="mt-2 text-sm text-muted">
          Idempotent — each eligible ticket gets one certificate (checked-in attendees when certificates are
          gated, otherwise all non-void tickets). Recipients with an email on file are emailed a copy; anyone can
          verify at <code className="text-text">/verify/&#123;code&#125;</code>.
        </p>
        {canManage ? (
          <div className="mt-4"><GenerateCertificatesForm eventId={params.id} /></div>
        ) : (
          <p className="mt-4 text-xs text-muted">Only Owners and Managers can generate certificates.</p>
        )}
      </Card>

      <section className="space-y-3">
        <h2 className="text-lg font-semibold">Issued certificates ({roster.length})</h2>
        {roster.length === 0 ? (
          <Card><p className="text-sm text-muted">No certificates issued yet.</p></Card>
        ) : (
          <Card className="overflow-x-auto p-0">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-border text-left text-muted">
                  <th scope="col" className="px-5 py-3 font-medium">Holder</th>
                  <th scope="col" className="px-5 py-3 font-medium">Code</th>
                  <th scope="col" className="px-5 py-3 font-medium">Issued</th>
                  <th scope="col" className="px-5 py-3 font-medium">Status</th>
                  {canManage ? <th scope="col" className="px-5 py-3" /> : null}
                </tr>
              </thead>
              <tbody>
                {roster.map((c) => (
                  <tr key={c.id} className="border-b border-border last:border-0">
                    <td className="px-5 py-3 text-text">{c.holder_name || "—"}</td>
                    <td className="px-5 py-3 font-mono text-xs text-muted">{c.verify_code}</td>
                    <td className="whitespace-nowrap px-5 py-3 text-muted">{formatDate(c.issued_at, "en-IN", { day: "numeric", month: "short", year: "numeric" })}</td>
                    <td className={`px-5 py-3 font-medium ${c.is_revoked ? "text-danger" : "text-success"}`}>{c.is_revoked ? "Revoked" : "Valid"}</td>
                    {canManage ? (
                      <td className="px-5 py-3 text-right">
                        {c.is_revoked ? (
                          <span className="text-xs text-muted">{c.revoked_reason || "revoked"}</span>
                        ) : (
                          <form action={revokeCertificateAction.bind(null, c.id, params.id)} className="flex items-center justify-end gap-1">
                            <input name="reason" placeholder="Reason" aria-label="Reason for revoking" className={`${controlClass} w-40`} />
                            <ConfirmSubmitButton
                              label="Revoke"
                              title="Revoke this certificate?"
                              description="Anyone checking its verification link will be told it is no longer valid."
                              confirmLabel="Revoke"
                            />
                          </form>
                        )}
                      </td>
                    ) : null}
                  </tr>
                ))}
              </tbody>
            </table>
          </Card>
        )}
      </section>
    </div>
  );
}
