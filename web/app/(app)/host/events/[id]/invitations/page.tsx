import { requireEventOrg } from "@/lib/event-org";
import { can } from "@/lib/capabilities";
import { listInvitations, listInviteLinks } from "@/lib/api";
import { Button, Card, Stat } from "@kurx/ui";
import { AddInvitationForm } from "@/components/host/add-invitation-form";
import { UsernameInvite } from "@/components/host/username-invite";
import { sendInvitationsAction, revokeInvitationAction, resendInvitationAction, importInvitationsAction } from "@/lib/invitation-actions";
import { InviteLinks } from "@/components/host/invite-links";
import { ConfirmSubmitButton } from "@/components/host/confirm-submit-button";

export default async function EventInvitationsPage({ params }: { params: { id: string } }) {
  const { session, orgId, caps } = await requireEventOrg(params.id);
  // From the capability matrix, not `role` (D-289). Inviting people to attend is an operational
  // action on this event's attendees; `role` is representation authority and is `null` for a
  // personal event (D-268).
  const canManage = can(caps, "attendees", "manage");
  const { items, funnel } = await listInvitations(session.accessToken, params.id);
  // D-266 M6 (D9 Method B). Non-fatal: the per-invitee list is still useful if links fail to load.
  const inviteLinks = canManage ? await listInviteLinks(session.accessToken, params.id).catch(() => []) : [];

  return (
    <div className="space-y-6">
      <div className="grid gap-4 sm:grid-cols-3 xl:grid-cols-5">
        <Stat label="Invited" value={String(funnel.invited)} />
        <Stat label="Sent" value={String(funnel.sent)} />
        <Stat label="Accepted" value={String(funnel.accepted)} />
        <Stat label="Declined" value={String(funnel.declined)} />
        <Stat label="Registered" value={String(funnel.registered)} />
      </div>

      <section className="space-y-3">
        {canManage ? (
          <div className="flex justify-end">
            <form action={sendInvitationsAction.bind(null, params.id)}>
              <Button type="submit" variant="secondary">Send pending</Button>
            </form>
          </div>
        ) : null}

        {items.length === 0 ? (
          <Card><p className="text-sm text-muted">No guests invited to this event yet.</p></Card>
        ) : (
          <Card className="p-0">
            {/* Focusable like DataTable's wrapper, so keyboard users can scroll the table. */}
            <div tabIndex={0} aria-label="Invited guests" className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-border text-left text-muted">
                  <th scope="col" className="px-5 py-3 font-medium">Guest</th>
                  <th scope="col" className="px-5 py-3 font-medium">Channel</th>
                  <th scope="col" className="px-5 py-3 font-medium">Sent</th>
                  <th scope="col" className="px-5 py-3 font-medium">RSVP</th>
                  {canManage ? <th scope="col" className="px-5 py-3" /> : null}
                </tr>
              </thead>
              <tbody>
                {items.map((inv) => (
                  <tr key={inv.id} className="border-b border-border last:border-0">
                    <td className="px-5 py-3">
                      <div className="font-medium text-text">{inv.name || inv.email || inv.phone}</div>
                      <div className="text-xs text-muted">{inv.email || inv.phone}</div>
                    </td>
                    <td className="px-5 py-3 text-muted">{inv.channel}</td>
                    <td className="px-5 py-3 capitalize text-muted">{inv.send_status}{inv.send_count > 0 ? ` (${inv.send_count})` : ""}</td>
                    <td className={`px-5 py-3 font-medium capitalize ${inv.rsvp_status.toLowerCase() === "accepted" ? "text-success" : "text-muted"}`}>{inv.rsvp_status}</td>
                    {canManage ? (
                      <td className="px-5 py-3">
                        <div className="flex items-center justify-end gap-2">
                          <form action={resendInvitationAction.bind(null, inv.id, params.id)}>
                            <Button type="submit" variant="ghost">Resend</Button>
                          </form>
                          {inv.status.toLowerCase() !== "revoked" ? (
                            <form action={revokeInvitationAction.bind(null, inv.id, params.id)}>
                              <ConfirmSubmitButton
                                label="Revoke"
                          title="Revoke this invitation?"
                          description="The invitee will no longer be able to accept it."
                          confirmLabel="Revoke"
                              />
                            </form>
                          ) : <span className="text-xs text-muted">revoked</span>}
                        </div>
                      </td>
                    ) : null}
                  </tr>
                ))}
              </tbody>
            </table>
            </div>
          </Card>
        )}
      </section>

      {canManage ? (
        <details className="rounded-lg border border-border bg-surface p-5 shadow-github">
          <summary className="cursor-pointer text-sm font-semibold text-text">Invite a guest</summary>
          <div className="mt-4 space-y-6">
            {/* D-266 M6 (D9) — the two delivery methods of ONE `Invite Only` policy, side by side.
                Method A first: it needs no contact detail, so it is the better choice whenever the
                person is already on Kurx. */}
            <UsernameInvite eventId={params.id} />
            <div className="border-t border-border pt-6">
              <p className="mb-3 text-sm font-medium text-text">Or invite by email or phone</p>
              <AddInvitationForm eventId={params.id} />
            </div>
          </div>
        </details>
      ) : (
        <p className="text-xs text-muted">Only Owners and Managers can invite guests.</p>
      )}

      {/* D-266 M6 (D9 Method B) — the other delivery method for the same `Invite Only` policy. */}
      {canManage ? (
        <Card>
          <h2 className="mb-1 text-lg font-semibold">Invite links</h2>
          <p className="mb-4 text-sm text-muted">
            Share a link with people who aren&apos;t on Kurx yet. A link grants permission to register — it
            never skips payment.
          </p>
          <InviteLinks eventId={params.id} links={inviteLinks} />
        </Card>
      ) : null}

      {canManage ? (
        <details className="rounded-lg border border-border bg-surface p-5 shadow-github">
          <summary className="cursor-pointer text-sm font-semibold text-text">Import guests from CSV</summary>
          <form action={importInvitationsAction.bind(null, params.id, null)} className="mt-4 flex flex-wrap items-center gap-2">
            <input name="file" type="file" accept=".csv,text/csv" required className="text-sm" />
            <Button type="submit" variant="secondary">Import CSV</Button>
          </form>
        </details>
      ) : null}
    </div>
  );
}
