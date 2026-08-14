import { requireEventOrg } from "@/lib/event-org";
import { can } from "@/lib/capabilities";
import { listAnnouncements } from "@/lib/api";
import { Button, Card, controlClass } from "@kurx/ui";
import { formatDateTime } from "@/lib/formatters";
import { CreateAnnouncementForm } from "@/components/host/create-announcement-form";
import { cancelAnnouncementAction, updateAnnouncementAction } from "@/lib/announcement-actions";
import { ConfirmSubmitButton } from "@/components/host/confirm-submit-button";

export default async function EventAnnouncementsPage({ params }: { params: { id: string } }) {
  const { session, orgId, caps } = await requireEventOrg(params.id);
  // From the capability matrix, not `role`: representation authority is `null` on a personal
  // representation by design (D-268), so gating on it hid announcements from the event's own
  // creator (D-289).
  const canManage = can(caps, "announcements", "create");
  const announcements = await listAnnouncements(session.accessToken, params.id);

  return (
    <div className="space-y-6">
      <section className="space-y-3">
        {announcements.length === 0 ? (
          <Card><p className="text-sm text-muted">No announcements for this event yet.</p></Card>
        ) : (
          <div className="space-y-3">
            {announcements.map((a) => (
              <Card key={a.id}>
                <div className="flex items-start justify-between gap-3">
                  <div>
                    <h3 className="font-semibold">{a.title}</h3>
                    <p className="mt-1 text-xs text-muted">
                      {a.audience} · {a.channels.join(", ") || "no channels"}
                      {a.scheduled_at ? ` · scheduled ${formatDateTime(a.scheduled_at)}` : ""}
                    </p>
                  </div>
                  <span className={`shrink-0 text-xs font-semibold capitalize ${a.status.toLowerCase() === "sent" ? "text-success" : "text-muted"}`}>{a.status}</span>
                </div>
                <p className="mt-3 whitespace-pre-wrap text-sm text-text">{a.body}</p>
                <div className="mt-3 flex flex-wrap items-center justify-between gap-3 text-xs text-muted">
                  <span>{a.total_recipients} recipients · push {a.sent_push} · email {a.sent_email} · whatsapp {a.sent_whatsapp}{a.failed_count > 0 ? ` · ${a.failed_count} failed` : ""}</span>
                  {canManage && a.status.toLowerCase() === "queued" ? (
                    <form action={cancelAnnouncementAction.bind(null, a.id, params.id)}>
                      <ConfirmSubmitButton
                        label="Cancel"
                        title="Cancel this announcement?"
                        description="It will not be delivered to anyone who has not already received it. This cannot be undone."
                        confirmLabel="Cancel it"
                      />
                    </form>
                  ) : null}
                </div>
                {canManage ? (
                  <details className="mt-3">
                    <summary className="cursor-pointer text-xs text-accent-text">Edit</summary>
                    <form action={updateAnnouncementAction.bind(null, a.id, params.id, null)} className="mt-2 space-y-2">
                      <input name="title" defaultValue={a.title} aria-label="Announcement title" className={controlClass} />
                      <textarea name="body" defaultValue={a.body} rows={3} aria-label="Announcement body" className={controlClass} />
                      <Button type="submit" variant="secondary">Save announcement</Button>
                    </form>
                  </details>
                ) : null}
              </Card>
            ))}
          </div>
        )}
      </section>

      {canManage ? (
        <details className="rounded-lg border border-border bg-surface p-5 shadow-github">
          <summary className="cursor-pointer text-sm font-semibold text-text">New announcement</summary>
          <div className="mt-4">
            <CreateAnnouncementForm eventId={params.id} />
          </div>
        </details>
      ) : (
        <p className="text-xs text-muted">Only Owners and Managers can send announcements.</p>
      )}
    </div>
  );
}
