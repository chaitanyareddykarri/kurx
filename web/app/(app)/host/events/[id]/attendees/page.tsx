import { requireEventOrg } from "@/lib/event-org";
import { listAttendees, getAllyStatusBatch, type AllyRelationStatus } from "@/lib/api";
import { Avatar, Card } from "@kurx/ui";
import { formatDate } from "@/lib/formatters";
import { CsvDownloadButton } from "@/components/host/csv-download-button";
import { exportAttendeesAction } from "@/lib/event-actions";
import { AllyConnectButton } from "@/components/profile/ally-connect-button";
import { toAllyRelation } from "@/lib/ally-status";

export default async function EventAttendeesPage({ params }: { params: { id: string } }) {
  const { session, orgId } = await requireEventOrg(params.id);

  const { items, total } = await listAttendees(session.accessToken, orgId, params.id);
  const otherBuyerIds = items.map((a) => a.buyer_user_id).filter((id): id is string => id !== null && id !== session.me.id);
  const allyStatus = await getAllyStatusBatch(session.accessToken, otherBuyerIds).catch(() => ({}) as Record<string, AllyRelationStatus>);

  return (
    <section className="space-y-3">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h2 className="text-lg font-semibold">{total} {total === 1 ? "attendee" : "attendees"}</h2>
        {items.length > 0 ? <CsvDownloadButton action={exportAttendeesAction.bind(null, orgId, params.id)} filename={`attendees-${params.id}.csv`} /> : null}
      </div>
      {items.length === 0 ? (
        <Card><p className="text-sm text-muted">No attendees yet.</p></Card>
      ) : (
        <Card className="overflow-x-auto p-0">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-border text-left text-muted">
                <th scope="col" className="px-5 py-3 font-medium">Attendee</th>
                <th scope="col" className="px-5 py-3 font-medium">Ticket type</th>
                <th scope="col" className="px-5 py-3 font-medium">Group</th>
                <th scope="col" className="px-5 py-3 font-medium">Status</th>
                <th scope="col" className="px-5 py-3 font-medium">Checked in</th>
                <th scope="col" className="px-5 py-3 font-medium">&nbsp;</th>
              </tr>
            </thead>
            <tbody>
              {items.map((a) => (
                <tr key={a.ticket_id} className="border-b border-border last:border-0">
                  <td className="px-5 py-3">
                    <div className="flex items-center gap-2">
                      <Avatar name={a.buyer_name || a.buyer_phone || "?"} src={a.buyer_avatar_key ?? undefined} size={28} />
                      <div>
                        {a.buyer_username ? (
                          <a href={`/u/${a.buyer_username}`} className="font-medium text-text hover:text-accent-text">{a.buyer_name || a.buyer_phone}</a>
                        ) : (
                          <div className="font-medium text-text">{a.buyer_name || a.buyer_phone || "—"}</div>
                        )}
                        {a.buyer_phone ? <div className="text-xs text-muted">{a.buyer_phone}</div> : null}
                      </div>
                    </div>
                  </td>
                  <td className="px-5 py-3 text-muted">{a.ticket_type_name}</td>
                  <td className="px-5 py-3 text-muted">{a.group_number != null ? (a.group_display_name || `Team ${a.group_number}`) : "—"}</td>
                  <td className={`px-5 py-3 font-medium capitalize ${a.state.toLowerCase() === "checkedin" ? "text-success" : a.state.toLowerCase() === "void" ? "text-muted" : "text-text"}`}>{a.state}</td>
                  <td className="whitespace-nowrap px-5 py-3 text-muted">{a.checked_in_at ? formatDate(a.checked_in_at, "en-IN", { day: "numeric", month: "short", year: "numeric" }) : "—"}</td>
                  <td className="px-5 py-3">
                    {a.buyer_user_id && a.buyer_user_id !== session.me.id && (
                      <AllyConnectButton targetUserId={a.buyer_user_id} initialRelation={toAllyRelation(allyStatus[a.buyer_user_id])} initialConnectionId={null} />
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </Card>
      )}
    </section>
  );
}
