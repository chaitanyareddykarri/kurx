import { Alert, Button, Card, LinkButton } from "@kurx/ui";
import { requireEventOrg } from "@/lib/event-org";
import { can } from "@/lib/capabilities";
import { listOrgTicketTypes, listTicketTypeFields } from "@/lib/api";
import { formatCurrency } from "@/lib/formatters";
import { CreateTicketTypeForm } from "@/components/host/create-ticket-type-form";
import { EditTicketTypeForm } from "@/components/host/edit-ticket-type-form";
import { AddFormFieldForm } from "@/components/host/add-form-field-form";
import { ConfirmSubmitButton } from "@/components/host/confirm-submit-button";
import { deleteTicketTypeAction, deleteFieldAction, updateFieldAction } from "@/lib/ticket-actions";

function normalize(raw: string | string[] | undefined) {
  return Array.isArray(raw) ? raw[0] : raw;
}

export default async function EventTicketsPage({
  params,
  searchParams
}: {
  params: { id: string };
  searchParams: { [key: string]: string | string[] | undefined };
}) {
  const { session, orgId, caps, event } = await requireEventOrg(params.id);
  const eventId = params.id;
  // From the capability matrix, not from `role`. `role` is the caller's authority over the event's
  // *representation*, and D-268 is explicit that this is a grant on top of ownership and never its
  // source — so on a personal representation it is `null` by design, and gating on it hid the create
  // form from the one person who definitely may use it: the event's own creator (D-289). The backend
  // is the single source of truth for permissions and the client is not supposed to infer them.
  const canManage = can(caps, "tickets", "create");

  const ticketTypes = await listOrgTicketTypes(session.accessToken, orgId, eventId);
  const requestedTt = normalize(searchParams.ticketTypeId);
  const activeTt = requestedTt ? ticketTypes.find((t) => t.id === requestedTt) ?? null : null;
  const fields = activeTt ? await listTicketTypeFields(session.accessToken, orgId, eventId, activeTt.id) : [];

  const base = `/host/events/${eventId}/tickets`;

  return (
    <div className="space-y-6">
      {/* D-363 §4 — price is a field the reviewer assessed, so any change here (including adding a type)
          sends an approved event back to the queue, and a reviewer holding it refuses the change
          outright. Both are better learned before the form than from a rejected save. */}
      {event.status === "approved" ? (
        <Alert tone="warning" title="This event is approved">
          Changing a price, a quantity or the list of ticket types returns it to review — that is what a
          reviewer signed off on.
        </Alert>
      ) : null}
      {event.status === "pendingreview" || event.status === "underreview" ? (
        <Alert tone="info" title="This event is with a reviewer">
          Ticket types can&apos;t be changed until it comes back.
        </Alert>
      ) : null}
      <section className="space-y-3">
        <h2 className="text-lg font-semibold text-text">Ticket types</h2>
        {ticketTypes.length === 0 ? (
          <Card><p className="text-sm text-muted">No ticket types for this event yet.</p></Card>
        ) : (
          <div className="grid gap-3 md:grid-cols-2">
            {ticketTypes.map((t) => (
              <Card key={t.id} className={activeTt?.id === t.id ? "border-accent/70" : ""}>
                <div className="flex items-start justify-between gap-3">
                  <div>
                    <h3 className="font-semibold text-text">{t.name}</h3>
                    <p className="mt-1 text-xs text-muted">
                      {t.registration_mode}{t.is_competition ? " · competition" : ""}{t.is_all_access ? " · all-access" : ""}
                    </p>
                  </div>
                  {/* D-366 — a banded ticket has no single price, so the headline says "from" and the
                      table below carries the rest. Printing `price_paise` bare would show the cheapest
                      band as if it were the price, on the one screen where the organiser edits it. */}
                  <span className="shrink-0 text-sm font-semibold text-text">
                    {t.price_tiers?.length
                      ? `from ${formatCurrency(Math.min(...t.price_tiers.map((b) => b.price_paise)))}`
                      : t.price_paise > 0 ? formatCurrency(t.price_paise) : "Free"}
                  </span>
                </div>
                {/* The bands themselves. The edit form below cannot yet change them — it PATCHes the
                    row without them, which the server now reads as "leave them alone" — so this is the
                    only place an organiser can see what a team of each size actually pays. */}
                {t.price_tiers?.length ? (
                  <ul className="mt-3 space-y-0.5 text-xs text-muted">
                    {[...t.price_tiers].sort((a, b) => a.min_size - b.min_size).map((b) => (
                      <li key={`${b.min_size}-${b.max_size}`}>
                        {b.min_size === b.max_size ? `${b.min_size} members` : `${b.min_size}–${b.max_size} members`}
                        {" — "}
                        <span className="text-text">{formatCurrency(b.price_paise)}</span>
                      </li>
                    ))}
                  </ul>
                ) : null}
                <div className="mt-4 flex items-center justify-between text-xs text-muted">
                  <span>Sold {t.sold} / {t.quantity}</span>
                  <span className={t.available > 0 ? "text-success" : "text-muted"}>{t.available > 0 ? `${t.available} available` : "Sold out"}</span>
                </div>
                <div className="mt-4 flex items-center gap-3">
                  {/* No size override: `sm` is 36px and the design system reserves it for pointer-only
                      admin density. The host workspace is used on phones. */}
                  <LinkButton href={`${base}?ticketTypeId=${t.id}`} variant="secondary">Manage fields</LinkButton>
                  {canManage ? (
                    <form action={deleteTicketTypeAction.bind(null, orgId, eventId, t.id)}>
                      <ConfirmSubmitButton
                        label="Delete"
                        title={`Delete the ${t.name} ticket type?`}
                        description={
                          t.sold > 0
                            ? `${t.sold} ${t.sold === 1 ? "ticket has" : "tickets have"} already been sold on this type. This cannot be undone.`
                            : "This cannot be undone."
                        }
                      />
                    </form>
                  ) : null}
                </div>
              </Card>
            ))}
          </div>
        )}
      </section>

      {activeTt ? (
        <section className="space-y-3">
          {canManage ? (
            <details className="rounded-lg border border-border bg-surface p-5 shadow-github">
              <summary className="cursor-pointer text-sm font-semibold text-text">Edit ticket type · {activeTt.name}</summary>
              <div className="mt-4">
                <EditTicketTypeForm orgId={orgId} eventId={eventId} ticketType={activeTt} />
              </div>
            </details>
          ) : null}
          <h2 className="text-lg font-semibold">Registration form — {activeTt.name}</h2>
          {fields.length === 0 ? (
            <Card><p className="text-sm text-muted">No custom fields. Buyers just provide the built-in contact details.</p></Card>
          ) : (
            <Card className="overflow-x-auto p-0">
              <table className="w-full text-sm">
                <thead>
                  <tr className="border-b border-border text-left text-muted">
                    <th scope="col" className="px-5 py-3 font-medium">Key</th>
                    <th scope="col" className="px-5 py-3 font-medium">Label</th>
                    <th scope="col" className="px-5 py-3 font-medium">Type</th>
                    <th scope="col" className="px-5 py-3 font-medium">Scope</th>
                    <th scope="col" className="px-5 py-3 font-medium">Required</th>
                    {canManage ? <th scope="col" className="px-5 py-3" /> : null}
                  </tr>
                </thead>
                <tbody>
                  {fields.map((f) => (
                    <tr key={f.id} className="border-b border-border last:border-0">
                      <td className="px-5 py-3 font-mono text-xs text-text">{f.key}</td>
                      <td className="px-5 py-3">{f.label}</td>
                      <td className="px-5 py-3 text-muted">{f.type}</td>
                      <td className="px-5 py-3 text-muted">{f.scope}</td>
                      <td className="px-5 py-3">{f.required ? "Yes" : "No"}</td>
                      {canManage ? (
                        <td className="px-5 py-3">
                          <div className="flex items-start justify-end gap-2">
                            <details>
                              <summary className="cursor-pointer text-xs text-accent-text">Edit</summary>
                              <form action={updateFieldAction.bind(null, orgId, eventId, activeTt.id, f.id, null)} className="mt-2 flex flex-col gap-1 text-left">
                                <label className="text-xs text-muted">
                                  <span className="sr-only">Label for the {f.key} field</span>
                                  <input
                                    name="label"
                                    defaultValue={f.label}
                                    aria-label={`Label for the ${f.key} field`}
                                    className="min-h-11 w-full rounded-md border border-border-strong bg-background px-2 text-xs text-text focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/30"
                                  />
                                </label>
                                <label className="flex items-center gap-1 text-xs text-muted"><input type="checkbox" name="required" defaultChecked={f.required} /> Required</label>
                                <input type="hidden" name="key" value={f.key} />
                                <input type="hidden" name="type" value={f.type} />
                                <input type="hidden" name="scope" value={f.scope} />
                                <Button type="submit" variant="secondary">Save</Button>
                              </form>
                            </details>
                            <form action={deleteFieldAction.bind(null, orgId, eventId, activeTt.id, f.id)}>
                              <ConfirmSubmitButton
                                label="Remove"
                                title={`Remove the "${f.label}" field?`}
                                description="Buyers will no longer be asked for it. Answers already collected are not shown here once the field is gone."
                                confirmLabel="Remove"
                              />
                            </form>
                          </div>
                        </td>
                      ) : null}
                    </tr>
                  ))}
                </tbody>
              </table>
            </Card>
          )}
          {canManage ? (
            <Card>
              <h3 className="mb-3 font-semibold">Add a field</h3>
              <AddFormFieldForm orgId={orgId} eventId={eventId} ticketTypeId={activeTt.id} />
            </Card>
          ) : null}
        </section>
      ) : null}

      {canManage ? (
        <details className="rounded-lg border border-border bg-surface p-5 shadow-github">
          <summary className="cursor-pointer text-sm font-semibold text-text">Add a ticket type</summary>
          <div className="mt-4">
            <CreateTicketTypeForm orgId={orgId} eventId={eventId} />
          </div>
        </details>
      ) : (
        // Names the authority the reader lacks without asserting who they are. The old copy — "Only
        // Owners and Managers can create or edit ticket types" — was shown to the event's own owner
        // (D-289), so it told the one person with the most authority here that they had none.
        <p className="text-xs text-muted">You don&apos;t have permission to create or edit ticket types for this event.</p>
      )}
    </div>
  );
}
