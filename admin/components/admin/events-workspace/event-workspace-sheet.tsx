"use client";

import { useEffect, useState, type ReactNode } from "react";
import { useRouter } from "next/navigation";
import { WEB_URL } from "@/lib/site";
import { ReviewActions } from "@/components/admin/review-actions";
import { useFormState, useFormStatus } from "react-dom";
import {
  Badge, Button, ConfirmDialog, DataTable, Dialog, EmptyState, Field, Input, Menu, Sheet, Tabs, Textarea, useToast,
  type Column, type MenuItem
} from "@kurx/ui";
import {
  AlertTriangle, Archive, Award, CheckCircle2, ChevronDown, Eye, EyeOff, FileText, Image as ImageIcon,
  MessageSquare, MoreHorizontal, Pencil, Rocket, ScrollText, ShieldAlert, ShieldCheck, Trash2, Users,
  Wallet as WalletIcon,
  XCircle
} from "lucide-react";
import {
  manageEventAction, messageOrganizerAction, warnOrganizerAction, deleteDraftEventAction,
  emergencyEditEventAction
} from "@/lib/admin-actions";
import { useAdminEventHub, type AdminLiveEvent } from "@/lib/use-admin-event-hub";
import type {
  AdminEvent, EventAnalyticsAdmin, TicketTypeAdmin, AttendeeAdmin, WalletAdmin, LedgerEntryAdmin,
  OrgEventRow, AuditEntry, Report, eventMediaItemSchema
} from "@/lib/api";
import { money } from "@/lib/format";
import type { z } from "zod";

export type WorkspaceData = {
  analytics: EventAnalyticsAdmin | null;
  ticketTypes: TicketTypeAdmin[];
  attendees: { items: AttendeeAdmin[]; total: number };
  wallet: WalletAdmin | null;
  ledger: LedgerEntryAdmin[];
  orgEvents: OrgEventRow[];
  orgRiskScore: number;
  eventRiskScore: number;
  timeline: AuditEntry[];
  moderation: Report[];
  media: z.infer<typeof eventMediaItemSchema>[];
  publishedAt: string | null;
};

const WORKSPACE_TABS = [
  { id: "overview", label: "Overview" },
  { id: "registrations", label: "Registrations" },
  { id: "tickets", label: "Tickets" },
  { id: "attendees", label: "Attendees" },
  { id: "finance", label: "Finance" },
  { id: "organizer", label: "Organizer" },
  { id: "moderation", label: "Moderation" },
  { id: "media", label: "Media" },
  { id: "timeline", label: "Timeline" }
];

/*
 * D-381 — priority navigation. Nine tabs in one strip overflowed the sheet at laptop width, and an
 * `overflow-x-auto` that scrolls with no affordance reads as a CLIPPED LABEL rather than as more
 * content, which is how it was reported.
 *
 * The split is by how often a reviewer actually opens each section, and it is FIXED rather than
 * width-measured: a ResizeObserver here buys a smoother breakpoint and a whole class of layout bugs,
 * and the sheet has one width (`size="xl"`) anyway. Nothing is lost — the other four are one click
 * away and keep their own labels.
 */
const PRIMARY_TAB_IDS = ["overview", "registrations", "tickets", "attendees", "finance"];

function dt(iso: string | null | undefined) {
  return iso ? new Date(iso).toLocaleString("en-IN") : "—";
}

/// D-266 M4: an event sitting with the platform — submitted but unclaimed, or claimed by a reviewer.
/// Both accept the legacy `publish`/`reject` transitions this sheet posts.
const REVIEW_QUEUE_STATES = new Set(["pendingreview", "underreview"]);

const AUDIT_LABELS: Record<string, string> = {
  "event.status_change": "Status changed", "admin.event.suspend": "Suspended", "admin.event.unsuspend": "Unsuspended",
  "admin.event.hide": "Hidden", "admin.event.unhide": "Unhidden", "admin.event.message": "Message sent to organizer",
  "admin.event.warning": "Warning issued", "event.material_change": "Material change", "event.delete": "Deleted"
};

export function EventWorkspaceSheet({
  event, data, closeHref, isSuperAdmin
}: {
  event: AdminEvent; data: WorkspaceData; closeHref: string; isSuperAdmin: boolean;
}) {
  const [tab, setTab] = useState("overview");
  const [live, setLive] = useState<{ sold: number; checkedIn: number } | null>(null);
  const toast = useToast();
  const router = useRouter();

  const { live: connected, enabled: liveExpected } = useAdminEventHub(event.event_id, event.representing_org_id, (e: AdminLiveEvent) => {
    // AdminLiveEvent is a CLIENT-side shape: use-admin-event-hub maps the hub's raw snake_case payload
    // into camelCase before handing it here, so these stay camelCase regardless of the API convention.
    if (e.kind === "scan" && e.eventId === event.event_id) {
      setLive((prev) => ({ sold: prev?.sold ?? event.registrations_count, checkedIn: (prev?.checkedIn ?? event.checked_in) + 1 }));
      toast("New check-in", "info");
    } else if ((e.kind === "sale" || e.kind === "analytics") && e.eventId === event.event_id) {
      toast(`New sale · ${money(e.amountPaise)}`, "success");
    }
  });

  const close = () => router.push(closeHref, { scroll: false });

  const overflowTabs = WORKSPACE_TABS.filter((t) => !PRIMARY_TAB_IDS.includes(t.id));
  // A selected overflow tab joins the strip, so it is visibly the active one. Without this the strip
  // shows nothing selected while `More` silently owns the section on screen — the user's own
  // "no clipped labels, no unusable overflow" reading of a nav that has lost track of where it is.
  const visibleTabs = WORKSPACE_TABS.filter((t) => PRIMARY_TAB_IDS.includes(t.id) || t.id === tab);

  return (
    <Sheet
      open
      onClose={close}
      size="xl"
      title={
        <div className="flex min-w-0 items-center gap-2">
          <span className="truncate">{event.title}</span>
          {/* Both directions, or the operator cannot tell a quiet gate from a dead socket. */}
          {liveExpected ? (
            connected ? (
              <Badge tone="success">live</Badge>
            ) : (
              <Badge tone="warning">
                not live
                <span className="sr-only"> — showing the figures from when this opened</span>
              </Badge>
            )
          ) : null}
        </div>
      }
    >
      <div className="-mt-1 mb-4">
        <Tabs
          tabs={visibleTabs}
          value={tab}
          onChange={setTab}
          trailing={
            <Menu
              trigger={(props) => (
                <Button variant="ghost" {...props}>
                  More <ChevronDown size={13} />
                </Button>
              )}
              items={overflowTabs.map((t) => ({ label: t.label, onSelect: () => setTab(t.id) }))}
            />
          }
        />
      </div>

      {tab === "overview" ? <OverviewTab event={event} data={data} isSuperAdmin={isSuperAdmin} /> : null}
      {tab === "registrations" ? <RegistrationsTab event={event} data={data} live={live} /> : null}
      {tab === "tickets" ? <TicketsTab data={data} /> : null}
      {tab === "attendees" ? <AttendeesTab data={data} /> : null}
      {tab === "finance" ? <FinanceTab event={event} data={data} /> : null}
      {tab === "organizer" ? <OrganizerTab event={event} data={data} /> : null}
      {tab === "moderation" ? <ModerationTab event={event} data={data} /> : null}
      {tab === "media" ? <MediaTab data={data} /> : null}
      {tab === "timeline" ? <TimelineTab data={data} /> : null}
    </Sheet>
  );
}

// ── Shared bits ───────────────────────────────────────────────────────────
function Stat({ label, value, hint }: { label: string; value: ReactNode; hint?: string }) {
  return (
    <div className="rounded-md border border-border bg-background p-3">
      <p className="text-xs text-muted">{label}</p>
      <p className="mt-1 text-lg font-semibold text-text">{value}</p>
      {hint ? <p className="mt-0.5 text-[11px] text-muted">{hint}</p> : null}
    </div>
  );
}
function Row({ label, value }: { label: string; value: ReactNode }) {
  return (
    <div className="flex items-center justify-between gap-3 border-b border-border/60 py-2 text-sm last:border-0">
      <span className="text-muted">{label}</span>
      <span className="text-right text-text">{value}</span>
    </div>
  );
}
function Gap({ children }: { children: ReactNode }) {
  return <p className="text-xs italic text-muted">{children}</p>;
}
/// D-381 — the overview is grouped now rather than one flat table, so the groups need names.
function SectionTitle({ children }: { children: ReactNode }) {
  return <h3 className="text-xs font-semibold uppercase tracking-wide text-muted">{children}</h3>;
}
/// Secondary text inside a Row's value slot — a legacy note, or an absent name.
function Muted({ children }: { children: ReactNode }) {
  return <span className="text-xs text-muted">{children}</span>;
}

// ── Tab 1: Overview ──────────────────────────────────────────────────────
function OverviewTab({ event: e, data, isSuperAdmin }: { event: AdminEvent; data: WorkspaceData; isSuperAdmin: boolean }) {
  return (
    <div className="space-y-5">
      <ActionBar event={e} isSuperAdmin={isSuperAdmin} />

      {(e.is_suspended || e.is_hidden) && (
        <div className="flex items-start gap-2 rounded-md border border-danger/30 bg-danger/5 p-3">
          <AlertTriangle size={16} className="mt-0.5 shrink-0 text-danger" />
          <div className="text-sm">
            {e.is_suspended ? <p className="text-text">Suspended{e.suspended_reason ? ` — ${e.suspended_reason}` : ""}</p> : null}
            {e.is_hidden ? <p className="text-text">Hidden{e.hidden_reason ? ` — ${e.hidden_reason}` : ""}</p> : null}
          </div>
        </div>
      )}

      <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
        <Stat label="Status" value={<Badge tone="neutral">{e.status}</Badge>} />
        <Stat label="Featured" value={e.is_featured ? "Yes" : "No"} />
        <Stat label="Risk score" value={data.eventRiskScore} hint={data.eventRiskScore >= 100 ? "high risk" : undefined} />
        <Stat label="View count" value={data.analytics?.view_count ?? "—"} />
      </div>

      {/*
        D-381 — the event, then who owns it, then what it represents. Three separate facts.

        The old block printed `org_name` under "Representing organization" and explained, citing D-074,
        that organizer and organization "are the same field in this system". That stopped being true at
        D-267/D-268: a USER owns the event and the organization is what it REPRESENTS. The practical
        result was a legacy self-representation row rendering a person's name as an organization, which
        is the one thing a reviewer must not be shown.
      */}
      <div className="rounded-lg border border-border">
        <Row label="Event" value={e.title} />
        <Row label="Reference" value={<span className="break-all font-mono text-xs">{e.slug}</span>} />
        <Row label="Visibility" value={<Badge tone="neutral">{e.visibility}</Badge>} />
        <Row label="Category" value={e.category ?? "—"} />
        <Row label="Subcategory" value={e.subcategory ?? "—"} />
        <Row label="Created" value={dt(e.created_at)} />
        <Row label="Updated" value={dt(e.updated_at)} />
        <Row label="Published" value={dt(data.publishedAt)} />
      </div>

      <SectionTitle>Representation</SectionTitle>
      <div className="rounded-lg border border-border">
        {/* The user who owns the event (D-268). Never the organization. */}
        <Row label="Creator" value={e.creator_name?.trim() ? e.creator_name : <Muted>Name not set</Muted>} />
        {e.org_is_personal ? (
          <>
            {/*
              A pre-D-379 event whose "organization" is a self-representation row minted only to satisfy
              the non-null FK. Named as legacy rather than dressed up: fabricating an organization here
              would tell a reviewer this event has institutional backing that nobody ever gave.
            */}
            <Row
              label="Representing organization"
              value={<Badge tone="warning">Legacy personal representation</Badge>}
            />
            <Row
              label="Legacy record"
              value={<Muted>Created before every event was required to represent a real organization (D-379). Not migrated, not verified — new events cannot be created this way.</Muted>}
            />
          </>
        ) : (
          <>
            <Row label="Representing organization" value={e.org_name} />
            <Row
              label="Organization verification"
              value={<Badge tone={e.org_verification === "verified" ? "success" : "muted"}>{e.org_verification}</Badge>}
            />
          </>
        )}
      </div>

      {/*
        D-381 — the raw UUID lives here, not at the top of the overview.

        It was the FIRST row on the page, above the event's own name. It stays available because support
        and audit genuinely need it, but the readable identifiers are the title and the slug; an internal
        key is not overview information. No new identifier was invented — Kurx already has `slug`.
      */}
      <SectionTitle>Technical details</SectionTitle>
      <div className="rounded-lg border border-border">
        <Row label="Event ID" value={<CopyableId value={e.event_id} />} />
        <Row label="Organization ID" value={<CopyableId value={e.representing_org_id} />} />
        {e.creator_id ? <Row label="Creator ID" value={<CopyableId value={e.creator_id} />} /> : null}
      </div>
    </div>
  );
}

/// A monospace identifier with a copy control — the reason an admin wants a UUID on screen at all is to
/// paste it into a query or a ticket, so selecting it by hand is the whole friction.
function CopyableId({ value }: { value: string }) {
  const [copied, setCopied] = useState(false);
  return (
    <button
      type="button"
      onClick={() => {
        void navigator.clipboard?.writeText(value);
        setCopied(true);
        setTimeout(() => setCopied(false), 1500);
      }}
      className="inline-flex items-center gap-1.5 rounded px-1 py-0.5 font-mono text-xs text-text hover:bg-elevated"
      aria-label={`Copy ${value}`}
    >
      {value}
      <span className="not-sr-only text-[10px] uppercase tracking-wide text-muted">{copied ? "copied" : "copy"}</span>
    </button>
  );
}

function SubmitBtn({ label, pendingLabel = "…" }: { label: string; pendingLabel?: string }) {
  const { pending } = useFormStatus();
 return <Button type="submit" disabled={pending} variant="secondary" >{pending ? pendingLabel : label}</Button>;
}

function ActionBar({ event: e, isSuperAdmin }: { event: AdminEvent; isSuperAdmin: boolean }) {
  const [state, action] = useFormState(manageEventAction.bind(null, e.representing_org_id, e.event_id), null);
  const toast = useToast();
  const [reasonDialog, setReasonDialog] = useState<"suspend" | "hide" | null>(null);
  const [deleting, setDeleting] = useState(false);
  const [messaging, setMessaging] = useState<"message" | "warn" | null>(null);
  const [emergencyEditing, setEmergencyEditing] = useState(false);
  const [archiving, setArchiving] = useState(false);
  const router = useRouter();

  useEffect(() => {
    if (state && "ok" in state) toast("Action completed.", "success");
    else if (state && "error" in state) toast(String(state.error), "error");
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [state]);

  async function confirmDelete() {
    const res = await deleteDraftEventAction(e.representing_org_id, e.event_id);
    if (res && "error" in res) toast(String(res.error), "error");
    else toast("Draft deleted.", "success");
  }

  const canDelete = e.status === "draft";
  // Archive is terminal for the event's presence on every listing, so it joins the actions that ask
  // first. It reached the server on a single unconfirmed click while Delete — which is refused
  // outright once anyone has registered, and so is the LESS reachable of the two — had a dialog.
  const canArchive = e.status === "draft" || e.status === "closed"
    || e.status === "cancelled" || e.status === "completed";

  /* Menu items are plain callbacks, so the form-backed actions dispatch the same FormData their own
     <form> would have posted — identical server action, identical audit trail, no second code path
     that could drift from the buttons. */
  function dispatch(value: string) {
    const fd = new FormData();
    fd.set("action", value);
    action(fd);
  }

  const moreItems: MenuItem[] = [
    {
      label: e.is_featured ? "Unfeature" : "Feature",
      icon: <Rocket size={14} />,
      onSelect: () => dispatch(e.is_featured ? "unfeature" : "feature"),
    },
    ...(e.is_suspended ? [] : [{
      label: "Suspend", icon: <ShieldAlert size={14} />,
      onSelect: () => setReasonDialog("suspend"),
    }]),
    ...(e.is_hidden ? [] : [{
      label: "Hide", icon: <EyeOff size={14} />, onSelect: () => setReasonDialog("hide"),
    }]),
    ...(canArchive ? [{
      label: "Archive", icon: <Archive size={14} />, onSelect: () => setArchiving(true),
    }] : []),
    { label: "Issue warning", icon: <AlertTriangle size={14} />, destructive: true, onSelect: () => setMessaging("warn") },
    { label: "Audit log", icon: <ScrollText size={14} />, onSelect: () => router.push("/audit?entity=events") },
    { label: "Reports", icon: <FileText size={14} />, onSelect: () => router.push("/reports") },
    { label: "Org tools", icon: <WalletIcon size={14} />, onSelect: () => router.push("/organizations") },
    ...(isSuperAdmin ? [{
      label: "Emergency edit", icon: <Pencil size={14} />, destructive: true,
      onSelect: () => setEmergencyEditing(true),
    }] : []),
    ...(isSuperAdmin && canDelete ? [{
      label: "Delete draft", icon: <Trash2 size={14} />, destructive: true,
      onSelect: () => setDeleting(true),
    }] : []),
  ];

  return (
    <>
      <div className="flex flex-wrap items-center gap-1.5 rounded-lg border border-border bg-surface p-2.5">
        {/*
          D-381 — the review decision, delegated to the one component that models it.

          This used to render a button labelled "Approve" whose value was `publish`, so a reviewer who
          approved an event MADE IT PUBLIC in the same click. D-377 is explicit that the two are separate
          acts — approval grants the permission, the CREATOR decides when to go live — and D-379 keeps
          the whole review as one decision over the event and its authorization together.

          `ReviewActions` already encodes the real state machine (pendingreview → claim_review;
          underreview → approve_review / request_changes / reject_review / release_review) with reason
          codes and notes. A second spelling beside it is how the two drift, and this one had already
          drifted into publishing.
        */}
        {REVIEW_QUEUE_STATES.has(e.status) && (
          <ReviewActions orgId={e.representing_org_id} eventId={e.event_id} status={e.status} />
        )}
        {/*
          D-381 — one primary decision, everything else behind `More`.

          Thirteen equally-weighted controls shared one `flex-wrap`, so Approve sat between Unfeature
          and Archive at the same size and the bar re-flowed to three rows on a laptop. The ordering
          here is frequency: a reviewer opens this sheet to DECIDE, occasionally to reach the
          organizer, and almost never to unfeature. Nothing was removed — every action below is still
          reachable and still behind the confirmation it always had.

          Restore actions stay in the primary row because they are contextual, not routine: the button
          only exists while the event is actually suspended or hidden, and that state is the one thing
          an admin opening the sheet needs to be able to undo without hunting for it.
        */}
        {e.is_suspended && (
          <ConfirmSubmit form={action} name="action" value="unsuspend" label="Unsuspend" icon={ShieldCheck} tone="ok" />
        )}
        {e.is_hidden && (
          <ConfirmSubmit form={action} name="action" value="unhide" label="Unhide" icon={Eye} tone="ok" />
        )}
        <Button variant="ghost" onClick={() => setMessaging("message")}>
          <MessageSquare size={13} /> Message organizer
        </Button>
        {/* Absolute, against the PUBLIC site: the event page is served by web, not by this console, so a
            relative /e/{slug} resolves to the console's own origin and 404s — leaving a reviewer asked to
            judge an event they cannot open. */}
        <a href={`${WEB_URL}/e/${e.slug}`} target="_blank" rel="noopener noreferrer" className="inline-flex h-8 items-center gap-1.5 rounded-md px-2.5 text-xs font-semibold text-muted hover:bg-elevated hover:text-text">
          <Eye size={13} /> View public event
        </a>
        <Menu
          className="ml-auto"
          trigger={(props) => (
            <Button variant="ghost" {...props}>
              <MoreHorizontal size={13} /> More
            </Button>
          )}
          items={moreItems}
        />
      </div>

      {reasonDialog ? (
        <ReasonDialog orgId={e.representing_org_id} eventId={e.event_id} action={reasonDialog} onClose={() => setReasonDialog(null)} />
      ) : null}

      <ConfirmDialog
        open={deleting}
        onClose={() => setDeleting(false)}
        onConfirm={confirmDelete}
        title="Delete this draft?"
        /* This said "Soft delete … not purged" while calling a HARD delete that cascaded through 46
           tables — the D-025 design described on a button doing the opposite of it. D-364 made the code
           match: the row is now retained and hidden. The wording still leads with what the admin needs
           to know, because "soft" reads as reversible and there is no restore. */
        description="The event disappears from every surface — its schedule, ticket types, media and registration form go with it — and there is no way to restore it from here. Only available while the event is still Draft, and refused outright once anyone has registered."
        confirmLabel="Delete"
        tone="danger"
      />

      <ConfirmDialog
        open={archiving}
        onClose={() => setArchiving(false)}
        onConfirm={() => dispatch("archive")}
        title="Archive this event?"
        description="The event leaves every listing, search result and queue, and its organizer can no longer act on it. Its orders, tickets and ledger are kept intact."
        confirmLabel="Archive"
        tone="danger"
      />

      {messaging ? <MessageDialog eventId={e.event_id} kind={messaging} onClose={() => setMessaging(null)} /> : null}

      {emergencyEditing ? <EmergencyEditDialog event={e} onClose={() => setEmergencyEditing(false)} /> : null}
    </>
  );
}

// D-191: Super Admin only, mandatory reason, danger-toned so it reads as exceptional — never presented
// alongside Approve/Suspend/Hide as if it were routine moderation. Posts only the fields actually edited
// here (Title/Subtitle/Description) through the same UpdateEventInput shape the organizer PATCH uses —
// the backend applies it via the identical update core, this dialog is just a narrower field set.
function EmergencyEditDialog({ event: e, onClose }: { event: AdminEvent; onClose: () => void }) {
  const [reason, setReason] = useState("");
  const [title, setTitle] = useState(e.title);
  const [pending, setPending] = useState(false);
  const toast = useToast();

  async function confirm() {
    if (!reason.trim()) { toast("A reason is required.", "error"); return; }
    setPending(true);
    try {
      const res = await emergencyEditEventAction(e.event_id, reason, { title });
      if (res && "error" in res) toast(String(res.error), "error");
      else { toast("Emergency edit applied.", "success"); onClose(); }
    } finally {
      setPending(false);
    }
  }

  return (
    <Dialog open onClose={onClose} title="Emergency edit — Super Admin only">
      <div className="space-y-3">
        <p className="text-sm text-danger">
          This directly changes the organizer&apos;s event content, outside normal moderation. Use only for
          compromised accounts, legal/copyright removal, or critical corrections. Fully audited with a
          before/after snapshot, visible in this event&apos;s Timeline.
        </p>
        <Field label="Reason" htmlFor="ee-reason" helper="Required — recorded in the audit trail">
          <Textarea id="ee-reason" value={reason} onChange={(ev) => setReason(ev.target.value)} rows={3} required />
        </Field>
        <Field label="Title" htmlFor="ee-title">
          <Input id="ee-title" value={title} onChange={(ev) => setTitle(ev.target.value)} />
        </Field>
        <div className="flex justify-end gap-2 pt-1">
          <Button type="button" variant="ghost" onClick={onClose} disabled={pending}>Cancel</Button>
          <Button type="button" onClick={confirm} disabled={pending || !reason.trim()}>
            {pending ? "Applying…" : "Apply emergency edit"}
          </Button>
        </div>
      </div>
    </Dialog>
  );
}

/** Suspend/hide — a real form (hidden `action` field + reason textarea + its own submit button),
 *  same self-closing-on-success shape as CategoryDialog elsewhere in this console. Not a ConfirmDialog:
 *  these two take a reason, which ConfirmDialog's plain yes/no footer can't capture. Reason is required
 *  (D-193) — the backend now rejects an empty one; `required` here just avoids the round trip. */
function ReasonDialog({ orgId, eventId, action: kind, onClose }: { orgId: string; eventId: string; action: "suspend" | "hide"; onClose: () => void }) {
  const [state, action] = useFormState(manageEventAction.bind(null, orgId, eventId), null);
  const toast = useToast();
  const succeeded = Boolean(state && "ok" in state);

  useEffect(() => {
    if (succeeded) { toast(`Event ${kind}d.`, "success"); onClose(); }
    else if (state && "error" in state) toast(String(state.error), "error");
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [state, succeeded]);

  return (
    <Dialog open onClose={onClose} title={kind === "suspend" ? "Suspend this event?" : "Hide this event?"}>
      <form action={action} className="space-y-3">
        <input type="hidden" name="action" value={kind} />
        <p className="text-sm text-muted">
          This overrides public visibility immediately, independent of the event&apos;s lifecycle status. The
          organizer&apos;s own event still shows as {kind === "suspend" ? "suspended" : "hidden"} with this reason.
        </p>
        <Field label="Reason" htmlFor="reason-text" helper="Required — shown to the organizer and recorded in the audit trail">
          <Textarea id="reason-text" name="reason" rows={3} required />
        </Field>
        <div className="flex justify-end gap-2 pt-1">
          <Button type="button" variant="ghost" onClick={onClose}>Cancel</Button>
          <SubmitBtn label={kind === "suspend" ? "Suspend" : "Hide"} pendingLabel="Working…" />
        </div>
      </form>
    </Dialog>
  );
}

function ConfirmSubmit({ form, name, value, label, icon: Icon, tone }: {
  form: (payload: FormData) => void; name: string; value: string; label: string; icon: typeof CheckCircle2;
  tone: "ok" | "warn" | "accent" | "neutral";
}) {
  // Each instance renders its own <form>, so useFormStatus below correctly reflects only ITS OWN
  // pending state (not the shared useFormState `state` value) — disables the button against a
  // double-click without needing per-button local state.
  return (
    <form action={form}>
      <input type="hidden" name={name} value={value} />
      <ConfirmSubmitButton label={label} icon={Icon} tone={tone} />
    </form>
  );
}

function ConfirmSubmitButton({ label, icon: Icon, tone }: { label: string; icon: typeof CheckCircle2; tone: "ok" | "warn" | "accent" | "neutral" }) {
  const { pending } = useFormStatus();
  const cls = tone === "ok" ? "text-success hover:bg-success/10" : tone === "warn" ? "text-danger hover:bg-danger/10"
    : tone === "accent" ? "text-accent-text hover:bg-accent/10" : "text-muted hover:bg-elevated hover:text-text";
  return (
    <button type="submit" disabled={pending} className={`inline-flex h-8 items-center gap-1.5 rounded-md px-2.5 text-xs font-semibold disabled:opacity-50 ${cls}`}>
      <Icon size={13} /> {pending ? "…" : label}
    </button>
  );
}

function MessageDialog({ eventId, kind, onClose }: { eventId: string; kind: "message" | "warn"; onClose: () => void }) {
  const boundAction = kind === "warn" ? warnOrganizerAction.bind(null, eventId) : messageOrganizerAction.bind(null, eventId);
  const [state, action] = useFormState(boundAction, null);
  const toast = useToast();
  const succeeded = Boolean(state && "ok" in state);

  useEffect(() => {
    if (succeeded) { toast(kind === "warn" ? "Warning sent." : "Message sent.", "success"); onClose(); }
    else if (state && "error" in state) toast(String(state.error), "error");
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [state, succeeded]);

  return (
    <Dialog open onClose={onClose} title={kind === "warn" ? "Issue a warning to the organizer" : "Message the organizer"}>
      <form action={action} className="space-y-3">
        <Field label="Message" htmlFor="org-msg" helper="Delivered as an in-app notification to the event's creator.">
          {/* eslint-disable-next-line jsx-a11y/no-autofocus -- opens only on an explicit button click */}
          <Textarea id="org-msg" name="message" required rows={4} autoFocus
            placeholder={kind === "warn" ? "Explain the policy violation…" : "Write a message…"} />
        </Field>
        <div className="flex justify-end gap-2 pt-1">
          <Button type="button" variant="ghost" onClick={onClose}>Cancel</Button>
          <SubmitBtn label={kind === "warn" ? "Send warning" : "Send message"} pendingLabel="Sending…" />
        </div>
      </form>
    </Dialog>
  );
}

// ── Tab 2: Registrations ─────────────────────────────────────────────────
function RegistrationsTab({ event: e, data, live }: { event: AdminEvent; data: WorkspaceData; live: { sold: number; checkedIn: number } | null }) {
  const registered = live?.sold ?? e.registrations_count;
  const checkedIn = live?.checkedIn ?? e.checked_in;
  const remaining = e.capacity != null ? Math.max(0, e.capacity - registered) : null;
  const attendanceRate = registered > 0 ? Math.round((checkedIn / registered) * 100) : 0;

  return (
    <div className="space-y-5">
      <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
        <Stat label="Total registrations" value={registered} />
        <Stat label="Checked in" value={checkedIn} />
        <Stat label="Not yet checked in" value={Math.max(0, registered - checkedIn)} />
        <Stat label="Attendance %" value={`${attendanceRate}%`} />
        <Stat label="Capacity" value={e.capacity ?? "Unlimited"} />
        <Stat label="Remaining capacity" value={remaining ?? "—"} />
        <Stat label="Orders paid" value={data.analytics?.orders_paid ?? "—"} />
        <Stat label="Ticket types" value={data.analytics?.ticket_types ?? "—"} />
      </div>
      <Gap>
        Approved/Pending/Waitlist as distinct counts aren&apos;t modeled by the current Order/Ticket schema
        (Order status is Pending/Paid/Failed/Refunded; Ticket state is Issued/CheckedIn/Void) — the figures
        above are the real, authoritative ones this backend can compute. A day-by-day registration graph
        would need a new time-series read this pass didn&apos;t add.
      </Gap>
    </div>
  );
}

// ── Tab 3: Tickets ────────────────────────────────────────────────────────
function TicketsTab({ data }: { data: WorkspaceData }) {
  const columns: Column<TicketTypeAdmin>[] = [
    { key: "name", header: "Ticket type" },
    { key: "price_paise", header: "Price", align: "right", render: (t) => money(t.price_paise) },
    { key: "quantity", header: "Inventory", align: "right" },
    { key: "sold", header: "Sold", align: "right" },
    { key: "available", header: "Remaining", align: "right", render: (t) => <Badge tone={t.available === 0 ? "danger" : "success"}>{t.available}</Badge> },
    { key: "revenue", header: "Est. revenue", align: "right", render: (t) => <span className="text-text">{money(t.sold * t.price_paise)}</span> }
  ];
  return (
    <div className="space-y-3">
      <DataTable
        columns={columns}
        data={data.ticketTypes}
        keyField={(t) => t.id}
        caption="Ticket types"
        emptyState={<EmptyState icon={<Award size={20} />} title="No ticket types" message="This event has no ticket types yet." />}
      />
      <Gap>Est. revenue = sold × price; the ledger (Finance tab) is the authoritative money record, this is a quick estimate per type.</Gap>
    </div>
  );
}

// ── Tab 4: Attendees ─────────────────────────────────────────────────────
function AttendeesTab({ data }: { data: WorkspaceData }) {
  const [q, setQ] = useState("");
  const filtered = data.attendees.items.filter((a) =>
    !q || a.buyer_name.toLowerCase().includes(q.toLowerCase()) || a.buyer_phone.includes(q) || a.ticket_type_name.toLowerCase().includes(q.toLowerCase()));

  const columns: Column<typeof filtered[number]>[] = [
    { key: "buyer_name", header: "Name" },
    { key: "buyer_phone", header: "Phone" },
    { key: "ticket_type_name", header: "Ticket type" },
    { key: "code", header: "QR code", render: (a) => <span className="font-mono text-xs text-muted">{a.code.slice(0, 10)}…</span> },
    { key: "state", header: "Checked in", render: (a) => a.state === "CheckedIn" ? <Badge tone="success">checked in</Badge> : <Badge tone="muted">{a.state.toLowerCase()}</Badge> },
    { key: "checked_in_at", header: "Check-in time", render: (a) => <span className="text-xs text-muted">{dt(a.checked_in_at)}</span> }
  ];

  return (
    <div className="space-y-3">
      <Field label="Search attendees" htmlFor="att-q">
        <Input id="att-q" value={q} onChange={(e) => setQ(e.target.value)} placeholder="Name, phone, ticket type" />
      </Field>
      <DataTable
        columns={columns}
        data={filtered}
        keyField={(a) => a.ticket_id}
        caption="Attendees"
        emptyState={<EmptyState icon={<Users size={20} />} title="No attendees" message={q ? "No attendees match your search." : "No one has registered yet."} />}
      />
      <Gap>
        Email, payment status, certificate status, and a profile link aren&apos;t on the attendee contract this
        pass reuses (<code className="rounded bg-elevated px-1">GET /v1/orgs/{"{orgId}"}/events/{"{eventId}"}/attendees</code>) —
        showing what it actually returns rather than inventing the rest.
      </Gap>
    </div>
  );
}

// ── Tab 5: Finance ───────────────────────────────────────────────────────
function FinanceTab({ event: e, data }: { event: AdminEvent; data: WorkspaceData }) {
  const refunded = data.ledger.filter((l) => l.state === "Refunded").reduce((s, l) => s + l.amount_paise, 0);
  const columns: Column<LedgerEntryAdmin>[] = [
    { key: "created_at", header: "Date", render: (l) => <span className="text-xs text-muted">{dt(l.created_at)}</span> },
    { key: "ref_type", header: "Type" },
    { key: "state", header: "State", render: (l) => <Badge tone={l.state === "Refunded" ? "danger" : "neutral"}>{l.state}</Badge> },
    { key: "amount_paise", header: "Amount", align: "right", render: (l) => money(l.amount_paise, l.currency) }
  ];
  return (
    <div className="space-y-5">
      <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
        <Stat label="Gross revenue" value={money(data.analytics?.gross_paise ?? e.revenue_paise, e.currency)} />
        <Stat label="Refunds (this event)" value={money(refunded, e.currency)} />
        <Stat label="Org wallet — available" value={data.wallet ? money(data.wallet.available_paise, data.wallet.currency) : "—"} />
        <Stat label="Org wallet — pending payout" value={data.wallet ? money(data.wallet.reserved_paise + data.wallet.advanced_paise, data.wallet.currency) : "—"} />
      </div>
      <div className="rounded-lg border border-border">
        <Row label="Org wallet — collected" value={data.wallet ? money(data.wallet.collected_paise, data.wallet.currency) : "—"} />
        <Row label="Org wallet — settled (completed payouts)" value={data.wallet ? money(data.wallet.settled_paise, data.wallet.currency) : "—"} />
        <Row label="Org wallet — lifetime earned" value={data.wallet ? money(data.wallet.lifetime_earned_paise, data.wallet.currency) : "—"} />
        <Row label="Org wallet — lifetime withdrawn" value={data.wallet ? money(data.wallet.lifetime_withdrawn_paise, data.wallet.currency) : "—"} />
      </div>
      <DataTable
        columns={columns}
        data={data.ledger}
        keyField={(l) => l.id}
        caption="Ledger entries for this event"
        emptyState={<EmptyState icon={<WalletIcon size={20} />} title="No ledger entries" message="No money has moved for this event yet." />}
      />
      <Gap>
        Net revenue and a platform-fee figure aren&apos;t modeled anywhere in this backend (no fee percentage or
        split field exists yet) — shown honestly as gross only. Wallet figures are org-level (the ledger doesn&apos;t
        carry a per-event payout split), filtered to this event only where the ledger itself is event-scoped.
      </Gap>
    </div>
  );
}

// ── Tab 6: Organizer ─────────────────────────────────────────────────────
function OrganizerTab({ event: e, data }: { event: AdminEvent; data: WorkspaceData }) {
  const byStatus = (statuses: string[]) => data.orgEvents.filter((o) => statuses.includes(o.status.toLowerCase()));
  const totalRevenue = data.orgEvents.reduce((s, o) => s + o.revenue_paise, 0);
  const concluded = byStatus(["completed", "closed", "cancelled"]);
  const successRate = concluded.length > 0 ? Math.round((byStatus(["completed", "closed"]).length / concluded.length) * 100) : null;

  return (
    <div className="space-y-5">
      <div className="rounded-lg border border-border">
        <Row label="Organization" value={e.org_name} />
        <Row label="Verification" value={<Badge tone={e.org_verification === "verified" ? "success" : "muted"}>{e.org_verification}</Badge>} />
        <Row label="Risk score" value={data.orgRiskScore} />
        <Row label="Total events" value={data.orgEvents.length} />
        <Row label="Total revenue (all events)" value={money(totalRevenue)} />
        <Row label="Success rate" value={successRate !== null ? `${successRate}%` : "—"} />
      </div>

      <div className="grid gap-3 sm:grid-cols-2">
        <div>
          <p className="mb-2 text-xs font-semibold uppercase tracking-wide text-muted">Upcoming</p>
          <EventMiniList events={byStatus(["published", "scheduled", "live"])} />
        </div>
        <div>
          <p className="mb-2 text-xs font-semibold uppercase tracking-wide text-muted">Completed</p>
          <EventMiniList events={byStatus(["completed", "closed"])} />
        </div>
        <div>
          <p className="mb-2 text-xs font-semibold uppercase tracking-wide text-muted">Cancelled</p>
          <EventMiniList events={byStatus(["cancelled"])} />
        </div>
        <div>
          <p className="mb-2 text-xs font-semibold uppercase tracking-wide text-muted">Draft / In review</p>
          <EventMiniList events={byStatus(["draft", "pendingreview", "underreview", "changesrequested", "approved", "rejected"])} />
        </div>
      </div>

      <Gap>
        Profile (bio/avatar/phone/email), followers/reviews, devices, and recent logins aren&apos;t available for
        another user from any admin-scoped endpoint today — every device/session endpoint in this backend
        resolves the caller&apos;s own token only, with no <code className="rounded bg-elevated px-1">{"{userId}"}</code>{" "}
        route. Exposing that is a real, separate feature (new endpoints, its own security review), not
        something this pass fabricates.
      </Gap>
    </div>
  );
}

function EventMiniList({ events }: { events: OrgEventRow[] }) {
  if (events.length === 0) return <p className="text-xs text-muted">None.</p>;
  return (
    <ul className="space-y-1">
      {events.slice(0, 6).map((o) => (
        <li key={o.id} className="flex items-center justify-between gap-2 text-xs">
          <span className="truncate text-text">{o.title}</span>
          <span className="shrink-0 text-muted">{new Date(o.starts_at).toLocaleDateString("en-IN")}</span>
        </li>
      ))}
    </ul>
  );
}

// ── Tab 7: Moderation ────────────────────────────────────────────────────
function ModerationTab({ event: e, data }: { event: AdminEvent; data: WorkspaceData }) {
  const warnings = data.timeline.filter((a) => a.action === "admin.event.warning");
  const suspensions = data.timeline.filter((a) => a.action === "admin.event.suspend" || a.action === "admin.event.hide");

  return (
    <div className="space-y-5">
      <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
        <Stat label="Reports" value={data.moderation.length} />
        <Stat label="Fraud/risk score" value={data.eventRiskScore} />
        <Stat label="Warnings issued" value={warnings.length} />
        <Stat label="Moderation actions" value={suspensions.length} />
      </div>

      <div>
        <p className="mb-2 text-xs font-semibold uppercase tracking-wide text-muted">Reports &amp; complaints</p>
        {data.moderation.length === 0 ? <p className="text-xs text-muted">No reports filed against this event.</p> : (
          <ul className="space-y-2">
            {data.moderation.map((r) => (
              <li key={r.id} className="rounded-md border border-border p-2.5 text-sm">
                <div className="flex items-center justify-between gap-2">
                  <Badge tone={r.status === "open" ? "danger" : "muted"}>{r.status}</Badge>
                  <span className="text-xs text-muted">{dt(r.created_at)}</span>
                </div>
                <p className="mt-1 text-text">{r.reason}</p>
                {r.details ? <p className="mt-0.5 text-xs text-muted">{r.details}</p> : null}
              </li>
            ))}
          </ul>
        )}
      </div>

      <div>
        <p className="mb-2 text-xs font-semibold uppercase tracking-wide text-muted">Warnings issued to this organizer</p>
        {warnings.length === 0 ? <p className="text-xs text-muted">None.</p> : (
          <ul className="space-y-1">
            {warnings.map((w) => <li key={w.id} className="text-xs text-muted">{dt(w.created_at)} — {w.details ?? "Warning issued"}</li>)}
          </ul>
        )}
      </div>
      <Gap>
        A distinct &quot;admin notes&quot; feature (free-text notes not tied to a message/warning) isn&apos;t built —
        the message/warning history above (via the audit log) is the closest existing equivalent.
      </Gap>
    </div>
  );
}

// ── Tab 8: Media ─────────────────────────────────────────────────────────
function MediaTab({ data }: { data: WorkspaceData }) {
  const byKind = (kind: string) => data.media.filter((m) => m.kind.toLowerCase() === kind);
  const groups: { label: string; kind: string }[] = [
    { label: "Gallery", kind: "gallery" }, { label: "Documents", kind: "document" },
    { label: "Posters", kind: "poster" }, { label: "Brochures", kind: "brochure" }
  ];
  return (
    <div className="space-y-5">
      {data.media.length === 0 ? (
        <EmptyState icon={<ImageIcon size={20} />} title="No media" message="This event has no gallery images, documents, or attachments." />
      ) : (
        groups.map((g) => {
          const items = byKind(g.kind);
          if (items.length === 0) return null;
          return (
            <div key={g.kind}>
              <p className="mb-2 text-xs font-semibold uppercase tracking-wide text-muted">{g.label} ({items.length})</p>
              <ul className="space-y-1">
                {items.map((m) => (
                  <li key={m.id} className="flex items-center gap-2 text-xs text-muted">
                    <FileText size={13} /> <span className="truncate">{m.caption || m.key}</span>
                  </li>
                ))}
              </ul>
            </div>
          );
        })
      )}
      <Gap>
        Storage keys aren&apos;t rendered as images/downloads here (that needs a signed view URL per file, the
        same constraint speaker/sponsor photos already have elsewhere in this console) — shown as a list of
        what&apos;s attached, not a broken image.
      </Gap>
    </div>
  );
}

// ── Tab 9: Timeline ──────────────────────────────────────────────────────
function TimelineTab({ data }: { data: WorkspaceData }) {
  if (data.timeline.length === 0) return <EmptyState icon={<ScrollText size={20} />} title="No audit history" message="No recorded actions for this event yet." />;
  return (
    <div className="space-y-5">
      <ol className="space-y-3 border-l border-border pl-4">
        {[...data.timeline].reverse().map((a) => (
          <li key={a.id} className="relative">
            <span className="absolute -left-[21px] top-1 h-2.5 w-2.5 rounded-full border-2 border-background bg-accent" />
            <p className="text-sm text-text">{AUDIT_LABELS[a.action] ?? a.action}</p>
            <p className="text-xs text-muted">{dt(a.created_at)}</p>
          </li>
        ))}
      </ol>
      <Gap>
        Built entirely from the existing audit log (<code className="rounded bg-elevated px-1">GET /v1/admin/audit</code>) —
        every admin action already writes here (D-102); this tab is a read, not a new write path.
      </Gap>
    </div>
  );
}
